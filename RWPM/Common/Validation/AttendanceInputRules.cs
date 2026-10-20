using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RWPM.Common.Enums;
using RWPM.Infrastructure.Data;

namespace RWPM.Common.Validation;

public static class AttendanceInputRules
{
    public const long MaxPhotoBytes = 5 * 1024 * 1024;

    public static (double? Latitude, double? Longitude) ParseCoordinates(string? latitude, string? longitude)
    {
        InputRules.Require(string.IsNullOrWhiteSpace(latitude) == string.IsNullOrWhiteSpace(longitude), "UserLatitude",
            "Cần cung cấp đủ vĩ độ và kinh độ.", "Provide both latitude and longitude.");
        if (string.IsNullOrWhiteSpace(latitude)) return (null, null);
        InputRules.Require(double.TryParse(latitude.Trim().Replace(',', '.'), NumberStyles.Float,
            CultureInfo.InvariantCulture, out var lat) && double.IsFinite(lat) && lat is >= -90 and <= 90,
            "UserLatitude", "Vĩ độ phải từ -90 đến 90.", "Latitude must be between -90 and 90.");
        InputRules.Require(double.TryParse(longitude!.Trim().Replace(',', '.'), NumberStyles.Float,
            CultureInfo.InvariantCulture, out var lng) && double.IsFinite(lng) && lng is >= -180 and <= 180,
            "UserLongitude", "Kinh độ phải từ -180 đến 180.", "Longitude must be between -180 and 180.");
        return (lat, lng);
    }

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

    public static void ValidateRadius(int minimum, int maximum)
    {
        InputRules.Require(maximum is >= 10 and <= 10000, "AllowedRadiusMeters", "Bán kính cho phép phải từ 10 đến 10.000 m.",
            "Allowed radius must be between 10 and 10,000 m.");
        InputRules.Require(minimum >= 0 && minimum < maximum, "MinAllowedDistanceMeters",
            "Khoảng cách tối thiểu phải từ 0 đến nhỏ hơn bán kính cho phép.",
            "Minimum distance must be nonnegative and smaller than the allowed radius.");
    }

    public static TimeSpan? ParseClockTime(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        InputRules.Require(TimeSpan.TryParseExact(value.Trim(), new[] { @"hh\:mm", @"hh\:mm\:ss" },
            CultureInfo.InvariantCulture, out var time) && time >= TimeSpan.Zero && time < TimeSpan.FromDays(1)
            && time.Seconds == 0, field,
            "Giờ phải đúng định dạng HH:mm trong khoảng 00:00–23:59.",
            "Time must use HH:mm between 00:00 and 23:59.");
        return time;
    }

    public static void ValidateClockTime(TimeSpan? value, string field)
    {
        InputRules.Require(!value.HasValue || (value.Value >= TimeSpan.Zero && value.Value < TimeSpan.FromDays(1)), field,
            "Giờ phải nằm trong khoảng 00:00–23:59.", "Time must be between 00:00 and 23:59.");
    }

    public static void ValidateTimeOrder(DateTime workDate, TimeSpan? checkIn, TimeSpan? checkOut, byte endDayOffset)
    {
        ValidateClockTime(checkIn, "RequestedCheckInTime");
        ValidateClockTime(checkOut, "RequestedCheckOutTime");
        if (!checkIn.HasValue || !checkOut.HasValue) return;
        var start = workDate.Date.Add(checkIn.Value);
        var end = workDate.Date.Add(checkOut.Value);
        if (end <= start && endDayOffset == 1) end = end.AddDays(1);
        InputRules.Require(end > start && end - start <= TimeSpan.FromHours(16), "RequestedCheckOutTime",
            "Giờ ra phải sau giờ vào; chỉ ca qua đêm mới được sang ngày tiếp theo.",
            "Check-out must follow check-in; only overnight shifts may cross midnight.");
    }

    public static async Task EnsureUnlockedAsync(DefaultDatabaseContext db, DateTime date)
    {
        InputRules.Require(!await db.Payroll.AnyAsync(x => x.Status == PayrollStatus.Locked
            && x.StartDate <= date.Date && x.EndDate >= date.Date), "WorkDate",
            "Ngày công thuộc kỳ lương đã chốt; cần mở khóa bảng lương trước khi sửa.",
            "Attendance belongs to a locked payroll period; unlock payroll before changing it.");
    }

    public static async Task<string> ValidatePhotoAsync(IFormFile? photo)
    {
        InputRules.Require(photo is { Length: > 0 } && photo.Length <= MaxPhotoBytes, "Photo",
            "Ảnh chấm công phải có dữ liệu và không vượt quá 5 MB.",
            "Attendance photo must be nonempty and no larger than 5 MB.");
        await using var source = photo!.OpenReadStream();
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer);
        InputRules.Require(buffer.Length == photo.Length && buffer.Length >= 12, "Photo",
            "Ảnh chấm công bị thiếu hoặc hỏng.", "Attendance photo is incomplete or corrupt.");
        var bytes = buffer.GetBuffer();
        var jpeg = bytes[0] == 0xFF && bytes[1] == 0xD8
            && bytes[buffer.Length - 2] == 0xFF && bytes[buffer.Length - 1] == 0xD9;
        var png = bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
            && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A
            && bytes[buffer.Length - 8] == 0x49 && bytes[buffer.Length - 7] == 0x45
            && bytes[buffer.Length - 6] == 0x4E && bytes[buffer.Length - 5] == 0x44;
        InputRules.Require(jpeg || png, "Photo", "Ảnh phải là tệp JPEG hoặc PNG hợp lệ.",
            "Photo must be a valid JPEG or PNG file.");
        InputRules.Require(jpeg ? photo.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)
            : photo.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase), "Photo",
            "Loại nội dung ảnh không khớp với tệp.", "Photo content type does not match the file.");
        return jpeg ? ".jpg" : ".png";
    }
}
