using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using RWPM.Common;
using RWPM.Common.Attributes;
using RWPM.Common.Exceptions;
using RWPM.Common.Helper;
using RWPM.Models.ViewModels.Store;
using RWPM.Services.Abstraction;

namespace RWPM.Controllers
{
    [Authorize(Roles = "Admin,HR,AreaManager")]
    public class StoreController : Controller
    {
        private readonly IStringLocalizer _localizer;
        private readonly IStoreService _storeService;

        public StoreController(
            IStringLocalizer<ErrorServerDefinition> localizer,
            IStoreService storeService)
        {
            _localizer = localizer;
            _storeService = storeService;
        }

        // GET: Store
        [RemoveEmptyQueryString]
        public async Task<IActionResult> Index(StoreSearch searchObject)
        {
            var result = await _storeService.SearchAsync(searchObject);
            return View(new StoreListVM(result, searchObject));
        }

        // GET: Store/Create
        public IActionResult Create()
        {
            return View(new StoreCreateVM());
        }

        // POST: Store/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StoreCreateVM viewModel)
        {
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            try
            {
                await _storeService.CreateAsync(viewModel.ToEntity());
                AlertHelper.CreateSuccess(TempData);
            }
            catch (ModelValidationException ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.GetErrorString(_localizer));
                return View(viewModel);
            }
            catch (Exception ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.Message);
                return View(viewModel);
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Store/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var store = await _storeService.GetByIdAsync(id);
            if (store == null)
                return NotFound();

            return View(new StoreEditVM(store));
        }

        // POST: Store/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, StoreEditVM viewModel)
        {
            if (id != viewModel.StoreId)
                return BadRequest();

            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            try
            {
                var store = await _storeService.GetRequiredByIdAsync(id);
                viewModel.ApplyToEntity(store);
                await _storeService.UpdateAsync(store);
                AlertHelper.EditSuccess(TempData);
            }
            catch (ModelValidationException ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.GetErrorString(_localizer));
                return View(viewModel);
            }
            catch (Exception ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.Message);
                return View(viewModel);
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Store/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var store = await _storeService.GetByIdAsync(id);
            if (store == null)
                return NotFound();

            return View(store);
        }

        // POST: Store/DeleteConfirmed
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int storeId)
        {
            try
            {
                var store = await _storeService.GetRequiredByIdAsync(storeId);
                await _storeService.DeleteAsync(store);
                AlertHelper.DeleteSuccess(TempData);
            }
            catch (ModelValidationException ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.GetErrorString(_localizer));
                return RedirectToAction(nameof(Delete), new { id = storeId });
            }
            catch (Exception ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.Message);
                return RedirectToAction(nameof(Delete), new { id = storeId });
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Store/UpdateActiveStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateActiveStatus(int id, [FromBody] StoreUpdateActiveStatusVM viewModel)
        {
            if (id != viewModel.StoreId)
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
                await _storeService.UpdateActiveStatusAsync(viewModel.StoreId!.Value, viewModel.IsActive!.Value);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
