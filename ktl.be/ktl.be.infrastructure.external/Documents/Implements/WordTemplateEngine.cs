using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ktl.be.infrastructure.external.Documents.Interfaces;
using ktl.be.shared.Constants.Documents;
using ktl.be.shared.Requests.AppException;
using ktl.be.shared.Requests.ErrorRequest;
using System.Text.RegularExpressions;

namespace ktl.be.infrastructure.external.Documents.Implements
{
    public class WordTemplateEngine : IWordTemplateEngine
    {
        private static readonly Regex FieldRegex = new(DocumentMergeConstants.FieldPattern, RegexOptions.Compiled);

        public List<string> GetFieldNames(byte[] template)
        {
            using var stream = new MemoryStream(template, writable: false);
            using var document = Open(stream, isEditable: false);

            var names = new List<string>();
            foreach (var texts in EnumerateParagraphTexts(document))
            {
                foreach (Match match in FieldRegex.Matches(string.Concat(texts.Select(t => t.Text))))
                {
                    var name = match.Groups[1].Value.Trim();
                    if (name.Length > 0 && !names.Contains(name)) names.Add(name);
                }
            }
            return names;
        }

        public byte[] Fill(byte[] template, IReadOnlyDictionary<string, string> values)
        {
            using var stream = new MemoryStream();
            stream.Write(template);

            using (var document = Open(stream, isEditable: true))
            {
                foreach (var texts in EnumerateParagraphTexts(document))
                {
                    ReplaceFields(texts, values);
                }
            }

            return stream.ToArray();
        }

        private static WordprocessingDocument Open(Stream stream, bool isEditable)
        {
            try
            {
                return WordprocessingDocument.Open(stream, isEditable);
            }
            catch (Exception ex)
            {
                throw new UserFriendlyException(ErrorCodes.MergeTemplateInvalid,
                    $"Không đọc được file template Word: {ex.Message}");
            }
        }

        private static IEnumerable<List<Text>> EnumerateParagraphTexts(WordprocessingDocument document)
        {
            var main = document.MainDocumentPart
                ?? throw new UserFriendlyException(ErrorCodes.MergeTemplateInvalid,
                    "File template Word không có nội dung chính.");

            var roots = new List<OpenXmlElement?> { main.Document };
            roots.AddRange(main.HeaderParts.Select(p => p.Header));
            roots.AddRange(main.FooterParts.Select(p => p.Footer));

            foreach (var root in roots.OfType<OpenXmlElement>())
            {
                foreach (var paragraph in root.Descendants<Paragraph>())
                {
                    var texts = paragraph.Descendants<Text>()
                        .Where(t => t.Ancestors<Paragraph>().First() == paragraph)
                        .ToList();
                    if (texts.Count > 0) yield return texts;
                }
            }
        }

        private static void ReplaceFields(List<Text> texts, IReadOnlyDictionary<string, string> values)
        {
            var matches = FieldRegex.Matches(string.Concat(texts.Select(t => t.Text)));
            for (var k = matches.Count - 1; k >= 0; k--)
            {
                var match = matches[k];
                if (!values.TryGetValue(match.Groups[1].Value.Trim(), out var value)) continue;

                var start = match.Index;
                var end = match.Index + match.Length;
                var offset = 0;
                var isFirst = true;
                foreach (var text in texts)
                {
                    var current = text.Text;
                    var from = Math.Max(start - offset, 0);
                    var to = Math.Min(end - offset, current.Length);
                    if (from < to)
                    {
                        text.Text = current[..from] + (isFirst ? value : string.Empty) + current[to..];
                        text.Space = SpaceProcessingModeValues.Preserve;
                        isFirst = false;
                    }
                    offset += current.Length;
                }
            }
        }
    }
}
