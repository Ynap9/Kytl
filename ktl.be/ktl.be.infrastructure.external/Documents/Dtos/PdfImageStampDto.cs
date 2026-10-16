namespace ktl.be.infrastructure.external.Documents.Dtos
{
    public sealed class PdfImageStampDto
    {
        public int PageNumber { get; set; } = 1;

        public double XRatio { get; set; }

        public double YRatio { get; set; }

        public double WidthRatio { get; set; }

        public double HeightRatio { get; set; }

        public byte[] Image { get; set; } = Array.Empty<byte>();
    }
}
