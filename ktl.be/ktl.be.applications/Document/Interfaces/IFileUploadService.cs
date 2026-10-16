using ktl.be.applications.Document.Dtos.FileUpload;

namespace ktl.be.applications.Document.Interfaces
{
    public interface IFileUploadService
    {
        Task<UploadedFileDto> SaveAsync(string fileName, Stream content, long length,
            CancellationToken cancellationToken = default);

        bool IsUploaded(string? path);
    }
}
