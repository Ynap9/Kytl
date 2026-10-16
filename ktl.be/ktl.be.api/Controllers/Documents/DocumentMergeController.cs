using Asp.Versioning;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using ktl.be.api.Controllers.Base;
using ktl.be.applications.Document.Dtos.DocumentMerge;
using ktl.be.applications.Document.Interfaces;
using ktl.be.shared.Constants;
using ktl.be.shared.Requests;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ktl.be.api.Controllers.Documents
{
    [ApiVersion(ApiVersionConstants.V1)]
    [Route(ApiVersionConstants.RoutePrefix + "/core/document/tao-van-ban")]
    [ApiController]
    public class DocumentMergeController : BaseController
    {
        private readonly IDocumentMergeService _documentMergeService;

        public DocumentMergeController(IDocumentMergeService documentMergeService,
            ILogger<DocumentMergeController> logger) : base(logger)
        {
            _documentMergeService = documentMergeService;
        }

        [HttpGet("danh-sach-header")]
        public ApiResponse GetHeaders([FromQuery] string dataPath)
        {
            try
            {
                var result = _documentMergeService.GetHeaders(dataPath);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpGet("map-tu-dong")]
        public ApiResponse AutoMap([FromQuery] string templatePath, [FromQuery] string dataPath)
        {
            try
            {
                var result = _documentMergeService.AutoMap(templatePath, dataPath);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpPost("kiem-tra")]
        public ApiResponse Validate(MergeRequestDto input)
        {
            try
            {
                var result = _documentMergeService.Validate(input);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpPost("xem-truoc")]
        public ApiResponse Preview(MergePreviewRequestDto input)
        {
            try
            {
                var result = _documentMergeService.Preview(input);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpGet("xem-file")]
        public IActionResult ViewPreview([FromQuery] string path)
        {
            try
            {
                var stream = _documentMergeService.OpenPreview(path);
                return File(stream, "application/pdf");
            }
            catch (Exception)
            {
                return NotFound();
            }
        }

        [HttpGet("xem-anh")]
        public IActionResult ViewImage([FromQuery] string path)
        {
            try
            {
                var (stream, contentType) = _documentMergeService.OpenImage(path);
                return File(stream, contentType);
            }
            catch (Exception)
            {
                return NotFound();
            }
        }

        [HttpPost("sinh-file")]
        public async Task Merge(MergeRequestDto input)
        {
            IAsyncEnumerable<MergeProgressDto> progresses;
            try
            {
                progresses = _documentMergeService.MergeAsync(input, HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                await Response.WriteAsJsonAsync(OkException(ex), HttpContext.RequestAborted);
                return;
            }

            Response.Headers.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Connection = "keep-alive";

            HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            await foreach (var progress in progresses)
            {
                var json = JsonSerializer.Serialize(progress, jsonOptions);
                await Response.WriteAsync($"data: {json}\n\n", HttpContext.RequestAborted);
                await Response.Body.FlushAsync(HttpContext.RequestAborted);
            }
        }
    }
}
