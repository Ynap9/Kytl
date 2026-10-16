namespace ktl.be.infrastructure.external.Documents.Dtos
{
    public enum TimestampVerdict
    {
        ValidTrusted = 0,
        NoTimestamp = 1,
        UntrustedTsa = 2,
        TimeMismatch = 3,
        NotApplicable = 4,
        UntrustedSigner = 5,
        InvalidSignature = 6
    }
}
