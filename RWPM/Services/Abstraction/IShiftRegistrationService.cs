using RWPM.Common.Enums;
using RWPM.Models.Entities;

namespace RWPM.Services.Abstraction
{
    /// <summary>Một ca (ngày + ca) không phù hợp thời gian có thể làm việc của nhân viên.</summary>
    public sealed class ScheduleAvailabilityIssue
    {
        public int EmployeeId { get; init; }
        public string EmployeeName { get; init; } = string.Empty;
        public int ShiftId { get; init; }
        public string ShiftName { get; init; } = string.Empty;
        public DateTime Date { get; init; }
        /// <summary>"Unavailable" | "OutsideAvailability"</summary>
        public string? ReasonCode { get; init; }
        /// <summary>Thông báo đã bản địa hóa (AvailabilityCheckResult.Message).</summary>
        public string Message { get; init; } = string.Empty;
    }

    /// <summary>Cảnh báo (không chặn) khi quản lý xếp/duyệt ca.</summary>
    public sealed class ScheduleWarnings
    {
        /// <summary>Nhân viên ngoài khung giờ đã duyệt / vào ngày đã khai không thể làm.</summary>
        public List<string> Availability { get; } = new();
        /// <summary>Vượt nhu cầu (IShiftDemandService.GetOverDemandWarningsAsync).</summary>
        public List<string> Demand { get; } = new();
        public bool Any => Availability.Count > 0 || Demand.Count > 0;
        public List<string> All => Availability.Concat(Demand).ToList();
    }

    public interface IShiftRegistrationService
    {
        Task ValidateScheduleConstraintsAsync(int employeeId, List<ShiftRegistration> pendingRegistrations, List<int>? ignoreIds = null);
        Task<List<ShiftRegistration>> GetEventsAsync(DateTime start, DateTime end, int? employeeId = null, int? storeId = null);
        Task<List<ShiftRegistration>> GetRequestsAsync(DateTime start, DateTime end, RegistrationStatus? status = null);
        Task<ShiftRegistration?> GetByIdAsync(int id);
        Task<ShiftRegistration> CreateAsync(ShiftRegistration entity);

        /// <summary>
        /// Tạo nhiều ca cho một nhân viên. enforceAvailability = true (nhân viên tự đăng ký):
        /// chặn toàn bộ nếu có ca ngoài khung giờ đã duyệt / vào ngày không thể làm (thông báo liệt kê ngày + ca).
        /// </summary>
        Task<List<ShiftRegistration>> CreateBatchAsync(List<ShiftRegistration> entities, bool enforceAvailability = false);
        Task<ShiftRegistration> UpdateAsync(ShiftRegistration entity);
        Task DeleteAsync(int id);
        Task UpdateStatusAsync(int id, RegistrationStatus status, string? rejectionReason = null, string? approver = null);
        Task UpdateBulkStatusAsync(List<int> ids, RegistrationStatus status, string? rejectionReason = null, string? approver = null);

        /// <summary>
        /// Đồng bộ "Đăng ký tuần". enforceAvailability = true (tự đăng ký): các ca MỚI thêm không phù hợp thời gian có thể làm việc
        /// -> từ chối cả lần gửi (tất cả hoặc không), thông báo liệt kê ngày + ca.
        /// </summary>
        Task SyncWeeklyRegistrationAsync(int employeeId, int storeId, DateTime startOfWeek, List<ShiftRegistration> desiredRegistrations, bool enforceAvailability = false);

        // ===================== Bổ sung: tích hợp thời gian có thể làm việc / nhu cầu ca =====================

        /// <summary>Các (ca, ngày) của một nhân viên không phù hợp thời gian có thể làm việc (2–3 truy vấn).</summary>
        Task<List<ScheduleAvailabilityIssue>> GetAvailabilityIssuesAsync(int employeeId, IEnumerable<(int shiftId, DateTime date)> slots);

        /// <summary>
        /// Ô bị chặn trên lưới "Đăng ký ca của tôi": khóa "shiftId_yyyy-MM-dd" -> lý do (đã bản địa hóa).
        /// Chỉ xét các ca đang áp dụng tại chi nhánh và ngày từ hôm nay trở đi.
        /// </summary>
        Task<Dictionary<string, string>> GetBlockedSlotsAsync(int employeeId, int storeId, DateTime from, DateTime to);

        /// <summary>Cảnh báo thời gian có thể làm việc khi quản lý tạo ca cho nhân viên (đăng ký mới ở trạng thái Chờ duyệt).</summary>
        Task<ScheduleWarnings> GetAssignmentWarningsAsync(int employeeId, IEnumerable<(int shiftId, DateTime date)> slots);

        /// <summary>Cảnh báo khi duyệt các đăng ký (chỉ xét đăng ký đang Chờ duyệt): thời gian có thể làm việc + vượt nhu cầu (gộp theo chi nhánh/ca/ngày).</summary>
        Task<ScheduleWarnings> GetApprovalWarningsAsync(IReadOnlyCollection<int> registrationIds);

        /// <summary>
        /// Cảnh báo khi duyệt đơn đổi ca đang Chờ duyệt: thời gian có thể làm việc của người nhận ca mới;
        /// vượt nhu cầu chỉ với đơn chuyển sang ca/ngày khác (đổi với đồng nghiệp không đổi số người).
        /// </summary>
        Task<ScheduleWarnings> GetSwapApprovalWarningsAsync(IReadOnlyCollection<int> swapIds);
    }
}
