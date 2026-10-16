namespace ktl.be.infrastructure.external.Documents.Interfaces
{
    public interface IWordPdfConverter
    {
        bool IsAvailable();

        IWordPdfSession OpenSession();
    }

    public interface IWordPdfSession : IDisposable
    {
        void Convert(string docxPath, string pdfPath);
    }
}
