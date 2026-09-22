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
            ViewBag.ActiveShifts = await _context.Set<RWPM.Models.Entities.Shift>().Where(s => s.IsActive).ToListAsync();
            
            bool isAdmin = User.IsInRole("Admin") || User.IsInRole("HR") || User.IsInRole("SuperAdmin");
            ViewBag.IsAdmin = isAdmin;
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
        public async Task<IActionResult> CheckIn(int shiftId, double? userLatitude = null, double? userLongitude = null)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Login", "Auth");

            try
            {
                await _attendanceService.CheckInAsync(username, shiftId, userLatitude, userLongitude);
                TempData["SuccessMessage"] = SharedResource.ResourceManager.GetString("Attendance_CheckInSuccess");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> CheckOut(double? userLatitude = null, double? userLongitude = null)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Login", "Auth");

            try
            {
                await _attendanceService.CheckOutAsync(username, userLatitude, userLongitude);
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
