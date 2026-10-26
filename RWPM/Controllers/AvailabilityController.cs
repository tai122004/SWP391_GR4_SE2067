using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RWPM.Common;
using RWPM.Common.Enums;
using RWPM.Models.ViewModels.Availability;
using RWPM.Services.Abstraction;
using R = RWPM.Resources.Shared.AvailabilityResource;

namespace RWPM.Controllers
{
    /// <summary>
    /// Thời gian có thể làm việc.
    /// - My: mọi người có hồ sơ nhân viên tự khai khung giờ hằng tuần (cần duyệt) và ngày không thể làm (không cần duyệt).
    /// - Review: Quản lý cửa hàng (chi nhánh mình) / HR / Admin (tất cả) duyệt khung giờ.
    /// - Employee / EmployeeSummary: xem một nhân viên (kiểm tra phạm vi theo WorkflowGuard).
    /// </summary>
    [Authorize]
    public class AvailabilityController : Controller
    {
        private readonly IAvailabilityService _availability;
        private readonly IStoreService _storeService;
        private readonly ILogger<AvailabilityController> _logger;

        public AvailabilityController(IAvailabilityService availability, IStoreService storeService, ILogger<AvailabilityController> logger)
        {
            _availability = availability;
            _storeService = storeService;
            _logger = logger;
        }

        private string CurrentUsername => User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? string.Empty;

        // GET: /Availability/My
        [HttpGet]
        public async Task<IActionResult> My()
        {
            var vm = await _availability.GetMyAsync(CurrentUsername);
            return View(vm);
        }

        private async Task<IActionResult> MyWithError(string message, AvailabilityWindowInput? window = null, UnavailabilityInput? date = null)
        {
            AlertHelper.AddErrorMessage(TempData, message);
            ViewBag.WindowInput = window;
            ViewBag.DateInput = date;
            ViewBag.FormError = message;
            ViewBag.FormErrorTarget = window != null ? "window" : date != null ? "date" : null;
            return View(nameof(My), await _availability.GetMyAsync(CurrentUsername));
        }

        // POST: /Availability/AddWindows
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddWindows(AvailabilityWindowInput input)
        {
            input ??= new AvailabilityWindowInput();
            try
            {
                var ids = await _availability.AddWindowsAsync(CurrentUsername, input);
                AlertHelper.AddSuccessMessage(TempData, string.Format(R.Toast_WindowsAdded, ids.Count));
                return RedirectToAction(nameof(My));
            }
            catch (ValidationException ex) { return await MyWithError(ex.Message, window: input); }
            catch (UnauthorizedAccessException ex) { return await MyWithError(ex.Message, window: input); }
        }

        // POST: /Availability/DeleteWindow
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteWindow(int id)
        {
            try
            {
                await _availability.DeleteWindowAsync(CurrentUsername, id);
                AlertHelper.AddSuccessMessage(TempData, R.Toast_WindowDeleted);
            }
            catch (ValidationException ex) { AlertHelper.AddErrorMessage(TempData, ex.Message); }
            catch (UnauthorizedAccessException ex) { AlertHelper.AddErrorMessage(TempData, ex.Message); }
            return RedirectToAction(nameof(My));
        }

        // POST: /Availability/AddUnavailable
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddUnavailable(UnavailabilityInput input)
        {
            input ??= new UnavailabilityInput();
            if (!input.Date.HasValue && ModelState.TryGetValue(nameof(UnavailabilityInput.Date), out var entry) && entry.Errors.Count > 0)
                return await MyWithError(R.Err_DateRequired, date: input);
            try
            {
                await _availability.AddUnavailabilityAsync(CurrentUsername, input);
                AlertHelper.AddSuccessMessage(TempData, R.Toast_DateAdded);
                return RedirectToAction(nameof(My));
            }
            catch (ValidationException ex) { return await MyWithError(ex.Message, date: input); }
            catch (UnauthorizedAccessException ex) { return await MyWithError(ex.Message, date: input); }
        }

        // POST: /Availability/DeleteUnavailable
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUnavailable(int id)
        {
            try
            {
                await _availability.DeleteUnavailabilityAsync(CurrentUsername, id);
                AlertHelper.AddSuccessMessage(TempData, R.Toast_DateDeleted);
            }
            catch (ValidationException ex) { AlertHelper.AddErrorMessage(TempData, ex.Message); }
            catch (UnauthorizedAccessException ex) { AlertHelper.AddErrorMessage(TempData, ex.Message); }
            return RedirectToAction(nameof(My));
        }

        // GET: /Availability/Review
        [HttpGet]
        public async Task<IActionResult> Review(AvailabilityReviewFilter filter)
        {
            if (!await _availability.CanReviewAsync(CurrentUsername)) return Forbid();
            try
            {
                var vm = await _availability.GetReviewAsync(CurrentUsername, filter ?? new AvailabilityReviewFilter());
                if (!vm.IsStoreManager)
                    vm.StoreSelectList = await _storeService.GetSelectListAsync(R.Filter_AllStores);
                return View(vm);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        // POST: /Availability/Decide  (AJAX, JSON body, header RequestVerificationToken)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Decide([FromBody] AvailabilityDecisionInput? input)
        {
            if (input == null) return Json(new { success = false, message = R.Err_NothingSelected });
            if (input.Decision is not ((int)RegistrationStatus.Approved or (int)RegistrationStatus.Rejected))
                return Json(new { success = false, message = R.Err_DecisionInvalid });
            try
            {
                var decision = (RegistrationStatus)input.Decision;
                var result = await _availability.DecideAsync(CurrentUsername, input.Ids ?? new List<int>(), decision, input.Reason);
                var message = decision == RegistrationStatus.Approved
                    ? string.Format(R.Toast_Approved, result.Processed)
                    : string.Format(R.Toast_Rejected, result.Processed);
                if (result.Skipped > 0) message += " " + string.Format(R.Toast_Skipped, result.Skipped);
                return Json(new { success = result.Processed > 0, message = result.Processed > 0 ? message : R.Err_AlreadyProcessed, processed = result.Processed, skipped = result.Skipped });
            }
            catch (ValidationException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return Json(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Availability decision failed");
                return Json(new { success = false, message = R.Err_OperationFailed });
            }
        }

        // GET: /Availability/PendingCount  (badge menu)
        [HttpGet]
        public async Task<IActionResult> PendingCount()
            => Json(new { count = await _availability.GetPendingReviewCountAsync(CurrentUsername) });

        // GET: /Availability/Employee?employeeId=1
        [HttpGet]
        public async Task<IActionResult> Employee(int employeeId)
        {
            try
            {
                var vm = await _availability.GetEmployeeViewAsync(CurrentUsername, employeeId);
                return View(vm);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (ValidationException ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.Message);
                return await _availability.CanReviewAsync(CurrentUsername)
                    ? RedirectToAction(nameof(Review))
                    : RedirectToAction(nameof(My));
            }
        }

        // GET: /Availability/EmployeeSummary?employeeId=1  (JSON cho popover ở màn hình khác)
        [HttpGet]
        public async Task<IActionResult> EmployeeSummary(int employeeId)
        {
            try
            {
                var dto = await _availability.GetEmployeeSummaryAsync(CurrentUsername, employeeId, Common.Validation.AvailabilityRules.SummaryDays);
                return Json(new { success = true, data = dto });
            }
            catch (UnauthorizedAccessException ex)
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return Json(new { success = false, message = ex.Message });
            }
            catch (ValidationException ex)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
