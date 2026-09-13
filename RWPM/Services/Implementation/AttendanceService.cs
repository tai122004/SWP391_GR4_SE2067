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

        public async Task<AttendanceRecord?> GetTodayRecordAsync(string username, int? shiftId = null)
        {
            var query = _context.AttendanceRecord
                .Where(r => r.Username == username && r.Date == DateTime.Today.Date && !r.CheckOutTime.HasValue);
            
            if (shiftId.HasValue)
            {
                query = query.Where(r => r.ShiftId == shiftId.Value);
            }

            return await query
                .OrderByDescending(r => r.AttendanceId)
                .FirstOrDefaultAsync();
        }

        public async Task<AttendanceRecord> CheckInAsync(string username, int shiftId)
        {
            var today = DateTime.Today;
            var record = await GetTodayRecordAsync(username, shiftId);
            
            if (record != null)
            {
                throw new Exception(SharedResource.ResourceManager.GetString("Attendance_AlreadyCheckedIn"));
            }

            var shift = await _context.Set<RWPM.Models.Entities.Shift>().FindAsync(shiftId);
            if (shift == null || !shift.IsActive)
            {
                throw new Exception("Ca làm việc không tồn tại hoặc đã bị vô hiệu hóa.");
            }

            var now = DateTime.Now.TimeOfDay;
            bool isValid = false;

            if (now <= shift.StartTime.Add(TimeSpan.FromMinutes(30)))
            {
                isValid = true;
            }
            else if (shift.StartTime2.HasValue && now <= shift.StartTime2.Value.Add(TimeSpan.FromMinutes(30)) && now >= shift.StartTime2.Value.Subtract(TimeSpan.FromHours(2)))
            {
                isValid = true;
            }

            if (!isValid)
            {
                throw new Exception(SharedResource.ResourceManager.GetString("Attendance_TooLate"));
            }

            record = new AttendanceRecord
            {
                Username = username,
                Date = today,
                CheckInTime = now,
                ShiftId = shiftId
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
                .Include(x => x.Shift)
                .Where(x => x.Username == username)
                .OrderByDescending(x => x.Date)
                .ToListAsync();
        }

        public async Task<List<AttendanceRecord>> GetAllHistoryAsync(string? searchQuery = null)
        {
            var query = _context.AttendanceRecord.Include(x => x.Shift).AsQueryable();
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                query = query.Where(x => x.Username.Contains(searchQuery));
            }
            return await query.OrderByDescending(x => x.Date).ToListAsync();
        }

        public async Task DeleteRecordAsync(int attendanceId)
        {
            var record = await _context.AttendanceRecord.FindAsync(attendanceId);
            if (record != null)
            {
                _context.AttendanceRecord.Remove(record);
                await _context.SaveChangesAsync();
            }
        }
    }
}
