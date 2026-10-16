using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ktl.be.api.Controllers.Base;
using ktl.be.applications.Document.Interfaces;
using ktl.be.shared.Constants;
using ktl.be.shared.Requests;

namespace ktl.be.api.Controllers.Documents
{
    [ApiVersion(ApiVersionConstants.V1)]
    [Route(ApiVersionConstants.RoutePrefix + "/core/document/tai-len")]
    [ApiController]
    public class FileUploadController : BaseController
    {
        private readonly IFileUploadService _fileUploadService;

        public FileUploadController(IFileUploadService fileUploadService,
            ILogger<FileUploadController> logger) : base(logger)
        {
            _fileUploadService = fileUploadService;
        }

        [HttpPost]
        [RequestSizeLimit(FileUploadConstants.MaxFileBytes + 1024 * 1024)]
        public async Task<ApiResponse> Upload(IFormFile file)
        {
            try
            {
                await using var content = file.OpenReadStream();
                var result = await _fileUploadService.SaveAsync(file.FileName, content, file.Length,
                    HttpContext.RequestAborted);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }
    }
}
