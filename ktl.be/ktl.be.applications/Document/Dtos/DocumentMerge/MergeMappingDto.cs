using ktl.be.infrastructure.external.Documents.Dtos;

namespace ktl.be.applications.Document.Dtos.DocumentMerge
{
    public class MergeMappingDto
    {
        public string TemplatePath { get; set; } = string.Empty;

        public string DataPath { get; set; } = string.Empty;

        public int TotalRows { get; set; }

        public List<ExcelHeaderDto> Headers { get; set; } = new();

        public List<MergeFieldMappingDto> Fields { get; set; } = new();
    }
}
