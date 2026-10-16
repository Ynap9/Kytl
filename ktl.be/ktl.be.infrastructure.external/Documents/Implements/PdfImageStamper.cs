using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using ktl.be.infrastructure.external.Documents.Dtos;
using ktl.be.infrastructure.external.Documents.Interfaces;
using ktl.be.shared.Requests.AppException;
using ktl.be.shared.Requests.ErrorRequest;

namespace ktl.be.infrastructure.external.Documents.Implements
{
    public class PdfImageStamper : IPdfImageStamper
    {
        public int CountPages(byte[] pdf)
        {
            using var input = new MemoryStream(pdf, writable: false);
            using var document = Open(input, PdfDocumentOpenMode.Import);
            return document.PageCount;
        }

        public byte[] Stamp(byte[] pdf, IReadOnlyList<PdfImageStampDto> stamps)
        {
            using var input = new MemoryStream(pdf, writable: false);
            using var document = Open(input, PdfDocumentOpenMode.Modify);

            var images = new Dictionary<byte[], XImage>(ReferenceEqualityComparer.Instance);
            try
            {
                foreach (var group in stamps.GroupBy(s => s.PageNumber))
                {
                    if (group.Key < 1 || group.Key > document.PageCount)
                    {
                        throw new UserFriendlyException(ErrorCodes.MergeStampInvalid,
                            $"Trang {group.Key} không có trong file PDF ({document.PageCount} trang).");
                    }

                    var page = document.Pages[group.Key - 1];
                    var pageWidth = page.Width.Point;
                    var pageHeight = page.Height.Point;

                    using var graphics = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                    foreach (var stamp in group)
                    {
                        if (!images.TryGetValue(stamp.Image, out var image))
                        {
                            image = LoadImage(stamp.Image);
                            images[stamp.Image] = image;
                        }

                        graphics.DrawImage(image,
                            stamp.XRatio * pageWidth,
                            stamp.YRatio * pageHeight,
                            stamp.WidthRatio * pageWidth,
                            stamp.HeightRatio * pageHeight);
                    }
                }

                using var output = new MemoryStream();
                document.Save(output, false);
                return output.ToArray();
            }
            finally
            {
                foreach (var image in images.Values) image.Dispose();
            }
        }

        private static PdfDocument Open(Stream input, PdfDocumentOpenMode mode)
        {
            try
            {
                return PdfReader.Open(input, mode);
            }
            catch (Exception ex)
            {
                throw new UserFriendlyException(ErrorCodes.MergeFailed, $"Không đọc được file PDF: {ex.Message}");
            }
        }

        private static XImage LoadImage(byte[] image)
        {
            try
            {
                return XImage.FromStream(new MemoryStream(image, writable: false));
            }
            catch (Exception ex)
            {
                throw new UserFriendlyException(ErrorCodes.SignTemplateImageInvalid,
                    $"Không đọc được ảnh để đóng lên PDF: {ex.Message}");
            }
        }
    }
}
