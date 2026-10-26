using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RWPM.Models.Entities;
using RWPM.Resources.Shared;
using RWPM.Services.Abstraction;
using System.Security.Claims;
using RWPM.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using RWPM.Common.Security;
using RWPM.Common.Validation;

namespace RWPM.Controllers
{
    [Authorize]
    public class AttendanceController : Controller
    {
        private readonly IAttendanceService _attendanceService;
        private readonly DefaultDatabaseContext _context;
        private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;

        public AttendanceController(
            IAttendanceService attendanceService,
            DefaultDatabaseContext context,
            Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        {
            _env = env;
            _attendanceService = attendanceService;
            _context = context;
        }

        private string? CurrentUsername => User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Name);

        public async Task<IActionResult> Index(string? searchQuery = null, string? view = null)
        {
            var username = CurrentUsername;
            if (string.IsNullOrEmpty(username)) return Challenge();

            // Pure System Admin has no requirement to clock in or manage daily attendance.
            if (User.IsAdmin() && !User.IsHR() && !User.IsStoreManager())
            {
                return RedirectToAction("Index", "Acc");
            }

            ViewBag.TodayRecord = await _attendanceService.GetTodayRecordAsync(username);

            bool canManage = User.IsManager();
            ViewBag.CanManage = canManage;

            var today = DateTime.Today;
            var currentEmployee = await _context.Employee
                .Include(e => e.Store)
                .FirstOrDefaultAsync(e => e.Username == username);
            if (currentEmployee != null)
            {
                var todayRegistrations = await _context.Set<ShiftRegistration>()
                    .Include(sr => sr.Shift)
                    .Include(sr => sr.Store)
                    .Where(sr => sr.EmployeeId == currentEmployee.EmployeeId && sr.WorkDate == today && sr.Status == RWPM.Common.Enums.RegistrationStatus.Approved)
                    .ToListAsync();

                var completedShiftIds = await _context.AttendanceRecord
                    .Where(r => r.Username == username && r.Date == today && r.CheckOutTime.HasValue)
                    .Select(r => r.ShiftId)
                    .ToListAsync();

                var allTodayShifts = todayRegistrations
                    .Where(sr => sr.Shift != null && sr.Shift.IsActive && !completedShiftIds.Contains(sr.ShiftId))
                    .Select(sr => sr.Shift!)
                    .DistinctBy(s => s.ShiftId)
                    .ToList();
                    
                ViewBag.ActiveShifts = allTodayShifts;
            }
            else
            {
                ViewBag.ActiveShifts = new List<RWPM.Models.Entities.Shift>();
            }
            
            ViewBag.SearchQuery = searchQuery;
            bool isEmployeeTab = view == "employees" || !string.IsNullOrWhiteSpace(searchQuery);
            ViewBag.ActiveTab = isEmployeeTab ? "employees" : "my";

            // 1. Luôn tải lịch sử cá nhân của người đang đăng nhập (Quản trị viên hệ thống đã được chuyển hướng ở trên)
            var myHistory = await _attendanceService.GetHistoryAsync(username);
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
                if (User.IsStoreScoped())
                {
                    if (currentEmployee != null && currentEmployee.StoreId.HasValue)
                    {
                        var managerStoreId = currentEmployee.StoreId;
                        // Subquery (IN (SELECT ...)) thay vì tải danh sách username về rồi Contains
                        var storeUsernames = _context.Employee
                            .Where(e => e.StoreId == managerStoreId)
                            .Select(e => e.Username);

                        empQuery = empQuery.Where(x => storeUsernames.Contains(x.Username));
                        empListQuery = empListQuery.Where(e => e.StoreId == managerStoreId);

                        // Store đã được Include khi tải currentEmployee
                        var store = currentEmployee.Store;
                        ViewBag.ScopeTitle = store != null ? string.Format(SharedResource.Attendance_Scope_Branch, store.StoreName) : SharedResource.Attendance_Scope_YourStore;
                    }
                    else
                    {
                        empQuery = empQuery.Where(x => false);
                        empListQuery = empListQuery.Where(e => false);
                        ViewBag.ScopeTitle = SharedResource.Attendance_Scope_YourStore;
                    }
                }
                else
                {
                    ViewBag.ScopeTitle = SharedResource.Attendance_Scope_Company;
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

            return View(myHistory);
        }

        /// <summary>
        /// Xem ảnh chấm công: chính chủ, Admin/HR, hoặc Quản lý cửa hàng cùng chi nhánh với nhân viên.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Photo(int id, string type)
        {
            var username = CurrentUsername;
            if (string.IsNullOrEmpty(username)) return Challenge();

            var record = await _context.AttendanceRecord.AsNoTracking()
                .Where(r => r.AttendanceId == id)
                .Select(r => new { r.Username, r.CheckInPhotoPath, r.CheckOutPhotoPath })
                .FirstOrDefaultAsync();
            if (record == null) return NotFound();

            var allowed = record.Username == username || User.IsChainManager();
            if (!allowed && User.IsStoreManager())
            {
                var managerStoreId = await _context.Employee.Where(e => e.Username == username).Select(e => e.StoreId).FirstOrDefaultAsync();
                var ownerStoreId = await _context.Employee.Where(e => e.Username == record.Username).Select(e => e.StoreId).FirstOrDefaultAsync();
                allowed = managerStoreId.HasValue && managerStoreId == ownerStoreId;
            }
            if (!allowed) return Forbid();

            var stored = string.Equals(type, "out", StringComparison.OrdinalIgnoreCase) ? record.CheckOutPhotoPath : record.CheckInPhotoPath;
            var physical = RWPM.Infrastructure.Storage.AttendancePhotoStore.ResolvePhysicalPath(_env, stored);
            if (physical == null) return NotFound();

            Response.Headers["Cache-Control"] = "private, max-age=600";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return PhysicalFile(physical, RWPM.Infrastructure.Storage.AttendancePhotoStore.ContentType(physical));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckIn(string? userLatitude = null, string? userLongitude = null, Microsoft.AspNetCore.Http.IFormFile? photo = null)
        {
            var username = CurrentUsername;
            if (string.IsNullOrEmpty(username)) return Challenge();

            try
            {
                var (parsedLat, parsedLng) = AttendanceInputRules.ParseCoordinates(userLatitude, userLongitude);
                var result = await _attendanceService.CheckInAsync(username, parsedLat, parsedLng, photo);
                if (!string.IsNullOrEmpty(result.WarningMessage))
                {
                    TempData["WarningMessage"] = result.WarningMessage; // Chấm công thành công nhưng có cảnh báo -> toast vàng
                }
                else
                {
                    TempData["SuccessMessage"] = SharedResource.ResourceManager.GetString("Attendance_CheckInSuccess");
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = InputRules.UserMessage(ex);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckOut(string? userLatitude = null, string? userLongitude = null, Microsoft.AspNetCore.Http.IFormFile? photo = null)
        {
            var username = CurrentUsername;
            if (string.IsNullOrEmpty(username)) return Challenge();

            try
            {
                var (parsedLat, parsedLng) = AttendanceInputRules.ParseCoordinates(userLatitude, userLongitude);
                var result = await _attendanceService.CheckOutAsync(username, parsedLat, parsedLng, photo);
                if (!string.IsNullOrEmpty(result.WarningMessage))
                {
                    TempData["WarningMessage"] = result.WarningMessage;
                }
                else
                {
                    TempData["SuccessMessage"] = SharedResource.ResourceManager.GetString("Attendance_CheckOutSuccess");
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = InputRules.UserMessage(ex);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.ChainManagers)]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _attendanceService.DeleteRecordAsync(id);
                TempData["SuccessMessage"] = SharedResource.ResourceManager.GetString("Notification_DeletedSuccessfully");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = InputRules.UserMessage(ex);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.ChainManagers)]
        public async Task<IActionResult> Adjust(int recordId, TimeSpan? newCheckInTime, TimeSpan? newCheckOutTime, string reason)
        {
            try
            {
                if (!ModelState.IsValid)
                    throw new InputValidationException("Time", InputRules.Text("Giờ điều chỉnh không hợp lệ.", "Invalid adjustment time."));
                var username = User.Identity?.Name ?? "System";
                if (string.IsNullOrWhiteSpace(reason))
                {
                    throw new InvalidOperationException(SharedResource.Attendance_Err_AdjustReasonRequired);
                }

                await _attendanceService.AdjustAttendanceAsync(recordId, newCheckInTime, newCheckOutTime, reason, username);
                TempData["SuccessMessage"] = SharedResource.Attendance_AdjustSuccess;
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = InputRules.UserMessage(ex);
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
