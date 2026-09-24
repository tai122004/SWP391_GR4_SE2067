using RWPM.Models.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RWPM.Services.Abstraction
{
    public interface IAttendanceService
    {
        Task<AttendanceRecord?> GetTodayRecordAsync(string username, int? shiftId = null);
        Task<AttendanceRecord> CheckInAsync(string username, double? userLatitude = null, double? userLongitude = null, Microsoft.AspNetCore.Http.IFormFile? photo = null);
        Task<AttendanceRecord> CheckOutAsync(string username, double? userLatitude = null, double? userLongitude = null, Microsoft.AspNetCore.Http.IFormFile? photo = null);
        Task<List<AttendanceRecord>> GetHistoryAsync(string username);
        Task<List<AttendanceRecord>> GetAllHistoryAsync(string? searchQuery = null);
        Task DeleteRecordAsync(int attendanceId);
    }
}
