using System.Text.RegularExpressions;

namespace RWPM.Common.Helper
{
    /// <summary>Quy tắc số điện thoại Việt Nam: đúng 10 chữ số, bắt đầu bằng 0 (VD: 0912345678).</summary>
    public static class PhoneHelper
    {
        public const string VnPhonePattern = @"^0\d{9}$";
        public const int Length = 10;

        /// <summary>
        /// Chuẩn hoá: bỏ khoảng trắng/dấu chấm/gạch; nếu Excel làm mất số 0 đầu (còn 9 số) thì thêm lại.
        /// </summary>
        public static string Normalize(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
            var digits = Regex.Replace(phone, @"\D", "");
            if (digits.Length == Length - 1 && !digits.StartsWith("0")) digits = "0" + digits;
            return digits;
        }

        public static bool IsValid(string? phone) => !string.IsNullOrEmpty(phone) && Regex.IsMatch(phone, VnPhonePattern);
    }
}
