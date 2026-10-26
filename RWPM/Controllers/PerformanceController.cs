using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RWPM.Common;
using RWPM.Common.Enums;
using RWPM.Common.Models;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.Performance;
using RWPM.Services.Abstraction;
using System.Security.Claims;
using R = RWPM.Resources.Shared.PerformanceResource;

namespace RWPM.Controllers
{
    /// <summary>
    /// Đánh giá hiệu suất theo tháng.
    /// Admin/HR: xem & đánh giá mọi nhân viên (trừ chính mình).
    /// Quản lý cửa hàng: xem & đánh giá nhân viên (vai trò Employee) cùng chi nhánh, trừ chính mình.
    /// Mọi người có hồ sơ nhân viên: xem thẻ điểm của chính mình (My/MyDetail, chỉ đọc).
    /// </summary>
    [Authorize]
    public class PerformanceController : Controller
    {
        private readonly IPerformanceService _performanceService;
        private readonly IStoreService _storeService;

        public PerformanceController(IPerformanceService performanceService, IStoreService storeService)
        {
            _performanceService = performanceService;
            _storeService = storeService;
        }

        private string CurrentUsername => User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? string.Empty;
        private bool IsAdminOrHR => User.IsInRole("Admin") || User.IsInRole("HR") || User.IsInRole("SuperAdmin");
        private bool IsStoreManagerOnly => User.IsInRole("StoreManager") && !IsAdminOrHR;

        /// <summary>Chuẩn hóa tháng được chọn: mặc định / không hợp lệ / tương lai -> tháng hiện tại.</summary>
        private static (int Year, int Month) NormalizeMonth(int? year, int? month)
        {
            var today = DateTime.Today;
            if (!year.HasValue || !month.HasValue || month.Value is < 1 or > 12 || year.Value is < 2000 or > 9999)
                return (today.Year, today.Month);
            if (year.Value * 12 + month.Value > today.Year * 12 + today.Month)
                return (today.Year, today.Month);
            return (year.Value, month.Value);
        }

        private static List<int> AvailableYears(int selectedYear)
        {
            var current = DateTime.Today.Year;
            var years = Enumerable.Range(current - 4, 5).Reverse().ToList();
            if (!years.Contains(selectedYear)) years.Add(selectedYear);
            return years.OrderByDescending(y => y).ToList();
        }

        /// <summary>Phạm vi danh sách theo vai trò; null nếu không được phép xem.</summary>
        private async Task<(PerformanceScope? Scope, Employee? Current)> GetScopeAsync()
        {
            var current = await _performanceService.GetEmployeeByUsernameAsync(CurrentUsername);
            if (IsAdminOrHR) return (new PerformanceScope(), current);
            if (IsStoreManagerOnly && current?.StoreId != null)
            {
                return (new PerformanceScope
                {
                    ForcedStoreId = current.StoreId,
                    ExcludeEmployeeId = current.EmployeeId,
                    OnlyEmployeeRole = true
                }, current);
            }
            return (null, current);
        }

        /// <summary>Người xem hiện tại có được xem/đánh giá nhân viên này không (không áp dụng cho chính mình).</summary>
        private bool CanManage(Employee target, Employee? current)
        {
            if (current != null && current.EmployeeId == target.EmployeeId) return false;
            if (string.Equals(target.Username, CurrentUsername, StringComparison.OrdinalIgnoreCase)) return false;
            if (IsAdminOrHR) return true;
            return IsStoreManagerOnly
                && current?.StoreId != null
                && target.StoreId == current.StoreId
                && target.Account?.Role == AccountRole.Employee;
        }

        private static bool IsFutureMonth(int year, int month)
        {
            var today = DateTime.Today;
            return year * 12 + month > today.Year * 12 + today.Month;
        }

        // GET: /Performance/Index
        public async Task<IActionResult> Index(PerformanceFilter filter)
        {
            var (scope, current) = await GetScopeAsync();
            if (scope == null) return Forbid();

            var (year, month) = NormalizeMonth(filter.Year, filter.Month);
            var storeId = scope.ForcedStoreId ?? (filter.StoreId > 0 ? filter.StoreId : null);
            var search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
            if (search?.Length > 100) search = search[..100];

            var rows = await _performanceService.GetRowsAsync(scope, year, month, storeId, search);
            var pageSize = filter.PageSize > 0 ? filter.PageSize : 10;
            var totalPages = Math.Max(1, (int)Math.Ceiling(rows.Count / (double)pageSize));
            var page = Math.Clamp(filter.PageNumber, 1, totalPages);

            var vm = new PerformanceIndexVM
            {
                Year = year,
                Month = month,
                StoreId = storeId,
                Search = search,
                IsStoreManager = scope.ForcedStoreId.HasValue,
                AvailableYears = AvailableYears(year),
                Summary = _performanceService.Summarize(rows),
                Paged = new PaginationRes<PerformanceRowVM>(rows.Skip((page - 1) * pageSize).Take(pageSize).ToList(), page, pageSize, rows.Count)
            };
            if (!vm.IsStoreManager)
                vm.StoreSelectList = await _storeService.GetSelectListAsync(R.Filter_AllStores);
            ViewBag.StoreName = vm.IsStoreManager ? current?.Store?.StoreName : null;
            return View(vm);
        }

        // GET: /Performance/ExportExcel
        public async Task<IActionResult> ExportExcel(PerformanceFilter filter)
        {
            var (scope, _) = await GetScopeAsync();
            if (scope == null) return Forbid();

            var (year, month) = NormalizeMonth(filter.Year, filter.Month);
            var storeId = scope.ForcedStoreId ?? (filter.StoreId > 0 ? filter.StoreId : null);
            var search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
            if (search?.Length > 100) search = search[..100];

            try
            {
                var rows = await _performanceService.GetRowsAsync(scope, year, month, storeId, search);
                var bytes = await _performanceService.ExportExcelAsync(rows, year, month);
                var fileName = string.Format(R.Excel_FileName, year, month.ToString("D2")) + ".xlsx";
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                AlertHelper.AddErrorMessage(TempData, string.Format(R.Err_ExportFailed, ex.Message));
                return RedirectToAction(nameof(Index), new { year, month, storeId, search });
            }
        }

        // GET: /Performance/Detail?employeeId=1&year=2026&month=10
        public async Task<IActionResult> Detail(int employeeId, int? year, int? month)
        {
            var (y, m) = NormalizeMonth(year, month);
            var target = await _performanceService.GetEmployeeAsync(employeeId);
            if (target == null)
            {
                AlertHelper.AddErrorMessage(TempData, R.Err_EmployeeNotFound);
                return (IsAdminOrHR || IsStoreManagerOnly) ? RedirectToAction(nameof(Index), new { year = y, month = m }) : RedirectToAction(nameof(My));
            }

            if (string.Equals(target.Username, CurrentUsername, StringComparison.OrdinalIgnoreCase))
                return RedirectToAction(nameof(MyDetail), new { year = y, month = m });

            var current = await _performanceService.GetEmployeeByUsernameAsync(CurrentUsername);
            if (IsStoreManagerOnly && current?.StoreId == null) return Forbid();
            if (!CanManage(target, current)) return Forbid();

            var vm = await _performanceService.GetDetailAsync(employeeId, y, m);
            if (vm == null) return NotFound();
            vm.AvailableYears = AvailableYears(y);
            vm.IsOwnView = false;
            vm.CanReview = !IsFutureMonth(y, m);
            if (!vm.CanReview) vm.ReviewBlockedReason = R.Err_FutureMonth;
            return View("Detail", vm);
        }

        // GET: /Performance/My
        public async Task<IActionResult> My()
        {
            var current = await _performanceService.GetEmployeeByUsernameAsync(CurrentUsername);
            if (current == null) return View(new PerformanceMyVM { HasProfile = false });
            return View(await _performanceService.GetMyAsync(current.EmployeeId));
        }

        // GET: /Performance/MyDetail?year=2026&month=10
        public async Task<IActionResult> MyDetail(int? year, int? month)
        {
            var current = await _performanceService.GetEmployeeByUsernameAsync(CurrentUsername);
            if (current == null) return RedirectToAction(nameof(My));

            var (y, m) = NormalizeMonth(year, month);
            var vm = await _performanceService.GetDetailAsync(current.EmployeeId, y, m);
            if (vm == null) return RedirectToAction(nameof(My));
            vm.AvailableYears = AvailableYears(y);
            vm.IsOwnView = true;
            vm.CanReview = false;
            return View("Detail", vm);
        }

        // POST: /Performance/Review
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Review(PerformanceReviewInput input)
        {
            if (!IsAdminOrHR && !IsStoreManagerOnly) return Forbid();

            var target = await _performanceService.GetEmployeeAsync(input.EmployeeId);
            if (target == null)
            {
                AlertHelper.AddErrorMessage(TempData, R.Err_EmployeeNotFound);
                return RedirectToAction(nameof(Index));
            }

            if (string.Equals(target.Username, CurrentUsername, StringComparison.OrdinalIgnoreCase))
            {
                AlertHelper.AddErrorMessage(TempData, R.Err_SelfReview);
                return RedirectToAction(nameof(MyDetail), new { year = input.Year, month = input.Month });
            }

            var current = await _performanceService.GetEmployeeByUsernameAsync(CurrentUsername);
            if (!CanManage(target, current)) return Forbid();

            try
            {
                await _performanceService.SaveReviewAsync(input, CurrentUsername);
                AlertHelper.AddSuccessMessage(TempData, R.Toast_ReviewSaved);
            }
            catch (ArgumentException ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.Message);
            }

            var (y, m) = NormalizeMonth(input.Year, input.Month);
            return RedirectToAction(nameof(Detail), new { employeeId = input.EmployeeId, year = y, month = m });
        }
    }
}
