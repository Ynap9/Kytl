namespace ktl.be.applications.Document.Dtos.Sign
{
    public class SignPositionFileDto
    {
        public string FileName { get; set; } = string.Empty;

        public string FullPath { get; set; } = string.Empty;

        public int PageCount { get; set; }

        public int BlockCount { get; set; }

        public bool Readable { get; set; }

        public bool Fitted { get; set; }

        public List<string> Issues { get; set; } = new();
    }
}
