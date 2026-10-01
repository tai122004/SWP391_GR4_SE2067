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
        private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;

        public AttendanceService(DefaultDatabaseContext context, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task<AttendanceRecord?> GetTodayRecordAsync(string username, int? shiftId = null)
        {
            var query = _context.AttendanceRecord
                .Include(r => r.Shift)
                .Where(r => r.Username == username && r.Date >= DateTime.Today.AddDays(-1)
                    && r.Date <= DateTime.Today && !r.CheckOutTime.HasValue);
            
            if (shiftId.HasValue)
            {
                query = query.Where(r => r.ShiftId == shiftId.Value);
            }

            var records = await query
                .OrderByDescending(r => r.AttendanceId)
                .ToListAsync();
            return records.FirstOrDefault(r => r.Date.Date == DateTime.Today
                || r.Shift?.EndDayOffset == 1);
        }

        public async Task<AttendanceRecord> CheckInAsync(string username, double? userLatitude = null, double? userLongitude = null, Microsoft.AspNetCore.Http.IFormFile? photo = null)
        {
            if (photo == null || photo.Length == 0)
            {
                throw new Exception(SharedResource.ResourceManager.GetString("Attendance_Err_NoPhotoIn"));
            }

            var today = DateTime.Today;

            var employee = await _context.Employee.Include(e => e.Store).FirstOrDefaultAsync(e => e.Username == username);
            if (employee == null)
            {
                throw new Exception(SharedResource.ResourceManager.GetString("Attendance_Err_NoEmployee"));
            }

            var registrations = await _context.Set<ShiftRegistration>()
                .Include(sr => sr.Shift)
                .Include(sr => sr.Store)
                .Where(sr => sr.EmployeeId == employee.EmployeeId && sr.WorkDate >= today.AddDays(-1)
                    && sr.WorkDate <= today.AddDays(1) && sr.Status == RWPM.Common.Enums.RegistrationStatus.Approved)
                .ToListAsync();

            if (!registrations.Any())
            {
                throw new Exception(SharedResource.ResourceManager.GetString("Attendance_Err_NoShift"));
            }

            var nowAt = DateTime.Now;
            var now = nowAt.TimeOfDay;
            ShiftRegistration? matchedRegistration = null;
            bool isValid = false;
            string errorMessage = "Không nằm trong thời gian cho phép chấm công.";

            // Tìm ca phù hợp nhất với giờ hiện tại
            foreach (var reg in registrations)
            {
                var s = reg.Shift;
                int eCheckIn = ShiftDefaults.EarlyCheckInMinutes;
                int lThreshold = ShiftDefaults.LateThresholdMinutes;
                var startAt = reg.WorkDate.Date.Add(s.StartTime);
                var start1Early = startAt.AddMinutes(-eCheckIn);
                var start1Late = startAt.AddMinutes(lThreshold);
                if (nowAt >= start1Early && nowAt <= start1Late)
                {
                    matchedRegistration = reg;
                    isValid = true;
                    break;
                }
            }

            if (matchedRegistration == null)
            {
                matchedRegistration = registrations.OrderBy(r => Math.Abs((nowAt - r.WorkDate.Date.Add(r.Shift.StartTime)).TotalMinutes)).First();
                var shift = matchedRegistration.Shift;
                
                int eCheckIn = ShiftDefaults.EarlyCheckInMinutes;
                int lThreshold = ShiftDefaults.LateThresholdMinutes;
                if (nowAt < matchedRegistration.WorkDate.Date.Add(shift.StartTime).AddMinutes(-eCheckIn))
                {
                    errorMessage = string.Format(SharedResource.ResourceManager.GetString("Attendance_Err_TooEarlyIn"), shift.ShiftName, eCheckIn);
                }
                else
                {
                    errorMessage = string.Format(SharedResource.ResourceManager.GetString("Attendance_Err_TooLateIn"), shift.ShiftName, lThreshold);
                }
            }

            if (!isValid)
            {
                throw new Exception(errorMessage);
            }

            var matchedShift = matchedRegistration.Shift;
            var targetStore = matchedRegistration.Store ?? employee.Store; // Fallback to employee store if null

            var existingRecordForShift = await _context.AttendanceRecord
                .FirstOrDefaultAsync(r => r.Username == username && r.Date == matchedRegistration.WorkDate.Date && r.ShiftId == matchedShift.ShiftId);

            if (existingRecordForShift != null)
            {
                throw new Exception(string.Format(SharedResource.ResourceManager.GetString("Attendance_Err_AlreadyCompleted"), matchedShift.ShiftName));
            }


            // GeoLocation Validation
            double? calculatedDistance = null;
            if (targetStore != null && targetStore.Latitude.HasValue && targetStore.Longitude.HasValue)
            {
                if (!userLatitude.HasValue || !userLongitude.HasValue)
                {
                    throw new Exception(SharedResource.ResourceManager.GetString("Attendance_Err_NoGPS"));
                }

                double distance = GeoLocationHelper.CalculateDistanceMeters(
                    userLatitude.Value, userLongitude.Value, 
                    targetStore.Latitude.Value, targetStore.Longitude.Value);

                // Anti-Fake GPS check (0m or exact database match)
                if (distance < 0.1 || (Math.Abs(userLatitude.Value - targetStore.Latitude.Value) < 1e-6 && Math.Abs(userLongitude.Value - targetStore.Longitude.Value) < 1e-6))
                {
                    throw new Exception(SharedResource.ResourceManager.GetString("Attendance_Err_FakeGPS"));
                }

                int minDistance = targetStore.MinAllowedDistanceMeters;
                if (minDistance > 0 && distance < minDistance)
                {
                    throw new Exception(string.Format(SharedResource.ResourceManager.GetString("Attendance_Err_MinDistance"), targetStore.StoreName, Math.Round(distance), minDistance));
                }

                int allowedRadius = targetStore.AllowedRadiusMeters > 0 ? targetStore.AllowedRadiusMeters : 50;
                if (distance > allowedRadius)
                {
                    throw new Exception(string.Format(SharedResource.ResourceManager.GetString("Attendance_Err_MaxRadius"), targetStore.StoreName, Math.Round(distance), allowedRadius));
                }

                calculatedDistance = distance;
            }

            var record = new AttendanceRecord
            {
                Username = username,
                Date = matchedRegistration.WorkDate.Date,
                CheckInTime = now,
                ShiftId = matchedShift.ShiftId,
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
                throw new Exception(SharedResource.ResourceManager.GetString("Attendance_Err_NoPhotoOut"));
            }

            var record = await GetTodayRecordAsync(username);
            
            if (record == null)
            {
                throw new Exception(SharedResource.ResourceManager.GetString("Attendance_Err_NotCheckedIn"));
            }
            if (record.CheckOutTime.HasValue)
            {
                throw new Exception(SharedResource.ResourceManager.GetString("Attendance_Err_AlreadyCheckedOut"));
            }

            // Time Validation for CheckOut (Early Check-Out)
            var shift = await _context.Shift.FindAsync(record.ShiftId);
            if (shift != null)
            {
                int earlyCheckOutMinutes = shift.EarlyCheckOutMinutes ?? RWPM.Common.Constants.ShiftDefaults.EarlyCheckOutMinutes;
                var minCheckOutTime = record.Date.Date.AddDays(shift.EndDayOffset).Add(shift.EndTime).AddMinutes(-earlyCheckOutMinutes);
                var now = DateTime.Now;

                if (now < minCheckOutTime)
                {
                    throw new Exception(string.Format(SharedResource.ResourceManager.GetString("Attendance_Err_TooEarlyOut"), minCheckOutTime.ToString("dd/MM HH:mm"), earlyCheckOutMinutes, shift.EndTime.ToString(@"hh\:mm")));
                }
            }

            // GeoLocation Validation for CheckOut
            var employee = await _context.Employee.Include(e => e.Store).FirstOrDefaultAsync(e => e.Username == username);
            
            var registration = await _context.Set<ShiftRegistration>()
                .Include(sr => sr.Store)
                .FirstOrDefaultAsync(sr => employee != null && sr.EmployeeId == employee.EmployeeId && sr.WorkDate == record.Date && sr.ShiftId == record.ShiftId && sr.Status == RWPM.Common.Enums.RegistrationStatus.Approved);
            
            var targetStore = registration?.Store ?? employee?.Store;

            if (targetStore != null && targetStore.Latitude.HasValue && targetStore.Longitude.HasValue)
            {
                if (!userLatitude.HasValue || !userLongitude.HasValue)
                {
                    throw new Exception(SharedResource.ResourceManager.GetString("Attendance_Err_NoGPS"));
                }

                double distance = GeoLocationHelper.CalculateDistanceMeters(
                    userLatitude.Value, userLongitude.Value, 
                    targetStore.Latitude.Value, targetStore.Longitude.Value);

                // Anti-Fake GPS check
                if (distance < 0.1 || (Math.Abs(userLatitude.Value - targetStore.Latitude.Value) < 1e-6 && Math.Abs(userLongitude.Value - targetStore.Longitude.Value) < 1e-6))
                {
                    throw new Exception(SharedResource.ResourceManager.GetString("Attendance_Err_FakeGPS"));
                }

                int minDistance = targetStore.MinAllowedDistanceMeters;
                if (minDistance > 0 && distance < minDistance)
                {
                    throw new Exception(string.Format(SharedResource.ResourceManager.GetString("Attendance_Err_MinDistance"), targetStore.StoreName, Math.Round(distance), minDistance));
                }

                int allowedRadius = targetStore.AllowedRadiusMeters > 0 ? targetStore.AllowedRadiusMeters : 50;
                if (distance > allowedRadius)
                {
                    throw new Exception(string.Format(SharedResource.ResourceManager.GetString("Attendance_Err_MaxRadius"), targetStore.StoreName, Math.Round(distance), allowedRadius));
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

        public async Task AdjustAttendanceAsync(int recordId, TimeSpan? newCheckIn, TimeSpan? newCheckOut, string reason, string modifierUsername)
        {
            var record = await _context.AttendanceRecord
                .FirstOrDefaultAsync(r => r.AttendanceId == recordId);

            if (record == null)
            {
                throw new Exception("Bản ghi chấm công không tồn tại!");
            }

            // Ghi lại lịch sử
            var history = new AttendanceAdjustmentHistory
            {
                AttendanceRecordId = recordId,
                OldCheckInTime = record.CheckInTime,
                OldCheckOutTime = record.CheckOutTime,
                NewCheckInTime = newCheckIn,
                NewCheckOutTime = newCheckOut,
                Reason = reason,
                ModifiedBy = modifierUsername,
                ModifiedAt = DateTime.Now
            };

            _context.AttendanceAdjustmentHistory.Add(history);

            // Cập nhật bản ghi gốc
            record.CheckInTime = newCheckIn;
            record.CheckOutTime = newCheckOut;
            record.IsAdjusted = true;

            await _context.SaveChangesAsync();
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
