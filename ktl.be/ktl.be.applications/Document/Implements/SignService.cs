using Microsoft.Extensions.Logging;
using ktl.be.applications.Base;
using ktl.be.infrastructure.external.Documents.Dtos;
using ktl.be.applications.Document.Dtos.Sign;
using ktl.be.applications.Document.Interfaces;
using ktl.be.shared.Constants.Documents;
using ktl.be.shared.Requests;
using ktl.be.shared.Requests.AppException;
using ktl.be.shared.Requests.ErrorRequest;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using ktl.be.infrastructure.external.Documents.Interfaces;

namespace ktl.be.applications.Document.Implements
{
    public class SignService : BaseService, ISignService
    {
        private readonly ICertificateProvider _certificateProvider;
        private readonly ISigningKey _signingKey;
        private readonly IPdfSigner _pdfSigner;
        private readonly ISignatureVerifier _signatureVerifier;
        private readonly IPdfPageInspector _pdfPageInspector;
        private readonly ISignaturePlacementResolver _placementResolver;

        private const string PdfExtension = ".pdf";
        private const string TargetTypeManifest = "manifest";
        private const string TargetTypeFile = "file";
        private const string TargetTypeSummary = "summary";

        public SignService(ILogger<BaseService> logger,
            ICertificateProvider certificateProvider, ISigningKey signingKey, IPdfSigner pdfSigner,
            ISignatureVerifier signatureVerifier,
            IPdfPageInspector pdfPageInspector, ISignaturePlacementResolver placementResolver)
            : base(logger)
        {
            _certificateProvider = certificateProvider;
            _signingKey = signingKey;
            _pdfSigner = pdfSigner;
            _signatureVerifier = signatureVerifier;
            _pdfPageInspector = pdfPageInspector;
            _placementResolver = placementResolver;
        }

        public List<SignCertDto> GetCertificates(SignCertQueryDto query)
        {
            _logger.LogInformation($"{nameof(GetCertificates)}, onlySignable={query.OnlySignable}");

            var scan = _certificateProvider.GetCertificates();
            var certs = scan.Certificates;
            var scanInfo = string.Join("; ", scan.StoreDiagnostics);
            _logger.LogInformation($"{nameof(GetCertificates)}, đã quét: {scanInfo}");

            foreach (var c in certs)
            {
                _logger.LogInformation(
                    $"  cert: cn={c.CommonName}, source={c.Source}, hasKey={c.HasPrivateKey}, "
                    + $"expired={c.IsExpired}, allowsSigning={c.AllowsSigning}, trusted={c.IsTrusted}, canSign={c.CanSign}, "
                    + $"provider={c.KeyProvider ?? "(không có)"}, reason={c.Reason ?? "(ký được)"}");
            }

            if (certs.Count == 0)
            {
                throw new UserFriendlyException(ErrorCodes.CertificateNotFound,
                    "Không đọc được chứng thư số nào. Kiểm tra USB token đã cắm và middleware (VGCA PKI Manager/bit4id xPKI) đã chạy chưa. "
                    + $"Kết quả quét: {scanInfo}.");
            }

            if (query.OnlySignable)
            {
                var signable = certs.Where(c => c.CanSign).ToList();
                if (signable.Count == 0)
                {
                    throw new UserFriendlyException(ErrorCodes.CertificateNotSignable,
                        "Có chứng thư số nhưng không cái nào dùng để ký được (thiếu khóa bí mật, hết hạn, không có quyền ký, hoặc không thuộc CA Ban Cơ yếu).");
                }
                return signable;
            }

            return certs;
        }

        public SignFileListDto ListPdfFiles(SignSourceRequestDto request)
        {
            _logger.LogInformation($"{nameof(ListPdfFiles)}, sources={request.SourcePaths?.Count ?? 0}");

            return ResolvePdfFiles(request.SourcePaths);
        }

        private SignFileListDto ResolvePdfFiles(List<string>? sourcePaths)
        {
            var result = new SignFileListDto();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var sourcePath in (sourcePaths ?? new List<string>()).Select(s => (s ?? string.Empty).Trim()))
            {
                var isFolder = Directory.Exists(sourcePath);
                if (!isFolder && !File.Exists(sourcePath))
                {
                    throw new UserFriendlyException(ErrorCodes.SignSourceNotFound,
                        $"Không tìm thấy file hoặc thư mục cần ký: {sourcePath}");
                }

                var paths = isFolder
                    ? Directory.EnumerateFiles(sourcePath, "*", SearchOption.TopDirectoryOnly)
                        .Where(p => string.Equals(Path.GetExtension(p), PdfExtension, StringComparison.OrdinalIgnoreCase))
                        .Where(p => !Regex.IsMatch(Path.GetFileNameWithoutExtension(p), SigningConstants.SignedFileNamePattern))
                        .OrderBy(p => Regex.Replace(Path.GetFileName(p), @"\d+", m => m.Value.PadLeft(12, '0')),
                            StringComparer.OrdinalIgnoreCase)
                    : new[] { sourcePath }.Where(p =>
                        string.Equals(Path.GetExtension(p), PdfExtension, StringComparison.OrdinalIgnoreCase));

                foreach (var path in paths.Select(Path.GetFullPath).Where(seen.Add))
                {
                    var info = new FileInfo(path);
                    var verify = _signatureVerifier.Verify(path);

                    result.Files.Add(new SignFileItemDto
                    {
                        Name = info.Name,
                        FullPath = info.FullName,
                        SizeBytes = info.Length,
                        AlreadySigned = verify.ReadFailed || verify.SignatureBlockCount > 0 || verify.Signatures.Count > 0
                    });
                }
            }

            return result;
        }

        public SignPreviewDto GetPreviewAt(string sourcePath, int index)
        {
            _logger.LogInformation($"{nameof(GetPreviewAt)}, sourcePath={sourcePath}, index={index}");

            var list = ResolvePdfFiles(new List<string> { sourcePath });
            var total = list.Files.Count;

            var result = new SignPreviewDto
            {
                Index = index,
                Total = total,
                HasPrev = index > 0 && total > 0,
                HasNext = index >= 0 && index < total - 1
            };

            if (index >= 0 && index < total)
            {
                result.Files.Add(list.Files[index]);
            }

            return result;
        }

        public Stream OpenPdfForPreview(string filePath)
        {
            _logger.LogInformation($"{nameof(OpenPdfForPreview)}, filePath={filePath}");

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                throw new UserFriendlyException(ErrorCodes.SignSourceNotFound,
                    "Không tìm thấy file PDF cần xem trước.");
            }

            if (!string.Equals(Path.GetExtension(filePath), PdfExtension, StringComparison.OrdinalIgnoreCase))
            {
                throw new UserFriendlyException(ErrorCodes.SignSourceNotFound,
                    "Chỉ xem trước được file PDF.");
            }

            return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public TimestampVerifyDto VerifySignature(string filePath)
        {
            _logger.LogInformation($"{nameof(VerifySignature)}, filePath={filePath}");

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                throw new UserFriendlyException(ErrorCodes.SignSourceNotFound,
                    "Không tìm thấy file cần kiểm tra chữ ký.");
            }

            return _signatureVerifier.Verify(filePath);
        }

        public TokenVerifyDto VerifyToken(string thumbprint)
        {
            _logger.LogInformation($"{nameof(VerifyToken)}, thumbprint={thumbprint}");

            var result = _signingKey.Verify(thumbprint);
            _logger.LogInformation(
                $"{nameof(VerifyToken)}, valid={result.Valid}, failedCheck={result.FailedCheck}, reason={result.Reason}");
            return result;
        }

        public IAsyncEnumerable<SignProgressDto> SignAsync(SignRequestDto request,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"{nameof(SignAsync)}, thumbprint={request.Thumbprint}, sources={request.SourcePaths?.Count ?? 0}");

            var tokenCheck = _signingKey.Verify(request.Thumbprint);
            if (!tokenCheck.Valid)
            {
                throw new UserFriendlyException(ErrorCodes.SignTokenInvalid,
                    tokenCheck.Reason ?? "Chứng thư số không dùng để ký được.");
            }

            List<PdfSignPlacementDto>? placements = null;
            if (request.UseCustomPosition)
            {
                if ((request.Positions == null || request.Positions.Count == 0)
                    && (request.FileOverrides == null || request.FileOverrides.Count == 0))
                {
                    throw new UserFriendlyException(ErrorCodes.SignPositionInvalid,
                        "Đã chọn tự đặt vị trí chữ ký nhưng không có tọa độ gửi kèm.");
                }

                placements = NormalizePlacements(request.Positions ?? new List<SignPositionDto>());
            }

            var overrides = new Dictionary<string, List<PdfSignPlacementDto>>(StringComparer.OrdinalIgnoreCase);
            if (request.UseCustomPosition && request.FileOverrides != null)
            {
                foreach (var file in request.FileOverrides)
                {
                    if (string.IsNullOrWhiteSpace(file.Path)) continue;
                    overrides[Path.GetFullPath(file.Path)] = NormalizePlacements(file.Positions);
                }
            }

            var signOptions = new PdfSignOptionsDto
            {
                Thumbprint = request.Thumbprint,
                Reason = request.Reason,
                Location = request.Location,
                Placements = placements,
                AllowOverlapContent = request.AllowOverlapContent,
                FontFamily = SigningConstants.AppearanceFontFamilies.Contains(request.FontFamily)
                    ? request.FontFamily!
                    : SigningConstants.AppearanceFontFamily,
                FontSize = request.FontSize is > 0
                    ? Math.Clamp(request.FontSize.Value, SigningConstants.AppearanceMinFontSize,
                        SigningConstants.AppearanceMaxFontSize)
                    : SigningConstants.AppearanceFontSize,
                TextColor = string.IsNullOrWhiteSpace(request.TextColor)
                    ? SigningConstants.AppearanceTextColor
                    : request.TextColor
            };

            foreach (var (path, isSeal) in new[] { (request.SealImagePath, true), (request.WetSignatureImagePath, false) })
            {
                if (string.IsNullOrWhiteSpace(path)) continue;

                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (!File.Exists(path) || !SignTemplateConstants.AllowedImageExtensions.Contains(ext)
                    || new FileInfo(path).Length > SignTemplateConstants.MaxImageBytes)
                {
                    throw new UserFriendlyException(ErrorCodes.SignTemplateImageInvalid,
                        $"Ảnh {(isSeal ? "dấu đỏ" : "chữ ký tươi")} không hợp lệ hoặc không tồn tại: {path}");
                }

                var bytes = File.ReadAllBytes(path);
                if (isSeal) signOptions.SealImage = bytes;
                else signOptions.WetSignatureImage = bytes;
            }

            var fileList = ResolvePdfFiles(request.SourcePaths);
            if (fileList.Files.Count == 0)
            {
                throw new UserFriendlyException(ErrorCodes.SignNoPdfFound, "Không có file PDF nào để ký.");
            }

            var folders = fileList.Files.Select(f => Path.GetDirectoryName(f.FullPath)!)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var outputFolder = folders.Count == 1 ? folders[0] : string.Empty;
            var signedStamp = GetVietnamTime().ToString(SigningConstants.SignedFileTimeFormat);

            return RunSignAsync(request.Thumbprint, signOptions, overrides, fileList, outputFolder, signedStamp,
                cancellationToken);
        }

        private async IAsyncEnumerable<SignProgressDto> RunSignAsync(string thumbprint, PdfSignOptionsDto signOptions,
            Dictionary<string, List<PdfSignPlacementDto>> overrides, SignFileListDto fileList, string outputFolder,
            string signedStamp, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var total = fileList.Files.Count;
            var channel = Channel.CreateUnbounded<SignProgressDto>();

            _ = Task.Run(async () =>
            {
                using var runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var done = new ConcurrentDictionary<string, byte>();
                var tokenRemoved = false;

                var monitor = Task.Run(async () =>
                {
                    while (!runCts.IsCancellationRequested)
                    {
                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(SigningConstants.TokenMonitorIntervalSeconds), runCts.Token);
                        }
                        catch (OperationCanceledException) { return; }

                        if (!_signingKey.IsTokenPresent(thumbprint))
                        {
                            tokenRemoved = true;
                            _signingKey.ClearCache();
                            runCts.Cancel();
                            return;
                        }
                    }
                }, CancellationToken.None);

                var success = 0;
                var failed = 0;

                try
                {
                    await channel.Writer.WriteAsync(new SignProgressDto
                    {
                        TargetType = TargetTypeManifest,
                        Target = outputFolder,
                        Status = StatusCodeE.Success,
                        Code = 200,
                        Message = $"Chuẩn bị ký {total} file.",
                        Current = 0,
                        Total = total,
                        Files = fileList.Files.Select(f => f.Name).ToList()
                    }, CancellationToken.None);

                    var parallelOptions = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount,
                            SigningConstants.MinDegreeOfParallelism, SigningConstants.MaxDegreeOfParallelism),
                        CancellationToken = runCts.Token
                    };

                    await Parallel.ForEachAsync(fileList.Files, parallelOptions, async (item, ct) =>
                    {
                        var signedName = $"{Path.GetFileNameWithoutExtension(item.Name)}" +
                            $"{SigningConstants.SignedFileSuffix}{signedStamp}{Path.GetExtension(item.Name)}";
                        var outputPath = Path.Combine(Path.GetDirectoryName(item.FullPath)!, signedName);
                        var fileResult = new SignFileResultDto { FileName = item.Name };

                        try
                        {
                            var fileOptions = overrides.TryGetValue(item.FullPath, out var own)
                                ? new PdfSignOptionsDto
                                {
                                    Thumbprint = signOptions.Thumbprint,
                                    Reason = signOptions.Reason,
                                    Location = signOptions.Location,
                                    Placements = own,
                                    AllowOverlapContent = signOptions.AllowOverlapContent,
                                    FontFamily = signOptions.FontFamily,
                                    FontSize = signOptions.FontSize,
                                    TextColor = signOptions.TextColor,
                                    SealImage = signOptions.SealImage,
                                    WetSignatureImage = signOptions.WetSignatureImage
                                }
                                : signOptions;

                            await _pdfSigner.SignAsync(item.FullPath, outputPath, fileOptions, ct);

                            var verify = _signatureVerifier.Verify(outputPath);
                            var ownSignature = verify.Signatures.LastOrDefault();
                            if (ownSignature?.Verdict != TimestampVerdict.ValidTrusted)
                            {
                                throw new UserFriendlyException(ErrorCodes.TimestampInvalid,
                                    ownSignature?.Reason ?? verify.Reason);
                            }

                            fileResult.Success = true;
                            fileResult.OutputPath = outputPath;
                            fileResult.Verdict = verify.Verdict;
                            fileResult.VerifyReason = verify.Reason;
                            fileResult.TrustedTime = verify.TrustedTime;
                            fileResult.SignedTime = verify.SigningTime;
                            fileResult.SignatureCount = verify.Signatures.Count;
                            fileResult.ValidSignatureCount = verify.ValidSignatureCount;
                            fileResult.Signatures = verify.Signatures;
                            Interlocked.Increment(ref success);
                        }
                        catch (Exception ex)
                        {
                            fileResult.Success = false;
                            fileResult.Reason = ex is BaseException be
                                ? be.MessageLocalize ?? ErrorMessages.GetMessage(be.ErrorCode)
                                : ex.Message;
                            Interlocked.Increment(ref failed);

                            try { if (File.Exists(outputPath)) File.Delete(outputPath); } catch { }
                        }

                        done[item.FullPath] = 1;
                        await channel.Writer.WriteAsync(new SignProgressDto
                        {
                            TargetType = TargetTypeFile,
                            Target = item.Name,
                            Status = fileResult.Success ? StatusCodeE.Success : StatusCodeE.Error,
                            Code = fileResult.Success ? 200 : ErrorCodes.SignFailed,
                            Message = fileResult.Success ? "Đã ký" : (fileResult.Reason ?? "Ký thất bại"),
                            Current = done.Count,
                            Total = total,
                            File = fileResult
                        }, CancellationToken.None);
                    });
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"{nameof(SignAsync)} lỗi ngoài dự kiến");
                }

                var pendingReason = tokenRemoved
                    ? "Đã rút USB token khi đang ký nên dừng tiến trình."
                    : "Tiến trình ký bị dừng.";
                foreach (var item in fileList.Files.Where(f => !done.ContainsKey(f.FullPath)))
                {
                    failed++;
                    await channel.Writer.WriteAsync(new SignProgressDto
                    {
                        TargetType = TargetTypeFile,
                        Target = item.Name,
                        Status = StatusCodeE.Error,
                        Code = ErrorCodes.SignFailed,
                        Message = pendingReason,
                        Current = total,
                        Total = total,
                        File = new SignFileResultDto
                        {
                            FileName = item.Name,
                            Success = false,
                            Reason = pendingReason
                        }
                    }, CancellationToken.None);
                }

                await channel.Writer.WriteAsync(new SignProgressDto
                {
                    TargetType = TargetTypeSummary,
                    Target = outputFolder,
                    Status = failed == 0 ? StatusCodeE.Success : StatusCodeE.Error,
                    Code = failed == 0 ? 200 : ErrorCodes.SignFailed,
                    Message = tokenRemoved
                        ? $"Đã rút token khi đang ký. Ký thành công {success}/{total} file."
                        : $"Ký xong: {success} đã ký, {failed} lỗi.",
                    Completed = true,
                    Current = total,
                    Total = total,
                    Summary = new SignResultDto
                    {
                        OutputFolder = outputFolder,
                        TotalFiles = total,
                        SuccessCount = success,
                        FailedCount = failed
                    }
                }, CancellationToken.None);

                runCts.Cancel();
                await monitor;

                _signingKey.ClearCache();
                channel.Writer.Complete();
            }, CancellationToken.None);

            await foreach (var progress in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return progress;
            }
        }

        public SignPositionCheckResultDto CheckPositions(SignPositionCheckRequestDto request)
        {
            _logger.LogInformation($"{nameof(CheckPositions)}, sources={request.SourcePaths?.Count ?? 0}");

            var fileList = ResolvePdfFiles(request.SourcePaths);
            var shared = request.UseCustomPosition
                ? NormalizePlacements(request.Positions ?? new List<SignPositionDto>())
                : new List<PdfSignPlacementDto>();

            var overrides = new Dictionary<string, List<PdfSignPlacementDto>>(StringComparer.OrdinalIgnoreCase);
            if (request.UseCustomPosition && request.FileOverrides != null)
            {
                foreach (var file in request.FileOverrides)
                {
                    if (string.IsNullOrWhiteSpace(file.Path)) continue;
                    overrides[Path.GetFullPath(file.Path)] = NormalizePlacements(file.Positions);
                }
            }

            var result = new SignPositionCheckResultDto { TotalFiles = fileList.Files.Count };
            foreach (var item in fileList.Files)
            {
                var placements = overrides.TryGetValue(item.FullPath, out var own) ? own : shared;
                var metrics = _pdfPageInspector.Read(item.FullPath);
                var fileResult = new SignPositionFileDto
                {
                    FileName = item.Name,
                    FullPath = item.FullPath,
                    PageCount = metrics.Pages.Count,
                    BlockCount = placements.Count > 0 ? placements.Count : 1,
                    Readable = metrics.Readable
                };

                if (!metrics.Readable)
                {
                    fileResult.Issues.Add("Không đọc được file để đo khổ trang. File hỏng hoặc không phải PDF.");
                    result.UnreadableCount++;
                    result.Files.Add(fileResult);
                    continue;
                }

                for (var i = 0; i < placements.Count; i++)
                {
                    var placement = placements[i];
                    var pageIndex = Math.Clamp(placement.PageNumber - 1, 0, metrics.Pages.Count - 1);
                    if (placement.PageNumber > metrics.Pages.Count)
                    {
                        fileResult.Issues.Add($"Khối {i + 1} đặt ở trang {placement.PageNumber} nhưng file chỉ " +
                            $"có {metrics.Pages.Count} trang, sẽ đóng vào trang {metrics.Pages.Count}.");
                    }

                    var pageBox = metrics.Pages[pageIndex];
                    var requested = new PdfRectDto
                    {
                        X = placement.XRatio * pageBox.Width,
                        Y = pageBox.Height - placement.YRatio * pageBox.Height - placement.HeightRatio * pageBox.Height,
                        Width = placement.WidthRatio * pageBox.Width,
                        Height = placement.HeightRatio * pageBox.Height
                    };
                    var actual = _placementResolver.Resolve(pageBox, placement, requested.Width, requested.Height);

                    if (Math.Abs(actual.X - requested.X) > 0.5 || Math.Abs(actual.Y - requested.Y) > 0.5
                        || Math.Abs(actual.Width - requested.Width) > 0.5 || Math.Abs(actual.Height - requested.Height) > 0.5)
                    {
                        fileResult.Issues.Add($"Khối {i + 1} tràn ra ngoài trang {pageIndex + 1}, sẽ được dịch " +
                            "vào trong cho khỏi mất chữ ký.");
                    }
                }

                fileResult.Fitted = fileResult.Issues.Count == 0;
                if (fileResult.Fitted) result.FittedCount++;
                else result.AdjustedCount++;
                result.Files.Add(fileResult);
            }

            return result;
        }

        private static List<PdfSignPlacementDto> NormalizePlacements(List<SignPositionDto> positions)
        {
            var result = new List<PdfSignPlacementDto>();
            foreach (var position in positions)
            {
                var width = position.WidthRatio > 0
                    ? Math.Min(position.WidthRatio, 1)
                    : SigningConstants.AppearanceWidth / SigningConstants.AppearanceReferencePageWidth;
                var height = position.HeightRatio > 0
                    ? Math.Min(position.HeightRatio, 1)
                    : SigningConstants.AppearanceHeight / SigningConstants.AppearanceReferencePageHeight;

                result.Add(new PdfSignPlacementDto
                {
                    PageNumber = Math.Max(position.PageNumber, 1),
                    XRatio = Math.Clamp(position.XRatio, 0, 1 - width),
                    YRatio = Math.Clamp(position.YRatio, 0, 1 - height),
                    WidthRatio = width,
                    HeightRatio = height,
                    Kind = Enum.IsDefined(position.Kind) ? position.Kind : PdfSignPlacementKind.Signature
                });
            }
            return result;
        }
    }
}
