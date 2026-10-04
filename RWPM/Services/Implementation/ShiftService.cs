using Microsoft.EntityFrameworkCore;
using RWPM.Common;
using RWPM.Common.Enums;
using RWPM.Common.Exceptions;
using RWPM.Common.Helper;
using RWPM.Common.Models;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.Shift;
using RWPM.Services.Abstraction;

namespace RWPM.Services.Implementation;

public class ShiftService : IShiftService
{
    private readonly DefaultDatabaseContext _ctx;
    private readonly IHttpContextAccessor _accessor;
    public ShiftService(DefaultDatabaseContext ctx, IHttpContextAccessor accessor)
    { _ctx = ctx; _accessor = accessor; }
    private bool IsStoreManager => _accessor.HttpContext?.User.IsInRole("StoreManager") == true
        && _accessor.HttpContext.User.IsInRole("Admin") == false;
    private bool IsAdmin => _accessor.HttpContext?.User.IsInRole("Admin") == true;
    public async Task<int?> GetManagedStoreIdAsync()
    {
        if (!IsStoreManager) return null;
        var username = _accessor.HttpContext?.User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username)) return null;
        return await _ctx.Employee.AsNoTracking()
            .Where(x => x.Username == username && x.IsActive && x.Store.IsActive
                && x.Account.IsActive && x.Account.Role == AccountRole.StoreManager)
            .Select(x => (int?)x.StoreId).FirstOrDefaultAsync();
    }
    private async Task<int> RequireManagerStoreAsync() => await GetManagedStoreIdAsync()
        ?? throw new UnauthorizedAccessException("Tài khoản quản lý chưa được gán chi nhánh hoạt động.");
    private IQueryable<Shift> Query() => _ctx.Shift.Include(x => x.StoreShifts).ThenInclude(x => x.Store);
    public async Task<Shift?> GetByIdAsync(int id, QueryOptions<Shift>? options = null) =>
        await QueryHelper.ApplyQueryOptions(Query().Where(x => x.ShiftId == id), options).FirstOrDefaultAsync();
    public async Task<Shift> GetRequiredByIdAsync(int id, QueryOptions<Shift>? options = null) =>
        await GetByIdAsync(id, options) ?? throw new KeyNotFoundException($"Không tìm thấy ca {id}.");
    public async Task<List<Shift>> GetAllAsync(QueryOptions<Shift>? options = null) =>
        await QueryHelper.ApplyQueryOptions(Query(), options).ToListAsync();
    public Task<List<Shift>> GetAvailableForDateAsync(DateTime date, int storeId)
    {
        var day = date.Date;
        return Query().AsNoTracking().Where(s => s.IsActive && s.EffectiveFrom <= day
            && (s.EffectiveTo == null || s.EffectiveTo >= day)
            && s.StoreShifts.Any(x => x.StoreId == storeId && x.IsActive && x.Store.IsActive))
            .OrderBy(s => s.StartTime).ThenBy(s => s.ShiftName).ToListAsync();
    }
    public async Task<PaginationRes<Shift>> SearchAsync(ShiftSearch search, QueryOptions<Shift>? options = null)
    {
        var query = Query();
        if (IsStoreManager)
        {
            var managedStoreId = await RequireManagerStoreAsync();
            query = query.Where(s => s.StoreShifts.Any(x => x.StoreId == managedStoreId));
            search.StoreId = managedStoreId;
        }
        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var term = search.Search.Trim();
            query = query.Where(s => s.ShiftName.Contains(term) || (s.Description != null && s.Description.Contains(term)));
        }
        if (search.IsActive.HasValue) query = query.Where(s => s.IsActive == search.IsActive);
        if (search.StoreId.HasValue) query = query.Where(s => s.StoreShifts.Any(x => x.StoreId == search.StoreId && x.IsActive));
        if (search.EffectiveOn.HasValue)
        {
            var day = search.EffectiveOn.Value.Date;
            query = query.Where(s => s.EffectiveFrom <= day && (s.EffectiveTo == null || s.EffectiveTo >= day));
        }
        query = QueryHelper.ApplyQueryOptions(query, options);
        var total = await query.CountAsync();
        var page = Math.Max(1, search.PageNumber);
        return new PaginationRes<Shift>(await query.OrderBy(s => s.ShiftId).Skip((page - 1) * 10).Take(10).ToListAsync(), page, 10, total);
    }
    private async Task ValidateAsync(Shift shift)
    {
        var error = ShiftRules.Validate(shift).FirstOrDefault();
        if (error != null) throw new ModelValidationException("Shift_Invalid", error);
        var ids = shift.StoreShifts.Where(x => x.IsActive).Select(x => x.StoreId).Distinct().ToList();
        if (ids.Count == 0) throw new ModelValidationException("StoreId_Required", "Vui lòng chọn ít nhất một chi nhánh.");
        if (await _ctx.Store.CountAsync(x => ids.Contains(x.StoreId) && x.IsActive) != ids.Count)
            throw new ModelValidationException("Shift_StoreInactive", "Chi nhánh không tồn tại hoặc đã ngừng hoạt động.");
    }
    public async Task<Shift> CreateAsync(Shift shift)
    {
        if (!IsAdmin)
        {
            if (!IsStoreManager) throw new UnauthorizedAccessException("Không có quyền tạo ca.");
            var managedStoreId = await RequireManagerStoreAsync();
            if (shift.StoreShifts.Count != 1 || shift.StoreShifts.Single().StoreId != managedStoreId)
                throw new UnauthorizedAccessException("Chỉ được tạo ca cho chi nhánh đang quản lý.");
        }
        await ValidateAsync(shift);
        shift.ShiftName = shift.ShiftName.Trim();
        shift.CreatedDate = DateTime.Now; shift.CreatedBy = AccountHelper.GetCurrentUsername(_accessor);
        foreach (var link in shift.StoreShifts) { link.CreatedDate = shift.CreatedDate; link.CreatedBy = shift.CreatedBy; }
        _ctx.Shift.Add(shift);
        await _ctx.SaveChangesAsync();
        return shift;
    }
    public async Task UpdateAsync(Shift candidate)
    {
        var existing = await GetRequiredByIdAsync(candidate.ShiftId, new QueryOptions<Shift> { NoTracking = false });
        if (!IsAdmin)
        {
            if (!IsStoreManager) throw new UnauthorizedAccessException("Không có quyền sửa ca.");
            var managedStoreId = await RequireManagerStoreAsync();
            if (existing.StoreShifts.Count != 1 || existing.StoreShifts.Single().StoreId != managedStoreId
                || candidate.StoreShifts.Count != 1 || candidate.StoreShifts.Single().StoreId != managedStoreId)
                throw new UnauthorizedAccessException("Ca dùng chung hoặc ca thuộc chi nhánh khác không thể sửa tại cửa hàng này.");
        }
        await ValidateAsync(candidate);
        var used = await _ctx.ShiftRegistration.AnyAsync(x => x.ShiftId == existing.ShiftId)
            || await _ctx.AttendanceRecord.AnyAsync(x => x.ShiftId == existing.ShiftId);
        if (used && (existing.StartTime != candidate.StartTime || existing.EndTime != candidate.EndTime
            || existing.EndDayOffset != candidate.EndDayOffset || existing.BreakStartTime != candidate.BreakStartTime
            || existing.BreakEndTime != candidate.BreakEndTime || existing.BreakStartDayOffset != candidate.BreakStartDayOffset
            || existing.BreakEndDayOffset != candidate.BreakEndDayOffset || existing.GracePeriodMinutes != candidate.GracePeriodMinutes
            || existing.EarlyCheckOutMinutes != candidate.EarlyCheckOutMinutes
            || existing.EffectiveFrom.Date != candidate.EffectiveFrom.Date || existing.EffectiveTo?.Date != candidate.EffectiveTo?.Date))
            throw new ModelValidationException("Shift_Used", "Ca đã được sử dụng. Vui lòng tạo ca mới để thay đổi giờ làm, hiệu lực hoặc chính sách chấm công.");
        if (existing.IsActive && !candidate.IsActive) await EnsureNoFutureAsync(existing.ShiftId);
        var selected = candidate.StoreShifts.Where(x => x.IsActive).Select(x => x.StoreId).ToHashSet();
        foreach (var link in existing.StoreShifts.Where(x => x.IsActive && !selected.Contains(x.StoreId)))
            await EnsureNoFutureAsync(existing.ShiftId, link.StoreId);
        var now = DateTime.Now; var username = AccountHelper.GetCurrentUsername(_accessor);
        foreach (var link in existing.StoreShifts)
        {
            if (link.IsActive != selected.Contains(link.StoreId))
            { link.IsActive = selected.Contains(link.StoreId); link.UpdatedDate = now; link.UpdatedBy = username; }
        }
        foreach (var storeId in selected.Where(id => !existing.StoreShifts.Any(x => x.StoreId == id)))
            existing.StoreShifts.Add(new StoreShift { StoreId = storeId, CreatedDate = now, CreatedBy = username });
        existing.ShiftName = candidate.ShiftName.Trim(); existing.Description = candidate.Description?.Trim();
        existing.StartTime = candidate.StartTime; existing.EndTime = candidate.EndTime; existing.EndDayOffset = candidate.EndDayOffset;
        existing.BreakStartTime = candidate.BreakStartTime; existing.BreakEndTime = candidate.BreakEndTime;
        existing.BreakStartDayOffset = candidate.BreakStartDayOffset; existing.BreakEndDayOffset = candidate.BreakEndDayOffset;
        existing.GracePeriodMinutes = candidate.GracePeriodMinutes; existing.EarlyCheckOutMinutes = candidate.EarlyCheckOutMinutes;
        existing.EffectiveFrom = candidate.EffectiveFrom.Date; existing.EffectiveTo = candidate.EffectiveTo?.Date;
        existing.IsActive = candidate.IsActive; existing.UpdatedDate = now; existing.UpdatedBy = username;
        await _ctx.SaveChangesAsync();
    }
    public async Task UpdateActiveStatusAsync(int id, bool active)
    {
        var shift = await GetRequiredByIdAsync(id, new QueryOptions<Shift> { NoTracking = false });
        if (!IsAdmin)
        {
            if (!IsStoreManager) throw new UnauthorizedAccessException("Không có quyền thay đổi ca.");
            var managedStoreId = await RequireManagerStoreAsync();
            var link = shift.StoreShifts.SingleOrDefault(x => x.StoreId == managedStoreId)
                ?? throw new UnauthorizedAccessException("Ca không thuộc chi nhánh đang quản lý.");
            if (active && !shift.IsActive)
                throw new ModelValidationException("Shift_Inactive", "Ca đã bị ngừng hoạt động toàn hệ thống; vui lòng liên hệ Admin.");
            if (link.IsActive && !active) await EnsureNoFutureAsync(id, managedStoreId);
            link.IsActive = active;
            link.UpdatedDate = DateTime.Now;
            link.UpdatedBy = AccountHelper.GetCurrentUsername(_accessor);
            await _ctx.SaveChangesAsync();
            return;
        }
        if (shift.IsActive && !active) await EnsureNoFutureAsync(id);
        shift.IsActive = active; shift.UpdatedDate = DateTime.Now; shift.UpdatedBy = AccountHelper.GetCurrentUsername(_accessor);
        await _ctx.SaveChangesAsync();
    }
    private async Task EnsureNoFutureAsync(int id, int? storeId = null)
    {
        if (await _ctx.ShiftRegistration.AnyAsync(x => x.ShiftId == id && (!storeId.HasValue || x.StoreId == storeId)
            && x.WorkDate >= DateTime.Today && (x.Status == RegistrationStatus.Pending || x.Status == RegistrationStatus.Approved)))
            throw new ModelValidationException("Shift_FutureRegistrations", "Còn đăng ký từ hôm nay trở đi. Hãy xử lý trước khi ngừng ca hoặc bỏ chi nhánh áp dụng.");
    }
}
