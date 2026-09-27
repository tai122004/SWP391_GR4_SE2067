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
using System.Text.RegularExpressions;

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
            var query = _ctx.Shift.Include(x => x.Store).Where(x => x.ShiftId == shiftId);
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
            var query = _ctx.Shift.Include(x => x.Store).AsQueryable();
            query = QueryHelper.ApplyQueryOptions(query, options);
            return await query.ToListAsync();
        }

        public Task<List<Shift>> GetAvailableForDateAsync(DateTime workDate, int storeId)
        {
            var day = workDate.Date;
            return _ctx.Shift.AsNoTracking()
                .Where(s => s.IsActive && (s.StoreId == null || s.StoreId == storeId)
                    && s.EffectiveFrom <= day && (s.EffectiveTo == null || s.EffectiveTo >= day))
                .OrderBy(s => s.StartTime).ThenBy(s => s.ShiftCode)
                .ToListAsync();
        }

        public async Task<PaginationRes<Shift>> SearchAsync(ShiftSearch searchObject, QueryOptions<Shift>? options = null)
        {
            var query = _ctx.Shift.Include(s => s.Store).AsQueryable();

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

            if (searchObject.IsTemplate.HasValue)
            {
                query = query.Where(s => s.IsTemplate == searchObject.IsTemplate.Value);
            }

            if (searchObject.StoreId.HasValue)
            {
                query = query.Where(s => s.StoreId == null || s.StoreId == searchObject.StoreId.Value);
            }
            if (searchObject.EffectiveOn.HasValue)
            {
                var day = searchObject.EffectiveOn.Value.Date;
                query = query.Where(s => s.EffectiveFrom <= day && (s.EffectiveTo == null || s.EffectiveTo >= day));
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
            entity.ShiftCode = NormalizeCode(entity.ShiftCode);
            entity.ShiftName = entity.ShiftName.Trim();

            if (await ExistsByCodeAsync(entity.ShiftCode))
            {
                throw new ModelValidationException("ShiftCode_Exists", $"Mã ca '{entity.ShiftCode}' đã tồn tại trong hệ thống.");
            }

            await ValidateShiftAsync(entity);

            entity.CreatedDate = DateTime.Now;
            entity.CreatedBy = AccountHelper.GetCurrentUsername(_httpContextAccessor);

            await _ctx.Shift.AddAsync(entity);
            await _ctx.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateAsync(Shift entity)
        {
            entity.ShiftCode = NormalizeCode(entity.ShiftCode);
            entity.ShiftName = entity.ShiftName.Trim();
            var existingShift = await GetRequiredByIdAsync(entity.ShiftId, new QueryOptions<Shift> { NoTracking = false });

            if (await ExistsByCodeAsync(entity.ShiftCode, entity.ShiftId))
            {
                throw new ModelValidationException("ShiftCode_Exists", $"Mã ca '{entity.ShiftCode}' đã được sử dụng bởi ca khác.");
            }

            var used = await _ctx.ShiftRegistration.AnyAsync(x => x.ShiftId == entity.ShiftId)
                       || await _ctx.AttendanceRecord.AnyAsync(x => x.ShiftId == entity.ShiftId);
            if (used && (existingShift.ShiftCode != entity.ShiftCode || existingShift.Type != entity.Type
                || existingShift.StartTime != entity.StartTime || existingShift.EndTime != entity.EndTime
                || existingShift.BreakMinutes != entity.BreakMinutes || existingShift.IsBreakPaid != entity.IsBreakPaid
                || existingShift.GracePeriodMinutes != entity.GracePeriodMinutes
                || existingShift.EarlyCheckInMinutes != entity.EarlyCheckInMinutes
                || existingShift.EarlyCheckOutMinutes != entity.EarlyCheckOutMinutes
                || existingShift.LateThresholdMinutes != entity.LateThresholdMinutes
                || existingShift.IsTemplate != entity.IsTemplate || existingShift.StoreId != entity.StoreId
                || existingShift.EffectiveFrom.Date != entity.EffectiveFrom.Date
                || existingShift.EffectiveTo?.Date != entity.EffectiveTo?.Date
                || existingShift.AllowOutsideStoreHours != entity.AllowOutsideStoreHours))
            {
                throw new ModelValidationException("Shift_Used", "Ca đã được sử dụng. Hãy nhân bản ca để đổi giờ, phạm vi, hiệu lực hoặc chính sách chấm công.");
            }
            // Ca đã được sử dụng vẫn phải chạy đầy đủ validation, đặc biệt là
            // kiểm tra giờ cửa hàng và rule ca qua ngày. Phần phía trên đã chặn
            // thay đổi các thuộc tính lõi của ca đã có dữ liệu.
            await ValidateShiftAsync(entity);
            if (existingShift.IsActive && !entity.IsActive)
                await EnsureNoFutureRegistrationsAsync(entity.ShiftId);

            existingShift.ShiftCode = entity.ShiftCode;
            existingShift.ShiftName = entity.ShiftName;
            existingShift.Type = entity.Type;
            existingShift.StartTime = entity.StartTime;
            existingShift.EndTime = entity.EndTime;
            existingShift.BreakMinutes = entity.BreakMinutes;
            existingShift.IsBreakPaid = entity.IsBreakPaid;
            existingShift.EffectiveFrom = entity.EffectiveFrom.Date;
            existingShift.EffectiveTo = entity.EffectiveTo?.Date;
            existingShift.AllowOutsideStoreHours = entity.AllowOutsideStoreHours;
            existingShift.OutsideHoursReason = null;
            existingShift.DefaultRequiredHeadcount = entity.DefaultRequiredHeadcount;
            existingShift.DefaultMaximumHeadcount = entity.DefaultMaximumHeadcount;
            existingShift.GracePeriodMinutes = entity.GracePeriodMinutes;
            existingShift.EarlyCheckInMinutes = entity.EarlyCheckInMinutes;
            existingShift.EarlyCheckOutMinutes = entity.EarlyCheckOutMinutes;
            existingShift.LateThresholdMinutes = entity.LateThresholdMinutes;
            existingShift.IsTemplate = entity.IsTemplate;
            existingShift.StoreId = entity.StoreId;
            existingShift.Description = entity.Description?.Trim() ?? string.Empty;
            existingShift.IsActive = entity.IsActive;
            existingShift.UpdatedDate = DateTime.Now;
            existingShift.UpdatedBy = AccountHelper.GetCurrentUsername(_httpContextAccessor);

            await _ctx.SaveChangesAsync();
        }

        private async Task ValidateShiftAsync(Shift entity)
        {
            if (string.IsNullOrWhiteSpace(entity.ShiftCode) || !Regex.IsMatch(entity.ShiftCode, "^[A-Z0-9_-]{1,30}$"))
                throw new ModelValidationException("ShiftCode_Invalid", "Mã ca chỉ gồm chữ A-Z, số, dấu gạch dưới hoặc gạch ngang (tối đa 30 ký tự).");
            if (string.IsNullOrWhiteSpace(entity.ShiftName) || entity.ShiftName.Length > 100)
                throw new ModelValidationException("ShiftName_Invalid", "Tên ca phải có từ 1 đến 100 ký tự.");
            if (!Enum.IsDefined(typeof(ShiftType), entity.Type))
                throw new ModelValidationException("ShiftType_Invalid", "Loại ca không hợp lệ.");
            if (entity.Type == ShiftType.Flexible && (entity.IsTemplate || !entity.StoreId.HasValue))
                throw new ModelValidationException("Shift_FlexibleStoreRequired", "Ca Flexible phải là ca riêng của một cửa hàng.");
            if (entity.StartTime < TimeSpan.Zero || entity.EndTime > TimeSpan.FromDays(1))
                throw new ModelValidationException("Invalid_TimeRange", "Giờ ca phải nằm trong một ngày.");
            if (entity.BreakMinutes < 0 || entity.BreakMinutes > 60)
            {
                throw new ModelValidationException("Invalid_MaxBreakMinutes", "Thời gian nghỉ giữa ca tối đa là 60 phút (1 giờ).");
            }

            if (entity.EndTime == entity.StartTime)
            {
                throw new ModelValidationException("Invalid_TimeRange", "Giờ kết thúc không được trùng giờ bắt đầu.");
            }

            double period1Minutes = ShiftTimeHelper.GetDuration(entity.StartTime, entity.EndTime).TotalMinutes;
            double totalMinutes = period1Minutes;

            if (period1Minutes < 120)
            {
                throw new ModelValidationException("Invalid_MinShiftDuration", "Thời lượng ca làm việc tối thiểu phải từ 2 tiếng (120 phút) trở lên.");
            }

            if (entity.BreakMinutes > 0 && entity.BreakMinutes >= totalMinutes)
            {
                throw new ModelValidationException("Invalid_BreakMinutes", "Thời gian nghỉ không được vượt quá hoặc bằng thời lượng ca làm việc.");
            }

            if ((totalMinutes - entity.BreakMinutes) < 120)
            {
                throw new ModelValidationException("Invalid_MinShiftDuration", "Thời lượng làm việc thực tế sau khi trừ giờ nghỉ tối thiểu phải từ 2 tiếng trở lên.");
            }

            if (entity.EffectiveFrom == default || entity.EffectiveTo?.Date < entity.EffectiveFrom.Date)
                throw new ModelValidationException("Shift_EffectiveDate", "Ngày hết hiệu lực phải từ ngày bắt đầu hiệu lực trở đi.");
            ValidateHeadcount(entity);
            if (entity.IsTemplate)
            {
                if (entity.StoreId.HasValue)
                    throw new ModelValidationException("Shift_StoreScope", "Ca dùng chung không được gắn cửa hàng.");
            }
            else
            {
                if (!entity.StoreId.HasValue)
                    throw new ModelValidationException("StoreId_Required", "Ca riêng phải chọn cửa hàng.");
                var store = await _ctx.Store.AsNoTracking().FirstOrDefaultAsync(x => x.StoreId == entity.StoreId);
                if (store == null || !store.IsActive)
                    throw new ModelValidationException("Shift_StoreInactive", "Cửa hàng không tồn tại hoặc đã ngừng hoạt động.");

                if (store.OpeningTime.HasValue && store.ClosingTime.HasValue)
                {
                    var insideStoreHours = IsWithinStoreHours(
                        entity.StartTime,
                        entity.EndTime,
                        store.OpeningTime.Value,
                        store.ClosingTime.Value);

                    // Áp dụng cho cả ca thường và ca qua ngày, ví dụ 21:00–01:00.
                    if (!insideStoreHours && !entity.AllowOutsideStoreHours)
                    {
                        throw new ModelValidationException(
                            "Shift_OutsideStoreHours",
                            "Khung giờ ca nằm ngoài giờ hoạt động của cửa hàng. Vui lòng bật 'Cho phép làm ngoài giờ cửa hàng' và nhập lý do.");
                    }

                }
            }

            if (entity.GracePeriodMinutes is < 0 or > 60)
            {
                throw new ModelValidationException("Invalid_GracePeriodMinutes", "Thời gian cho phép đi muộn phải từ 0 đến 60 phút.");
            }

            if (entity.EarlyCheckInMinutes is < 0 or > 120)
            {
                throw new ModelValidationException("Invalid_EarlyCheckInMinutes", "Thời gian chấm vào sớm phải từ 0 đến 120 phút.");
            }

            if (entity.LateThresholdMinutes is < 0 or > 240)
            {
                throw new ModelValidationException("Invalid_LateThresholdMinutes", "Ngưỡng đi muộn phải từ 0 đến 240 phút.");
            }

            if (entity.EarlyCheckOutMinutes is < 0 or > 60)
                throw new ModelValidationException("Invalid_EarlyCheckOutMinutes", "Ngưỡng chấm ra sớm phải từ 0 đến 60 phút.");
            var grace = entity.GracePeriodMinutes ?? Common.Constants.ShiftDefaults.GracePeriodMinutes;
            var late = entity.LateThresholdMinutes ?? Common.Constants.ShiftDefaults.LateThresholdMinutes;
            if (grace >= late || late >= totalMinutes)
            {
                throw new ModelValidationException("Invalid_GracePeriodThreshold", "Thời gian cho phép đi muộn phải nhỏ hơn ngưỡng vắng mặt.");
            }
        }



        public async Task<bool> ExistsByCodeAsync(string shiftCode, int? excludeShiftId = null)
        {
            var code = NormalizeCode(shiftCode);
            if (excludeShiftId.HasValue)
            {
                return await _ctx.Shift.AnyAsync(s => s.ShiftCode.ToUpper() == code && s.ShiftId != excludeShiftId.Value);
            }
            return await _ctx.Shift.AnyAsync(s => s.ShiftCode.ToUpper() == code);
        }

        public async Task UpdateActiveStatusAsync(int shiftId, bool active)
        {
            var shift = await GetRequiredByIdAsync(shiftId, new QueryOptions<Shift> { NoTracking = false });
            if (shift.IsActive && !active)
                await EnsureNoFutureRegistrationsAsync(shiftId);
            shift.IsActive = active;
            shift.UpdatedDate = DateTime.Now;
            shift.UpdatedBy = AccountHelper.GetCurrentUsername(_httpContextAccessor);
            await _ctx.SaveChangesAsync();
        }

        public async Task<Shift> CloneAsync(int shiftId, string newCode)
        {
            var source = await GetRequiredByIdAsync(shiftId, new QueryOptions<Shift> { NoTracking = true });
            return await CreateAsync(new Shift
            {
                ShiftCode = newCode, ShiftName = source.ShiftName, Type = source.Type,
                StartTime = source.StartTime, EndTime = source.EndTime, BreakMinutes = source.BreakMinutes,
                IsBreakPaid = source.IsBreakPaid, GracePeriodMinutes = source.GracePeriodMinutes,
                EarlyCheckInMinutes = source.EarlyCheckInMinutes, EarlyCheckOutMinutes = source.EarlyCheckOutMinutes,
                LateThresholdMinutes = source.LateThresholdMinutes, IsTemplate = source.IsTemplate,
                StoreId = source.StoreId, Description = source.Description, IsActive = true,
                EffectiveFrom = DateTime.Today, EffectiveTo = null,
                AllowOutsideStoreHours = source.AllowOutsideStoreHours,
                OutsideHoursReason = null,
                DefaultRequiredHeadcount = source.DefaultRequiredHeadcount,
                DefaultMaximumHeadcount = source.DefaultMaximumHeadcount
            });
        }

        private static string NormalizeCode(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

        private static bool IsWithinStoreHours(TimeSpan shiftStart, TimeSpan shiftEnd, TimeSpan storeOpen, TimeSpan storeClose)
        {
            var shiftDuration = ShiftTimeHelper.GetDuration(shiftStart, shiftEnd);
            var storeDuration = ShiftTimeHelper.GetDuration(storeOpen, storeClose);
            if (shiftDuration > storeDuration) return false;

            var shiftStartMinutes = shiftStart.TotalMinutes;
            var shiftEndMinutes = shiftStartMinutes + shiftDuration.TotalMinutes;
            var storeStartMinutes = storeOpen.TotalMinutes;
            var storeEndMinutes = storeStartMinutes + storeDuration.TotalMinutes;

            // Với cửa hàng mở qua đêm, chuẩn hóa khoảng giờ cửa hàng về cùng trục thời gian.
            // Với cửa hàng thường, ca qua đêm sẽ tự vượt quá giờ đóng cửa và bị từ chối.
            if (storeClose < storeOpen && shiftStartMinutes < storeStartMinutes)
            {
                shiftStartMinutes += 1440;
                shiftEndMinutes += 1440;
            }

            return shiftStartMinutes >= storeStartMinutes && shiftEndMinutes <= storeEndMinutes;
        }


        private static void ValidateHeadcount(Shift entity)
        {
            if (entity.DefaultRequiredHeadcount.HasValue != entity.DefaultMaximumHeadcount.HasValue
                || entity.DefaultRequiredHeadcount is < 1 or > 1000 || entity.DefaultMaximumHeadcount is < 1 or > 1000
                || (entity.DefaultRequiredHeadcount.HasValue && entity.DefaultMaximumHeadcount < entity.DefaultRequiredHeadcount))
                throw new ModelValidationException("Shift_Headcount", "Số người mặc định phải dương và số tối đa không nhỏ hơn số cần.");
        }

        private async Task EnsureNoFutureRegistrationsAsync(int shiftId)
        {
            var count = await _ctx.ShiftRegistration.CountAsync(x => x.ShiftId == shiftId
                && x.WorkDate >= DateTime.Today
                && (x.Status == RegistrationStatus.Pending || x.Status == RegistrationStatus.Approved));
            if (count > 0)
                throw new ModelValidationException("Shift_FutureRegistrations", $"Còn {count} đăng ký ca từ hôm nay trở đi. Hãy xử lý trước khi ngừng ca.");
        }
    }
}
