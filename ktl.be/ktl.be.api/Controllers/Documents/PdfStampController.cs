using Asp.Versioning;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using ktl.be.api.Controllers.Base;
using ktl.be.applications.Document.Dtos.PdfStamp;
using ktl.be.applications.Document.Interfaces;
using ktl.be.shared.Constants;
using ktl.be.shared.Requests;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ktl.be.api.Controllers.Documents
{
    [ApiVersion(ApiVersionConstants.V1)]
    [Route(ApiVersionConstants.RoutePrefix + "/core/document/dau-chu-ky")]
    [ApiController]
    public class PdfStampController : BaseController
    {
        private readonly IPdfStampService _pdfStampService;

        public PdfStampController(IPdfStampService pdfStampService, ILogger<PdfStampController> logger) : base(logger)
        {
            _pdfStampService = pdfStampService;
        }

        [HttpPost("danh-sach-file")]
        public ApiResponse GetFiles(PdfStampSourceRequestDto input)
        {
            try
            {
                var result = _pdfStampService.ListFiles(input);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpPost("xem-truoc")]
        public ApiResponse Preview(PdfStampPreviewRequestDto input)
        {
            try
            {
                var result = _pdfStampService.Preview(input);
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
                var stream = _pdfStampService.OpenPdf(path);
                return File(stream, "application/pdf");
            }
            catch (Exception)
            {
                return NotFound();
            }
        }

        [HttpPost("chen")]
        public async Task Stamp(PdfStampRequestDto input)
        {
            IAsyncEnumerable<PdfStampProgressDto> progresses;
            try
            {
                progresses = _pdfStampService.StampAsync(input, HttpContext.RequestAborted);
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
