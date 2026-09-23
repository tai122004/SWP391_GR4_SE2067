using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RWPM.Models.Entities
{
    [Table("AttendanceRecord")]
    public class AttendanceRecord
    {
        [Key]
        public int AttendanceId { get; set; }

        [Required]
        [Column(TypeName = "nvarchar(30)")]
        public string Username { get; set; } // Link to Acc table directly as Acc acts as the user. (Wait, let's check Employee vs Acc)

        [Required]
        [Column(TypeName = "date")]
        public DateTime Date { get; set; }

        public TimeSpan? CheckInTime { get; set; }
        
        public TimeSpan? CheckOutTime { get; set; }

        public double? CheckInLatitude { get; set; }

        public double? CheckInLongitude { get; set; }

        public double? CheckOutLatitude { get; set; }

        public double? CheckOutLongitude { get; set; }

        public double? DistanceMeters { get; set; }

        public string? CheckInPhotoPath { get; set; }

        public string? CheckOutPhotoPath { get; set; }

        [ForeignKey("Username")]
        public virtual Acc Account { get; set; }

        public int? ShiftId { get; set; }

        [ForeignKey("ShiftId")]
        public virtual Shift? Shift { get; set; }
    }
}
