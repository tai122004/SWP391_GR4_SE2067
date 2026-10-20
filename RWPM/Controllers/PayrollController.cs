using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RWPM.Common.Enums;
using RWPM.Infrastructure.Data;
using RWPM.Models.ViewModels.Payroll;
using RWPM.Services.Abstraction;
using System.Security.Claims;
using PayrollResource = RWPM.Resources.Shared.PayrollResource;

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
                AvailableYears = availableYears,
                NextPeriod = await _payrollService.GetNextPeriodSuggestionAsync()
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
                TempData["ErrorMessage"] = PayrollResource.Err_PayrollNotFound;
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: /Payroll/Calculate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Calculate(DateTime startDate, DateTime endDate)
        {
            if (!IsAdminOrHR)
            {
                return Forbid();
            }

            try
            {
                var payroll = await _payrollService.CalculatePayrollAsync(startDate, endDate, CurrentUsername);
                TempData["SuccessMessage"] = string.Format(PayrollResource.Toast_CalculateSuccess, payroll.StartDate.ToString("dd/MM/yyyy"), payroll.EndDate.ToString("dd/MM/yyyy"), payroll.TotalEmployees);
                return RedirectToAction(nameof(Detail), new { id = payroll.PayrollId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = string.Format(PayrollResource.Err_CalculateFailed, ex.Message);
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
                TempData["ErrorMessage"] = PayrollResource.Err_PayrollNotFound;
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await _payrollService.CalculatePayrollAsync(payroll.StartDate, payroll.EndDate, CurrentUsername);
                TempData["SuccessMessage"] = string.Format(PayrollResource.Toast_RecalculateSuccess, payroll.StartDate.ToString("dd/MM/yyyy"), payroll.EndDate.ToString("dd/MM/yyyy"));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = string.Format(PayrollResource.Err_RecalculateFailed, ex.Message);
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
                TempData["SuccessMessage"] = PayrollResource.Toast_LockSuccess;
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = string.Format(PayrollResource.Err_LockFailed, ex.Message);
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
                await _payrollService.UnlockPayrollAsync(id, reason ?? "", CurrentUsername);
                TempData["SuccessMessage"] = PayrollResource.Toast_UnlockSuccess;
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = string.Format(PayrollResource.Err_UnlockFailed, ex.Message);
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
                TempData["SuccessMessage"] = PayrollResource.Toast_DeleteSuccess;
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = string.Format(PayrollResource.Err_DeleteFailed, ex.Message);
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
                TempData["ErrorMessage"] = PayrollResource.Err_EmployeeDetailNotFound;
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
                    TempData["ErrorMessage"] = PayrollResource.Err_PayrollNotFound;
                    return RedirectToAction(nameof(Index));
                }

                var excelBytes = await _payrollService.ExportPayrollToExcelAsync(id, effectiveStoreId);
                var fileName = string.Format(PayrollResource.Excel_FileName, payroll.StartDate.ToString("ddMMyy"), payroll.EndDate.ToString("ddMMyy")) + ".xlsx";
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = string.Format(PayrollResource.Err_ExportFailed, ex.Message);
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
                TempData["ErrorMessage"] = PayrollResource.Err_PayslipNotFound;
                return RedirectToAction(nameof(MyPayslips));
            }

            if (vm.Payroll.Status != PayrollStatus.Locked && !IsAdminOrHR)
            {
                TempData["ErrorMessage"] = PayrollResource.Err_PayslipNotLocked;
                return RedirectToAction(nameof(MyPayslips));
            }

            return View(vm);
        }
    }
}
