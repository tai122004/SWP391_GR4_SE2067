using RWPM.Common.Attributes;
using RWPM.Common.Helper;
using System.ComponentModel.DataAnnotations;

namespace RWPM.Models.ViewModels.Shift;

public class ShiftCreateVM : IValidatableObject
{
    [RequiredLocalization, MaxLengthLocalized(100)]
    public string ShiftName { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public bool EndsNextDay { get; set; }
    public TimeSpan? BreakStartTime { get; set; }
    public TimeSpan? BreakEndTime { get; set; }
    public bool BreakStartsNextDay { get; set; }
    public bool BreakEndsNextDay { get; set; }
    [RequiredLocalization, Range(0,60)] public int? GracePeriodMinutes { get; set; } = RWPM.Common.Constants.ShiftDefaults.GracePeriodMinutes;
    [RequiredLocalization, Range(0,60)] public int? EarlyCheckOutMinutes { get; set; } = RWPM.Common.Constants.ShiftDefaults.EarlyCheckOutMinutes;
    [DataType(DataType.Date)] public DateTime EffectiveFrom { get; set; } = DateTime.Today;
    [DataType(DataType.Date)] public DateTime? EffectiveTo { get; set; }
    [MaxLengthLocalized(255)] public string? Description { get; set; }
    public List<int> StoreIds { get; set; } = new();
    public bool IsActive { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (StoreIds.Count == 0)
            yield return new ValidationResult(Resources.Shared.Shift.ResourceManager.GetString("ValidationStores"), new[] { nameof(StoreIds) });
        foreach (var error in ShiftRules.Validate(ToEntity()))
            yield return new ValidationResult(error);
    }

    public Entities.Shift ToEntity() => new()
    {
        ShiftName = ShiftName?.Trim() ?? string.Empty, StartTime = StartTime, EndTime = EndTime,
        EndDayOffset = (byte)(EndsNextDay ? 1 : 0),
        BreakStartTime = BreakStartTime, BreakEndTime = BreakEndTime,
        BreakStartDayOffset = BreakStartTime.HasValue ? (byte)(BreakStartsNextDay ? 1 : 0) : null,
        BreakEndDayOffset = BreakEndTime.HasValue ? (byte)(BreakEndsNextDay ? 1 : 0) : null,
        GracePeriodMinutes = GracePeriodMinutes, EarlyCheckOutMinutes = EarlyCheckOutMinutes,
        EffectiveFrom = EffectiveFrom.Date, EffectiveTo = EffectiveTo?.Date,
        Description = Description?.Trim(), IsActive = IsActive,
        StoreShifts = StoreIds.Distinct().Select(id => new Entities.StoreShift { StoreId = id }).ToList()
    };
}
