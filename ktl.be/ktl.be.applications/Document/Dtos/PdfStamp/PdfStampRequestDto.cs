using ktl.be.applications.Document.Dtos.DocumentMerge;

namespace ktl.be.applications.Document.Dtos.PdfStamp
{
    public class PdfStampRequestDto
    {
        public List<string> SourcePaths { get; set; } = new();

        public string? OutputFolder { get; set; }

        public string? SealImagePath { get; set; }

        public string? WetSignatureImagePath { get; set; }

        public List<MergeStampDto> Stamps { get; set; } = new();
    }

    public class PdfStampPreviewRequestDto
    {
        public string FilePath { get; set; } = string.Empty;

        public string? SealImagePath { get; set; }

        public string? WetSignatureImagePath { get; set; }

        public List<MergeStampDto> Stamps { get; set; } = new();
    }
}
