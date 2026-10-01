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

namespace RWPM.Controllers
{
    [Authorize]
    public class ShiftRegistrationController : Controller
    {
        private readonly IShiftRegistrationService _registrationService;
        private readonly DefaultDatabaseContext _context;

        public ShiftRegistrationController(
            IShiftRegistrationService registrationService,
            DefaultDatabaseContext context)
        {
            _registrationService = registrationService;
            _context = context;
        }

        // GET: ShiftRegistration
        public IActionResult Index()
        {
            var role = User.FindFirstValue(ClaimTypes.Role);
            var isAdmin = role == "Admin" || role == "HR" || role == "AreaManager";
            var isStoreManager = role == "StoreManager";
            
            ViewBag.IsManager = isAdmin || isStoreManager;

            int? userStoreId = null;
            if (!isAdmin)
            {
                var username = User.Identity?.Name;
                var currentEmployee = _context.Employee.FirstOrDefault(e => e.Username == username);
                if (currentEmployee != null)
                {
                    userStoreId = currentEmployee.StoreId;
                }
            }

            if (userStoreId.HasValue)
            {
                ViewBag.Shifts = _context.Shift
                    .Include(x => x.StoreShifts)
                    .Where(x => x.IsActive && x.StoreShifts.Any(link => link.IsActive && link.StoreId == userStoreId.Value && link.Store.IsActive))
                    .ToList();
            }
            else
            {
                ViewBag.Shifts = isAdmin ? _context.Shift.Include(x => x.StoreShifts).Where(x => x.IsActive).ToList() : new List<Shift>();
            }
            
            if (isAdmin)
            {
                ViewBag.Employees = _context.Employee.Include(e => e.Account).Where(x => x.IsActive).ToList();
            }
            else if (isStoreManager)
            {
                if (userStoreId.HasValue)
                {
                    ViewBag.Employees = _context.Employee.Include(e => e.Account)
                        .Where(x => x.IsActive && x.StoreId == userStoreId.Value).ToList();
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
            var day = workDate.Date;
            var shifts = await _context.Shift.AsNoTracking().Where(x => x.IsActive && x.EffectiveFrom <= day
                && (x.EffectiveTo == null || x.EffectiveTo >= day)
                && x.StoreShifts.Any(link => link.StoreId == employee.StoreId && link.IsActive && link.Store.IsActive))
                .OrderBy(x => x.StartTime).ToListAsync();
            return Json(shifts.Select(x => new { id = x.ShiftId, name = x.ShiftName,
                time = $"{x.StartTime:hh\\:mm} – {x.EndTime:hh\\:mm}{(x.EndDayOffset == 1 ? " (+1)" : "")}" }));
        }

        // GET: ShiftRegistration/GetEvents
        [HttpGet]
        public async Task<IActionResult> GetEvents(DateTime start, DateTime end, int? storeId = null)
        {
            int? employeeId = null;
            var role = User.FindFirstValue(ClaimTypes.Role);
            var isAdmin = role == "Admin" || role == "HR" || role == "AreaManager";
            var isStoreManager = role == "StoreManager";

            if (isStoreManager)
            {
                var username = User.Identity?.Name;
                var currentEmployee = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
                if (currentEmployee != null)
                {
                    storeId = currentEmployee.StoreId; // Force storeId to the manager's store
                }
            }
            else if (!isAdmin)
            {
                var username = User.Identity?.Name;
                if (!string.IsNullOrEmpty(username))
                {
                    var employee = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
                    if (employee != null)
                    {
                        employeeId = employee.EmployeeId;
                    }
                }
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
                if (!isManager)
                {
                    var username = User.Identity?.Name;
                    employee = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
                    if (employee == null) return Unauthorized();
                    viewModel.EmployeeId = employee.EmployeeId;
                }
                else
                {
                    employee = await _context.Employee.FirstOrDefaultAsync(e => e.EmployeeId == viewModel.EmployeeId);
                    if (employee == null) return BadRequest(new { success = false, message = "Employee not found." });
                    
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

                var shiftIds = viewModel.ShiftIds != null && viewModel.ShiftIds.Any() 
                    ? viewModel.ShiftIds 
                    : new List<int> { viewModel.ShiftId };

                var entitiesToCreate = new List<ShiftRegistration>();

                foreach (var sId in shiftIds)
                {
                    var shift = await _context.Shift.FirstOrDefaultAsync(s => s.ShiftId == sId);
                    if (shift == null || !shift.IsActive)
                    {
                        return BadRequest(new { success = false, message = $"Ca làm việc có ID {sId} không tồn tại hoặc đã ngừng hoạt động." });
                    }
                    if (!await _context.StoreShift.AnyAsync(link => link.ShiftId == sId && link.StoreId == employee.StoreId && link.IsActive && link.Store.IsActive))
                    {
                        return BadRequest(new { success = false, message = $"Ca làm việc {shift.ShiftName} không áp dụng cho chi nhánh của nhân viên." });
                    }

                    var entity = viewModel.ToEntity();
                    entity.ShiftId = sId;
                    entity.StoreId = employee.StoreId;
                    entity.CreatedDate = DateTime.Now;
                    entity.CreatedBy = User.Identity?.Name ?? "system";
                    
                    entitiesToCreate.Add(entity);
                }

                await _registrationService.CreateBatchAsync(entitiesToCreate);

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
        public async Task<IActionResult> UpdateStatus(int id, RegistrationStatus status)
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
                
                await _registrationService.UpdateStatusAsync(id, status);
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
                var role = User.FindFirstValue(ClaimTypes.Role);
                var isAdmin = role == "Admin" || role == "HR" || role == "AreaManager";
                var isStoreManager = role == "StoreManager";
                var isManager = isAdmin || isStoreManager;

                if (isManager)
                {
                    return BadRequest(new { success = false, message = "Only employees can sync their weekly schedule." });
                }

                var username = User.Identity?.Name;
                var employee = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
                if (employee == null) return Unauthorized();

                var entities = viewModel.Registrations.Select(x => x.ToEntity()).ToList();
                
                await _registrationService.SyncWeeklyRegistrationAsync(employee.EmployeeId, employee.StoreId, viewModel.StartOfWeek, entities);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}
