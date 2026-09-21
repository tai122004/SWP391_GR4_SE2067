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

namespace RWPM.Controllers
{
    public class AccController : Controller
    {
        private readonly IStringLocalizer _localizer;
        private readonly IAccService _accService;
        private readonly IStoreService _storeService;
        private readonly IEmployeeService _employeeService;

        public AccController( 
            IStringLocalizer<ErrorServerDefinition> localizer,
            IAccService accService,
            IStoreService storeService,
            IEmployeeService employeeService
            )
        {
            _localizer = localizer;
            _accService = accService;
            _storeService = storeService;
            _employeeService = employeeService;
        }

        // GET: Acc
        [Authorize(Roles = "Admin,HR")]
        [RemoveEmptyQueryString]
        public async Task<IActionResult> Index(AccSearch searchObject)
        {
            var result = await _accService.SearchAsync(searchObject);

            return View(new AccListVM(result, searchObject));
        }

        [Authorize]
        public async Task<IActionResult> MyProfile()
        {
            var currentAcc = await _accService.GetByIdAsync(AccountHelper.GetCurrentUsername(HttpContext.User), new QueryOptions<Acc>());

            if (currentAcc == null)
                return NotFound();

            return View(currentAcc);
        }

        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> Create()
        {
            var storeList = await _storeService.GetSelectListAsync();
            return View(new AccCreateVM()
            {
                AccountRoleSelectList = _accService.GetAccountRoleSelectListAsync(),
                StoreSelectList = storeList,
                EmployeeCode = $"NV{DateTime.Now:yyMMddHHmm}"
            });
        }

        [Authorize(Roles = "Admin,HR")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AccCreateVM viewModel)
        {
            var isStoreRole = viewModel.Role == RWPM.Common.Enums.AccountRole.StoreManager ||
                              viewModel.Role == RWPM.Common.Enums.AccountRole.SalesStaff ||
                              viewModel.Role == RWPM.Common.Enums.AccountRole.PartTimeStaff;

            if (isStoreRole)
            {
                if (!viewModel.StoreId.HasValue || viewModel.StoreId <= 0)
                {
                    ModelState.AddModelError(nameof(viewModel.StoreId), "Vui lòng chọn cửa hàng làm việc cho nhân viên này.");
                }
                if (string.IsNullOrWhiteSpace(viewModel.EmployeeCode))
                {
                    ModelState.AddModelError(nameof(viewModel.EmployeeCode), "Vui lòng nhập mã nhân viên.");
                }
            }

            if (!ModelState.IsValid)
            {
                viewModel.AccountRoleSelectList = _accService.GetAccountRoleSelectListAsync();
                viewModel.StoreSelectList = await _storeService.GetSelectListAsync();
                return View(viewModel);
            }

            try
            {
                var createdAcc = await _accService.CreateAsync(viewModel.ToEntity());

                // Nếu là vai trò nhân viên làm việc tại cửa hàng, tự động tạo luôn bản ghi Employee
                if (isStoreRole && viewModel.StoreId.HasValue)
                {
                    var emp = new Employee
                    {
                        EmployeeCode = (viewModel.EmployeeCode ?? $"NV{DateTime.Now:yyMMddHHmm}").Trim(),
                        Username = createdAcc.Username,
                        StoreId = viewModel.StoreId.Value,
                        JoinDate = viewModel.JoinDate?.Date ?? DateTime.Today,
                        IsActive = true
                    };
                    await _employeeService.CreateAsync(emp);
                }

                AlertHelper.CreateSuccess(TempData);
                TempData["CreatedAccountNotice"] = $"Đã tạo tài khoản: {createdAcc.Username} | Mật khẩu: {viewModel.Password}";
            }
            catch (ModelValidationException ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.GetErrorString(_localizer));
                viewModel.AccountRoleSelectList = _accService.GetAccountRoleSelectListAsync();
                viewModel.StoreSelectList = await _storeService.GetSelectListAsync();
                return View(viewModel);
            }
            catch (Exception ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.Message);
                viewModel.AccountRoleSelectList = _accService.GetAccountRoleSelectListAsync();
                viewModel.StoreSelectList = await _storeService.GetSelectListAsync();
                return View(viewModel);
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> Edit(string id)
        {
            var category = await _accService.GetByIdAsync(id);
            if (category == null)
                return NotFound();

            return View(new AccUpdateVM(category)
            {
                AccountRoleSelectList = _accService.GetAccountRoleSelectListAsync(),
            });
        }

        [Authorize(Roles = "Admin,HR")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, AccUpdateVM viewModel)
        {
            if (id != viewModel.Username)
                return BadRequest();

            if (!ModelState.IsValid)
            {
                viewModel.AccountRoleSelectList = _accService.GetAccountRoleSelectListAsync();
                return View(viewModel);
            }

            try
            {
                await _accService.UpdateAsync(viewModel.ToEntity());
                AlertHelper.EditSuccess(TempData);
            }
            catch (ModelValidationException ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.GetErrorString(_localizer));
                viewModel.AccountRoleSelectList = _accService.GetAccountRoleSelectListAsync();
                return View(viewModel);
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> Delete(string id)
        {
            var account = await _accService.GetByIdAsync(id);
            if (account == null)
                return NotFound();
            return View(new AccCreateVM(account));
        }

        [Authorize(Roles = "Admin,HR")]
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
                AlertHelper.AddErrorMessage(TempData, ex.GetErrorString(_localizer));
                return RedirectToAction(nameof(Delete), new { username });
            }
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> ChangePasswordAccount(string id)
        {
            var account = await _accService.GetByIdAsync(id);
            if (account == null)
                return NotFound();

            return View(new ChangePasswordAccVM(account));
        }

        [Authorize]
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

                AlertHelper.AddSuccessMessage(TempData, _localizer["Acc_ChangePasswordSuccess"]);
            }
            catch (ModelValidationException ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.GetErrorString(_localizer));
                return View(viewModel);
            }

            return RedirectToAction("Index", "Home");
        }

        [Authorize(Roles = "Admin,HR")]
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
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [Authorize(Roles = "Admin,HR")]
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
                return Ok(new { success = true, message = "Đổi mật khẩu thành công!" });
            }
            catch (ModelValidationException ex)
            {
                return BadRequest(ex.GetErrorString(_localizer));
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
