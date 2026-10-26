using RWPM.Models.Entities;

namespace RWPM.Services.Abstraction
{
    public interface IAttendanceService
    {
        Task<AttendanceRecord?> GetTodayRecordAsync(string username, int? shiftId = null);
        Task<(AttendanceRecord Record, string? WarningMessage)> CheckInAsync(string username, double? userLatitude = null, double? userLongitude = null, Microsoft.AspNetCore.Http.IFormFile? photo = null);
        Task<(AttendanceRecord Record, string? WarningMessage)> CheckOutAsync(string username, double? userLatitude = null, double? userLongitude = null, Microsoft.AspNetCore.Http.IFormFile? photo = null);
        Task<List<AttendanceRecord>> GetHistoryAsync(string username);
        Task AdjustAttendanceAsync(int recordId, TimeSpan? newCheckIn, TimeSpan? newCheckOut, string reason, string modifierUsername);
        Task DeleteRecordAsync(int attendanceId);
    }
}
