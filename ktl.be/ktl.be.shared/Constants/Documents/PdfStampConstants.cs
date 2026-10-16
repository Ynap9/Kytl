namespace ktl.be.shared.Constants.Documents
{
    public static class PdfStampConstants
    {
        public const string PdfExtension = ".pdf";

        public const string TempFolderName = "dau-chu-ky";
        public const string PreviewFolderName = "xem-truoc";
        public const int PreviewRetentionMinutes = 60;

        public const string OutputSuffix = "_dauky_";
        public const string OutputTimeFormat = "yyyyMMdd_HHmmss";
        public const string OutputFileNamePattern = OutputSuffix + @"\d{8}_\d{6}$";
    }
}
