using ktl.be.infrastructure.external.Documents.Interfaces;
using ktl.be.shared.Constants.Documents;
using ktl.be.shared.Requests.AppException;
using ktl.be.shared.Requests.ErrorRequest;
using System.Runtime.InteropServices;

namespace ktl.be.infrastructure.external.Documents.Implements
{
    public class WordPdfConverter : IWordPdfConverter
    {
        public bool IsAvailable()
        {
            return OperatingSystem.IsWindows() && Type.GetTypeFromProgID(DocumentMergeConstants.WordProgId) != null;
        }

        public IWordPdfSession OpenSession()
        {
            var type = OperatingSystem.IsWindows() ? Type.GetTypeFromProgID(DocumentMergeConstants.WordProgId) : null;
            if (type == null)
            {
                throw new UserFriendlyException(ErrorCodes.MergeConverterUnavailable,
                    "Máy chưa cài Microsoft Word nên không xuất được PDF.");
            }

            object? word = null;
            try
            {
                word = Activator.CreateInstance(type)!;
                return new WordPdfSession(word);
            }
            catch (Exception ex)
            {
                if (word != null && OperatingSystem.IsWindows()) Marshal.FinalReleaseComObject(word);
                throw new UserFriendlyException(ErrorCodes.MergeConverterUnavailable,
                    $"Không khởi động được Microsoft Word: {ex.Message}");
            }
        }

        private sealed class WordPdfSession : IWordPdfSession
        {
            private readonly dynamic _word;

            public WordPdfSession(object word)
            {
                _word = word;
                _word.Visible = false;
                _word.DisplayAlerts = DocumentMergeConstants.WordAlertsNone;
            }

            public void Convert(string docxPath, string pdfPath)
            {
                dynamic document = _word.Documents.Open(docxPath, false, true, false);
                try
                {
                    document.ExportAsFixedFormat(pdfPath, DocumentMergeConstants.WordExportFormatPdf);
                }
                finally
                {
                    document.Close(DocumentMergeConstants.WordDoNotSaveChanges);
                    if (OperatingSystem.IsWindows()) Marshal.FinalReleaseComObject(document);
                }
            }

            public void Dispose()
            {
                try
                {
                    _word.Quit(DocumentMergeConstants.WordDoNotSaveChanges);
                }
                catch
                {
                }

                if (OperatingSystem.IsWindows()) Marshal.FinalReleaseComObject(_word);
            }
        }
    }
}
