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

        [ForeignKey("Username")]
        public virtual Acc Account { get; set; }
    }
}
