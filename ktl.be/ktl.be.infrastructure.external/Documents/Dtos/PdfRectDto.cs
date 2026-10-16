namespace ktl.be.infrastructure.external.Documents.Dtos
{
    public sealed class PdfRectDto
    {
        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public double Right => X + Width;

        public double Top => Y + Height;
    }
}
