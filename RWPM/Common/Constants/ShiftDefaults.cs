namespace RWPM.Common.Constants
{
    /// <summary>
    /// Cấu hình mặc định toàn hệ thống cho Ca làm việc và Chấm công (Attendance Policy Defaults).
    /// Khi ban giám đốc hoặc phòng nhân sự thay đổi chính sách chung, bạn chỉ cần sửa đổi các con số tại đây.
    /// Toàn bộ giao diện hiển thị và logic tính công sẽ tự động áp dụng giá trị mới.
    /// </summary>
    public static class ShiftDefaults
    {
        /// <summary>Maximum configured break duration for one shift.</summary>
        public const int MaximumBreakMinutes = 120;
        /// <summary>Minimum actual work time retained in a configured shift.</summary>
        public const int MinimumProductiveWorkMinutes = 120;
        /// <summary>Daily scheduled work threshold that requires a continuous break.</summary>
        public const int WorkMinutesRequiringBreak = 360;
        public const int MinimumBreakMinutes = 30;
        public const int MinimumNightBreakMinutes = 45;
        public const int NightWorkMinutesForLongerBreak = 180;

        /// <summary>
        /// Số phút cho phép nhân viên quẹt thẻ TRƯỚC giờ bắt đầu ca làm việc.
        /// Mặc định:15 phút.
        /// Ví dụ: Ca 08:00, nhân viên có thể bắt đầu chấm công từ 07:45
        /// </summary>
        public const int EarlyCheckInMinutes = 15;
        /// <summary>
        /// Số phút cho phép nhân viên đến muộn mà KHÔNG bị tính phạt / không bị đánh dấu "Đi trễ" (Grace Period).
        /// Mặc định: 15 phút.
        /// Ví dụ: Ca 08:00, quẹt thẻ lúc 08:05 vẫn được ghi nhận là "Đúng giờ".
        /// </summary>
        public const int GracePeriodMinutes = 30;
        /// <summary>
        /// Số phút cho phép nhân viên quẹt thẻ về sớm trước khi hết ca mà KHÔNG bị tính là "Về sớm".
        /// Mặc định: 15 phút.
        /// Ví dụ: Ca kết thúc 16:00, quẹt thẻ từ 15:45 vẫn được ghi nhận là hoàn thành ca đúng giờ.
        /// </summary>
        public const int EarlyCheckOutMinutes = 30;

        //public const int LateThresholdMinutes = 480;
    }
}
