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

namespace RWPM.Services.Implementation
{
    public class AttendanceService : IAttendanceService
    {
        private readonly DefaultDatabaseContext _context;
        private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;

        public AttendanceService(DefaultDatabaseContext context, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
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

        public async Task<AttendanceRecord> CheckInAsync(string username, double? userLatitude = null, double? userLongitude = null, Microsoft.AspNetCore.Http.IFormFile? photo = null)
        {
            if (photo == null || photo.Length == 0)
            {
                throw new Exception("Bắt buộc phải chụp ảnh khi chấm công vào làm!");
            }

            var today = DateTime.Today;

            var employee = await _context.Employee.Include(e => e.Store).FirstOrDefaultAsync(e => e.Username == username);
            if (employee == null)
            {
                throw new Exception("Tài khoản của bạn chưa được liên kết với hồ sơ nhân viên.");
            }

            // Lấy danh sách các ca đã đăng ký và được duyệt trong ngày
            var registeredShifts = await _context.Set<ShiftRegistration>()
                .Include(sr => sr.Shift)
                .Where(sr => sr.EmployeeId == employee.EmployeeId && sr.WorkDate == today && sr.Status == RWPM.Common.Enums.RegistrationStatus.Approved && sr.Shift.IsActive)
                .Select(sr => sr.Shift)
                .ToListAsync();

            if (!registeredShifts.Any())
            {
                throw new Exception("Bạn không có lịch làm việc (đã được duyệt) trong hôm nay!");
            }

            var now = DateTime.Now.TimeOfDay;
            RWPM.Models.Entities.Shift shift = null;
            bool isValid = false;
            string errorMessage = "Không nằm trong thời gian cho phép chấm công.";

            // Tìm ca phù hợp nhất với giờ hiện tại
            foreach (var s in registeredShifts)
            {
                int eCheckIn = s.EarlyCheckInMinutes ?? RWPM.Common.Constants.ShiftDefaults.EarlyCheckInMinutes;
                int lThreshold = s.LateThresholdMinutes ?? RWPM.Common.Constants.ShiftDefaults.LateThresholdMinutes;

                var start1Early = s.StartTime.Subtract(TimeSpan.FromMinutes(eCheckIn));
                var start1Late = s.StartTime.Add(TimeSpan.FromMinutes(lThreshold));

                if (now >= start1Early && now <= start1Late)
                {
                    shift = s;
                    isValid = true;
                    break;
                }

                if (s.StartTime2.HasValue)
                {
                    var start2Early = s.StartTime2.Value.Subtract(TimeSpan.FromMinutes(eCheckIn));
                    var start2Late = s.StartTime2.Value.Add(TimeSpan.FromMinutes(lThreshold));
                    if (now >= start2Early && now <= start2Late)
                    {
                        shift = s;
                        isValid = true;
                        break;
                    }
                }
            }

            // Nếu không ca nào đang trong khung giờ hợp lệ, chọn ca gần nhất để báo lỗi chính xác
            if (shift == null)
            {
                shift = registeredShifts.OrderBy(s => Math.Abs((now - s.StartTime).TotalMinutes)).First();
                
                int eCheckIn = shift.EarlyCheckInMinutes ?? RWPM.Common.Constants.ShiftDefaults.EarlyCheckInMinutes;
                int lThreshold = shift.LateThresholdMinutes ?? RWPM.Common.Constants.ShiftDefaults.LateThresholdMinutes;
                
                if (now < shift.StartTime.Subtract(TimeSpan.FromMinutes(eCheckIn)))
                {
                    errorMessage = $"Chưa đến giờ chấm công ca {shift.ShiftName}! Bạn chỉ được phép chấm công trước giờ làm {eCheckIn} phút.";
                }
                else
                {
                    errorMessage = $"Bạn đã đến quá muộn cho ca {shift.ShiftName}! Hệ thống chỉ cho phép đi muộn tối đa {lThreshold} phút.";
                }
            }

            if (!isValid)
            {
                throw new Exception(errorMessage);
            }

            var record = await GetTodayRecordAsync(username, shift.ShiftId);
            if (record != null)
            {
                throw new Exception(SharedResource.ResourceManager.GetString("Attendance_AlreadyCheckedIn"));
            }

            // GeoLocation Validation
            double? calculatedDistance = null;
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

                int allowedRadius = employee.Store.AllowedRadiusMeters > 0 ? employee.Store.AllowedRadiusMeters : 50;
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
                ShiftId = shift.ShiftId,
                CheckInLatitude = userLatitude,
                CheckInLongitude = userLongitude,
                DistanceMeters = calculatedDistance,
                CheckInPhotoPath = await SavePhotoAsync(photo, username, "CheckIn")
            };

            _context.AttendanceRecord.Add(record);
            await _context.SaveChangesAsync();

            return record;
        }

        public async Task<AttendanceRecord> CheckOutAsync(string username, double? userLatitude = null, double? userLongitude = null, Microsoft.AspNetCore.Http.IFormFile? photo = null)
        {
            if (photo == null || photo.Length == 0)
            {
                throw new Exception("Bắt buộc phải chụp ảnh khi chấm công tan làm!");
            }

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

                int allowedRadius = employee.Store.AllowedRadiusMeters > 0 ? employee.Store.AllowedRadiusMeters : 50;
                if (distance > allowedRadius)
                {
                    throw new Exception($"Vị trí không hợp lệ! Bạn đang cách chi nhánh {employee.Store.StoreName} khoảng {Math.Round(distance)}m (Vượt quá bán kính cho phép {allowedRadius}m).");
                }
            }

            record.CheckOutTime = DateTime.Now.TimeOfDay;
            record.CheckOutLatitude = userLatitude;
            record.CheckOutLongitude = userLongitude;
            record.CheckOutPhotoPath = await SavePhotoAsync(photo, username, "CheckOut");

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

        private async Task<string> SavePhotoAsync(Microsoft.AspNetCore.Http.IFormFile photo, string username, string type)
        {
            var uploadsFolder = System.IO.Path.Combine(_env.WebRootPath, "images", "attendance");
            if (!System.IO.Directory.Exists(uploadsFolder))
            {
                System.IO.Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = $"{username}_{DateTime.Now:yyyyMMdd_HHmmss}_{type}_{Guid.NewGuid().ToString().Substring(0, 4)}{System.IO.Path.GetExtension(photo.FileName)}";
            var filePath = System.IO.Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
            {
                await photo.CopyToAsync(fileStream);
            }

            return $"/images/attendance/{uniqueFileName}";
        }
    }
}
