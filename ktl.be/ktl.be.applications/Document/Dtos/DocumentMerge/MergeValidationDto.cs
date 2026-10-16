namespace ktl.be.applications.Document.Dtos.DocumentMerge
{
    public class MergeRowCheckDto
    {
        public int RowNumber { get; set; }

        public string FileName { get; set; } = string.Empty;

        public List<string> Warnings { get; set; } = new();
    }

    public class MergeValidationDto
    {
        public int TotalRows { get; set; }

        public int MappedFieldCount { get; set; }

        public List<string> UnmappedFields { get; set; } = new();

        public List<string> Warnings { get; set; } = new();

        public int WarningRowCount { get; set; }

        public List<MergeRowCheckDto> Rows { get; set; } = new();
    }
}
