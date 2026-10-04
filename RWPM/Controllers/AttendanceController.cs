using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using RWPM.Models.Entities;
using RWPM.Resources.Shared;
using RWPM.Services.Abstraction;
using System.Collections.Generic;
using System.Security.Claims;
using RWPM.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace RWPM.Controllers
{
    [Authorize]
    public class AttendanceController : Controller
    {
        private readonly IAttendanceService _attendanceService;
        private readonly IAccService _accService;
        private readonly DefaultDatabaseContext _context;

        public AttendanceController(IAttendanceService attendanceService, IAccService accService, DefaultDatabaseContext context)
        {
            _attendanceService = attendanceService;
            _accService = accService;
            _context = context;
        }

        public async Task<IActionResult> Index(string? searchQuery = null)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Login", "Auth");

            ViewBag.TodayRecord = await _attendanceService.GetTodayRecordAsync(username);
            bool isAdmin = User.IsInRole("Admin") || User.IsInRole("HR") || User.IsInRole("SuperAdmin");
            ViewBag.IsAdmin = isAdmin;

            var today = DateTime.Today;
            var employee = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
            if (employee != null)
            {
                var registeredShiftIds = await _context.Set<ShiftRegistration>()
                    .Where(sr => sr.EmployeeId == employee.EmployeeId && sr.WorkDate == today && sr.Status == RWPM.Common.Enums.RegistrationStatus.Approved)
                    .Select(sr => sr.ShiftId)
                    .ToListAsync();

                var completedShiftIds = await _context.AttendanceRecord
                    .Where(r => r.Username == username && r.Date == today && r.CheckOutTime.HasValue)
                    .Select(r => r.ShiftId)
                    .ToListAsync();

                var allTodayShifts = await _context.Set<RWPM.Models.Entities.Shift>()
                    .Where(s => s.IsActive && registeredShiftIds.Contains(s.ShiftId) && !completedShiftIds.Contains(s.ShiftId))
                    .ToListAsync();
                    
                ViewBag.ActiveShifts = allTodayShifts;
            }
            else
            {
                ViewBag.ActiveShifts = new List<RWPM.Models.Entities.Shift>();
            }
            
            ViewBag.SearchQuery = searchQuery;

            List<AttendanceRecord> history;
            if (isAdmin)
            {
                ViewBag.Employees = await _accService.GetAllAsync();
                history = await _attendanceService.GetAllHistoryAsync(searchQuery);
            }
            else
            {
                history = await _attendanceService.GetHistoryAsync(username);
            }

            return View(history);
        }

        [HttpPost]
        public async Task<IActionResult> CheckIn(string? userLatitude = null, string? userLongitude = null, Microsoft.AspNetCore.Http.IFormFile? photo = null)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Login", "Auth");

            double? parsedLat = null;
            double? parsedLng = null;
            if (double.TryParse(userLatitude?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lat)) parsedLat = lat;
            if (double.TryParse(userLongitude?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lng)) parsedLng = lng;

            try
            {
                var result = await _attendanceService.CheckInAsync(username, parsedLat, parsedLng, photo);
                if (!string.IsNullOrEmpty(result.WarningMessage))
                {
                    TempData["ErrorMessage"] = result.WarningMessage; // Hiển thị màu đỏ để chú ý
                }
                else
                {
                    TempData["SuccessMessage"] = SharedResource.ResourceManager.GetString("Attendance_CheckInSuccess");
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> CheckOut(string? userLatitude = null, string? userLongitude = null, Microsoft.AspNetCore.Http.IFormFile? photo = null)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Login", "Auth");

            double? parsedLat = null;
            double? parsedLng = null;
            if (double.TryParse(userLatitude?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lat)) parsedLat = lat;
            if (double.TryParse(userLongitude?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lng)) parsedLng = lng;

            try
            {
                var result = await _attendanceService.CheckOutAsync(username, parsedLat, parsedLng, photo);
                if (!string.IsNullOrEmpty(result.WarningMessage))
                {
                    TempData["ErrorMessage"] = result.WarningMessage;
                }
                else
                {
                    TempData["SuccessMessage"] = SharedResource.ResourceManager.GetString("Attendance_CheckOutSuccess");
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "Admin,HR,SuperAdmin")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _attendanceService.DeleteRecordAsync(id);
                TempData["SuccessMessage"] = SharedResource.ResourceManager.GetString("Notification_DeletedSuccessfully");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "Admin,HR,SuperAdmin")]
        public async Task<IActionResult> Adjust(int recordId, TimeSpan? newCheckInTime, TimeSpan? newCheckOutTime, string reason)
        {
            try
            {
                var username = User.Identity?.Name ?? "System";
                if (string.IsNullOrWhiteSpace(reason))
                {
                    throw new Exception("Lý do điều chỉnh không được để trống!");
                }

                await _attendanceService.AdjustAttendanceAsync(recordId, newCheckInTime, newCheckOutTime, reason, username);
                TempData["SuccessMessage"] = "Điều chỉnh chấm công thành công!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
