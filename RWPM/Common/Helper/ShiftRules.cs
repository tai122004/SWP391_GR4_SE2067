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

        if ((shift.GracePeriodMinutes ?? ShiftDefaults.GracePeriodMinutes) >= ShiftDefaults.LateThresholdMinutes)
        {
            yield return Message("ValidationGraceThreshold");
        }
    }

    private static IEnumerable<string> ValidateBreak(Shift shift)
    {
        var hasBreak = shift.BreakStartTime.HasValue
            || shift.BreakEndTime.HasValue
            || shift.BreakStartDayOffset.HasValue
            || shift.BreakEndDayOffset.HasValue;
        if (hasBreak)
        {
            if (!shift.BreakStartTime.HasValue
                || !shift.BreakEndTime.HasValue
                || !shift.BreakStartDayOffset.HasValue
                || !shift.BreakEndDayOffset.HasValue)
            {
                yield return Message("ValidationBreakFields");
            }
            else
            {
                var breakStart = shift.BreakStartTime.Value.Add(TimeSpan.FromDays(shift.BreakStartDayOffset.Value));
                var breakEnd = shift.BreakEndTime.Value.Add(TimeSpan.FromDays(shift.BreakEndDayOffset.Value));
                if (shift.BreakStartDayOffset > 1
                    || shift.BreakEndDayOffset > 1
                    || shift.BreakStartTime < TimeSpan.Zero
                    || shift.BreakStartTime >= TimeSpan.FromDays(1)
                    || shift.BreakEndTime < TimeSpan.Zero
                    || shift.BreakEndTime >= TimeSpan.FromDays(1)
                    || breakStart < shift.StartTime
                    || breakEnd > shift.EndTime.Add(TimeSpan.FromDays(shift.EndDayOffset))
                    || breakEnd <= breakStart
                    || (breakEnd - breakStart).TotalMinutes > 60)
                {
                    yield return Message("ValidationBreakRange");
                }

                if (shift.ScheduledWorkHours < 2)
                {
                    yield return Message("ValidationWorkDuration");
                }
            }
        }
    }
}
