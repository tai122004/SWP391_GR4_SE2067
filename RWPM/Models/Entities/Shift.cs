using RWPM.Models.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RWPM.Models.Entities;

public class Shift : AuditableEntity, IActivatable
{
    [Key] public int ShiftId { get; set; }
    [Required, MaxLength(100)] public string ShiftName { get; set; } = string.Empty;
    [Column(TypeName = "time(0)")] public TimeSpan StartTime { get; set; }
    [Column(TypeName = "time(0)")] public TimeSpan EndTime { get; set; }
    public byte EndDayOffset { get; set; }
    [Column(TypeName = "time(0)")] public TimeSpan? BreakStartTime { get; set; }
    [Column(TypeName = "time(0)")] public TimeSpan? BreakEndTime { get; set; }
    public byte? BreakStartDayOffset { get; set; }
    public byte? BreakEndDayOffset { get; set; }
    [Required] public int? GracePeriodMinutes { get; set; } = RWPM.Common.Constants.ShiftDefaults.GracePeriodMinutes;
    [Required] public int? EarlyCheckOutMinutes { get; set; } = RWPM.Common.Constants.ShiftDefaults.EarlyCheckOutMinutes;
    [Column(TypeName = "date")] public DateTime EffectiveFrom { get; set; } = DateTime.Today;
    [Column(TypeName = "date")] public DateTime? EffectiveTo { get; set; }
    [MaxLength(255)] public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<StoreShift> StoreShifts { get; set; } = new List<StoreShift>();
    [NotMapped] public TimeSpan Duration => EndTime.Add(TimeSpan.FromDays(EndDayOffset)) - StartTime;
    [NotMapped] public double BreakMinutes => BreakStartTime.HasValue && BreakEndTime.HasValue
        ? (BreakEndTime.Value.Add(TimeSpan.FromDays(BreakEndDayOffset ?? 0))
           - BreakStartTime.Value.Add(TimeSpan.FromDays(BreakStartDayOffset ?? 0))).TotalMinutes : 0;
    // Every configured break is unpaid; use the same calculation as attendance.
    [NotMapped] public double ScheduledWorkHours
    {
        get
        {
            var workDate = new DateTime(2000, 1, 1);
            var (start, end) = RWPM.Common.Helper.ShiftTimeHelper.GetDateTimeRange(workDate, this);
            return RWPM.Common.Helper.ShiftTimeHelper.GetWorkedHours(workDate, this, start, end);
        }
    }
}

public static class ShiftExtensions
{
    public static bool IsAvailableOn(this Shift shift, DateTime workDate) => shift.IsActive
        && shift.EffectiveFrom.Date <= workDate.Date
        && (!shift.EffectiveTo.HasValue || shift.EffectiveTo.Value.Date >= workDate.Date);
    public static string GetLocalizedName(this Shift shift) => shift.ShiftName;
}
