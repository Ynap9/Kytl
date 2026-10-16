using ktl.be.infrastructure.external.Documents.Dtos;
using ktl.be.infrastructure.external.Documents.Interfaces;

namespace ktl.be.applications.Document.Dtos.Sign
{
    public class SignFileResultDto
    {
        public string FileName { get; set; } = string.Empty;

        public string? OutputPath { get; set; }

        public bool Success { get; set; }

        public string? Reason { get; set; }

        public TimestampVerdict Verdict { get; set; }

        public string? VerifyReason { get; set; }

        public string? TrustedTime { get; set; }

        public string? SignedTime { get; set; }

        public int SignatureCount { get; set; }

        public int ValidSignatureCount { get; set; }

        public List<SignatureDetailDto> Signatures { get; set; } = new();
    }

    public class SignResultDto
    {
        public string OutputFolder { get; set; } = string.Empty;

        public int TotalFiles { get; set; }

        public int SuccessCount { get; set; }

        public int FailedCount { get; set; }
    }
}
