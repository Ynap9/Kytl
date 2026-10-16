using ktl.be.infrastructure.external.Documents.Dtos;
using ktl.be.infrastructure.external.Documents.Interfaces;
using ktl.be.shared.Constants;
using ktl.be.shared.Constants.Documents;
using ktl.be.shared.Requests.AppException;
using ktl.be.shared.Requests.ErrorRequest;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ktl.be.infrastructure.external.Documents.Implements
{
    public static class PdfIncrementalSigner
    {
        private const int ByteRangeFieldWidth = 10;

        public static async Task<byte[]> SignAsync(
            byte[] original,
            string signerName,
            string reason,
            string location,
            DateTime signedAt,
            int signatureSizeBytes,
            PdfSignOptionsDto options,
            ISignaturePlacementResolver placementResolver,
            Func<byte[], CancellationToken, Task<byte[]>> computeSignature,
            CancellationToken cancellationToken = default)
        {
            var info = PdfRevisionInfo.Load(original);

            var catalogNo = info.RootObjectNumber;
            var catalogRaw = info.GetObjectBody(catalogNo)
                ?? throw new UserFriendlyException(ErrorCodes.SignPdfStructureUnsupported,
                    "Không đọc được Catalog của file PDF.");

            var pages = info.GetPages();

            var nextNumber = info.Size;
            var sigFieldNo = nextNumber++;
            var sigValueNo = nextNumber++;

            var placementList = options.StampPlacements.Select(p => (PdfSignPlacementDto?)p).ToList();
            if (options.ShowSignature)
            {
                placementList.Insert(0, options.SignaturePlacement);
            }

            var signatureBlocks = new List<(int PageNo, string PageRaw, PdfRectDto Rect, AppearanceResult? Appearance)>();
            var stampBlocks = new List<(int PageNo, string PageRaw, PdfRectDto Rect, AppearanceResult Appearance)>();
            var signedAtText = signedAt.ToString(SigningConstants.AppearanceTimeFormat, CultureInfo.InvariantCulture);
            foreach (var placement in placementList)
            {
                var kind = placement?.Kind ?? PdfSignPlacementKind.Signature;
                var image = kind switch
                {
                    PdfSignPlacementKind.Seal => options.SealImage,
                    PdfSignPlacementKind.WetSignature => options.WetSignatureImage,
                    _ => null
                };
                if (kind != PdfSignPlacementKind.Signature && (image == null || image.Length == 0)) continue;

                var pageIndex = Math.Clamp((placement?.PageNumber ?? 1) - 1, 0, pages.Count - 1);
                var page = pages[pageIndex];
                var no = page.ObjectNumber;
                var raw = info.GetObjectBody(no)
                    ?? throw new UserFriendlyException(ErrorCodes.SignPdfStructureUnsupported,
                        $"Không đọc được trang {pageIndex + 1} của file PDF.");

                var displayBox = page.DisplayBox;

                if (kind == PdfSignPlacementKind.Signature)
                {
                    var pageFontSize = options.FontSize * Math.Min(
                        displayBox.Width / SigningConstants.AppearanceReferencePageWidth,
                        displayBox.Height / SigningConstants.AppearanceReferencePageHeight);
                    var textSize = PdfAppearanceBuilder.MeasureText(signerName, signedAtText, options.FontFamily,
                        pageFontSize);
                    var rect = placementResolver.Resolve(displayBox, placement, textSize.Width, textSize.Height);

                    var appearance = PdfAppearanceBuilder.Build(signerName, signedAtText, nextNumber,
                        rect.Width, rect.Height, options.FontFamily, pageFontSize, options.TextColor, page.Rotation);
                    nextNumber = appearance.Objects.Max(o => o.Number) + 1;
                    signatureBlocks.Add((no, raw, ToUserSpace(page, rect), appearance));
                }
                else
                {
                    var isSeal = kind == PdfSignPlacementKind.Seal;
                    var rect = placementResolver.Resolve(displayBox, placement,
                        placement!.WidthRatio * displayBox.Width, placement.HeightRatio * displayBox.Height);
                    var appearance = PdfAppearanceBuilder.BuildImage(image!, nextNumber, rect.Width, rect.Height,
                        page.Rotation,
                        isSeal ? options.SealDarkness : options.WetSignatureDarkness,
                        isSeal ? StampImageConstants.StrokeMin : options.WetSignatureStroke,
                        isSeal ? null : options.WetSignatureColor);
                    nextNumber = appearance.Objects.Max(o => o.Number) + 1;
                    if (options.EmbedSignatureInImage)
                    {
                        signatureBlocks.Add((no, raw, ToUserSpace(page, rect), appearance));
                    }
                    else
                    {
                        stampBlocks.Add((no, raw, ToUserSpace(page, rect), appearance));
                    }
                }
            }

            if (signatureBlocks.Count == 0)
            {
                var firstPageNo = pages[0].ObjectNumber;
                var firstPageRaw = info.GetObjectBody(firstPageNo)
                    ?? throw new UserFriendlyException(ErrorCodes.SignPdfStructureUnsupported,
                        "Không đọc được trang 1 của file PDF.");
                signatureBlocks.Add((firstPageNo, firstPageRaw, new PdfRectDto(), null));
            }

            var widgetNos = signatureBlocks.Select(_ => signatureBlocks.Count == 1 ? sigFieldNo : nextNumber++).ToList();
            var stampNos = stampBlocks.Select(_ => nextNumber++).ToList();

            var xrefNo = nextNumber++;

            var newCatalog = BuildCatalogOverride(catalogRaw, sigFieldNo);

            var annotations = signatureBlocks.Select((b, i) => (b.PageNo, b.PageRaw, AnnotNo: widgetNos[i]))
                .Concat(stampBlocks.Select((b, i) => (b.PageNo, b.PageRaw, AnnotNo: stampNos[i])))
                .ToList();
            var newPages = annotations.GroupBy(a => a.PageNo).Select(g =>
            {
                var body = g.First().PageRaw;
                foreach (var a in g)
                {
                    body = PdfObjectWriter.AppendToArray(body, "Annots", a.AnnotNo)
                        ?? throw new UserFriendlyException(ErrorCodes.SignPdfStructureUnsupported,
                            "/Annots của trang là tham chiếu gián tiếp — dạng này chưa hỗ trợ ký nối bản.");
                }
                return (PageNo: g.Key, Body: body);
            }).ToList();

            var sb = new StringBuilder();
            var offsets = new Dictionary<int, long>();

            var baseLength = original.Length;
            var prefixNewline = !(original[^1] == '\n' || original[^1] == '\r');
            if (prefixNewline) sb.Append('\n');

            long CurrentOffset() => baseLength + sb.Length;

            void WriteRaw(int number, string body, byte[]? stream)
            {
                offsets[number] = CurrentOffset();
                sb.Append(number.ToString(CultureInfo.InvariantCulture)).Append(" 0 obj\n").Append(body);
                if (stream != null)
                {
                    sb.Append("\nstream\n").Append(Encoding.Latin1.GetString(stream)).Append("\nendstream");
                }
                sb.Append("\nendobj\n");
            }

            WriteRaw(catalogNo, newCatalog, null);
            foreach (var p in newPages) WriteRaw(p.PageNo, p.Body, null);

            var fieldSb = new StringBuilder();
            if (signatureBlocks.Count == 1)
            {
                var only = signatureBlocks[0];
                var fieldDict = new PdfObjectWriter.Dict()
                    .Add("Type", new PdfObjectWriter.Name("Annot"))
                    .Add("Subtype", new PdfObjectWriter.Name("Widget"))
                    .Add("FT", new PdfObjectWriter.Name("Sig"))
                    .Add("Ff", 0)
                    .Add("T", new PdfObjectWriter.Str("SaoMai_Signature_" + sigFieldNo))
                    .Add("V", new PdfObjectWriter.Ref(sigValueNo))
                    .Add("P", new PdfObjectWriter.Ref(only.PageNo))
                    .Add("F", 4)
                    .Add("Rect", new object[] { only.Rect.X, only.Rect.Y, only.Rect.Right, only.Rect.Top });
                if (only.Appearance != null)
                {
                    fieldDict.Add("AP", new PdfObjectWriter.Dict()
                        .Add("N", new PdfObjectWriter.Ref(only.Appearance.FormObjectNumber)));
                }
                PdfObjectWriter.WriteValue(fieldSb, fieldDict);
                WriteRaw(sigFieldNo, fieldSb.ToString(), null);
            }
            else
            {
                PdfObjectWriter.WriteValue(fieldSb, new PdfObjectWriter.Dict()
                    .Add("FT", new PdfObjectWriter.Name("Sig"))
                    .Add("Ff", 0)
                    .Add("T", new PdfObjectWriter.Str("SaoMai_Signature_" + sigFieldNo))
                    .Add("V", new PdfObjectWriter.Ref(sigValueNo))
                    .Add("Kids", widgetNos.Select(n => new PdfObjectWriter.Ref(n)).ToArray()));
                WriteRaw(sigFieldNo, fieldSb.ToString(), null);

                for (var i = 0; i < signatureBlocks.Count; i++)
                {
                    var p = signatureBlocks[i];
                    var widgetDict = new PdfObjectWriter.Dict()
                        .Add("Type", new PdfObjectWriter.Name("Annot"))
                        .Add("Subtype", new PdfObjectWriter.Name("Widget"))
                        .Add("Parent", new PdfObjectWriter.Ref(sigFieldNo))
                        .Add("P", new PdfObjectWriter.Ref(p.PageNo))
                        .Add("F", 4)
                        .Add("Rect", new object[] { p.Rect.X, p.Rect.Y, p.Rect.Right, p.Rect.Top });
                    if (p.Appearance != null)
                    {
                        widgetDict.Add("AP", new PdfObjectWriter.Dict()
                            .Add("N", new PdfObjectWriter.Ref(p.Appearance.FormObjectNumber)));
                    }
                    var widgetSb = new StringBuilder();
                    PdfObjectWriter.WriteValue(widgetSb, widgetDict);
                    WriteRaw(widgetNos[i], widgetSb.ToString(), null);
                }
            }

            for (var i = 0; i < stampBlocks.Count; i++)
            {
                var s = stampBlocks[i];
                var stampSb = new StringBuilder();
                PdfObjectWriter.WriteValue(stampSb, new PdfObjectWriter.Dict()
                    .Add("Type", new PdfObjectWriter.Name("Annot"))
                    .Add("Subtype", new PdfObjectWriter.Name("Stamp"))
                    .Add("P", new PdfObjectWriter.Ref(s.PageNo))
                    .Add("F", 4)
                    .Add("Rect", new object[] { s.Rect.X, s.Rect.Y, s.Rect.Right, s.Rect.Top })
                    .Add("AP", new PdfObjectWriter.Dict()
                        .Add("N", new PdfObjectWriter.Ref(s.Appearance.FormObjectNumber))));
                WriteRaw(stampNos[i], stampSb.ToString(), null);
            }

            foreach (var p in signatureBlocks)
            {
                if (p.Appearance == null) continue;
                foreach (var o in p.Appearance.Objects) WriteRaw(o.Number, o.DictText, o.StreamData);
            }
            foreach (var s in stampBlocks)
            {
                foreach (var o in s.Appearance.Objects) WriteRaw(o.Number, o.DictText, o.StreamData);
            }

            var contentsHexLength = signatureSizeBytes * 2;
            var byteRangePlaceholder = "/ByteRange [" +
                string.Join(" ", Enumerable.Repeat(new string(' ', ByteRangeFieldWidth), 4)) + "]";

            var sigSb = new StringBuilder();
            sigSb.Append("<</Type /Sig/Filter /Adobe.PPKLite/SubFilter /")
                .Append(SigningConstants.PdfSubFilter);
            sigSb.Append("/Name ");
            PdfObjectWriter.WriteValue(sigSb, new PdfObjectWriter.Str(signerName));
            sigSb.Append("/Reason ");
            PdfObjectWriter.WriteValue(sigSb, new PdfObjectWriter.Str(reason));
            sigSb.Append("/Location ");
            PdfObjectWriter.WriteValue(sigSb, new PdfObjectWriter.Str(location));
            var vnOffset = DateTimeConstants.VietnamTimeZone.BaseUtcOffset;
            sigSb.Append("/M (D:").Append(signedAt.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture))
                .Append(vnOffset >= TimeSpan.Zero ? '+' : '-')
                .Append(Math.Abs(vnOffset.Hours).ToString("00", CultureInfo.InvariantCulture)).Append('\'')
                .Append(Math.Abs(vnOffset.Minutes).ToString("00", CultureInfo.InvariantCulture)).Append("')");
            sigSb.Append(byteRangePlaceholder);
            sigSb.Append("/Contents <").Append(new string('0', contentsHexLength)).Append('>');
            sigSb.Append(">>");
            WriteRaw(sigValueNo, sigSb.ToString(), null);

            var xrefOffset = CurrentOffset();
            if (info.LastXrefIsStream)
            {
                offsets[xrefNo] = xrefOffset;
                sb.Append(BuildXrefStream(xrefNo, offsets, Math.Max(info.Size, offsets.Keys.Max() + 1), info));
            }
            else
            {
                sb.Append(BuildXrefTable(offsets, Math.Max(info.Size, offsets.Keys.Max() + 1), xrefOffset, info));
            }

            var appended = Encoding.Latin1.GetBytes(sb.ToString());
            var output = new byte[baseLength + appended.Length];
            Buffer.BlockCopy(original, 0, output, 0, baseLength);
            Buffer.BlockCopy(appended, 0, output, baseLength, appended.Length);

            var text = Encoding.Latin1.GetString(output);

            var contentsIdx = text.LastIndexOf("/Contents <", StringComparison.Ordinal);
            var brIdx = text.LastIndexOf("/ByteRange [", StringComparison.Ordinal);
            if (contentsIdx < baseLength || brIdx < baseLength)
            {
                throw new UserFriendlyException(ErrorCodes.SignFailed,
                    "Không định vị được chỗ trống chữ ký trong bản ký nối vừa dựng.");
            }

            var gapStart = contentsIdx + "/Contents ".Length;
            var gapEnd = gapStart + 1 + contentsHexLength + 1;

            var ranges = new long[] { 0, gapStart, gapEnd, output.Length - gapEnd };
            var realByteRange = "/ByteRange [" + string.Join(" ", ranges.Select(v =>
                v.ToString(CultureInfo.InvariantCulture).PadRight(ByteRangeFieldWidth))) + "]";
            var placeholderLength = byteRangePlaceholder.Length;
            if (realByteRange.Length != placeholderLength)
            {
                throw new UserFriendlyException(ErrorCodes.SignFailed,
                    $"/ByteRange thật dài {realByteRange.Length} byte, khác chỗ trống {placeholderLength} byte.");
            }
            Encoding.Latin1.GetBytes(realByteRange).CopyTo(output, brIdx);

            var signed = new byte[ranges[1] + ranges[3]];
            Buffer.BlockCopy(output, 0, signed, 0, (int)ranges[1]);
            Buffer.BlockCopy(output, (int)ranges[2], signed, (int)ranges[1], (int)ranges[3]);

            var cms = await computeSignature(signed, cancellationToken);

            if (cms.Length > signatureSizeBytes)
            {
                throw new UserFriendlyException(ErrorCodes.SignFailed,
                    $"Khối chữ ký {cms.Length} byte vượt chỗ trống {signatureSizeBytes} byte đã chừa.");
            }

            var hex = Convert.ToHexString(cms);
            Encoding.Latin1.GetBytes(hex).CopyTo(output, gapStart + 1);

            return output;
        }

        private static PdfRectDto ToUserSpace(PdfPageGeometryDto page, PdfRectDto rect)
        {
            var crop = page.CropBox;
            var (x, y, width, height) = page.Rotation switch
            {
                90 => (crop.Width - rect.Top, rect.X, rect.Height, rect.Width),
                180 => (crop.Width - rect.Right, crop.Height - rect.Top, rect.Width, rect.Height),
                270 => (rect.Y, crop.Height - rect.Right, rect.Height, rect.Width),
                _ => (rect.X, rect.Y, rect.Width, rect.Height)
            };

            return new PdfRectDto { X = crop.X + x, Y = crop.Y + y, Width = width, Height = height };
        }

        private static string BuildCatalogOverride(string catalogRaw, int sigFieldNo)
        {
            var idx = catalogRaw.IndexOf("/AcroForm", StringComparison.Ordinal);
            if (idx < 0)
            {
                var close = catalogRaw.LastIndexOf(">>", StringComparison.Ordinal);
                if (close < 0)
                {
                    throw new UserFriendlyException(ErrorCodes.SignPdfStructureUnsupported,
                        "Catalog của file PDF không hợp lệ.");
                }
                return catalogRaw[..close] +
                       "/AcroForm<</Fields[" + sigFieldNo + " 0 R]/SigFlags 3/DA(/Helv 0 Tf 0 g )>>" +
                       catalogRaw[close..];
            }

            var p = idx + "/AcroForm".Length;
            while (p < catalogRaw.Length && char.IsWhiteSpace(catalogRaw[p])) p++;
            if (p + 1 >= catalogRaw.Length || catalogRaw[p] != '<' || catalogRaw[p + 1] != '<')
            {
                throw new UserFriendlyException(ErrorCodes.SignPdfStructureUnsupported,
                    "/AcroForm là tham chiếu gián tiếp — dạng này chưa hỗ trợ ký nối bản.");
            }

            var acro = PdfRevisionInfo.ExtractDictionary(catalogRaw, p);
            var newAcro = PdfObjectWriter.AppendToArray(acro, "Fields", sigFieldNo)
                ?? throw new UserFriendlyException(ErrorCodes.SignPdfStructureUnsupported,
                    "/Fields của AcroForm là tham chiếu gián tiếp — dạng này chưa hỗ trợ ký nối bản.");

            if (!Regex.IsMatch(newAcro, @"/SigFlags\s+\d"))
            {
                var close = newAcro.LastIndexOf(">>", StringComparison.Ordinal);
                newAcro = newAcro[..close] + "/SigFlags 3" + newAcro[close..];
            }

            return catalogRaw[..p] + newAcro + catalogRaw[(p + acro.Length)..];
        }

        private static string BuildXrefStream(int xrefNo, Dictionary<int, long> offsets, int newSize,
            PdfRevisionInfo info)
        {
            var numbers = offsets.Keys.OrderBy(n => n).ToList();

            var index = new List<int>();
            var data = new List<byte>();
            var i = 0;
            while (i < numbers.Count)
            {
                var start = numbers[i];
                var count = 1;
                while (i + count < numbers.Count && numbers[i + count] == start + count) count++;
                index.Add(start);
                index.Add(count);
                for (var k = 0; k < count; k++)
                {
                    var off = offsets[start + k];
                    data.Add(1);
                    data.Add((byte)((off >> 24) & 0xFF));
                    data.Add((byte)((off >> 16) & 0xFF));
                    data.Add((byte)((off >> 8) & 0xFF));
                    data.Add((byte)(off & 0xFF));
                    data.Add(0);
                    data.Add(0);
                }
                i += count;
            }

            var sb = new StringBuilder();
            sb.Append(xrefNo.ToString(CultureInfo.InvariantCulture)).Append(" 0 obj\n");
            sb.Append("<</Type /XRef/Size ").Append(newSize);
            sb.Append("/Index [").Append(string.Join(" ", index)).Append(']');
            sb.Append("/W [1 4 2]");
            sb.Append("/Root ").Append(info.RootObjectNumber).Append(" 0 R");
            if (info.InfoRaw != null) sb.Append("/Info ").Append(info.InfoRaw);
            if (info.IdRaw != null) sb.Append("/ID ").Append(info.IdRaw);
            sb.Append("/Prev ").Append(info.LastXrefOffset);
            sb.Append("/Length ").Append(data.Count).Append(">>\n");
            sb.Append("stream\n").Append(Encoding.Latin1.GetString(data.ToArray())).Append("\nendstream\nendobj\n");
            sb.Append("startxref\n").Append(offsets[xrefNo]).Append("\n%%EOF\n");
            return sb.ToString();
        }

        private static string BuildXrefTable(Dictionary<int, long> offsets, int newSize, long xrefStart,
            PdfRevisionInfo info)
        {
            var numbers = offsets.Keys.OrderBy(n => n).ToList();
            var sb = new StringBuilder();
            sb.Append("xref\n");
            var i = 0;
            while (i < numbers.Count)
            {
                var start = numbers[i];
                var count = 1;
                while (i + count < numbers.Count && numbers[i + count] == start + count) count++;
                sb.Append(start).Append(' ').Append(count).Append('\n');
                for (var k = 0; k < count; k++)
                {
                    sb.Append(offsets[start + k].ToString("D10", CultureInfo.InvariantCulture))
                        .Append(" 00000 n\r\n");
                }
                i += count;
            }

            sb.Append("trailer\n<</Size ").Append(newSize);
            sb.Append("/Root ").Append(info.RootObjectNumber).Append(" 0 R");
            if (info.InfoRaw != null) sb.Append("/Info ").Append(info.InfoRaw);
            if (info.IdRaw != null) sb.Append("/ID ").Append(info.IdRaw);
            sb.Append("/Prev ").Append(info.LastXrefOffset).Append(">>\n");
            sb.Append("startxref\n").Append(xrefStart).Append("\n%%EOF\n");
            return sb.ToString();
        }
    }
}
