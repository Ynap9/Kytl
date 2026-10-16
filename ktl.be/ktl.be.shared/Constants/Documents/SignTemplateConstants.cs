namespace ktl.be.shared.Constants.Documents
{
    public static class SignTemplateConstants
    {
        public const string AppDataFolderName = "KTL";
        public const string DatabaseFileName = "sign-templates.db";
        public const string MachineBindingFileName = "sign-templates.machine";
        public const string MachineGuidRegistryPath = @"SOFTWARE\Microsoft\Cryptography";
        public const string MachineGuidValueName = "MachineGuid";

        public const string AssetFolderName = "template-assets";

        public const string SamplePdfRelativePath = @"Templates\sample\file-mau-ky-so.pdf";

        public static readonly string[] AllowedImageExtensions = { ".png", ".jpg", ".jpeg" };

        public const long MaxImageBytes = 5 * 1024 * 1024;

        public const int DefaultPageNumber = 1;
        public const int DefaultPageSize = 10;
        public const int MaxPageSize = 200;

        public static string AppDataDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataFolderName);

        public static string DatabasePath => Path.Combine(AppDataDirectory, DatabaseFileName);

        public static string MachineBindingPath => Path.Combine(AppDataDirectory, MachineBindingFileName);

        public static string AssetDirectory => Path.Combine(AppDataDirectory, AssetFolderName);

        public static string SamplePdfPath => Path.Combine(AppContext.BaseDirectory, SamplePdfRelativePath);
    }
}
