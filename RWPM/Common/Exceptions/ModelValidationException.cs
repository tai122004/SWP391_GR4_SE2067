using Microsoft.Extensions.Localization;
using RWPM.Common.Enums;

namespace RWPM.Common.Exceptions
{
    public class ModelValidationException : Exception
    {
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorDetail { get; set; } = string.Empty;

        public ModelValidationException(string errorCode, string errorDetail = "") : base($"{errorCode} ({errorDetail})")
        {
            ErrorCode = errorCode;
            ErrorDetail = errorDetail;
        }

        public ModelValidationException(ModelValidationEnum modelValidation)
        {
            ErrorCode = modelValidation.ToString();
        }

        public string GetErrorString(IStringLocalizer localizer)
        {
            return localizer[ErrorCode] + (string.IsNullOrWhiteSpace(ErrorDetail) ? string.Empty : $" ({ErrorDetail})");
        }
    }
}
