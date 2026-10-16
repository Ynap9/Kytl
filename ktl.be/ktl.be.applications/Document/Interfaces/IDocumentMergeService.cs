using ktl.be.applications.Document.Dtos.DocumentMerge;
using ktl.be.infrastructure.external.Documents.Dtos;

namespace ktl.be.applications.Document.Interfaces
{
    public interface IDocumentMergeService
    {
        List<ExcelHeaderDto> GetHeaders(string dataPath);

        MergeMappingDto AutoMap(string templatePath, string dataPath);

        MergeValidationDto Validate(MergeRequestDto request);

        MergePreviewDto Preview(MergePreviewRequestDto request);

        Stream OpenPreview(string filePath);

        IAsyncEnumerable<MergeProgressDto> MergeAsync(MergeRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
