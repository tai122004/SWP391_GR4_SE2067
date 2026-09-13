using RWPM.Models.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RWPM.Services.Abstraction
{
    public interface IAttendanceService
    {
        Task<AttendanceRecord?> GetTodayRecordAsync(string username);
        Task<AttendanceRecord> CheckInAsync(string username);
        Task<AttendanceRecord> CheckOutAsync(string username);
        Task<List<AttendanceRecord>> GetHistoryAsync(string username);
        Task<List<AttendanceRecord>> GetAllHistoryAsync(string? searchQuery = null);
    }
}
