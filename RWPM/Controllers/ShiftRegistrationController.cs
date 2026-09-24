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
                    .Where(x => x.IsActive && (x.StoreId == null || x.StoreId == userStoreId.Value))
                    .ToList();
            }
            else
            {
                ViewBag.Shifts = _context.Shift.Where(x => x.IsActive).ToList();
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

                var shift = await _context.Shift.FirstOrDefaultAsync(s => s.ShiftId == viewModel.ShiftId);
                if (shift == null || !shift.IsActive)
                {
                    return BadRequest(new { success = false, message = "Ca làm việc không tồn tại hoặc đã ngừng hoạt động." });
                }
                if (shift.StoreId.HasValue && shift.StoreId.Value != employee.StoreId)
                {
                    return BadRequest(new { success = false, message = "Ca làm việc này không áp dụng cho chi nhánh của nhân viên." });
                }

                var entity = viewModel.ToEntity();
                entity.StoreId = employee.StoreId;
                entity.CreatedDate = DateTime.Now;
                entity.CreatedBy = User.Identity?.Name ?? "system";

                await _registrationService.CreateAsync(entity);
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
    }
}
