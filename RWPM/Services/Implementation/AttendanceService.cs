using RWPM.Common.Helper;
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
using RWPM.Common.Constants;

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

        public async Task<AttendanceRecord> CheckInAsync(string username, int shiftId, double? userLatitude = null, double? userLongitude = null)
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

            var lateThreshold = shift.LateThresholdMinutes ?? ShiftDefaults.LateThresholdMinutes;
            if (now <= shift.StartTime.Add(TimeSpan.FromMinutes(lateThreshold)))
            {
                isValid = true;
            }

            if (!isValid)
            {
                throw new Exception(SharedResource.ResourceManager.GetString("Attendance_TooLate"));
            }

            // GeoLocation Validation
            double? calculatedDistance = null;
            var employee = await _context.Employee.Include(e => e.Store).FirstOrDefaultAsync(e => e.Username == username);
            if (employee?.Store != null && employee.Store.Latitude.HasValue && employee.Store.Longitude.HasValue)
            {
                if (!userLatitude.HasValue || !userLongitude.HasValue)
                {
                    throw new Exception("Không thể xác định vị trí của bạn. Vui lòng bật định vị GPS trên thiết bị để chấm công.");
                }

                double distance = GeoLocationHelper.CalculateDistanceMeters(
                    userLatitude.Value, userLongitude.Value, 
                    employee.Store.Latitude.Value, employee.Store.Longitude.Value);

                // Anti-Fake GPS check (0m or exact database match)
                if (distance < 0.1 || (Math.Abs(userLatitude.Value - employee.Store.Latitude.Value) < 1e-6 && Math.Abs(userLongitude.Value - employee.Store.Longitude.Value) < 1e-6))
                {
                    throw new Exception("Phát hiện vị trí GPS bất thường (0m / Trùng khớp vị trí tĩnh). Vui lòng di chuyển hoặc tắt phần mềm giả lập GPS.");
                }

                int minDistance = employee.Store.MinAllowedDistanceMeters;
                if (minDistance > 0 && distance < minDistance)
                {
                    throw new Exception($"Vị trí không hợp lệ! Bạn đang cách chi nhánh {employee.Store.StoreName} khoảng {Math.Round(distance)}m (Nhỏ hơn khoảng cách tối thiểu cho phép {minDistance}m).");
                }

                int allowedRadius = employee.Store.AllowedRadiusMeters > 0 ? employee.Store.AllowedRadiusMeters : 100;
                if (distance > allowedRadius)
                {
                    throw new Exception($"Vị trí không hợp lệ! Bạn đang cách chi nhánh {employee.Store.StoreName} khoảng {Math.Round(distance)}m (Vượt quá bán kính cho phép {allowedRadius}m).");
                }

                calculatedDistance = distance;
            }

            record = new AttendanceRecord
            {
                Username = username,
                Date = today,
                CheckInTime = now,
                ShiftId = shiftId,
                CheckInLatitude = userLatitude,
                CheckInLongitude = userLongitude,
                DistanceMeters = calculatedDistance
            };

            _context.AttendanceRecord.Add(record);
            await _context.SaveChangesAsync();

            return record;
        }

        public async Task<AttendanceRecord> CheckOutAsync(string username, double? userLatitude = null, double? userLongitude = null)
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

            // GeoLocation Validation for CheckOut
            var employee = await _context.Employee.Include(e => e.Store).FirstOrDefaultAsync(e => e.Username == username);
            if (employee?.Store != null && employee.Store.Latitude.HasValue && employee.Store.Longitude.HasValue)
            {
                if (!userLatitude.HasValue || !userLongitude.HasValue)
                {
                    throw new Exception("Không thể xác định vị trí của bạn. Vui lòng bật định vị GPS trên thiết bị để kết thúc ca làm việc.");
                }

                double distance = GeoLocationHelper.CalculateDistanceMeters(
                    userLatitude.Value, userLongitude.Value, 
                    employee.Store.Latitude.Value, employee.Store.Longitude.Value);

                // Anti-Fake GPS check
                if (distance < 0.1 || (Math.Abs(userLatitude.Value - employee.Store.Latitude.Value) < 1e-6 && Math.Abs(userLongitude.Value - employee.Store.Longitude.Value) < 1e-6))
                {
                    throw new Exception("Phát hiện vị trí GPS bất thường (0m / Trùng khớp vị trí tĩnh). Vui lòng di chuyển hoặc tắt phần mềm giả lập GPS.");
                }

                int minDistance = employee.Store.MinAllowedDistanceMeters;
                if (minDistance > 0 && distance < minDistance)
                {
                    throw new Exception($"Vị trí không hợp lệ! Bạn đang cách chi nhánh {employee.Store.StoreName} khoảng {Math.Round(distance)}m (Nhỏ hơn khoảng cách tối thiểu cho phép {minDistance}m).");
                }

                int allowedRadius = employee.Store.AllowedRadiusMeters > 0 ? employee.Store.AllowedRadiusMeters : 100;
                if (distance > allowedRadius)
                {
                    throw new Exception($"Vị trí không hợp lệ! Bạn đang cách chi nhánh {employee.Store.StoreName} khoảng {Math.Round(distance)}m (Vượt quá bán kính cho phép {allowedRadius}m).");
                }
            }

            record.CheckOutTime = DateTime.Now.TimeOfDay;
            record.CheckOutLatitude = userLatitude;
            record.CheckOutLongitude = userLongitude;

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
