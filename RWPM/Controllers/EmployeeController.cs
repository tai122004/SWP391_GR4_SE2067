using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using RWPM.Common;
using RWPM.Common.Attributes;
using RWPM.Common.Exceptions;
using RWPM.Common.Helper;
using RWPM.Models.ViewModels.Employee;
using RWPM.Services.Abstraction;

namespace RWPM.Controllers
{
    [Authorize(Roles = "Admin,HR,AreaManager")]
    public class EmployeeController : Controller
    {
        private readonly IStringLocalizer _localizer;
        private readonly IEmployeeService _employeeService;
        private readonly IStoreService _storeService;

        public EmployeeController(
            IStringLocalizer<ErrorServerDefinition> localizer,
            IEmployeeService employeeService,
            IStoreService storeService)
        {
            _localizer = localizer;
            _employeeService = employeeService;
            _storeService = storeService;
        }

        // GET: Employee
        [RemoveEmptyQueryString]
        public async Task<IActionResult> Index(EmployeeSearch searchObject)
        {
            var result = await _employeeService.SearchAsync(searchObject);
            var storeSelectList = await _storeService.GetSelectListAsync();
            return View(new EmployeeListVM(result, searchObject, storeSelectList));
        }

        // GET: Employee/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.AccountList = await _employeeService.GetAvailableAccountsSelectListAsync();
            ViewBag.StoreList = await _storeService.GetSelectListAsync();
            return View(new EmployeeCreateVM());
        }

        // POST: Employee/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
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

            ViewBag.AccountList = await _employeeService.GetAvailableAccountsSelectListAsync(employee.Username);
            ViewBag.StoreList = await _storeService.GetSelectListAsync();
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

        // GET: Employee/Delete/5
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
