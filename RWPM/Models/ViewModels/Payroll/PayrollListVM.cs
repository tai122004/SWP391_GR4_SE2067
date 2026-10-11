using RWPM.Common.Enums;
using RWPM.Models.Entities;

namespace RWPM.Models.ViewModels.Payroll
{
    public class PayrollListVM
    {
        public List<RWPM.Models.Entities.Payroll> Payrolls { get; set; } = new();
        public int SelectedYear { get; set; }
        public List<int> AvailableYears { get; set; } = new();

        // Metrics
        public int TotalPayrolls => Payrolls.Count;
        public decimal TotalAnnualGrossSalary => Payrolls.Sum(p => p.TotalGrossSalary);
        public int TotalLockedPayrolls => Payrolls.Count(p => p.Status == PayrollStatus.Locked);
        public int TotalDraftPayrolls => Payrolls.Count(p => p.Status == PayrollStatus.Draft);
    }
}
