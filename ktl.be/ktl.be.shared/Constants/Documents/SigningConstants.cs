namespace ktl.be.shared.Constants.Documents
{
    public static class SigningConstants
    {
        public const string TsaUrl = "http://tsa.ca.gov.vn/";
        public const string TsaContentType = "application/timestamp-query";
        public const string TsaReplyContentType = "application/timestamp-reply";

        public const int TsaTimeoutSeconds = 180;

        public const string OidSigningCertificateV2 = "1.2.840.113549.1.9.16.2.47";

        public const string PdfSubFilter = "ETSI.CAdES.detached";
        public const string DefaultReason = "Ký số tài liệu";
        public const string DefaultLocation = "Việt Nam";

        public const double AppearanceWidth = 170;
        public const double AppearanceHeight = 30;
        public const string AppearanceFontFamily = "Times New Roman";
        public const double AppearanceFontSize = 10;
        public const double AppearanceMinFontSize = 6;
        public const double AppearanceMaxFontSize = 24;
        public const string AppearanceTextColor = "#000000";
        public static readonly string[] AppearanceFontFamilies =
            { "Times New Roman", "Arial", "Verdana", "Georgia", "Courier New" };
        public const string AppearanceTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffffff'+07:00'";

        public const double AppearanceReferencePageWidth = 595;
        public const double AppearanceReferencePageHeight = 842;

        public const double AppearancePageMargin = 8;

        public const string SignedFileSuffix = "_daky_";
        public const string SignedFileTimeFormat = "yyyyMMdd_HHmmss";
        public const string SignedFileNamePattern = SignedFileSuffix + @"\d{8}_\d{6}$";

        public const int MinDegreeOfParallelism = 2;
        public const int MaxDegreeOfParallelism = 8;
        public const int MaxTsaConcurrency = MaxDegreeOfParallelism;

        public const int PreflightTestDataSize = 32;

        public const int TokenMonitorIntervalSeconds = 2;

        public static readonly string[] HardwareKeyProviderMarkers =
        {
            "smart card",
            "bit4id",
            "vgca",
            "token",
            "etoken",
            "safenet",
            "gemalto",
            "thales",
            "epass",
            "feitian",
        };

        public static readonly string[] SoftwareKeyProviderMarkers =
        {
            "microsoft software key storage provider",
            "microsoft enhanced cryptographic provider",
            "microsoft base cryptographic provider",
            "microsoft strong cryptographic provider",
            "microsoft enhanced rsa and aes cryptographic provider",
        };
    }
}
