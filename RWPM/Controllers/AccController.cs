using RWPM.Common.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using RWPM.Common.Attributes;
using RWPM.Common;
using RWPM.Common.Helper;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.Acc;
using RWPM.Models.ViewModels.Acc.ChangePassword;
using RWPM.Models.ViewModels.Acc.ChangePasswordAcc;
using RWPM.Services.Abstraction;
using RWPM.Common.Models;
using RWPM.Common.Exceptions;
using RWPM.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using RWPM.Models.ViewModels.Employee;
using RWPM.Common.Enums;
using RWPM.Common.Security;
using AccRes = RWPM.Resources.Shared.Acc;
using EmpRes = RWPM.Resources.Shared.Employee;

namespace RWPM.Controllers
{
    public class AccController : Controller
    {
        private readonly IStringLocalizer _localizer;
        private readonly IAccService _accService;
        private readonly IStoreService _storeService;
        private readonly IEmployeeService _employeeService;
        private readonly DefaultDatabaseContext _context;
        private readonly IAuthService _authService;

        public AccController(
            IStringLocalizer<ErrorServerDefinition> localizer,
            IAccService accService,
            IStoreService storeService,
            IEmployeeService employeeService,
            DefaultDatabaseContext context,
            IAuthService authService
            )
        {
            _authService = authService;
            _localizer = localizer;
            _accService = accService;
            _storeService = storeService;
            _employeeService = employeeService;
            _context = context;
        }

        /// <summary>Nạp lại danh sách vai trò / chi nhánh rồi hiển thị lại form tạo tài khoản.</summary>
        private async Task<IActionResult> CreateFormView(AccCreateVM viewModel)
        {
            viewModel.AccountRoleSelectList = _accService.GetAccountRoleSelectListAsync();
            viewModel.StoreSelectList = await _storeService.GetSelectListAsync();
            return View(viewModel);
        }

        /// <summary>Nạp lại danh sách vai trò / chi nhánh rồi hiển thị lại form sửa tài khoản.</summary>
        private async Task<IActionResult> EditFormView(AccUpdateVM viewModel)
        {
            viewModel.AccountRoleSelectList = _accService.GetAccountRoleSelectListAsync();
            viewModel.StoreSelectList = await _storeService.GetSelectListAsync();
            return View(viewModel);
        }

        // GET: Acc
        [Authorize(Roles = AppRoles.ChainManagers)]
        [RemoveEmptyQueryString]
        public async Task<IActionResult> Index(AccSearch searchObject)
        {
            var result = await _accService.SearchAsync(searchObject);

            return View(new AccListVM(result, searchObject));
        }

        [Authorize(Roles = AppRoles.ChainManagers)]
        [HttpGet]
        public async Task<IActionResult> DownloadTemplate()
        {
            var fileBytes = await _employeeService.GenerateImportTemplateAsync();
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{InputRules.Text("Mau_Nhap_Nhan_Vien", "Employee_Import_Template")}_{DateTime.Now:yyyyMMdd}.xlsx");
        }

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

        [Authorize]
        public async Task<IActionResult> MyProfile()
        {
            var username = AccountHelper.GetCurrentUsername(HttpContext.User);
            var currentAcc = await _accService.GetByIdAsync(username, new QueryOptions<Acc>());

            if (currentAcc == null)
                return NotFound();

            var currentEmp = await _context.Employee.Include(e => e.Store).FirstOrDefaultAsync(e => e.Username == username);
            ViewBag.Employee = currentEmp;

            return View(currentAcc);
        }

        [Authorize(Roles = AppRoles.ChainManagers)]
        public async Task<IActionResult> Create()
        {
            var storeList = await _storeService.GetSelectListAsync();
            return View(new AccCreateVM()
            {
                AccountRoleSelectList = _accService.GetAccountRoleSelectListAsync(),
                StoreSelectList = storeList,
                EmployeeCode = $"NV{DateTime.Now:yyMMddHHmm}",
                HourlyRate = 25000,
                JoinDate = DateTime.Today
            });
        }

        [Authorize(Roles = AppRoles.ChainManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AccCreateVM viewModel)
        {
            var isStoreRole = viewModel.Role == AccountRole.StoreManager ||
                              viewModel.Role == AccountRole.Employee;
            var isHrRole = viewModel.Role == AccountRole.HR;

            if (isStoreRole)
            {
                if (!viewModel.StoreId.HasValue || viewModel.StoreId <= 0)
                {
                    ModelState.AddModelError(nameof(viewModel.StoreId), AccRes.Err_SelectStoreForEmployee);
                }
                if (string.IsNullOrWhiteSpace(viewModel.EmployeeCode))
                {
                    ModelState.AddModelError(nameof(viewModel.EmployeeCode), AccRes.Err_EmployeeCodeRequired);
                }
                else
                {
                    var codeExists = await _context.Employee.AnyAsync(e => e.EmployeeCode == viewModel.EmployeeCode.Trim());
                    if (codeExists)
                    {
                        ModelState.AddModelError(nameof(viewModel.EmployeeCode), string.Format(AccRes.Err_EmployeeCodeExists, viewModel.EmployeeCode));
                    }
                }
                if (!viewModel.HourlyRate.HasValue || viewModel.HourlyRate <= 0)
                {
                    ModelState.AddModelError(nameof(viewModel.HourlyRate), InputRules.Text(
                        "Vui lòng nhập mức lương theo giờ (lớn hơn 0) để có thể tính lương cho nhân viên.",
                        "Please enter an hourly rate greater than 0 so that payroll can be calculated."));
                }
            }
            else if (isHrRole)
            {
                // HR có thể chọn chi nhánh hoặc mặc định chi nhánh đầu tiên
                if (!viewModel.StoreId.HasValue || viewModel.StoreId <= 0)
                {
                    viewModel.StoreId = await _context.Store.Where(s => s.IsActive).Select(s => (int?)s.StoreId).FirstOrDefaultAsync();
                }
                viewModel.EmploymentType = EmploymentType.FullTime;

                if (string.IsNullOrWhiteSpace(viewModel.EmployeeCode))
                {
                    ModelState.AddModelError(nameof(viewModel.EmployeeCode), AccRes.Err_EmployeeCodeRequiredHr);
                }
                else
                {
                    var codeExists = await _context.Employee.AnyAsync(e => e.EmployeeCode == viewModel.EmployeeCode.Trim());
                    if (codeExists)
                    {
                        ModelState.AddModelError(nameof(viewModel.EmployeeCode), string.Format(AccRes.Err_EmployeeCodeExists, viewModel.EmployeeCode));
                    }
                }

                if (!viewModel.HourlyRate.HasValue || viewModel.HourlyRate <= 0)
                {
                    ModelState.AddModelError(nameof(viewModel.HourlyRate), AccRes.Err_HourlyRateRequiredHrCreate);
                }
            }

            // Kiểm tra trùng Email
            if (!string.IsNullOrWhiteSpace(viewModel.Email))
            {
                var trimmedEmail = viewModel.Email.Trim().ToLower();
                var emailExists = await _context.Acc.AnyAsync(a => a.Email.ToLower() == trimmedEmail);
                if (emailExists)
                {
                    ModelState.AddModelError(nameof(viewModel.Email), string.Format(AccRes.Err_EmailInUse, viewModel.Email));
                }
            }

            // Kiểm tra trùng Số điện thoại
            if (!string.IsNullOrWhiteSpace(viewModel.PhoneNumber))
            {
                var trimmedPhone = viewModel.PhoneNumber.Trim();
                var phoneExists = await _context.Acc.AnyAsync(a => a.PhoneNumber == trimmedPhone);
                if (phoneExists)
                {
                    ModelState.AddModelError(nameof(viewModel.PhoneNumber), string.Format(AccRes.Err_PhoneInUse, viewModel.PhoneNumber));
                }
            }

            try
            {
                InputRules.ValidateBirthDate(viewModel.DateOfBirth);
                InputRules.Require(viewModel.JoinDate.HasValue, "JoinDate", "Chưa chọn ngày vào làm.", "Select a join date.");
                InputRules.ValidateEmployment(viewModel.EmployeeCode ?? "", viewModel.JoinDate ?? default, viewModel.DateOfBirth,
                    null, viewModel.EmploymentType, EmploymentStatus.Official, viewModel.HourlyRate, null, 12, viewModel.CitizenId);
            }
            catch (InputValidationException ex) { ModelState.AddModelError(ex.Field, ex.Message); }

            // Kiểm tra trùng số CCCD/CMND
            if (!string.IsNullOrWhiteSpace(viewModel.CitizenId)
                && await _context.Employee.AnyAsync(e => e.CitizenId == viewModel.CitizenId))
            {
                ModelState.AddModelError(nameof(viewModel.CitizenId), InputRules.Text("Số CCCD/CMND đã được dùng cho nhân viên khác.", "This citizen ID is already used by another employee."));
            }

            if (!ModelState.IsValid)
            {
                return await CreateFormView(viewModel);
            }

            try
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                var createdAcc = await _accService.CreateAsync(viewModel.ToEntity());

                // Nếu là vai trò nhân viên làm việc tại cửa hàng hoặc HR, tự động tạo hồ sơ Employee
                if (isStoreRole || isHrRole)
                {
                    var emp = new Employee
                    {
                        EmployeeCode = (viewModel.EmployeeCode ?? $"NV{DateTime.Now:yyMMddHHmm}").Trim(),
                        Username = createdAcc.Username,
                        StoreId = viewModel.StoreId,
                        JoinDate = viewModel.JoinDate?.Date ?? DateTime.Today,
                        EmploymentType = isHrRole ? EmploymentType.FullTime : viewModel.EmploymentType,
                        HourlyRate = viewModel.HourlyRate,
                        CitizenId = viewModel.CitizenId,
                        IsActive = true
                    };
                    await _employeeService.CreateAsync(emp);
                }

                await transaction.CommitAsync();
                AlertHelper.CreateSuccess(TempData);
                TempData["CreatedAccountNotice"] = string.Format(AccRes.Notice_CreatedAccount, createdAcc.Username, viewModel.Password);
            }
            catch (ModelValidationException ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.GetErrorString(_localizer));
                return await CreateFormView(viewModel);
            }
            catch (Exception ex)
            {
                if (ex is InputValidationException input) ModelState.AddModelError(input.Field, input.Message);
                else if (InputRules.Duplicate(ex) is { } duplicate) ModelState.AddModelError(duplicate.Field, duplicate.Message);
                else AlertHelper.AddErrorMessage(TempData, InputRules.UserMessage(ex));
                return await CreateFormView(viewModel);
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = AppRoles.ChainManagers)]
        public async Task<IActionResult> Edit(string id)
        {
            var account = await _accService.GetByIdAsync(id);
            if (account == null)
                return NotFound();

            var vm = new AccUpdateVM(account)
            {
                AccountRoleSelectList = _accService.GetAccountRoleSelectListAsync(),
                StoreSelectList = await _storeService.GetSelectListAsync()
            };

            var existingEmp = await _context.Employee.FirstOrDefaultAsync(e => e.Username == id);
            if (existingEmp != null)
            {
                vm.HasExistingEmployee = true;
                vm.EmployeeId = existingEmp.EmployeeId;
                vm.EmployeeCode = existingEmp.EmployeeCode;
                vm.StoreId = existingEmp.StoreId;
                vm.JoinDate = existingEmp.JoinDate;
                vm.EmploymentType = existingEmp.EmploymentType;
                vm.HourlyRate = existingEmp.HourlyRate;
                vm.CitizenId = existingEmp.CitizenId;
            }
            else
            {
                vm.HasExistingEmployee = false;
                vm.EmployeeCode = $"NV{DateTime.Now:yyMMddHHmm}";
                vm.JoinDate = DateTime.Today;
                vm.HourlyRate = 25000;
            }

            return View(vm);
        }

        [Authorize(Roles = AppRoles.ChainManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, AccUpdateVM viewModel)
        {
            if (id != viewModel.Username)
                return BadRequest();

            var isStoreRole = viewModel.Role == AccountRole.StoreManager ||
                              viewModel.Role == AccountRole.Employee;
            var isHrRole = viewModel.Role == AccountRole.HR;

            if (isStoreRole)
            {
                if (!viewModel.StoreId.HasValue || viewModel.StoreId <= 0)
                {
                    ModelState.AddModelError(nameof(viewModel.StoreId), AccRes.Err_SelectStoreForEmployee);
                }
                if (string.IsNullOrWhiteSpace(viewModel.EmployeeCode))
                {
                    ModelState.AddModelError(nameof(viewModel.EmployeeCode), AccRes.Err_EmployeeCodeRequired);
                }
                else
                {
                    var duplicateEmp = await _context.Employee.AnyAsync(e => e.EmployeeCode == viewModel.EmployeeCode.Trim() && e.Username != viewModel.Username);
                    if (duplicateEmp)
                    {
                        ModelState.AddModelError(nameof(viewModel.EmployeeCode), string.Format(AccRes.Err_EmployeeCodeUsedByOther, viewModel.EmployeeCode));
                    }
                }
                // Bắt buộc đơn giá khi tạo mới hồ sơ hoặc khi nhập giá trị không hợp lệ; hồ sơ cũ (import/legacy) chưa có đơn giá vẫn sửa được thông tin khác
                var hasProfile = await _context.Employee.AnyAsync(e => e.Username == viewModel.Username);
                if ((!hasProfile && (!viewModel.HourlyRate.HasValue || viewModel.HourlyRate <= 0))
                    || (viewModel.HourlyRate.HasValue && viewModel.HourlyRate <= 0))
                {
                    ModelState.AddModelError(nameof(viewModel.HourlyRate), InputRules.Text(
                        "Vui lòng nhập mức lương theo giờ (lớn hơn 0) để có thể tính lương cho nhân viên.",
                        "Please enter an hourly rate greater than 0 so that payroll can be calculated."));
                }
            }
            else if (isHrRole)
            {
                if (!viewModel.StoreId.HasValue || viewModel.StoreId <= 0)
                {
                    var existingStore = await _context.Employee.Where(e => e.Username == viewModel.Username).Select(e => e.StoreId).FirstOrDefaultAsync();
                    viewModel.StoreId = existingStore ?? await _context.Store.Where(s => s.IsActive).Select(s => (int?)s.StoreId).FirstOrDefaultAsync();
                }
                viewModel.EmploymentType = EmploymentType.FullTime;

                if (string.IsNullOrWhiteSpace(viewModel.EmployeeCode))
                {
                    ModelState.AddModelError(nameof(viewModel.EmployeeCode), AccRes.Err_EmployeeCodeRequiredHr);
                }
                else
                {
                    var duplicateEmp = await _context.Employee.AnyAsync(e => e.EmployeeCode == viewModel.EmployeeCode.Trim() && e.Username != viewModel.Username);
                    if (duplicateEmp)
                    {
                        ModelState.AddModelError(nameof(viewModel.EmployeeCode), string.Format(AccRes.Err_EmployeeCodeUsedByOther, viewModel.EmployeeCode));
                    }
                }

                if (!viewModel.HourlyRate.HasValue || viewModel.HourlyRate <= 0)
                {
                    ModelState.AddModelError(nameof(viewModel.HourlyRate), AccRes.Err_HourlyRateRequiredHrEdit);
                }
            }

            // Kiểm tra trùng Email
            if (!string.IsNullOrWhiteSpace(viewModel.Email))
            {
                var trimmedEmail = viewModel.Email.Trim().ToLower();
                var emailExists = await _context.Acc.AnyAsync(a => a.Username != id && a.Email.ToLower() == trimmedEmail);
                if (emailExists)
                {
                    ModelState.AddModelError(nameof(viewModel.Email), string.Format(AccRes.Err_EmailInUse, viewModel.Email));
                }
            }

            // Kiểm tra trùng Số điện thoại
            if (!string.IsNullOrWhiteSpace(viewModel.PhoneNumber))
            {
                var trimmedPhone = viewModel.PhoneNumber.Trim();
                var phoneExists = await _context.Acc.AnyAsync(a => a.Username != id && a.PhoneNumber == trimmedPhone);
                if (phoneExists)
                {
                    ModelState.AddModelError(nameof(viewModel.PhoneNumber), string.Format(AccRes.Err_PhoneInUse, viewModel.PhoneNumber));
                }
            }

            if (isStoreRole || isHrRole)
            {
                try
                {
                    InputRules.ValidateBirthDate(viewModel.DateOfBirth);
                    InputRules.Require(viewModel.JoinDate.HasValue, "JoinDate", "Chưa chọn ngày vào làm.", "Select a join date.");
                    InputRules.ValidateEmployment(viewModel.EmployeeCode ?? "", viewModel.JoinDate ?? default, viewModel.DateOfBirth,
                        null, viewModel.EmploymentType, EmploymentStatus.Official, viewModel.HourlyRate, null, 12, viewModel.CitizenId);
                }
                catch (InputValidationException ex) { ModelState.AddModelError(ex.Field, ex.Message); }

                // Kiểm tra trùng số CCCD/CMND với nhân viên khác
                if (!string.IsNullOrWhiteSpace(viewModel.CitizenId)
                    && await _context.Employee.AnyAsync(e => e.CitizenId == viewModel.CitizenId && e.Username != viewModel.Username))
                {
                    ModelState.AddModelError(nameof(viewModel.CitizenId), InputRules.Text("Số CCCD/CMND đã được dùng cho nhân viên khác.", "This citizen ID is already used by another employee."));
                }
            }
            else
            {
                try { InputRules.ValidateBirthDate(viewModel.DateOfBirth); }
                catch (InputValidationException ex) { ModelState.AddModelError(ex.Field, ex.Message); }
            }

            if (!ModelState.IsValid)
            {
                return await EditFormView(viewModel);
            }

            try
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                await _accService.UpdateAsync(viewModel.ToEntity());

                // Xử lý hồ sơ nhân viên (Employee)
                var existingEmp = await _context.Employee.FirstOrDefaultAsync(e => e.Username == viewModel.Username);
                if (existingEmp != null)
                {
                    if (isStoreRole || isHrRole)
                    {
                        existingEmp.StoreId = viewModel.StoreId;
                        if (!string.IsNullOrWhiteSpace(viewModel.EmployeeCode))
                        {
                            existingEmp.EmployeeCode = viewModel.EmployeeCode.Trim();
                        }
                        if (viewModel.JoinDate.HasValue)
                        {
                            existingEmp.JoinDate = viewModel.JoinDate.Value.Date;
                        }
                        existingEmp.EmploymentType = isHrRole ? EmploymentType.FullTime : viewModel.EmploymentType;
                        existingEmp.HourlyRate = viewModel.HourlyRate;
                        existingEmp.CitizenId = viewModel.CitizenId;
                        existingEmp.UpdatedDate = DateTime.Now;
                        existingEmp.UpdatedBy = AccountHelper.GetCurrentUsername(HttpContext.User);

                        await _employeeService.UpdateAsync(existingEmp);
                    }
                }
                else if (isStoreRole || isHrRole)
                {
                    // Trường hợp tài khoản chưa có hoặc từng bị xóa hồ sơ nhân viên: Tự động tái tạo và liên kết lại
                    var newEmp = new Employee
                    {
                        EmployeeCode = (viewModel.EmployeeCode ?? $"NV{DateTime.Now:yyMMddHHmm}").Trim(),
                        Username = viewModel.Username,
                        StoreId = viewModel.StoreId,
                        JoinDate = viewModel.JoinDate?.Date ?? DateTime.Today,
                        EmploymentType = isHrRole ? EmploymentType.FullTime : viewModel.EmploymentType,
                        HourlyRate = viewModel.HourlyRate,
                        CitizenId = viewModel.CitizenId,
                        IsActive = true
                    };
                    await _employeeService.CreateAsync(newEmp);
                }

                // Vai trò mới không còn quyền quản lý chi nhánh: gỡ khỏi vị trí quản lý các chi nhánh đang phụ trách
                if (existingEmp != null && viewModel.Role is not (AccountRole.StoreManager or AccountRole.HR or AccountRole.Admin))
                {
                    var managedStores = await _context.Store.Where(s => s.ManagerId == existingEmp.EmployeeId).ToListAsync();
                    if (managedStores.Count > 0)
                    {
                        foreach (var managedStore in managedStores)
                        {
                            managedStore.ManagerId = null;
                            managedStore.UpdatedDate = DateTime.Now;
                            managedStore.UpdatedBy = AccountHelper.GetCurrentUsername(HttpContext.User);
                        }
                        await _context.SaveChangesAsync();
                    }
                }

                await transaction.CommitAsync();
                AlertHelper.EditSuccess(TempData);
            }
            catch (ModelValidationException ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.GetErrorString(_localizer));
                return await EditFormView(viewModel);
            }
            catch (Exception ex)
            {
                if (ex is InputValidationException input) ModelState.AddModelError(input.Field, input.Message);
                else if (InputRules.Duplicate(ex) is { } duplicate) ModelState.AddModelError(duplicate.Field, duplicate.Message);
                else AlertHelper.AddErrorMessage(TempData, InputRules.UserMessage(ex));
                return await EditFormView(viewModel);
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = AppRoles.ChainManagers)]
        public async Task<IActionResult> Delete(string id)
        {
            var account = await _accService.GetByIdAsync(id);
            if (account == null)
                return NotFound();
            return View(new AccCreateVM(account));
        }

        [Authorize(Roles = AppRoles.ChainManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string username)
        {
            try
            {
                var account = await _accService.GetRequiredByIdAsync(username);
                if(account == null)
                    return NotFound();
                await _accService.DeleteAsync(account);
                AlertHelper.DeleteSuccess(TempData);
            }
            catch(ModelValidationException ex)
            {
                // Xóa được xác nhận bằng popup ngay tại trang danh sách -> lỗi cũng hiển thị bằng toast tại đó
                AlertHelper.AddErrorMessage(TempData, ex.GetErrorString(_localizer));
            }
            catch (Exception ex)
            {
                AlertHelper.AddErrorMessage(TempData, InputRules.UserMessage(ex));
            }
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = AppRoles.ChainManagers)]
        public async Task<IActionResult> ChangePasswordAccount(string id)
        {
            var account = await _accService.GetByIdAsync(id);
            if (account == null)
                return NotFound();

            return View(new ChangePasswordAccVM(account));
        }

        [Authorize(Roles = AppRoles.ChainManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePasswordAccount(string id, ChangePasswordAccVM viewModel)
        {
            viewModel.Username = id;
            var account = await _accService.GetByIdAsync(id);
            if (account == null)
                return NotFound();

            if (!ModelState.IsValid)
            {
                viewModel.ApplyEntityValue(account);
                return View(viewModel);
            }

            try
            {
                await _accService.ChangePasswordForAccountAsync(viewModel.ToDto());
                AlertHelper.AddSuccessMessage(TempData, _localizer["Acc_ChangePasswordSuccess"]);
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InputValidationException ex)
            {
                ModelState.AddModelError(ex.Field, ex.Message);
                viewModel.ApplyEntityValue(account);
                return View(viewModel);
            }
            catch (ModelValidationException ex)
            {
                viewModel.ApplyEntityValue(account);
                AlertHelper.AddErrorMessage(TempData, ex.GetErrorString(_localizer));
                return View(viewModel);
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordVM viewModel)
        {
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            try
            {
                await _accService.ChangePasswordAsync(viewModel.OldPassword,
                                                      viewModel.NewPassword,
                                                      viewModel.ConfirmPassword);
                // Cấp lại cookie với SecurityStamp mới và gỡ cờ bắt buộc đổi mật khẩu cho phiên hiện tại
                await _authService.RefreshSignInAsync(AccountHelper.GetCurrentUsername(HttpContext.User));

                AlertHelper.AddSuccessMessage(TempData, _localizer["Acc_ChangePasswordSuccess"]);
            }
            catch (InputValidationException ex)
            {
                ModelState.AddModelError(ex.Field, ex.Message);

                return View(viewModel);
            }
            catch (ModelValidationException ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.GetErrorString(_localizer));
                return View(viewModel);
            }

            return RedirectToAction("Index", "Home");
        }

        [Authorize(Roles = AppRoles.ChainManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateActiveStatus(string id, [FromBody] AccUpdateActiveStatusVM viewModel)
        {
            if (id != viewModel.Username)
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
                await _accService.UpdateActiveStatusAsync(viewModel.Username!, viewModel.IsActive!.Value);
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

        [Authorize(Roles = AppRoles.ChainManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickResetPassword([FromBody] QuickResetPasswordVM request)
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState.Values
                                    .SelectMany(v => v.Errors)
                                    .Select(e => e.ErrorMessage));
                return BadRequest(errors);
            }

            try
            {
                await _accService.ChangePasswordForAccountAsync(new ChangePasswordAccDto
                {
                    Username = request.Username,
                    NewPassword = request.NewPassword,
                    ConfirmPassword = request.ConfirmPassword
                });
                return Ok(new { success = true, message = AccRes.Toast_ChangePasswordSuccess });
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

