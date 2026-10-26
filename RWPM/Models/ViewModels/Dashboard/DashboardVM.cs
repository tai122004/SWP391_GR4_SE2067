using RWPM.Common.Enums;

namespace RWPM.Models.ViewModels.Dashboard
{
    public enum DashboardMode
    {
        Employee = 0,
        Store = 1,
        Chain = 2
    }

    /// <summary>Trạng thái chấm công của một lượt ca (một nhân viên trong một ca).</summary>
    public enum ShiftAttendanceState
    {
        NotStarted = 0,      // Chưa tới giờ
        AwaitingCheckIn = 1, // Đã tới giờ nhưng còn trong thời gian ân hạn
        Late = 2,            // Đang trễ X phút (quá ân hạn, ca chưa kết thúc)
        CheckedIn = 3,       // Đã vào ca
        CheckedOut = 4,      // Đã ra ca
        Absent = 5           // Vắng (ca đã kết thúc, không chấm công vào)
    }

    public class DashboardVM
    {
        public DashboardMode Mode { get; set; }
        public string FullName { get; set; } = string.Empty;
        public DateTime Now { get; set; }
        public bool HasProfile { get; set; }
        /// <summary>Admin hệ thống thuần (không HR/StoreManager) không chấm công.</summary>
        public bool CanUseAttendance { get; set; } = true;
        public StoreDashboardVM? Store { get; set; }
        public ChainDashboardVM? Chain { get; set; }
        public MyDashboardVM? My { get; set; }
    }

    public class PendingSummaryVM
    {
        public int ShiftRegistration { get; set; }
        public int Swap { get; set; }
        public int Leave { get; set; }
        public int Adjustment { get; set; }
        public int Total => ShiftRegistration + Swap + Leave + Adjustment;
        /// <summary>Khoảng ngày bao trùm các đơn chờ duyệt (để link Request/Index hiển thị đủ).</summary>
        public DateTime? RangeStart { get; set; }
        public DateTime? RangeEnd { get; set; }
    }

    public class TodayShiftVM
    {
        public int ShiftId { get; set; }
        public string ShiftName { get; set; } = string.Empty;
        public DateTime WorkDate { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public bool IsCarryOver { get; set; }
        public int GraceMinutes { get; set; }
        public int ApprovedCount => Employees.Count;
        public int CheckedInCount => Employees.Count(e => e.State == ShiftAttendanceState.CheckedIn || e.State == ShiftAttendanceState.CheckedOut);
        public List<TodayShiftEmployeeVM> Employees { get; set; } = new();
    }

    public class TodayShiftEmployeeVM
    {
        public int EmployeeId { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public ShiftAttendanceState State { get; set; }
        public int LateMinutes { get; set; }
        public TimeSpan? CheckInTime { get; set; }
        public TimeSpan? CheckOutTime { get; set; }
    }

    public class LeaveTodayVM
    {
        public string EmployeeCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string StoreName { get; set; } = string.Empty;
        public LeaveType LeaveType { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class StoreDashboardVM
    {
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public int ActiveHeadcount { get; set; }
        public PendingSummaryVM Pending { get; set; } = new();
        public List<TodayShiftVM> Shifts { get; set; } = new();
        public List<LeaveTodayVM> OnLeave { get; set; } = new();
        /// <summary>Ca thiếu người so với nhu cầu trong 7 ngày tới (hôm nay .. +6).</summary>
        public ShortageSummaryVM Shortage { get; set; } = new();
        /// <summary>Số khung giờ "thời gian có thể làm việc" đang chờ người dùng này duyệt.</summary>
        public int PendingAvailability { get; set; }
    }

    /// <summary>Một ca thiếu người (đã duyệt &lt; cần).</summary>
    public class ShortageItemVM
    {
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public int ShiftId { get; set; }
        public string ShiftName { get; set; } = string.Empty;
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public DateTime Date { get; set; }
        public int Approved { get; set; }
        public int Required { get; set; }
        public int Pending { get; set; }
        public int Missing => Math.Max(0, Required - Approved);
        /// <summary>Thứ Hai của tuần chứa Date (tham số weekStart của ShiftDemand/Coverage).</summary>
        public DateTime WeekStart => Date.AddDays(-(((int)Date.DayOfWeek + 6) % 7));
    }

    public class ShortageSummaryVM
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        /// <summary>Có ít nhất một ô được cấu hình nhu cầu trong khoảng ngày.</summary>
        public bool HasDemand { get; set; }
        public int ShortShiftCount { get; set; }
        public int MissingPeople { get; set; }
        /// <summary>Tối đa ListLimit dòng, sắp theo ngày rồi giờ bắt đầu.</summary>
        public List<ShortageItemVM> Items { get; set; } = new();
    }

    public class StoreRowVM
    {
        public int StoreId { get; set; }
        public string StoreCode { get; set; } = string.Empty;
        public string StoreName { get; set; } = string.Empty;
        public int Headcount { get; set; }
        public int TodayAssignments { get; set; }
        public int CheckedInNow { get; set; }
        public int PendingCount { get; set; }
        /// <summary>Số ca thiếu người 7 ngày tới.</summary>
        public int ShortShifts { get; set; }
        /// <summary>Tổng số người còn thiếu 7 ngày tới.</summary>
        public int MissingPeople { get; set; }
    }

    public class PayrollSummaryVM
    {
        public int PayrollId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public PayrollStatus Status { get; set; }
        public int TotalEmployees { get; set; }
        public decimal TotalGrossSalary { get; set; }
    }

    public class NewHireVM
    {
        public string EmployeeCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string StoreName { get; set; } = string.Empty;
        public DateTime JoinDate { get; set; }
    }

    public class ChainDashboardVM
    {
        public int ActiveHeadcount { get; set; }
        public int UnassignedHeadcount { get; set; }
        public int TodayAssignments { get; set; }
        public int CheckedInNow { get; set; }
        public List<StoreRowVM> Stores { get; set; } = new();
        public PendingSummaryVM Pending { get; set; } = new();
        public PayrollSummaryVM? Payroll { get; set; }
        public bool PreviousMonthMissing { get; set; }
        public DateTime PreviousMonth { get; set; }
        public List<LeaveTodayVM> OnLeave { get; set; } = new();
        public int OnLeaveTotal { get; set; }
        public List<NewHireVM> NewHires { get; set; } = new();
        public int NewHiresTotal { get; set; }
        /// <summary>Ca thiếu người toàn chuỗi 7 ngày tới.</summary>
        public ShortageSummaryVM Shortage { get; set; } = new();
        public int PendingAvailability { get; set; }
    }

    public class MyShiftVM
    {
        public string ShiftName { get; set; } = string.Empty;
        public string StoreName { get; set; } = string.Empty;
        public DateTime WorkDate { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public bool IsCarryOver { get; set; }
        public ShiftAttendanceState State { get; set; }
        public int LateMinutes { get; set; }
        public TimeSpan? CheckInTime { get; set; }
        public TimeSpan? CheckOutTime { get; set; }
    }

    public class MyPayslipVM
    {
        public int PayrollId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal TotalSalary { get; set; }
    }

    public class MyDashboardVM
    {
        public string EmployeeCode { get; set; } = string.Empty;
        public string StoreName { get; set; } = string.Empty;
        public int AnnualLeaveBalance { get; set; }
        public List<MyShiftVM> Today { get; set; } = new();
        public List<MyShiftVM> Upcoming { get; set; } = new();
        public PendingSummaryVM Pending { get; set; } = new();
        public MyPayslipVM? LatestPayslip { get; set; }
    }
}
