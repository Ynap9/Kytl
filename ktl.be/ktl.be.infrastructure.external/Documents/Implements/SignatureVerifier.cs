using ktl.be.infrastructure.external.Documents.Dtos;
using ktl.be.infrastructure.external.Documents.Interfaces;
using ktl.be.shared.Constants;
using ktl.be.shared.Constants.Documents;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace ktl.be.infrastructure.external.Documents.Implements
{
    public class SignatureVerifier : ISignatureVerifier
    {
        private static readonly Regex ByteRangeRegex =
            new(@"/ByteRange\s*\[\s*(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s*\]", RegexOptions.Compiled);

        private static readonly Lazy<X509Certificate2?> RootG2 =
            new(() => LoadPinnedCert(SignatureConstants.RootG2FileName, SignatureConstants.RootG2Sha256));

        private static readonly Lazy<X509Certificate2Collection> IntermediateCAs = new(LoadIntermediateCAs);

        private static readonly Lazy<X509Certificate2?> RootG1 =
            new(() => LoadPinnedCert(SignatureConstants.RootG1FileName, SignatureConstants.RootG1Sha256));

        private static readonly Lazy<X509Certificate2?> SubCaG1 =
            new(() => LoadPinnedCert(SignatureConstants.SubCaG1FileName, SignatureConstants.SubCaG1Sha256));

        public TimestampVerifyDto Verify(string filePath)
        {
            var dto = new TimestampVerifyDto();
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) ||
                    !string.Equals(Path.GetExtension(filePath), ".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    dto.Verdict = TimestampVerdict.NotApplicable;
                    dto.Reason = "Đang phát triển trình kiểm tra ký số cho file không phải PDF, vui lòng liên hệ admin.";
                    return dto;
                }

                var bytes = File.ReadAllBytes(filePath);
                var latin1 = Encoding.Latin1.GetString(bytes);

                var blocks = new List<SignatureBlock>();
                var skipped = 0;
                foreach (Match m in ByteRangeRegex.Matches(latin1))
                {
                    var block = ParseBlock(latin1, bytes, m);
                    if (block != null) blocks.Add(block);
                    else skipped++;
                }

                if (blocks.Count == 0)
                {
                    dto.Verdict = TimestampVerdict.NoTimestamp;
                    dto.HasTimestamp = false;
                    dto.SkippedBlocks = skipped;
                    dto.Reason = "PDF không có chữ ký số/dấu thời gian tin cậy.";
                    return dto;
                }

                var decodable = blocks.Where(b => b.Cms != null).ToList();
                var outermostEnd = decodable.Count > 0 ? decodable.Max(b => b.SignedEnd) : 0L;

                var verified = new List<(TimestampVerifyDto Result, long SignedEnd)>();
                foreach (var block in blocks)
                {
                    if (block.Cms == null)
                    {
                        verified.Add((new TimestampVerifyDto
                        {
                            Verdict = TimestampVerdict.InvalidSignature,
                            Reason = block.CorruptReason
                        }, block.SignedEnd));
                        continue;
                    }
                    foreach (SignerInfo signer in block.Cms.SignerInfos)
                    {
                        verified.Add((VerifySigner(signer, block, bytes.LongLength, outermostEnd), block.SignedEnd));
                    }
                }

                verified = verified.OrderBy(v => v.SignedEnd).ToList();
                var results = verified.Select(v => v.Result).ToList();

                if (results.Count == 0)
                {
                    dto.Verdict = TimestampVerdict.NoTimestamp;
                    dto.HasTimestamp = false;
                    dto.SkippedBlocks = skipped;
                    dto.Reason = "PDF không có chữ ký số/dấu thời gian tin cậy.";
                    return dto;
                }

                var worst = results.OrderByDescending(r => Rank(r.Verdict)).First();
                worst.SkippedBlocks = skipped;
                worst.SignatureBlockCount = blocks.Count;
                worst.ValidSignatureCount = results.Count(r => r.Verdict == TimestampVerdict.ValidTrusted);
                worst.Signatures = results.Select(r => new SignatureDetailDto
                {
                    SignedBy = r.SignedBy,
                    Verdict = r.Verdict,
                    Reason = r.Reason,
                    TrustedTime = r.TrustedTime,
                    SigningTime = r.SigningTime
                }).ToList();

                var newest = results[^1];
                worst.SigningTime = newest.SigningTime;
                worst.TrustedTime = newest.TrustedTime;
                return worst;
            }
            catch (Exception ex)
            {
                dto.Verdict = TimestampVerdict.NoTimestamp;
                dto.ReadFailed = true;
                dto.Reason = $"Không đọc được chữ ký/dấu thời gian: {ex.Message}";
                return dto;
            }
        }

        private static string FormatVietnam(DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(utc, DateTimeConstants.VietnamTimeZone)
                .ToString("dd/MM/yyyy HH:mm:ss");

        private static int Rank(TimestampVerdict v) => v switch
        {
            TimestampVerdict.InvalidSignature => 6,
            TimestampVerdict.UntrustedSigner => 5,
            TimestampVerdict.UntrustedTsa => 4,
            TimestampVerdict.NoTimestamp => 3,
            TimestampVerdict.TimeMismatch => 2,
            TimestampVerdict.ValidTrusted => 1,
            TimestampVerdict.NotApplicable => 0,
            _ => 6
        };

        private sealed class SignatureBlock
        {
            public SignedCms? Cms { get; init; }
            public string? CorruptReason { get; init; }
            public required long SignedEnd { get; init; }
            public required bool RangeSane { get; init; }
        }

        private static SignatureBlock? ParseBlock(string latin1, byte[] bytes, Match m)
        {
            if (!long.TryParse(m.Groups[1].Value, out var start1) || !long.TryParse(m.Groups[2].Value, out var len1) ||
                !long.TryParse(m.Groups[3].Value, out var start2) || !long.TryParse(m.Groups[4].Value, out var len2))
                return null;
            if (start1 < 0 || len1 < 0 || start2 < 0 || len2 < 0) return null;

            var gapStart = start1 + len1;
            var signedEnd = start2 + len2;
            if (gapStart > bytes.LongLength || signedEnd > bytes.LongLength || gapStart >= start2) return null;

            var der = ExtractContents(latin1, m);
            if (der == null) return null;

            var content = new byte[len1 + len2];
            Array.Copy(bytes, start1, content, 0, len1);
            Array.Copy(bytes, start2, content, len1, len2);

            var rangeSane = start1 == 0 && GapHoldsOnlyContents(latin1, gapStart, start2);

            SignedCms cms;
            try
            {
                cms = new SignedCms(new ContentInfo(content), detached: true);
                cms.Decode(der);
            }
            catch (Exception ex)
            {
                return new SignatureBlock
                {
                    CorruptReason = $"Khối chữ ký trong file đã hỏng, không giải mã được (CMS lỗi: {ex.Message}).",
                    SignedEnd = signedEnd,
                    RangeSane = rangeSane
                };
            }
            if (cms.SignerInfos.Count == 0)
            {
                return new SignatureBlock
                {
                    CorruptReason = "Khối chữ ký trong file không chứa người ký nào (SignerInfos rỗng).",
                    SignedEnd = signedEnd,
                    RangeSane = rangeSane
                };
            }

            return new SignatureBlock
            {
                Cms = cms,
                SignedEnd = signedEnd,
                RangeSane = rangeSane
            };
        }

        private static bool GapHoldsOnlyContents(string latin1, long gapStart, long gapEnd)
        {
            var gap = latin1.Substring((int)gapStart, (int)(gapEnd - gapStart));
            var lt = gap.IndexOf('<');
            var gt = gap.LastIndexOf('>');
            if (lt < 0 || gt < 0 || gt < lt) return false;
            if (gap[..lt].Trim().Length != 0 || gap[(gt + 1)..].Trim().Length != 0) return false;
            return gap.IndexOf('<', lt + 1) < 0 && gap.IndexOf('>', lt + 1) == gt;
        }

        private static TimestampVerifyDto VerifySigner(SignerInfo signer, SignatureBlock block,
            long fileLength, long outermostEnd)
        {
            var dto = new TimestampVerifyDto
            {
                SignedBy = signer.Certificate?.GetNameInfo(X509NameType.SimpleName, false)
            };

            try { signer.CheckSignature(verifySignatureOnly: true); }
            catch
            {
                dto.Verdict = TimestampVerdict.InvalidSignature;
                dto.Reason = "Nội dung tài liệu đã bị sửa sau khi ký (chữ ký không khớp nội dung).";
                return dto;
            }

            if (!block.RangeSane)
            {
                dto.Verdict = TimestampVerdict.InvalidSignature;
                dto.Reason = "Vùng ký (/ByteRange) không hợp lệ — không phủ từ đầu file hoặc có byte lạ trong khoảng hở.";
                return dto;
            }

            if (block.SignedEnd == outermostEnd && outermostEnd != fileLength)
            {
                dto.Verdict = TimestampVerdict.InvalidSignature;
                dto.Reason = "File có byte nằm ngoài mọi vùng ký (nội dung được nối thêm sau khi ký).";
                return dto;
            }

            DateTime? signingUtc = null;
            foreach (var a in signer.SignedAttributes)
            {
                if (a.Oid?.Value == SignatureConstants.OidSigningTime && a.Values.Count > 0)
                {
                    try { signingUtc = new Pkcs9SigningTime(a.Values[0].RawData).SigningTime.ToUniversalTime(); }
                    catch {  }
                }
            }

            byte[]? tokenBytes = null;
            foreach (var a in signer.UnsignedAttributes)
            {
                if (a.Oid?.Value == SignatureConstants.OidSignatureTimeStampToken && a.Values.Count > 0)
                {
                    tokenBytes = a.Values[0].RawData;
                    break;
                }
            }

            dto.SigningTime = signingUtc == null ? null : FormatVietnam(signingUtc.Value);
            dto.HasTimestamp = tokenBytes != null;

            var cmsCerts = block.Cms.Certificates;

            if (tokenBytes == null)
            {
                dto.Verdict = TimestampVerdict.NoTimestamp;
                dto.Reason = "Chữ ký không có dấu thời gian tin cậy (RFC 3161).";
                return dto;
            }

            if (!Rfc3161TimestampToken.TryDecode(tokenBytes, out var token, out _) || token == null)
            {
                dto.Verdict = TimestampVerdict.UntrustedTsa;
                dto.Reason = "Không giải mã được token dấu thời gian.";
                return dto;
            }

            if (!token.VerifySignatureForSignerInfo(signer, out var tsaCert) || tsaCert == null)
            {
                dto.Verdict = TimestampVerdict.UntrustedTsa;
                dto.Reason = "Chữ ký của TSA không hợp lệ với chữ ký này.";
                return dto;
            }
            dto.TimestampAuthority = tsaCert.GetNameInfo(X509NameType.SimpleName, false);

            var trustedUtc = token.TokenInfo.Timestamp.UtcDateTime;
            dto.TrustedTime = FormatVietnam(trustedUtc);

            var root = RootG2.Value;
            if (root == null)
            {
                dto.Verdict = TimestampVerdict.UntrustedTsa;
                dto.Reason = "Không nạp được trust anchor Root G2 (kiểm tra file rootcag2.crt / pin).";
                return dto;
            }
            if (!ChainsToRoot(tsaCert, token.AsSignedCms().Certificates, root, trustedUtc))
            {
                dto.Verdict = TimestampVerdict.UntrustedTsa;
                dto.Reason = "Dấu thời gian không thuộc TSA Ban Cơ yếu (chain không về Root G2).";
                return dto;
            }

            if (signer.Certificate == null)
            {
                dto.Verdict = TimestampVerdict.UntrustedSigner;
                dto.Reason = "Chữ ký không nhúng chứng thư số của người ký.";
                return dto;
            }
            if (!IsTrustedSigner(signer.Certificate, cmsCerts, trustedUtc, root))
            {
                dto.Verdict = TimestampVerdict.UntrustedSigner;
                dto.Reason = RootG1.Value != null && SubCaG1.Value != null
                    ? "Chứng thư số người ký không thuộc CA Ban Cơ yếu (chain không về Root G1 lẫn Root G2)."
                    : "Chứng thư số người ký không thuộc CA Ban Cơ yếu (nhánh G1 không kiểm được do thiếu/sai pin rootca.crt hoặc cp.crt; chain cũng không về Root G2).";
                return dto;
            }

            if (signingUtc == null)
            {
                dto.Verdict = TimestampVerdict.ValidTrusted;
                return dto;
            }

            var delta = (signingUtc.Value - trustedUtc).TotalSeconds;

            var quirkOffset = SignatureConstants.SignerLocalTimeQuirkHours * 3600;
            var isLocalTimeQuirk = Math.Abs(delta - quirkOffset) <= SignatureConstants.SignerLocalTimeQuirkWindowSeconds;
            if (isLocalTimeQuirk)
            {
                delta -= quirkOffset;
                dto.Reason = $"Giờ ký tự khai ghi theo giờ địa phương (UTC+{SignatureConstants.SignerLocalTimeQuirkHours}) " +
                             $"nhưng đánh dấu UTC — sai chuẩn PKCS#9, chấp nhận theo diện tương thích ngược.";

                dto.SigningTime = FormatVietnam(
                    signingUtc.Value.AddHours(-SignatureConstants.SignerLocalTimeQuirkHours));
            }

            if (delta > SignatureConstants.TsaClockSkewGraceSeconds)
            {
                dto.Verdict = TimestampVerdict.TimeMismatch;
                dto.Reason = $"Giờ ký tự khai đứng SAU thời điểm TSA đóng dấu {Math.Round(delta)}s — " +
                             $"không thể xảy ra trong trình tự ký hợp lệ.";
                return dto;
            }

            dto.Verdict = TimestampVerdict.ValidTrusted;
            return dto;
        }

        private static bool ChainsToRoot(X509Certificate2 leaf, X509Certificate2Collection extra, X509Certificate2 root,
            DateTime verificationTime)
        {
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(root);
            chain.ChainPolicy.ExtraStore.AddRange(extra);
            chain.ChainPolicy.VerificationTime = verificationTime;
            chain.ChainPolicy.ExtraStore.AddRange(IntermediateCAs.Value);
            if (!chain.Build(leaf)) return false;
            var top = chain.ChainElements[^1].Certificate;
            return string.Equals(top.Thumbprint, root.Thumbprint, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsTrustedSignerCert(X509Certificate2 signerCert, DateTime verificationTime)
        {
            var root = RootG2.Value;
            if (root == null) return false;
            return IsTrustedSigner(signerCert, new X509Certificate2Collection(), verificationTime, root);
        }

        private static bool IsTrustedSigner(X509Certificate2 signerCert, X509Certificate2Collection cmsCerts,
            DateTime trustedUtc, X509Certificate2 rootG2)
        {
            var rootG1 = RootG1.Value;
            var subCaG1 = SubCaG1.Value;
            if (rootG1 != null && subCaG1 != null)
            {
                var extraG1 = new X509Certificate2Collection();
                extraG1.AddRange(cmsCerts);
                extraG1.Add(subCaG1);
                if (ChainsToRoot(signerCert, extraG1, rootG1, trustedUtc)) return true;
            }

            return ChainsToRoot(signerCert, cmsCerts, rootG2, trustedUtc);
        }

        private static byte[]? ExtractContents(string latin1, Match m)
        {
            var gapStart = (int)(long.Parse(m.Groups[1].Value) + long.Parse(m.Groups[2].Value));
            var gapEnd = (int)long.Parse(m.Groups[3].Value);
            if (gapStart < 0 || gapEnd > latin1.Length || gapStart >= gapEnd) return null;
            var lt = latin1.IndexOf('<', gapStart);
            if (lt < 0 || lt >= gapEnd) return null;
            var gt = latin1.IndexOf('>', lt + 1);
            if (gt < 0 || gt > gapEnd) return null;
            var hex = Regex.Replace(latin1.Substring(lt + 1, gt - lt - 1), "[^0-9A-Fa-f]", string.Empty);
            if (hex.Length % 2 != 0) hex = hex[..^1];
            if (hex.Length < 2) return null;
            try { return Convert.FromHexString(hex); } catch { return null; }
        }

        private static X509Certificate2Collection LoadIntermediateCAs()
        {
            var col = new X509Certificate2Collection();
            foreach (var (fileName, pin) in SignatureConstants.IntermediateCaPins)
            {
                var cert = LoadPinnedCert(fileName, pin);
                if (cert != null) col.Add(cert);
            }
            return col;
        }

        private static X509Certificate2? LoadPinnedCert(string fileName, string expectedSha256)
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, SignatureConstants.CertificateFolderName, fileName);
                if (!File.Exists(path)) path = Path.Combine(AppContext.BaseDirectory, fileName);
                if (!File.Exists(path)) return null;
                var text = File.ReadAllText(path);
                var cert = text.Contains("BEGIN CERTIFICATE")
                    ? X509Certificate2.CreateFromPem(text)
                    : X509CertificateLoader.LoadCertificateFromFile(path);
                var sha = cert.GetCertHashString(HashAlgorithmName.SHA256);
                return string.Equals(sha, expectedSha256, StringComparison.OrdinalIgnoreCase)
                    ? cert
                    : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
