namespace ktl.be.shared.Constants.Documents
{
    public static class DocumentMergeConstants
    {
        public const string TemplateExtension = ".docx";
        public const string DataExtension = ".xlsx";
        public const string PdfExtension = ".pdf";

        public const string FieldPattern = @"\{([^{}]+)\}";

        public const string CellDateFormat = "dd/MM/yyyy";

        public const string OutputTimeFormat = "yyyyMMdd_HHmmss";
        public const int FileNameMaxLength = 100;

        public const string TempFolderName = "tao-van-ban";
        public const string PreviewFolderName = "xem-truoc";
        public const int PreviewRetentionMinutes = 60;

        public const string CustomPagePattern = @"^(\d+)(?:\s*-\s*(\d+))?$";
        public const char CustomPageSeparator = ',';

        public const string WordProgId = "Word.Application";
        public const int WordExportFormatPdf = 17;
        public const int WordDoNotSaveChanges = 0;
        public const int WordAlertsNone = 0;
    }
}
