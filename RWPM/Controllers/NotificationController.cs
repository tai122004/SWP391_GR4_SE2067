using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RWPM.Services.Abstraction;
using NR = RWPM.Resources.Shared.NotificationResource;

namespace RWPM.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly INotificationService _notificationService;

        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        private string CurrentUsername => User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;

        private bool IsAjax => Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                               || Request.Headers.Accept.Any(h => h != null && h.Contains("application/json"));

        // GET: /Notification/Index?unread=true&pageNumber=1
        [HttpGet]
        public async Task<IActionResult> Index(bool unread = false, int pageNumber = 1)
        {
            if (string.IsNullOrEmpty(CurrentUsername)) return Forbid();
            if (pageNumber < 1) pageNumber = 1;

            var vm = await _notificationService.GetPagedAsync(CurrentUsername, unread, pageNumber);
            return View(vm);
        }

        // GET: /Notification/Latest?take=10
        [HttpGet]
        public async Task<IActionResult> Latest(int take = 10)
        {
            if (string.IsNullOrEmpty(CurrentUsername)) return Forbid();
            take = Math.Clamp(take, 1, 20);

            var items = await _notificationService.GetLatestAsync(CurrentUsername, take);
            var unreadCount = await _notificationService.GetUnreadCountAsync(CurrentUsername);
            return Json(new
            {
                success = true,
                unreadCount,
                items = items.Select(x => new
                {
                    id = x.Id,
                    type = x.Type,
                    title = x.Title,
                    message = x.Message,
                    url = x.Url,
                    isRead = x.IsRead,
                    createdDateStr = x.CreatedDateStr,
                    ageSeconds = x.AgeSeconds,
                    relativeTime = x.RelativeTime,
                    icon = x.Icon,
                    color = x.Color
                })
            });
        }

        // GET: /Notification/UnreadCount
        [HttpGet]
        public async Task<IActionResult> UnreadCount()
        {
            if (string.IsNullOrEmpty(CurrentUsername)) return Forbid();
            return Json(new { success = true, count = await _notificationService.GetUnreadCountAsync(CurrentUsername) });
        }

        // POST: /Notification/MarkRead
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(long id)
        {
            if (string.IsNullOrEmpty(CurrentUsername)) return Forbid();
            if (id <= 0)
            {
                return Json(new { success = false, message = NR.Err_NotFound });
            }

            // Chỉ đánh dấu được thông báo của chính mình (lọc theo Username trong service)
            var item = await _notificationService.MarkReadAsync(id, CurrentUsername);
            var url = item != null && !string.IsNullOrEmpty(item.Url) && Url.IsLocalUrl(item.Url) ? item.Url : null;
            if (!IsAjax)
            {
                return url != null ? LocalRedirect(url) : RedirectToAction(nameof(Index));
            }

            // Số chưa đọc chỉ cần cho phản hồi JSON (cập nhật badge)
            var unreadCount = await _notificationService.GetUnreadCountAsync(CurrentUsername);
            if (item == null)
            {
                return Json(new { success = false, message = NR.Err_NotFound, unreadCount });
            }
            return Json(new { success = true, unreadCount, url });
        }

        // POST: /Notification/MarkAllRead
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead(bool unread = false)
        {
            if (string.IsNullOrEmpty(CurrentUsername)) return Forbid();
            var count = await _notificationService.MarkAllReadAsync(CurrentUsername);

            if (IsAjax)
            {
                return Json(new { success = true, updated = count, unreadCount = 0, message = NR.Msg_AllMarkedRead });
            }

            TempData["SuccessMessage"] = NR.Msg_AllMarkedRead;
            return RedirectToAction(nameof(Index), new { unread });
        }
    }
}
