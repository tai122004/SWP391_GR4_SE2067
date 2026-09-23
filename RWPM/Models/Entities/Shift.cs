using RWPM.Common.Enums;
using RWPM.Models.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RWPM.Models.Entities
{
    public class Shift : AuditableEntity, IActivatable
    {
        [Key]
        public int ShiftId { get; set; }

        [Required]
        [MaxLength(20)]
        public string ShiftCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string ShiftName { get; set; } = string.Empty;

        public ShiftType Type { get; set; }

        //Khung giờ chính 
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        //Khung giờ dành cho Ca Gãy
        public TimeSpan? StartTime2 { get; set; }
        public TimeSpan? EndTime2 { get; set; }

        public int BreakMinutes { get; set; }

        // === CẤU HÌNH THỜI GIAN CHẤM CÔNG (Attendance Policy) ===

        /// <summary>
        /// Số phút cho phép đi muộn trước khi tính là "Late".
        /// Null = kế thừa cấu hình mặc định từ <see cref="Common.Constants.ShiftDefaults.GracePeriodMinutes"/>.
        /// Ví dụ: 5 → quẹt thẻ lúc 08:35 vẫn "Đúng giờ" nếu ca bắt đầu 08:30.
        /// </summary>
        public int? GracePeriodMinutes { get; set; }

        /// <summary>
        /// Số phút cho phép quẹt thẻ TRƯỚC giờ bắt đầu ca.
        /// Null = kế thừa cấu hình mặc định từ <see cref="Common.Constants.ShiftDefaults.EarlyCheckInMinutes"/>.
        /// Ví dụ: 15 → ca 08:30, cho phép quẹt thẻ từ 08:15.
        /// </summary>
        public int? EarlyCheckInMinutes { get; set; }

        /// <summary>
        /// Số phút tối đa được phép đi muộn trước khi bị tính "Vắng mặt" (Bỏ ca).
        /// Null = kế thừa cấu hình mặc định từ <see cref="Common.Constants.ShiftDefaults.LateThresholdMinutes"/>.
        /// Ví dụ: 60 → quá 1 tiếng không quẹt thẻ = Bỏ ca.
        /// </summary>
        public int? LateThresholdMinutes { get; set; }

        // === PHÂN LOẠI CA ===

        /// <summary>
        /// true = Ca mẫu (Template) dùng chung cho toàn hệ thống.
        /// false = Ca tùy chỉnh (Custom) cho sự kiện đặc biệt.
        /// </summary>
        public bool IsTemplate { get; set; } = true;

        [MaxLength(255)]
        public string Description { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }

    public static class ShiftExtensions
    {
        public static string GetLocalizedName(this Shift shift)
        {
            return shift.ShiftCode switch
            {
                "S" => Resources.Models.Shift.ShiftName_S,
                "C" => Resources.Models.Shift.ShiftName_C,
                "G" => Resources.Models.Shift.ShiftName_G,
                "HC" => Resources.Models.Shift.ShiftName_HC,
                _ => shift.ShiftName
            };
        }
    }
}
