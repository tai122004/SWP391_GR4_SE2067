using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RWPM.Models.Entities
{
    [Table("PayrollItemSnapshot")]
    public class PayrollItemSnapshot
    {
        [Key]
        public int PayrollItemSnapshotId { get; set; }

        [Required]
        public int PayrollDetailId { get; set; }

        [Required]
        [Column(TypeName = "date")]
        public DateTime Date { get; set; }

        public int? ShiftId { get; set; }

        [MaxLength(100)]
        public string? ShiftName { get; set; }

        public TimeSpan? StartTime { get; set; }

        public TimeSpan? EndTime { get; set; }

        public TimeSpan? CheckInTime { get; set; }

        public TimeSpan? CheckOutTime { get; set; }

        public double ActualWorkHours { get; set; } = 0; // Giờ làm thực tế từ chấm công

        public double PaidLeaveHours { get; set; } = 0; // Giờ nghỉ phép hưởng lương

        public double TotalPayableHours { get; set; } = 0; // Tổng giờ được tính lương (Actual + PaidLeave)

        [Column(TypeName = "decimal(18,2)")]
        public decimal HourlyRate { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; } = 0;

        [Required]
        [MaxLength(50)]
        public string SourceType { get; set; } = "Attendance"; // Attendance, PaidLeave, UnpaidLeave, MissingAttendance

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Valid"; // Valid, Late, Early, Warning, ApprovedLeave, Missing

        [MaxLength(255)]
        public string? Note { get; set; }

        // Navigation
        [ForeignKey("PayrollDetailId")]
        public virtual PayrollDetail PayrollDetail { get; set; } = null!;

        [ForeignKey("ShiftId")]
        public virtual Shift? Shift { get; set; }
    }
}
