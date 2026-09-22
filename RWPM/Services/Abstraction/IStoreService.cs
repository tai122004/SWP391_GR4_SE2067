using Microsoft.AspNetCore.Mvc.Rendering;
using RWPM.Common.Models;
using RWPM.Models.ViewModels.Store;

namespace RWPM.Services.Abstraction
{
    public interface IStoreService
    {
        Task<global::Store?> GetByIdAsync(int storeId, QueryOptions<global::Store>? options = null);
        Task<global::Store> GetRequiredByIdAsync(int storeId, QueryOptions<global::Store>? options = null);
        Task<SelectList> GetSelectListAsync(string? defaultOption = null);
        Task<List<global::Store>> GetAllAsync(QueryOptions<global::Store>? options = null);
        Task<PaginationRes<global::Store>> SearchAsync(StoreSearch searchObject, QueryOptions<global::Store>? options = null);
        Task<global::Store> CreateAsync(global::Store entity);
        Task UpdateAsync(global::Store entity);
        Task DeleteAsync(global::Store entity);
        Task<bool> ExistsByCodeAsync(string storeCode, int? excludeStoreId = null);
        Task UpdateActiveStatusAsync(int storeId, bool active);
    }
}
