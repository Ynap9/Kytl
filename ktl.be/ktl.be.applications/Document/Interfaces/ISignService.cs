using ktl.be.infrastructure.external.Documents.Dtos;
using ktl.be.applications.Document.Dtos.Sign;

namespace ktl.be.applications.Document.Interfaces
{
    public interface ISignService
    {
        List<SignCertDto> GetCertificates(SignCertQueryDto query);

        SignFileListDto ListPdfFiles(SignSourceRequestDto request);

        SignPreviewDto GetPreviewAt(string sourcePath, int index);

        Stream OpenPdfForPreview(string filePath);

        TimestampVerifyDto VerifySignature(string filePath);

        TokenVerifyDto VerifyToken(string thumbprint);

        SignPositionCheckResultDto CheckPositions(SignPositionCheckRequestDto request);

        IAsyncEnumerable<SignProgressDto> SignAsync(SignRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
