using RWPM.Common.Validation;
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
            var query = _ctx.Store
                .Include(x => x.Manager)
                .ThenInclude(m => m!.Account)
                .Where(x => x.StoreId == storeId);
            query = QueryHelper.ApplyQueryOptions(query, options);
            return await query.FirstOrDefaultAsync();
        }

        public async Task<global::Store> GetRequiredByIdAsync(int storeId, QueryOptions<global::Store>? options = null)
        {
            var store = await GetByIdAsync(storeId, options);
            if (store == null)
            {
                throw new ModelValidationException("Store_NotExists", $"ID {storeId}");
            }
            return store;
        }

        public async Task<SelectList> GetSelectListAsync(string? defaultOption = null)
        {
            var defaultDisplay = defaultOption ?? RWPM.Resources.Shared.Store.Dropdown_SelectStore;
            if (string.IsNullOrWhiteSpace(defaultDisplay))
            {
                defaultDisplay = "-- Select store --";
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
            searchObject.PageNumber = Math.Clamp(searchObject.PageNumber, 1, 1000000);
            searchObject.PageSize = Math.Clamp(searchObject.PageSize, 1, 50);

            var query = _ctx.Store
                .Include(x => x.Manager)
                .ThenInclude(m => m!.Account)
                .AsQueryable();

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

        private async Task ValidateStoreAsync(global::Store entity)
        {
            InputRules.Require(entity.OpeningTime.HasValue == entity.ClosingTime.HasValue, "ClosingTime", "Phải nhập đủ giờ mở và đóng cửa.", "Provide both opening and closing times.");
            AttendanceInputRules.ValidateCoordinates(entity.Latitude, entity.Longitude);
            AttendanceInputRules.ValidateRadius(entity.MinAllowedDistanceMeters, entity.AllowedRadiusMeters);
            InputRules.Require(!entity.IsActive || entity.Latitude.HasValue, "Latitude",
                "Vui lòng chọn vị trí chi nhánh trên bản đồ trước khi kích hoạt.",
                "Select the store location on the map before activating it.");
            if (entity.OpeningTime.HasValue)
                InputRules.Require(entity.OpeningTime >= TimeSpan.Zero && entity.OpeningTime < TimeSpan.FromDays(1)
                    && entity.ClosingTime >= TimeSpan.Zero && entity.ClosingTime < TimeSpan.FromDays(1)
                    && entity.OpeningTime != entity.ClosingTime, "ClosingTime", "Giờ mở/đóng không hợp lệ hoặc trùng nhau.", "Opening/closing times are invalid or identical.");
            InputRules.Require((entity.Address?.Length ?? 0) <= 255, "Address", "Địa chỉ không được vượt quá 255 ký tự.", "Address cannot exceed 255 characters.");
            InputRules.Require(string.IsNullOrEmpty(entity.Phone) || (entity.Phone.Length <= 20 && System.Text.RegularExpressions.Regex.IsMatch(entity.Phone, @"^[0-9]+$")),
                "Phone", "SĐT chi nhánh chỉ gồm chữ số, tối đa 20 ký tự.", "Store phone must contain digits only, up to 20 characters.");
            // Closing before opening denotes overnight operation; never silently swap the values.
            InputRules.Require(entity.StoreCode.Length is > 0 and <= 20 && System.Text.RegularExpressions.Regex.IsMatch(entity.StoreCode, RWPM.Common.RegexHelper.CodeRegex), "StoreCode", "Mã chi nhánh không hợp lệ.", "Invalid store code.");
            InputRules.Require(!string.IsNullOrWhiteSpace(entity.StoreName) && entity.StoreName.Length <= 100, "StoreName", "Tên chi nhánh không hợp lệ.", "Invalid store name.");
            if (entity.ManagerId.HasValue)
            {
                var manager = await _ctx.Employee.AsNoTracking().Include(x => x.Account).SingleOrDefaultAsync(x => x.EmployeeId == entity.ManagerId);
                InputRules.Require(manager != null && manager.IsActive && manager.Account.IsActive && (manager.Account.Role is RWPM.Common.Enums.AccountRole.StoreManager or RWPM.Common.Enums.AccountRole.Admin or RWPM.Common.Enums.AccountRole.HR)
                    , "ManagerId", "Người quản lý không hoạt động hoặc sai vai trò.", "Manager is inactive or has an invalid role.");
                InputRules.Require(!await _ctx.Store.AnyAsync(x => x.ManagerId == entity.ManagerId && x.StoreId != entity.StoreId), "ManagerId", "Người quản lý đã được phân công chi nhánh khác.", "Manager is already assigned to another store.");
            }
        }

        public async Task<global::Store> CreateAsync(global::Store entity)
        {
            entity.StoreCode = entity.StoreCode.Trim();
            entity.StoreName = entity.StoreName.Trim();
            await ValidateStoreAsync(entity);

            if (await ExistsByCodeAsync(entity.StoreCode))
            {
                throw new InputValidationException("StoreCode", InputRules.Text("Mã chi nhánh đã tồn tại.", "Store code already exists."));
            }

            entity.CreatedDate = DateTime.Now;
            entity.CreatedBy = AccountHelper.GetCurrentUsername(_httpContextAccessor);

            await _ctx.Store.AddAsync(entity);
            if (entity.ManagerId.HasValue)
            {
                // Quản lý cửa hàng được chuyển về chi nhánh mình quản lý; HR/Admin (hoặc người chưa có chi nhánh) chỉ gán khi chưa thuộc chi nhánh nào.
                var manager = await _ctx.Employee.Include(x => x.Account).SingleAsync(x => x.EmployeeId == entity.ManagerId);
                if (manager.StoreId == null || manager.Account.Role == RWPM.Common.Enums.AccountRole.StoreManager) manager.Store = entity;
            }
            await _ctx.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateAsync(global::Store entity)
        {
            entity.StoreCode = entity.StoreCode.Trim();
            entity.StoreName = entity.StoreName.Trim();
            await ValidateStoreAsync(entity);

            var existingStore = await GetRequiredByIdAsync(entity.StoreId);

            if (await ExistsByCodeAsync(entity.StoreCode, entity.StoreId))
            {
                throw new InputValidationException("StoreCode", InputRules.Text("Mã chi nhánh đã tồn tại.", "Store code already exists."));
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
            existingStore.ManagerId = entity.ManagerId;
            existingStore.OpeningTime = entity.OpeningTime;
            existingStore.ClosingTime = entity.ClosingTime;

            existingStore.UpdatedDate = DateTime.Now;
            existingStore.UpdatedBy = AccountHelper.GetCurrentUsername(_httpContextAccessor);

            // Bỏ navigation Manager cũ để EF không fix-up ManagerId về giá trị cũ khi attach graph.
            existingStore.Manager = null;
            _ctx.Store.Update(existingStore);
            if (existingStore.ManagerId.HasValue)
            {
                var manager = await _ctx.Employee.Include(x => x.Account).SingleAsync(x => x.EmployeeId == existingStore.ManagerId);
                if (manager.StoreId == null || manager.Account.Role == RWPM.Common.Enums.AccountRole.StoreManager) manager.StoreId = existingStore.StoreId;
            }
            await _ctx.SaveChangesAsync();
        }

        public async Task DeleteAsync(global::Store entity)
        {
            var store = await GetRequiredByIdAsync(entity.StoreId, new QueryOptions<global::Store> { NoTracking = false });

            var hasEmployees = await _ctx.Employee.AnyAsync(e => e.StoreId == entity.StoreId);
            if (hasEmployees)
            {
                throw new ModelValidationException("Store_Delete_HaveEmployeeUse", store.StoreName);
            }

            var hasShiftRegs = await _ctx.ShiftRegistration.AnyAsync(s => s.StoreId == entity.StoreId);
            if (hasShiftRegs)
            {
                throw new ModelValidationException("Store_Delete_HaveShiftRegUse", store.StoreName);
            }

            var storeId = entity.StoreId;
            var hasRelatedData = await _ctx.StoreShift.AnyAsync(x => x.StoreId == storeId)
                || await _ctx.AttendanceAdjustmentRequest.AnyAsync(x => x.StoreId == storeId)
                || await _ctx.LeaveRequest.AnyAsync(x => x.StoreId == storeId)
                || await _ctx.ShiftSwapRequest.AnyAsync(x => x.StoreId == storeId)
                || await _ctx.PayrollDetail.AnyAsync(x => x.StoreId == storeId)
                || await _ctx.PerformanceReview.AnyAsync(x => x.StoreId == storeId);
            if (hasRelatedData)
            {
                throw new InputValidationException("StoreId", InputRules.Text(
                    $"Không thể xóa chi nhánh {store.StoreName} vì đã có ca làm, đơn từ, chấm công hoặc dữ liệu lương liên quan. Hãy ngừng hoạt động chi nhánh thay vì xóa.",
                    $"Cannot delete store {store.StoreName} because it has shifts, requests, attendance or payroll data. Deactivate the store instead."));
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
            var store = await GetRequiredByIdAsync(storeId, new QueryOptions<global::Store> { NoTracking = false });
            store.IsActive = active;
            store.UpdatedDate = DateTime.Now;
            store.UpdatedBy = AccountHelper.GetCurrentUsername(_httpContextAccessor);
            await _ctx.SaveChangesAsync();
        }

        public async Task<StoreDetailsVM?> GetDetailsAsync(int storeId)
        {
            var store = await _ctx.Store.AsNoTracking()
                .Include(x => x.Manager)
                .ThenInclude(m => m!.Account)
                .FirstOrDefaultAsync(x => x.StoreId == storeId);

            if (store == null) return null;


            var employees = await _ctx.Employee.AsNoTracking()
                .Include(e => e.Account)
                .Where(e => e.StoreId == storeId)
                .OrderBy(e => e.EmployeeCode)
                .ToListAsync();

            var shifts = await _ctx.Shift.AsNoTracking()
                .Include(s => s.StoreShifts).ThenInclude(x => x.Store)
                .Where(s => s.StoreShifts.Any(x => x.StoreId == storeId && x.IsActive))
                .OrderBy(s => s.StartTime).ThenBy(s => s.ShiftName)
                .ToListAsync();

            var today = DateTime.Today;
            var usernames = employees.Select(e => e.Username).ToList();
            var todayRecords = await _ctx.AttendanceRecord.AsNoTracking()
                .Where(a => a.Date == today && usernames.Contains(a.Username))
                .ToListAsync();

            return new StoreDetailsVM
            {
                Store = store,
                Employees = employees,
                Shifts = shifts,
                TodayAttendanceRecords = todayRecords
            };
        }
    }
}
