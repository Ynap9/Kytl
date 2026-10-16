namespace ktl.be.applications.Document.Dtos.Sign
{
    public class SignPreviewDto
    {
        public int Index { get; set; }

        public int Total { get; set; }

        public bool HasPrev { get; set; }

        public bool HasNext { get; set; }

        public List<SignFileItemDto> Files { get; set; } = new();
    }
}
