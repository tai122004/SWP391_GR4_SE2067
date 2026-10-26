using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RWPM.Common.Enums;
using RWPM.Common.Security;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.ShiftDemand;
using RWPM.Services.Abstraction;
using System.Security.Claims;
using R = RWPM.Resources.Shared.ShiftDemandResource;

namespace RWPM.Controllers
{
    /// <summary>
    /// Nhu cầu nhân sự theo ca (cấu hình) và Độ phủ ca.
    /// Admin/HR: mọi chi nhánh. Quản lý cửa hàng: chỉ chi nhánh của mình (phạm vi đọc từ CSDL qua IShiftDemandService.GetScopeAsync).
    /// </summary>
    [Authorize(Roles = AppRoles.Managers)]
    public class ShiftDemandController : Controller
    {
        private readonly IShiftDemandService _demandService;
        private readonly IShiftRegistrationService _registrationService;
        private readonly INotificationService _notificationService;
        private readonly DefaultDatabaseContext _context;
        private readonly ILogger<ShiftDemandController> _logger;

        public ShiftDemandController(
            IShiftDemandService demandService,
            IShiftRegistrationService registrationService,
            INotificationService notificationService,
            DefaultDatabaseContext context,
            ILogger<ShiftDemandController> logger)
        {
            _demandService = demandService;
            _registrationService = registrationService;
            _notificationService = notificationService;
            _context = context;
            _logger = logger;
        }

        private string CurrentUsername => User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? string.Empty;

        private static DateTime StartOfWeek(DateTime date)
        {
            var d = date.Date;
            var diff = ((int)d.DayOfWeek + 6) % 7; // Thứ 2 là ngày đầu tuần
            return d.AddDays(-diff);
        }

        private IActionResult JsonError(string message, int status = 400) =>
            StatusCode(status, new { success = false, message });

        private IActionResult HandleError(Exception ex)
        {
            return ex switch
            {
                UnauthorizedAccessException => JsonError(ex.Message, 403),
                System.ComponentModel.DataAnnotations.ValidationException => JsonError(ex.Message),
                DbUpdateException => LogAndFail(ex),
                _ => JsonError(string.IsNullOrWhiteSpace(ex.Message) ? R.Common_Error : ex.Message)
            };
        }

        private IActionResult LogAndFail(Exception ex)
        {
            _logger.LogError(ex, "ShiftDemand database error");
            return JsonError(R.Common_Error);
        }

        /// <summary>Chọn chi nhánh hiển thị theo phạm vi; null nếu không có chi nhánh nào.</summary>
        private static int? PickStore(DemandScope scope, List<DemandStoreOption> stores, int? requested)
        {
            if (!scope.AllStores) return scope.OwnStoreId;
            if (requested.HasValue && stores.Any(s => s.StoreId == requested.Value)) return requested.Value;
            return stores.FirstOrDefault()?.StoreId;
        }

        // ===================== Cấu hình nhu cầu =====================

        // GET: /ShiftDemand?storeId=
        [HttpGet]
        public async Task<IActionResult> Index(int? storeId = null)
        {
            var scope = await _demandService.GetScopeAsync(CurrentUsername);
            if (!scope.CanAccess) return Forbid();
            if (!scope.AllStores && storeId.HasValue && storeId != scope.OwnStoreId) return Forbid();

            var stores = await _demandService.GetStoresAsync(scope);
            var selected = PickStore(scope, stores, storeId);

            var vm = new ShiftDemandIndexVM
            {
                StoreId = selected,
                StoreName = stores.FirstOrDefault(s => s.StoreId == selected)?.StoreName ?? scope.OwnStoreName,
                IsStoreManager = !scope.AllStores,
                Stores = stores,
                Template = selected.HasValue ? await _demandService.GetWeeklyTemplateAsync(selected.Value) : new WeeklyTemplateVM(),
                Overrides = selected.HasValue ? await _demandService.GetOverridesAsync(selected.Value) : new List<ShiftDemandOverrideItem>(),
                Today = DateTime.Today
            };
            return View(vm);
        }

        // POST: /ShiftDemand/SaveTemplate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveTemplate([FromBody] SaveWeeklyTemplateInput? input)
        {
            try
            {
                if (input == null || input.Cells == null) return JsonError(R.Err_InvalidInput);
                await _demandService.SaveWeeklyTemplateAsync(input.StoreId, input.Cells, CurrentUsername);
                return Json(new { success = true, message = R.Template_Saved });
            }
            catch (Exception ex) { return HandleError(ex); }
        }

        // POST: /ShiftDemand/SaveOverride
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveOverride([FromBody] ShiftDemandOverrideInput? input)
        {
            try
            {
                if (input == null) return JsonError(R.Err_InvalidInput);
                var id = await _demandService.SaveOverrideAsync(input, CurrentUsername);
                return Json(new { success = true, id, message = R.Override_Saved });
            }
            catch (Exception ex) { return HandleError(ex); }
        }

        // POST: /ShiftDemand/DeleteOverride
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOverride(int id)
        {
            try
            {
                await _demandService.DeleteOverrideAsync(id, CurrentUsername);
                return Json(new { success = true, message = R.Override_Deleted });
            }
            catch (Exception ex) { return HandleError(ex); }
        }

        // POST: /ShiftDemand/CopyTemplate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CopyTemplate([FromBody] CopyTemplateInput? input)
        {
            try
            {
                if (input == null) return JsonError(R.Err_InvalidInput);
                var count = await _demandService.CopyTemplateAsync(input.FromStoreId, input.ToStoreId, CurrentUsername);
                return Json(new { success = true, count, message = string.Format(R.Copy_Done, count) });
            }
            catch (Exception ex) { return HandleError(ex); }
        }

        // ===================== Độ phủ ca =====================

        // GET: /ShiftDemand/Coverage?storeId=&weekStart=yyyy-MM-dd
        [HttpGet]
        public async Task<IActionResult> Coverage(int? storeId = null, DateTime? weekStart = null)
        {
            var scope = await _demandService.GetScopeAsync(CurrentUsername);
            if (!scope.CanAccess) return Forbid();
            if (!scope.AllStores && storeId.HasValue && storeId != scope.OwnStoreId) return Forbid();

            var stores = await _demandService.GetStoresAsync(scope);
            var selected = PickStore(scope, stores, storeId);

            var monday = StartOfWeek(weekStart ?? DateTime.Today);
            if (monday.Year < 2000 || monday.Year > 9998) monday = StartOfWeek(DateTime.Today);
            var days = Enumerable.Range(0, 7).Select(i => monday.AddDays(i)).ToList();

            var rows = selected.HasValue
                ? await _demandService.GetCoverageAsync(selected.Value, monday, monday.AddDays(6))
                : new List<ShiftCoverageRow>();

            var shifts = rows.GroupBy(r => r.ShiftId)
                .Select(g => g.First())
                .Select(r => new DemandShiftInfo
                {
                    ShiftId = r.ShiftId,
                    ShiftName = r.ShiftName,
                    StartTime = r.StartTime,
                    EndTime = r.EndTime,
                    EndDayOffset = (byte)(r.EndTime <= r.StartTime ? 1 : 0)
                })
                .OrderBy(s => s.StartTime).ThenBy(s => s.ShiftName)
                .ToList();

            var vm = new CoverageWeekVM
            {
                StoreId = selected,
                StoreName = stores.FirstOrDefault(s => s.StoreId == selected)?.StoreName ?? scope.OwnStoreName,
                IsStoreManager = !scope.AllStores,
                Stores = stores,
                WeekStart = monday,
                Days = days,
                Shifts = shifts,
                Rows = rows,
                Summary = CoverageSummary.From(rows),
                Today = DateTime.Today
            };
            return View(vm);
        }

        // GET: /ShiftDemand/CellDetail?storeId=&shiftId=&date=yyyy-MM-dd
        [HttpGet]
        public async Task<IActionResult> CellDetail(int storeId, int shiftId, DateTime? date)
        {
            try
            {
                if (!date.HasValue) return JsonError(R.Err_DateRequired);
                var detail = await _demandService.GetCellDetailAsync(storeId, shiftId, date.Value, CurrentUsername);
                return Json(new { success = true, data = detail });
            }
            catch (Exception ex) { return HandleError(ex); }
        }

        // GET: /ShiftDemand/CoverageJson?storeId=&from=yyyy-MM-dd&to=yyyy-MM-dd
        /// <summary>
        /// Danh sách ShiftCoverageRow cho màn hình khác. Admin/HR: storeId tùy chọn (bỏ trống = mọi chi nhánh);
        /// Quản lý cửa hàng: luôn là chi nhánh của mình. Tối đa 93 ngày.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CoverageJson(int? storeId, DateTime? from, DateTime? to)
        {
            try
            {
                var scope = await _demandService.GetScopeAsync(CurrentUsername);
                if (!scope.CanAccess) return JsonError(R.Err_NoStoreAccess, 403);
                if (!scope.AllStores)
                {
                    if (storeId.HasValue && storeId != scope.OwnStoreId) return JsonError(R.Err_NoStoreAccess, 403);
                    storeId = scope.OwnStoreId;
                }
                var start = (from ?? DateTime.Today).Date;
                var end = (to ?? start).Date;
                var rows = await _demandService.GetCoverageAsync(storeId, start, end);
                return Json(rows);
            }
            catch (Exception ex) { return HandleError(ex); }
        }

        // POST: /ShiftDemand/Assign
        /// <summary>
        /// Phân công nhanh: tạo đăng ký qua IShiftRegistrationService.CreateAsync rồi duyệt ngay qua UpdateStatusAsync
        /// trong cùng một giao dịch (mọi kiểm tra sẵn có: kỳ lương đã chốt, trùng ca, nghỉ phép, phạm vi... vẫn áp dụng).
        /// Nếu vượt nhu cầu và chưa xác nhận -> trả needConfirm + warnings (không chặn).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign([FromBody] CoverageAssignInput? input)
        {
            try
            {
                if (input == null || !input.Date.HasValue) return JsonError(R.Err_InvalidInput);
                var actor = CurrentUsername;
                var scope = await _demandService.GetScopeAsync(actor);
                if (!scope.CanAccessStore(input.StoreId)) return JsonError(R.Err_NoStoreAccess, 403);

                var date = input.Date.Value.Date;
                if (date < DateTime.Today) return JsonError(R.Err_PastDateAssign);
                if (scope.OwnEmployeeId == input.EmployeeId) return JsonError(R.Reason_Self);

                var employee = await _context.Employee.AsNoTracking().Include(e => e.Account)
                    .FirstOrDefaultAsync(e => e.EmployeeId == input.EmployeeId && e.IsActive);
                if (employee == null || employee.StoreId != input.StoreId) return JsonError(R.Err_EmployeeNotInStore);

                var coverage = await _demandService.GetCoverageForAsync(input.StoreId, input.ShiftId, date);
                if (coverage == null) return JsonError(R.Err_ShiftNotApplicable);

                if (!input.ConfirmOver)
                {
                    // Cảnh báo cả thời gian có thể làm việc (ngoài khung giờ / ngày không thể làm) lẫn vượt nhu cầu
                    var availability = await _registrationService.GetAssignmentWarningsAsync(employee.EmployeeId, new[] { (input.ShiftId, date) });
                    var warnings = availability.Availability
                        .Concat(await _demandService.GetOverDemandWarningsAsync(new[] { (input.StoreId, input.ShiftId, date, 1) }))
                        .ToList();
                    if (warnings.Count > 0) return Json(new { success = false, needConfirm = true, warnings });
                }

                int registrationId;
                await using (var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable))
                {
                    var created = await _registrationService.CreateAsync(new ShiftRegistration
                    {
                        EmployeeId = employee.EmployeeId,
                        ShiftId = input.ShiftId,
                        StoreId = input.StoreId,
                        WorkDate = date,
                        Status = RegistrationStatus.Pending,
                        Note = R.Assign_Note,
                        CreatedDate = DateTime.Now,
                        CreatedBy = actor
                    });
                    registrationId = created.ShiftRegistrationId;
                    await _registrationService.UpdateStatusAsync(registrationId, RegistrationStatus.Approved, null, actor);
                    await transaction.CommitAsync();
                }

                try
                {
                    await _notificationService.NotifyRequestDecisionAsync(NotificationRequestTypes.ShiftReg, new[] { registrationId }, actor);
                }
                catch (Exception notifyError)
                {
                    _logger.LogWarning(notifyError, "Could not notify employee about assigned shift registration {Id}", registrationId);
                }

                var updated = await _demandService.GetCoverageForAsync(input.StoreId, input.ShiftId, date);
                var name = string.IsNullOrWhiteSpace(employee.Account?.FullName) ? employee.EmployeeCode : employee.Account.FullName;
                return Json(new { success = true, id = registrationId, coverage = updated, message = string.Format(R.Assign_Success, name) });
            }
            catch (Exception ex)
            {
                _context.ChangeTracker.Clear();
                return HandleError(ex);
            }
        }
    }
}
