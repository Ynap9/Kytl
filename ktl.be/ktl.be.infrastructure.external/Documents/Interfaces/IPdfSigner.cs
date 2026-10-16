using ktl.be.infrastructure.external.Documents.Dtos;

namespace ktl.be.infrastructure.external.Documents.Interfaces
{
    public interface IPdfSigner
    {
        Task SignAsync(string inputPath, string outputPath, PdfSignOptionsDto options,
            CancellationToken cancellationToken = default);
    }
}
