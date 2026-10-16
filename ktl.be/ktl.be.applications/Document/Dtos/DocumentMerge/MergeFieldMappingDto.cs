namespace ktl.be.applications.Document.Dtos.DocumentMerge
{
    public class MergeFieldMappingDto
    {
        public string FieldName { get; set; } = string.Empty;

        public int? ColumnNumber { get; set; }

        public string? HeaderName { get; set; }

        public bool AutoMapped { get; set; }
    }
}
