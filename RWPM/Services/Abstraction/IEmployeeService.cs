using Microsoft.AspNetCore.Mvc.Rendering;
using RWPM.Common.Models;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.Employee;

namespace RWPM.Services.Abstraction
{
    public interface IEmployeeService
    {
        Task<Employee?> GetByIdAsync(int employeeId, QueryOptions<Employee>? options = null);
        Task<Employee> GetRequiredByIdAsync(int employeeId, QueryOptions<Employee>? options = null);
        Task<PaginationRes<Employee>> SearchAsync(EmployeeSearch searchObject, QueryOptions<Employee>? options = null);
        Task<Employee> CreateAsync(Employee entity);
        Task UpdateAsync(Employee entity);
        Task DeleteAsync(Employee entity);
        Task<bool> ExistsByCodeAsync(string employeeCode, int? excludeId = null);
        Task<bool> ExistsByUsernameAsync(string username, int? excludeId = null);
        Task UpdateActiveStatusAsync(int employeeId, bool active);
        Task<SelectList> GetAvailableAccountsSelectListAsync(string? currentUsername = null);
    }
}
