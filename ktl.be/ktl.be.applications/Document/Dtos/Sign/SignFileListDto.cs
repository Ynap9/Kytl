namespace ktl.be.applications.Document.Dtos.Sign
{
    public class SignFileItemDto
    {
        public string Name { get; set; } = string.Empty;

        public string FullPath { get; set; } = string.Empty;

        public long SizeBytes { get; set; }

        public bool AlreadySigned { get; set; }
    }

    public class SignSourceRequestDto
    {
        public List<string> SourcePaths { get; set; } = new();
    }

    public class SignFileListDto
    {
        public List<SignFileItemDto> Files { get; set; } = new();

        public int Total => Files.Count;
    }
}
