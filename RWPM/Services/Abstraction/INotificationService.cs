using RWPM.Models.ViewModels.Notification;

namespace RWPM.Services.Abstraction
{
    /// <summary>Mã loại thông báo (lưu ở cột Notification.Type).</summary>
    public static class NotificationTypes
    {
        /// <summary>Args: [requestTypeKey, code, detail]</summary>
        public const string RequestApproved = "RequestApproved";
        /// <summary>Args: [requestTypeKey, code, detail, reason]</summary>
        public const string RequestRejected = "RequestRejected";
        /// <summary>Args: [code, requesterName, detail]</summary>
        public const string SwapCoworkerApproved = "SwapCoworkerApproved";
        /// <summary>Args: [requestTypeKey, code, requesterName, detail]</summary>
        public const string RequestSubmitted = "RequestSubmitted";
        /// <summary>Args: [requesterName, count, dateRange]</summary>
        public const string ShiftRegSubmittedBatch = "ShiftRegSubmittedBatch";
        /// <summary>Args: [startDdMm, endDdMm]</summary>
        public const string PayslipAvailable = "PayslipAvailable";
    }

    /// <summary>Loại đơn dùng chung với RequestController ("ShiftReg", "ShiftSwap", "Leave", "Attendance").</summary>
    public static class NotificationRequestTypes
    {
        public const string ShiftReg = "ShiftReg";
        public const string ShiftSwap = "ShiftSwap";
        public const string Leave = "Leave";
        public const string Attendance = "Attendance";
    }

    public interface INotificationService
    {
        /// <summary>Lưu thông báo cho một người và đẩy realtime qua SignalR. Không bao giờ ném lỗi (lỗi chỉ ghi log).</summary>
        Task NotifyAsync(string username, string type, IEnumerable<string?>? args = null, string? url = null);

        /// <summary>Lưu thông báo cho nhiều người và đẩy realtime qua SignalR. Không bao giờ ném lỗi (lỗi chỉ ghi log).</summary>
        Task NotifyAsync(IEnumerable<string> usernames, string type, IEnumerable<string?>? args = null, string? url = null);

        Task<List<NotificationItemVM>> GetLatestAsync(string username, int take = 10);
        Task<int> GetUnreadCountAsync(string username);

        /// <summary>Đánh dấu đã đọc; chỉ áp dụng cho thông báo của chính người dùng. Trả về null nếu không tìm thấy.</summary>
        Task<NotificationItemVM?> MarkReadAsync(long id, string username);
        Task<int> MarkAllReadAsync(string username);
        Task<NotificationListVM> GetPagedAsync(string username, bool unreadOnly, int pageNumber, int pageSize = 20);

        /// <summary>
        /// Người duyệt đơn của một chi nhánh: Quản lý cửa hàng (đang hoạt động) của chi nhánh + HR.
        /// adminOnly = true: chỉ Admin (đơn điều chỉnh công của StoreManager/HR).
        /// Người làm đơn luôn bị loại; nếu không còn ai thì gửi Admin.
        /// </summary>
        Task<List<string>> GetApproverUsernamesAsync(int storeId, string? requesterUsername, bool adminOnly = false);

        /// <summary>Báo cho người làm đơn khi đơn được duyệt/từ chối (đọc trạng thái hiện tại sau khi lưu). Bỏ qua nếu người xử lý chính là người làm đơn.</summary>
        Task NotifyRequestDecisionAsync(string requestType, IEnumerable<int> ids, string? actorUsername);

        /// <summary>Báo cho người duyệt khi có đơn mới.</summary>
        Task NotifyRequestSubmittedAsync(string requestType, int id, string? actorUsername);

        /// <summary>Một thông báo gộp cho nhiều ca đăng ký mới (tạo nhiều ca / đồng bộ tuần).</summary>
        Task NotifyShiftRegistrationsSubmittedAsync(int employeeId, IReadOnlyCollection<int> registrationIds, string? actorUsername);

        /// <summary>Báo phiếu lương đã có cho toàn bộ nhân viên trong bảng lương đã chốt.</summary>
        Task NotifyPayslipsAvailableAsync(int payrollId, string? actorUsername);
    }
}
