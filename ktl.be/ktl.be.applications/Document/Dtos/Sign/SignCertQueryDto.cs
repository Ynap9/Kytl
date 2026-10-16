using ktl.be.infrastructure.external.Documents.Dtos;

namespace ktl.be.applications.Document.Dtos.Sign
{
    public class SignCertQueryDto
    {
        public bool OnlySignable { get; set; } = false;
    }
}
