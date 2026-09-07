using Microsoft.AspNetCore.Mvc.Rendering;
using RWPM.Common.Models;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.Acc;
using RWPM.Models.ViewModels.Acc.ChangePasswordAcc;

namespace RWPM.Services.Abstraction
{
    public interface IAccService
    {
        Task<Acc?> GetByIdAsync(string username, QueryOptions<Acc>? options = null);
        Task<Acc> GetRequiredByIdAsync(string username, QueryOptions<Acc>? options = null);
        Task<SelectList> GetSelectListAsync();
        SelectList GetAccountRoleSelectListAsync();
        Task<SelectList> GetSelectListWithExceptsAsync(string[] exceptUsernames);
        Task<List<Acc>> GetAllAsync(QueryOptions<Acc>? options = null);
        Task<PaginationRes<Acc>> SearchAsync(AccSearch searchObject, QueryOptions<Acc>? options = null);
        Task<Acc> CreateAsync(Acc entity);
        Task UpdateAsync(Acc entity);
        Task DeleteAsync(Acc entity);
        Task<bool> ExistsAsync(string username);
        Task EnsureExistsAsync(string username);
        Task ChangePasswordAsync(string oldPass, string newPass, string confirmNewPass);
        Task ChangePasswordForAccountAsync(ChangePasswordAccDto changePasswordDto);
        Task UpdateActiveStatusAsync(string username, bool active);

    }
}
