using ktl.be.shared.Constants.Documents;

namespace ktl.be.infrastructure.external.Documents.Dtos
{
    public sealed class PdfSignOptionsDto
    {
        public string Thumbprint { get; set; } = string.Empty;

        public string? Reason { get; set; }

        public string? Location { get; set; }

        public bool ShowSignature { get; set; } = true;

        public PdfSignPlacementDto? SignaturePlacement { get; set; }

        public IReadOnlyList<PdfSignPlacementDto> StampPlacements { get; set; } = new List<PdfSignPlacementDto>();

        public bool EmbedSignatureInImage { get; set; }

        public string FontFamily { get; set; } = SigningConstants.AppearanceFontFamily;

        public double FontSize { get; set; } = SigningConstants.AppearanceFontSize;

        public string TextColor { get; set; } = SigningConstants.AppearanceTextColor;

        public byte[]? SealImage { get; set; }

        public byte[]? WetSignatureImage { get; set; }

        public int SealDarkness { get; set; } = StampImageConstants.DarknessDefault;

        public int WetSignatureDarkness { get; set; } = StampImageConstants.DarknessDefault;

        public int WetSignatureStroke { get; set; } = StampImageConstants.StrokeDefault;

        public string? WetSignatureColor { get; set; }
    }
}
