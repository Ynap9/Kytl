namespace ktl.be.applications.Document.Dtos.DocumentMerge
{
    public class MergePreviewDto
    {
        public int RowNumber { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public int PageCount { get; set; }

        public List<string> Warnings { get; set; } = new();
    }
}
