using ktl.be.infrastructure.external.Documents.Dtos;
using ktl.be.infrastructure.external.Documents.Interfaces;

namespace ktl.be.infrastructure.external.Documents.Implements
{
    public class PdfPageInspector : IPdfPageInspector
    {
        public PdfPageMetricsDto Read(string filePath)
        {
            var result = new PdfPageMetricsDto();
            try
            {
                foreach (var page in PdfRevisionInfo.Load(File.ReadAllBytes(filePath)).GetPages())
                {
                    result.Pages.Add(page.DisplayBox);
                }
                result.Readable = result.Pages.Count > 0;
            }
            catch
            {
                result.Readable = false;
                result.Pages.Clear();
            }
            return result;
        }
    }
}
