using RWPM.Common.Enums;
using RWPM.Models.Common;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RWPM.Models.Entities
{
    [Table("AttendanceAdjustmentRequest")]
    public class AttendanceAdjustmentRequest : AuditableEntity
    {
        [Key]
        public int AttendanceAdjustmentRequestId { get; set; }

        public int? AttendanceRecordId { get; set; }
        [ForeignKey("AttendanceRecordId")]
        public virtual AttendanceRecord? AttendanceRecord { get; set; }

        [Required]
        public int EmployeeId { get; set; }
        [ForeignKey("EmployeeId")]
        public virtual Employee Employee { get; set; } = null!;

        [Required]
        public int StoreId { get; set; }
        [ForeignKey("StoreId")]
        public virtual Store Store { get; set; } = null!;

        [Required]
        [Column(TypeName = "date")]
        public DateTime WorkDate { get; set; }

        public int? ShiftId { get; set; }
        [ForeignKey("ShiftId")]
        public virtual Shift? Shift { get; set; }

        public TimeSpan? CurrentCheckInTime { get; set; }
        public TimeSpan? CurrentCheckOutTime { get; set; }

        public TimeSpan? RequestedCheckInTime { get; set; }
        public TimeSpan? RequestedCheckOutTime { get; set; }

        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        public RegistrationStatus Status { get; set; } = RegistrationStatus.Pending;

        [MaxLength(255)]
        public string? RejectionReason { get; set; }

        [MaxLength(50)]
        public string? ApprovedBy { get; set; }

        public DateTime? ApprovedAt { get; set; }
    }
}
