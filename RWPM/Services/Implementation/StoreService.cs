using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using RWPM.Common;
using RWPM.Common.Exceptions;
using RWPM.Common.Helper;
using RWPM.Common.Models;
using RWPM.Infrastructure.Data;
using RWPM.Models.ViewModels.Store;
using RWPM.Resources.Shared;
using RWPM.Services.Abstraction;

namespace RWPM.Services.Implementation
{
    public class StoreService : IStoreService
    {
        private readonly DefaultDatabaseContext _ctx;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IStringLocalizer<Store> _localizer;

        public StoreService(
            DefaultDatabaseContext ctx,
            IHttpContextAccessor httpContextAccessor,
            IStringLocalizer<Store> localizer)
        {
            _ctx = ctx;
            _httpContextAccessor = httpContextAccessor;
            _localizer = localizer;
        }

        public async Task<global::Store?> GetByIdAsync(int storeId, QueryOptions<global::Store>? options = null)
        {
            var query = _ctx.Store.Where(x => x.StoreId == storeId);
            query = QueryHelper.ApplyQueryOptions(query, options);
            return await query.FirstOrDefaultAsync();
        }

        public async Task<global::Store> GetRequiredByIdAsync(int storeId, QueryOptions<global::Store>? options = null)
        {
            var store = await GetByIdAsync(storeId, options);
            if (store == null)
            {
                throw new ModelValidationException("Store_NotExists", $"Không tìm thấy cửa hàng với ID {storeId}.");
            }
            return store;
        }

        public async Task<SelectList> GetSelectListAsync(string? defaultOption = null)
        {
            var defaultDisplay = defaultOption ?? _localizer["Dropdown_SelectStore"].Value;
            if (string.IsNullOrWhiteSpace(defaultDisplay) || defaultDisplay == "Dropdown_SelectStore")
            {
                defaultDisplay = "-- Chọn cửa hàng --";
            }

            var data = new List<object>
            {
                new { Id = 0, Display = defaultDisplay }
            };

            var stores = await _ctx.Store.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.StoreName)
                .Select(x => new { Id = x.StoreId, Display = $"{x.StoreName} ({x.StoreCode})" })
                .ToListAsync();

            data.AddRange(stores);
            return new SelectList(data, "Id", "Display");
        }

        public async Task<List<global::Store>> GetAllAsync(QueryOptions<global::Store>? options = null)
        {
            var query = _ctx.Store.AsQueryable();
            query = QueryHelper.ApplyQueryOptions(query, options);
            return await query.ToListAsync();
        }

        public async Task<PaginationRes<global::Store>> SearchAsync(StoreSearch searchObject, QueryOptions<global::Store>? options = null)
        {
            var query = _ctx.Store.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchObject.Search))
            {
                var search = searchObject.Search.Trim();
                query = query.Where(s => s.StoreCode.Contains(search) ||
                                         s.StoreName.Contains(search) ||
                                         s.Address.Contains(search) ||
                                         s.Phone.Contains(search));
            }

            if (searchObject.IsActive.HasValue)
            {
                query = query.Where(s => s.IsActive == searchObject.IsActive.Value);
            }

            query = QueryHelper.ApplyQueryOptions(query, options);

            var totalRecords = await query.CountAsync();
            var data = await query
                .OrderByDescending(s => s.StoreId)
                .Skip((searchObject.PageNumber - 1) * searchObject.PageSize)
                .Take(searchObject.PageSize)
                .ToListAsync();

            return new PaginationRes<global::Store>(data, searchObject.PageNumber, searchObject.PageSize, totalRecords);
        }

        public async Task<global::Store> CreateAsync(global::Store entity)
        {
            entity.StoreCode = entity.StoreCode.Trim();
            entity.StoreName = entity.StoreName.Trim();

            if (await ExistsByCodeAsync(entity.StoreCode))
            {
                throw new ModelValidationException("StoreCode_Exists", $"Mã cửa hàng '{entity.StoreCode}' đã tồn tại trong hệ thống.");
            }

            entity.CreatedDate = DateTime.Now;
            entity.CreatedBy = AccountHelper.GetCurrentUsername(_httpContextAccessor);

            await _ctx.Store.AddAsync(entity);
            await _ctx.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateAsync(global::Store entity)
        {
            entity.StoreCode = entity.StoreCode.Trim();
            entity.StoreName = entity.StoreName.Trim();

            var existingStore = await GetRequiredByIdAsync(entity.StoreId);

            if (await ExistsByCodeAsync(entity.StoreCode, entity.StoreId))
            {
                throw new ModelValidationException("StoreCode_Exists", $"Mã cửa hàng '{entity.StoreCode}' đã được sử dụng bởi cửa hàng khác.");
            }

            existingStore.StoreCode = entity.StoreCode;
            existingStore.StoreName = entity.StoreName;
            existingStore.Address = entity.Address?.Trim() ?? string.Empty;
            existingStore.Phone = entity.Phone?.Trim() ?? string.Empty;
            existingStore.Latitude = entity.Latitude;
            existingStore.Longitude = entity.Longitude;
            existingStore.MinAllowedDistanceMeters = entity.MinAllowedDistanceMeters;
            existingStore.AllowedRadiusMeters = entity.AllowedRadiusMeters;
            existingStore.IsActive = entity.IsActive;
            existingStore.UpdatedDate = DateTime.Now;
            existingStore.UpdatedBy = AccountHelper.GetCurrentUsername(_httpContextAccessor);

            _ctx.Store.Update(existingStore);
            await _ctx.SaveChangesAsync();
        }

        public async Task DeleteAsync(global::Store entity)
        {
            var store = await GetRequiredByIdAsync(entity.StoreId, new QueryOptions<global::Store> { NoTracking = false });

            var hasEmployees = await _ctx.Employee.AnyAsync(e => e.StoreId == entity.StoreId);
            if (hasEmployees)
            {
                throw new ModelValidationException("Store_Delete_HaveEmployeeUse", $"Không thể xóa cửa hàng '{store.StoreName}' vì đang có nhân viên thuộc cửa hàng này.");
            }

            var hasShiftRegs = await _ctx.ShiftRegistration.AnyAsync(s => s.StoreId == entity.StoreId);
            if (hasShiftRegs)
            {
                throw new ModelValidationException("Store_Delete_HaveShiftRegUse", $"Không thể xóa cửa hàng '{store.StoreName}' vì đã có dữ liệu đăng ký ca thuộc cửa hàng này.");
            }
            _ctx.Store.Remove(store);
            await _ctx.SaveChangesAsync();
        }

        public async Task<bool> ExistsByCodeAsync(string storeCode, int? excludeStoreId = null)
        {
            var code = storeCode.Trim();
            if (excludeStoreId.HasValue)
            {
                return await _ctx.Store.AnyAsync(s => s.StoreCode == code && s.StoreId != excludeStoreId.Value);
            }
            return await _ctx.Store.AnyAsync(s => s.StoreCode == code);
        }

        public async Task UpdateActiveStatusAsync(int storeId, bool active)
        {
            var store = await GetRequiredByIdAsync(storeId);
            store.IsActive = active;
            store.UpdatedDate = DateTime.Now;
            store.UpdatedBy = AccountHelper.GetCurrentUsername(_httpContextAccessor);
            await _ctx.SaveChangesAsync();
        }
    }
}
