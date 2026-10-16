namespace ktl.be.applications.Document.Dtos.PdfStamp
{
    public class PdfStampFileDto
    {
        public string Name { get; set; } = string.Empty;

        public string FullPath { get; set; } = string.Empty;

        public long SizeBytes { get; set; }

        public int PageCount { get; set; }

        public bool Readable { get; set; }

        public bool AlreadySigned { get; set; }
    }

    public class PdfStampSourceRequestDto
    {
        public List<string> SourcePaths { get; set; } = new();
    }

    public class PdfStampFileListDto
    {
        public List<PdfStampFileDto> Files { get; set; } = new();
    }
}
