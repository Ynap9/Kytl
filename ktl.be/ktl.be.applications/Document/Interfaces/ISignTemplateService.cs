using ktl.be.applications.Document.Dtos.SignTemplate;
using ktl.be.shared.Requests.BaseRequest;

namespace ktl.be.applications.Document.Interfaces
{
    public interface ISignTemplateService
    {
        Task<ViewSignTemplateDto> CreateAsync(AddSignTemplateDto input);

        Task<ViewSignTemplateDto> UpdateAsync(UpdateSignTemplateDto input);

        Task DeleteAsync(int id);

        Task<ViewSignTemplateDto> GetByIdAsync(int id);

        Task<BaseResponsePagingDto<ViewSignTemplateDto>> FindPagingAsync(BaseRequestPagingDto input);

        SampleFileDto GetSampleFile();

        (Stream Stream, string ContentType) OpenImageForPreview(string path);
    }
}
