using Microsoft.Extensions.Logging;
using ktl.be.applications.Base;
using ktl.be.applications.Document.Dtos.DocumentMerge;
using ktl.be.applications.Document.Dtos.Stamp;
using ktl.be.applications.Document.Interfaces;
using ktl.be.infrastructure.external.Documents.Dtos;
using ktl.be.infrastructure.external.Documents.Interfaces;
using ktl.be.shared.Constants;
using ktl.be.shared.Constants.Documents;
using ktl.be.shared.Requests;
using ktl.be.shared.Requests.AppException;
using ktl.be.shared.Requests.ErrorRequest;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace ktl.be.applications.Document.Implements
{
    public class DocumentMergeService : BaseService, IDocumentMergeService
    {
        private readonly IExcelReader _excelReader;
        private readonly IWordTemplateEngine _templateEngine;
        private readonly IWordPdfConverter _pdfConverter;
        private readonly IPdfImageStamper _pdfImageStamper;
        private readonly IFileUploadService _fileUploadService;
        private readonly IStampPlanner _stampPlanner;

        private const string TargetTypeManifest = "manifest";
        private const string TargetTypeFile = "file";
        private const string TargetTypeSummary = "summary";

        private static readonly Regex WordRegex = new(@"[\p{L}\p{N}]+", RegexOptions.Compiled);
        private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

        private static string PreviewFolder => Path.Combine(Path.GetTempPath(), AppConstants.AppFolderName,
            DocumentMergeConstants.TempFolderName, DocumentMergeConstants.PreviewFolderName);

        public DocumentMergeService(ILogger<BaseService> logger,
            IExcelReader excelReader, IWordTemplateEngine templateEngine, IWordPdfConverter pdfConverter,
            IPdfImageStamper pdfImageStamper, IFileUploadService fileUploadService, IStampPlanner stampPlanner)
            : base(logger)
        {
            _excelReader = excelReader;
            _templateEngine = templateEngine;
            _pdfConverter = pdfConverter;
            _pdfImageStamper = pdfImageStamper;
            _fileUploadService = fileUploadService;
            _stampPlanner = stampPlanner;
        }

        public List<ExcelHeaderDto> GetHeaders(string dataPath)
        {
            _logger.LogInformation($"{nameof(GetHeaders)}, dataPath={dataPath}");

            return ReadData(dataPath).Headers;
        }

        public MergeMappingDto AutoMap(string templatePath, string dataPath)
        {
            _logger.LogInformation($"{nameof(AutoMap)}, templatePath={templatePath}, dataPath={dataPath}");

            var fields = _templateEngine.GetFieldNames(ReadTemplate(templatePath));
            var data = ReadData(dataPath);
            var headerKeys = data.Headers
                .Select(h => (Header: h, Key: NormalizeWords(h.Name)))
                .Where(h => h.Key.Length > 0)
                .ToList();

            return new MergeMappingDto
            {
                TemplatePath = templatePath,
                DataPath = dataPath,
                TotalRows = data.Rows.Count,
                Headers = data.Headers,
                Fields = fields.Select(field =>
                {
                    var key = NormalizeWords(field);
                    var header = headerKeys.FirstOrDefault(h => h.Key == key).Header;
                    return new MergeFieldMappingDto
                    {
                        FieldName = field,
                        ColumnNumber = header?.ColumnNumber,
                        HeaderName = header?.Name,
                        AutoMapped = header != null
                    };
                }).ToList()
            };
        }

        public MergeValidationDto Validate(MergeRequestDto request)
        {
            _logger.LogInformation($"{nameof(Validate)}, templatePath={request.TemplatePath}, dataPath={request.DataPath}");

            var context = BuildContext(request);
            var unmapped = context.Mappings.Where(m => m.ColumnNumber == null).Select(m => m.FieldName).ToList();

            var result = new MergeValidationDto
            {
                TotalRows = context.Data.Rows.Count,
                MappedFieldCount = context.Mappings.Count - unmapped.Count,
                UnmappedFields = unmapped,
                Rows = context.Data.Rows.Select(row => new MergeRowCheckDto
                {
                    RowNumber = row.RowNumber,
                    FileName = context.FileNames[row.RowNumber] + DocumentMergeConstants.PdfExtension,
                    Warnings = BuildRowWarnings(context, row)
                }).ToList()
            };

            if (unmapped.Count > 0)
            {
                result.Warnings.Add($"Có {unmapped.Count} trường chưa map cột Excel, sẽ để trống khi sinh file: " +
                    string.Join(", ", unmapped) + ".");
            }
            if (request.FileNameColumnNumber == null)
            {
                result.Warnings.Add("Chưa chọn cột đặt tên file, tên file sẽ dùng số dòng Excel.");
            }
            result.WarningRowCount = result.Rows.Count(r => r.Warnings.Count > 0);

            return result;
        }

        public MergePreviewDto Preview(MergePreviewRequestDto request)
        {
            _logger.LogInformation($"{nameof(Preview)}, templatePath={request.TemplatePath}, dataPath={request.DataPath}, " +
                $"rowNumber={request.RowNumber}");

            var context = BuildContext(request);
            var row = request.RowNumber is int rowNumber
                ? context.Data.Rows.FirstOrDefault(r => r.RowNumber == rowNumber)
                    ?? throw new UserFriendlyException(ErrorCodes.MergeDataInvalid,
                        $"File Excel không có dòng dữ liệu số {rowNumber}.")
                : context.Data.Rows[0];

            Directory.CreateDirectory(PreviewFolder);
            CleanupPreviews();

            var previewName = Guid.NewGuid().ToString("N");
            var docxPath = Path.Combine(PreviewFolder, previewName + DocumentMergeConstants.TemplateExtension);
            var pdfPath = Path.Combine(PreviewFolder, previewName + DocumentMergeConstants.PdfExtension);

            RenderedPdf rendered;
            try
            {
                using var session = _pdfConverter.OpenSession();
                rendered = RenderPdf(session, context, row, docxPath, pdfPath);
            }
            catch (Exception ex)
            {
                try { if (File.Exists(pdfPath)) File.Delete(pdfPath); } catch { }
                if (ex is BaseException) throw;
                throw new UserFriendlyException(ErrorCodes.MergeFailed, $"Không tạo được bản xem trước: {ex.Message}");
            }
            finally
            {
                try { if (File.Exists(docxPath)) File.Delete(docxPath); } catch { }
            }

            return new MergePreviewDto
            {
                RowNumber = row.RowNumber,
                FileName = context.FileNames[row.RowNumber] + DocumentMergeConstants.PdfExtension,
                FilePath = pdfPath,
                PageCount = rendered.PageCount,
                Warnings = rendered.Warnings
            };
        }

        public Stream OpenPreview(string filePath)
        {
            _logger.LogInformation($"{nameof(OpenPreview)}, filePath={filePath}");

            var fullPath = string.IsNullOrWhiteSpace(filePath) ? string.Empty : Path.GetFullPath(filePath);
            var folder = Path.GetFullPath(PreviewFolder) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(folder, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Path.GetExtension(fullPath), DocumentMergeConstants.PdfExtension,
                    StringComparison.OrdinalIgnoreCase)
                || !File.Exists(fullPath))
            {
                throw new UserFriendlyException(ErrorCodes.NotFound, "Không tìm thấy file xem trước.");
            }

            return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public (Stream Stream, string ContentType) OpenImage(string path)
        {
            _logger.LogInformation($"{nameof(OpenImage)}, path={path}");

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                throw new UserFriendlyException(ErrorCodes.StampImageInvalid, $"Không tìm thấy file ảnh: {path}");
            }

            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (!StampImageConstants.AllowedExtensions.Contains(ext))
            {
                throw new UserFriendlyException(ErrorCodes.StampImageInvalid, $"Định dạng ảnh {ext} không được hỗ trợ.");
            }

            var contentType = ext == ".png" ? "image/png" : "image/jpeg";

            return (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), contentType);
        }

        public IAsyncEnumerable<MergeProgressDto> MergeAsync(MergeRequestDto request,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"{nameof(MergeAsync)}, templatePath={request.TemplatePath}, dataPath={request.DataPath}");

            var context = BuildContext(request);

            if (!_pdfConverter.IsAvailable())
            {
                throw new UserFriendlyException(ErrorCodes.MergeConverterUnavailable,
                    "Máy chưa cài Microsoft Word nên không xuất được PDF.");
            }

            if (string.IsNullOrWhiteSpace(request.OutputFolder) && _fileUploadService.IsUploaded(request.DataPath))
            {
                throw new UserFriendlyException(ErrorCodes.MergeFailed,
                    "File Excel được kéo thả vào ứng dụng nên cần chọn thư mục lưu kết quả.");
            }

            var outputRoot = string.IsNullOrWhiteSpace(request.OutputFolder)
                ? Path.GetDirectoryName(Path.GetFullPath(request.DataPath))!
                : request.OutputFolder;
            if (!Directory.Exists(outputRoot))
            {
                throw new UserFriendlyException(ErrorCodes.MergeFailed, "Không tìm thấy thư mục lưu file kết quả.");
            }

            var outputFolder = Path.Combine(outputRoot,
                $"{context.TemplateName}_{GetVietnamTime().ToString(DocumentMergeConstants.OutputTimeFormat)}");

            return RunMergeAsync(context, outputFolder, cancellationToken);
        }

        private async IAsyncEnumerable<MergeProgressDto> RunMergeAsync(MergeContext context, string outputFolder,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var rows = context.Data.Rows;
            var total = rows.Count;
            var workFolder = Path.Combine(Path.GetTempPath(), AppConstants.AppFolderName,
                DocumentMergeConstants.TempFolderName, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputFolder);
            Directory.CreateDirectory(workFolder);

            yield return new MergeProgressDto
            {
                TargetType = TargetTypeManifest,
                Target = outputFolder,
                Status = StatusCodeE.Success,
                Code = 200,
                Message = $"Chuẩn bị sinh {total} file.",
                Current = 0,
                Total = total,
                Files = rows.Select(r => context.FileNames[r.RowNumber] + DocumentMergeConstants.PdfExtension).ToList()
            };

            IWordPdfSession? session = null;
            string? sessionError = null;
            try
            {
                session = _pdfConverter.OpenSession();
            }
            catch (Exception ex)
            {
                sessionError = GetReason(ex);
                _logger.LogError(ex, $"{nameof(RunMergeAsync)} không mở được Microsoft Word");
            }

            var success = 0;
            var failed = 0;
            try
            {
                for (var i = 0; i < total; i++)
                {
                    var row = rows[i];
                    var fileName = context.FileNames[row.RowNumber] + DocumentMergeConstants.PdfExtension;

                    var fileResult = session == null || cancellationToken.IsCancellationRequested
                        ? new MergeFileResultDto
                        {
                            RowNumber = row.RowNumber,
                            FileName = fileName,
                            Reason = sessionError ?? "Tiến trình sinh file bị dừng."
                        }
                        : await Task.Run(() => ProcessRow(session, context, row, fileName, outputFolder, workFolder),
                            CancellationToken.None);

                    if (fileResult.Success) success++;
                    else failed++;

                    yield return new MergeProgressDto
                    {
                        TargetType = TargetTypeFile,
                        Target = fileName,
                        Status = fileResult.Success ? StatusCodeE.Success : StatusCodeE.Error,
                        Code = fileResult.Success ? 200 : ErrorCodes.MergeFailed,
                        Message = fileResult.Success ? "Đã sinh file" : (fileResult.Reason ?? "Sinh file thất bại"),
                        Current = i + 1,
                        Total = total,
                        File = fileResult
                    };
                }

                yield return new MergeProgressDto
                {
                    TargetType = TargetTypeSummary,
                    Target = outputFolder,
                    Status = failed == 0 ? StatusCodeE.Success : StatusCodeE.Error,
                    Code = failed == 0 ? 200 : ErrorCodes.MergeFailed,
                    Message = $"Sinh file xong: {success} thành công, {failed} lỗi.",
                    Completed = true,
                    Current = total,
                    Total = total,
                    Summary = new MergeResultDto
                    {
                        OutputFolder = outputFolder,
                        TotalFiles = total,
                        SuccessCount = success,
                        FailedCount = failed
                    }
                };
            }
            finally
            {
                session?.Dispose();
                try { Directory.Delete(workFolder, recursive: true); } catch { }
            }
        }

        private MergeFileResultDto ProcessRow(IWordPdfSession session, MergeContext context, ExcelRowDto row,
            string fileName, string outputFolder, string workFolder)
        {
            var result = new MergeFileResultDto { RowNumber = row.RowNumber, FileName = fileName };
            var docxPath = Path.Combine(workFolder, row.RowNumber + DocumentMergeConstants.TemplateExtension);
            var pdfPath = Path.Combine(workFolder, row.RowNumber + DocumentMergeConstants.PdfExtension);

            try
            {
                result.Warnings = RenderPdf(session, context, row, docxPath, pdfPath).Warnings;

                var outputPath = Path.Combine(outputFolder, fileName);
                File.Move(pdfPath, outputPath);

                result.Success = true;
                result.OutputPath = outputPath;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Reason = GetReason(ex);
                _logger.LogError(ex, $"{nameof(ProcessRow)} lỗi ở dòng {row.RowNumber}");
            }
            finally
            {
                try { if (File.Exists(docxPath)) File.Delete(docxPath); } catch { }
                try { if (File.Exists(pdfPath)) File.Delete(pdfPath); } catch { }
            }

            return result;
        }

        private RenderedPdf RenderPdf(IWordPdfSession session, MergeContext context, ExcelRowDto row,
            string docxPath, string pdfPath)
        {
            var values = context.Mappings.ToDictionary(
                m => m.FieldName,
                m => m.ColumnNumber is int column && row.Values.TryGetValue(column, out var value)
                    ? value
                    : string.Empty);

            File.WriteAllBytes(docxPath, _templateEngine.Fill(context.TemplateBytes, values));
            session.Convert(docxPath, pdfPath);

            var pdf = File.ReadAllBytes(pdfPath);
            var result = new RenderedPdf { PageCount = _pdfImageStamper.CountPages(pdf) };

            var plan = _stampPlanner.Plan(context.Stamps, result.PageCount);
            result.Warnings.AddRange(plan.Warnings);
            if (plan.Stamps.Count > 0)
            {
                File.WriteAllBytes(pdfPath, _pdfImageStamper.Stamp(pdf, plan.Stamps));
            }

            return result;
        }

        private static void CleanupPreviews()
        {
            var expiredBefore = DateTime.UtcNow.AddMinutes(-DocumentMergeConstants.PreviewRetentionMinutes);
            foreach (var file in Directory.EnumerateFiles(PreviewFolder))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < expiredBefore) File.Delete(file);
                }
                catch
                {
                }
            }
        }

        private MergeContext BuildContext(MergeRequestDto request)
        {
            var templateBytes = ReadTemplate(request.TemplatePath);
            var fields = _templateEngine.GetFieldNames(templateBytes);
            var data = ReadData(request.DataPath);
            var columns = data.Headers.Select(h => h.ColumnNumber).ToHashSet();

            var requested = new Dictionary<string, int?>(StringComparer.Ordinal);
            foreach (var mapping in request.Mappings ?? new List<MergeFieldMappingDto>())
            {
                var fieldName = (mapping.FieldName ?? string.Empty).Trim();
                if (!fields.Contains(fieldName))
                {
                    throw new UserFriendlyException(ErrorCodes.MergeMappingInvalid,
                        $"Trường \"{fieldName}\" không có trong template.");
                }
                if (!requested.TryAdd(fieldName, mapping.ColumnNumber))
                {
                    throw new UserFriendlyException(ErrorCodes.MergeMappingInvalid,
                        $"Trường \"{fieldName}\" được map nhiều lần.");
                }
                if (mapping.ColumnNumber is int column && !columns.Contains(column))
                {
                    throw new UserFriendlyException(ErrorCodes.MergeMappingInvalid,
                        $"Cột số {column} được map cho trường \"{fieldName}\" không có trong file Excel.");
                }
            }

            if (request.FileNameColumnNumber is int nameColumn && !columns.Contains(nameColumn))
            {
                throw new UserFriendlyException(ErrorCodes.MergeMappingInvalid,
                    $"Cột số {nameColumn} chọn để đặt tên file không có trong file Excel.");
            }

            var context = new MergeContext
            {
                TemplateBytes = templateBytes,
                TemplateName = Path.GetFileNameWithoutExtension(request.TemplatePath),
                Data = data,
                Headers = data.Headers.ToDictionary(h => h.ColumnNumber, h => h.Name),
                FileNameColumnNumber = request.FileNameColumnNumber,
                Mappings = fields.Select(field => new MergeFieldMappingDto
                {
                    FieldName = field,
                    ColumnNumber = requested.TryGetValue(field, out var column) ? column : null
                }).ToList(),
                Stamps = _stampPlanner.BuildRules(request.SealImagePath, request.WetSignatureImagePath, request.Stamps)
            };
            BuildFileNames(context);
            return context;
        }

        private static void BuildFileNames(MergeContext context)
        {
            var identifiers = context.Data.Rows.ToDictionary(
                r => r.RowNumber,
                r => context.FileNameColumnNumber is int column && r.Values.TryGetValue(column, out var value)
                    ? ToFileNamePart(value)
                    : string.Empty);
            var duplicated = identifiers.Values
                .Where(v => v.Length > 0)
                .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in context.Data.Rows)
            {
                var identifier = identifiers[row.RowNumber];
                var name = identifier.Length == 0
                    ? $"{context.TemplateName}_{row.RowNumber}"
                    : duplicated.Contains(identifier)
                        ? $"{context.TemplateName}_{identifier}_{row.RowNumber}"
                        : $"{context.TemplateName}_{identifier}";

                while (!used.Add(name)) name = $"{name}_{row.RowNumber}";

                context.FileNames[row.RowNumber] = name;
                if (context.FileNameColumnNumber == null) continue;

                if (identifier.Length == 0)
                {
                    context.FileNameIssues[row.RowNumber] = "Cột đặt tên file để trống, tên file dùng số dòng Excel.";
                }
                else if (duplicated.Contains(identifier))
                {
                    context.FileNameIssues[row.RowNumber] =
                        $"Giá trị \"{identifier}\" ở cột đặt tên file trùng với dòng khác, tên file thêm số dòng Excel.";
                }
            }
        }

        private static List<string> BuildRowWarnings(MergeContext context, ExcelRowDto row)
        {
            var warnings = context.Mappings
                .Where(m => m.ColumnNumber is int column
                    && (!row.Values.TryGetValue(column, out var value) || string.IsNullOrWhiteSpace(value)))
                .Select(m => $"Trường \"{m.FieldName}\" để trống (cột \"{context.Headers[m.ColumnNumber!.Value]}\").")
                .ToList();

            if (context.FileNameIssues.TryGetValue(row.RowNumber, out var issue)) warnings.Add(issue);
            return warnings;
        }

        private static byte[] ReadTemplate(string templatePath)
        {
            if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath)
                || !string.Equals(Path.GetExtension(templatePath), DocumentMergeConstants.TemplateExtension,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new UserFriendlyException(ErrorCodes.MergeTemplateInvalid,
                    "Không tìm thấy file template Word (.docx).");
            }

            byte[] bytes;
            try
            {
                using var stream = new FileStream(templatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                bytes = memory.ToArray();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new UserFriendlyException(ErrorCodes.MergeTemplateInvalid,
                    $"Không đọc được file template Word: {ex.Message}");
            }

            return bytes;
        }

        private ExcelSheetDto ReadData(string dataPath)
        {
            if (string.IsNullOrWhiteSpace(dataPath) || !File.Exists(dataPath)
                || !string.Equals(Path.GetExtension(dataPath), DocumentMergeConstants.DataExtension,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new UserFriendlyException(ErrorCodes.MergeDataInvalid,
                    "Không tìm thấy file dữ liệu Excel (.xlsx).");
            }

            var data = _excelReader.Read(dataPath);
            if (data.Rows.Count == 0)
            {
                throw new UserFriendlyException(ErrorCodes.MergeDataInvalid, "File Excel không có dòng dữ liệu nào.");
            }
            return data;
        }

        private static string NormalizeWords(string value)
        {
            var decomposed = value.Replace('_', ' ').Replace('đ', 'd').Replace('Đ', 'D')
                .Normalize(NormalizationForm.FormD);
            var stripped = new string(decomposed
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray());

            return string.Join(' ', WordRegex.Matches(stripped.ToLowerInvariant()).Select(m => m.Value));
        }

        private static string ToFileNamePart(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            cleaned = WhitespaceRegex.Replace(cleaned, "_").Trim('_', '.');
            return cleaned.Length > DocumentMergeConstants.FileNameMaxLength
                ? cleaned[..DocumentMergeConstants.FileNameMaxLength]
                : cleaned;
        }

        private static string GetReason(Exception ex)
        {
            return ex is BaseException be
                ? be.MessageLocalize ?? ErrorMessages.GetMessage(be.ErrorCode)
                : ex.Message;
        }

        private sealed class MergeContext
        {
            public byte[] TemplateBytes { get; init; } = Array.Empty<byte>();

            public string TemplateName { get; init; } = string.Empty;

            public ExcelSheetDto Data { get; init; } = new();

            public Dictionary<int, string> Headers { get; init; } = new();

            public int? FileNameColumnNumber { get; init; }

            public List<MergeFieldMappingDto> Mappings { get; init; } = new();

            public Dictionary<int, string> FileNames { get; } = new();

            public Dictionary<int, string> FileNameIssues { get; } = new();

            public List<StampRuleDto> Stamps { get; init; } = new();
        }

        private sealed class RenderedPdf
        {
            public int PageCount { get; init; }

            public List<string> Warnings { get; } = new();
        }
    }
}
