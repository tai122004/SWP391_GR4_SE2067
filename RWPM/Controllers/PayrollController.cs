using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RWPM.Common.Enums;
using RWPM.Infrastructure.Data;
using RWPM.Models.ViewModels.Payroll;
using RWPM.Services.Abstraction;
using System.Security.Claims;

namespace RWPM.Controllers
{
    [Authorize]
    public class PayrollController : Controller
    {
        private readonly IPayrollService _payrollService;
        private readonly DefaultDatabaseContext _context;

        public PayrollController(IPayrollService payrollService, DefaultDatabaseContext context)
        {
            _payrollService = payrollService;
            _context = context;
        }

        private string CurrentUsername => User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? string.Empty;

        private bool IsAdminOrHR => User.IsInRole("Admin") || User.IsInRole("HR") || User.IsInRole("SuperAdmin");
        private bool IsStoreManager => User.IsInRole("StoreManager");

        private async Task<int?> GetCurrentStoreIdAsync()
        {
            if (IsAdminOrHR) return null;
            var emp = await _context.Employee.FirstOrDefaultAsync(e => e.Username == CurrentUsername);
            return emp?.StoreId;
        }

        // GET: /Payroll/Index
        public async Task<IActionResult> Index(int? year)
        {
            if (!IsAdminOrHR && !IsStoreManager)
            {
                return RedirectToAction(nameof(MyPayslips));
            }

            int? effectiveStoreId = null;
            if (IsStoreManager)
            {
                effectiveStoreId = await GetCurrentStoreIdAsync();
                if (!effectiveStoreId.HasValue || effectiveStoreId.Value <= 0)
                {
                    return Forbid();
                }
            }

            var currentYear = year ?? DateTime.Now.Year;
            var payrolls = await _payrollService.GetPayrollsAsync(currentYear, effectiveStoreId);

            var availableYears = await _context.Payroll
                .Select(p => p.PeriodYear)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();

            if (!availableYears.Contains(DateTime.Now.Year))
            {
                availableYears.Insert(0, DateTime.Now.Year);
            }

            var vm = new PayrollListVM
            {
                Payrolls = payrolls,
                SelectedYear = currentYear,
                AvailableYears = availableYears
            };

            ViewBag.IsAdminOrHR = IsAdminOrHR;
            ViewBag.IsStoreManager = IsStoreManager;
            ViewBag.CurrentStoreId = effectiveStoreId;
            return View(vm);
        }

        // GET: /Payroll/Detail/{id}
        public async Task<IActionResult> Detail(int id, int? storeId, string? search, PayrollDetailStatus? status)
        {
            if (!IsAdminOrHR && !IsStoreManager)
            {
                return Forbid();
            }

            int? effectiveStoreId = storeId;
            if (IsStoreManager)
            {
                var mgrStoreId = await GetCurrentStoreIdAsync();
                if (!mgrStoreId.HasValue || mgrStoreId.Value <= 0)
                {
                    return Forbid();
                }
                effectiveStoreId = mgrStoreId.Value; // Luôn áp dụng store của Store Manager
            }

            try
            {
                var vm = await _payrollService.GetPayrollDetailAsync(id, effectiveStoreId, search, status);
                ViewBag.IsAdminOrHR = IsAdminOrHR;
                ViewBag.IsStoreManager = IsStoreManager;
                ViewBag.CurrentStoreId = effectiveStoreId;
                ViewBag.AuditLogs = await _payrollService.GetPayrollAuditLogsAsync(id);
                return View(vm);
            }
            catch (KeyNotFoundException)
            {
                TempData["ErrorMessage"] = "Không tìm thấy kỳ lương!";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: /Payroll/Calculate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Calculate(int month, int year)
        {
            if (!IsAdminOrHR)
            {
                return Forbid();
            }

            try
            {
                var payroll = await _payrollService.CalculatePayrollAsync(month, year, CurrentUsername);
                TempData["SuccessMessage"] = $"Tính lương kỳ Tháng {month:D2}/{year} thành công với {payroll.TotalEmployees} nhân viên!";
                return RedirectToAction(nameof(Detail), new { id = payroll.PayrollId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi tính lương: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: /Payroll/Recalculate/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Recalculate(int id)
        {
            if (!IsAdminOrHR)
            {
                return Forbid();
            }

            var payroll = await _payrollService.GetPayrollByIdAsync(id);
            if (payroll == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy kỳ lương!";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await _payrollService.CalculatePayrollAsync(payroll.PeriodMonth, payroll.PeriodYear, CurrentUsername);
                TempData["SuccessMessage"] = $"Tính lại bảng lương Tháng {payroll.PeriodMonth:D2}/{payroll.PeriodYear} thành công!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi tính lại bảng lương: {ex.Message}";
            }

            return RedirectToAction(nameof(Detail), new { id });
        }

        // POST: /Payroll/Lock/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Lock(int id)
        {
            if (!IsAdminOrHR)
            {
                return Forbid();
            }

            try
            {
                await _payrollService.LockPayrollAsync(id, CurrentUsername);
                TempData["SuccessMessage"] = "Đã chốt bảng lương thành công! Dữ liệu bảng lương hiện đã được bảo vệ bất biến.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi chốt bảng lương: {ex.Message}";
            }

            return RedirectToAction(nameof(Detail), new { id });
        }

        // POST: /Payroll/Unlock/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(int id, string reason)
        {
            if (!IsAdminOrHR)
            {
                return Forbid();
            }

            try
            {
                await _payrollService.UnlockPayrollAsync(id, reason ?? "HR mở khóa bảng lương", CurrentUsername);
                TempData["SuccessMessage"] = "Đã mở khóa bảng lương để điều chỉnh.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi mở khóa bảng lương: {ex.Message}";
            }

            return RedirectToAction(nameof(Detail), new { id });
        }

        // POST: /Payroll/Delete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsAdminOrHR)
            {
                return Forbid();
            }

            try
            {
                await _payrollService.DeletePayrollAsync(id, CurrentUsername);
                TempData["SuccessMessage"] = "Đã xóa kỳ lương nháp thành công.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi xóa kỳ lương: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: /Payroll/EmployeeDetail?payrollId=1&employeeId=2
        public async Task<IActionResult> EmployeeDetail(int payrollId, int? employeeId = null, string? username = null)
        {
            if (!IsAdminOrHR && !IsStoreManager)
            {
                return Forbid();
            }

            EmployeePayslipVM? vm = null;

            if (employeeId.HasValue && employeeId.Value > 0)
            {
                vm = await _payrollService.GetEmployeePayslipByIdAsync(payrollId, employeeId.Value);
            }
            else if (!string.IsNullOrEmpty(username))
            {
                vm = await _payrollService.GetEmployeePayslipAsync(username, payrollId: payrollId);
            }

            if (vm == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy dữ liệu chi tiết của nhân viên trong kỳ lương!";
                return RedirectToAction(nameof(Detail), new { id = payrollId });
            }

            if (IsStoreManager)
            {
                var mgrStoreId = await GetCurrentStoreIdAsync();
                if (!mgrStoreId.HasValue || mgrStoreId.Value <= 0 || vm.Detail.StoreId != mgrStoreId.Value)
                {
                    return Forbid();
                }
            }

            ViewBag.IsAdminOrHR = IsAdminOrHR;
            return View(vm);
        }

        // GET: /Payroll/ExportExcel/{id}
        public async Task<IActionResult> ExportExcel(int id, int? storeId)
        {
            if (!IsAdminOrHR && !IsStoreManager)
            {
                return Forbid();
            }

            int? effectiveStoreId = storeId;
            if (IsStoreManager)
            {
                var mgrStoreId = await GetCurrentStoreIdAsync();
                if (!mgrStoreId.HasValue || mgrStoreId.Value <= 0)
                {
                    return Forbid();
                }
                effectiveStoreId = mgrStoreId.Value; // Luôn áp dụng store của Store Manager
            }

            try
            {
                var payroll = await _payrollService.GetPayrollByIdAsync(id);
                if (payroll == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy kỳ lương!";
                    return RedirectToAction(nameof(Index));
                }

                var excelBytes = await _payrollService.ExportPayrollToExcelAsync(id, effectiveStoreId);
                var fileName = $"BangLuong_{payroll.PeriodMonth:D2}_{payroll.PeriodYear}.xlsx";
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi xuất Excel: {ex.Message}";
                return RedirectToAction(nameof(Detail), new { id });
            }
        }

        // GET: /Payroll/MyPayslips
        public async Task<IActionResult> MyPayslips()
        {
            var history = await _payrollService.GetEmployeePayslipHistoryAsync(CurrentUsername);
            return View(history);
        }

        // GET: /Payroll/MyPayslipDetail/{payrollId}
        public async Task<IActionResult> MyPayslipDetail(int payrollId)
        {
            var vm = await _payrollService.GetEmployeePayslipAsync(CurrentUsername, payrollId: payrollId);
            if (vm == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy phiếu lương này!";
                return RedirectToAction(nameof(MyPayslips));
            }

            if (vm.Payroll.Status != PayrollStatus.Locked && !IsAdminOrHR)
            {
                TempData["ErrorMessage"] = "Bảng lương này chưa được chốt duyệt!";
                return RedirectToAction(nameof(MyPayslips));
            }

            return View(vm);
        }
    }
}
