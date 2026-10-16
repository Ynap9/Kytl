using ktl.be.domain.Documents;

namespace ktl.be.applications.Document.Dtos.SignTemplate
{
    public class SignTemplatePositionDto
    {
        public SignTemplatePositionKind Kind { get; set; }

        public int PageNumber { get; set; } = 1;

        public double XRatio { get; set; }

        public double YRatio { get; set; }

        public double WidthRatio { get; set; }

        public double HeightRatio { get; set; }
    }
}
