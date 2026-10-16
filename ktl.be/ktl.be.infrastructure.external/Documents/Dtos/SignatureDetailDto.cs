namespace ktl.be.infrastructure.external.Documents.Dtos
{
    public class SignatureDetailDto
    {
        public string? SignedBy { get; set; }

        public TimestampVerdict Verdict { get; set; }

        public string? Reason { get; set; }

        public string? TrustedTime { get; set; }

        public string? SigningTime { get; set; }
    }
}
