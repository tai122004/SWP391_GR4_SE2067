using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RWPM.Common.Enums;
using RWPM.Models.ViewModels.ShiftRegistration;
using RWPM.Resources.Shared;
using RWPM.Services.Abstraction;

namespace RWPM.Controllers
{
    [Authorize]
    public class RequestController : Controller
    {
        private readonly IShiftRegistrationService _shiftRegistrationService;
        private readonly IShiftSwapService _shiftSwapService;
        private readonly RWPM.Infrastructure.Data.DefaultDatabaseContext _context;
        private readonly IShiftService _shiftService;

        public RequestController(
            IShiftRegistrationService shiftRegistrationService,
            IShiftSwapService shiftSwapService,
            RWPM.Infrastructure.Data.DefaultDatabaseContext context,
            IShiftService shiftService)
        {
            _shiftRegistrationService = shiftRegistrationService;
            _shiftSwapService = shiftSwapService;
            _context = context;
            _shiftService = shiftService;
        }

        public async Task<IActionResult> Index(string start, string end, int? status, string? type = null)
        {
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

            bool isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin") || User.IsInRole("AreaManager") || User.IsInRole("HR");
            bool isStoreManager = User.IsInRole("StoreManager");
            ViewBag.IsManager = isAdmin || isStoreManager;

            var username = User.Identity?.Name;
            var currentEmployee = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);

            // 1. Fetch Shift Registrations
            var allRegistrations = await _shiftRegistrationService.GetRequestsAsync(startDate, endDate, null);

            // 2. Fetch Shift Swap Requests
            int? storeFilter = null;
            if (isStoreManager && !isAdmin && currentEmployee != null)
            {
                storeFilter = currentEmployee.StoreId;
            }
            var allSwaps = await _shiftSwapService.GetSwapRequestsAsync(startDate, endDate, storeFilter, null);

            // Filter for regular employees (only see own requests)
            if (!isAdmin && !isStoreManager)
            {
                if (!string.IsNullOrEmpty(username))
                {
                    allRegistrations = allRegistrations.Where(x => x.Employee?.Username == username).ToList();
                    allSwaps = allSwaps.Where(x => x.RequesterEmployee?.Username == username).ToList();
                }
            }
            else if (isStoreManager && !isAdmin && currentEmployee != null)
            {
                allRegistrations = allRegistrations.Where(x => x.StoreId == currentEmployee.StoreId).ToList();
            }

            // Map to unified items
            var unifiedList = new List<RequestItemVM>();

            foreach (var r in allRegistrations)
            {
                unifiedList.Add(new RequestItemVM
                {
                    Id = r.ShiftRegistrationId,
                    RequestType = "ShiftReg",
                    RequestTypeName = RequestResource.ShiftRegistration,
                    Code = "YCCL" + r.CreatedDate.ToString("yyMMdd") + "-" + r.ShiftRegistrationId.ToString("D3"),
                    EmployeeId = r.EmployeeId,
                    EmployeeName = r.Employee?.Account?.FullName ?? r.Employee?.Username ?? "N/A",
                    EmployeeCode = r.Employee?.EmployeeCode ?? "",
                    StoreId = r.StoreId,
                    StoreName = r.Store?.StoreName ?? "",
                    CreatedDate = r.CreatedDate,
                    CreatedDateStr = r.CreatedDate.ToString("dd/MM/yyyy HH:mm"),
                    Status = r.Status,
                    ShiftName = r.Shift?.ShiftName ?? "",
                    WorkDateStr = r.WorkDate.ToString("dd/MM/yyyy"),
                    Note = r.Note,
                    RejectionReason = r.RejectionReason,
                    ApprovedBy = r.ApprovedBy,
                    ApprovedAtStr = r.ApprovedAt?.ToString("dd/MM/yyyy HH:mm")
                });
            }

            foreach (var s in allSwaps)
            {
                string targetDesc = s.TargetEmployee != null
                    ? $"{RequestResource.Swap_WithCoworker}: {s.TargetEmployee.Account?.FullName ?? s.TargetEmployee.Username} ({(s.TargetRegistration?.Shift?.ShiftName ?? "")} {s.TargetRegistration?.WorkDate.ToString("dd/MM/yyyy")})"
                    : $"{RequestResource.Swap_DirectChange}: {(s.TargetShift?.ShiftName ?? "")} ({(s.TargetWorkDate.HasValue ? s.TargetWorkDate.Value.ToString("dd/MM/yyyy") : "")})";

                unifiedList.Add(new RequestItemVM
                {
                    Id = s.ShiftSwapRequestId,
                    RequestType = "ShiftSwap",
                    RequestTypeName = RequestResource.ShiftSwap,
                    Code = "DCCL" + s.CreatedDate.ToString("yyMMdd") + "-" + s.ShiftSwapRequestId.ToString("D3"),
                    EmployeeId = s.RequesterEmployeeId,
                    EmployeeName = s.RequesterEmployee?.Account?.FullName ?? s.RequesterEmployee?.Username ?? "N/A",
                    EmployeeCode = s.RequesterEmployee?.EmployeeCode ?? "",
                    StoreId = s.StoreId,
                    StoreName = s.Store?.StoreName ?? "",
                    CreatedDate = s.CreatedDate,
                    CreatedDateStr = s.CreatedDate.ToString("dd/MM/yyyy HH:mm"),
                    Status = s.Status,
                    ShiftName = s.SourceRegistration?.Shift?.ShiftName ?? "",
                    WorkDateStr = s.SourceRegistration?.WorkDate.ToString("dd/MM/yyyy") ?? "",
                    Note = s.Reason,
                    RejectionReason = s.RejectionReason,
                    ApprovedBy = s.ApprovedBy,
                    ApprovedAtStr = s.ApprovedAt?.ToString("dd/MM/yyyy HH:mm"),
                    TargetEmployeeName = s.TargetEmployee != null ? (s.TargetEmployee.Account?.FullName ?? s.TargetEmployee.Username) : null,
                    TargetShiftName = s.TargetRegistration != null ? s.TargetRegistration.Shift?.ShiftName : s.TargetShift?.ShiftName,
                    TargetWorkDateStr = s.TargetRegistration != null ? s.TargetRegistration.WorkDate.ToString("dd/MM/yyyy") : (s.TargetWorkDate?.ToString("dd/MM/yyyy")),
                    SwapSummary = targetDesc
                });
            }

            // Summary counts before tab filter
            ViewBag.PendingCount = unifiedList.Count(x => x.Status == RegistrationStatus.Pending);
            ViewBag.ApprovedCount = unifiedList.Count(x => x.Status == RegistrationStatus.Approved);
            ViewBag.RejectedCount = unifiedList.Count(x => x.Status == RegistrationStatus.Rejected);

            var filtered = unifiedList;
            if (regStatus.HasValue)
            {
                filtered = filtered.Where(x => x.Status == regStatus.Value).ToList();
            }

            if (!string.IsNullOrEmpty(type))
            {
                filtered = filtered.Where(x => x.RequestType == type).ToList();
            }

            ViewBag.StartDate = startDate.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate.ToString("yyyy-MM-dd");
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentType = type;

            if (isAdmin || isStoreManager)
            {
                var empQuery = _context.Employee.Include(e => e.Account).Where(e => e.IsActive);
                if (isStoreManager && !isAdmin && currentEmployee != null)
                {
                    empQuery = empQuery.Where(e => e.StoreId == currentEmployee.StoreId);
                }
                ViewBag.Employees = await empQuery.ToListAsync();
            }

            if (!isAdmin && currentEmployee != null)
            {
                ViewBag.Shifts = await _context.Shift
                    .Where(s => s.IsActive && s.StoreShifts.Any(ss => ss.StoreId == currentEmployee.StoreId && ss.IsActive && ss.Store.IsActive))
                    .OrderBy(s => s.StartTime)
                    .ToListAsync();
            }
            else
            {
                ViewBag.Shifts = await _shiftService.GetAllAsync();
            }

            return View(filtered.OrderByDescending(x => x.CreatedDate).ToList());
        }

        [HttpGet]
        public async Task<IActionResult> GetSwapFormData()
        {
            var username = User.Identity?.Name;
            var emp = await _context.Employee.Include(e => e.Store).FirstOrDefaultAsync(e => e.Username == username);
            if (emp == null) return NotFound(new { success = false, message = "Employee profile not found." });

            var myShifts = await _shiftSwapService.GetEligibleShiftsToSwapAsync(emp.EmployeeId);
            var coworkers = await _shiftSwapService.GetStoreCoworkersAsync(emp.EmployeeId, emp.StoreId);
            var allStoreShifts = await _context.Shift
                .Where(s => s.IsActive && s.StoreShifts.Any(ss => ss.StoreId == emp.StoreId && ss.IsActive))
                .OrderBy(s => s.StartTime)
                .Select(s => new {
                    s.ShiftId,
                    s.ShiftName,
                    time = $"{s.StartTime:hh\\:mm} - {s.EndTime:hh\\:mm}"
                })
                .ToListAsync();

            return Json(new
            {
                success = true,
                myShifts = myShifts.Select(s => new {
                    s.ShiftRegistrationId,
                    shiftName = s.Shift?.ShiftName ?? "",
                    dateStr = s.WorkDate.ToString("dd/MM/yyyy"),
                    workDate = s.WorkDate.ToString("yyyy-MM-dd"),
                    time = s.Shift != null ? $"{s.Shift.StartTime:hh\\:mm} - {s.Shift.EndTime:hh\\:mm}" : ""
                }),
                coworkers = coworkers.Select(c => new {
                    c.EmployeeId,
                    c.EmployeeCode,
                    fullName = c.Account?.FullName ?? c.Username
                }),
                availableShifts = allStoreShifts
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetCoworkerShifts(int coworkerId)
        {
            var shifts = await _shiftSwapService.GetCoworkerShiftsAsync(coworkerId);
            return Json(new
            {
                success = true,
                shifts = shifts.Select(s => new {
                    s.ShiftRegistrationId,
                    shiftName = s.Shift?.ShiftName ?? "",
                    dateStr = s.WorkDate.ToString("dd/MM/yyyy"),
                    workDate = s.WorkDate.ToString("yyyy-MM-dd"),
                    time = s.Shift != null ? $"{s.Shift.StartTime:hh\\:mm} - {s.Shift.EndTime:hh\\:mm}" : ""
                })
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSwap([FromBody] ShiftSwapCreateVM model)
        {
            try
            {
                var username = User.Identity?.Name ?? "system";
                var emp = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
                if (emp != null) model.RequesterEmployeeId = emp.EmployeeId;

                await _shiftSwapService.CreateSwapRequestAsync(model, username);
                return Json(new { success = true, message = RequestResource.Swap_SubmitSuccess });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin,HR,AreaManager,SuperAdmin,StoreManager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, int status, string? reason = null, string requestType = "ShiftReg")
        {
            try
            {
                if (requestType == "ShiftSwap")
                {
                    if (status == 1)
                    {
                        await _shiftSwapService.ApproveSwapAsync(id, User.Identity?.Name ?? "manager");
                        return Json(new { success = true, message = RequestResource.Swap_ApproveSuccess });
                    }
                    else
                    {
                        await _shiftSwapService.RejectSwapAsync(id, reason, User.Identity?.Name ?? "manager");
                        return Json(new { success = true, message = RequestResource.Swap_RejectSuccess });
                    }
                }
                else
                {
                    if (User.IsInRole("StoreManager") && !User.IsInRole("Admin") && !User.IsInRole("SuperAdmin") && !User.IsInRole("AreaManager") && !User.IsInRole("HR"))
                    {
                        var username = User.Identity?.Name;
                        var currentEmployee = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
                        var request = await _shiftRegistrationService.GetByIdAsync(id);
                        if (request == null || currentEmployee == null || request.StoreId != currentEmployee.StoreId)
                        {
                            return Json(new { success = false, message = "Bạn không có quyền duyệt yêu cầu của cửa hàng khác." });
                        }
                    }

                    await _shiftRegistrationService.UpdateStatusAsync(id, (RegistrationStatus)status, reason, User.Identity?.Name);
                    return Json(new { success = true });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRequest(int id, string requestType = "ShiftReg")
        {
            try
            {
                if (requestType == "ShiftSwap")
                {
                    await _shiftSwapService.DeleteSwapAsync(id, User.Identity?.Name ?? "system");
                    return Json(new { success = true, message = RequestResource.Swap_DeleteSuccess });
                }
                else
                {
                    await _shiftRegistrationService.DeleteAsync(id);
                    return Json(new { success = true, message = SharedResource.Req_DeleteSuccess });
                }
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
                    var currentEmployee = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);

                    var requests = await _context.ShiftRegistration.Where(x => model.Ids.Contains(x.ShiftRegistrationId)).ToListAsync();
                    if (currentEmployee == null || requests.Any(x => x.StoreId != currentEmployee.StoreId))
                    {
                        return Json(new { success = false, message = "Bạn không có quyền duyệt yêu cầu của cửa hàng khác." });
                    }
                }

                await _shiftRegistrationService.UpdateBulkStatusAsync(model.Ids, (RegistrationStatus)model.Status, model.Reason, User.Identity?.Name);
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
        public List<int> Ids { get; set; } = new();
        public int Status { get; set; }
        public string? Reason { get; set; }
    }
}
