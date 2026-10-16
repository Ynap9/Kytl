namespace ktl.be.infrastructure.external.Documents.Dtos
{
    public sealed class PdfObjectLocation
    {
        public int Type { get; init; }

        public long Value { get; init; }

        public int IndexInStream { get; init; }
    }
}
