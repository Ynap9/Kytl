using ktl.be.shared.Constants.Documents;

namespace ktl.be.infrastructure.external.Documents.Dtos
{
    public sealed class PdfSignOptionsDto
    {
        public string Thumbprint { get; set; } = string.Empty;

        public string? Reason { get; set; }

        public string? Location { get; set; }

        public IReadOnlyList<PdfSignPlacementDto>? Placements { get; set; }

        public bool AllowOverlapContent { get; set; }

        public string FontFamily { get; set; } = SigningConstants.AppearanceFontFamily;

        public double FontSize { get; set; } = SigningConstants.AppearanceFontSize;

        public string TextColor { get; set; } = SigningConstants.AppearanceTextColor;

        public byte[]? SealImage { get; set; }

        public byte[]? WetSignatureImage { get; set; }
    }
}
