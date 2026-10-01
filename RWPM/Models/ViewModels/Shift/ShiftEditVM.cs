namespace RWPM.Models.ViewModels.Shift;

public class ShiftEditVM : ShiftCreateVM
{
    public int ShiftId { get; set; }
    public ShiftEditVM() { }
    public ShiftEditVM(Entities.Shift shift)
    {
        ShiftId = shift.ShiftId; ShiftName = shift.ShiftName;
        StartTime = shift.StartTime; EndTime = shift.EndTime; EndsNextDay = shift.EndDayOffset == 1;
        BreakStartTime = shift.BreakStartTime; BreakEndTime = shift.BreakEndTime;
        BreakStartsNextDay = shift.BreakStartDayOffset == 1; BreakEndsNextDay = shift.BreakEndDayOffset == 1;
        GracePeriodMinutes = shift.GracePeriodMinutes; EarlyCheckOutMinutes = shift.EarlyCheckOutMinutes;
        EffectiveFrom = shift.EffectiveFrom; EffectiveTo = shift.EffectiveTo;
        Description = shift.Description; IsActive = shift.IsActive;
        StoreIds = shift.StoreShifts.Where(x => x.IsActive).Select(x => x.StoreId).ToList();
    }
    public void ApplyToEntity(Entities.Shift entity)
    {
        var value = ToEntity();
        entity.ShiftName = value.ShiftName; entity.StartTime = value.StartTime; entity.EndTime = value.EndTime;
        entity.EndDayOffset = value.EndDayOffset; entity.BreakStartTime = value.BreakStartTime;
        entity.BreakEndTime = value.BreakEndTime; entity.BreakStartDayOffset = value.BreakStartDayOffset;
        entity.BreakEndDayOffset = value.BreakEndDayOffset; entity.GracePeriodMinutes = value.GracePeriodMinutes;
        entity.EarlyCheckOutMinutes = value.EarlyCheckOutMinutes; entity.EffectiveFrom = value.EffectiveFrom;
        entity.EffectiveTo = value.EffectiveTo; entity.Description = value.Description;
        entity.IsActive = value.IsActive; entity.StoreShifts = value.StoreShifts;
    }
}
