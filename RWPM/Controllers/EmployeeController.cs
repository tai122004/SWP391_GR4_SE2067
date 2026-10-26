using RWPM.Common.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using RWPM.Common;
using RWPM.Common.Attributes;
using RWPM.Common.Exceptions;
using RWPM.Common.Security;
using RWPM.Infrastructure.Data;
using RWPM.Models.ViewModels.Employee;
using RWPM.Services.Abstraction;
using AccRes = RWPM.Resources.Shared.Acc;
using EmpRes = RWPM.Resources.Shared.Employee;

namespace RWPM.Controllers
{
    [Authorize(Roles = AppRoles.Managers)]
    public class EmployeeController : Controller
    {
        private readonly IStringLocalizer _localizer;
        private readonly IEmployeeService _employeeService;
        private readonly IStoreService _storeService;
        private readonly DefaultDatabaseContext _context;

        public EmployeeController(
            IStringLocalizer<ErrorServerDefinition> localizer,
            IEmployeeService employeeService,
            IStoreService storeService,
            DefaultDatabaseContext context)
        {
            _localizer = localizer;
            _employeeService = employeeService;
            _storeService = storeService;
            _context = context;
        }

        private bool IsStoreManagerOnly => User.IsStoreScoped();

        private Task<RWPM.Models.Entities.Employee?> GetCurrentEmployeeAsync()
        {
            var username = User.Identity?.Name;
            return _context.Employee.AsNoTracking().FirstOrDefaultAsync(e => e.Username == username);
        }

        /// <summary>
        /// Quản lý cửa hàng chỉ được thao tác trên nhân viên (vai trò Employee) cùng chi nhánh, không phải chính mình.
        /// </summary>
        private async Task<bool> StoreManagerCanManageAsync(int employeeId, RWPM.Models.Entities.Employee? currentEmp = null)
        {
            currentEmp ??= await GetCurrentEmployeeAsync();
            if (currentEmp?.StoreId == null || currentEmp.EmployeeId == employeeId) return false;
            return await _context.Employee.AsNoTracking().AnyAsync(e => e.EmployeeId == employeeId
                && e.StoreId == currentEmp.StoreId
                && e.Account.Role == RWPM.Common.Enums.AccountRole.Employee);
        }

        /// <summary>
        /// Quản lý cửa hàng chỉ xem được nhân viên chi nhánh mình: ép StoreId của bộ lọc. Trả về false nếu QLCH chưa gán chi nhánh.
        /// </summary>
        private async Task<bool> ApplyStoreScopeAsync(EmployeeSearch searchObject)
        {
            if (!IsStoreManagerOnly) return true;
            var currentEmp = await GetCurrentEmployeeAsync();
            if (currentEmp?.StoreId == null) return false;
            searchObject.StoreId = currentEmp.StoreId;
            return true;
        }

        private async Task PopulateFormListsAsync(string? username)
        {
            ViewBag.AccountList = await _employeeService.GetAvailableAccountsSelectListAsync(username);
            ViewBag.StoreList = await _storeService.GetSelectListAsync();
        }

        private static string StoreManagerEditDeniedMessage => InputRules.Text(
            "Quản lý cửa hàng chỉ được sửa hồ sơ nhân viên thuộc chi nhánh của mình, không được sửa hồ sơ của chính mình hoặc của quản lý/nhân sự khác.",
            "Store managers can only edit employees of their own store, not their own profile or other managers/HR profiles.");

        // GET: Employee
        [RemoveEmptyQueryString]
        public async Task<IActionResult> Index(EmployeeSearch searchObject)
        {
            if (!await ApplyStoreScopeAsync(searchObject))
            {
                return Forbid();
            }

            var result = await _employeeService.SearchAsync(searchObject);
            var storeSelectList = await _storeService.GetSelectListAsync();

            ViewBag.Statistics = await _employeeService.GetStatisticsAsync(searchObject.StoreId);
            ViewBag.IsStoreManager = IsStoreManagerOnly;

            return View(new EmployeeListVM(result, searchObject, storeSelectList));
        }

        // GET: Employee/ExportExcel
        [HttpGet]
        public async Task<IActionResult> ExportExcel(EmployeeSearch searchObject)
        {
            if (!await ApplyStoreScopeAsync(searchObject))
            {
                return Forbid();
            }

            var fileBytes = await _employeeService.ExportToExcelAsync(searchObject);
            var fileName = $"{InputRules.Text("Danh_Sach_Nhan_Vien", "Employees")}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // GET: Employee/DownloadTemplate
        [Authorize(Roles = AppRoles.ChainManagers)]
        [HttpGet]
        public async Task<IActionResult> DownloadTemplate()
        {
            var fileBytes = await _employeeService.GenerateImportTemplateAsync();
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{InputRules.Text("Mau_Nhap_Nhan_Vien", "Employee_Import_Template")}_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        // POST: Employee/ImportExcel
        [Authorize(Roles = AppRoles.ChainManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportExcel(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                AlertHelper.AddErrorMessage(TempData, EmpRes.Import_InvalidFile);
                return RedirectToAction(nameof(Index));
            }

            if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                AlertHelper.AddErrorMessage(TempData, EmpRes.Import_OnlyXlsx);
                return RedirectToAction(nameof(Index));
            }

            try
            {
                using var stream = file.OpenReadStream();
                var result = await _employeeService.ImportFromExcelAsync(stream, User.Identity?.Name ?? "system");

                if (result.SuccessCount > 0 && result.FailureCount == 0)
                {
                    AlertHelper.AddSuccessMessage(TempData, string.Format(EmpRes.Import_SuccessAll, result.SuccessCount, result.TotalRows));
                }
                else if (result.SuccessCount > 0 && result.FailureCount > 0)
                {
                    AlertHelper.AddWarningMessage(TempData, string.Format(EmpRes.Import_SuccessPartial, result.SuccessCount, result.FailureCount, string.Join(" | ", result.ErrorMessages.Take(3))));
                }
                else
                {
                    AlertHelper.AddErrorMessage(TempData, string.Format(EmpRes.Import_Failed, string.Join(" | ", result.ErrorMessages.Take(5))));
                }
            }
            catch (Exception ex)
            {
                AlertHelper.AddErrorMessage(TempData, string.Format(EmpRes.Import_ProcessError, InputRules.UserMessage(ex)));
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Employee/PreviewImportExcel
        [Authorize(Roles = AppRoles.ChainManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PreviewImportExcel(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return Json(new { success = false, message = EmpRes.Import_InvalidFile });
            }

            if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = EmpRes.Import_OnlyXlsx });
            }

            try
            {
                using var stream = file.OpenReadStream();
                var preview = await _employeeService.PreviewImportFromExcelAsync(stream);
                return Json(new { success = true, data = preview });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = InputRules.UserMessage(ex) });
            }
        }

        // POST: Employee/ConfirmImportExcel
        [Authorize(Roles = AppRoles.ChainManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmImportExcel([FromBody] ConfirmImportRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.ImportToken))
            {
                return Json(new { success = false, message = EmpRes.Import_InvalidSession });
            }

            try
            {
                var count = await _employeeService.ConfirmImportAsync(request.ImportToken, User.Identity?.Name ?? "system");
                return Json(new { success = true, count = count, message = string.Format(EmpRes.Import_ConfirmSuccess, count) });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = InputRules.UserMessage(ex) });
            }
        }

        // GET: Employee/Create
        [Authorize(Roles = AppRoles.ChainManagers)]
        public async Task<IActionResult> Create()
        {
            await PopulateFormListsAsync(null);
            return View(new EmployeeCreateVM());
        }

        // POST: Employee/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.ChainManagers)]
        public async Task<IActionResult> Create(EmployeeCreateVM viewModel)
        {
            if (!ModelState.IsValid)
            {
                await PopulateFormListsAsync(viewModel.Username);
                return View(viewModel);
            }

            try
            {
                // Lưu thông tin cá nhân (SĐT, ngày sinh, giới tính) vào tài khoản liên kết; được ghi cùng transaction trong CreateAsync
                var account = await _context.Acc.FirstOrDefaultAsync(a => a.Username == viewModel.Username);
                if (account != null)
                {
                    account.PhoneNumber = string.IsNullOrWhiteSpace(viewModel.PhoneNumber) ? null : viewModel.PhoneNumber.Trim();
                    account.DateOfBirth = viewModel.DateOfBirth;
                    account.Gender = viewModel.Gender;
                }
                await _employeeService.CreateAsync(viewModel.ToEntity());
                AlertHelper.CreateSuccess(TempData);
                return RedirectToAction(nameof(Index));
            }
            catch (ModelValidationException ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.GetErrorString(_localizer));
                await PopulateFormListsAsync(viewModel.Username);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                if (ex is InputValidationException input) ModelState.AddModelError(input.Field, input.Message);
                else if (InputRules.Duplicate(ex) is { } duplicate) ModelState.AddModelError(duplicate.Field, duplicate.Message);
                else AlertHelper.AddErrorMessage(TempData, InputRules.UserMessage(ex));
                await PopulateFormListsAsync(viewModel.Username);
                return View(viewModel);
            }
        }

        // GET: Employee/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var employee = await _employeeService.GetByIdAsync(id);
            if (employee == null)
            {
                return NotFound();
            }

            // Check quyền StoreManager
            var isStoreManager = IsStoreManagerOnly;
            if (isStoreManager)
            {
                var currentEmp = await GetCurrentEmployeeAsync();
                if (currentEmp?.StoreId == null || currentEmp.StoreId != employee.StoreId)
                {
                    return Forbid();
                }
                if (!await StoreManagerCanManageAsync(employee.EmployeeId, currentEmp))
                {
                    AlertHelper.AddErrorMessage(TempData, StoreManagerEditDeniedMessage);
                    return RedirectToAction(nameof(Index));
                }
            }

            await PopulateFormListsAsync(employee.Username);
            ViewBag.IsStoreManager = isStoreManager;
            return View(new EmployeeEditVM(employee));
        }

        // POST: Employee/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EmployeeEditVM viewModel)
        {
            if (id != viewModel.EmployeeId)
            {
                return NotFound();
            }

            var isStoreManager = IsStoreManagerOnly;
            ViewBag.IsStoreManager = isStoreManager;
            if (isStoreManager)
            {
                var stored = await _employeeService.GetByIdAsync(id);
                if (stored == null) return NotFound();
                var currentEmp = await GetCurrentEmployeeAsync();
                if (currentEmp?.StoreId == null || currentEmp.StoreId != stored.StoreId || viewModel.StoreId != stored.StoreId)
                {
                    return Forbid();
                }
                if (!await StoreManagerCanManageAsync(id, currentEmp))
                {
                    AlertHelper.AddErrorMessage(TempData, StoreManagerEditDeniedMessage);
                    return RedirectToAction(nameof(Index));
                }
                // Quản lý cửa hàng không được đổi tài khoản liên kết và chế độ lương: giữ nguyên giá trị trong DB
                viewModel.Username = stored.Username;
                viewModel.HourlyRate = stored.HourlyRate;
                viewModel.BaseSalary = stored.BaseSalary;
                ModelState.Remove(nameof(viewModel.Username));
                ModelState.Remove(nameof(viewModel.HourlyRate));
                ModelState.Remove(nameof(viewModel.BaseSalary));
            }

            if (!string.IsNullOrWhiteSpace(viewModel.PhoneNumber))
            {
                var trimmedPhone = viewModel.PhoneNumber.Trim();
                var phoneExists = await _context.Acc.AnyAsync(a => a.Username != viewModel.Username && a.PhoneNumber == trimmedPhone);
                if (phoneExists)
                {
                    ModelState.AddModelError(nameof(viewModel.PhoneNumber), string.Format(AccRes.Err_PhoneInUse, viewModel.PhoneNumber));
                }
            }

            if (!ModelState.IsValid)
            {
                await PopulateFormListsAsync(viewModel.Username);
                return View(viewModel);
            }

            try
            {
                var employee = await _employeeService.GetRequiredByIdAsync(id);
                viewModel.ApplyToEntity(employee);
                await _employeeService.UpdateAsync(employee);
                AlertHelper.EditSuccess(TempData);
                return RedirectToAction(nameof(Index));
            }
            catch (ModelValidationException ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.GetErrorString(_localizer));
                await PopulateFormListsAsync(viewModel.Username);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                if (ex is InputValidationException input) ModelState.AddModelError(input.Field, input.Message);
                else if (InputRules.Duplicate(ex) is { } duplicate) ModelState.AddModelError(duplicate.Field, duplicate.Message);
                else AlertHelper.AddErrorMessage(TempData, InputRules.UserMessage(ex));
                await PopulateFormListsAsync(viewModel.Username);
                return View(viewModel);
            }
        }

        // POST: Employee/Resign
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resign(int employeeId, DateTime? resignDate, string? resignReason)
        {
            if (IsStoreManagerOnly && !await StoreManagerCanManageAsync(employeeId))
            {
                return Forbid();
            }

            try
            {
                var targetDate = resignDate ?? DateTime.Today;
                await _employeeService.ResignAsync(employeeId, targetDate, resignReason);
                AlertHelper.AddSuccessMessage(TempData, EmpRes.Resign_Success);
            }
            catch (Exception ex)
            {
                AlertHelper.AddErrorMessage(TempData, InputRules.UserMessage(ex));
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: Employee/Delete/5
        [Authorize(Roles = AppRoles.ChainManagers)]
        public async Task<IActionResult> Delete(int id)
        {
            var employee = await _employeeService.GetByIdAsync(id);
            if (employee == null)
            {
                return NotFound();
            }

            return View(employee);
        }

        // POST: Employee/DeleteConfirmed
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.ChainManagers)]
        public async Task<IActionResult> DeleteConfirmed(int employeeId)
        {
            var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            try
            {
                var employee = await _employeeService.GetRequiredByIdAsync(employeeId);
                await _employeeService.DeleteAsync(employee);
                if (isAjax) return Json(new { success = true });
                AlertHelper.DeleteSuccess(TempData);
            }
            catch (InputValidationException ex)
            {
                if (isAjax) return Conflict(new { message = InputRules.UserMessage(ex) });
                AlertHelper.AddErrorMessage(TempData, InputRules.UserMessage(ex));
                return RedirectToAction(nameof(Delete), new { id = employeeId });
            }
            catch (ModelValidationException ex)
            {
                var message = ex.GetErrorString(_localizer);
                if (isAjax) return Conflict(new { message });
                AlertHelper.AddErrorMessage(TempData, message);
                return RedirectToAction(nameof(Delete), new { id = employeeId });
            }
            catch (Exception)
            {
                // Lỗi khác (thường là ràng buộc khóa ngoại): hướng người dùng ngừng hoạt động/thôi việc thay vì xóa
                var message = InputRules.Text("Không thể xóa nhân viên vì còn dữ liệu liên quan. Hãy ngừng hoạt động hoặc cho thôi việc để giữ lịch sử.",
                    "Cannot delete the employee because related records exist. Deactivate or resign the employee to preserve history.");
                if (isAjax) return Conflict(new { message });
                AlertHelper.AddErrorMessage(TempData, message);
                return RedirectToAction(nameof(Delete), new { id = employeeId });
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Employee/UpdateActiveStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateActiveStatus(int id, [FromBody] EmployeeUpdateActiveStatusVM viewModel)
        {
            if (id != viewModel.EmployeeId)
                return BadRequest();

            if (!ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState.Values
                                    .SelectMany(v => v.Errors)
                                    .Select(e => e.ErrorMessage));
                return BadRequest(errors);
            }

            if (IsStoreManagerOnly && !await StoreManagerCanManageAsync(viewModel.EmployeeId!.Value))
            {
                return Forbid();
            }

            try
            {
                await _employeeService.UpdateActiveStatusAsync(viewModel.EmployeeId!.Value, viewModel.IsActive!.Value);
                return Ok();
            }
            catch (ModelValidationException ex)
            {
                return BadRequest(ex.GetErrorString(_localizer));
            }
            catch (Exception ex)
            {
                return BadRequest(InputRules.UserMessage(ex));
            }
        }
    }
}
