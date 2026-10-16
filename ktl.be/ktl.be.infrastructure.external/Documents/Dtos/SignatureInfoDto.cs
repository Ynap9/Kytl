namespace ktl.be.infrastructure.external.Documents.Dtos
{
    public class SignatureInfoDto
    {
        public bool IsSigned { get; set; }

        public int SignatureCount { get; set; }

        public List<SignerInfoDto> Signers { get; set; } = new();

        public string? Error { get; set; }

        public TimestampVerifyDto? Timestamp { get; set; }
    }
}
