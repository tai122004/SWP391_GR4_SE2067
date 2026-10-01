namespace RWPM.Common.Helper
{
    public static class ShiftTimeHelper
    {
        public static (DateTime StartAt, DateTime EndAt) GetDateTimeRange(DateTime date, RWPM.Models.Entities.Shift shift) =>
            (date.Date.Add(shift.StartTime), date.Date.AddDays(shift.EndDayOffset).Add(shift.EndTime));

        public static double GetWorkedHours(DateTime workDate, RWPM.Models.Entities.Shift shift, DateTime checkIn, DateTime checkOut)
        {
            var (start, end) = GetDateTimeRange(workDate, shift);
            var actualStart = checkIn > start ? checkIn : start;
            var actualEnd = checkOut < end ? checkOut : end;
            if (actualEnd <= actualStart) return 0;
            var minutes = (actualEnd - actualStart).TotalMinutes;
            if (shift.BreakStartTime.HasValue && shift.BreakEndTime.HasValue)
            {
                var breakStart = workDate.Date.AddDays(shift.BreakStartDayOffset ?? 0).Add(shift.BreakStartTime.Value);
                var breakEnd = workDate.Date.AddDays(shift.BreakEndDayOffset ?? 0).Add(shift.BreakEndTime.Value);
                var overlapStart = actualStart > breakStart ? actualStart : breakStart;
                var overlapEnd = actualEnd < breakEnd ? actualEnd : breakEnd;
                if (overlapEnd > overlapStart) minutes -= (overlapEnd - overlapStart).TotalMinutes;
            }
            return minutes / 60;
        }
        public static TimeSpan GetDuration(TimeSpan startTime, TimeSpan endTime)
        {
            if (startTime == endTime)
                return TimeSpan.Zero;

            var duration = endTime - startTime;
            return duration < TimeSpan.Zero ? duration.Add(TimeSpan.FromDays(1)) : duration;
        }

        public static (DateTime StartAt, DateTime EndAt) GetDateTimeRange(DateTime workDate, TimeSpan startTime, TimeSpan endTime)
        {
            var startAt = workDate.Date.Add(startTime);
            var endAt = workDate.Date.Add(endTime);
            if (endTime < startTime)
                endAt = endAt.AddDays(1);

            return (startAt, endAt);
        }

        public static bool IsOvernight(TimeSpan startTime, TimeSpan endTime) => endTime < startTime;
    }
}
