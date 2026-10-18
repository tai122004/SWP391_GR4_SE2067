using RWPM.Common.Enums;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.Payroll;

namespace RWPM.Services.Abstraction
{
    public interface IPayrollService
    {
        Task<List<Payroll>> GetPayrollsAsync(int? year = null, int? storeId = null);
        Task<Payroll?> GetPayrollByIdAsync(int id);
        Task<PayrollDetailVM> GetPayrollDetailAsync(int payrollId, int? storeId = null, string? search = null, PayrollDetailStatus? status = null);
        Task<Payroll> CalculatePayrollAsync(DateTime startDate, DateTime endDate, string operatorUsername);
        Task<PayrollPeriodSuggestionVM> GetNextPeriodSuggestionAsync();
        Task LockPayrollAsync(int payrollId, string operatorUsername);
        Task UnlockPayrollAsync(int payrollId, string reason, string operatorUsername);
        Task DeletePayrollAsync(int payrollId, string operatorUsername);
        Task<EmployeePayslipVM?> GetEmployeePayslipAsync(string username, int? month = null, int? year = null, int? payrollId = null);
        Task<EmployeePayslipVM?> GetEmployeePayslipByIdAsync(int payrollId, int employeeId);
        Task<List<EmployeePayslipSummaryVM>> GetEmployeePayslipHistoryAsync(string username);
        Task<byte[]> ExportPayrollToExcelAsync(int payrollId, int? storeId = null);
        Task<List<PayrollAuditLog>> GetPayrollAuditLogsAsync(int payrollId);
    }
}
