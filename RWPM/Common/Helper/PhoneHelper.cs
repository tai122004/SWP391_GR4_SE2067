namespace RWPM.Common.Helper
{
    /// <summary>Quy tắc số điện thoại Việt Nam: đúng 10 chữ số, bắt đầu bằng 0 (VD: 0912345678).</summary>
    public static class PhoneHelper
    {
        public const string VnPhonePattern = @"^0\d{9}$";
        public const int Length = 10;
    }
}
