namespace ktl.be.infrastructure.external.Documents.Dtos
{
    public class TimestampVerifyDto
    {
        public TimestampVerdict Verdict { get; set; }

        public bool HasTimestamp { get; set; }

        public string? TimestampAuthority { get; set; }

        public string? SignedBy { get; set; }

        public string? TrustedTime { get; set; }

        public string? SigningTime { get; set; }

        public string? Reason { get; set; }

        public List<SignatureDetailDto> Signatures { get; set; } = new();

        public int ValidSignatureCount { get; set; }

        public int SignatureBlockCount { get; set; }

        public int SkippedBlocks { get; set; }

        public bool ReadFailed { get; set; }

        public bool IsValid => Verdict == TimestampVerdict.ValidTrusted;

        public bool Blocks => Verdict != TimestampVerdict.ValidTrusted && Verdict != TimestampVerdict.NotApplicable;
    }
}
