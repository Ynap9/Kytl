using ktl.be.infrastructure.external.Documents.Dtos;

namespace ktl.be.infrastructure.external.Documents.Interfaces
{
    public interface ISigningKey
    {
        TokenVerifyDto Verify(string thumbprint);

        byte[] SignData(string thumbprint, byte[] data);

        bool IsTokenPresent(string thumbprint);

        void ClearCache();
    }
}
