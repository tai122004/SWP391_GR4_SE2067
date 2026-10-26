using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RWPM.Common.Enums;
using RWPM.Common.Helper;
using RWPM.Models.ViewModels.ShiftRegistration;
using RWPM.Services.Abstraction;
using System.Security.Claims;

using Microsoft.EntityFrameworkCore;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;
using SR = RWPM.Resources.Shared.ShiftRegistrationResource;

namespace RWPM.Controllers
{
    [Authorize]
    public class ShiftRegistrationController : Controller
    {
        private readonly IShiftRegistrationService _registrationService;
        private readonly DefaultDatabaseContext _context;
        private readonly INotificationService _notificationService;

        public ShiftRegistrationController(
            IShiftRegistrationService registrationService,
            DefaultDatabaseContext context,
            INotificationService notificationService)
        {
            _registrationService = registrationService;
            _context = context;
            _notificationService = notificationService;
        }

        // GET: ShiftRegistration
        public async Task<IActionResult> Index(int? storeId = null)
        {
            bool isSystemAdmin = (User.IsInRole("Admin") || User.IsInRole("SuperAdmin")) && !User.IsInRole("HR") && !User.IsInRole("StoreManager");
            if (isSystemAdmin)
            {
                return RedirectToAction("Index", "Acc");
            }

            var role = User.FindFirstValue(ClaimTypes.Role);
            var isAdmin = role == "Admin" || role == "HR" || role == "AreaManager";
            var isStoreManager = role == "StoreManager";
            
            ViewBag.IsManager = isAdmin || isStoreManager;

            var username = User.Identity?.Name;
            var currentEmployee = await _context.Employee.AsNoTracking().FirstOrDefaultAsync(e => e.Username == username);

            // Không tự gán chi nhánh cho nhân viên chưa có StoreId; chỉ dùng chi nhánh tạm để hiển thị
            int? userStoreId = storeId ?? currentEmployee?.StoreId;
            if (!userStoreId.HasValue)
            {
                userStoreId = await _context.Store.Where(s => s.IsActive).Select(s => (int?)s.StoreId).FirstOrDefaultAsync();
            }

            ViewBag.CurrentStoreId = userStoreId;
            ViewBag.AllStores = await _context.Store.Where(s => s.IsActive).ToListAsync();

            if (userStoreId.HasValue)
            {
                ViewBag.Shifts = await _context.Shift
                    .Include(x => x.StoreShifts)
                    .Where(x => x.IsActive && x.StoreShifts.Any(link => link.IsActive && link.StoreId == userStoreId.Value && link.Store.IsActive))
                    .ToListAsync();
            }
            else
            {
                ViewBag.Shifts = await _context.Shift.Include(x => x.StoreShifts).Where(x => x.IsActive).ToListAsync();
            }
            
            if (isAdmin)
            {
                ViewBag.Employees = await _context.Employee.Include(e => e.Account).Where(x => x.IsActive).ToListAsync();
            }
            else if (isStoreManager)
            {
                if (userStoreId.HasValue)
                {
                    ViewBag.Employees = await _context.Employee.Include(e => e.Account)
                        .Where(x => x.IsActive && x.StoreId == userStoreId.Value).ToListAsync();
                }
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> AvailableShifts(int employeeId, DateTime workDate)
        {
            var current = await _context.Employee.AsNoTracking().FirstOrDefaultAsync(x => x.Username == User.Identity!.Name);
            var chainAccess = User.IsInRole("Admin") || User.IsInRole("HR") || User.IsInRole("AreaManager");
            if (!chainAccess && !User.IsInRole("StoreManager"))
            {
                if (current == null) return Forbid();
                employeeId = current.EmployeeId;
            }
            var employee = await _context.Employee.AsNoTracking().FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.IsActive);
            if (employee == null) return NotFound();
            if (!chainAccess && User.IsInRole("StoreManager") && (current == null || current.StoreId != employee.StoreId)) return Forbid();
            
            var targetStoreId = employee.StoreId ?? current?.StoreId ?? await _context.Store.Where(s => s.IsActive).Select(s => (int?)s.StoreId).FirstOrDefaultAsync();

            var day = workDate.Date;
            var shiftsQuery = _context.Shift.AsNoTracking().Where(x => x.IsActive && x.EffectiveFrom <= day
                && (x.EffectiveTo == null || x.EffectiveTo >= day));

            if (targetStoreId.HasValue)
            {
                shiftsQuery = shiftsQuery.Where(x => x.StoreShifts.Any(link => link.StoreId == targetStoreId.Value && link.IsActive && link.Store.IsActive));
            }

            var shifts = await shiftsQuery.OrderBy(x => x.StartTime).ToListAsync();
            return Json(shifts.Select(x => new { id = x.ShiftId, name = x.ShiftName,
                time = $"{x.StartTime:hh\\:mm} – {x.EndTime:hh\\:mm}{(x.EndDayOffset == 1 ? " (+1)" : "")}" }));
        }

        // GET: ShiftRegistration/GetEvents
        [HttpGet]
        public async Task<IActionResult> GetEvents(DateTime start, DateTime end, int? storeId = null, int? employeeId = null, bool myOnly = false)
        {
            var role = User.FindFirstValue(ClaimTypes.Role);
            var isAdmin = role == "Admin" || role == "HR" || role == "AreaManager";
            var isStoreManager = role == "StoreManager";
            var username = User.Identity?.Name;
            var currentEmployee = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);

            if (myOnly || (!isAdmin && !isStoreManager))
            {
                employeeId = currentEmployee?.EmployeeId ?? 0;
                storeId = null;
            }
            else if (isStoreManager)
            {
                if (currentEmployee != null)
                {
                    storeId = currentEmployee.StoreId;
                }
            }
            else if (employeeId.HasValue)
            {
                // filter by specific employee
            }

            var events = await _registrationService.GetEventsAsync(start, end, employeeId, storeId);
            var eventVMs = events.Select(ShiftRegistrationEventVM.FromEntity).ToList();

            return Json(eventVMs);
        }

        // POST: ShiftRegistration/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] ShiftRegistrationCreateVM viewModel)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // If not manager, force the EmployeeId to be the logged in user
                var role = User.FindFirstValue(ClaimTypes.Role);
                var isAdmin = role == "Admin" || role == "HR" || role == "AreaManager";
                var isStoreManager = role == "StoreManager";
                var isManager = isAdmin || isStoreManager;

                Employee employee = null;
                if (!isManager || viewModel.EmployeeId <= 0)
                {
                    var username = User.Identity?.Name;
                    employee = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
                    if (employee == null) return Unauthorized();
                    viewModel.EmployeeId = employee.EmployeeId;
                }
                else
                {
                    employee = await _context.Employee.FirstOrDefaultAsync(e => e.EmployeeId == viewModel.EmployeeId);
                    if (employee == null) return BadRequest(new { success = false, message = SR.Err_EmployeeNotFound });
                    
                    // If StoreManager, check if they are trying to assign someone outside their store
                    if (isStoreManager)
                    {
                        var username = User.Identity?.Name;
                        var currentEmployee = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
                        if (currentEmployee == null || employee.StoreId != currentEmployee.StoreId)
                        {
                            return Forbid();
                        }
                    }
                }

                if (!employee.StoreId.HasValue)
                {
                    return BadRequest(new { success = false, message = RWPM.Common.Validation.InputRules.Text("Nhân viên chưa được gán chi nhánh.", "The employee is not assigned to a store.") });
                }

                var shiftIds = viewModel.ShiftIds != null && viewModel.ShiftIds.Any() 
                    ? viewModel.ShiftIds 
                    : new List<int> { viewModel.ShiftId };

                var entitiesToCreate = new List<ShiftRegistration>();

                foreach (var sId in shiftIds)
                {
                    var shift = await _context.Shift.FirstOrDefaultAsync(s => s.ShiftId == sId);
                    if (shift == null || !shift.IsActive)
                    {
                        return BadRequest(new { success = false, message = string.Format(SR.Err_ShiftNotFoundOrInactive, sId) });
                    }
                    if (employee.StoreId.HasValue && !await _context.StoreShift.AnyAsync(link => link.ShiftId == sId && link.StoreId == employee.StoreId.Value && link.IsActive && link.Store.IsActive))
                    {
                        return BadRequest(new { success = false, message = string.Format(SR.Err_ShiftNotAppliedToStore, shift.ShiftName) });
                    }

                    var entity = viewModel.ToEntity();
                    entity.ShiftId = sId;
                    entity.StoreId = employee.StoreId ?? 0;
                    entity.CreatedDate = DateTime.Now;
                    entity.CreatedBy = User.Identity?.Name ?? "system";
                    
                    entitiesToCreate.Add(entity);
                }

                var created = await _registrationService.CreateBatchAsync(entitiesToCreate);
                // Một thông báo gộp cho người duyệt (không gửi từng ca)
                await _notificationService.NotifyShiftRegistrationsSubmittedAsync(employee.EmployeeId,
                    created.Select(x => x.ShiftRegistrationId).ToList(), User.Identity?.Name);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // POST: ShiftRegistration/UpdateStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,HR,AreaManager,StoreManager")]
        public async Task<IActionResult> UpdateStatus(int id, RegistrationStatus status, string? reason = null)
        {
            try
            {
                var role = User.FindFirstValue(ClaimTypes.Role);
                if (role == "StoreManager")
                {
                    var entity = await _registrationService.GetByIdAsync(id);
                    if (entity == null) return NotFound();
                    
                    var username = User.Identity?.Name;
                    var currentEmployee = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
                    if (currentEmployee == null || entity.StoreId != currentEmployee.StoreId)
                    {
                        return Forbid();
                    }
                }
                
                await _registrationService.UpdateStatusAsync(id, status, reason, User.Identity?.Name);
                await _notificationService.NotifyRequestDecisionAsync(NotificationRequestTypes.ShiftReg, new[] { id }, User.Identity?.Name);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // POST: ShiftRegistration/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var entity = await _registrationService.GetByIdAsync(id);
                if (entity == null) return NotFound();

                var role = User.FindFirstValue(ClaimTypes.Role);
                var isAdmin = role == "Admin" || role == "HR" || role == "AreaManager";
                var isStoreManager = role == "StoreManager";
                var isManager = isAdmin || isStoreManager;

                if (!isManager)
                {
                    var username = User.Identity?.Name;
                    var employee = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
                    if (employee == null || entity.EmployeeId != employee.EmployeeId)
                    {
                        return Forbid();
                    }

                    if (entity.Status == RegistrationStatus.Approved)
                    {
                        return BadRequest(new { success = false, message = SR.Err_ApprovedCannotCancel });
                    }

                    if (entity.WorkDate.Date < DateTime.Today)
                    {
                        return BadRequest(new { success = false, message = SR.Err_CannotCancelPast });
                    }
                }
                else if (isStoreManager)
                {
                    var username = User.Identity?.Name;
                    var currentEmployee = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
                    if (currentEmployee == null || entity.StoreId != currentEmployee.StoreId)
                    {
                        return Forbid();
                    }
                }

                await _registrationService.DeleteAsync(id);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
        // POST: ShiftRegistration/SyncWeekly
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncWeekly([FromBody] SyncWeeklyRegistrationVM viewModel)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var username = User.Identity?.Name;
                var employee = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
                if (employee == null) return Unauthorized(new { success = false, message = SR.Err_EmployeeProfileNotFound });

                if (!employee.StoreId.HasValue)
                {
                    return BadRequest(new { success = false, message = RWPM.Common.Validation.InputRules.Text("Nhân viên chưa được gán chi nhánh.", "The employee is not assigned to a store.") });
                }

                int storeId = employee.StoreId.Value;

                var entities = viewModel.Registrations.Select(x => x.ToEntity()).ToList();
                
                await _registrationService.SyncWeeklyRegistrationAsync(employee.EmployeeId, storeId, viewModel.StartOfWeek, entities);
                // Các ca mới được thêm sẽ có Id sau khi lưu; gộp thành một thông báo cho mỗi lần đồng bộ
                await _notificationService.NotifyShiftRegistrationsSubmittedAsync(employee.EmployeeId,
                    entities.Where(x => x.ShiftRegistrationId > 0).Select(x => x.ShiftRegistrationId).ToList(), username);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}
