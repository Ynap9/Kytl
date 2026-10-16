namespace ktl.be.applications.Document.Dtos.FileUpload
{
    public class UploadedFileDto
    {
        public string FileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public long SizeBytes { get; set; }
    }
}
