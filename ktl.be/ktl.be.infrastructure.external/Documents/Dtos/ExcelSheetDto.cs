namespace ktl.be.infrastructure.external.Documents.Dtos
{
    public class ExcelHeaderDto
    {
        public int ColumnNumber { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    public class ExcelRowDto
    {
        public int RowNumber { get; set; }

        public Dictionary<int, string> Values { get; set; } = new();
    }

    public class ExcelSheetDto
    {
        public List<ExcelHeaderDto> Headers { get; set; } = new();

        public List<ExcelRowDto> Rows { get; set; } = new();
    }
}
