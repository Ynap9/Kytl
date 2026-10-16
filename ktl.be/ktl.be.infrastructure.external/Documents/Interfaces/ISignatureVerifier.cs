using ktl.be.infrastructure.external.Documents.Dtos;

namespace ktl.be.infrastructure.external.Documents.Interfaces
{
    public interface ISignatureVerifier
    {
        TimestampVerifyDto Verify(string filePath);
    }
}
