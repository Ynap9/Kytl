namespace ktl.be.infrastructure.external.Documents.Dtos
{
    public sealed class PdfPageGeometryDto
    {
        public int ObjectNumber { get; init; }

        public PdfRectDto CropBox { get; init; } = new();

        public int Rotation { get; init; }

        public PdfRectDto DisplayBox => Rotation is 90 or 270
            ? new PdfRectDto { Width = CropBox.Height, Height = CropBox.Width }
            : new PdfRectDto { Width = CropBox.Width, Height = CropBox.Height };
    }
}
