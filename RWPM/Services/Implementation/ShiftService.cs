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

            ValidateShiftTime(entity);

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

            ValidateShiftTime(entity);

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

        private void ValidateShiftTime(Shift entity)
        {
            if (entity.BreakMinutes > 60)
            {
                throw new ModelValidationException("Invalid_MaxBreakMinutes", "Thời gian nghỉ giữa ca tối đa là 60 phút (1 giờ).");
            }

            if (entity.EndTime <= entity.StartTime)
            {
                throw new ModelValidationException("Invalid_TimeRange", "Giờ kết thúc phải lớn hơn giờ bắt đầu.");
            }

            double period1Minutes = (entity.EndTime - entity.StartTime).TotalMinutes;
            double totalMinutes = period1Minutes;

            if (entity.Type == ShiftType.Split)
            {
                if (!entity.StartTime2.HasValue || !entity.EndTime2.HasValue)
                {
                    throw new ModelValidationException("Invalid_SplitShift_Required", "Ca gãy bắt buộc phải nhập đủ giờ bắt đầu và kết thúc của đợt 2.");
                }

                if (entity.EndTime2.Value <= entity.StartTime2.Value)
                {
                    throw new ModelValidationException("Invalid_SplitShift_TimeRange", "Giờ kết thúc đợt 2 phải lớn hơn giờ bắt đầu đợt 2.");
                }

                if (entity.StartTime2.Value < entity.EndTime)
                {
                    throw new ModelValidationException("Invalid_SplitShift_Overlap", "Khung giờ đợt 2 phải bắt đầu sau khi đợt 1 kết thúc.");
                }

                double period2Minutes = (entity.EndTime2.Value - entity.StartTime2.Value).TotalMinutes;

                if (period1Minutes < 90 || period2Minutes < 90 || (period1Minutes + period2Minutes) < 240)
                {
                    throw new ModelValidationException("Invalid_MinSplitPeriodDuration", "Mỗi đợt của ca gãy phải có thời lượng tối thiểu 1.5 tiếng (90 phút) và tổng ca tối thiểu 4 tiếng.");
                }

                totalMinutes += period2Minutes;
            }
            else
            {
                entity.StartTime2 = null;
                entity.EndTime2 = null;

                if (period1Minutes < 120)
                {
                    throw new ModelValidationException("Invalid_MinShiftDuration", "Thời lượng ca làm việc tối thiểu phải từ 2 tiếng (120 phút) trở lên.");
                }
            }

            if (entity.BreakMinutes > 0 && entity.BreakMinutes >= totalMinutes)
            {
                throw new ModelValidationException("Invalid_BreakMinutes", "Thời gian nghỉ không được vượt quá hoặc bằng thời lượng ca làm việc.");
            }

            if ((totalMinutes - entity.BreakMinutes) < 120)
            {
                throw new ModelValidationException("Invalid_MinShiftDuration", "Thời lượng làm việc thực tế sau khi trừ giờ nghỉ tối thiểu phải từ 2 tiếng trở lên.");
            }
        }

        public async Task DeleteAsync(Shift entity)
        {
            if (await _ctx.ShiftRegistration.AnyAsync(r => r.ShiftId == entity.ShiftId))
            {
                throw new ModelValidationException("Shift_InUse", "Không thể xóa ca làm việc đang được sử dụng trong lịch làm việc/đăng ký ca.");
            }

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
