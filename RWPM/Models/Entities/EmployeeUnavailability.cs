using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RWPM.Models.Common;

namespace RWPM.Models.Entities
{
    /// <summary>
    /// Ngày cụ thể nhân viên không thể làm việc. Khác nghỉ phép: không trừ phép, không cần duyệt,
    /// chỉ là thông tin xếp lịch; phải khai trước tối thiểu AvailabilityRules.UnavailabilityLeadDays ngày.
    /// </summary>
    [Table("EmployeeUnavailability")]
    public class EmployeeUnavailability : AuditableEntity
    {
        [Key]
        public int EmployeeUnavailabilityId { get; set; }

        public int EmployeeId { get; set; }

        [Column(TypeName = "date")]
        public DateTime Date { get; set; }

        [MaxLength(255)]
        public string? Reason { get; set; }

        [ForeignKey(nameof(EmployeeId))]
        public virtual Employee Employee { get; set; } = null!;
    }
}
