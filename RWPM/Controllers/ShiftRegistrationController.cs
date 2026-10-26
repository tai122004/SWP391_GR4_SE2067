using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RWPM.Common.Enums;
using RWPM.Common.Security;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.ShiftRegistration;
using RWPM.Services.Abstraction;
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

        /// <summary>Hồ sơ nhân viên của người đang đăng nhập (theo Username); null nếu chưa có.</summary>
        private Task<Employee?> FindCurrentEmployeeAsync()
        {
            var username = User.Identity?.Name;
            return _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
        }

        // GET: ShiftRegistration
        public async Task<IActionResult> Index(int? storeId = null)
        {
            bool isSystemAdmin = User.IsAdmin() && !User.IsHR() && !User.IsStoreManager();
            if (isSystemAdmin)
            {
                return RedirectToAction("Index", "Acc");
            }

            var isAdmin = User.IsChainManager();
            var isStoreManager = User.IsStoreManager();

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
            else if (isStoreManager && userStoreId.HasValue)
            {
                ViewBag.Employees = await _context.Employee.Include(e => e.Account)
                    .Where(x => x.IsActive && x.StoreId == userStoreId.Value).ToListAsync();
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> AvailableShifts(int employeeId, DateTime workDate)
        {
            var current = await _context.Employee.AsNoTracking().FirstOrDefaultAsync(x => x.Username == User.Identity!.Name);
            var chainAccess = User.IsChainManager();
            if (!chainAccess && !User.IsStoreManager())
            {
                if (current == null) return Forbid();
                employeeId = current.EmployeeId;
            }
            var employee = await _context.Employee.AsNoTracking().FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.IsActive);
            if (employee == null) return NotFound();
            if (!chainAccess && User.IsStoreManager() && (current == null || current.StoreId != employee.StoreId)) return Forbid();

            var targetStoreId = employee.StoreId ?? current?.StoreId ?? await _context.Store.Where(s => s.IsActive).Select(s => (int?)s.StoreId).FirstOrDefaultAsync();

            var day = workDate.Date;
            var shiftsQuery = _context.Shift.AsNoTracking().Where(x => x.IsActive && x.EffectiveFrom <= day
                && (x.EffectiveTo == null || x.EffectiveTo >= day));

            if (targetStoreId.HasValue)
            {
                shiftsQuery = shiftsQuery.Where(x => x.StoreShifts.Any(link => link.StoreId == targetStoreId.Value && link.IsActive && link.Store.IsActive));
            }

            var shifts = await shiftsQuery.OrderBy(x => x.StartTime).ToListAsync();

            // Thời gian có thể làm việc: tự đăng ký -> ca không phù hợp bị khóa (blocked); quản lý xếp cho người khác -> chỉ cảnh báo (warning)
            var isSelf = current != null && current.EmployeeId == employee.EmployeeId;
            var issues = day >= DateTime.Today
                ? (await _registrationService.GetAvailabilityIssuesAsync(employee.EmployeeId, shifts.Select(x => (x.ShiftId, day))))
                    .ToDictionary(x => x.ShiftId, x => x.Message)
                : new Dictionary<int, string>();
            return Json(shifts.Select(x => new { id = x.ShiftId, name = x.ShiftName,
                time = $"{x.StartTime:hh\\:mm} – {x.EndTime:hh\\:mm}{(x.EndDayOffset == 1 ? " (+1)" : "")}",
                blocked = isSelf && issues.ContainsKey(x.ShiftId) ? issues[x.ShiftId] : null,
                warning = !isSelf && issues.ContainsKey(x.ShiftId) ? issues[x.ShiftId] : null }));
        }

        // GET: ShiftRegistration/GetEvents
        [HttpGet]
        public async Task<IActionResult> GetEvents(DateTime start, DateTime end, int? storeId = null, int? employeeId = null, bool myOnly = false, bool withAvailability = false)
        {
            var isAdmin = User.IsChainManager();
            var isStoreManager = User.IsStoreManager();
            var currentEmployee = await FindCurrentEmployeeAsync();

            if (myOnly || (!isAdmin && !isStoreManager))
            {
                employeeId = currentEmployee?.EmployeeId ?? 0;
                storeId = null;
            }
            else if (isStoreManager && currentEmployee != null)
            {
                storeId = currentEmployee.StoreId;
            }

            var events = await _registrationService.GetEventsAsync(start, end, employeeId, storeId);
            var eventVMs = events.Select(ShiftRegistrationEventVM.FromEntity).ToList();

            // withAvailability (lưới "Đăng ký ca của tôi"): trả thêm các ô bị chặn theo thời gian có thể làm việc
            // dạng { events, blocked: { "shiftId_yyyy-MM-dd": lý do } }; không truyền -> giữ nguyên mảng sự kiện như cũ.
            if (withAvailability && (myOnly || (!isAdmin && !isStoreManager)))
            {
                var blocked = currentEmployee?.StoreId != null
                    ? await _registrationService.GetBlockedSlotsAsync(currentEmployee.EmployeeId, currentEmployee.StoreId.Value, start, end)
                    : new Dictionary<string, string>();
                return Json(new { events = eventVMs, blocked });
            }

            return Json(eventVMs);
        }

        // POST: ShiftRegistration/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] ShiftRegistrationCreateVM viewModel, [FromQuery] bool confirmWarnings = false)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // If not manager, force the EmployeeId to be the logged in user
                var isStoreManager = User.IsStoreManager();
                var isManager = User.IsChainManager() || isStoreManager;

                Employee? employee;
                if (!isManager || viewModel.EmployeeId <= 0)
                {
                    employee = await FindCurrentEmployeeAsync();
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
                        var currentEmployee = await FindCurrentEmployeeAsync();
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

                var storeId = employee.StoreId.Value;
                var shiftIds = viewModel.ShiftIds != null && viewModel.ShiftIds.Any()
                    ? viewModel.ShiftIds
                    : new List<int> { viewModel.ShiftId };

                // Nạp toàn bộ ca + liên kết chi nhánh bằng 2 truy vấn (thay vì 2 truy vấn cho mỗi ca)
                var shiftsById = await _context.Shift.Where(s => shiftIds.Contains(s.ShiftId)).ToDictionaryAsync(s => s.ShiftId);
                var appliedShiftIds = (await _context.StoreShift
                    .Where(link => shiftIds.Contains(link.ShiftId) && link.StoreId == storeId && link.IsActive && link.Store.IsActive)
                    .Select(link => link.ShiftId).ToListAsync()).ToHashSet();

                var entitiesToCreate = new List<ShiftRegistration>();

                foreach (var sId in shiftIds)
                {
                    if (!shiftsById.TryGetValue(sId, out var shift) || !shift.IsActive)
                    {
                        return BadRequest(new { success = false, message = string.Format(SR.Err_ShiftNotFoundOrInactive, sId) });
                    }
                    if (!appliedShiftIds.Contains(sId))
                    {
                        return BadRequest(new { success = false, message = string.Format(SR.Err_ShiftNotAppliedToStore, shift.ShiftName) });
                    }

                    var entity = viewModel.ToEntity();
                    entity.ShiftId = sId;
                    entity.StoreId = storeId;
                    entity.CreatedDate = DateTime.Now;
                    entity.CreatedBy = User.Identity?.Name ?? "system";

                    entitiesToCreate.Add(entity);
                }

                // Tự đăng ký cho chính mình -> chặn ca ngoài thời gian có thể làm việc;
                // quản lý tạo cho người khác -> cảnh báo, cần xác nhận (confirmWarnings=true) mới tạo
                var isSelf = string.Equals(employee.Username, User.Identity?.Name, StringComparison.OrdinalIgnoreCase);
                if (!isSelf && !confirmWarnings)
                {
                    var warnings = await _registrationService.GetAssignmentWarningsAsync(employee.EmployeeId,
                        entitiesToCreate.Select(x => (x.ShiftId, x.WorkDate)));
                    if (warnings.Any)
                        return Ok(new { success = false, needConfirm = true, warnings = warnings.All });
                }

                var created = await _registrationService.CreateBatchAsync(entitiesToCreate, enforceAvailability: isSelf);
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
        [Authorize(Roles = AppRoles.Managers)]
        public async Task<IActionResult> UpdateStatus(int id, RegistrationStatus status, string? reason = null, bool confirmWarnings = false)
        {
            try
            {
                if (User.IsStoreManager())
                {
                    var entity = await _registrationService.GetByIdAsync(id);
                    if (entity == null) return NotFound();

                    var currentEmployee = await FindCurrentEmployeeAsync();
                    if (currentEmployee == null || entity.StoreId != currentEmployee.StoreId)
                    {
                        return Forbid();
                    }
                }

                if (status == RegistrationStatus.Approved && !confirmWarnings)
                {
                    var warnings = await _registrationService.GetApprovalWarningsAsync(new[] { id });
                    if (warnings.Any)
                        return Ok(new { success = false, needConfirm = true, warnings = warnings.All,
                            availabilityWarnings = warnings.Availability, demandWarnings = warnings.Demand });
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

                var isStoreManager = User.IsStoreManager();
                var isManager = User.IsChainManager() || isStoreManager;

                if (!isManager)
                {
                    var employee = await FindCurrentEmployeeAsync();
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
                    var currentEmployee = await FindCurrentEmployeeAsync();
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

                await _registrationService.SyncWeeklyRegistrationAsync(employee.EmployeeId, storeId, viewModel.StartOfWeek, entities, enforceAvailability: true);
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
