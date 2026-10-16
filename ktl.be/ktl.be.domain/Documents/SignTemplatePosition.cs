namespace ktl.be.domain.Documents
{
    public enum SignTemplatePositionKind
    {
        ChuKy = 0,

        DauDo = 1,

        ChuKyTuoi = 2,
    }

    public class SignTemplatePosition
    {
        public int Id { get; set; }

        public int SignTemplateId { get; set; }
        public SignTemplate? SignTemplate { get; set; }

        public SignTemplatePositionKind Kind { get; set; }

        public int PageNumber { get; set; } = 1;

        public double XRatio { get; set; }

        public double YRatio { get; set; }

        public double WidthRatio { get; set; }

        public double HeightRatio { get; set; }
    }
}
