using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RWPM.Models.Entities
{
    public class AttendanceAdjustmentHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int AttendanceRecordId { get; set; }

        [ForeignKey("AttendanceRecordId")]
        public AttendanceRecord? AttendanceRecord { get; set; }

        // Store old values (can be null if employee missed check-in/out entirely)
        public TimeSpan? OldCheckInTime { get; set; }
        public TimeSpan? OldCheckOutTime { get; set; }

        // Store new adjusted values
        public TimeSpan? NewCheckInTime { get; set; }
        public TimeSpan? NewCheckOutTime { get; set; }

        [Required]
        [StringLength(500)]
        public string Reason { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string ModifiedBy { get; set; } = null!; // Admin/Manager username

        [Required]
        public DateTime ModifiedAt { get; set; } = DateTime.Now;
    }
}
