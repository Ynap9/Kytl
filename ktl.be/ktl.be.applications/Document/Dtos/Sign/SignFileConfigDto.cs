using ktl.be.applications.Document.Dtos.DocumentMerge;

namespace ktl.be.applications.Document.Dtos.Sign
{
    public class SignFileConfigDto
    {
        public string Path { get; set; } = string.Empty;

        public SignPositionDto? SignaturePosition { get; set; }

        public List<MergeStampDto> Stamps { get; set; } = new();
    }
}
