using ktl.be.shared.Requests;

namespace ktl.be.applications.Document.Dtos.PdfStamp
{
    public class PdfStampPreviewDto
    {
        public string FilePath { get; set; } = string.Empty;

        public int PageCount { get; set; }

        public List<string> Warnings { get; set; } = new();
    }

    public class PdfStampFileResultDto
    {
        public string FileName { get; set; } = string.Empty;

        public string? OutputPath { get; set; }

        public bool Success { get; set; }

        public string? Reason { get; set; }

        public List<string> Warnings { get; set; } = new();
    }

    public class PdfStampResultDto
    {
        public string OutputFolder { get; set; } = string.Empty;

        public int TotalFiles { get; set; }

        public int SuccessCount { get; set; }

        public int FailedCount { get; set; }
    }

    public class PdfStampProgressDto
    {
        public string TargetType { get; set; } = string.Empty;

        public string Target { get; set; } = string.Empty;

        public StatusCodeE Status { get; set; }

        public int Code { get; set; }

        public string Message { get; set; } = string.Empty;

        public bool Completed { get; set; }

        public int Current { get; set; }

        public int Total { get; set; }

        public int Percent => Total > 0 ? (int)System.Math.Round(Current * 100.0 / Total) : 0;

        public List<string>? Files { get; set; }

        public PdfStampFileResultDto? File { get; set; }

        public PdfStampResultDto? Summary { get; set; }
    }
}
