namespace ktl.be.shared.Requests.AppException
{
    public class UserFriendlyException : BaseException
    {
        public UserFriendlyException(int errorCode) : base(errorCode)
        {
        }

        public UserFriendlyException(int errorCode, string? message) : base(errorCode, message)
        {
        }
    }
}
