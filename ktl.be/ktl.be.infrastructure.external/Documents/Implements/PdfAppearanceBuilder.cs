using PdfSharp.Drawing;
using PdfSharp.Pdf;
using ktl.be.infrastructure.external.Documents.Dtos;
using ktl.be.shared.Constants.Documents;
using ktl.be.shared.Requests.AppException;
using ktl.be.shared.Requests.ErrorRequest;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ktl.be.infrastructure.external.Documents.Implements
{
    public static class PdfAppearanceBuilder
    {
        public static (double Width, double Height) MeasureText(string line1, string line2, string fontFamily,
            double fontSize)
        {
            var font = new XFont(fontFamily, fontSize);
            using var gfx = XGraphics.CreateMeasureContext(new XSize(1, 1), XGraphicsUnit.Point,
                XPageDirection.Downwards);

            return (Math.Max(gfx.MeasureString(line1, font).Width, gfx.MeasureString(line2, font).Width),
                font.GetHeight() * 2);
        }

        public static AppearanceResult Build(string line1, string line2, int firstObjectNumber,
            double width, double height, string fontFamily, double fontSize, string textColor, int rotation)
        {
            var hex = (textColor ?? string.Empty).TrimStart('#');
            var color = hex.Length == 6 && int.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out var rgb)
                ? XColor.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF)
                : XColors.Black;

            return DrawAndTransplant(firstObjectNumber, width, height, rotation, gfx =>
            {
                var font = new XFont(fontFamily, fontSize);
                var brush = new XSolidBrush(color);
                var lineHeight = font.GetHeight();

                gfx.DrawString(line1, font, brush, new XRect(0, 0, width, lineHeight), XStringFormats.TopLeft);
                gfx.DrawString(line2, font, brush, new XRect(0, lineHeight, width, lineHeight),
                    XStringFormats.TopLeft);
            });
        }

        public static AppearanceResult BuildImage(byte[] image, int firstObjectNumber, double width, double height,
            int rotation, int darkness, int stroke, string? color)
        {
            using var stream = new MemoryStream(image);
            var xImage = XImage.FromStream(stream);

            var appearance = DrawAndTransplant(firstObjectNumber, width, height, rotation, gfx =>
            {
                var scale = Math.Min(width / xImage.PointWidth, height / xImage.PointHeight);
                var drawWidth = xImage.PointWidth * scale;
                var drawHeight = xImage.PointHeight * scale;
                var box = new XRect((width - drawWidth) / 2, (height - drawHeight) / 2, drawWidth, drawHeight);
                foreach (var layer in GetStrokeLayers(box, stroke))
                {
                    gfx.DrawImage(xImage, layer);
                }
            });

            return ApplyDarknessAndColor(appearance, darkness, color);
        }

        private static List<XRect> GetStrokeLayers(XRect box, int stroke)
        {
            var radius = box.Width * StampImageConstants.StrokeRadiusRatio * stroke / 100d;
            if (radius <= 0 || box.Width <= radius * 2 || box.Height <= radius * 2)
            {
                return new List<XRect> { box };
            }

            var core = new XRect(box.X + radius, box.Y + radius, box.Width - radius * 2, box.Height - radius * 2);
            var layers = Enumerable.Range(0, StampImageConstants.StrokeDirections)
                .Select(i =>
                {
                    var angle = 2 * Math.PI * i / StampImageConstants.StrokeDirections;
                    return new XRect(core.X + radius * Math.Cos(angle), core.Y + radius * Math.Sin(angle),
                        core.Width, core.Height);
                })
                .ToList();
            layers.Add(core);
            return layers;
        }

        private static AppearanceResult ApplyDarknessAndColor(AppearanceResult appearance, int darkness, string? color)
        {
            var factor = darkness / 100d;
            var ink = ParseColor(color);
            if (Math.Abs(factor - 1d) < 0.001 && ink == null)
            {
                return appearance;
            }

            var masks = appearance.Objects
                .SelectMany(o => Regex.Matches(o.DictText, @"/SMask\s+(\d+)\s+\d+\s+R").Select(m => int.Parse(m.Groups[1].Value)))
                .ToHashSet();

            var brightness = Math.Max(StampImageConstants.DarknessMinBrightness,
                1 - (factor - 1) * StampImageConstants.DarknessBrightnessFactor);
            var low = brightness * (1 - factor) / 2;
            var high = low + brightness * factor;

            var objects = appearance.Objects.Select(o => new TransplantedObject
            {
                Number = o.Number,
                DictText = masks.Contains(o.Number) ? o.DictText : AdjustImageDict(o.DictText, low, high, ink),
                StreamData = o.StreamData,
            }).ToList();

            return new AppearanceResult { Objects = objects, FormObjectNumber = appearance.FormObjectNumber };
        }

        private static string AdjustImageDict(string dict, double low, double high, (double R, double G, double B)? ink)
        {
            if (!Regex.IsMatch(dict, @"/Subtype\s*/Image")
                || Regex.IsMatch(dict, @"/ImageMask\s+true")
                || Regex.IsMatch(dict, @"/Decode\s*\["))
            {
                return dict;
            }

            var components = Regex.IsMatch(dict, @"/ColorSpace\s*/DeviceRGB") ? 3
                : Regex.IsMatch(dict, @"/ColorSpace\s*/DeviceGray") ? 1
                : Regex.IsMatch(dict, @"/ColorSpace\s*/DeviceCMYK") ? 4
                : 0;
            if (components == 0)
            {
                return dict;
            }

            var bitsMatch = Regex.Match(dict, @"/BitsPerComponent\s+(\d+)");
            var bits = bitsMatch.Success ? int.Parse(bitsMatch.Groups[1].Value) : 8;

            if (components == 1 && ink != null && bits <= 8)
            {
                return Regex.Replace(dict, @"/ColorSpace\s*/DeviceGray",
                    "/ColorSpace " + BuildIndexedPalette(low, high, (1 << bits) - 1, ink.Value));
            }

            var tint = ink != null && components == 3;
            var channels = new[] { ink?.R ?? 0d, ink?.G ?? 0d, ink?.B ?? 0d };
            var values = new List<string>();
            for (var i = 0; i < components; i++)
            {
                var c = tint ? channels[i] : 0d;
                values.Add(FormatUnit(c + (1 - c) * low));
                values.Add(FormatUnit(c + (1 - c) * high));
            }

            var close = dict.LastIndexOf(">>", StringComparison.Ordinal);
            return dict[..close] + "/Decode [" + string.Join(" ", values) + "]" + dict[close..];
        }

        private static string BuildIndexedPalette(double low, double high, int hival, (double R, double G, double B) ink)
        {
            var palette = new StringBuilder();
            for (var i = 0; i <= hival; i++)
            {
                var level = Math.Clamp(low + (double)i / hival * (high - low), 0d, 1d);
                foreach (var c in new[] { ink.R, ink.G, ink.B })
                {
                    var value = Math.Clamp(c + (1 - c) * level, 0d, 1d);
                    palette.Append(((int)Math.Round(value * 255)).ToString("X2", CultureInfo.InvariantCulture));
                }
            }

            return $"[/Indexed /DeviceRGB {hival} <{palette}>]";
        }

        private static (double R, double G, double B)? ParseColor(string? color)
        {
            if (string.IsNullOrWhiteSpace(color) || !Regex.IsMatch(color, StampImageConstants.ColorPattern))
            {
                return null;
            }

            var rgb = int.Parse(color[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return (((rgb >> 16) & 0xFF) / 255d, ((rgb >> 8) & 0xFF) / 255d, (rgb & 0xFF) / 255d);
        }

        private static string FormatUnit(double value) =>
            Math.Clamp(value, 0d, 1d).ToString("0.####", CultureInfo.InvariantCulture);

        private static AppearanceResult DrawAndTransplant(int firstObjectNumber, double width, double height,
            int rotation, Action<XGraphics> draw)
        {
            byte[] tempPdf;
            using (var document = new PdfDocument())
            {
                document.Options.CompressContentStreams = false;

                var page = document.AddPage();
                page.Width = XUnit.FromPoint(width);
                page.Height = XUnit.FromPoint(height);

                using (var gfx = XGraphics.FromPdfPage(page))
                {
                    draw(gfx);
                }

                using var ms = new MemoryStream();
                document.Save(ms, closeStream: false);
                tempPdf = ms.ToArray();
            }

            var temp = PdfRevisionInfo.Load(tempPdf);

            var catalog = temp.GetObjectBody(temp.RootObjectNumber)
                ?? throw new UserFriendlyException(ErrorCodes.SignFailed,
                    "Không đọc được Catalog của tài liệu khối chữ ký tạm.");
            var pagesNo = Regex.Match(catalog, @"/Pages\s+(\d+)\s+\d+\s+R");
            var pagesBody = pagesNo.Success ? temp.GetObjectBody(int.Parse(pagesNo.Groups[1].Value)) : null;
            var kid = Regex.Match(pagesBody ?? "", @"/Kids\s*\[\s*(\d+)");
            var pageBody = kid.Success ? temp.GetObjectBody(int.Parse(kid.Groups[1].Value)) : null;
            if (pageBody == null)
            {
                throw new UserFriendlyException(ErrorCodes.SignFailed,
                    "Không đọc được trang của tài liệu khối chữ ký tạm.");
            }

            var contentsM = Regex.Match(pageBody, @"/Contents\s+(\d+)\s+\d+\s+R");
            if (!contentsM.Success)
            {
                throw new UserFriendlyException(ErrorCodes.SignFailed,
                    "Trang khối chữ ký tạm không có luồng nội dung.");
            }
            var contentNo = int.Parse(contentsM.Groups[1].Value);
            var contentDict = temp.GetObjectBody(contentNo) ?? "<<>>";
            var contentData = temp.GetRawStreamBytes(contentNo) ?? Array.Empty<byte>();

            var resIdx = pageBody.IndexOf("/Resources", StringComparison.Ordinal);
            string resourcesRaw;
            var sourceRefs = new List<int>();
            if (resIdx < 0)
            {
                resourcesRaw = "<<>>";
            }
            else
            {
                var p = resIdx + "/Resources".Length;
                while (p < pageBody.Length && char.IsWhiteSpace(pageBody[p])) p++;
                if (p < pageBody.Length && pageBody[p] == '<')
                {
                    resourcesRaw = PdfRevisionInfo.ExtractDictionary(pageBody, p);
                }
                else
                {
                    var rm = Regex.Match(pageBody[p..], @"^(\d+)\s+\d+\s+R");
                    resourcesRaw = rm.Success
                        ? temp.GetObjectBody(int.Parse(rm.Groups[1].Value)) ?? "<<>>"
                        : "<<>>";
                }
            }

            var order = new List<int>();
            var visited = new HashSet<int>();
            var queue = new Queue<int>();
            foreach (var r in FindRefs(resourcesRaw)) queue.Enqueue(r);
            while (queue.Count > 0)
            {
                var n = queue.Dequeue();
                if (!visited.Add(n)) continue;
                var body = temp.GetObjectBody(n);
                if (body == null) continue;
                order.Add(n);
                foreach (var r in FindRefs(body)) if (!visited.Contains(r)) queue.Enqueue(r);
            }

            var formNumber = firstObjectNumber;
            var map = new Dictionary<int, int>();
            var next = firstObjectNumber + 1;
            foreach (var n in order) map[n] = next++;

            var objects = new List<TransplantedObject>();

            var formDict = new StringBuilder();
            formDict.Append("<</Type /XObject/Subtype /Form/FormType 1/BBox [0 0 ")
                .Append(width.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture))
                .Append(' ')
                .Append(height.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture))
                .Append("]/Resources ").Append(Renumber(resourcesRaw, map));

            var matrix = rotation switch
            {
                90 => "[0 1 -1 0 0 0]",
                180 => "[-1 0 0 -1 0 0]",
                270 => "[0 -1 1 0 0 0]",
                _ => null
            };
            if (matrix != null) formDict.Append("/Matrix ").Append(matrix);

            var filterM = Regex.Match(contentDict, @"/Filter\s*(/\w+|\[[^\]]*\])");
            if (filterM.Success) formDict.Append("/Filter ").Append(filterM.Groups[1].Value);
            formDict.Append("/Length ").Append(contentData.Length).Append(">>");

            objects.Add(new TransplantedObject
            {
                Number = formNumber,
                DictText = formDict.ToString(),
                StreamData = contentData,
            });

            foreach (var n in order)
            {
                var body = temp.GetObjectBody(n)!;
                var data = temp.GetRawStreamBytes(n);
                var dict = Renumber(body, map);

                if (data != null)
                {
                    dict = Regex.IsMatch(dict, @"/Length\s+\d+")
                        ? Regex.Replace(dict, @"/Length\s+\d+", "/Length " + data.Length)
                        : dict[..dict.LastIndexOf(">>", StringComparison.Ordinal)] + "/Length " + data.Length + ">>";
                }

                objects.Add(new TransplantedObject { Number = map[n], DictText = dict, StreamData = data });
            }

            return new AppearanceResult { Objects = objects, FormObjectNumber = formNumber };
        }

        private static IEnumerable<int> FindRefs(string dictRaw)
        {
            foreach (Match m in Regex.Matches(dictRaw, @"(\d+)\s+\d+\s+R\b"))
            {
                yield return int.Parse(m.Groups[1].Value);
            }
        }

        private static string Renumber(string dictRaw, IReadOnlyDictionary<int, int> map)
        {
            return Regex.Replace(dictRaw, @"(\d+)(\s+\d+\s+R\b)", m =>
            {
                var n = int.Parse(m.Groups[1].Value);
                return (map.TryGetValue(n, out var t) ? t : n) + m.Groups[2].Value;
            });
        }
    }
}
