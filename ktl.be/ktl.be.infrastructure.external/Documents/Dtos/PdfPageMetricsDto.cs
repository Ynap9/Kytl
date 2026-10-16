namespace ktl.be.infrastructure.external.Documents.Dtos
{
    public sealed class PdfPageMetricsDto
    {
        public bool Readable { get; set; }

        public List<PdfRectDto> Pages { get; set; } = new();
    }
}
