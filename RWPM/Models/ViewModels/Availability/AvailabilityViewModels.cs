using Microsoft.AspNetCore.Mvc.Rendering;
using RWPM.Common.Enums;
using RWPM.Common.Models;
using RWPM.Common.Validation;

namespace RWPM.Models.ViewModels.Availability
{
    public class AvailabilityEmployeeInfoVM
    {
        public int EmployeeId { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public int? StoreId { get; set; }
        public string? StoreName { get; set; }
    }

    public class AvailabilityWindowVM
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public RegistrationStatus Status { get; set; }
        public string? Note { get; set; }
        public string? RejectionReason { get; set; }
        public string? ApprovedBy { get; set; }
        public string? ApprovedByName { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime CreatedDate { get; set; }

        public bool CrossesMidnight => AvailabilityRules.CrossesMidnight(StartTime, EndTime);
        public string TimeRange => AvailabilityRules.FormatRange(StartTime, EndTime);
        public string DayName => AvailabilityRules.DayName(DayOfWeek);
    }

    public class UnavailabilityVM
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string? Reason { get; set; }
        public bool CanDelete { get; set; }
        public string DayName => AvailabilityRules.DayName(Date.DayOfWeek);
    }

    public class AvailabilityMyVM
    {
        public bool HasProfile { get; set; }
        public AvailabilityEmployeeInfoVM? Employee { get; set; }
        public List<AvailabilityWindowVM> Windows { get; set; } = new();
        public List<UnavailabilityVM> Unavailable { get; set; } = new();
        public DateTime MinUnavailableDate { get; set; }
        public DateTime MaxUnavailableDate { get; set; }
        public int PendingCount => Windows.Count(w => w.Status == RegistrationStatus.Pending);
        public int ApprovedCount => Windows.Count(w => w.Status == RegistrationStatus.Approved);
    }

    /// <summary>Form thêm khung giờ: có thể chọn nhiều thứ cùng một khoảng giờ.</summary>
    public class AvailabilityWindowInput
    {
        public List<int>? Days { get; set; }
        /// <summary>"HH:mm" 24h (time-picker giữ giá trị 24h ở ô gốc).</summary>
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public string? Note { get; set; }
    }

    public class UnavailabilityInput
    {
        public DateTime? Date { get; set; }
        public string? Reason { get; set; }
    }

    public class AvailabilityDecisionInput
    {
        public List<int>? Ids { get; set; }
        /// <summary>1 = Approved, 2 = Rejected.</summary>
        public int Decision { get; set; }
        public string? Reason { get; set; }
    }

    public sealed record AvailabilityDecisionResult(int Processed, int Skipped);

    public class AvailabilityReviewFilter
    {
        public int? StoreId { get; set; }
        /// <summary>"Pending" (mặc định) | "Approved" | "Rejected" | "All".</summary>
        public string? Status { get; set; }
        public string? Search { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class AvailabilityReviewRowVM : AvailabilityWindowVM
    {
        public string EmployeeCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? StoreName { get; set; }
    }

    public class AvailabilityReviewVM
    {
        public int? StoreId { get; set; }
        /// <summary>null = tất cả trạng thái.</summary>
        public RegistrationStatus? Status { get; set; }
        public string StatusKey => Status?.ToString() ?? "All";
        public string? Search { get; set; }
        public bool IsStoreManager { get; set; }
        public string? ScopeStoreName { get; set; }
        public SelectList? StoreSelectList { get; set; }
        public int PendingCount { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }
        public int UpcomingUnavailableCount { get; set; }
        public PaginationRes<AvailabilityReviewRowVM> Paged { get; set; } = new(new List<AvailabilityReviewRowVM>(), 1, 20, 0);
    }

    public class AvailabilityEmployeeVM
    {
        public AvailabilityEmployeeInfoVM Employee { get; set; } = new();
        public List<AvailabilityWindowVM> Windows { get; set; } = new();
        public List<UnavailabilityVM> Upcoming { get; set; } = new();
        public bool CanReview { get; set; }
        public bool HasApproved => Windows.Any(w => w.Status == RegistrationStatus.Approved);
    }

    // ===== JSON cho popover ở các màn hình khác =====

    public class AvailabilitySummaryWindowDto
    {
        public int DayOfWeek { get; set; }
        public string DayName { get; set; } = string.Empty;
        public string Start { get; set; } = string.Empty;
        public string End { get; set; } = string.Empty;
        public bool CrossesMidnight { get; set; }
        public string Display { get; set; } = string.Empty;
    }

    public class AvailabilitySummaryDateDto
    {
        public string Date { get; set; } = string.Empty;
        public string Display { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }

    public class AvailabilitySummaryDto
    {
        public int EmployeeId { get; set; }
        public string FullName { get; set; } = string.Empty;
        /// <summary>false = chưa có khung giờ được duyệt -> làm được mọi lúc.</summary>
        public bool HasDeclaredAvailability { get; set; }
        public List<AvailabilitySummaryWindowDto> Windows { get; set; } = new();
        public List<AvailabilitySummaryDateDto> UnavailableDates { get; set; } = new();
    }
}
