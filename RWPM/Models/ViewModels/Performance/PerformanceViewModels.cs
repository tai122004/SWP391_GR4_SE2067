using Microsoft.AspNetCore.Mvc.Rendering;
using RWPM.Common.Enums;
using RWPM.Common.Models;

namespace RWPM.Models.ViewModels.Performance
{
    /// <summary>Bộ lọc màn hình Hiệu suất (Performance/Index, ExportExcel).</summary>
    public class PerformanceFilter : PagedReq
    {
        public int? Year { get; set; }
        public int? Month { get; set; }
        public int? StoreId { get; set; }
        public string? Search { get; set; }
    }

    /// <summary>Phạm vi dữ liệu do controller xác định theo vai trò người xem.</summary>
    public class PerformanceScope
    {
        /// <summary>Bắt buộc lọc theo chi nhánh (Quản lý cửa hàng).</summary>
        public int? ForcedStoreId { get; set; }
        /// <summary>Loại trừ hồ sơ của chính người xem.</summary>
        public int? ExcludeEmployeeId { get; set; }
        /// <summary>Chỉ nhân viên có vai trò Employee (Quản lý cửa hàng).</summary>
        public bool OnlyEmployeeRole { get; set; }
    }

    public class PerformanceReviewInfoVM
    {
        public byte Score { get; set; }
        public string? Comment { get; set; }
        public string ReviewedBy { get; set; } = string.Empty;
        public string? ReviewerName { get; set; }
        public DateTime ReviewedAt { get; set; }
    }

    public class PerformanceRowVM
    {
        public int EmployeeId { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public int? StoreId { get; set; }
        public string? StoreName { get; set; }
        public AccountRole? Role { get; set; }
        public PerformanceMetrics Metrics { get; set; } = new();
        public PerformanceReviewInfoVM? Review { get; set; }
    }

    public class PerformanceSummaryVM
    {
        public double? AverageAttendanceRate { get; set; }
        public int TotalLates { get; set; }
        public int TotalAbsences { get; set; }
        public int ReviewedCount { get; set; }
        public int TotalEmployees { get; set; }
    }

    public class PerformanceIndexVM
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int? StoreId { get; set; }
        public string? Search { get; set; }
        public bool IsStoreManager { get; set; }
        public SelectList? StoreSelectList { get; set; }
        public List<int> AvailableYears { get; set; } = new();
        public PerformanceSummaryVM Summary { get; set; } = new();
        public PaginationRes<PerformanceRowVM> Paged { get; set; } = new(new List<PerformanceRowVM>(), 1, 10, 0);
    }

    public enum PerformanceShiftState
    {
        /// <summary>Đã chấm công vào (xem thêm cờ đi muộn/về sớm/thiếu check-out).</summary>
        Worked = 0,
        Absent = 1,
        Leave = 2,
        /// <summary>Ca chưa bắt đầu.</summary>
        Upcoming = 3,
        /// <summary>Ca đang diễn ra nhưng chưa chấm công vào.</summary>
        Pending = 4
    }

    public class PerformanceShiftRowVM
    {
        public DateTime Date { get; set; }
        public string? ShiftName { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public TimeSpan? CheckInTime { get; set; }
        public TimeSpan? CheckOutTime { get; set; }
        /// <summary>true: ca đăng ký đã duyệt; false: chấm công ngoài lịch hoặc ca đã hủy do nghỉ phép.</summary>
        public bool IsScheduled { get; set; }
        public PerformanceShiftState State { get; set; }
        public bool IsLate { get; set; }
        public int LateMinutes { get; set; }
        public bool IsEarlyLeave { get; set; }
        public int EarlyLeaveMinutes { get; set; }
        public bool IsMissingCheckOut { get; set; }
        /// <summary>Đã check-in, ca chưa kết thúc và chưa check-out.</summary>
        public bool IsInProgress { get; set; }
        public double WorkedHours { get; set; }
        public LeaveType? LeaveType { get; set; }
    }

    public class PerformanceTrendItemVM
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public PerformanceMetrics Metrics { get; set; } = new();
        public byte? ReviewScore { get; set; }
    }

    public class PerformanceReviewInput
    {
        public int EmployeeId { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public byte? Score { get; set; }
        public string? Comment { get; set; }
    }

    public class PerformanceDetailVM
    {
        public PerformanceRowVM Employee { get; set; } = new();
        public int Year { get; set; }
        public int Month { get; set; }
        public List<PerformanceShiftRowVM> Shifts { get; set; } = new();
        public List<PerformanceTrendItemVM> Trend { get; set; } = new();
        public List<int> AvailableYears { get; set; } = new();
        /// <summary>Xem từ "Hiệu suất của tôi" (chỉ đọc, điều hướng về My).</summary>
        public bool IsOwnView { get; set; }
        public bool CanReview { get; set; }
        /// <summary>Lý do không thể đánh giá (hiển thị cho người có quyền nhưng bị chặn, ví dụ tháng tương lai).</summary>
        public string? ReviewBlockedReason { get; set; }
    }

    public class PerformanceMyVM
    {
        public bool HasProfile { get; set; }
        public PerformanceRowVM? Employee { get; set; }
        public List<PerformanceTrendItemVM> Months { get; set; } = new();
        public Dictionary<string, PerformanceReviewInfoVM> Reviews { get; set; } = new();

        public static string Key(int year, int month) => $"{year:D4}-{month:D2}";
    }
}
