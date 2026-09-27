namespace RWPM.Common.Helper
{
    public static class ShiftTimeHelper
    {
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
