using ktl.be.applications.Document.Dtos.DocumentMerge;
using ktl.be.shared.Constants.Documents;

namespace ktl.be.applications.Document.Dtos.Sign
{
    public class SignRequestDto
    {
        public string Thumbprint { get; set; } = string.Empty;

        public List<string> SourcePaths { get; set; } = new();

        public string? Reason { get; set; }

        public string? Location { get; set; }

        public string? FontFamily { get; set; }

        public double? FontSize { get; set; }

        public string? TextColor { get; set; }

        public bool ShowSignature { get; set; } = true;

        public bool EmbedSignatureInImage { get; set; }

        public SignPositionDto? SignaturePosition { get; set; }

        public string? SealImagePath { get; set; }

        public string? WetSignatureImagePath { get; set; }

        public int SealDarkness { get; set; } = StampImageConstants.DarknessDefault;

        public int WetSignatureDarkness { get; set; } = StampImageConstants.DarknessDefault;

        public int WetSignatureStroke { get; set; } = StampImageConstants.StrokeDefault;

        public string? WetSignatureColor { get; set; }

        public List<MergeStampDto> Stamps { get; set; } = new();

        public List<SignFileConfigDto> FileOverrides { get; set; } = new();
    }
}
