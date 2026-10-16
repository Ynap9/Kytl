using ktl.be.infrastructure.external.Documents.Dtos;

namespace ktl.be.infrastructure.external.Documents.Interfaces
{
    public interface IPdfSignatureReader
    {
        SignatureInfoDto Read(string filePath);
    }
}
