using RWPM.Common.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RWPM.Common.Enums;
using RWPM.Models.Entities;
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
        private readonly ILeaveRequestService _leaveRequestService;
        private readonly IAttendanceAdjustmentService _attendanceAdjustmentService;
        private readonly RWPM.Infrastructure.Data.DefaultDatabaseContext _context;
        private readonly IShiftService _shiftService;

        public RequestController(
            IShiftRegistrationService shiftRegistrationService,
            IShiftSwapService shiftSwapService,
            ILeaveRequestService leaveRequestService,
            IAttendanceAdjustmentService attendanceAdjustmentService,
            RWPM.Infrastructure.Data.DefaultDatabaseContext context,
            IShiftService shiftService)
        {
            _shiftRegistrationService = shiftRegistrationService;
            _shiftSwapService = shiftSwapService;
            _leaveRequestService = leaveRequestService;
            _attendanceAdjustmentService = attendanceAdjustmentService;
            _context = context;
            _shiftService = shiftService;
        }

        public async Task<IActionResult> Index(string start, string end, int? status, string? type = null, string? q = null, int pageNumber = 1)
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

            bool isSuperOrAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
            bool isAdmin = isSuperOrAdmin || User.IsInRole("AreaManager") || User.IsInRole("HR");
            bool isStoreManager = User.IsInRole("StoreManager");
            ViewBag.IsManager = isAdmin || isStoreManager;
            ViewBag.IsAdminOnly = isSuperOrAdmin;

            var username = User.Identity?.Name;
            var currentEmployee = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
            ViewBag.CurrentUserStoreId = currentEmployee?.StoreId ?? 0;

            // 1. Fetch Shift Registrations
            var allRegistrations = await _shiftRegistrationService.GetRequestsAsync(startDate, endDate, null);

            // 2. Fetch Shift Swap Requests
            int? storeFilter = null;
            if (isStoreManager && !isAdmin && currentEmployee != null)
            {
                storeFilter = currentEmployee.StoreId;
            }
            var allSwaps = await _shiftSwapService.GetSwapRequestsAsync(startDate, endDate, storeFilter, null);

            // 3. Fetch Leave Requests
            var allLeaves = await _leaveRequestService.GetLeaveRequestsAsync(startDate, endDate, storeFilter, null);

            // 4. Fetch Attendance Adjustment Requests
            var allAdjustments = await _attendanceAdjustmentService.GetRequestsAsync(startDate, endDate, storeFilter, null);

            // Filter for regular employees (only see own requests)
            if (!isAdmin && !isStoreManager)
            {
                if (!string.IsNullOrEmpty(username))
                {
                    allRegistrations = allRegistrations.Where(x => x.Employee?.Username == username).ToList();
                    allSwaps = allSwaps.Where(x => x.RequesterEmployee?.Username == username).ToList();
                    allLeaves = allLeaves.Where(x => x.Employee?.Username == username).ToList();
                    allAdjustments = allAdjustments.Where(x => x.Employee?.Username == username).ToList();
                }
            }
            else if (isStoreManager && !isAdmin && currentEmployee != null)
            {
                allRegistrations = allRegistrations.Where(x => x.StoreId == currentEmployee.StoreId).ToList();
                allLeaves = allLeaves.Where(x => x.StoreId == currentEmployee.StoreId).ToList();
                allAdjustments = allAdjustments.Where(x => x.StoreId == currentEmployee.StoreId).ToList();
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
                    ApprovedAtStr = r.ApprovedAt?.ToString("dd/MM/yyyy HH:mm"),
                    RequesterUsername = r.Employee?.Username,
                    RequesterRole = r.Employee?.Account?.Role
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
                    SwapSummary = targetDesc,
                    RequesterUsername = s.RequesterEmployee?.Username,
                    RequesterRole = s.RequesterEmployee?.Account?.Role
                });
            }

            foreach (var l in allLeaves)
            {
                int days = (l.EndDate.Date - l.StartDate.Date).Days + 1;
                string typeText = l.LeaveType == LeaveType.Paid ? RequestResource.Leave_Paid : RequestResource.Leave_Unpaid;
                string dateRangeText = l.StartDate == l.EndDate
                    ? l.StartDate.ToString("dd/MM/yyyy")
                    : $"{l.StartDate:dd/MM/yyyy} ~ {l.EndDate:dd/MM/yyyy}";

                unifiedList.Add(new RequestItemVM
                {
                    Id = l.LeaveRequestId,
                    RequestType = "Leave",
                    RequestTypeName = RequestResource.LeaveRequest,
                    Code = "DXN" + l.CreatedDate.ToString("yyMMdd") + "-" + l.LeaveRequestId.ToString("D3"),
                    EmployeeId = l.EmployeeId,
                    EmployeeName = l.Employee?.Account?.FullName ?? l.Employee?.Username ?? "N/A",
                    EmployeeCode = l.Employee?.EmployeeCode ?? "",
                    StoreId = l.StoreId,
                    StoreName = l.Store?.StoreName ?? "",
                    CreatedDate = l.CreatedDate,
                    CreatedDateStr = l.CreatedDate.ToString("dd/MM/yyyy HH:mm"),
                    Status = l.Status,
                    ShiftName = $"{typeText} ({days} {RequestResource.Leave_DaysUnit})",
                    WorkDateStr = dateRangeText,
                    Note = l.Reason,
                    RejectionReason = l.RejectionReason,
                    ApprovedBy = l.ApprovedBy,
                    ApprovedAtStr = l.ApprovedAt?.ToString("dd/MM/yyyy HH:mm"),
                    StartDateStr = l.StartDate.ToString("dd/MM/yyyy"),
                    EndDateStr = l.EndDate.ToString("dd/MM/yyyy"),
                    LeaveType = l.LeaveType,
                    LeaveTypeName = typeText,
                    LeaveDays = days,
                    AnnualLeaveBalance = l.Employee?.AnnualLeaveBalance ?? 0,
                    RequesterUsername = l.Employee?.Username,
                    RequesterRole = l.Employee?.Account?.Role
                });
            }

            foreach (var a in allAdjustments)
            {
                string inOutDesc = string.Format(RequestResource.Adj_InOutSummary,
                    a.RequestedCheckInTime.HasValue ? a.RequestedCheckInTime.Value.ToString(@"hh\:mm") : "-",
                    a.RequestedCheckOutTime.HasValue ? a.RequestedCheckOutTime.Value.ToString(@"hh\:mm") : "-");
                unifiedList.Add(new RequestItemVM
                {
                    Id = a.AttendanceAdjustmentRequestId,
                    RequestType = "Attendance",
                    RequestTypeName = RequestResource.AttendanceAdjustment,
                    Code = "YCDC" + a.CreatedDate.ToString("yyMMdd") + "-" + a.AttendanceAdjustmentRequestId.ToString("D3"),
                    EmployeeId = a.EmployeeId,
                    EmployeeName = a.Employee?.Account?.FullName ?? a.Employee?.Username ?? "N/A",
                    EmployeeCode = a.Employee?.EmployeeCode ?? "",
                    StoreId = a.StoreId,
                    StoreName = a.Store?.StoreName ?? "",
                    CreatedDate = a.CreatedDate,
                    CreatedDateStr = a.CreatedDate.ToString("dd/MM/yyyy HH:mm"),
                    Status = a.Status,
                    ShiftName = a.Shift?.ShiftName ?? RequestResource.Adj_DefaultShiftName,
                    WorkDateStr = a.WorkDate.ToString("dd/MM/yyyy"),
                    Note = a.Reason,
                    // Default rejection text is stored in Vietnamese; translate it at display time
                    RejectionReason = a.RejectionReason == RWPM.Services.Implementation.AttendanceAdjustmentService.DefaultRejectionReason
                        ? RequestResource.Adj_DefaultRejectReason
                        : a.RejectionReason,
                    ApprovedBy = a.ApprovedBy,
                    ApprovedAtStr = a.ApprovedAt?.ToString("dd/MM/yyyy HH:mm"),
                    ActualCheckInTimeStr = a.CurrentCheckInTime?.ToString(@"hh\:mm") ?? "-",
                    ActualCheckOutTimeStr = a.CurrentCheckOutTime?.ToString(@"hh\:mm") ?? "-",
                    RequestedCheckInTimeStr = a.RequestedCheckInTime?.ToString(@"hh\:mm") ?? "-",
                    RequestedCheckOutTimeStr = a.RequestedCheckOutTime?.ToString(@"hh\:mm") ?? "-",
                    AdjustmentSummary = inOutDesc,
                    RequesterUsername = a.Employee?.Username,
                    RequesterRole = a.Employee?.Account?.Role
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

            // Tìm kiếm phía server (mã đơn, nhân viên, ca làm, chi nhánh...) để tìm được trên tất cả các trang
            if (!string.IsNullOrWhiteSpace(q))
            {
                var keyword = q.Trim();
                bool Has(string? value) => !string.IsNullOrEmpty(value) && value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
                filtered = filtered.Where(x => Has(x.Code) || Has(x.EmployeeName) || Has(x.EmployeeCode) || Has(x.ShiftName)
                                            || Has(x.StoreName) || Has(x.RequestTypeName) || Has(x.WorkDateStr) || Has(x.Note)
                                            || Has(x.SwapSummary)).ToList();
            }

            ViewBag.StartDate = startDate.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate.ToString("yyyy-MM-dd");
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentType = type;
            ViewBag.SearchQuery = q;

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

            // Phân trang (tái sử dụng PaginationRes + _PaginationPartial như các màn hình danh sách khác)
            var ordered = filtered
                .OrderByDescending(x => x.Status == RegistrationStatus.Pending)
                .ThenByDescending(x => x.CreatedDate)
                .ThenByDescending(x => x.Id)
                .ToList();
            var paging = new RWPM.Common.Models.PagedReq { PageNumber = pageNumber };
            int totalPages = Math.Max(1, (int)Math.Ceiling(ordered.Count / (double)paging.PageSize));
            paging.PageNumber = Math.Clamp(paging.PageNumber, 1, totalPages);
            var pageItems = ordered.Skip((paging.PageNumber - 1) * paging.PageSize).Take(paging.PageSize).ToList();

            ViewBag.Pagination = new RWPM.Common.Models.PaginationRes<RequestItemVM>(pageItems, paging.PageNumber, paging.PageSize, ordered.Count)
                .ToPaginationRes(new Dictionary<string, string?>
                {
                    ["start"] = ViewBag.StartDate,
                    ["end"] = ViewBag.EndDate,
                    ["status"] = status?.ToString(),
                    ["type"] = type,
                    ["q"] = q
                });
            ViewBag.TotalFiltered = ordered.Count;
            ViewBag.ExportItems = ordered; // Xuất file vẫn lấy toàn bộ danh sách đã lọc, không chỉ trang hiện tại

            return View(pageItems);
        }

        [HttpGet]
        public async Task<IActionResult> GetSwapFormData()
        {
            var username = User.Identity?.Name;
            var emp = await _context.Employee.Include(e => e.Store).FirstOrDefaultAsync(e => e.Username == username);
            if (emp == null) return NotFound(new { success = false, message = RequestResource.Err_EmployeeProfileNotFound });

            var myShifts = await _shiftSwapService.GetEligibleShiftsToSwapAsync(emp.EmployeeId);
            var coworkers = await _shiftSwapService.GetStoreCoworkersAsync(emp.EmployeeId, emp.StoreId ?? 0);
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
            bool isChainAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin") || User.IsInRole("AreaManager") || User.IsInRole("HR");
            if (!isChainAdmin)
            {
                var username = User.Identity?.Name;
                var emp = await _context.Employee.AsNoTracking().FirstOrDefaultAsync(e => e.Username == username);
                var coworker = await _context.Employee.AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeId == coworkerId);
                if (emp == null || coworker == null || !emp.StoreId.HasValue || coworker.StoreId != emp.StoreId)
                {
                    return Json(new { success = false, message = InputRules.Text("Bạn không có quyền xem lịch ca của nhân viên này.", "You cannot view this employee's shifts.") });
                }
            }

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
            if (model == null || !ModelState.IsValid)
                return Json(new { success = false, message = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).DefaultIfEmpty(InputRules.Text("Dữ liệu không hợp lệ.", "Invalid input."))) });
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

        [HttpGet]
        public async Task<IActionResult> GetLeaveFormData(int? employeeId = null)
        {
            var username = User.Identity?.Name;
            bool isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin") || User.IsInRole("AreaManager") || User.IsInRole("HR");
            bool isStoreManager = User.IsInRole("StoreManager");

            RWPM.Models.Entities.Employee? targetEmployee = null;
            if ((isAdmin || isStoreManager) && employeeId.HasValue && employeeId.Value > 0)
            {
                targetEmployee = await _context.Employee.Include(e => e.Account).FirstOrDefaultAsync(e => e.EmployeeId == employeeId.Value);
                if (targetEmployee != null && isStoreManager && !isAdmin)
                {
                    var currentStoreId = await _context.Employee.Where(e => e.Username == username).Select(e => e.StoreId).FirstOrDefaultAsync();
                    if (!currentStoreId.HasValue || targetEmployee.StoreId != currentStoreId)
                    {
                        return NotFound(new { success = false, message = RequestResource.Err_EmployeeProfileNotFound });
                    }
                }
            }
            else
            {
                targetEmployee = await _context.Employee.Include(e => e.Account).FirstOrDefaultAsync(e => e.Username == username);
            }

            if (targetEmployee == null)
            {
                return NotFound(new { success = false, message = RequestResource.Err_EmployeeProfileNotFound });
            }

            return Json(new
            {
                success = true,
                employeeId = targetEmployee.EmployeeId,
                employeeCode = targetEmployee.EmployeeCode,
                fullName = targetEmployee.Account?.FullName ?? targetEmployee.Username,
                annualLeaveBalance = targetEmployee.AnnualLeaveBalance
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLeave([FromBody] LeaveRequestCreateVM model)
        {
            if (model == null || !ModelState.IsValid)
                return Json(new { success = false, message = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).DefaultIfEmpty(InputRules.Text("Dữ liệu không hợp lệ.", "Invalid input."))) });
            try
            {
                var username = User.Identity?.Name ?? "system";
                bool isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin") || User.IsInRole("AreaManager") || User.IsInRole("HR");
                bool isStoreManager = User.IsInRole("StoreManager");

                if (!isAdmin && !isStoreManager)
                {
                    var emp = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
                    if (emp == null) return Unauthorized(new { success = false, message = RequestResource.Err_EmployeeNotFound });
                    model.EmployeeId = emp.EmployeeId;
                }
                else if (model.EmployeeId <= 0)
                {
                    var emp = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
                    if (emp != null) model.EmployeeId = emp.EmployeeId;
                }

                await _leaveRequestService.CreateLeaveRequestAsync(model, username);
                return Json(new { success = true, message = RequestResource.Leave_SubmitSuccess });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin,HR,AreaManager,SuperAdmin,StoreManager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(
            int id, 
            int status, 
            string? reason = null, 
            string requestType = "ShiftReg",
            string? approvedCheckInTime = null,
            string? approvedCheckOutTime = null,
            string? approverNote = null)
        {
            try
            {
                if (requestType != "Attendance")
                {
                    InputRules.Require(requestType is "ShiftSwap" or "Leave" or "ShiftReg", "RequestType", "Loại yêu cầu không hợp lệ.", "Invalid request type.");
                    InputRules.Decision((RegistrationStatus)status);
                }
                if (requestType == "ShiftSwap")
                {
                    if (User.IsInRole("StoreManager") && !User.IsInRole("Admin") && !User.IsInRole("SuperAdmin") && !User.IsInRole("AreaManager") && !User.IsInRole("HR"))
                    {
                        var username = User.Identity?.Name;
                        var currentEmployee = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
                        var swapReq = await _shiftSwapService.GetByIdAsync(id);
                        if (swapReq == null || currentEmployee == null || swapReq.StoreId != currentEmployee.StoreId)
                        {
                            return Json(new { success = false, message = RequestResource.Err_NoPermissionApproveSwapOtherStore });
                        }
                    }

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
                else if (requestType == "Leave")
                {
                    if (User.IsInRole("StoreManager") && !User.IsInRole("Admin") && !User.IsInRole("SuperAdmin") && !User.IsInRole("AreaManager") && !User.IsInRole("HR"))
                    {
                        var username = User.Identity?.Name;
                        var currentEmployee = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
                        var request = await _leaveRequestService.GetByIdAsync(id);
                        if (request == null || currentEmployee == null || request.StoreId != currentEmployee.StoreId)
                        {
                            return Json(new { success = false, message = RequestResource.Err_NoPermissionApproveOtherStore });
                        }
                    }

                    if (status == 1)
                    {
                        await _leaveRequestService.ApproveLeaveRequestAsync(id, User.Identity?.Name ?? "manager");
                        return Json(new { success = true, message = RequestResource.Leave_ApproveSuccess });
                    }
                    else
                    {
                        await _leaveRequestService.RejectLeaveRequestAsync(id, reason, User.Identity?.Name ?? "manager");
                        return Json(new { success = true, message = RequestResource.Leave_RejectSuccess });
                    }
                }
                else if (requestType == "Attendance")
                {
                    InputRules.Decision((RegistrationStatus)status);
                    var request = await _attendanceAdjustmentService.GetByIdAsync(id);
                    if (request == null)
                    {
                        return Json(new { success = false, message = RequestResource.Adj_ErrNotFound });
                    }

                    var currentUsername = User.Identity?.Name;
                    var requesterUsername = request.Employee?.Username;
                    var requesterRole = request.Employee?.Account?.Role;

                    // 1. Chống tự duyệt: Không ai được tự phê duyệt đơn của chính mình
                    if (!string.IsNullOrEmpty(currentUsername) && currentUsername == requesterUsername)
                    {
                        return Json(new { success = false, message = RequestResource.Adj_ErrSelfApprove });
                    }

                    // 2. Nếu người làm đơn là Quản lý cửa hàng hoặc HR: CHỈ DUY NHẤT Admin / SuperAdmin mới có quyền duyệt
                    bool isRequesterManagement = requesterRole == AccountRole.StoreManager || requesterRole == AccountRole.HR;
                    if (isRequesterManagement)
                    {
                        if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
                        {
                            return Json(new { success = false, message = RequestResource.Adj_ErrAdminOnly });
                        }
                    }

                    // 3. Nếu người làm đơn là Nhân viên:
                    // Quản lý cửa hàng chỉ được duyệt cho nhân viên thuộc cùng cửa hàng mình
                    if (User.IsInRole("StoreManager") && !User.IsInRole("Admin") && !User.IsInRole("SuperAdmin") && !User.IsInRole("HR"))
                    {
                        var currentEmployee = await _context.Employee.FirstOrDefaultAsync(e => e.Username == currentUsername);
                        if (currentEmployee == null || request.StoreId != currentEmployee.StoreId)
                        {
                            return Json(new { success = false, message = RequestResource.Err_NoPermissionApproveOtherStoreEmployee });
                        }
                    }

                    if (status == 1)
                    {
                        var parsedIn = AttendanceInputRules.ParseClockTime(approvedCheckInTime, "ApprovedCheckInTime");
                        var parsedOut = AttendanceInputRules.ParseClockTime(approvedCheckOutTime, "ApprovedCheckOutTime");

                        await _attendanceAdjustmentService.ApproveAsync(id, currentUsername ?? "manager", parsedIn, parsedOut, approverNote);
                        return Json(new { success = true, message = RequestResource.Adj_ApproveSuccess });
                    }
                    else
                    {
                        await _attendanceAdjustmentService.RejectAsync(id, reason ?? "", currentUsername ?? "manager");
                        return Json(new { success = true, message = RequestResource.Adj_RejectSuccess });
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
                            return Json(new { success = false, message = RequestResource.Err_NoPermissionApproveOtherStore });
                        }
                    }

                    await _shiftRegistrationService.UpdateStatusAsync(id, (RegistrationStatus)status, reason, User.Identity?.Name);
                    return Json(new { success = true });
                }
            }
            catch (Exception ex)
            {
                var message = ex.Message;
                if (ex.InnerException != null && !string.IsNullOrWhiteSpace(ex.InnerException.Message))
                {
                    if (ex is DbUpdateException)
                    {
                        message = string.Format(RequestResource.Err_Database, ex.InnerException.Message);
                    }
                }
                return Json(new { success = false, message = message });
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
                    if (User.IsInRole("StoreManager") && !User.IsInRole("Admin") && !User.IsInRole("SuperAdmin") && !User.IsInRole("AreaManager") && !User.IsInRole("HR"))
                    {
                        var username = User.Identity?.Name;
                        var currentEmployee = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
                        var swapReq = await _shiftSwapService.GetByIdAsync(id);
                        if (swapReq == null || currentEmployee == null || swapReq.StoreId != currentEmployee.StoreId)
                        {
                            return Json(new { success = false, message = RequestResource.Err_NoPermissionDeleteSwapOtherStore });
                        }
                    }

                    await _shiftSwapService.DeleteSwapAsync(id, User.Identity?.Name ?? "system");
                    return Json(new { success = true, message = RequestResource.Swap_DeleteSuccess });
                }
                else if (requestType == "Leave")
                {
                    await _leaveRequestService.DeleteLeaveRequestAsync(id, User.Identity?.Name ?? "system");
                    return Json(new { success = true, message = RequestResource.Leave_DeleteSuccess });
                }
                else if (requestType == "Attendance")
                {
                    await _attendanceAdjustmentService.DeleteAsync(id, User.Identity?.Name ?? "system");
                    return Json(new { success = true, message = RequestResource.Adj_DeleteSuccess });
                }
                else
                {
                    InputRules.Require(requestType == "ShiftReg", "RequestType", "Loại yêu cầu không hợp lệ.", "Invalid request type.");
                    var entity = await _shiftRegistrationService.GetByIdAsync(id);
                    if (entity == null)
                    {
                        return Json(new { success = false, message = InputRules.Text("Không tìm thấy đăng ký ca.", "Registration not found.") });
                    }

                    var username = User.Identity?.Name;
                    var isChainAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin") || User.IsInRole("AreaManager") || User.IsInRole("HR");
                    var isStoreManager = User.IsInRole("StoreManager");
                    var noPermission = InputRules.Text("Bạn không có quyền xóa đăng ký ca này.", "You do not have permission to delete this registration.");

                    if (!isChainAdmin && !isStoreManager)
                    {
                        var employee = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
                        if (employee == null || entity.EmployeeId != employee.EmployeeId)
                        {
                            return Json(new { success = false, message = noPermission });
                        }
                        if (entity.Status == RegistrationStatus.Approved)
                        {
                            return Json(new { success = false, message = ShiftRegistrationResource.Err_ApprovedCannotCancel });
                        }
                        if (entity.WorkDate.Date < DateTime.Today)
                        {
                            return Json(new { success = false, message = ShiftRegistrationResource.Err_CannotCancelPast });
                        }
                    }
                    else if (!isChainAdmin)
                    {
                        var currentEmployee = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
                        if (currentEmployee == null || entity.StoreId != currentEmployee.StoreId)
                        {
                            return Json(new { success = false, message = noPermission });
                        }
                    }

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
                    return Json(new { success = false, message = RequestResource.Err_NoRequestSelected });
                }

                if (model.RequestType is "ShiftSwap" or "Leave")
                {
                    InputRules.Decision((RegistrationStatus)model.Status);
                    InputRules.Require(model.Ids.All(x => x > 0) && model.Ids.Distinct().Count() == model.Ids.Count,
                        "Ids", "Danh sách đơn không hợp lệ hoặc bị trùng.", "Invalid or duplicate request IDs.");
                    await using var batch = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                    var username = User.Identity?.Name ?? "";
                    // Preflight every ID and permission before mutating any request.
                    foreach (var id in model.Ids)
                    {
                        if (model.RequestType == "ShiftSwap")
                        {
                            var req = await _shiftSwapService.GetByIdAsync(id) ?? throw new InvalidOperationException(InputRules.Text("Không tìm thấy đơn đổi ca.", "Swap request not found."));
                            await WorkflowGuard.EmployeeAsync(_context, username, req.RequesterEmployeeId, true);
                            InputRules.Require(req.Status == RegistrationStatus.Pending, "Status", "Đơn đã được xử lý.", "Request already processed.");
                        }
                        else
                        {
                            var req = await _leaveRequestService.GetByIdAsync(id) ?? throw new InvalidOperationException(RequestResource.Leave_ErrNotFound);
                            await WorkflowGuard.EmployeeAsync(_context, username, req.EmployeeId, true);
                            InputRules.Require(req.Status == RegistrationStatus.Pending, "Status", "Đơn đã được xử lý.", "Request already processed.");
                        }
                    }
                    foreach (var id in model.Ids)
                    {
                        if (model.RequestType == "ShiftSwap")
                        {
                            if (model.Status == 1) await _shiftSwapService.ApproveSwapAsync(id, username);
                            else await _shiftSwapService.RejectSwapAsync(id, model.Reason, username);
                        }
                        else
                        {
                            if (model.Status == 1) await _leaveRequestService.ApproveLeaveRequestAsync(id, username);
                            else await _leaveRequestService.RejectLeaveRequestAsync(id, model.Reason, username);
                        }
                    }
                    await batch.CommitAsync();
                    return Json(new { success = true });
                }

                if (model.RequestType == "Attendance")
                {
                    InputRules.Decision((RegistrationStatus)model.Status);
                    InputRules.Require(model.Ids.All(x => x > 0) && model.Ids.Distinct().Count() == model.Ids.Count,
                        "Ids", "Danh sách đơn không hợp lệ hoặc bị trùng.", "Invalid or duplicate request IDs.");
                    var currentUsername = User.Identity?.Name;
                    var isSuperOrAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
                    var isHR = User.IsInRole("HR");
                    var currentEmployee = await _context.Employee.FirstOrDefaultAsync(e => e.Username == currentUsername);
                    InputRules.Decision((RegistrationStatus)model.Status);
                    InputRules.Require(model.Ids.All(x => x > 0) && model.Ids.Distinct().Count() == model.Ids.Count,
                        "Ids", "Danh sách đơn không hợp lệ hoặc bị trùng.", "Invalid or duplicate request IDs.");
                    await using var adjBatch = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                    var requests = new List<AttendanceAdjustmentRequest>();

                    // Vòng 1: kiểm tra toàn bộ đơn và quyền trước khi ghi dữ liệu
                    foreach (var adjId in model.Ids)
                    {
                        var request = await _attendanceAdjustmentService.GetByIdAsync(adjId);
                        if (request == null)
                        {
                            return Json(new { success = false, message = RequestResource.Adj_ErrNotFound });
                        }
                        if (request.Status != RegistrationStatus.Pending)
                        {
                            return Json(new { success = false, message = RequestResource.Adj_ErrAlreadyProcessed });
                        }

                        var requesterUsername = request.Employee?.Username;
                        var requesterRole = request.Employee?.Account?.Role;

                        // Chống tự duyệt
                        if (currentUsername == requesterUsername)
                        {
                            return Json(new { success = false, message = RequestResource.Err_SelfApprove });
                        }

                        // Đơn của StoreManager hoặc HR: Chỉ Admin duyệt
                        if (requesterRole == AccountRole.StoreManager || requesterRole == AccountRole.HR)
                        {
                            if (!isSuperOrAdmin)
                            {
                                return Json(new { success = false, message = RequestResource.Adj_ErrAdminOnly });
                            }
                        }

                        // StoreManager chỉ duyệt đơn trong cửa hàng mình
                        if (User.IsInRole("StoreManager") && !isSuperOrAdmin && !isHR)
                        {
                            if (currentEmployee == null || request.StoreId != currentEmployee.StoreId)
                            {
                                return Json(new { success = false, message = RequestResource.Err_NoPermissionApproveOtherStoreEmployee });
                            }
                        }
                        requests.Add(request);
                    }

                    // Vòng 2: duyệt/từ chối trong cùng một transaction
                    foreach (var request in requests)
                    {
                        if (model.Status == 1)
                            await AttendanceInputRules.EnsureUnlockedAsync(_context, request.WorkDate);
                    }
                    foreach (var request in requests)
                    {
                        if (model.Status == 1)
                            await _attendanceAdjustmentService.ApproveAsync(request.AttendanceAdjustmentRequestId, currentUsername ?? "manager");
                        else
                            await _attendanceAdjustmentService.RejectAsync(request.AttendanceAdjustmentRequestId, model.Reason ?? "", currentUsername ?? "manager");
                    }
                    await adjBatch.CommitAsync();
                    return Json(new { success = true });
                }

                if (User.IsInRole("StoreManager") && !User.IsInRole("Admin") && !User.IsInRole("SuperAdmin") && !User.IsInRole("AreaManager") && !User.IsInRole("HR"))
                {
                    var username = User.Identity?.Name;
                    var currentEmployee = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);

                    var requests = await _context.ShiftRegistration.Where(x => model.Ids.Contains(x.ShiftRegistrationId)).ToListAsync();
                    if (currentEmployee == null || requests.Any(x => x.StoreId != currentEmployee.StoreId))
                    {
                        return Json(new { success = false, message = RequestResource.Err_NoPermissionApproveOtherStore });
                    }
                }

                InputRules.Require(model.RequestType == "ShiftReg", "RequestType", "Loại yêu cầu không hợp lệ.", "Invalid request type.");
                InputRules.Decision((RegistrationStatus)model.Status);
                await _shiftRegistrationService.UpdateBulkStatusAsync(model.Ids, (RegistrationStatus)model.Status, model.Reason, User.Identity?.Name);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                var message = ex.Message;
                if (ex.InnerException != null && !string.IsNullOrWhiteSpace(ex.InnerException.Message))
                {
                    if (ex is DbUpdateException)
                    {
                        message = string.Format(RequestResource.Err_Database, ex.InnerException.Message);
                    }
                }
                return Json(new { success = false, message = message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAttendanceAdjustmentFormData()
        {
            var username = User.Identity?.Name;
            var emp = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
            if (emp == null) return NotFound(new { success = false, message = RequestResource.Err_EmployeeProfileNotFound });

            var recentDays = DateTime.Today.AddDays(-30);
            var records = await _context.AttendanceRecord
                .Include(r => r.Shift)
                .Where(r => r.Username == username && r.Date >= recentDays)
                .OrderByDescending(r => r.Date)
                .ThenByDescending(r => r.CheckInTime)
                .ThenByDescending(r => r.AttendanceId)
                .ToListAsync();

            return Json(new
            {
                success = true,
                records = records.Select(r => new
                {
                    attendanceId = r.AttendanceId,
                    dateStr = r.Date.ToString("dd/MM/yyyy"),
                    workDate = r.Date.ToString("yyyy-MM-dd"),
                    shiftName = r.Shift?.ShiftName ?? RequestResource.Adj_DefaultShiftName,
                    shiftId = r.ShiftId,
                    checkIn = r.CheckInTime.HasValue ? r.CheckInTime.Value.ToString(@"hh\:mm") : "",
                    checkOut = r.CheckOutTime.HasValue ? r.CheckOutTime.Value.ToString(@"hh\:mm") : ""
                })
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAttendanceAdjustment([FromBody] AttendanceAdjustmentCreateVM? model)
        {
            try
            {
                if (model == null)
                {
                    return Json(new { success = false, message = RequestResource.Err_InvalidRequestData });
                }
                if (!ModelState.IsValid)
                {
                    return Json(new { success = false, message = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).DefaultIfEmpty(InputRules.Text("Dữ liệu không hợp lệ.", "Invalid input."))) });
                }

                var username = User.Identity?.Name ?? "system";
                await _attendanceAdjustmentService.CreateRequestAsync(model, username);
                return Json(new { success = true, message = RequestResource.Adj_SubmitSuccess });
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
        public string RequestType { get; set; } = "ShiftReg";
    }
}
