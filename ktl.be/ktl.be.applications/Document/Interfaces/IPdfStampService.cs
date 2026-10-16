using ktl.be.applications.Document.Dtos.PdfStamp;

namespace ktl.be.applications.Document.Interfaces
{
    public interface IPdfStampService
    {
        PdfStampFileListDto ListFiles(PdfStampSourceRequestDto request);

        PdfStampPreviewDto Preview(PdfStampPreviewRequestDto request);

        Stream OpenPdf(string filePath);

        IAsyncEnumerable<PdfStampProgressDto> StampAsync(PdfStampRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
