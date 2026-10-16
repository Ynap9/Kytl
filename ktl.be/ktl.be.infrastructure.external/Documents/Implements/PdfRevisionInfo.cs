using ktl.be.infrastructure.external.Documents.Dtos;
using ktl.be.shared.Constants.Documents;
using ktl.be.shared.Requests.AppException;
using ktl.be.shared.Requests.ErrorRequest;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace ktl.be.infrastructure.external.Documents.Implements
{
    public sealed class PdfRevisionInfo
    {
        public byte[] Bytes { get; private init; } = Array.Empty<byte>();

        public long LastXrefOffset { get; private init; }

        public bool LastXrefIsStream { get; private set; }

        private string Text => _text ??= Encoding.Latin1.GetString(Bytes);
        private string? _text;

        public int Size { get; private init; }

        public int RootObjectNumber { get; private init; }

        public string? IdRaw { get; private init; }

        public string? InfoRaw { get; private init; }

        public IReadOnlyDictionary<int, PdfObjectLocation> Objects => _objects;

        private readonly Dictionary<int, PdfObjectLocation> _objects = new();

        private PdfRevisionInfo() { }

        public static PdfRevisionInfo Load(byte[] bytes)
        {
            if (bytes.Length < 32 || bytes[0] != '%' || bytes[1] != 'P')
            {
                throw new UserFriendlyException(ErrorCodes.SignPdfStructureUnsupported,
                    "File không phải PDF hợp lệ (thiếu header %PDF).");
            }

            var tailStart = Math.Max(0, bytes.Length - 2048);
            var tail = Encoding.Latin1.GetString(bytes, tailStart, bytes.Length - tailStart);
            var sx = Regex.Matches(tail, @"startxref\s+(\d+)");
            if (sx.Count == 0)
            {
                throw new UserFriendlyException(ErrorCodes.SignPdfStructureUnsupported,
                    "Không tìm thấy startxref ở cuối file PDF.");
            }

            var lastXref = long.Parse(sx[^1].Groups[1].Value);
            if (lastXref <= 0 || lastXref >= bytes.Length)
            {
                throw new UserFriendlyException(ErrorCodes.SignPdfStructureUnsupported,
                    $"startxref trỏ ra ngoài file ({lastXref}/{bytes.Length}).");
            }

            var info = new PdfRevisionInfo { Bytes = bytes, LastXrefOffset = lastXref };

            var seen = new HashSet<long>();
            long? offset = lastXref;
            string? firstTrailer = null;
            var isFirst = true;
            while (offset is long cur && seen.Add(cur))
            {
                if (cur < 0 || cur >= bytes.Length) break;

                var probe = Encoding.Latin1.GetString(bytes, (int)cur, Math.Min(20, bytes.Length - (int)cur)).TrimStart();
                string trailerDict;
                if (probe.StartsWith("xref"))
                {
                    trailerDict = info.ReadClassicTable(cur);
                    if (isFirst) info.LastXrefIsStream = false;
                }
                else
                {
                    trailerDict = info.ReadXrefStream(cur);
                    if (isFirst) info.LastXrefIsStream = true;
                }

                firstTrailer ??= trailerDict;
                isFirst = false;

                var prev = Regex.Match(trailerDict, @"/Prev\s+(\d+)");
                offset = prev.Success ? long.Parse(prev.Groups[1].Value) : null;
            }

            if (firstTrailer == null)
            {
                throw new UserFriendlyException(ErrorCodes.SignPdfStructureUnsupported,
                    "Không đọc được trailer của file PDF.");
            }

            if (Regex.IsMatch(firstTrailer, @"/Encrypt\s"))
            {
                throw new UserFriendlyException(ErrorCodes.SignPdfEncrypted,
                    "File PDF được mã hóa (/Encrypt) nên không ký số nối bản được.");
            }

            var root = Regex.Match(firstTrailer, @"/Root\s+(\d+)\s+\d+\s+R");
            var size = Regex.Match(firstTrailer, @"/Size\s+(\d+)");
            if (!root.Success || !size.Success)
            {
                throw new UserFriendlyException(ErrorCodes.SignPdfStructureUnsupported,
                    "Trailer PDF thiếu /Root hoặc /Size.");
            }

            var id = Regex.Match(firstTrailer, @"/ID\s*(\[[^\]]*\])");
            var infoRef = Regex.Match(firstTrailer, @"/Info\s+(\d+\s+\d+\s+R)");

            var result = new PdfRevisionInfo
            {
                InfoRaw = infoRef.Success ? infoRef.Groups[1].Value : null,
                Bytes = bytes,
                LastXrefOffset = lastXref,
                LastXrefIsStream = info.LastXrefIsStream,
                RootObjectNumber = int.Parse(root.Groups[1].Value),
                Size = int.Parse(size.Groups[1].Value),
                IdRaw = id.Success ? id.Groups[1].Value : null,
            };
            foreach (var kv in info._objects) result._objects[kv.Key] = kv.Value;

            var latin1 = result.Text;
            foreach (Match m in Regex.Matches(latin1, @"/DocMDP"))
            {
                var window = latin1.Substring(m.Index, Math.Min(400, latin1.Length - m.Index));
                var p = Regex.Match(window, @"/P\s+(\d+)");
                if (p.Success && p.Groups[1].Value == "1")
                {
                    throw new UserFriendlyException(ErrorCodes.SignPdfLocked,
                        "File PDF có chữ ký chứng thực cấm mọi thay đổi (/DocMDP /P 1) nên không ký thêm được.");
                }
            }

            return result;
        }

        public List<PdfPageGeometryDto> GetPages()
        {
            var catalogRaw = GetObjectBody(RootObjectNumber)
                ?? throw new UserFriendlyException(ErrorCodes.SignPdfStructureUnsupported,
                    "Không đọc được Catalog của file PDF.");

            var pagesM = Regex.Match(catalogRaw, @"/Pages\s+(\d+)\s+\d+\s+R");
            if (!pagesM.Success)
            {
                throw new UserFriendlyException(ErrorCodes.SignPdfStructureUnsupported,
                    "Catalog của file PDF không có /Pages.");
            }

            var pages = new List<PdfPageGeometryDto>();
            var visited = new HashSet<int>();
            var stack = new Stack<(int Node, double[]? MediaBox, double[]? CropBox, int Rotation)>();
            stack.Push((int.Parse(pagesM.Groups[1].Value), null, null, 0));
            while (stack.Count > 0)
            {
                var (node, mediaBox, cropBox, rotation) = stack.Pop();
                if (!visited.Add(node)) continue;

                var raw = GetObjectBody(node);
                if (raw == null) continue;

                mediaBox = ReadBox(raw, "MediaBox") ?? mediaBox;
                cropBox = ReadBox(raw, "CropBox") ?? cropBox;
                var rotateM = Regex.Match(raw, @"/Rotate\s+(-?\d+)");
                if (rotateM.Success) rotation = int.Parse(rotateM.Groups[1].Value);

                var kidsM = Regex.Match(raw, @"/Kids\s*\[(.*?)\]", RegexOptions.Singleline);
                if (!kidsM.Success)
                {
                    pages.Add(BuildGeometry(node, mediaBox, cropBox, rotation));
                    continue;
                }

                var kids = Regex.Matches(kidsM.Groups[1].Value, @"(\d+)\s+\d+\s+R")
                    .Select(m => int.Parse(m.Groups[1].Value)).ToList();
                for (var i = kids.Count - 1; i >= 0; i--) stack.Push((kids[i], mediaBox, cropBox, rotation));
            }

            if (pages.Count == 0)
            {
                throw new UserFriendlyException(ErrorCodes.SignPdfStructureUnsupported,
                    "Không tìm được trang nào trong file PDF.");
            }

            return pages;
        }

        private static PdfPageGeometryDto BuildGeometry(int objectNumber, double[]? mediaBox, double[]? cropBox,
            int rotation)
        {
            var media = ToRect(mediaBox ?? new[]
            {
                0d, 0d, SigningConstants.AppearanceReferencePageWidth, SigningConstants.AppearanceReferencePageHeight
            });

            var crop = media;
            if (cropBox != null)
            {
                var requested = ToRect(cropBox);
                var left = Math.Max(media.X, requested.X);
                var bottom = Math.Max(media.Y, requested.Y);
                var right = Math.Min(media.Right, requested.Right);
                var top = Math.Min(media.Top, requested.Top);
                if (right > left && top > bottom)
                {
                    crop = new PdfRectDto { X = left, Y = bottom, Width = right - left, Height = top - bottom };
                }
            }

            var normalized = (rotation % 360 + 360) % 360;
            return new PdfPageGeometryDto
            {
                ObjectNumber = objectNumber,
                CropBox = crop,
                Rotation = normalized % 90 == 0 ? normalized : 0
            };
        }

        private static double[]? ReadBox(string raw, string key)
        {
            var m = Regex.Match(raw, "/" + key + @"\s*\[([^\]]*)\]");
            if (!m.Success) return null;

            var values = m.Groups[1].Value.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (values.Length != 4) return null;

            var box = new double[4];
            for (var i = 0; i < 4; i++)
            {
                if (!double.TryParse(values[i], NumberStyles.Float, CultureInfo.InvariantCulture, out box[i])) return null;
            }
            return box;
        }

        private static PdfRectDto ToRect(double[] box) => new()
        {
            X = Math.Min(box[0], box[2]),
            Y = Math.Min(box[1], box[3]),
            Width = Math.Abs(box[2] - box[0]),
            Height = Math.Abs(box[3] - box[1])
        };

        private string ReadClassicTable(long offset)
        {
            var text = Text;
            var pos = (int)offset;
            var m = Regex.Match(text[pos..Math.Min(text.Length, pos + 20)], @"\s*xref\s*");
            pos += m.Length;

            while (true)
            {
                var head = Regex.Match(text[pos..Math.Min(text.Length, pos + 40)], @"^\s*(\d+)\s+(\d+)\s*");
                if (!head.Success) break;
                var start = int.Parse(head.Groups[1].Value);
                var count = int.Parse(head.Groups[2].Value);
                pos += head.Length;

                for (var i = 0; i < count; i++)
                {
                    var line = text.Substring(pos, Math.Min(20, text.Length - pos));
                    var e = Regex.Match(line, @"(\d{10})\s+(\d{5})\s+([nf])");
                    if (e.Success && e.Groups[3].Value == "n")
                    {
                        var num = start + i;
                        if (!_objects.ContainsKey(num))
                        {
                            _objects[num] = new PdfObjectLocation
                            { Type = 1, Value = long.Parse(e.Groups[1].Value) };
                        }
                    }
                    pos += 20;
                }
            }

            var t = text.IndexOf("trailer", pos, StringComparison.Ordinal);
            if (t < 0) return string.Empty;
            var ds = text.IndexOf("<<", t, StringComparison.Ordinal);
            return ds < 0 ? string.Empty : ExtractDictionary(text, ds);
        }

        private string ReadXrefStream(long offset)
        {
            var text = Text;
            var ds = text.IndexOf("<<", (int)offset, StringComparison.Ordinal);
            if (ds < 0 || ds > offset + 60) return string.Empty;
            var dict = ExtractDictionary(text, ds);

            var data = ReadStreamData(text, ds + dict.Length, dict);
            if (data == null) return dict;

            var wm = Regex.Match(dict, @"/W\s*\[([^\]]*)\]");
            if (!wm.Success) return dict;
            var w = wm.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse).ToArray();

            var sizeM = Regex.Match(dict, @"/Size\s+(\d+)");
            var size = sizeM.Success ? int.Parse(sizeM.Groups[1].Value) : 0;

            var idxM = Regex.Match(dict, @"/Index\s*\[([^\]]*)\]");
            var index = idxM.Success
                ? idxM.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray()
                : new[] { 0, size };

            var rowWidth = w.Sum();
            if (rowWidth == 0) return dict;
            var pos = 0;
            for (var k = 0; k + 1 < index.Length; k += 2)
            {
                for (var n = index[k]; n < index[k] + index[k + 1]; n++)
                {
                    if (pos + rowWidth > data.Length) return dict;
                    var f = new long[3];
                    var o = pos;
                    for (var c = 0; c < w.Length && c < 3; c++)
                    {
                        long v = 0;
                        for (var b = 0; b < w[c]; b++) v = (v << 8) | data[o++];
                        f[c] = v;
                    }
                    pos += rowWidth;

                    var type = w[0] == 0 ? 1 : (int)f[0];
                    if (type != 1 && type != 2) continue;
                    if (!_objects.ContainsKey(n))
                    {
                        _objects[n] = new PdfObjectLocation
                        { Type = type, Value = f[1], IndexInStream = (int)f[2] };
                    }
                }
            }

            return dict;
        }

        public string? GetObjectBody(int number)
        {
            if (!_objects.TryGetValue(number, out var loc)) return null;
            var text = Text;

            if (loc.Type == 1)
            {
                if (loc.Value < 0 || loc.Value >= Bytes.Length) return null;
                var objKw = text.IndexOf("obj", (int)loc.Value, StringComparison.Ordinal);
                if (objKw < 0 || objKw > loc.Value + 40) return null;
                var p = objKw + 3;
                while (p < text.Length && char.IsWhiteSpace(text[p])) p++;
                if (p >= text.Length) return null;

                if (text[p] == '<' && p + 1 < text.Length && text[p + 1] == '<') return ExtractDictionary(text, p);

                var end = text.IndexOf("endobj", p, StringComparison.Ordinal);
                return end < 0 ? null : text[p..end].Trim();
            }

            if (!_objects.TryGetValue((int)loc.Value, out var stmLoc) || stmLoc.Type != 1) return null;
            var sds = text.IndexOf("<<", (int)stmLoc.Value, StringComparison.Ordinal);
            if (sds < 0) return null;
            var stmDict = ExtractDictionary(text, sds);
            var raw = ReadStreamData(text, sds + stmDict.Length, stmDict);
            if (raw == null) return null;

            var nM = Regex.Match(stmDict, @"/N\s+(\d+)");
            var firstM = Regex.Match(stmDict, @"/First\s+(\d+)");
            if (!nM.Success || !firstM.Success) return null;
            var count = int.Parse(nM.Groups[1].Value);
            var first = int.Parse(firstM.Groups[1].Value);
            if (first > raw.Length) return null;

            var header = Encoding.Latin1.GetString(raw, 0, first)
                .Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var body = Encoding.Latin1.GetString(raw);
            for (var i = 0; i + 1 < header.Length && i / 2 < count; i += 2)
            {
                if (int.Parse(header[i]) != number) continue;
                var start = first + int.Parse(header[i + 1]);
                var end = i + 3 < header.Length ? first + int.Parse(header[i + 3]) : raw.Length;
                if (start < 0 || end > raw.Length || start >= end) return null;
                var slice = body[start..end];
                var d = slice.IndexOf("<<", StringComparison.Ordinal);
                return d >= 0 ? ExtractDictionary(slice, d) : slice.Trim();
            }
            return null;
        }

        public byte[]? GetRawStreamBytes(int number)
        {
            if (!_objects.TryGetValue(number, out var loc) || loc.Type != 1) return null;
            var text = Text;
            var objKw = text.IndexOf("obj", (int)loc.Value, StringComparison.Ordinal);
            if (objKw < 0 || objKw > loc.Value + 40) return null;
            var ds = objKw + 3;
            while (ds < text.Length && char.IsWhiteSpace(text[ds])) ds++;
            if (ds + 1 >= text.Length || text[ds] != '<' || text[ds + 1] != '<') return null;
            var dict = ExtractDictionary(text, ds);

            var st = text.IndexOf("stream", ds + dict.Length, StringComparison.Ordinal);
            if (st < 0 || st > ds + dict.Length + 20) return null;
            var p = st + 6;
            if (p + 1 < text.Length && text[p] == '\r' && text[p + 1] == '\n') p += 2;
            else if (p < text.Length && (text[p] == '\n' || text[p] == '\r')) p += 1;

            var lenM = Regex.Match(dict, @"/Length\s+(\d+)");
            int len;
            if (lenM.Success)
            {
                len = int.Parse(lenM.Groups[1].Value);
            }
            else
            {
                var es = text.IndexOf("endstream", p, StringComparison.Ordinal);
                if (es < 0) return null;
                len = es - p;
                while (len > 0 && (text[p + len - 1] == '\n' || text[p + len - 1] == '\r')) len--;
            }

            if (p + len > Bytes.Length) return null;
            var raw = new byte[len];
            Array.Copy(Bytes, p, raw, 0, len);
            return raw;
        }

        private byte[]? ReadStreamData(string text, int afterDict, string dict)
        {
            var st = text.IndexOf("stream", afterDict, StringComparison.Ordinal);
            if (st < 0) return null;
            var p = st + 6;
            if (p + 1 < text.Length && text[p] == '\r' && text[p + 1] == '\n') p += 2;
            else if (p < text.Length && (text[p] == '\n' || text[p] == '\r')) p += 1;

            var lenM = Regex.Match(dict, @"/Length\s+(\d+)");
            if (!lenM.Success) return null;
            var len = int.Parse(lenM.Groups[1].Value);
            if (p + len > Bytes.Length) return null;

            var raw = new byte[len];
            Array.Copy(Bytes, p, raw, 0, len);

            if (dict.Contains("/FlateDecode", StringComparison.Ordinal))
            {
                try
                {
                    using var input = new MemoryStream(raw);
                    using var z = new ZLibStream(input, CompressionMode.Decompress);
                    using var outMs = new MemoryStream();
                    z.CopyTo(outMs);
                    raw = outMs.ToArray();
                }
                catch
                {
                    return null;
                }
            }
            else if (Regex.IsMatch(dict, @"/Filter\s*/?\w"))
            {
                return null;
            }

            var predM = Regex.Match(dict, @"/Predictor\s+(\d+)");
            if (predM.Success && int.Parse(predM.Groups[1].Value) >= 10)
            {
                var colM = Regex.Match(dict, @"/Columns\s+(\d+)");
                var columns = colM.Success ? int.Parse(colM.Groups[1].Value) : 1;
                var outBytes = new List<byte>(raw.Length);
                var prev = new byte[columns];
                var q = 0;
                while (q + 1 + columns <= raw.Length)
                {
                    var filter = raw[q++];
                    var row = new byte[columns];
                    Array.Copy(raw, q, row, 0, columns);
                    q += columns;
                    for (var i = 0; i < columns; i++)
                    {
                        row[i] = filter switch
                        {
                            0 => row[i],
                            1 => (byte)(row[i] + (i > 0 ? row[i - 1] : 0)),
                            2 => (byte)(row[i] + prev[i]),
                            _ => row[i],
                        };
                    }
                    outBytes.AddRange(row);
                    prev = row;
                }
                raw = outBytes.ToArray();
            }

            return raw;
        }

        public static string ExtractDictionary(string text, int start)
        {
            var depth = 0;
            var i = start;
            while (i < text.Length - 1)
            {
                var c = text[i];
                if (c == '(')
                {
                    var level = 1;
                    i++;
                    while (i < text.Length && level > 0)
                    {
                        if (text[i] == '\\') { i += 2; continue; }
                        if (text[i] == '(') level++;
                        else if (text[i] == ')') level--;
                        i++;
                    }
                    continue;
                }
                if (c == '<' && text[i + 1] == '<') { depth++; i += 2; continue; }
                if (c == '>' && text[i + 1] == '>')
                {
                    depth--;
                    i += 2;
                    if (depth == 0) return text[start..i];
                    continue;
                }
                i++;
            }
            return text[start..];
        }
    }
}
