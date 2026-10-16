namespace ktl.be.applications.Document.Dtos.Sign
{
    public class SignRequestDto
    {
        public string Thumbprint { get; set; } = string.Empty;

        public List<string> SourcePaths { get; set; } = new();

        public string? Reason { get; set; }

        public string? Location { get; set; }

        public bool UseCustomPosition { get; set; }

        public List<SignPositionDto>? Positions { get; set; }

        public List<SignFileConfigDto>? FileOverrides { get; set; }

        public bool AllowOverlapContent { get; set; }

        public string? FontFamily { get; set; }

        public double? FontSize { get; set; }

        public string? TextColor { get; set; }

        public string? SealImagePath { get; set; }

        public string? WetSignatureImagePath { get; set; }
    }
}
