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

                var allTodayShifts = await _context.Set<RWPM.Models.Entities.Shift>()
                    .Where(s => s.IsActive && registeredShiftIds.Contains(s.ShiftId))
                    .ToListAsync();
                    
                var now = DateTime.Now.TimeOfDay;
                var validShifts = new List<RWPM.Models.Entities.Shift>();
                foreach(var s in allTodayShifts)
                {
                    int lThreshold = s.LateThresholdMinutes ?? RWPM.Common.Constants.ShiftDefaults.LateThresholdMinutes;
                    var start1Late = s.StartTime.Add(TimeSpan.FromMinutes(lThreshold));
                    
                    bool isExpired = now > start1Late;

                    if (!isExpired) {
                        validShifts.Add(s);
                    }
                }
                ViewBag.ActiveShifts = validShifts;
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
        public async Task<IActionResult> CheckIn(double? userLatitude = null, double? userLongitude = null, Microsoft.AspNetCore.Http.IFormFile? photo = null)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Login", "Auth");

            try
            {
                await _attendanceService.CheckInAsync(username, userLatitude, userLongitude, photo);
                TempData["SuccessMessage"] = SharedResource.ResourceManager.GetString("Attendance_CheckInSuccess");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> CheckOut(double? userLatitude = null, double? userLongitude = null, Microsoft.AspNetCore.Http.IFormFile? photo = null)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Login", "Auth");

            try
            {
                await _attendanceService.CheckOutAsync(username, userLatitude, userLongitude, photo);
                TempData["SuccessMessage"] = SharedResource.ResourceManager.GetString("Attendance_CheckOutSuccess");
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
    }
}
