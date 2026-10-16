namespace ktl.be.shared.Requests.ErrorRequest
{
    public static class ErrorMessages
    {
        private static readonly Dictionary<int, string> _messages = new()
        {
            { ErrorCodes.System, "Lỗi hệ thống" },
            { ErrorCodes.InternalServerError, "Lỗi server" },
            { ErrorCodes.BadRequest, "Request không hợp lệ" },
            { ErrorCodes.NotFound, "Không tìm thấy trong hệ thống" },
            { ErrorCodes.Found, "Đã tồn tại trong hệ thống" },

            { ErrorCodes.TimestampInvalid, "Dấu thời gian/chữ ký số của file không hợp lệ" },
            { ErrorCodes.SignatureInvalid, "Nội dung file đã bị sửa sau khi ký số" },

            { ErrorCodes.CertificateNotFound, "Không đọc được chứng thư số nào trên máy" },
            { ErrorCodes.CertificateNotSignable, "Không có chứng thư số nào dùng để ký được" },
            { ErrorCodes.SignSourceNotFound, "Không tìm thấy file hoặc thư mục cần ký" },
            { ErrorCodes.SignNoPdfFound, "Không có file PDF nào để ký" },
            { ErrorCodes.SignTokenInvalid, "Chứng thư số không dùng để ký được" },
            { ErrorCodes.SignFailed, "Ký số file thất bại" },
            { ErrorCodes.SignPdfEncrypted, "File PDF được mã hóa nên không ký số được" },
            { ErrorCodes.SignPdfLocked, "File PDF bị khóa không cho ký thêm (/DocMDP)" },
            { ErrorCodes.SignPdfStructureUnsupported, "Cấu trúc file PDF không hỗ trợ ký nối bản" },
            { ErrorCodes.SignPositionInvalid, "Vị trí đặt chữ ký không hợp lệ" },
            { ErrorCodes.StampImageInvalid, "Ảnh dấu đỏ hoặc chữ ký tươi không hợp lệ" },

            { ErrorCodes.MergeTemplateInvalid, "File template Word không hợp lệ" },
            { ErrorCodes.MergeDataInvalid, "File dữ liệu Excel không hợp lệ" },
            { ErrorCodes.MergeMappingInvalid, "Cấu hình map trường không hợp lệ" },
            { ErrorCodes.MergeConverterUnavailable, "Máy chưa cài Microsoft Word để xuất PDF" },
            { ErrorCodes.MergeFailed, "Sinh file thất bại" },
            { ErrorCodes.MergeStampInvalid, "Cấu hình ảnh dấu đỏ hoặc chữ ký tươi không hợp lệ" },
            { ErrorCodes.FileUploadInvalid, "File kéo thả vào ứng dụng không hợp lệ" },
        };

        public static string GetMessage(int code)
        {
            return _messages.TryGetValue(code, out var message) ? message : "Unknown error.";
        }
    }
}
