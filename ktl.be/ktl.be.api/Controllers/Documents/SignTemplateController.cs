using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ktl.be.api.Controllers.Base;
using ktl.be.applications.Document.Dtos.SignTemplate;
using ktl.be.applications.Document.Interfaces;
using ktl.be.shared.Constants;
using ktl.be.shared.Requests;
using ktl.be.shared.Requests.BaseRequest;

namespace ktl.be.api.Controllers.Documents
{
    [ApiVersion(ApiVersionConstants.V1)]
    [Route(ApiVersionConstants.RoutePrefix + "/core/document/template-chu-ky")]
    [ApiController]
    public class SignTemplateController : BaseController
    {
        private readonly ISignTemplateService _signTemplateService;

        public SignTemplateController(
            ISignTemplateService signTemplateService,
            ILogger<SignTemplateController> logger) : base(logger)
        {
            _signTemplateService = signTemplateService;
        }

        [HttpPost]
        public async Task<ApiResponse> Create(AddSignTemplateDto dto)
        {
            try
            {
                var result = await _signTemplateService.CreateAsync(dto);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpPut]
        public async Task<ApiResponse> Update(UpdateSignTemplateDto dto)
        {
            try
            {
                var result = await _signTemplateService.UpdateAsync(dto);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<ApiResponse> Delete(int id)
        {
            try
            {
                await _signTemplateService.DeleteAsync(id);
                return new();
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpGet("file-mau")]
        public ApiResponse GetSampleFile()
        {
            try
            {
                var result = _signTemplateService.GetSampleFile();
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpGet("xem-anh")]
        public IActionResult ViewImage([FromQuery] string path)
        {
            try
            {
                var (stream, contentType) = _signTemplateService.OpenImageForPreview(path);
                return File(stream, contentType);
            }
            catch (Exception)
            {
                return NotFound();
            }
        }

        [HttpGet("find-paging")]
        public async Task<ApiResponse> FindPaging([FromQuery] BaseRequestPagingDto input)
        {
            try
            {
                var result = await _signTemplateService.FindPagingAsync(input);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpGet("{id:int}")]
        public async Task<ApiResponse> GetById(int id)
        {
            try
            {
                var result = await _signTemplateService.GetByIdAsync(id);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }
    }
}
