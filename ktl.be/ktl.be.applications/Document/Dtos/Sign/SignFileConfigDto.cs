namespace ktl.be.applications.Document.Dtos.Sign
{
    public class SignFileConfigDto
    {
        public string Path { get; set; } = string.Empty;

        public List<SignPositionDto> Positions { get; set; } = new();
    }
}
