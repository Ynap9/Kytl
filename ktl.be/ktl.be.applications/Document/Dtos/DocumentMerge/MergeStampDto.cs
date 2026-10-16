namespace ktl.be.applications.Document.Dtos.DocumentMerge
{
    public enum MergeStampKind
    {
        Seal = 1,

        WetSignature = 2,
    }

    public enum MergeStampScope
    {
        CurrentPage = 0,

        Repeat = 1,
    }

    public enum MergeStampRepeatMode
    {
        All = 0,

        Even = 1,

        Odd = 2,

        Custom = 3,
    }

    public class MergeStampPositionDto
    {
        public int PageNumber { get; set; }

        public double XRatio { get; set; }

        public double YRatio { get; set; }

        public double WidthRatio { get; set; }

        public double HeightRatio { get; set; }
    }

    public class MergeStampDto
    {
        public MergeStampKind Kind { get; set; }

        public MergeStampScope Scope { get; set; }

        public MergeStampRepeatMode RepeatMode { get; set; }

        public string? CustomPages { get; set; }

        public int PageNumber { get; set; } = 1;

        public double XRatio { get; set; }

        public double YRatio { get; set; }

        public double WidthRatio { get; set; }

        public double HeightRatio { get; set; }

        public List<MergeStampPositionDto> PagePositions { get; set; } = new();
    }
}
