using ktl.be.infrastructure.external.Documents.Dtos;

namespace ktl.be.infrastructure.external.Documents.Interfaces
{
    public interface IPdfImageStamper
    {
        int CountPages(byte[] pdf);

        byte[] Stamp(byte[] pdf, IReadOnlyList<PdfImageStampDto> stamps);
    }
}
