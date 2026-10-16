namespace ktl.be.shared.Requests.AppException
{
    public class BaseException : Exception
    {
        public readonly int ErrorCode;
        public readonly string? MessageLocalize;
        public string? ErrorMessage;
        public string[]? ListParam;

        public BaseException(int errorCode) : base()
        {
            ErrorCode = errorCode;
        }

        public BaseException(int errorCode, string? messageLocalize) : base(messageLocalize)
        {
            ErrorCode = errorCode;
            MessageLocalize = messageLocalize;
        }

        public BaseException(int errorCode, params string[] listParam) : base()
        {
            ErrorCode = errorCode;
            ListParam = listParam;
        }
    }
}
