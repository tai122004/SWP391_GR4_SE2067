using RWPM.Common.Enums;
using RWPM.Models.Entities;

namespace RWPM.Models.ViewModels.Payroll
{
    public class EmployeePayslipSummaryVM
    {
        public int PayrollId { get; set; }
        public int PeriodMonth { get; set; }
        public int PeriodYear { get; set; }
        public string Title { get; set; } = string.Empty;
        public double TotalWorkHours { get; set; }
        public decimal HourlyRate { get; set; }
        public decimal TotalSalary { get; set; }
        public PayrollStatus Status { get; set; }
        public DateTime? LockedDate { get; set; }
    }

    public class EmployeePayslipVM
    {
        public PayrollDetail Detail { get; set; } = null!;
        public RWPM.Models.Entities.Payroll Payroll { get; set; } = null!;
        public RWPM.Models.Entities.Employee Employee { get; set; } = null!;
        public List<AttendanceRecord> AttendanceRecords { get; set; } = new();
        public List<LeaveRequest> LeaveRequests { get; set; } = new();
    }
}
