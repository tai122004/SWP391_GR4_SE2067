namespace RWPM.Common.Constants
{
    /// <summary>
    /// Cấu hình mặc định toàn hệ thống cho Ca làm việc & Chấm công (Attendance Policy Defaults).
    /// Khi ban giám đốc hoặc phòng nhân sự thay đổi chính sách chung, bạn chỉ cần sửa đổi các con số tại đây.
    /// Toàn bộ giao diện hiển thị và logic tính công sẽ tự động áp dụng giá trị mới.
    /// </summary>
    public static class ShiftDefaults
    {
        /// <summary>
        /// Số phút cho phép nhân viên quẹt thẻ TRƯỚC giờ bắt đầu ca làm việc.
        /// Mặc định: 15 phút.
        /// Ví dụ: Ca 08:00, nhân viên có thể bắt đầu chấm công từ 07:45.
        /// </summary>
        public const int EarlyCheckInMinutes = 30;

        /// <summary>
        /// Số phút cho phép nhân viên đến muộn mà KHÔNG bị tính phạt / không bị đánh dấu "Đi trễ" (Grace Period).
        /// Mặc định: 5 phút.
        /// Ví dụ: Ca 08:00, quẹt thẻ lúc 08:05 vẫn được ghi nhận là "Đúng giờ".
        /// </summary>
        public const int GracePeriodMinutes = 15;

        /// <summary>
        /// Số phút cho phép nhân viên quẹt thẻ về sớm trước khi hết ca mà KHÔNG bị tính là "Về sớm".
        /// Mặc định: 5 phút.
        /// Ví dụ: Ca kết thúc 16:00, quẹt thẻ từ 15:55 vẫn được ghi nhận là hoàn thành ca đúng giờ.
        /// </summary>
        public const int EarlyCheckOutMinutes = 15;

        /// <summary>
        /// Số phút tối đa được phép đi muộn trước khi hệ thống đánh dấu là "Vắng mặt" (Bỏ ca).
        /// Mặc định: 60 phút.
        /// Ví dụ: Ca 08:00, nếu sau 09:00 nhân viên vẫn chưa quẹt thẻ thì tính là vắng không phép.
        /// </summary>
        public const int LateThresholdMinutes = 45;
    }
}
