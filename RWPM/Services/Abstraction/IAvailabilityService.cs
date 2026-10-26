using RWPM.Common.Enums;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.Availability;

namespace RWPM.Services.Abstraction
{
    /// <summary>Kết quả kiểm tra một nhân viên có làm được một ca cụ thể không.</summary>
    public sealed class AvailabilityCheckResult
    {
        public bool IsAvailable { get; init; } = true;
        /// <summary>null | "Unavailable" (ngày đã khai không thể làm) | "OutsideAvailability" (ngoài khung giờ đã được duyệt)</summary>
        public string? ReasonCode { get; init; }
        /// <summary>Thông báo đã bản địa hóa theo ngôn ngữ hiện tại.</summary>
        public string? Message { get; init; }
        /// <summary>Nhân viên có khai báo khung giờ lặp lại nào đã được duyệt hay chưa.</summary>
        public bool HasDeclaredAvailability { get; init; }

        public static AvailabilityCheckResult Available(bool hasDeclared) => new() { IsAvailable = true, HasDeclaredAvailability = hasDeclared };
    }

    public interface IAvailabilityService
    {
        /// <summary>
        /// Kiểm tra một nhân viên với một ca vào một ngày làm việc.
        /// Quy tắc: ngày nằm trong EmployeeUnavailability -> không làm được;
        /// có ít nhất một EmployeeAvailability Approved -> ca (tính cả ca qua đêm, theo ShiftTimeHelper) phải nằm trọn trong một khung giờ Approved của thứ tương ứng;
        /// chưa có khung giờ Approved nào -> làm được mọi lúc.
        /// </summary>
        Task<AvailabilityCheckResult> CheckAsync(int employeeId, DateTime workDate, Shift shift);

        /// <summary>Phiên bản hàng loạt (ít truy vấn) cho nhiều nhân viên cùng một ca/ngày.</summary>
        Task<Dictionary<int, AvailabilityCheckResult>> CheckManyAsync(IReadOnlyCollection<int> employeeIds, DateTime workDate, Shift shift);

        /// <summary>
        /// Bổ sung: kiểm tra hàng loạt nhiều (nhân viên, ngày, ca) cùng lúc với 2 truy vấn (cùng quy tắc với CheckAsync).
        /// Khóa kết quả: (EmployeeId, Date.Date, ShiftId).
        /// </summary>
        Task<Dictionary<(int EmployeeId, DateTime Date, int ShiftId), AvailabilityCheckResult>> CheckAssignmentsAsync(IEnumerable<(int employeeId, DateTime date, Shift shift)> items);

        // ===== Khai báo của nhân viên (chính mình) =====

        /// <summary>Dữ liệu trang "Thời gian có thể làm việc" của người dùng hiện tại (HasProfile=false nếu không có hồ sơ nhân viên).</summary>
        Task<AvailabilityMyVM> GetMyAsync(string username);

        /// <summary>Thêm khung giờ hằng tuần (trạng thái Chờ duyệt) cho chính mình; báo người duyệt. Trả về Id các khung đã tạo.</summary>
        Task<List<int>> AddWindowsAsync(string username, AvailabilityWindowInput input);

        /// <summary>Xóa khung giờ của chính mình (kể cả đã duyệt; có hiệu lực ngay).</summary>
        Task DeleteWindowAsync(string username, int windowId);

        /// <summary>Khai ngày không thể làm (không cần duyệt; trước tối thiểu AvailabilityRules.UnavailabilityLeadDays ngày, tối đa 90 ngày).</summary>
        Task<int> AddUnavailabilityAsync(string username, UnavailabilityInput input);

        /// <summary>Xóa ngày không thể làm của chính mình (chỉ ngày từ hôm nay trở đi).</summary>
        Task DeleteUnavailabilityAsync(string username, int unavailabilityId);

        // ===== Duyệt (StoreManager chi nhánh mình; HR/Admin tất cả) =====

        /// <summary>Người dùng có quyền duyệt khung giờ không (Admin/HR, hoặc StoreManager có chi nhánh đang hoạt động).</summary>
        Task<bool> CanReviewAsync(string username);

        /// <summary>Danh sách duyệt. Ném UnauthorizedAccessException nếu không có quyền.</summary>
        Task<AvailabilityReviewVM> GetReviewAsync(string username, AvailabilityReviewFilter filter);

        /// <summary>Duyệt / từ chối hàng loạt các khung giờ Chờ duyệt (bỏ qua khung đã xử lý). Từ chối bắt buộc lý do 1–255 ký tự.</summary>
        Task<AvailabilityDecisionResult> DecideAsync(string username, IReadOnlyCollection<int> windowIds, RegistrationStatus decision, string? reason);

        /// <summary>Số khung giờ đang chờ người dùng này duyệt (0 nếu không có quyền) — dùng cho badge menu.</summary>
        Task<int> GetPendingReviewCountAsync(string username);

        /// <summary>Màn hình quản lý xem một nhân viên (kiểm tra phạm vi; ném UnauthorizedAccessException / InputValidationException).</summary>
        Task<AvailabilityEmployeeVM> GetEmployeeViewAsync(string username, int employeeId);

        /// <summary>Tóm tắt khung giờ đã duyệt + ngày không thể làm trong <paramref name="days"/> ngày tới (kiểm tra phạm vi).</summary>
        Task<AvailabilitySummaryDto> GetEmployeeSummaryAsync(string username, int employeeId, int days = 30);
    }
}
