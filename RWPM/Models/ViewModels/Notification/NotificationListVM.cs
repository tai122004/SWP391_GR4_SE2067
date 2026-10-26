using RWPM.Common.Models;

namespace RWPM.Models.ViewModels.Notification
{
    public class NotificationListVM
    {
        public List<NotificationItemVM> Items { get; set; } = new();
        public bool UnreadOnly { get; set; }
        public int UnreadCount { get; set; }
        public int TotalCount { get; set; }
        public PaginationRes<NotificationItemVM> Pagination { get; set; } = new(new List<NotificationItemVM>(), 1, 20, 0);
    }
}
