namespace ktl.be.applications.Document.Dtos.Sign
{
    public class SignPositionCheckRequestDto
    {
        public List<string> SourcePaths { get; set; } = new();

        public bool ShowSignature { get; set; } = true;

        public SignPositionDto? SignaturePosition { get; set; }

        public List<SignFileConfigDto> FileOverrides { get; set; } = new();
    }
}
