using Microsoft.EntityFrameworkCore;
using RWPM.Common.Helper;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;

namespace RWPM.Infrastructure.Seeder;

class ShiftSeeder
{
    public async Task SeedAsync(DefaultDatabaseContext ctx)
    {
        // Seed only an empty catalogue; never overwrite users' assignments or re-enable links.
        if (await ctx.Shift.AnyAsync()) return;
        var stores = await ctx.Store.Where(x => x.IsActive).Select(x => x.StoreId).ToListAsync();
        if (stores.Count == 0) return;
        var shifts = new[] {
            Create("Ca sáng", new(8,30,0), new(16,0,0), new(12,0,0), new(12,30,0)),
            Create("Ca chiều tối", new(15,0,0), new(22,30,0), new(18,0,0), new(18,30,0)),
            Create("Ca cao điểm trưa", new(10,0,0), new(14,0,0)),
            Create("Ca cao điểm tối", new(18,0,0), new(22,0,0)),
            Create("Ca hành chính", new(8,30,0), new(17,30,0), new(12,0,0), new(13,0,0))
        };
        foreach (var shift in shifts)
            shift.StoreShifts = stores.Select(id => new StoreShift { StoreId = id, CreatedDate = shift.CreatedDate, CreatedBy = AccountHelper.SEEDER }).ToList();
        ctx.Shift.AddRange(shifts);
        await ctx.SaveChangesAsync();
    }
    private static Shift Create(string name, TimeSpan start, TimeSpan end, TimeSpan? breakStart = null, TimeSpan? breakEnd = null) => new()
    {
        ShiftName = name, StartTime = start, EndTime = end, EndDayOffset = 0,
        BreakStartTime = breakStart, BreakEndTime = breakEnd,
        BreakStartDayOffset = breakStart.HasValue ? (byte)0 : null,
        BreakEndDayOffset = breakEnd.HasValue ? (byte)0 : null,
        GracePeriodMinutes = RWPM.Common.Constants.ShiftDefaults.GracePeriodMinutes,
        EarlyCheckOutMinutes = RWPM.Common.Constants.ShiftDefaults.EarlyCheckOutMinutes,
        EffectiveFrom = new DateTime(2026,1,1), EffectiveTo = null,
        Description = name, IsActive = true, CreatedDate = DateTime.Now, CreatedBy = AccountHelper.SEEDER
    };
}
