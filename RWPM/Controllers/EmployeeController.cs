using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using RWPM.Common;
using RWPM.Common.Attributes;
using RWPM.Common.Exceptions;
using RWPM.Common.Helper;
using RWPM.Infrastructure.Data;
using RWPM.Models.ViewModels.Employee;
using RWPM.Services.Abstraction;

namespace RWPM.Controllers
{
    [Authorize(Roles = "Admin,HR,AreaManager,StoreManager,SuperAdmin")]
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

        // GET: Employee
        [RemoveEmptyQueryString]
        public async Task<IActionResult> Index(EmployeeSearch searchObject)
        {
            var isStoreManager = User.IsInRole("StoreManager") && !User.IsInRole("Admin") && !User.IsInRole("HR") && !User.IsInRole("SuperAdmin");
            if (isStoreManager)
            {
                var username = User.Identity?.Name;
                var currentEmp = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
                if (currentEmp != null)
                {
                    searchObject.StoreId = currentEmp.StoreId;
                }
            }

            var result = await _employeeService.SearchAsync(searchObject);
            var storeSelectList = await _storeService.GetSelectListAsync();

            ViewBag.Statistics = await _employeeService.GetStatisticsAsync(searchObject.StoreId);
            ViewBag.IsStoreManager = isStoreManager;

            return View(new EmployeeListVM(result, searchObject, storeSelectList));
        }

        // GET: Employee/ExportExcel
        [HttpGet]
        public async Task<IActionResult> ExportExcel(EmployeeSearch searchObject)
        {
            var isStoreManager = User.IsInRole("StoreManager") && !User.IsInRole("Admin") && !User.IsInRole("HR") && !User.IsInRole("SuperAdmin");
            if (isStoreManager)
            {
                var username = User.Identity?.Name;
                var currentEmp = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
                if (currentEmp != null)
                {
                    searchObject.StoreId = currentEmp.StoreId;
                }
            }

            var fileBytes = await _employeeService.ExportToExcelAsync(searchObject);
            var fileName = $"Employees_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // GET: Employee/DownloadTemplate
        [Authorize(Roles = "Admin,HR,AreaManager,SuperAdmin")]
        [HttpGet]
        public async Task<IActionResult> DownloadTemplate()
        {
            var fileBytes = await _employeeService.GenerateImportTemplateAsync();
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Mau_Nhap_Nhan_Vien_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        // POST: Employee/ImportExcel
        [Authorize(Roles = "Admin,HR,AreaManager,SuperAdmin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportExcel(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                AlertHelper.AddErrorMessage(TempData, "Vui lòng chọn một file Excel (.xlsx) hợp lệ.");
                return RedirectToAction(nameof(Index));
            }

            if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                AlertHelper.AddErrorMessage(TempData, "Hệ thống chỉ hỗ trợ file định dạng Excel (.xlsx).");
                return RedirectToAction(nameof(Index));
            }

            try
            {
                using var stream = file.OpenReadStream();
                var result = await _employeeService.ImportFromExcelAsync(stream, User.Identity?.Name ?? "system");

                if (result.SuccessCount > 0 && result.FailureCount == 0)
                {
                    AlertHelper.AddSuccessMessage(TempData, $"Nhập thành công {result.SuccessCount}/{result.TotalRows} nhân viên từ file Excel!");
                }
                else if (result.SuccessCount > 0 && result.FailureCount > 0)
                {
                    AlertHelper.AddWarningMessage(TempData, $"Nhập thành công {result.SuccessCount} nhân viên. Bỏ qua {result.FailureCount} dòng lỗi. Chi tiết: {string.Join(" | ", result.ErrorMessages.Take(3))}");
                }
                else
                {
                    AlertHelper.AddErrorMessage(TempData, $"Nhập thất bại. {string.Join(" | ", result.ErrorMessages.Take(5))}");
                }
            }
            catch (Exception ex)
            {
                AlertHelper.AddErrorMessage(TempData, $"Lỗi khi xử lý file Excel: {ex.Message}");
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Employee/PreviewImportExcel
        [Authorize(Roles = "Admin,HR,AreaManager,SuperAdmin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PreviewImportExcel(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn một file Excel (.xlsx) hợp lệ." });
            }

            if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = "Hệ thống chỉ hỗ trợ file định dạng Excel (.xlsx)." });
            }

            try
            {
                using var stream = file.OpenReadStream();
                var preview = await _employeeService.PreviewImportFromExcelAsync(stream);
                return Json(new { success = true, data = preview });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: Employee/ConfirmImportExcel
        [Authorize(Roles = "Admin,HR,AreaManager,SuperAdmin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmImportExcel([FromBody] ConfirmImportRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.ImportToken))
            {
                return Json(new { success = false, message = "Mã phiên nhập không hợp lệ hoặc đã hết hạn." });
            }

            try
            {
                var count = await _employeeService.ConfirmImportAsync(request.ImportToken, User.Identity?.Name ?? "system");
                return Json(new { success = true, count = count, message = $"Nhập thành công {count} nhân viên vào hệ thống!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: Employee/Create
        [Authorize(Roles = "Admin,HR,AreaManager,SuperAdmin")]
        public async Task<IActionResult> Create()
        {
            ViewBag.AccountList = await _employeeService.GetAvailableAccountsSelectListAsync();
            ViewBag.StoreList = await _storeService.GetSelectListAsync();
            return View(new EmployeeCreateVM());
        }

        // POST: Employee/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,HR,AreaManager,SuperAdmin")]
        public async Task<IActionResult> Create(EmployeeCreateVM viewModel)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.AccountList = await _employeeService.GetAvailableAccountsSelectListAsync(viewModel.Username);
                ViewBag.StoreList = await _storeService.GetSelectListAsync();
                return View(viewModel);
            }

            try
            {
                await _employeeService.CreateAsync(viewModel.ToEntity());
                AlertHelper.CreateSuccess(TempData);
                return RedirectToAction(nameof(Index));
            }
            catch (ModelValidationException ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.GetErrorString(_localizer));
                ViewBag.AccountList = await _employeeService.GetAvailableAccountsSelectListAsync(viewModel.Username);
                ViewBag.StoreList = await _storeService.GetSelectListAsync();
                return View(viewModel);
            }
            catch (Exception ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.Message);
                ViewBag.AccountList = await _employeeService.GetAvailableAccountsSelectListAsync(viewModel.Username);
                ViewBag.StoreList = await _storeService.GetSelectListAsync();
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
            var isStoreManager = User.IsInRole("StoreManager") && !User.IsInRole("Admin") && !User.IsInRole("HR") && !User.IsInRole("SuperAdmin");
            if (isStoreManager)
            {
                var username = User.Identity?.Name;
                var currentEmp = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
                if (currentEmp != null && currentEmp.StoreId != employee.StoreId)
                {
                    return Forbid();
                }
            }

            ViewBag.AccountList = await _employeeService.GetAvailableAccountsSelectListAsync(employee.Username);
            ViewBag.StoreList = await _storeService.GetSelectListAsync();
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

            if (!ModelState.IsValid)
            {
                ViewBag.AccountList = await _employeeService.GetAvailableAccountsSelectListAsync(viewModel.Username);
                ViewBag.StoreList = await _storeService.GetSelectListAsync();
                return View(viewModel);
            }

            try
            {
                var employee = await _employeeService.GetRequiredByIdAsync(id);

                var isStoreManager = User.IsInRole("StoreManager") && !User.IsInRole("Admin") && !User.IsInRole("HR") && !User.IsInRole("SuperAdmin");
                if (isStoreManager)
                {
                    var username = User.Identity?.Name;
                    var currentEmp = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
                    if (currentEmp != null && (currentEmp.StoreId != employee.StoreId || viewModel.StoreId != employee.StoreId))
                    {
                        return Forbid();
                    }
                }

                viewModel.ApplyToEntity(employee);
                await _employeeService.UpdateAsync(employee);
                AlertHelper.EditSuccess(TempData);
                return RedirectToAction(nameof(Index));
            }
            catch (ModelValidationException ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.GetErrorString(_localizer));
                ViewBag.AccountList = await _employeeService.GetAvailableAccountsSelectListAsync(viewModel.Username);
                ViewBag.StoreList = await _storeService.GetSelectListAsync();
                return View(viewModel);
            }
            catch (Exception ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.Message);
                ViewBag.AccountList = await _employeeService.GetAvailableAccountsSelectListAsync(viewModel.Username);
                ViewBag.StoreList = await _storeService.GetSelectListAsync();
                return View(viewModel);
            }
        }

        // POST: Employee/Resign
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resign(int employeeId, DateTime? resignDate, string? resignReason)
        {
            try
            {
                var targetDate = resignDate ?? DateTime.Today;
                await _employeeService.ResignAsync(employeeId, targetDate, resignReason);
                AlertHelper.AddSuccessMessage(TempData, "Đã ghi nhận thôi việc cho nhân viên thành công.");
            }
            catch (Exception ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.Message);
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: Employee/Delete/5
        [Authorize(Roles = "Admin,HR,AreaManager,SuperAdmin")]
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
        [Authorize(Roles = "Admin,HR,AreaManager,SuperAdmin")]
        public async Task<IActionResult> DeleteConfirmed(int employeeId)
        {
            try
            {
                var employee = await _employeeService.GetRequiredByIdAsync(employeeId);
                await _employeeService.DeleteAsync(employee);
                AlertHelper.DeleteSuccess(TempData);
            }
            catch (ModelValidationException ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.GetErrorString(_localizer));
                return RedirectToAction(nameof(Delete), new { id = employeeId });
            }
            catch (Exception ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.Message);
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

            try
            {
                await _employeeService.UpdateActiveStatusAsync(viewModel.EmployeeId!.Value, viewModel.IsActive!.Value);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
