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
    [Authorize(Roles = "Admin,HR,StoreManager")]
    public class ShiftController : Controller
    {
        private readonly IStringLocalizer _localizer;
        private readonly IShiftService _shiftService;
        private readonly IStoreService _storeService;
        private bool IsStoreManager => User.IsInRole("StoreManager") && !User.IsInRole("Admin");

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
            var managedStoreId = IsStoreManager ? await _shiftService.GetManagedStoreIdAsync() : null;
            if (IsStoreManager && !managedStoreId.HasValue) return Forbid();
            var result = await _shiftService.SearchAsync(searchObject);
            var storeSelectList = await _storeService.GetSelectListAsync();
            if (managedStoreId.HasValue)
                storeSelectList = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                    storeSelectList.Where(x => x.Value == managedStoreId.Value.ToString()), "Value", "Text");
            ViewBag.ManagedStoreId = managedStoreId;
            ViewBag.CanManageShifts = !User.IsInRole("HR") || User.IsInRole("Admin") || IsStoreManager;
            return View(new ShiftListVM(result, searchObject, storeSelectList));
        }

        // GET: Shift/Create
        [Authorize(Roles = "Admin,StoreManager")]
        public async Task<IActionResult> Create()
        {
            var managedStoreId = IsStoreManager ? await _shiftService.GetManagedStoreIdAsync() : null;
            if (IsStoreManager && !managedStoreId.HasValue) return Forbid();
            ViewBag.StoreList = await _storeService.GetSelectListAsync();
            ViewBag.ManagedStoreId = managedStoreId;
            return View(new ShiftCreateVM { StoreIds = managedStoreId.HasValue ? new List<int> { managedStoreId.Value } : new List<int>() });
        }

        // POST: Shift/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,StoreManager")]
        public async Task<IActionResult> Create(ShiftCreateVM viewModel)
        {
            var managedStoreId = IsStoreManager ? await _shiftService.GetManagedStoreIdAsync() : null;
            if (IsStoreManager && (!managedStoreId.HasValue || viewModel.StoreIds.Count != 1 || viewModel.StoreIds[0] != managedStoreId.Value)) return Forbid();
            ViewBag.ManagedStoreId = managedStoreId;
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
                AlertHelper.AddErrorMessage(TempData, _localizer[ex.ErrorCode].ResourceNotFound ? ex.ErrorDetail : ex.GetErrorString(_localizer));
                ViewBag.StoreList = await _storeService.GetSelectListAsync();
                return View(viewModel);
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (Exception ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.Message);
                ViewBag.StoreList = await _storeService.GetSelectListAsync();
                return View(viewModel);
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Shift/Edit/5
        [Authorize(Roles = "Admin,StoreManager")]
        public async Task<IActionResult> Edit(int id)
        {
            var shift = await _shiftService.GetByIdAsync(id);
            if (shift == null)
                return NotFound();

            var managedStoreId = IsStoreManager ? await _shiftService.GetManagedStoreIdAsync() : null;
            if (IsStoreManager && (!managedStoreId.HasValue || shift.StoreShifts.Count != 1
                || shift.StoreShifts.Single().StoreId != managedStoreId.Value)) return Forbid();

            ViewBag.StoreList = await _storeService.GetSelectListAsync();
            ViewBag.ManagedStoreId = managedStoreId;
            return View(new ShiftEditVM(shift));
        }

        // POST: Shift/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,StoreManager")]
        public async Task<IActionResult> Edit(int id, ShiftEditVM viewModel)
        {
            if (id != viewModel.ShiftId)
                return BadRequest();

            var managedStoreId = IsStoreManager ? await _shiftService.GetManagedStoreIdAsync() : null;
            if (IsStoreManager && (!managedStoreId.HasValue || viewModel.StoreIds.Count != 1 || viewModel.StoreIds[0] != managedStoreId.Value)) return Forbid();
            ViewBag.ManagedStoreId = managedStoreId;

            if (!ModelState.IsValid)
            {
                ViewBag.StoreList = await _storeService.GetSelectListAsync();
                return View(viewModel);
            }

            try
            {
                var candidate = new RWPM.Models.Entities.Shift { ShiftId = id };
                viewModel.ApplyToEntity(candidate);
                await _shiftService.UpdateAsync(candidate);
                AlertHelper.EditSuccess(TempData);
            }
            catch (ModelValidationException ex)
            {
                AlertHelper.AddErrorMessage(TempData, _localizer[ex.ErrorCode].ResourceNotFound ? ex.ErrorDetail : ex.GetErrorString(_localizer));
                ViewBag.StoreList = await _storeService.GetSelectListAsync();
                return View(viewModel);
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
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
        [Authorize(Roles = "Admin,StoreManager")]
        public IActionResult Delete(int id)
        {
            AlertHelper.AddErrorMessage(TempData, "Hệ thống áp dụng chính sách Bảo toàn dữ liệu (Soft Delete). Ca làm việc không hỗ trợ xóa vĩnh viễn khỏi hệ thống, vui lòng gạt tắt công tắc Trạng thái sang 'Ngừng hoạt động'.");
            return RedirectToAction(nameof(Index));
        }

        // POST: Shift/DeleteConfirmed (Chính sách Bảo toàn dữ liệu - Chặn xóa cứng)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,StoreManager")]
        public IActionResult DeleteConfirmed(int shiftId)
        {
            AlertHelper.AddErrorMessage(TempData, "Hệ thống áp dụng chính sách Bảo toàn dữ liệu (Soft Delete). Ca làm việc không hỗ trợ xóa vĩnh viễn khỏi hệ thống, vui lòng gạt tắt công tắc Trạng thái sang 'Ngừng hoạt động'.");
            return RedirectToAction(nameof(Index));
        }


        // POST: Shift/UpdateActiveStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,StoreManager")]
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
            catch (ModelValidationException ex)
            {
                return BadRequest(_localizer[ex.ErrorCode].ResourceNotFound ? ex.ErrorDetail : ex.GetErrorString(_localizer));
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
