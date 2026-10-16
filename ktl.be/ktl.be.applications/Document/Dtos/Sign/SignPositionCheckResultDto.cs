namespace ktl.be.applications.Document.Dtos.Sign
{
    public class SignPositionCheckResultDto
    {
        public int TotalFiles { get; set; }

        public int FittedCount { get; set; }

        public int AdjustedCount { get; set; }

        public int UnreadableCount { get; set; }

        public List<SignPositionFileDto> Files { get; set; } = new();
    }
}
