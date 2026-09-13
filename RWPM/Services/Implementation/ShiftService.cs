using Microsoft.EntityFrameworkCore;
using RWPM.Common;
using RWPM.Common.Exceptions;
using RWPM.Common.Helper;
using RWPM.Common.Models;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.Shift;
using RWPM.Services.Abstraction;

namespace RWPM.Services.Implementation
{
    public class ShiftService : IShiftService
    {
        private readonly DefaultDatabaseContext _ctx;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ShiftService(DefaultDatabaseContext ctx, IHttpContextAccessor httpContextAccessor)
        {
            _ctx = ctx;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<Shift?> GetByIdAsync(int shiftId, QueryOptions<Shift>? options = null)
        {
            var query = _ctx.Shift.Where(x => x.ShiftId == shiftId);
            query = QueryHelper.ApplyQueryOptions(query, options);
            return await query.FirstOrDefaultAsync();
        }

        public async Task<Shift> GetRequiredByIdAsync(int shiftId, QueryOptions<Shift>? options = null)
        {
            var shift = await GetByIdAsync(shiftId, options);
            if (shift == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy ca làm việc với ID {shiftId}.");
            }
            return shift;
        }

        public async Task<List<Shift>> GetAllAsync(QueryOptions<Shift>? options = null)
        {
            var query = _ctx.Shift.AsQueryable();
            query = QueryHelper.ApplyQueryOptions(query, options);
            return await query.ToListAsync();
        }

        public async Task<PaginationRes<Shift>> SearchAsync(ShiftSearch searchObject, QueryOptions<Shift>? options = null)
        {
            var query = _ctx.Shift.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchObject.Search))
            {
                var search = searchObject.Search.Trim();
                query = query.Where(s => s.ShiftCode.Contains(search) ||
                                         s.ShiftName.Contains(search) ||
                                         s.Description.Contains(search));
            }

            if (searchObject.IsActive.HasValue)
            {
                query = query.Where(s => s.IsActive == searchObject.IsActive.Value);
            }

            query = QueryHelper.ApplyQueryOptions(query, options);

            var totalRecords = await query.CountAsync();
            var data = await query
                .OrderBy(s => s.ShiftId)
                .Skip((searchObject.PageNumber - 1) * searchObject.PageSize)
                .Take(searchObject.PageSize)
                .ToListAsync();

            return new PaginationRes<Shift>(data, searchObject.PageNumber, searchObject.PageSize, totalRecords);
        }

        public async Task<Shift> CreateAsync(Shift entity)
        {
            entity.ShiftCode = entity.ShiftCode.Trim();
            entity.ShiftName = entity.ShiftName.Trim();

            if (await ExistsByCodeAsync(entity.ShiftCode))
            {
                throw new ModelValidationException("ShiftCode_Exists", $"Mã ca '{entity.ShiftCode}' đã tồn tại trong hệ thống.");
            }

            entity.CreatedDate = DateTime.Now;
            entity.CreatedBy = AccountHelper.GetCurrentUsername(_httpContextAccessor);

            await _ctx.Shift.AddAsync(entity);
            await _ctx.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateAsync(Shift entity)
        {
            entity.ShiftCode = entity.ShiftCode.Trim();
            entity.ShiftName = entity.ShiftName.Trim();

            var existingShift = await GetRequiredByIdAsync(entity.ShiftId, new QueryOptions<Shift> { NoTracking = false });

            if (await ExistsByCodeAsync(entity.ShiftCode, entity.ShiftId))
            {
                throw new ModelValidationException("ShiftCode_Exists", $"Mã ca '{entity.ShiftCode}' đã được sử dụng bởi ca khác.");
            }

            existingShift.ShiftCode = entity.ShiftCode;
            existingShift.ShiftName = entity.ShiftName;
            existingShift.Type = entity.Type;
            existingShift.StartTime = entity.StartTime;
            existingShift.EndTime = entity.EndTime;
            existingShift.StartTime2 = entity.StartTime2;
            existingShift.EndTime2 = entity.EndTime2;
            existingShift.BreakMinutes = entity.BreakMinutes;
            existingShift.Description = entity.Description?.Trim() ?? string.Empty;
            existingShift.IsActive = entity.IsActive;
            existingShift.UpdatedDate = DateTime.Now;
            existingShift.UpdatedBy = AccountHelper.GetCurrentUsername(_httpContextAccessor);

            await _ctx.SaveChangesAsync();
        }

        public async Task DeleteAsync(Shift entity)
        {
            var shift = await GetRequiredByIdAsync(entity.ShiftId, new QueryOptions<Shift> { NoTracking = false });
            _ctx.Shift.Remove(shift);
            await _ctx.SaveChangesAsync();
        }

        public async Task<bool> ExistsByCodeAsync(string shiftCode, int? excludeShiftId = null)
        {
            var code = shiftCode.Trim();
            if (excludeShiftId.HasValue)
            {
                return await _ctx.Shift.AnyAsync(s => s.ShiftCode == code && s.ShiftId != excludeShiftId.Value);
            }
            return await _ctx.Shift.AnyAsync(s => s.ShiftCode == code);
        }

        public async Task UpdateActiveStatusAsync(int shiftId, bool active)
        {
            var shift = await GetRequiredByIdAsync(shiftId, new QueryOptions<Shift> { NoTracking = false });
            shift.IsActive = active;
            shift.UpdatedDate = DateTime.Now;
            shift.UpdatedBy = AccountHelper.GetCurrentUsername(_httpContextAccessor);
            await _ctx.SaveChangesAsync();
        }
    }
}
