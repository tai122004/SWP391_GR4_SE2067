using Microsoft.EntityFrameworkCore;
using RWPM.Common.Enums;
using RWPM.Common.Helper;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;

namespace RWPM.Infrastructure.Seeder
{
    class ShiftSeeder
    {
        private static readonly DateTime SeedEffectiveFrom = new(2026, 1, 1);

        public async Task SeedAsync(DefaultDatabaseContext databaseContext)
        {
            var seedShifts = new[]
            {
                CreateTemplate("S", "Ca sáng", ShiftType.Morning, new(8, 30, 0), new(16, 0, 0), 30, 2, 4, "Ca sáng: 08:30 - 16:00"),
                CreateTemplate("C", "Ca chiều tối", ShiftType.Afternoon, new(15, 0, 0), new(22, 30, 0), 30, 2, 4, "Ca chiều tối: 15:00 - 22:30"),
                CreateTemplate("CDT", "Ca cao điểm trưa", ShiftType.Peak, new(10, 0, 0), new(14, 0, 0), 0, 1, 3, "Ca cao điểm trưa: 10:00 - 14:00"),
                CreateTemplate("CDTOI", "Ca cao điểm tối", ShiftType.Peak, new(18, 0, 0), new(22, 0, 0), 0, 2, 5, "Ca cao điểm tối: 18:00 - 22:00"),
                CreateTemplate("HC", "Ca hành chính", ShiftType.FullDay, new(8, 30, 0), new(17, 30, 0), 60, 1, 2, "Ca hành chính: 08:30 - 17:30"),
                CreateTemplate("OT", "Ca tăng ca", ShiftType.Overtime, new(18, 0, 0), new(22, 0, 0), 0, 1, 3, "Ca tăng ca: 18:00 - 22:00")
            };

            var existingShifts = await databaseContext.Shift
                .Where(s => s.IsTemplate && s.StoreId == null)
                .ToDictionaryAsync(s => s.ShiftCode);

            foreach (var seedShift in seedShifts)
            {
                if (!existingShifts.TryGetValue(seedShift.ShiftCode, out var existing))
                {
                    await databaseContext.Shift.AddAsync(seedShift);
                    continue;
                }

                // Chỉ đồng bộ dữ liệu mẫu chuẩn; không đụng vào ca riêng của cửa hàng.
                existing.EffectiveFrom = seedShift.EffectiveFrom;
                existing.EffectiveTo = seedShift.EffectiveTo;
                existing.DefaultRequiredHeadcount = seedShift.DefaultRequiredHeadcount;
                existing.DefaultMaximumHeadcount = seedShift.DefaultMaximumHeadcount;
                existing.IsTemplate = true;
                existing.StoreId = null;
            }

            await databaseContext.SaveChangesAsync();
        }

        private static Shift CreateTemplate(string code, string name, ShiftType type, TimeSpan start, TimeSpan end,
            int breakMinutes, int requiredHeadcount, int maximumHeadcount, string description)
        {
            code = code.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(code) || code.Any(char.IsWhiteSpace))
                throw new InvalidOperationException("Shift seed code must be uppercase and contain no whitespace.");
            if (end <= start || breakMinutes < 0 || breakMinutes >= (end - start).TotalMinutes)
                throw new InvalidOperationException($"Invalid time or break configuration for shift seed '{code}'.");
            if (requiredHeadcount < 1 || maximumHeadcount < requiredHeadcount)
                throw new InvalidOperationException($"Invalid headcount configuration for shift seed '{code}'.");

            return new Shift
            {
                ShiftCode = code, ShiftName = name, Type = type,
                StartTime = start, EndTime = end, BreakMinutes = breakMinutes, IsBreakPaid = false,
                GracePeriodMinutes = null, EarlyCheckInMinutes = null, EarlyCheckOutMinutes = null, LateThresholdMinutes = null,
                IsTemplate = true, StoreId = null, EffectiveFrom = SeedEffectiveFrom, EffectiveTo = null,
                AllowOutsideStoreHours = false, OutsideHoursReason = null,
                DefaultRequiredHeadcount = requiredHeadcount, DefaultMaximumHeadcount = maximumHeadcount,
                Description = description, IsActive = true, CreatedDate = DateTime.Now, CreatedBy = AccountHelper.SEEDER
            };
        }
    }
}
