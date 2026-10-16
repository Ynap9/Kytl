namespace ktl.be.applications.Document.Dtos.Sign
{
    public class SignPositionCheckRequestDto
    {
        public List<string> SourcePaths { get; set; } = new();

        public bool UseCustomPosition { get; set; }

        public List<SignPositionDto>? Positions { get; set; }

        public List<SignFileConfigDto>? FileOverrides { get; set; }
    }
}
