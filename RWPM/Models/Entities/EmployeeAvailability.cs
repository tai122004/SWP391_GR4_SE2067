using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RWPM.Common.Enums;
using RWPM.Models.Common;

namespace RWPM.Models.Entities
{
    /// <summary>
    /// Khung giờ có thể làm việc lặp lại hằng tuần do nhân viên khai báo (cần Quản lý duyệt).
    /// EndTime &lt;= StartTime nghĩa là khung giờ qua nửa đêm (ví dụ 22:00 - 06:00).
    /// Nhân viên chưa có khung giờ nào được duyệt được coi là làm được mọi lúc.
    /// </summary>
    [Table("EmployeeAvailability")]
    public class EmployeeAvailability : AuditableEntity
    {
        [Key]
        public int EmployeeAvailabilityId { get; set; }

        public int EmployeeId { get; set; }

        public DayOfWeek DayOfWeek { get; set; }

        public TimeSpan StartTime { get; set; }

        public TimeSpan EndTime { get; set; }

        public RegistrationStatus Status { get; set; } = RegistrationStatus.Pending;

        [MaxLength(255)]
        public string? Note { get; set; }

        [MaxLength(255)]
        public string? RejectionReason { get; set; }

        [MaxLength(30)]
        public string? ApprovedBy { get; set; }

        public DateTime? ApprovedAt { get; set; }

        [ForeignKey(nameof(EmployeeId))]
        public virtual Employee Employee { get; set; } = null!;
    }
}
