using System.Globalization;
using R = RWPM.Resources.Shared.AvailabilityResource;

namespace RWPM.Common.Validation;

/// <summary>
/// Quy tắc dùng chung cho "Thời gian có thể làm việc" (khung giờ lặp lại hằng tuần + ngày không thể làm).
/// Toàn bộ là hàm thuần (không truy cập DB) để các màn hình khác (xếp ca, nhu cầu ca...) dùng lại được.
/// </summary>
public static class AvailabilityRules
{
    /// <summary>Ngày không thể làm phải khai trước tối thiểu bao nhiêu ngày (Date &gt;= hôm nay + 3).</summary>
    public const int UnavailabilityLeadDays = 3;
    /// <summary>Chỉ được khai ngày không thể làm trong vòng bao nhiêu ngày tới.</summary>
    public const int UnavailabilityMaxAheadDays = 90;
    /// <summary>Số khung giờ tối đa (chờ duyệt + đã duyệt) trên một thứ trong tuần.</summary>
    public const int MaxWindowsPerDay = 3;
    /// <summary>Tổng số khung giờ tối đa (chờ duyệt + đã duyệt) của một nhân viên = 7 * MaxWindowsPerDay.</summary>
    public const int MaxWindowsTotal = 7 * MaxWindowsPerDay;
    public const int MaxTextLength = 255;
    /// <summary>Số ngày tới dùng cho tóm tắt (popover) ngày không thể làm.</summary>
    public const int SummaryDays = 30;

    private const int MinutesPerDay = 24 * 60;
    private const int MinutesPerWeek = 7 * MinutesPerDay;

    public static DateTime MinUnavailableDate(DateTime today) => today.Date.AddDays(UnavailabilityLeadDays);
    public static DateTime MaxUnavailableDate(DateTime today) => today.Date.AddDays(UnavailabilityMaxAheadDays);

    /// <summary>Thứ tự hiển thị: Thứ Hai -> Chủ nhật.</summary>
    public static readonly DayOfWeek[] WeekOrder =
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    };

    public static int SortIndex(DayOfWeek day) => ((int)day + 6) % 7;

    public static string DayName(DayOfWeek day) => R.ResourceManager.GetString("Day_" + (int)day, R.Culture) ?? day.ToString();
    public static string DayShortName(DayOfWeek day) => R.ResourceManager.GetString("DayShort_" + (int)day, R.Culture) ?? day.ToString()[..3];

    /// <summary>Khung giờ có qua nửa đêm không (EndTime &lt;= StartTime).</summary>
    public static bool CrossesMidnight(TimeSpan start, TimeSpan end) => end <= start;

    /// <summary>Độ dài khung giờ (khung qua đêm được cộng 24h).</summary>
    public static TimeSpan Duration(TimeSpan start, TimeSpan end) => CrossesMidnight(start, end) ? end + TimeSpan.FromDays(1) - start : end - start;

    public static string FormatTime(TimeSpan t) => t.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

    /// <summary>"08:00–12:00" hoặc "22:00–06:00 (+1)" với khung qua đêm.</summary>
    public static string FormatRange(TimeSpan start, TimeSpan end)
        => FormatTime(start) + "–" + FormatTime(end) + (CrossesMidnight(start, end) ? " " + R.NextDaySuffix : string.Empty);

    /// <summary>Phân tích "HH:mm" (24h). Trả về null nếu không hợp lệ.</summary>
    public static TimeSpan? ParseTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim();
        if (TimeSpan.TryParseExact(text, new[] { @"hh\:mm", @"h\:mm", @"hh\:mm\:ss" }, CultureInfo.InvariantCulture, out var t)
            && t >= TimeSpan.Zero && t < TimeSpan.FromDays(1))
            return new TimeSpan(t.Hours, t.Minutes, 0);
        return null;
    }

    /// <summary>Hai khung giờ hằng tuần có chồng lấn không (tính cả phần qua nửa đêm sang thứ kế tiếp, vòng tuần CN -> T2).</summary>
    public static bool WindowsOverlap(DayOfWeek dayA, TimeSpan startA, TimeSpan endA, DayOfWeek dayB, TimeSpan startB, TimeSpan endB)
    {
        var a = (int)dayA * MinutesPerDay + (int)startA.TotalMinutes;
        var la = (int)Duration(startA, endA).TotalMinutes;
        var b = (int)dayB * MinutesPerDay + (int)startB.TotalMinutes;
        var lb = (int)Duration(startB, endB).TotalMinutes;
        for (var k = -1; k <= 1; k++)
        {
            var bs = b + k * MinutesPerWeek;
            if (a < bs + lb && bs < a + la) return true;
        }
        return false;
    }

    /// <summary>
    /// Khoảng thời gian thực [start, end) có nằm trọn trong một khung giờ hằng tuần không.
    /// Khung giờ của thứ trước đó qua nửa đêm cũng được tính; các khung liền kề/chồng nhau được gộp lại thành một.
    /// </summary>
    public static bool FitsWindows(IEnumerable<(DayOfWeek Day, TimeSpan Start, TimeSpan End)> windows, DateTime start, DateTime end)
    {
        var list = windows.ToList();
        if (list.Count == 0) return false;
        if (end < start) end = start;

        var instances = new List<(DateTime S, DateTime E)>();
        for (var d = start.Date.AddDays(-1); d <= end.Date; d = d.AddDays(1))
        {
            foreach (var w in list.Where(w => w.Day == d.DayOfWeek))
            {
                var ws = d.Add(w.Start);
                var we = d.Add(w.End);
                if (CrossesMidnight(w.Start, w.End)) we = we.AddDays(1);
                instances.Add((ws, we));
            }
        }
        if (instances.Count == 0) return false;

        instances.Sort((x, y) => x.S.CompareTo(y.S));
        var merged = new List<(DateTime S, DateTime E)>();
        foreach (var it in instances)
        {
            if (merged.Count > 0 && it.S <= merged[^1].E)
            {
                var last = merged[^1];
                merged[^1] = (last.S, it.E > last.E ? it.E : last.E);
            }
            else merged.Add(it);
        }
        return merged.Any(m => m.S <= start && end <= m.E);
    }

    // ===== Mã hóa danh sách khung giờ cho tham số thông báo (dịch lúc hiển thị) =====
    // Định dạng: "1|08:00|12:00;3|22:00|06:00"

    public static string EncodeWindows(IEnumerable<(DayOfWeek Day, TimeSpan Start, TimeSpan End)> windows)
        => string.Join(";", windows
            .OrderBy(w => SortIndex(w.Day)).ThenBy(w => w.Start)
            .Select(w => ((int)w.Day).ToString(CultureInfo.InvariantCulture) + "|" + FormatTime(w.Start) + "|" + FormatTime(w.End)));

    /// <summary>Dịch chuỗi đã mã hóa sang "T2 08:00–12:00, T4 22:00–06:00 (+1)" theo ngôn ngữ hiện tại. Bỏ qua phần lỗi.</summary>
    public static string DescribeEncoded(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded)) return string.Empty;
        var parts = new List<string>();
        foreach (var item in encoded.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = item.Split('|');
            if (p.Length != 3 || !int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var d) || d < 0 || d > 6) continue;
            var s = ParseTime(p[1]); var e = ParseTime(p[2]);
            if (s == null || e == null) continue;
            parts.Add(DayShortName((DayOfWeek)d) + " " + FormatRange(s.Value, e.Value));
        }
        return string.Join(", ", parts);
    }
}
