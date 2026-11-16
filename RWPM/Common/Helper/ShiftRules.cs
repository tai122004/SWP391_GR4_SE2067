using RWPM.Common.Constants;
using RWPM.Models.Entities;

namespace RWPM.Common.Helper;
public static class ShiftRules
{
    private static string Message(string key) => Resources.Shared.Shift.ResourceManager.GetString(key) ?? key;
    public static IEnumerable<string> Validate(Shift shift)
    {
        if (string.IsNullOrWhiteSpace(shift.ShiftName) || shift.ShiftName.Trim().Length > 100)
        {
            yield return Message("ValidationName");
        }

        if (shift.Description?.Length > 255)
        {
            yield return Message("ValidationDescription");
        }

        if (shift.StartTime < TimeSpan.Zero
            || shift.StartTime >= TimeSpan.FromDays(1)
            || shift.EndTime < TimeSpan.Zero
            || shift.EndTime >= TimeSpan.FromDays(1)
            || shift.EndDayOffset > 1)
        {
            yield return Message("ValidationTime");
        }

        if (shift.Duration.TotalMinutes is < 120 or > 720)
        {
            yield return Message("ValidationDuration");
        }

        foreach (var error in ValidateBreak(shift))
        {
            yield return error;
        }

        if (shift.EffectiveFrom == default || shift.EffectiveTo?.Date < shift.EffectiveFrom.Date)
        {
            yield return Message("ValidationDates");
        }

        if (!shift.GracePeriodMinutes.HasValue
            || !shift.EarlyCheckOutMinutes.HasValue
            || shift.GracePeriodMinutes is < 0 or > 60
            || shift.EarlyCheckOutMinutes is < 0 or > 60)
        {
            yield return Message("ValidationTolerance");
        }

        //if ((shift.GracePeriodMinutes ?? ShiftDefaults.GracePeriodMinutes) >= ShiftDefaults.LateThresholdMinutes)
        //{
        //    yield return Message("ValidationGraceThreshold");
        //}
    }

    private static IEnumerable<string> ValidateBreak(Shift shift)
    {
        var hasBreak = shift.BreakStartTime.HasValue
            || shift.BreakEndTime.HasValue
            || shift.BreakStartDayOffset.HasValue
            || shift.BreakEndDayOffset.HasValue;
        var hasRequiredBreak = shift.Duration.TotalMinutes >= ShiftDefaults.WorkMinutesRequiringBreak;

        if (!hasBreak)
        {
            if (hasRequiredBreak)
            {
                var requiredBreakMinutes = GetRequiredBreakMinutes(shift, null, null);
                yield return Message(requiredBreakMinutes == ShiftDefaults.MinimumNightBreakMinutes
                    ? "ValidationBreakRequiredNight"
                    : "ValidationBreakRequiredStandard");
            }
            yield break;
        }

        if (!shift.BreakStartTime.HasValue
            || !shift.BreakEndTime.HasValue
            || !shift.BreakStartDayOffset.HasValue
            || !shift.BreakEndDayOffset.HasValue)
        {
            yield return Message("ValidationBreakFields");
            yield break;
        }

        var breakStart = shift.BreakStartTime.Value.Add(TimeSpan.FromDays(shift.BreakStartDayOffset.Value));
        var breakEnd = shift.BreakEndTime.Value.Add(TimeSpan.FromDays(shift.BreakEndDayOffset.Value));
        var breakDuration = (breakEnd - breakStart).TotalMinutes;
        if (shift.BreakStartDayOffset > 1
            || shift.BreakEndDayOffset > 1
            || shift.BreakStartTime < TimeSpan.Zero
            || shift.BreakStartTime >= TimeSpan.FromDays(1)
            || shift.BreakEndTime < TimeSpan.Zero
            || shift.BreakEndTime >= TimeSpan.FromDays(1)
            || breakStart < shift.StartTime
            || breakEnd > shift.EndTime.Add(TimeSpan.FromDays(shift.EndDayOffset))
            || breakEnd <= breakStart
            || breakDuration > ShiftDefaults.MaximumBreakMinutes
            || breakDuration >= shift.Duration.TotalMinutes)
        {
            yield return Message("ValidationBreakRange");
            yield break;
        }

        if (shift.ScheduledProductiveHours * 60 < ShiftDefaults.MinimumProductiveWorkMinutes)
        {
            yield return Message("ValidationWorkDuration");
        }

        if (hasRequiredBreak)
        {
            var requiredBreakMinutes = GetRequiredBreakMinutes(shift, breakStart, breakEnd);
            if (breakDuration < requiredBreakMinutes)
            {
                yield return Message(requiredBreakMinutes == ShiftDefaults.MinimumNightBreakMinutes
                    ? "ValidationBreakMinimumNight"
                    : "ValidationBreakMinimumStandard");
            }
        }
    }

    private static int GetRequiredBreakMinutes(Shift shift, TimeSpan? breakStart, TimeSpan? breakEnd)
    {
        var anchorDate = new DateTime(2000, 1, 1);
        var shiftStart = anchorDate.Add(shift.StartTime);
        var shiftEnd = anchorDate.AddDays(shift.EndDayOffset).Add(shift.EndTime);
        var actualBreakStart = breakStart.HasValue ? anchorDate.Add(breakStart.Value) : (DateTime?)null;
        var actualBreakEnd = breakEnd.HasValue ? anchorDate.Add(breakEnd.Value) : (DateTime?)null;
        var nightWorkMinutes = 0d;

        for (var nightDate = shiftStart.Date.AddDays(-1); nightDate <= shiftEnd.Date; nightDate = nightDate.AddDays(1))
        {
            var nightStart = nightDate.AddHours(22);
            var nightEnd = nightDate.AddDays(1).AddHours(6);
            nightWorkMinutes += OverlapMinutes(shiftStart, shiftEnd, nightStart, nightEnd);
            if (actualBreakStart.HasValue && actualBreakEnd.HasValue)
            {
                nightWorkMinutes -= OverlapMinutes(actualBreakStart.Value, actualBreakEnd.Value, nightStart, nightEnd);
            }
        }

        if (nightWorkMinutes >= ShiftDefaults.NightWorkMinutesForLongerBreak)
            return ShiftDefaults.MinimumNightBreakMinutes;

        return ShiftDefaults.MinimumBreakMinutes;
    }

    private static double OverlapMinutes(DateTime firstStart, DateTime firstEnd, DateTime secondStart, DateTime secondEnd)
    {
        var start = firstStart > secondStart ? firstStart : secondStart;
        var end = firstEnd < secondEnd ? firstEnd : secondEnd;
        return end > start ? (end - start).TotalMinutes : 0;
    }
}
