using ktl.be.infrastructure.external.Documents.Dtos;

namespace ktl.be.infrastructure.external.Documents.Interfaces
{
    public interface ISignaturePlacementResolver
    {
        PdfRectDto Resolve(PdfRectDto pageBox, PdfSignPlacementDto? placement, double width, double height);
    }
}
