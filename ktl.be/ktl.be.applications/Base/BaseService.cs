using ktl.be.shared.Constants;
using Microsoft.Extensions.Logging;

namespace ktl.be.applications.Base
{
    public class BaseService
    {
        public readonly ILogger<BaseService> _logger;

        public BaseService(ILogger<BaseService> logger)
        {
            _logger = logger;
        }

        protected static DateTime GetVietnamTime()
        {
            return DateTimeConstants.VietnamNow;
        }
    }
}
