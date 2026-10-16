using ktl.be.applications.Document.Dtos.DocumentMerge;
using ktl.be.applications.Document.Dtos.Stamp;
using ktl.be.applications.Document.Interfaces;
using ktl.be.infrastructure.external.Documents.Dtos;
using ktl.be.shared.Constants.Documents;
using ktl.be.shared.Requests.AppException;
using ktl.be.shared.Requests.ErrorRequest;
using System.Text.RegularExpressions;

namespace ktl.be.applications.Document.Implements
{
    public class StampPlanner : IStampPlanner
    {
        private static readonly Regex CustomPageRegex = new(DocumentMergeConstants.CustomPagePattern, RegexOptions.Compiled);

        public List<StampRuleDto> BuildRules(string? sealImagePath, string? wetSignatureImagePath,
            List<MergeStampDto>? stamps)
        {
            var items = stamps ?? new List<MergeStampDto>();
            if (items.Count == 0) return new List<StampRuleDto>();

            var sealImage = ReadStampImage(sealImagePath, MergeStampKind.Seal);
            var wetSignatureImage = ReadStampImage(wetSignatureImagePath, MergeStampKind.WetSignature);

            var rules = new List<StampRuleDto>();
            for (var i = 0; i < items.Count; i++)
            {
                var stamp = items[i];
                var index = i + 1;
                if (!Enum.IsDefined(stamp.Kind) || !Enum.IsDefined(stamp.Scope) || !Enum.IsDefined(stamp.RepeatMode))
                {
                    throw new UserFriendlyException(ErrorCodes.MergeStampInvalid,
                        $"Khối ảnh {index} có loại ảnh, phạm vi hoặc kiểu lặp không hợp lệ.");
                }

                var image = stamp.Kind == MergeStampKind.Seal ? sealImage : wetSignatureImage;
                if (image == null)
                {
                    throw new UserFriendlyException(ErrorCodes.MergeStampInvalid,
                        $"Khối ảnh {index} cần ảnh {GetStampLabel(stamp.Kind)} nhưng chưa chọn ảnh.");
                }
                if (stamp.Scope == MergeStampScope.CurrentPage && stamp.PageNumber < 1)
                {
                    throw new UserFriendlyException(ErrorCodes.MergeStampInvalid,
                        $"Khối ảnh {index} đặt ở trang {stamp.PageNumber} không hợp lệ.");
                }

                var customPages = stamp.Scope == MergeStampScope.Repeat && stamp.RepeatMode == MergeStampRepeatMode.Custom
                    ? ParseCustomPages(stamp.CustomPages, index)
                    : new List<(int From, int To)>();

                var pageAreas = new Dictionary<int, StampAreaDto>();
                foreach (var position in stamp.PagePositions ?? new List<MergeStampPositionDto>())
                {
                    if (position.PageNumber < 1)
                    {
                        throw new UserFriendlyException(ErrorCodes.MergeStampInvalid,
                            $"Khối ảnh {index} có vị trí riêng ở trang {position.PageNumber} không hợp lệ.");
                    }
                    var area = NormalizeArea(position.XRatio, position.YRatio, position.WidthRatio,
                        position.HeightRatio, index);
                    if (!pageAreas.TryAdd(position.PageNumber, area))
                    {
                        throw new UserFriendlyException(ErrorCodes.MergeStampInvalid,
                            $"Khối ảnh {index} có nhiều vị trí riêng cho trang {position.PageNumber}.");
                    }
                }

                rules.Add(new StampRuleDto
                {
                    Kind = stamp.Kind,
                    Scope = stamp.Scope,
                    RepeatMode = stamp.RepeatMode,
                    CustomPages = customPages,
                    PageNumber = stamp.PageNumber,
                    Area = NormalizeArea(stamp.XRatio, stamp.YRatio, stamp.WidthRatio, stamp.HeightRatio, index),
                    PageAreas = pageAreas,
                    Image = image
                });
            }

            return rules;
        }

        public StampPlanDto Plan(IReadOnlyList<StampRuleDto> rules, int pageCount)
        {
            var plan = new StampPlanDto();
            for (var i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                var pages = ResolvePages(rule, pageCount, out var warning);
                if (warning != null)
                {
                    plan.Warnings.Add($"Khối ảnh {i + 1} ({GetStampLabel(rule.Kind)}) {warning}");
                }

                plan.Stamps.AddRange(pages.Select(page =>
                {
                    var area = rule.PageAreas.TryGetValue(page, out var own) ? own : rule.Area;
                    return new PdfImageStampDto
                    {
                        PageNumber = page,
                        XRatio = area.XRatio,
                        YRatio = area.YRatio,
                        WidthRatio = area.WidthRatio,
                        HeightRatio = area.HeightRatio,
                        Image = rule.Image
                    };
                }));
            }

            return plan;
        }

        private static List<int> ResolvePages(StampRuleDto rule, int pageCount, out string? warning)
        {
            warning = null;
            if (rule.Scope == MergeStampScope.CurrentPage)
            {
                if (rule.PageNumber <= pageCount) return new List<int> { rule.PageNumber };

                warning = $"đặt ở trang {rule.PageNumber} nhưng file chỉ có {pageCount} trang nên bỏ qua.";
                return new List<int>();
            }

            var allPages = Enumerable.Range(1, pageCount);
            var pages = (rule.RepeatMode switch
            {
                MergeStampRepeatMode.Even => allPages.Where(p => p % 2 == 0),
                MergeStampRepeatMode.Odd => allPages.Where(p => p % 2 == 1),
                MergeStampRepeatMode.Custom => allPages.Where(p => rule.CustomPages.Any(r => p >= r.From && p <= r.To)),
                _ => allPages
            }).ToList();

            if (rule.RepeatMode == MergeStampRepeatMode.Custom && rule.CustomPages.Any(r => r.To > pageCount))
            {
                warning = $"có trang vượt quá {pageCount} trang của file nên bỏ qua các trang đó.";
            }
            else if (pages.Count == 0)
            {
                warning = "không có trang nào khớp kiểu lặp đã chọn nên bỏ qua.";
            }

            return pages;
        }

        private static StampAreaDto NormalizeArea(double xRatio, double yRatio, double widthRatio, double heightRatio,
            int index)
        {
            if (widthRatio <= 0 || heightRatio <= 0)
            {
                throw new UserFriendlyException(ErrorCodes.MergeStampInvalid,
                    $"Khối ảnh {index} có kích thước không hợp lệ (bề rộng hoặc chiều cao bằng 0).");
            }

            var width = Math.Min(widthRatio, 1);
            var height = Math.Min(heightRatio, 1);
            return new StampAreaDto
            {
                XRatio = Math.Clamp(xRatio, 0, 1 - width),
                YRatio = Math.Clamp(yRatio, 0, 1 - height),
                WidthRatio = width,
                HeightRatio = height
            };
        }

        private static List<(int From, int To)> ParseCustomPages(string? customPages, int index)
        {
            var ranges = new List<(int From, int To)>();
            var parts = (customPages ?? string.Empty).Split(DocumentMergeConstants.CustomPageSeparator,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var part in parts)
            {
                var match = CustomPageRegex.Match(part);
                if (!match.Success || !int.TryParse(match.Groups[1].Value, out var from))
                {
                    throw new UserFriendlyException(ErrorCodes.MergeStampInvalid,
                        $"Khối ảnh {index} có danh sách trang \"{customPages}\" sai định dạng, ví dụ đúng: 1,3,5-7.");
                }

                var to = from;
                if (match.Groups[2].Success && !int.TryParse(match.Groups[2].Value, out to))
                {
                    throw new UserFriendlyException(ErrorCodes.MergeStampInvalid,
                        $"Khối ảnh {index} có khoảng trang \"{part}\" không hợp lệ.");
                }
                if (from < 1 || to < from)
                {
                    throw new UserFriendlyException(ErrorCodes.MergeStampInvalid,
                        $"Khối ảnh {index} có khoảng trang \"{part}\" không hợp lệ.");
                }

                ranges.Add((from, to));
            }

            if (ranges.Count == 0)
            {
                throw new UserFriendlyException(ErrorCodes.MergeStampInvalid,
                    $"Khối ảnh {index} chọn lặp tùy chọn nhưng chưa nhập danh sách trang.");
            }

            return ranges;
        }

        private static byte[]? ReadStampImage(string? path, MergeStampKind kind)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (!File.Exists(path) || !StampImageConstants.AllowedExtensions.Contains(ext)
                || new FileInfo(path).Length > StampImageConstants.MaxBytes)
            {
                throw new UserFriendlyException(ErrorCodes.StampImageInvalid,
                    $"Ảnh {GetStampLabel(kind)} không hợp lệ hoặc không tồn tại: {path}");
            }

            return File.ReadAllBytes(path);
        }

        private static string GetStampLabel(MergeStampKind kind)
        {
            return kind == MergeStampKind.Seal ? "dấu đỏ" : "chữ ký tươi";
        }
    }
}
