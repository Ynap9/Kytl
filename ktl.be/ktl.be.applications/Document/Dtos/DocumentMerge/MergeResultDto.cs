namespace ktl.be.applications.Document.Dtos.DocumentMerge
{
    public class MergeFileResultDto
    {
        public int RowNumber { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string? OutputPath { get; set; }

        public bool Success { get; set; }

        public string? Reason { get; set; }

        public List<string> Warnings { get; set; } = new();
    }

    public class MergeResultDto
    {
        public string OutputFolder { get; set; } = string.Empty;

        public int TotalFiles { get; set; }

        public int SuccessCount { get; set; }

        public int FailedCount { get; set; }
    }
}
