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
using Microsoft.Extensions.Hosting;
using RWPM.Common.Validation;

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

        public async Task<(AttendanceRecord Record, string? WarningMessage)> CheckInAsync(string username, double? userLatitude = null, double? userLongitude = null, Microsoft.AspNetCore.Http.IFormFile? photo = null)
        {
            if (photo == null || photo.Length == 0)
            {
                throw new Exception(SharedResource.ResourceManager.GetString("Attendance_Err_NoPhotoIn"));
            }
            var photoExtension = await AttendanceInputRules.ValidatePhotoAsync(photo);
            AttendanceInputRules.ValidateCoordinates(userLatitude, userLongitude);

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
            var matchedRegistration = registrations
                .Where(r => r.Shift != null)
                .Where(r => {
                    var range = ShiftTimeHelper.GetDateTimeRange(r.WorkDate.Date, r.Shift);
                    return nowAt >= range.StartAt.AddMinutes(-ShiftDefaults.EarlyCheckInMinutes)
                        && nowAt < range.EndAt;
                })
                .OrderBy(r => Math.Abs((nowAt - r.WorkDate.Date.Add(r.Shift.StartTime)).TotalMinutes))
                .FirstOrDefault();
            if (matchedRegistration == null)
                throw new Exception(SharedResource.Attendance_Err_NotInAllowedTime);

            var matchedShift = matchedRegistration.Shift;
            var targetStore = matchedRegistration.Store ?? employee.Store; // Fallback to employee store if null
            InputRules.Require(targetStore != null && targetStore.Latitude.HasValue && targetStore.Longitude.HasValue,
                "Latitude", "Chi nhánh chưa cấu hình GPS để chấm công.", "Store GPS is not configured for attendance.");
            if (targetStore != null) AttendanceInputRules.ValidateCoordinates(targetStore.Latitude, targetStore.Longitude);

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

                // Anti-Fake GPS: Chỉ kiểm tra chặt chẽ ngoài môi trường Development
                bool isExactMatch = Math.Abs(userLatitude.Value - targetStore.Latitude.Value) < 0.000001 
                                 && Math.Abs(userLongitude.Value - targetStore.Longitude.Value) < 0.000001;
                if (isExactMatch && !_env.IsDevelopment())
                {
                    throw new Exception(SharedResource.ResourceManager.GetString("Attendance_Err_FakeGPS"));
                }

                // Reject invalid store configuration instead of silently skipping the minimum distance.
                int minDistance = targetStore.MinAllowedDistanceMeters;
                int allowedRadius = targetStore.AllowedRadiusMeters;
                AttendanceInputRules.ValidateRadius(minDistance, allowedRadius);
                if (minDistance > 0 && distance < minDistance)
                {
                    throw new Exception(string.Format(SharedResource.ResourceManager.GetString("Attendance_Err_MinDistance"), targetStore.StoreName, Math.Round(distance), minDistance));
                }

                // Kiểm tra bán kính tối đa — phải nằm trong phạm vi cho phép
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
                CheckInPhotoPath = await SavePhotoAsync(photo!, username, "CheckIn", photoExtension)
            };

            _context.AttendanceRecord.Add(record);
            await _context.SaveChangesAsync();

            string? warningMsg = null;
            var shiftRange = RWPM.Common.Helper.ShiftTimeHelper.GetDateTimeRange(record.Date, matchedShift);
            var checkInActual = new[] { -1, 0, 1 }.Select(day => record.Date.AddDays(day).Add(record.CheckInTime.Value))
                .OrderBy(time => Math.Abs((time - shiftRange.StartAt).TotalMinutes)).First();
                
            int lateMins = (int)(checkInActual - shiftRange.StartAt).TotalMinutes;
            int gracePeriod = matchedShift.GracePeriodMinutes ?? RWPM.Common.Constants.ShiftDefaults.GracePeriodMinutes;
            if (lateMins > gracePeriod)
            {
                warningMsg = string.Format(SharedResource.Attendance_Warn_LateCheckIn, lateMins, gracePeriod);
            }

            return (record, warningMsg);
        }

        public async Task<(AttendanceRecord Record, string? WarningMessage)> CheckOutAsync(string username, double? userLatitude = null, double? userLongitude = null, Microsoft.AspNetCore.Http.IFormFile? photo = null)
        {
            if (photo == null || photo.Length == 0)
            {
                throw new Exception(SharedResource.ResourceManager.GetString("Attendance_Err_NoPhotoOut"));
            }
            var photoExtension = await AttendanceInputRules.ValidatePhotoAsync(photo);
            AttendanceInputRules.ValidateCoordinates(userLatitude, userLongitude);

            var record = await GetTodayRecordAsync(username);
            
            if (record == null)
            {
                throw new Exception(SharedResource.ResourceManager.GetString("Attendance_Err_NotCheckedIn"));
            }
            if (record.CheckOutTime.HasValue)
            {
                throw new Exception(SharedResource.ResourceManager.GetString("Attendance_Err_AlreadyCheckedOut"));
            }
            if (!record.CheckInTime.HasValue)
                throw new Exception(SharedResource.Attendance_Err_NotCheckedIn);
            (DateTime StartAt, DateTime EndAt)? checkInRange = record.Shift == null
                ? null : ShiftTimeHelper.GetDateTimeRange(record.Date, record.Shift);
            var actualCheckIn = new[] { -1, 0, 1 }
                .Select(day => record.Date.AddDays(day).Add(record.CheckInTime.Value))
                .OrderBy(time => Math.Abs((time - (checkInRange?.StartAt ?? record.Date)).TotalMinutes))
                .First();
            InputRules.Require(DateTime.Now > actualCheckIn, "CheckOutTime",
                "Giờ ra phải sau giờ vào.", "Check-out must follow check-in.");

            // Time Validation for CheckOut (Early Check-Out)
            // Người dùng có thể check out bất cứ lúc nào. Việc tính lương sẽ phạt nếu về quá sớm.
            var shift = await _context.Shift.FindAsync(record.ShiftId);

            // GeoLocation Validation for CheckOut
            var employee = await _context.Employee.Include(e => e.Store).FirstOrDefaultAsync(e => e.Username == username);
            
            var registration = await _context.Set<ShiftRegistration>()
                .Include(sr => sr.Store)
                .FirstOrDefaultAsync(sr => employee != null && sr.EmployeeId == employee.EmployeeId && sr.WorkDate == record.Date && sr.ShiftId == record.ShiftId && sr.Status == RWPM.Common.Enums.RegistrationStatus.Approved);
            
            var targetStore = registration?.Store ?? employee?.Store;
            InputRules.Require(targetStore != null && targetStore.Latitude.HasValue && targetStore.Longitude.HasValue,
                "Latitude", "Chi nhánh chưa cấu hình GPS để chấm công.", "Store GPS is not configured for attendance.");
            if (targetStore != null) AttendanceInputRules.ValidateCoordinates(targetStore.Latitude, targetStore.Longitude);

            if (targetStore != null && targetStore.Latitude.HasValue && targetStore.Longitude.HasValue)
            {
                if (!userLatitude.HasValue || !userLongitude.HasValue)
                {
                    throw new Exception(SharedResource.ResourceManager.GetString("Attendance_Err_NoGPS"));
                }

                double distance = GeoLocationHelper.CalculateDistanceMeters(
                    userLatitude.Value, userLongitude.Value, 
                    targetStore.Latitude.Value, targetStore.Longitude.Value);

                // Anti-Fake GPS: Chỉ kiểm tra chặt chẽ ngoài môi trường Development
                bool isExactMatch = Math.Abs(userLatitude.Value - targetStore.Latitude.Value) < 0.000001 
                                 && Math.Abs(userLongitude.Value - targetStore.Longitude.Value) < 0.000001;
                if (isExactMatch && !_env.IsDevelopment())
                {
                    throw new Exception(SharedResource.ResourceManager.GetString("Attendance_Err_FakeGPS"));
                }

                // Reject invalid store configuration instead of silently skipping the minimum distance.
                int minDistance = targetStore.MinAllowedDistanceMeters;
                int allowedRadius = targetStore.AllowedRadiusMeters;
                AttendanceInputRules.ValidateRadius(minDistance, allowedRadius);
                if (minDistance > 0 && distance < minDistance)
                {
                    throw new Exception(string.Format(SharedResource.ResourceManager.GetString("Attendance_Err_MinDistance"), targetStore.StoreName, Math.Round(distance), minDistance));
                }

                // Kiểm tra bán kính tối đa — phải nằm trong phạm vi cho phép
                if (distance > allowedRadius)
                {
                    throw new Exception(string.Format(SharedResource.ResourceManager.GetString("Attendance_Err_MaxRadius"), targetStore.StoreName, Math.Round(distance), allowedRadius));
                }
            }

            record.CheckOutTime = DateTime.Now.TimeOfDay;
            record.CheckOutLatitude = userLatitude;
            record.CheckOutLongitude = userLongitude;
            record.CheckOutPhotoPath = await SavePhotoAsync(photo!, username, "CheckOut", photoExtension);

            await _context.SaveChangesAsync();

            string? warningMsg = null;
            if (shift != null)
            {
                var shiftRange = RWPM.Common.Helper.ShiftTimeHelper.GetDateTimeRange(record.Date, shift);
                var checkOutActual = new[] { -1, 0, 1, 2 }.Select(day => record.Date.AddDays(day).Add(record.CheckOutTime.Value))
                    .Where(time => time >= record.Date.Add(record.CheckInTime.Value)).OrderBy(time => time).First();
                    
                int earlyMins = (int)(shiftRange.EndAt - checkOutActual).TotalMinutes;
                int earlyOutLimit = shift.EarlyCheckOutMinutes ?? RWPM.Common.Constants.ShiftDefaults.EarlyCheckOutMinutes;
                if (earlyMins > earlyOutLimit)
                {
                    warningMsg = string.Format(SharedResource.Attendance_Warn_EarlyCheckOut, earlyMins, earlyOutLimit);
                }
            }

            return (record, warningMsg);
        }

        public async Task<List<AttendanceRecord>> GetHistoryAsync(string username)
        {
            return await _context.AttendanceRecord
                .Include(x => x.Shift)
                .Include(x => x.Account)
                .Include(x => x.AdjustmentHistories)
                .Where(x => x.Username == username)
                .OrderByDescending(x => x.Date)
                .ThenByDescending(x => x.CheckInTime)
                .ThenByDescending(x => x.AttendanceId)
                .ToListAsync();
        }

        public async Task<List<AttendanceRecord>> GetAllHistoryAsync(string? searchQuery = null)
        {
            var query = _context.AttendanceRecord
                .Include(x => x.Shift)
                .Include(x => x.AdjustmentHistories)
                .AsQueryable();
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                query = query.Where(x => x.Username.Contains(searchQuery));
            }
            return await query
                .OrderByDescending(x => x.Date)
                .ThenByDescending(x => x.CheckInTime)
                .ThenByDescending(x => x.AttendanceId)
                .ToListAsync();
        }

        public async Task AdjustAttendanceAsync(int recordId, TimeSpan? newCheckIn, TimeSpan? newCheckOut, string reason, string modifierUsername)
        {
            var record = await _context.AttendanceRecord.Include(r => r.Shift)
                .FirstOrDefaultAsync(r => r.AttendanceId == recordId);

            if (record == null)
            {
                throw new Exception(SharedResource.Attendance_Err_RecordNotFound);
            }

            await AttendanceInputRules.EnsureUnlockedAsync(_context, record.Date);
            InputRules.Require(!string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= 500, "Reason",
                "Lý do điều chỉnh phải có từ 1 đến 500 ký tự.", "Adjustment reason must contain 1–500 characters.");
            InputRules.Require(newCheckIn.HasValue || newCheckOut.HasValue, "Time",
                "Cần nhập ít nhất một giờ muốn điều chỉnh.", "Enter at least one time to adjust.");
            var effectiveIn = newCheckIn ?? record.CheckInTime;
            var effectiveOut = newCheckOut ?? record.CheckOutTime;
            AttendanceInputRules.ValidateTimeOrder(record.Date, effectiveIn, effectiveOut, record.Shift?.EndDayOffset ?? 0);
            InputRules.Require(effectiveIn != record.CheckInTime || effectiveOut != record.CheckOutTime, "Time",
                "Giờ điều chỉnh phải khác giờ hiện tại.", "Adjusted time must differ from the current time.");

            // Ghi lại lịch sử
            var history = new AttendanceAdjustmentHistory
            {
                AttendanceRecordId = recordId,
                OldCheckInTime = record.CheckInTime,
                OldCheckOutTime = record.CheckOutTime,
                NewCheckInTime = effectiveIn,
                NewCheckOutTime = effectiveOut,
                Reason = reason.Trim(),
                ModifiedBy = modifierUsername,
                ModifiedAt = DateTime.Now
            };

            _context.AttendanceAdjustmentHistory.Add(history);

            // Cập nhật bản ghi gốc
            record.CheckInTime = effectiveIn;
            record.CheckOutTime = effectiveOut;
            record.IsAdjusted = true;

            await _context.SaveChangesAsync();
        }

        public async Task DeleteRecordAsync(int attendanceId)
        {
            var record = await _context.AttendanceRecord.FindAsync(attendanceId);
            if (record != null)
            {
                await AttendanceInputRules.EnsureUnlockedAsync(_context, record.Date);
                _context.AttendanceRecord.Remove(record);
                await _context.SaveChangesAsync();
            }
        }

        private async Task<string> SavePhotoAsync(Microsoft.AspNetCore.Http.IFormFile photo, string username, string type, string extension)
        {
            var uploadsFolder = System.IO.Path.Combine(_env.WebRootPath, "images", "attendance");
            if (!System.IO.Directory.Exists(uploadsFolder))
            {
                System.IO.Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = $"{username}_{DateTime.Now:yyyyMMdd_HHmmss}_{type}_{Guid.NewGuid().ToString().Substring(0, 4)}{extension}";
            var filePath = System.IO.Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
            {
                await photo.CopyToAsync(fileStream);
            }

            return $"/images/attendance/{uniqueFileName}";
        }
    }
}
