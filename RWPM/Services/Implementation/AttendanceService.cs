using Microsoft.EntityFrameworkCore;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;
using RWPM.Services.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using RWPM.Resources.Shared;

namespace RWPM.Services.Implementation
{
    public class AttendanceService : IAttendanceService
    {
        private readonly DefaultDatabaseContext _context;

        public AttendanceService(DefaultDatabaseContext context)
        {
            _context = context;
        }

        public async Task<AttendanceRecord?> GetTodayRecordAsync(string username)
        {
            // Để hỗ trợ nhiều ca (multiple shifts) trong 1 ngày, 
            // hàm này sẽ trả về ca làm việc đang "MỞ" (chưa check-out)
            return await _context.AttendanceRecord
                .Where(r => r.Username == username && r.Date == DateTime.Today.Date && !r.CheckOutTime.HasValue)
                .OrderByDescending(r => r.AttendanceId)
                .FirstOrDefaultAsync();
        }

        public async Task<AttendanceRecord> CheckInAsync(string username)
        {
            var today = DateTime.Today;
            var record = await GetTodayRecordAsync(username);
            
            if (record != null)
            {
                throw new Exception(SharedResource.ResourceManager.GetString("Attendance_AlreadyCheckedIn"));
            }

            record = new AttendanceRecord
            {
                Username = username,
                Date = today,
                CheckInTime = DateTime.Now.TimeOfDay
            };

            _context.AttendanceRecord.Add(record);
            await _context.SaveChangesAsync();

            return record;
        }

        public async Task<AttendanceRecord> CheckOutAsync(string username)
        {
            var record = await GetTodayRecordAsync(username);
            
            if (record == null)
            {
                throw new Exception(SharedResource.ResourceManager.GetString("Attendance_NotCheckedIn"));
            }
            if (record.CheckOutTime.HasValue)
            {
                throw new Exception(SharedResource.ResourceManager.GetString("Attendance_AlreadyCheckedOut"));
            }

            record.CheckOutTime = DateTime.Now.TimeOfDay;
            await _context.SaveChangesAsync();

            return record;
        }

        public async Task<List<AttendanceRecord>> GetHistoryAsync(string username)
        {
            return await _context.AttendanceRecord
                .Where(x => x.Username == username)
                .OrderByDescending(x => x.Date)
                .ToListAsync();
        }

        public async Task<List<AttendanceRecord>> GetAllHistoryAsync(string? searchQuery = null)
        {
            var query = _context.AttendanceRecord.AsQueryable();
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                query = query.Where(x => x.Username.Contains(searchQuery));
            }
            return await query.OrderByDescending(x => x.Date).ToListAsync();
        }
    }
}
