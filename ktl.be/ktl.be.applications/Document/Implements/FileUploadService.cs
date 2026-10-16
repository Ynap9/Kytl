using Microsoft.Extensions.Logging;
using ktl.be.applications.Base;
using ktl.be.applications.Document.Dtos.FileUpload;
using ktl.be.applications.Document.Interfaces;
using ktl.be.shared.Constants;
using ktl.be.shared.Requests.AppException;
using ktl.be.shared.Requests.ErrorRequest;

namespace ktl.be.applications.Document.Implements
{
    public class FileUploadService : BaseService, IFileUploadService
    {
        public FileUploadService(ILogger<BaseService> logger) : base(logger)
        {
        }

        public async Task<UploadedFileDto> SaveAsync(string fileName, Stream content, long length,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"{nameof(SaveAsync)}, fileName={fileName}, length={length}");

            var name = Path.GetFileName(fileName ?? string.Empty);
            var ext = Path.GetExtension(name).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(name) || !FileUploadConstants.AllowedExtensions.Contains(ext))
            {
                throw new UserFriendlyException(ErrorCodes.FileUploadInvalid,
                    $"Chỉ nhận file {string.Join(", ", FileUploadConstants.AllowedExtensions)}.");
            }
            if (length <= 0 || length > FileUploadConstants.MaxFileBytes)
            {
                throw new UserFriendlyException(ErrorCodes.FileUploadInvalid,
                    $"File {name} rỗng hoặc vượt quá {FileUploadConstants.MaxFileBytes / 1024 / 1024} MB.");
            }

            Directory.CreateDirectory(FileUploadConstants.UploadDirectory);
            CleanupExpired();

            var folder = Path.Combine(FileUploadConstants.UploadDirectory, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, name);

            await using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            {
                await content.CopyToAsync(target, cancellationToken);
            }

            return new UploadedFileDto { FileName = name, FilePath = path, SizeBytes = length };
        }

        public bool IsUploaded(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;

            var folder = Path.GetFullPath(FileUploadConstants.UploadDirectory) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).StartsWith(folder, StringComparison.OrdinalIgnoreCase);
        }

        private void CleanupExpired()
        {
            var expiredBefore = DateTime.UtcNow.AddHours(-FileUploadConstants.RetentionHours);
            foreach (var folder in Directory.EnumerateDirectories(FileUploadConstants.UploadDirectory))
            {
                try
                {
                    if (Directory.GetLastWriteTimeUtc(folder) < expiredBefore) Directory.Delete(folder, recursive: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không xoá được thư mục tải lên đã hết hạn {Folder}", folder);
                }
            }
        }
    }
}
