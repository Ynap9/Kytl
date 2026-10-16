using ktl.be.shared.Requests;
using ktl.be.shared.Requests.AppException;
using ktl.be.shared.Requests.ErrorRequest;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ktl.be.api.Controllers.Base
{
    [ApiController]
    public class BaseController : ControllerBase
    {
        private readonly ILogger<BaseController> _logger;

        public BaseController(ILogger<BaseController> logger)
        {
            _logger = logger;
        }

        [NonAction]
        public ApiResponse OkException(Exception ex)
        {
            var request = HttpContext.Request;
            string errStr = $"Path = {request.Path}, Query = {JsonSerializer.Serialize(request.Query)}";
            int errorCode;
            string message = ex.Message;
            object? data = null;

            if (ex is UserFriendlyException userFriendlyException)
            {
                errorCode = userFriendlyException.ErrorCode;
                try
                {
                    if (!string.IsNullOrWhiteSpace(userFriendlyException.MessageLocalize))
                    {
                        message = userFriendlyException.MessageLocalize;
                    }
                    else
                    {
                        message = ErrorMessages.GetMessage(errorCode);
                    }
                }
                catch (Exception exc)
                {
                    message = exc.Message;
                }
            }
            else if (ex is InvalidOperationException invalidOperationException)
            {
                errorCode = ErrorCodes.InternalServerError;
                message = invalidOperationException.InnerException?.Message ?? invalidOperationException.Message;
            }
            else
            {
                errorCode = ErrorCodes.InternalServerError;
                _logger.LogError(ex, $"{ex.GetType()}: {errStr}, ErrorCode = {errorCode}, Message = {message}");
                message = "Internal server error";
                return new ApiResponse(StatusCodeE.Error, data, errorCode, message);
            }

            _logger.LogError(ex, $"{ex.GetType()}: {errStr}, ErrorCode = {errorCode}, Message = {message}");
            return new ApiResponse(StatusCodeE.Error, data, errorCode, message);
        }
    }
}
