namespace ktl.be.infrastructure.external.Documents.Dtos
{
    public class TokenVerifyDto
    {
        public bool Valid { get; set; }

        public string Thumbprint { get; set; } = string.Empty;

        public string? CommonName { get; set; }

        public string? IssuerCommonName { get; set; }

        public bool FoundInStore { get; set; }

        public bool HasPrivateKey { get; set; }

        public bool NotExpired { get; set; }

        public bool AllowsSigning { get; set; }

        public bool CanSignTest { get; set; }

        public bool TrustedChain { get; set; }

        public string? FailedCheck { get; set; }

        public string? Reason { get; set; }
    }
}
