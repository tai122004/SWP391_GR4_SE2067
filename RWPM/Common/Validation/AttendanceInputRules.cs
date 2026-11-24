using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RWPM.Common.Enums;
using RWPM.Infrastructure.Data;

namespace RWPM.Common.Validation;

// Kiểm tra tính hợp lệ dữ liệu đầu vào cho chấm công
public static class AttendanceInputRules
{
    public const long MaxPhotoBytes = 5 * 1024 * 1024; // Dung lượng ảnh tối đa 5MB

    // Parse và validate tọa độ GPS gửi lên từ client
    public static (double? Latitude, double? Longitude) ParseCoordinates(string? latitude, string? longitude)
    {
        InputRules.Require(string.IsNullOrWhiteSpace(latitude) == string.IsNullOrWhiteSpace(longitude), "UserLatitude",
            "Cần cung cấp đủ vĩ độ và kinh độ.", "Provide both latitude and longitude.");
        if (string.IsNullOrWhiteSpace(latitude)) return (null, null);

        // Chuẩn hóa dấu phẩy thành dấu chấm và kiểm tra vĩ độ [-90, 90]
        InputRules.Require(double.TryParse(latitude.Trim().Replace(',', '.'), NumberStyles.Float,
            CultureInfo.InvariantCulture, out var lat) && double.IsFinite(lat) && lat is >= -90 and <= 90,
            "UserLatitude", "Vĩ độ phải từ -90 đến 90.", "Latitude must be between -90 and 90.");

        // Kiểm tra kinh độ [-180, 180]
        InputRules.Require(double.TryParse(longitude!.Trim().Replace(',', '.'), NumberStyles.Float,
            CultureInfo.InvariantCulture, out var lng) && double.IsFinite(lng) && lng is >= -180 and <= 180,
            "UserLongitude", "Kinh độ phải từ -180 đến 180.", "Longitude must be between -180 and 180.");

        return (lat, lng);
    }

    // Kiểm tra tọa độ chi nhánh có hợp lệ không
    public static void ValidateCoordinates(double? latitude, double? longitude)
    {
        InputRules.Require(latitude.HasValue == longitude.HasValue, "Latitude", "Cần nhập đủ vĩ độ và kinh độ.",
            "Provide both latitude and longitude.");
        if (!latitude.HasValue) return;

        InputRules.Require(double.IsFinite(latitude.Value) && latitude.Value is >= -90 and <= 90, "Latitude",
            "Vĩ độ phải từ -90 đến 90.", "Latitude must be between -90 and 90.");
        InputRules.Require(double.IsFinite(longitude!.Value) && longitude.Value is >= -180 and <= 180, "Longitude",
            "Kinh độ phải từ -180 đến 180.", "Longitude must be between -180 and 180.");
    }

    // Kiểm tra bán kính cửa hàng khi admin cấu hình
    public static void ValidateRadius(int minimum, int maximum)
    {
        InputRules.Require(maximum is >= 10 and <= 10000, "AllowedRadiusMeters", "Bán kính cho phép phải từ 10 đến 10.000 m.",
            "Allowed radius must be between 10 and 10,000 meters.");
        InputRules.Require(minimum >= 0, "MinAllowedDistanceMeters", "Khoảng cách tối thiểu không được âm.",
            "Minimum distance cannot be negative.");
        InputRules.Require(minimum < maximum, "MinAllowedDistanceMeters",
            "Khoảng cách tối thiểu phải nhỏ hơn bán kính cho phép.", "Minimum distance must be less than allowed radius.");
    }

    // Parse chuỗi giờ hh:mm sang TimeSpan
    public static TimeSpan? ParseClockTime(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var formats = new[] { @"hh\:mm", @"h\:mm", @"hh\:mm\:ss", @"h\:mm\:ss" };
        if (TimeSpan.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, out var ts))
        {
            InputRules.Require(ts.TotalHours is >= 0 and < 24, field, "Giờ phải trong khoảng 00:00 - 23:59.",
                "Time must be between 00:00 and 23:59.");
            return ts;
        }

        if (DateTime.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            return dt.TimeOfDay;
        }

        throw new InputValidationException(field, InputRules.Text("Định dạng giờ không hợp lệ.", "Invalid time format."));
    }

    public static void ValidateClockTime(TimeSpan? time, string field)
    {
        if (!time.HasValue) return;
        InputRules.Require(time.Value.TotalHours is >= 0 and < 24, field, "Giờ phải trong khoảng 00:00 - 23:59.",
            "Time must be between 00:00 and 23:59.");
    }

    // Kiểm tra giờ ra phải sau giờ vào (trừ ca làm qua đêm)
    public static void ValidateTimeOrder(DateTime workDate, TimeSpan? checkIn, TimeSpan? checkOut, int endDayOffset)
    {
        if (!checkIn.HasValue || !checkOut.HasValue) return;
        var inDateTime = workDate.Date.Add(checkIn.Value);
        var outDateTime = workDate.Date.AddDays(endDayOffset).Add(checkOut.Value);
        InputRules.Require(outDateTime > inDateTime, "RequestedCheckOutTime", "Giờ ra phải sau giờ vào.",
            "Check-out time must be after check-in time.");
    }

    // Chặn thay đổi nếu ngày này thuộc kỳ lương đã chốt
    public static async Task EnsureUnlockedAsync(DefaultDatabaseContext context, DateTime workDate)
    {
        var targetDate = workDate.Date;
        var isLocked = await context.Payroll.AnyAsync(p => p.Status == PayrollStatus.Locked
            && p.StartDate <= targetDate && p.EndDate >= targetDate);
        InputRules.Require(!isLocked, "WorkDate",
            "Ngày làm việc thuộc kỳ lương đã chốt. Không thể thay đổi chấm công.",
            "The work date belongs to a locked payroll period. Attendance cannot be modified.");
    }
}