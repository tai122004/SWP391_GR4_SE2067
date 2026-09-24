using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RWPM.Common.Enums;
using RWPM.Services.Abstraction;

namespace RWPM.Controllers
{
    [Authorize]
    public class RequestController : Controller
    {
        private readonly IShiftRegistrationService _shiftRegistrationService;
        private readonly RWPM.Infrastructure.Data.DefaultDatabaseContext _context;
        private readonly IShiftService _shiftService;

        public RequestController(IShiftRegistrationService shiftRegistrationService, RWPM.Infrastructure.Data.DefaultDatabaseContext context, IShiftService shiftService)
        {
            _shiftRegistrationService = shiftRegistrationService;
            _context = context;
            _shiftService = shiftService;
        }

        public async Task<IActionResult> Index(string start, string end, int? status)
        {
            // Default to current month if no dates provided
            DateTime startDate;
            DateTime endDate;

            if (string.IsNullOrEmpty(start) || !DateTime.TryParse(start, out startDate))
            {
                startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            }

            if (string.IsNullOrEmpty(end) || !DateTime.TryParse(end, out endDate))
            {
                endDate = startDate.AddMonths(2).AddDays(-1);
            }

            RegistrationStatus? regStatus = null;
            if (status.HasValue && Enum.IsDefined(typeof(RegistrationStatus), status.Value))
            {
                regStatus = (RegistrationStatus)status.Value;
            }

            var allRequests = await _shiftRegistrationService.GetRequestsAsync(startDate, endDate, null);

            bool isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin") || User.IsInRole("AreaManager") || User.IsInRole("HR");
            bool isStoreManager = User.IsInRole("StoreManager");
            ViewBag.IsManager = isAdmin || isStoreManager;

            var username = User.Identity?.Name;
            var currentEmployee = _context.Employee.FirstOrDefault(e => e.Username == username);

            if (!isAdmin && !isStoreManager)
            {
                if (!string.IsNullOrEmpty(username))
                {
                    allRequests = allRequests.Where(x => x.Employee?.Username == username).ToList();
                }
            }
            else if (isStoreManager && !isAdmin)
            {
                if (currentEmployee != null)
                {
                    allRequests = allRequests.Where(x => x.StoreId == currentEmployee.StoreId).ToList();
                }
            }

            ViewBag.PendingCount = allRequests.Count(x => x.Status == RegistrationStatus.Pending);
            ViewBag.ApprovedCount = allRequests.Count(x => x.Status == RegistrationStatus.Approved);
            ViewBag.RejectedCount = allRequests.Count(x => x.Status == RegistrationStatus.Rejected);

            var requests = allRequests;
            if (regStatus.HasValue)
            {
                requests = requests.Where(x => x.Status == regStatus.Value).ToList();
            }

            ViewBag.StartDate = startDate.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate.ToString("yyyy-MM-dd");
            ViewBag.CurrentStatus = status;

            if (isAdmin || isStoreManager)
            {
                var empQuery = Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.Include(_context.Employee, e => e.Account).Where(e => e.IsActive);
                if (isStoreManager && !isAdmin && currentEmployee != null)
                {
                    empQuery = empQuery.Where(e => e.StoreId == currentEmployee.StoreId);
                }
                ViewBag.Employees = empQuery.ToList();
            }
            ViewBag.Shifts = await _shiftService.GetAllAsync();

            return View(requests);
        }
        
        [HttpPost]
        [Authorize(Roles = "Admin,HR,AreaManager,SuperAdmin,StoreManager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, int status)
        {
            try
            {
                if (User.IsInRole("StoreManager") && !User.IsInRole("Admin") && !User.IsInRole("SuperAdmin") && !User.IsInRole("AreaManager") && !User.IsInRole("HR"))
                {
                    var username = User.Identity?.Name;
                    var currentEmployee = _context.Employee.FirstOrDefault(e => e.Username == username);
                    var request = await _shiftRegistrationService.GetByIdAsync(id);
                    if (request == null || currentEmployee == null || request.StoreId != currentEmployee.StoreId)
                    {
                        return Json(new { success = false, message = "Bạn không có quyền duyệt yêu cầu của cửa hàng khác." });
                    }
                }

                await _shiftRegistrationService.UpdateStatusAsync(id, (RegistrationStatus)status);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin,HR,AreaManager,SuperAdmin,StoreManager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateBulkStatus([FromBody] BulkStatusUpdateModel model)
        {
            try
            {
                if (model?.Ids == null || !model.Ids.Any())
                {
                    return Json(new { success = false, message = "Không có yêu cầu nào được chọn." });
                }

                if (User.IsInRole("StoreManager") && !User.IsInRole("Admin") && !User.IsInRole("SuperAdmin") && !User.IsInRole("AreaManager") && !User.IsInRole("HR"))
                {
                    var username = User.Identity?.Name;
                    var currentEmployee = _context.Employee.FirstOrDefault(e => e.Username == username);
                    
                    var requests = _context.ShiftRegistration.Where(x => model.Ids.Contains(x.ShiftRegistrationId)).ToList();
                    if (currentEmployee == null || requests.Any(x => x.StoreId != currentEmployee.StoreId))
                    {
                        return Json(new { success = false, message = "Bạn không có quyền duyệt yêu cầu của cửa hàng khác." });
                    }
                }

                await _shiftRegistrationService.UpdateBulkStatusAsync(model.Ids, (RegistrationStatus)model.Status);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }

    public class BulkStatusUpdateModel
    {
        public List<int> Ids { get; set; }
        public int Status { get; set; }
    }
}
