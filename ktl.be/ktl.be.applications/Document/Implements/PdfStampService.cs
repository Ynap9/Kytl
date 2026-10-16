using Microsoft.Extensions.Logging;
using ktl.be.applications.Base;
using ktl.be.applications.Document.Dtos.PdfStamp;
using ktl.be.applications.Document.Dtos.Stamp;
using ktl.be.applications.Document.Interfaces;
using ktl.be.infrastructure.external.Documents.Interfaces;
using ktl.be.shared.Constants.Documents;
using ktl.be.shared.Requests;
using ktl.be.shared.Requests.AppException;
using ktl.be.shared.Requests.ErrorRequest;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace ktl.be.applications.Document.Implements
{
    public class PdfStampService : BaseService, IPdfStampService
    {
        private readonly IStampPlanner _stampPlanner;
        private readonly IPdfImageStamper _pdfImageStamper;
        private readonly ISignatureVerifier _signatureVerifier;
        private readonly IFileUploadService _fileUploadService;

        private const string TargetTypeManifest = "manifest";
        private const string TargetTypeFile = "file";
        private const string TargetTypeSummary = "summary";

        private static readonly Regex OutputFileNameRegex = new(PdfStampConstants.OutputFileNamePattern, RegexOptions.Compiled);
        private static readonly Regex NumberRegex = new(@"\d+", RegexOptions.Compiled);

        private static string PreviewFolder => Path.Combine(Path.GetTempPath(), SignTemplateConstants.AppDataFolderName,
            PdfStampConstants.TempFolderName, PdfStampConstants.PreviewFolderName);

        public PdfStampService(ILogger<BaseService> logger, IStampPlanner stampPlanner,
            IPdfImageStamper pdfImageStamper, ISignatureVerifier signatureVerifier, IFileUploadService fileUploadService)
            : base(logger)
        {
            _stampPlanner = stampPlanner;
            _pdfImageStamper = pdfImageStamper;
            _signatureVerifier = signatureVerifier;
            _fileUploadService = fileUploadService;
        }

        public PdfStampFileListDto ListFiles(PdfStampSourceRequestDto request)
        {
            _logger.LogInformation($"{nameof(ListFiles)}, sources={request.SourcePaths?.Count ?? 0}");

            var result = new PdfStampFileListDto();
            foreach (var path in ResolveSources(request.SourcePaths))
            {
                var info = new FileInfo(path);
                var file = new PdfStampFileDto { Name = info.Name, FullPath = info.FullName, SizeBytes = info.Length };
                try
                {
                    file.PageCount = _pdfImageStamper.CountPages(File.ReadAllBytes(path));
                    file.Readable = file.PageCount > 0;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không đọc được số trang của {File}", path);
                }

                var verify = _signatureVerifier.Verify(path);
                file.AlreadySigned = verify.SignatureBlockCount > 0 || verify.Signatures.Count > 0;
                result.Files.Add(file);
            }

            return result;
        }

        public PdfStampPreviewDto Preview(PdfStampPreviewRequestDto request)
        {
            _logger.LogInformation($"{nameof(Preview)}, filePath={request.FilePath}, stamps={request.Stamps?.Count ?? 0}");

            EnsurePdfFile(request.FilePath);
            var rules = _stampPlanner.BuildRules(request.SealImagePath, request.WetSignatureImagePath, request.Stamps);

            Directory.CreateDirectory(PreviewFolder);
            CleanupPreviews();

            var pdf = File.ReadAllBytes(request.FilePath);
            var pageCount = _pdfImageStamper.CountPages(pdf);
            var plan = _stampPlanner.Plan(rules, pageCount);

            var previewPath = Path.Combine(PreviewFolder, Guid.NewGuid().ToString("N") + PdfStampConstants.PdfExtension);
            File.WriteAllBytes(previewPath, plan.Stamps.Count > 0 ? _pdfImageStamper.Stamp(pdf, plan.Stamps) : pdf);

            return new PdfStampPreviewDto { FilePath = previewPath, PageCount = pageCount, Warnings = plan.Warnings };
        }

        public Stream OpenPdf(string filePath)
        {
            _logger.LogInformation($"{nameof(OpenPdf)}, filePath={filePath}");

            EnsurePdfFile(filePath);
            return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public IAsyncEnumerable<PdfStampProgressDto> StampAsync(PdfStampRequestDto request,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"{nameof(StampAsync)}, sources={request.SourcePaths?.Count ?? 0}, stamps={request.Stamps?.Count ?? 0}");

            var rules = _stampPlanner.BuildRules(request.SealImagePath, request.WetSignatureImagePath, request.Stamps);
            if (rules.Count == 0)
            {
                throw new UserFriendlyException(ErrorCodes.MergeStampInvalid, "Chưa đặt khối dấu đỏ hoặc chữ ký tươi nào.");
            }

            var paths = ResolveSources(request.SourcePaths);
            if (paths.Count == 0)
            {
                throw new UserFriendlyException(ErrorCodes.PdfStampSourceInvalid, "Không có file PDF nào để chèn dấu, chữ ký.");
            }

            var outputFolder = string.IsNullOrWhiteSpace(request.OutputFolder) ? null : request.OutputFolder;
            if (outputFolder == null && paths.Any(_fileUploadService.IsUploaded))
            {
                throw new UserFriendlyException(ErrorCodes.PdfStampFailed,
                    "Có file được kéo thả vào ứng dụng nên cần chọn thư mục lưu kết quả.");
            }
            if (outputFolder != null && !Directory.Exists(outputFolder))
            {
                throw new UserFriendlyException(ErrorCodes.PdfStampFailed, "Không tìm thấy thư mục lưu file kết quả.");
            }

            var stamp = GetVietnamTime().ToString(PdfStampConstants.OutputTimeFormat);
            return RunStampAsync(rules, paths, outputFolder, stamp, cancellationToken);
        }

        private async IAsyncEnumerable<PdfStampProgressDto> RunStampAsync(List<StampRuleDto> rules, List<string> paths,
            string? outputFolder, string stamp, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var total = paths.Count;
            var folders = paths.Select(path => outputFolder ?? Path.GetDirectoryName(path)!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var summaryFolder = folders.Count == 1 ? folders[0] : string.Empty;

            yield return new PdfStampProgressDto
            {
                TargetType = TargetTypeManifest,
                Target = summaryFolder,
                Status = StatusCodeE.Success,
                Code = 200,
                Message = $"Chuẩn bị chèn dấu, chữ ký vào {total} file.",
                Total = total,
                Files = paths.Select(path => Path.GetFileName(path)).ToList()
            };

            var success = 0;
            var failed = 0;
            for (var i = 0; i < total; i++)
            {
                var path = paths[i];
                var fileResult = cancellationToken.IsCancellationRequested
                    ? new PdfStampFileResultDto { FileName = Path.GetFileName(path), Reason = "Tiến trình chèn dấu, chữ ký bị dừng." }
                    : await Task.Run(() => ProcessFile(rules, path, outputFolder ?? Path.GetDirectoryName(path)!, stamp),
                        CancellationToken.None);

                if (fileResult.Success) success++;
                else failed++;

                yield return new PdfStampProgressDto
                {
                    TargetType = TargetTypeFile,
                    Target = fileResult.FileName,
                    Status = fileResult.Success ? StatusCodeE.Success : StatusCodeE.Error,
                    Code = fileResult.Success ? 200 : ErrorCodes.PdfStampFailed,
                    Message = fileResult.Success ? "Đã chèn dấu, chữ ký" : (fileResult.Reason ?? "Chèn dấu, chữ ký thất bại"),
                    Current = i + 1,
                    Total = total,
                    File = fileResult
                };
            }

            yield return new PdfStampProgressDto
            {
                TargetType = TargetTypeSummary,
                Target = summaryFolder,
                Status = failed == 0 ? StatusCodeE.Success : StatusCodeE.Error,
                Code = failed == 0 ? 200 : ErrorCodes.PdfStampFailed,
                Message = $"Chèn dấu, chữ ký xong: {success} thành công, {failed} lỗi.",
                Completed = true,
                Current = total,
                Total = total,
                Summary = new PdfStampResultDto
                {
                    OutputFolder = summaryFolder,
                    TotalFiles = total,
                    SuccessCount = success,
                    FailedCount = failed
                }
            };
        }

        private PdfStampFileResultDto ProcessFile(List<StampRuleDto> rules, string path, string outputFolder, string stamp)
        {
            var result = new PdfStampFileResultDto { FileName = Path.GetFileName(path) };
            var outputPath = Path.Combine(outputFolder,
                Path.GetFileNameWithoutExtension(path) + PdfStampConstants.OutputSuffix + stamp + PdfStampConstants.PdfExtension);
            var tempPath = outputPath + ".tmp";

            try
            {
                var pdf = File.ReadAllBytes(path);
                var plan = _stampPlanner.Plan(rules, _pdfImageStamper.CountPages(pdf));
                result.Warnings = plan.Warnings;

                File.WriteAllBytes(tempPath, plan.Stamps.Count > 0 ? _pdfImageStamper.Stamp(pdf, plan.Stamps) : pdf);
                File.Move(tempPath, outputPath, overwrite: true);

                result.Success = true;
                result.OutputPath = outputPath;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Reason = ex is BaseException be ? be.MessageLocalize ?? ErrorMessages.GetMessage(be.ErrorCode) : ex.Message;
                _logger.LogError(ex, $"{nameof(ProcessFile)} lỗi với file {path}");
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            }

            return result;
        }

        private static List<string> ResolveSources(List<string>? sourcePaths)
        {
            var files = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var source in (sourcePaths ?? new List<string>()).Select(s => (s ?? string.Empty).Trim()))
            {
                var candidates = Directory.Exists(source)
                    ? Directory.EnumerateFiles(source, "*", SearchOption.TopDirectoryOnly)
                        .Where(IsPdf)
                        .Where(p => !OutputFileNameRegex.IsMatch(Path.GetFileNameWithoutExtension(p)))
                        .OrderBy(p => NumberRegex.Replace(Path.GetFileName(p), m => m.Value.PadLeft(12, '0')),
                            StringComparer.OrdinalIgnoreCase)
                    : EnsurePdfFile(source);

                foreach (var path in candidates.Select(Path.GetFullPath))
                {
                    if (seen.Add(path)) files.Add(path);
                }
            }

            return files;
        }

        private static IEnumerable<string> EnsurePdfFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath) || !IsPdf(filePath))
            {
                throw new UserFriendlyException(ErrorCodes.PdfStampSourceInvalid,
                    $"Không tìm thấy file hoặc thư mục PDF: {filePath}");
            }

            return new[] { filePath };
        }

        private static bool IsPdf(string path)
        {
            return string.Equals(Path.GetExtension(path), PdfStampConstants.PdfExtension, StringComparison.OrdinalIgnoreCase);
        }

        private static void CleanupPreviews()
        {
            var expiredBefore = DateTime.UtcNow.AddMinutes(-PdfStampConstants.PreviewRetentionMinutes);
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
    }
}
