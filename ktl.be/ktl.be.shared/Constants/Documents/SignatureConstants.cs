namespace ktl.be.shared.Constants.Documents
{
    public static class SignatureConstants
    {
        public const string CertificateFolderName = "certs";

        public const string RootG2Sha256 = "F04BA4B459A7C9A1B971AD9B1CB833E695A80C79AEA63B174B66A70EDFE2FDB8";

        public const string RootG2FileName = "rootcag2.crt";

        public static readonly IReadOnlyDictionary<string, string> IntermediateCaPins =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["dcscag2.crt"] = "E4B402E3CB4CACC0A20D21FA49BCE76BEB4EB47BA7FFB7A8F71C554DBE561A81",
                ["cpg2.crt"] = "D8CAB7A4AEF92E6BB2509D7C61CEE7B44F3731B2D21074963530277B366FAE4B",
            };

        public const string RootG1Sha256 = "F7BC89054CEC7F12730D7B8CD3EB7955B163835087E5EB873EDFB584763B3F90";
        public const string RootG1FileName = "rootca.crt";

        public const string SubCaG1FileName = "cp.crt";
        public const string SubCaG1Sha256 = "75E969C07BA35C29C72BBC1BFB880F20F3891A619B00B77EB02CA6160736A1FE";

        public const string OidSignatureTimeStampToken = "1.2.840.113549.1.9.16.2.14";
        public const string OidSigningTime = "1.2.840.113549.1.9.5";
        public const string OidEkuTimeStamping = "1.3.6.1.5.5.7.3.8";

        public const int SignerLocalTimeQuirkHours = 7;

        public const int SignerLocalTimeQuirkWindowSeconds = 3600;

        public const int TsaClockSkewGraceSeconds = 60;
    }
}
