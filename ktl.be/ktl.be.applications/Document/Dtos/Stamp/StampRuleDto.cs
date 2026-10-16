using ktl.be.applications.Document.Dtos.DocumentMerge;

namespace ktl.be.applications.Document.Dtos.Stamp
{
    public sealed class StampAreaDto
    {
        public double XRatio { get; init; }

        public double YRatio { get; init; }

        public double WidthRatio { get; init; }

        public double HeightRatio { get; init; }
    }

    public sealed class StampRuleDto
    {
        public MergeStampKind Kind { get; init; }

        public MergeStampScope Scope { get; init; }

        public MergeStampRepeatMode RepeatMode { get; init; }

        public List<(int From, int To)> CustomPages { get; init; } = new();

        public int PageNumber { get; init; }

        public StampAreaDto Area { get; init; } = new();

        public Dictionary<int, StampAreaDto> PageAreas { get; init; } = new();

        public byte[] Image { get; init; } = Array.Empty<byte>();
    }
}
