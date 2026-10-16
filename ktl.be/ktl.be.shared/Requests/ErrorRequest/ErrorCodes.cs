namespace ktl.be.shared.Requests.ErrorRequest
{
    public static class ErrorCodes
    {
        public const int System = 1;
        public const int BadRequest = 400;
        public const int NotFound = 404;
        public const int Found = 409;
        public const int InternalServerError = 500;

        public const int TimestampInvalid = 1025;
        public const int SignatureInvalid = 1026;
        public const int CertificateNotFound = 1027;
        public const int CertificateNotSignable = 1028;
        public const int SignSourceNotFound = 1029;
        public const int SignNoPdfFound = 1030;
        public const int SignTokenInvalid = 1031;
        public const int SignFailed = 1032;
        public const int SignPdfEncrypted = 1033;
        public const int SignPdfLocked = 1034;
        public const int SignPdfStructureUnsupported = 1035;
        public const int SignPositionInvalid = 1037;
        public const int SignTemplateNotFound = 1038;
        public const int SignTemplateNameDuplicated = 1039;
        public const int SignTemplateImageInvalid = 1040;
        public const int SignTemplateSampleMissing = 1041;
        public const int MergeTemplateInvalid = 1042;
        public const int MergeDataInvalid = 1043;
        public const int MergeMappingInvalid = 1044;
        public const int MergeConverterUnavailable = 1045;
        public const int MergeFailed = 1046;
        public const int MergeStampInvalid = 1047;
        public const int FileUploadInvalid = 1048;
        public const int PdfStampSourceInvalid = 1049;
        public const int PdfStampFailed = 1050;
    }
}
