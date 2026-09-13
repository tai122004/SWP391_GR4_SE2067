using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RWPM.Common.Enums;
using RWPM.Common.Helper;
using RWPM.Models.ViewModels.ShiftRegistration;
using RWPM.Services.Abstraction;
using System.Security.Claims;

using Microsoft.EntityFrameworkCore;
using RWPM.Infrastructure.Data;

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
            ViewBag.IsManager = role == "Admin" || role == "HR" || role == "AreaManager";

            ViewBag.Shifts = _context.Shift.Where(x => x.IsActive).ToList();
            if (ViewBag.IsManager)
            {
                ViewBag.Employees = _context.Employee.Include(e => e.Account).Where(x => x.IsActive).ToList();
            }

            return View();
        }

        // GET: ShiftRegistration/GetEvents
        [HttpGet]
        public async Task<IActionResult> GetEvents(DateTime start, DateTime end, int? storeId = null)
        {
            int? employeeId = null;
            var role = User.FindFirstValue(ClaimTypes.Role);
            var isManager = role == "Admin" || role == "HR" || role == "AreaManager";

            if (!isManager)
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
                var isManager = role == "Admin" || role == "HR" || role == "AreaManager";

                if (!isManager)
                {
                    var username = User.Identity?.Name;
                    var employee = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
                    if (employee == null) return Unauthorized();
                    viewModel.EmployeeId = employee.EmployeeId;
                }

                var entity = viewModel.ToEntity();
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
        [Authorize(Roles = "Admin,HR,AreaManager")]
        public async Task<IActionResult> UpdateStatus(int id, RegistrationStatus status)
        {
            try
            {
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
                var isManager = role == "Admin" || role == "HR" || role == "AreaManager";

                if (!isManager)
                {
                    var username = User.Identity?.Name;
                    var employee = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
                    if (employee == null || entity.EmployeeId != employee.EmployeeId)
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
