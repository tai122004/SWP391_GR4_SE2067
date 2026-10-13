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
        private readonly IAttendanceAdjustmentService _attendanceAdjustmentService;
        private readonly IAccService _accService;
        private readonly DefaultDatabaseContext _context;

        public AttendanceController(
            IAttendanceService attendanceService,
            IAttendanceAdjustmentService attendanceAdjustmentService,
            IAccService accService,
            DefaultDatabaseContext context)
        {
            _attendanceService = attendanceService;
            _attendanceAdjustmentService = attendanceAdjustmentService;
            _accService = accService;
            _context = context;
        }

        public async Task<IActionResult> Index(string? searchQuery = null, string? view = null)
        {
            var username = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Name);
            if (string.IsNullOrEmpty(username)) return Challenge();

            ViewBag.TodayRecord = await _attendanceService.GetTodayRecordAsync(username);
            
            // Pure System Admin has no requirement to clock in or manage daily attendance.
            bool isSystemAdmin = (User.IsInRole("Admin") || User.IsInRole("SuperAdmin")) && !User.IsInRole("HR") && !User.IsInRole("StoreManager");
            if (isSystemAdmin)
            {
                return RedirectToAction("Index", "Acc");
            }

            bool canManage = User.IsInRole("Admin") || User.IsInRole("HR") || User.IsInRole("StoreManager") || User.IsInRole("SuperAdmin");
            
            ViewBag.IsAdmin = isSystemAdmin;
            ViewBag.CanManage = canManage;

            var today = DateTime.Today;
            var currentEmployee = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
            if (currentEmployee != null)
            {
                var registeredShiftIds = await _context.Set<ShiftRegistration>()
                    .Where(sr => sr.EmployeeId == currentEmployee.EmployeeId && sr.WorkDate == today && sr.Status == RWPM.Common.Enums.RegistrationStatus.Approved)
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
            ViewBag.CurrentView = view;
            bool isEmployeeTab = view == "employees" || !string.IsNullOrWhiteSpace(searchQuery);
            ViewBag.ActiveTab = isEmployeeTab ? "employees" : "my";

            // 1. Luôn tải lịch sử cá nhân của người đang đăng nhập (trừ Quản trị viên hệ thống không chấm công)
            var myHistory = isSystemAdmin ? new List<AttendanceRecord>() : await _attendanceService.GetHistoryAsync(username);
            ViewBag.MyHistory = myHistory;

            // 2. Nếu có quyền quản lý (HR, Quản lý cửa hàng, Admin): Tải lịch sử nhân viên
            var employeeHistory = new List<AttendanceRecord>();
            var empList = new List<RWPM.Models.Entities.Acc>();
            if (canManage)
            {
                IQueryable<AttendanceRecord> empQuery = _context.AttendanceRecord
                    .Include(x => x.Shift)
                    .Include(x => x.Account)
                    .Include(x => x.AdjustmentHistories)
                    .AsQueryable();

                IQueryable<RWPM.Models.Entities.Employee> empListQuery = _context.Employee
                    .Include(e => e.Account)
                    .Where(e => e.IsActive);

                // Nếu là StoreManager (không phải Admin/HR): Chỉ xem nhân viên thuộc cùng cửa hàng
                if (User.IsInRole("StoreManager") && !User.IsInRole("Admin") && !User.IsInRole("SuperAdmin") && !User.IsInRole("HR"))
                {
                    if (currentEmployee != null && currentEmployee.StoreId.HasValue)
                    {
                        var storeUsernames = await _context.Employee
                            .Where(e => e.StoreId == currentEmployee.StoreId)
                            .Select(e => e.Username)
                            .ToListAsync();

                        empQuery = empQuery.Where(x => storeUsernames.Contains(x.Username));
                        empListQuery = empListQuery.Where(e => e.StoreId == currentEmployee.StoreId);

                        var store = await _context.Store.FirstOrDefaultAsync(s => s.StoreId == currentEmployee.StoreId);
                        ViewBag.ScopeTitle = store != null ? $"Chi nhánh {store.StoreName}" : "Cửa hàng của bạn";
                    }
                    else
                    {
                        empQuery = empQuery.Where(x => false);
                        empListQuery = empListQuery.Where(e => false);
                        ViewBag.ScopeTitle = "Cửa hàng của bạn";
                    }
                }
                else
                {
                    ViewBag.ScopeTitle = "Toàn công ty";
                }

                if (!string.IsNullOrWhiteSpace(searchQuery))
                {
                    empQuery = empQuery.Where(x => x.Username.Contains(searchQuery) || (x.Account != null && x.Account.FullName.Contains(searchQuery)));
                }

                employeeHistory = await empQuery
                    .OrderByDescending(x => x.Date)
                    .ThenByDescending(x => x.CheckInTime)
                    .ThenByDescending(x => x.AttendanceId)
                    .ToListAsync();

                var rawAccounts = await empListQuery
                    .Select(e => e.Account)
                    .Where(a => a != null)
                    .ToListAsync();

                empList = rawAccounts
                    .GroupBy(a => a.Username)
                    .Select(g => g.First())
                    .OrderBy(a => a.FullName)
                    .ToList();
            }
            ViewBag.EmployeeHistory = employeeHistory;
            ViewBag.Employees = empList;

            return View(isSystemAdmin ? employeeHistory : myHistory);
        }

        [HttpPost]
        public async Task<IActionResult> CheckIn(string? userLatitude = null, string? userLongitude = null, Microsoft.AspNetCore.Http.IFormFile? photo = null)
        {
            var username = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Name);
            if (string.IsNullOrEmpty(username)) return Challenge();

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
            var username = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Name);
            if (string.IsNullOrEmpty(username)) return Challenge();

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
