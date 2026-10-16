namespace ktl.be.infrastructure.external.Documents.Dtos
{
    public class SignCertDto
    {
        public string Subject { get; set; } = string.Empty;

        public string CommonName { get; set; } = string.Empty;

        public string Issuer { get; set; } = string.Empty;

        public string IssuerCommonName { get; set; } = string.Empty;

        public string SerialNumber { get; set; } = string.Empty;

        public string Thumbprint { get; set; } = string.Empty;

        public CertSource Source { get; set; }

        public string? KeyProvider { get; set; }

        public string ValidFrom { get; set; } = string.Empty;

        public string ValidTo { get; set; } = string.Empty;

        public bool HasPrivateKey { get; set; }

        public bool IsExpired { get; set; }

        public bool AllowsSigning { get; set; }

        public bool IsTrusted { get; set; }

        public bool CanSign => HasPrivateKey && !IsExpired && AllowsSigning && IsTrusted;

        public string? Reason { get; set; }
    }
}
