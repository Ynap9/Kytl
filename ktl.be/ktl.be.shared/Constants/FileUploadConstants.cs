using ktl.be.shared.Constants.Documents;

namespace ktl.be.shared.Constants
{
    public static class FileUploadConstants
    {
        public const string FolderName = "tai-len";
        public const int RetentionHours = 24;
        public const long MaxFileBytes = 50 * 1024 * 1024;

        public static readonly string[] AllowedExtensions = { ".docx", ".xlsx", ".pdf", ".png", ".jpg", ".jpeg" };

        public static string UploadDirectory => Path.Combine(Path.GetTempPath(),
            SignTemplateConstants.AppDataFolderName, FolderName);
    }
}
