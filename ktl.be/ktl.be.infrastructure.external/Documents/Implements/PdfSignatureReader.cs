using ktl.be.infrastructure.external.Documents.Dtos;
using ktl.be.infrastructure.external.Documents.Interfaces;
using ktl.be.shared.Constants;
using System;
using System.IO;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace ktl.be.infrastructure.external.Documents.Implements
{
    public class PdfSignatureReader : IPdfSignatureReader
    {
        private static readonly Regex ByteRangeRegex =
            new(@"/ByteRange\s*\[\s*(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s*\]", RegexOptions.Compiled);

        private const string SigningTimeOid = "1.2.840.113549.1.9.5";

        private const int DictLookBack = 6000;

        public SignatureInfoDto Read(string filePath)
        {
            var info = new SignatureInfoDto();
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) ||
                    !string.Equals(Path.GetExtension(filePath), ".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    return info;
                }

                var bytes = File.ReadAllBytes(filePath);
                var latin1 = Encoding.Latin1.GetString(bytes);

                foreach (Match m in ByteRangeRegex.Matches(latin1))
                {
                    var start1 = long.Parse(m.Groups[1].Value);
                    var len1 = long.Parse(m.Groups[2].Value);
                    var start2 = long.Parse(m.Groups[3].Value);

                    var gapStart = (int)(start1 + len1);
                    var gapEnd = (int)start2;
                    if (gapStart < 0 || gapEnd > latin1.Length || gapStart >= gapEnd) continue;

                    var lt = latin1.IndexOf('<', gapStart);
                    if (lt < 0 || lt >= gapEnd) continue;
                    var gt = latin1.IndexOf('>', lt + 1);
                    if (gt < 0 || gt > gapEnd) continue;

                    var hex = Regex.Replace(latin1.Substring(lt + 1, gt - lt - 1), @"[^0-9A-Fa-f]", string.Empty);
                    if (hex.Length % 2 != 0) hex = hex[..^1];
                    if (hex.Length < 2) continue;

                    byte[] der;
                    try { der = TrimDer(Convert.FromHexString(hex)); }
                    catch { continue; }

                    var signer = ParseCms(der);
                    var dictWindow = latin1.Substring(Math.Max(0, lt - DictLookBack), lt - Math.Max(0, lt - DictLookBack));

                    var mDate = ParseMDate(dictWindow, out var mHasOffset);
                    if (mHasOffset && !string.IsNullOrWhiteSpace(mDate)) signer.SignedAt = mDate;
                    else if (string.IsNullOrWhiteSpace(signer.SignedAt)) signer.SignedAt = mDate;
                    if (string.IsNullOrWhiteSpace(signer.SignedBy))
                        signer.SignedBy = ParseDictString(dictWindow, "Name");
                    signer.Reason ??= ParseDictString(dictWindow, "Reason");
                    signer.Location ??= ParseDictString(dictWindow, "Location");

                    info.Signers.Add(signer);
                }

                info.SignatureCount = info.Signers.Count;
                info.IsSigned = info.Signers.Count > 0;
            }
            catch (Exception ex)
            {
                info.Error = ex.Message;
            }
            return info;
        }

        private static SignerInfoDto ParseCms(byte[] der)
        {
            var result = new SignerInfoDto();
            try
            {
                var cms = new SignedCms();
                cms.Decode(der);

                foreach (SignerInfo si in cms.SignerInfos)
                {
                    var cert = si.Certificate;
                    if (cert != null)
                    {
                        var cn = cert.GetNameInfo(X509NameType.SimpleName, false);
                        if (!string.IsNullOrWhiteSpace(cn)) result.SignedBy = cn.Trim();
                    }

                    foreach (var attr in si.SignedAttributes)
                    {
                        if (attr.Oid?.Value == SigningTimeOid && attr.Values.Count > 0)
                        {
                            var st = new Pkcs9SigningTime(attr.Values[0].RawData);
                            result.SignedAt = TimeZoneInfo.ConvertTimeFromUtc(
                                st.SigningTime.ToUniversalTime(), DateTimeConstants.VietnamTimeZone)
                                .ToString("dd/MM/yyyy HH:mm:ss");
                        }
                    }
                    break;
                }
            }
            catch
            {
            }
            return result;
        }

        private static byte[] TrimDer(byte[] data)
        {
            if (data.Length < 2 || data[0] != 0x30) return data;
            int idx;
            int len;
            if ((data[1] & 0x80) == 0)
            {
                len = data[1];
                idx = 2;
            }
            else
            {
                var numBytes = data[1] & 0x7F;
                idx = 2;
                len = 0;
                for (var i = 0; i < numBytes && idx < data.Length; i++)
                {
                    len = (len << 8) | data[idx++];
                }
            }
            var total = idx + len;
            return total > 0 && total <= data.Length ? data[..total] : data;
        }

        private static string? ParseMDate(string dictWindow, out bool hasOffset)
        {
            hasOffset = false;
            var m = Regex.Match(dictWindow,
                @"/M\s*\(\s*D:(\d{4})(\d{2})(\d{2})(\d{2})(\d{2})(\d{2})(?:([+\-Z])(\d{2})'?(\d{2})?)?");
            if (!m.Success) return null;
            try
            {
                var local = new DateTime(
                    int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value),
                    int.Parse(m.Groups[4].Value), int.Parse(m.Groups[5].Value), int.Parse(m.Groups[6].Value));

                var sign = m.Groups[7].Value;
                if (string.IsNullOrEmpty(sign)) return local.ToString("dd/MM/yyyy HH:mm:ss");

                hasOffset = true;

                var declared = sign == "Z"
                    ? TimeSpan.Zero
                    : new TimeSpan(int.Parse(m.Groups[8].Value),
                        m.Groups[9].Success ? int.Parse(m.Groups[9].Value) : 0, 0);
                if (sign == "-") declared = declared.Negate();

                var utc = local - declared;
                return TimeZoneInfo.ConvertTimeFromUtc(utc, DateTimeConstants.VietnamTimeZone)
                    .ToString("dd/MM/yyyy HH:mm:ss");
            }
            catch
            {
                return null;
            }
        }

        private static string? ParseDictString(string dictWindow, string key)
        {
            var m = Regex.Match(dictWindow, "/" + key + @"\s*\(([^)]*)\)");
            if (!m.Success) return null;
            var value = m.Groups[1].Value.Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
