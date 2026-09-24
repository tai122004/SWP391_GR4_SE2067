using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using RWPM.Common;
using RWPM.Common.Attributes;
using RWPM.Common.Exceptions;
using RWPM.Common.Helper;
using RWPM.Models.ViewModels.Shift;
using RWPM.Services.Abstraction;

namespace RWPM.Controllers
{
    [Authorize(Roles = "Admin,HR,AreaManager")]
    public class ShiftController : Controller
    {
        private readonly IStringLocalizer _localizer;
        private readonly IShiftService _shiftService;
        private readonly IStoreService _storeService;

        public ShiftController(
            IStringLocalizer<ErrorServerDefinition> localizer,
            IShiftService shiftService,
            IStoreService storeService)
        {
            _localizer = localizer;
            _shiftService = shiftService;
            _storeService = storeService;
        }

        // GET: Shift
        [RemoveEmptyQueryString]
        public async Task<IActionResult> Index(ShiftSearch searchObject)
        {
            var result = await _shiftService.SearchAsync(searchObject);
            var storeSelectList = await _storeService.GetSelectListAsync();
            return View(new ShiftListVM(result, searchObject, storeSelectList));
        }

        // GET: Shift/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.StoreList = await _storeService.GetSelectListAsync();
            return View(new ShiftCreateVM());
        }

        // POST: Shift/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ShiftCreateVM viewModel)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.StoreList = await _storeService.GetSelectListAsync();
                return View(viewModel);
            }

            try
            {
                await _shiftService.CreateAsync(viewModel.ToEntity());
                AlertHelper.CreateSuccess(TempData);
            }
            catch (ModelValidationException ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.GetErrorString(_localizer));
                ViewBag.StoreList = await _storeService.GetSelectListAsync();
                return View(viewModel);
            }
            catch (Exception ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.Message);
                ViewBag.StoreList = await _storeService.GetSelectListAsync();
                return View(viewModel);
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Shift/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var shift = await _shiftService.GetByIdAsync(id);
            if (shift == null)
                return NotFound();

            ViewBag.StoreList = await _storeService.GetSelectListAsync();
            return View(new ShiftEditVM(shift));
        }

        // POST: Shift/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ShiftEditVM viewModel)
        {
            if (id != viewModel.ShiftId)
                return BadRequest();

            if (!ModelState.IsValid)
            {
                ViewBag.StoreList = await _storeService.GetSelectListAsync();
                return View(viewModel);
            }

            try
            {
                var shift = await _shiftService.GetRequiredByIdAsync(id);
                viewModel.ApplyToEntity(shift);
                await _shiftService.UpdateAsync(shift);
                AlertHelper.EditSuccess(TempData);
            }
            catch (ModelValidationException ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.GetErrorString(_localizer));
                ViewBag.StoreList = await _storeService.GetSelectListAsync();
                return View(viewModel);
            }
            catch (Exception ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.Message);
                ViewBag.StoreList = await _storeService.GetSelectListAsync();
                return View(viewModel);
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Shift/Delete/5 (Chính sách Bảo toàn dữ liệu - Không hỗ trợ xóa cứng)
        [HttpGet]
        public IActionResult Delete(int id)
        {
            AlertHelper.AddErrorMessage(TempData, "Hệ thống áp dụng chính sách Bảo toàn dữ liệu (Soft Delete). Ca làm việc không hỗ trợ xóa vĩnh viễn khỏi hệ thống, vui lòng gạt tắt công tắc Trạng thái sang 'Ngừng hoạt động'.");
            return RedirectToAction(nameof(Index));
        }

        // POST: Shift/DeleteConfirmed (Chính sách Bảo toàn dữ liệu - Chặn xóa cứng)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int shiftId)
        {
            AlertHelper.AddErrorMessage(TempData, "Hệ thống áp dụng chính sách Bảo toàn dữ liệu (Soft Delete). Ca làm việc không hỗ trợ xóa vĩnh viễn khỏi hệ thống, vui lòng gạt tắt công tắc Trạng thái sang 'Ngừng hoạt động'.");
            return RedirectToAction(nameof(Index));
        }

        // POST: Shift/UpdateActiveStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateActiveStatus(int id, [FromBody] ShiftUpdateActiveStatusVM viewModel)
        {
            if (id != viewModel.ShiftId)
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
                await _shiftService.UpdateActiveStatusAsync(viewModel.ShiftId!.Value, viewModel.IsActive!.Value);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
