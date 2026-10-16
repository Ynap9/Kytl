namespace ktl.be.infrastructure.external.Documents.Interfaces
{
    public interface ITimestampClient
    {
        Task<byte[]> RequestTokenAsync(byte[] signatureValue, CancellationToken cancellationToken = default);
    }
}
