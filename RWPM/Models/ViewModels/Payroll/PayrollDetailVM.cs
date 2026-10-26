using RWPM.Common.Enums;
using RWPM.Models.Entities;

namespace RWPM.Models.ViewModels.Payroll
{
    public class PayrollDetailVM
    {
        public RWPM.Models.Entities.Payroll Payroll { get; set; } = null!;
        public List<PayrollDetail> Details { get; set; } = new();
        public int? SelectedStoreId { get; set; }
        public string? Search { get; set; }
        public PayrollDetailStatus? StatusFilter { get; set; }
        public List<global::Store> Stores { get; set; } = new();

        // Metrics
        public int TotalEmployeesCount => Details.Count;
        public double TotalHours => Details.Sum(d => d.TotalWorkHours + d.PaidLeaveHours);
        public decimal TotalAmount => Details.Sum(d => d.TotalSalary);
        public int MissingRateCount => Details.Count(d => d.Status == PayrollDetailStatus.MissingRate);
        public int WarningCount => Details.Count(d => d.Status == PayrollDetailStatus.HasWarnings);
    }
}
