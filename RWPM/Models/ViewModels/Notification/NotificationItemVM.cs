namespace RWPM.Models.ViewModels.Notification
{
    /// <summary>Một thông báo đã được dịch sang ngôn ngữ của người xem (render lúc hiển thị).</summary>
    public class NotificationItemVM
    {
        public long Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Url { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedDateStr { get; set; } = string.Empty;
        /// <summary>Số giây kể từ lúc tạo (tính phía server để client không phụ thuộc múi giờ).</summary>
        public long AgeSeconds { get; set; }
        public string RelativeTime { get; set; } = string.Empty;
        /// <summary>Bootstrap icon class, ví dụ "bi-check-circle-fill".</summary>
        public string Icon { get; set; } = "bi-bell";
        /// <summary>Bootstrap color name: success, danger, primary, warning, info.</summary>
        public string Color { get; set; } = "primary";
    }
}
