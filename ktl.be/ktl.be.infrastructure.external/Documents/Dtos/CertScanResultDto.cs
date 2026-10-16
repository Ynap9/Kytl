namespace ktl.be.infrastructure.external.Documents.Dtos
{
    public class CertScanResultDto
    {
        public List<SignCertDto> Certificates { get; set; } = [];

        public List<string> StoreDiagnostics { get; set; } = [];
    }
}
