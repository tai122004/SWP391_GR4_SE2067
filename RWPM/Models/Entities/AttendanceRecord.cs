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
        public string Username { get; set; } = null!; // FK tới Acc (tài khoản của nhân viên)

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
        public virtual Acc Account { get; set; } = null!;

        public int? ShiftId { get; set; }

        [ForeignKey("ShiftId")]
        public virtual Shift? Shift { get; set; }

        public bool IsAdjusted { get; set; } = false;

        public virtual ICollection<AttendanceAdjustmentHistory> AdjustmentHistories { get; set; } = new List<AttendanceAdjustmentHistory>();
        public virtual ICollection<AttendanceAdjustmentRequest> AdjustmentRequests { get; set; } = new List<AttendanceAdjustmentRequest>();

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

                // Nếu không phải do HR/Admin chỉnh sửa (IsAdjusted = false) thì áp dụng quy tắc theo thời gian cho phép của ca
                if (!IsAdjusted)
                {
                    int gracePeriod = Shift.GracePeriodMinutes ?? RWPM.Common.Constants.ShiftDefaults.GracePeriodMinutes;
                    int earlyCheckOut = Shift.EarlyCheckOutMinutes ?? RWPM.Common.Constants.ShiftDefaults.EarlyCheckOutMinutes;

                    // 1. Đi muộn: Trong khoảng thời gian cho phép của ca -> Tính đủ giờ ca
                    if (lateMins > 0 && lateMins <= gracePeriod)
                    {
                        checkInActual = shiftRange.StartAt;
                    }
                    // Nếu đi muộn vượt quá thời gian cho phép: Giữ nguyên checkInActual (tính giờ công theo thời gian thực tế vào làm)

                    // 2. Về sớm: Trong khoảng thời gian cho phép của ca -> Tính đủ giờ ca
                    if (earlyMins > 0 && earlyMins <= earlyCheckOut)
                    {
                        checkOutActual = shiftRange.EndAt;
                    }
                    // Nếu về sớm vượt quá thời gian cho phép: Giữ nguyên checkOutActual (tính giờ công theo thời gian thực tế tan làm)
                }

                return RWPM.Common.Helper.ShiftTimeHelper.GetWorkedHours(Date, Shift, checkInActual, checkOutActual);
            }
        }
    }
}
