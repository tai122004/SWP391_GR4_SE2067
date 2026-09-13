using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RWPM.Common;
using RWPM.Common.Exceptions;
using RWPM.Common.Helper;
using RWPM.Common.Models;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.Employee;
using RWPM.Services.Abstraction;

namespace RWPM.Services.Implementation
{
    public class EmployeeService : IEmployeeService
    {
        private readonly DefaultDatabaseContext _ctx;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public EmployeeService(DefaultDatabaseContext ctx, IHttpContextAccessor httpContextAccessor)
        {
            _ctx = ctx;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<Employee?> GetByIdAsync(int employeeId, QueryOptions<Employee>? options = null)
        {
            var query = _ctx.Employee
                .Include(x => x.Account)
                .Include(x => x.Store)
                .Where(x => x.EmployeeId == employeeId);
            query = QueryHelper.ApplyQueryOptions(query, options);
            return await query.FirstOrDefaultAsync();
        }

        public async Task<Employee> GetRequiredByIdAsync(int employeeId, QueryOptions<Employee>? options = null)
        {
            var employee = await GetByIdAsync(employeeId, options);
            if (employee == null)
            {
                throw new KeyNotFoundException($"Khong tim thay nhan vien voi ID {employeeId}.");
            }
            return employee;
        }

        public async Task<SelectList> GetAvailableAccountsSelectListAsync(string? currentUsername = null)
        {
            var data = new List<object>
            {
                new { Id = string.Empty, Display = Resources.Shared.SharedResource.Dropdown_SelectAccount }
            };

            var assignedUsernamesQuery = _ctx.Employee.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(currentUsername))
            {
                assignedUsernamesQuery = assignedUsernamesQuery.Where(e => e.Username != currentUsername);
            }
            var assignedUsernames = await assignedUsernamesQuery.Select(e => e.Username).ToListAsync();

            var availableAccounts = await _ctx.Acc.AsNoTracking()
                .Where(a => a.IsActive && !assignedUsernames.Contains(a.Username))
                .OrderBy(a => a.FullName)
                .ToListAsync();

            var list = availableAccounts.Select(a => new
            {
                Id = a.Username,
                Display = $"{a.FullName} ({a.Username}) - {UIHelper.GetDisplayName(a.Role)}"
            });

            data.AddRange(list);
            return new SelectList(data, "Id", "Display", currentUsername);
        }

        public async Task<PaginationRes<Employee>> SearchAsync(EmployeeSearch searchObject, QueryOptions<Employee>? options = null)
        {
            var query = _ctx.Employee
                .Include(x => x.Account)
                .Include(x => x.Store)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchObject.Search))
            {
                var search = searchObject.Search.Trim();
                query = query.Where(e => e.EmployeeCode.Contains(search) ||
                                         e.Username.Contains(search) ||
                                         e.Account.FullName.Contains(search));
            }

            if (searchObject.StoreId.HasValue && searchObject.StoreId.Value > 0)
            {
                query = query.Where(e => e.StoreId == searchObject.StoreId.Value);
            }

            if (searchObject.IsActive.HasValue)
            {
                query = query.Where(e => e.IsActive == searchObject.IsActive.Value);
            }

            query = QueryHelper.ApplyQueryOptions(query, options);

            var totalRecords = await query.CountAsync();
            var data = await query
                .OrderByDescending(e => e.EmployeeId)
                .Skip((searchObject.PageNumber - 1) * searchObject.PageSize)
                .Take(searchObject.PageSize)
                .ToListAsync();

            return new PaginationRes<Employee>(data, searchObject.PageNumber, searchObject.PageSize, totalRecords);
        }

        public async Task<Employee> CreateAsync(Employee entity)
        {
            entity.EmployeeCode = entity.EmployeeCode.Trim();
            entity.Username = entity.Username.Trim();

            if (await ExistsByCodeAsync(entity.EmployeeCode))
            {
                throw new ModelValidationException("EmployeeCode_Exists", $"Ma nhan vien '{entity.EmployeeCode}' da ton tai trong he thong.");
            }

            if (await ExistsByUsernameAsync(entity.Username))
            {
                throw new ModelValidationException("Username_Exists", $"Tai khoan '{entity.Username}' da duoc lien ket voi mot nhan vien khac.");
            }

            if (!await _ctx.Store.AnyAsync(s => s.StoreId == entity.StoreId))
            {
                throw new ModelValidationException("Store_NotFound", "Cua hang duoc chon khong ton tai.");
            }

            entity.CreatedDate = DateTime.Now;
            entity.CreatedBy = AccountHelper.GetCurrentUsername(_httpContextAccessor);

            await _ctx.Employee.AddAsync(entity);
            await _ctx.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateAsync(Employee entity)
        {
            entity.EmployeeCode = entity.EmployeeCode.Trim();
            entity.Username = entity.Username.Trim();

            var existing = await GetRequiredByIdAsync(entity.EmployeeId, new QueryOptions<Employee> { NoTracking = false });

            if (await ExistsByCodeAsync(entity.EmployeeCode, entity.EmployeeId))
            {
                throw new ModelValidationException("EmployeeCode_Exists", $"Ma nhan vien '{entity.EmployeeCode}' da duoc su dung boi nhan vien khac.");
            }

            if (await ExistsByUsernameAsync(entity.Username, entity.EmployeeId))
            {
                throw new ModelValidationException("Username_Exists", $"Tai khoan '{entity.Username}' da duoc lien ket voi nhan vien khac.");
            }

            if (!await _ctx.Store.AnyAsync(s => s.StoreId == entity.StoreId))
            {
                throw new ModelValidationException("Store_NotFound", "Cua hang duoc chon khong ton tai.");
            }

            existing.EmployeeCode = entity.EmployeeCode;
            existing.Username = entity.Username;
            existing.StoreId = entity.StoreId;
            existing.JoinDate = entity.JoinDate;
            existing.IsActive = entity.IsActive;
            existing.UpdatedDate = DateTime.Now;
            existing.UpdatedBy = AccountHelper.GetCurrentUsername(_httpContextAccessor);

            // EF Core tracking will automatically detect changes
            await _ctx.SaveChangesAsync();
        }

        public async Task DeleteAsync(Employee entity)
        {
            var employee = await GetRequiredByIdAsync(entity.EmployeeId);
            _ctx.Employee.Remove(employee);
            await _ctx.SaveChangesAsync();
        }

        public async Task<bool> ExistsByCodeAsync(string employeeCode, int? excludeId = null)
        {
            var code = employeeCode.Trim();
            if (excludeId.HasValue)
            {
                return await _ctx.Employee.AnyAsync(e => e.EmployeeCode == code && e.EmployeeId != excludeId.Value);
            }
            return await _ctx.Employee.AnyAsync(e => e.EmployeeCode == code);
        }

        public async Task<bool> ExistsByUsernameAsync(string username, int? excludeId = null)
        {
            var u = username.Trim();
            if (excludeId.HasValue)
            {
                return await _ctx.Employee.AnyAsync(e => e.Username == u && e.EmployeeId != excludeId.Value);
            }
            return await _ctx.Employee.AnyAsync(e => e.Username == u);
        }

        public async Task UpdateActiveStatusAsync(int employeeId, bool active)
        {
            var employee = await GetRequiredByIdAsync(employeeId, new QueryOptions<Employee>() { NoTracking = false });
            employee.IsActive = active;
            employee.UpdatedDate = DateTime.Now;
            employee.UpdatedBy = AccountHelper.GetCurrentUsername(_httpContextAccessor);
            await _ctx.SaveChangesAsync();
        }
    }
}