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
            var errorMessage = SharedResource.ResourceManager.GetString("Attendance_Err_NotInAllowedTime");
            bool isValid = false;
            if (photo == null || photo.Length == 0)
            {
                throw new Exception(SharedResource.ResourceManager.GetString("Attendance_Err_NoPhotoIn"));
            }
            ValidatePhoto(photo);

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
            {
                // Chỉ xét các ca chưa kết thúc và chưa có bản ghi chấm công
                var checkedShifts = await _context.AttendanceRecord
                    .Where(r => r.Username == username && r.Date >= today.AddDays(-1) && r.Date <= today.AddDays(1))
                    .Select(r => new { r.Date, r.ShiftId })
                    .ToListAsync();
                var candidates = registrations
                    .Where(r => ShiftTimeHelper.GetDateTimeRange(r.WorkDate, r.Shift).EndAt >= nowAt)
                    .Where(r => !checkedShifts.Any(c => c.Date.Date == r.WorkDate.Date && c.ShiftId == r.ShiftId))
                    .ToList();
                if (!candidates.Any())
                {
                    throw new Exception(errorMessage);
                }

                matchedRegistration = candidates.OrderBy(r => Math.Abs((nowAt - r.WorkDate.Date.Add(r.Shift.StartTime)).TotalMinutes)).First();
                var shift = matchedRegistration.Shift;

                int eCheckIn = ShiftDefaults.EarlyCheckInMinutes;
                int lThreshold = ShiftDefaults.LateThresholdMinutes;
                if (nowAt > ShiftTimeHelper.GetDateTimeRange(matchedRegistration.WorkDate, shift).EndAt)
                {
                    isValid = false;
                }
                else if (nowAt < matchedRegistration.WorkDate.Date.Add(shift.StartTime).AddMinutes(-eCheckIn))
                {
                    if (!_env.IsDevelopment())
                    {
                        errorMessage = string.Format(SharedResource.ResourceManager.GetString("Attendance_Err_TooEarlyIn"), shift.ShiftName, eCheckIn);
                    }
                    else
                    {
                        isValid = true;
                    }
                }
                else
                {
                    isValid = true;
                }
            }

            if (!isValid)
            {
                throw new Exception(errorMessage);
            }

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
                CheckInPhotoPath = await SavePhotoAsync(photo!, username, "CheckIn")
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
            ValidatePhoto(photo);

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
            record.CheckOutPhotoPath = await SavePhotoAsync(photo!, username, "CheckOut");

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

            await AttendanceAdjustmentService.EnsurePeriodUnlockedAsync(_context, record.Date);

            var effectiveIn = newCheckIn ?? record.CheckInTime;
            var effectiveOut = newCheckOut ?? record.CheckOutTime;

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
                await AttendanceAdjustmentService.EnsurePeriodUnlockedAsync(_context, record.Date);
                _context.AttendanceRecord.Remove(record);
                await _context.SaveChangesAsync();
            }
        }

        private const long MaxPhotoBytes = 5 * 1024 * 1024;

        // Chỉ chấp nhận ảnh JPEG/PNG (kiểm tra content-type và chữ ký file), tối đa 5MB; trả về phần mở rộng cố định
        private static string ValidatePhoto(Microsoft.AspNetCore.Http.IFormFile photo)
        {
            if (photo.Length > MaxPhotoBytes)
            {
                throw new Exception(RWPM.Common.Validation.InputRules.Text("Ảnh chấm công vượt quá dung lượng cho phép (5MB).", "Attendance photo exceeds the 5MB limit."));
            }

            var contentType = (photo.ContentType ?? string.Empty).ToLowerInvariant();
            var header = new byte[8];
            int read;
            using (var stream = photo.OpenReadStream())
            {
                read = stream.Read(header, 0, header.Length);
            }

            bool isJpeg = read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
            bool isPng = read >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
                && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;

            if ((contentType == "image/jpeg" || contentType == "image/jpg" || contentType == "image/pjpeg") && isJpeg) return ".jpg";
            if (contentType == "image/png" && isPng) return ".png";

            throw new Exception(RWPM.Common.Validation.InputRules.Text("Ảnh chấm công phải là file JPEG hoặc PNG.", "Attendance photo must be a JPEG or PNG image."));
        }

        private async Task<string> SavePhotoAsync(Microsoft.AspNetCore.Http.IFormFile photo, string username, string type)
        {
            var extension = ValidatePhoto(photo);

            // Lưu ngoài wwwroot; chỉ xem qua Attendance/Photo có kiểm tra quyền
            var uniqueFileName = $"{type}_{Guid.NewGuid():N}{extension}";
            return await RWPM.Infrastructure.Storage.AttendancePhotoStore.SaveAsync(_env, photo, uniqueFileName);
        }
    }
}
