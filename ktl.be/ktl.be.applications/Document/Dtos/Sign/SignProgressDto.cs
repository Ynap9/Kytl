using ktl.be.shared.Requests;

namespace ktl.be.applications.Document.Dtos.Sign
{
    public class SignProgressDto
    {
        public string TargetType { get; set; } = string.Empty;

        public string Target { get; set; } = string.Empty;

        public StatusCodeE Status { get; set; }

        public int Code { get; set; }

        public string Message { get; set; } = string.Empty;

        public bool Completed { get; set; }

        public int Current { get; set; }

        public int Total { get; set; }

        public int Percent => Total > 0 ? (int)System.Math.Round(Current * 100.0 / Total) : 0;

        public List<string>? Files { get; set; }

        public SignFileResultDto? File { get; set; }

        public SignResultDto? Summary { get; set; }
    }
}
