using ktl.be.infrastructure.external.Documents.Dtos;

namespace ktl.be.applications.Document.Dtos.Stamp
{
    public sealed class StampPlanDto
    {
        public List<PdfImageStampDto> Stamps { get; } = new();

        public List<string> Warnings { get; } = new();
    }
}
