using ktl.be.applications.Document.Dtos.DocumentMerge;
using ktl.be.applications.Document.Dtos.Stamp;

namespace ktl.be.applications.Document.Interfaces
{
    public interface IStampPlanner
    {
        List<StampRuleDto> BuildRules(string? sealImagePath, string? wetSignatureImagePath, List<MergeStampDto>? stamps);

        StampPlanDto Plan(IReadOnlyList<StampRuleDto> rules, int pageCount);
    }
}
