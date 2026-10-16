using System.Globalization;
using System.Text;

namespace ktl.be.infrastructure.external.Documents.Implements
{
    public static class PdfObjectWriter
    {
        public sealed class Name
        {
            public string Value { get; }
            public Name(string value) => Value = value;
        }

        public sealed class Ref
        {
            public int Number { get; }
            public Ref(int number) => Number = number;
        }

        public sealed class Raw
        {
            public string Text { get; }
            public Raw(string text) => Text = text;
        }

        public sealed class Str
        {
            public string Value { get; }
            public Str(string value) => Value = value;
        }

        public sealed class Dict
        {
            public List<KeyValuePair<string, object?>> Items { get; } = new();

            public Dict Add(string key, object? value)
            {
                Items.Add(new KeyValuePair<string, object?>(key, value));
                return this;
            }
        }

        public static void WriteObject(StringBuilder sb, int number, object? value)
        {
            sb.Append(number.ToString(CultureInfo.InvariantCulture)).Append(" 0 obj\n");
            WriteValue(sb, value);
            sb.Append("\nendobj\n");
        }

        public static void WriteValue(StringBuilder sb, object? value)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    break;

                case Raw raw:
                    sb.Append(raw.Text);
                    break;

                case Name name:
                    sb.Append('/');
                    foreach (var c in name.Value)
                    {
                        if (c is > (char)0x20 and < (char)0x7F && "()<>[]{}/%#".IndexOf(c) < 0)
                            sb.Append(c);
                        else
                            sb.Append('#').Append(((int)c).ToString("X2", CultureInfo.InvariantCulture));
                    }
                    break;

                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;

                case int i:
                    sb.Append(i.ToString(CultureInfo.InvariantCulture));
                    break;

                case long l:
                    sb.Append(l.ToString(CultureInfo.InvariantCulture));
                    break;

                case double d:
                    sb.Append(d.ToString("0.####", CultureInfo.InvariantCulture));
                    break;

                case Ref r:
                    sb.Append(r.Number.ToString(CultureInfo.InvariantCulture)).Append(" 0 R");
                    break;

                case Str s:
                    WriteString(sb, s.Value);
                    break;

                case Dict dict:
                    sb.Append("<<");
                    foreach (var kv in dict.Items)
                    {
                        WriteValue(sb, new Name(kv.Key));
                        sb.Append(' ');
                        WriteValue(sb, kv.Value);
                    }
                    sb.Append(">>");
                    break;

                case System.Collections.IEnumerable list when value is not string:
                    sb.Append('[');
                    var first = true;
                    foreach (var item in list)
                    {
                        if (!first) sb.Append(' ');
                        WriteValue(sb, item);
                        first = false;
                    }
                    sb.Append(']');
                    break;

                default:
                    throw new InvalidOperationException(
                        $"PdfObjectWriter không hỗ trợ kiểu giá trị {value.GetType().Name}.");
            }
        }

        private static void WriteString(StringBuilder sb, string value)
        {
            var ascii = value.All(c => c is >= (char)0x20 and < (char)0x7F);
            if (ascii)
            {
                sb.Append('(');
                foreach (var c in value)
                {
                    if (c is '(' or ')' or '\\') sb.Append('\\');
                    sb.Append(c);
                }
                sb.Append(')');
                return;
            }

            sb.Append('<').Append("FEFF");
            foreach (var b in Encoding.BigEndianUnicode.GetBytes(value))
            {
                sb.Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
            sb.Append('>');
        }

        public static string? AppendToArray(string dictRaw, string key, int newObjectNumber)
        {
            var marker = "/" + key;
            var at = -1;
            var from = 0;
            while (true)
            {
                var idx = dictRaw.IndexOf(marker, from, StringComparison.Ordinal);
                if (idx < 0) break;
                var after = idx + marker.Length;
                if (after >= dictRaw.Length ||
                    dictRaw[after] is ' ' or '\r' or '\n' or '\t' or '[' or '/' or '<')
                {
                    at = idx;
                    break;
                }
                from = idx + 1;
            }

            if (at < 0)
            {
                var close = dictRaw.LastIndexOf(">>", StringComparison.Ordinal);
                if (close < 0) return null;
                return dictRaw[..close] + marker + "[" + newObjectNumber + " 0 R]" + dictRaw[close..];
            }

            var p = at + marker.Length;
            while (p < dictRaw.Length && char.IsWhiteSpace(dictRaw[p])) p++;
            if (p >= dictRaw.Length || dictRaw[p] != '[') return null;

            var depth = 0;
            var q = p;
            while (q < dictRaw.Length)
            {
                if (dictRaw[q] == '[') depth++;
                else if (dictRaw[q] == ']')
                {
                    depth--;
                    if (depth == 0) break;
                }
                q++;
            }
            if (q >= dictRaw.Length) return null;

            return dictRaw[..q] + " " + newObjectNumber + " 0 R" + dictRaw[q..];
        }
    }
}
