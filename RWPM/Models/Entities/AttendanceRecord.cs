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

        public bool IsAdjusted { get; set; } = false;

        public virtual ICollection<AttendanceAdjustmentHistory> AdjustmentHistories { get; set; } = new List<AttendanceAdjustmentHistory>();

        [NotMapped]
        public double? WorkHours
        {
            get
            {
                if (Shift == null || !CheckInTime.HasValue || !CheckOutTime.HasValue) return null;
                var start = Date.Date.Add(Shift.StartTime);
                var checkInActual = new[] { -1, 0, 1 }.Select(day => Date.Date.AddDays(day).Add(CheckInTime.Value))
                    .OrderBy(time => Math.Abs((time - start).TotalMinutes)).First();
                var checkOutActual = new[] { -1, 0, 1, 2 }.Select(day => Date.Date.AddDays(day).Add(CheckOutTime.Value))
                    .Where(time => time >= checkInActual).OrderBy(time => time).First();

                var shiftRange = RWPM.Common.Helper.ShiftTimeHelper.GetDateTimeRange(Date, Shift);
                
                int lateMins = (int)(checkInActual - shiftRange.StartAt).TotalMinutes;
                int earlyMins = (int)(shiftRange.EndAt - checkOutActual).TotalMinutes;

                // Nếu không phải do HR/Admin chỉnh sửa (IsAdjusted = false) thì chạy luật phạt tự động
                if (!IsAdjusted)
                {
                    // 1. Phạt đi muộn
                    if (lateMins > 60)
                    {
                        return 0; // Đi muộn quá 1 tiếng -> Mất trắng ca làm
                    }
                    else if (lateMins > 0 && lateMins <= 15)
                    {
                        // Châm chước 15 phút đầu -> Coi như đi đúng giờ
                        checkInActual = shiftRange.StartAt;
                    }
                    // Nếu lateMins từ 16 đến 60 thì giữ nguyên checkInActual (trừ lương theo số phút muộn)

                    // 2. Phạt về sớm
                    if (earlyMins > 0 && earlyMins <= 15)
                    {
                        // Châm chước về sớm 15 phút -> Coi như về đúng giờ
                        checkOutActual = shiftRange.EndAt;
                    }
                    // Nếu earlyMins > 15 thì giữ nguyên checkOutActual (trừ lương theo số phút về sớm)
                }

                return RWPM.Common.Helper.ShiftTimeHelper.GetWorkedHours(Date, Shift, checkInActual, checkOutActual);
            }
        }
    }
}
