namespace ktl.be.applications.Document.Dtos.DocumentMerge
{
    public class MergeRequestDto
    {
        public string TemplatePath { get; set; } = string.Empty;

        public string DataPath { get; set; } = string.Empty;

        public List<MergeFieldMappingDto> Mappings { get; set; } = new();

        public int? FileNameColumnNumber { get; set; }

        public string? OutputFolder { get; set; }

        public string? SealImagePath { get; set; }

        public string? WetSignatureImagePath { get; set; }

        public List<MergeStampDto> Stamps { get; set; } = new();
    }
}
