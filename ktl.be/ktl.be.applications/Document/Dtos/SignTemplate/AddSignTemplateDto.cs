namespace ktl.be.applications.Document.Dtos.SignTemplate
{
    public class AddSignTemplateDto
    {
        public string TenTemplate { get; set; } = string.Empty;

        public string Thumbprint { get; set; } = string.Empty;

        public string? TenChungThu { get; set; }

        public string? LyDoKy { get; set; }

        public string? NoiKy { get; set; }

        public string? AnhDauDoPath { get; set; }

        public string? AnhChuKyTuoiPath { get; set; }

        public string? FontChuKySo { get; set; }

        public double? CoChuKySo { get; set; }

        public string? MauChuKySo { get; set; }

        public List<SignTemplatePositionDto> Positions { get; set; } = new();
    }
}
