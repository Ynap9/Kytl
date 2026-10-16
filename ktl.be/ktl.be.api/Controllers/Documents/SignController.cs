using Asp.Versioning;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using ktl.be.api.Controllers.Base;
using ktl.be.applications.Document.Dtos.Sign;
using ktl.be.infrastructure.external.Documents.Dtos;
using ktl.be.applications.Document.Interfaces;
using ktl.be.shared.Constants;
using ktl.be.shared.Requests;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ktl.be.api.Controllers.Documents
{
    [ApiVersion(ApiVersionConstants.V1)]
    [Route(ApiVersionConstants.RoutePrefix + "/core/document/sign")]
    [ApiController]
    public class SignController : BaseController
    {
        private readonly ISignService _signService;
        public SignController(ISignService signService, ILogger<SignController> logger) : base(logger)
        {
            _signService = signService;
        }

        [HttpGet("chung-thu-so")]
        public ApiResponse GetCertificates([FromQuery] SignCertQueryDto query)
        {
            try
            {
                var result = _signService.GetCertificates(query);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpPost("kiem-tra-token")]
        public ApiResponse VerifyToken([FromBody] SignRequestDto input)
        {
            try
            {
                var result = _signService.VerifyToken(input.Thumbprint);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpPost("kiem-tra-chu-ky")]
        public ApiResponse VerifySignature([FromBody] SignatureCheckRequestDto input)
        {
            try
            {
                var result = _signService.VerifySignature(input.FilePath);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpGet("xem-truoc")]
        public ApiResponse GetPreview([FromQuery] string sourcePath, [FromQuery] int index = 0)
        {
            try
            {
                var result = _signService.GetPreviewAt(sourcePath, index);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpGet("xem-file")]
        public IActionResult ViewPdf([FromQuery] string path)
        {
            try
            {
                var stream = _signService.OpenPdfForPreview(path);
                return File(stream, "application/pdf");
            }
            catch (Exception)
            {
                return NotFound();
            }
        }

        [HttpPost("danh-sach-file")]
        public ApiResponse GetFiles(SignSourceRequestDto input)
        {
            try
            {
                var result = _signService.ListPdfFiles(input);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpPost("kiem-tra-vi-tri")]
        public ApiResponse CheckPositions(SignPositionCheckRequestDto input)
        {
            try
            {
                var result = _signService.CheckPositions(input);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpPost("ky-thu-muc")]
        public async Task SignFolder(SignRequestDto input)
        {
            IAsyncEnumerable<SignProgressDto> progresses;
            try
            {
                progresses = _signService.SignAsync(input, HttpContext.RequestAborted);
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
