using ClosedXML.Excel;
using ktl.be.infrastructure.external.Documents.Dtos;
using ktl.be.infrastructure.external.Documents.Interfaces;
using ktl.be.shared.Constants.Documents;
using ktl.be.shared.Requests.AppException;
using ktl.be.shared.Requests.ErrorRequest;
using System.Globalization;

namespace ktl.be.infrastructure.external.Documents.Implements
{
    public class ExcelReader : IExcelReader
    {
        public ExcelSheetDto Read(string filePath)
        {
            XLWorkbook workbook;
            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                workbook = new XLWorkbook(stream);
            }
            catch (Exception ex)
            {
                throw new UserFriendlyException(ErrorCodes.MergeDataInvalid,
                    $"Không đọc được file Excel: {ex.Message}");
            }

            using (workbook)
            {
                var sheet = workbook.Worksheets.FirstOrDefault()
                    ?? throw new UserFriendlyException(ErrorCodes.MergeDataInvalid, "File Excel không có sheet nào.");
                var range = sheet.RangeUsed()
                    ?? throw new UserFriendlyException(ErrorCodes.MergeDataInvalid, "File Excel không có dữ liệu.");

                var result = new ExcelSheetDto();
                foreach (var cell in range.FirstRow().Cells())
                {
                    var name = cell.GetFormattedString().Trim();
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    result.Headers.Add(new ExcelHeaderDto { ColumnNumber = cell.Address.ColumnNumber, Name = name });
                }

                if (result.Headers.Count == 0)
                {
                    throw new UserFriendlyException(ErrorCodes.MergeDataInvalid,
                        "Dòng đầu tiên của file Excel không có tên cột nào.");
                }

                var headerRowNumber = range.FirstRow().RowNumber();
                foreach (var row in range.RowsUsed().Where(r => r.RowNumber() > headerRowNumber))
                {
                    var values = result.Headers.ToDictionary(
                        h => h.ColumnNumber,
                        h => ReadCell(sheet.Cell(row.RowNumber(), h.ColumnNumber)));
                    if (values.Values.All(string.IsNullOrWhiteSpace)) continue;

                    result.Rows.Add(new ExcelRowDto { RowNumber = row.RowNumber(), Values = values });
                }

                return result;
            }
        }

        private static string ReadCell(IXLCell cell)
        {
            return cell.DataType == XLDataType.DateTime
                ? cell.GetDateTime().ToString(DocumentMergeConstants.CellDateFormat, CultureInfo.InvariantCulture)
                : cell.GetFormattedString().Trim();
        }
    }
}
