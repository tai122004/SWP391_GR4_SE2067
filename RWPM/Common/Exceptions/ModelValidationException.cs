using Microsoft.Extensions.Localization;
using RWPM.Common.Enums;

namespace RWPM.Common.Exceptions
{
    public class ModelValidationException : Exception
    {
        /// <summary>
        /// Mã lỗi cũ được truyền thẳng bằng câu tiếng Anh -> key tương ứng trong ErrorServerDefinition.resx
        /// </summary>
        private static readonly Dictionary<string, string> LegacyCodes = new(StringComparer.Ordinal)
        {
            ["File is empty or null."] = "File_EmptyOrNull",
        };

        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorDetail { get; set; } = string.Empty;

        public ModelValidationException(string errorCode, string errorDetail = "") : base($"{errorCode} ({errorDetail})")
        {
            ErrorCode = LegacyCodes.TryGetValue(errorCode, out var key) ? key : errorCode;
            ErrorDetail = errorDetail;
        }

        public ModelValidationException(ModelValidationEnum modelValidation) : base(modelValidation.ToString())
        {
            ErrorCode = modelValidation.ToString();
        }

        /// <summary>
        /// Thông báo hiển thị cho người dùng theo ngôn ngữ hiện tại.
        /// Mã lỗi chưa có trong resx: nếu ErrorDetail (thường đã được dịch sẵn) có giá trị thì chỉ hiển thị ErrorDetail,
        /// tránh lộ mã lỗi kỹ thuật kiểu "Shift_Invalid (...)".
        /// </summary>
        public string GetErrorString(IStringLocalizer localizer)
        {
            var text = localizer[ErrorCode];
            if (text.ResourceNotFound && !string.IsNullOrWhiteSpace(ErrorDetail)) return ErrorDetail;
            return text.Value + (string.IsNullOrWhiteSpace(ErrorDetail) ? string.Empty : $" ({ErrorDetail})");
        }
    }
}
