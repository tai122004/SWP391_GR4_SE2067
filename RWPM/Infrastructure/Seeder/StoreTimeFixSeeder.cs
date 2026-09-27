using Microsoft.EntityFrameworkCore;
using RWPM.Infrastructure.Data;

namespace RWPM.Infrastructure.Seeder
{
    public static class StoreTimeFixSeeder
    {
        public static async Task FixStoreTimesAsync(DefaultDatabaseContext ctx)
        {
            try
            {
                var stores = await ctx.Store.ToListAsync();
                bool modified = false;
                foreach (var store in stores)
                {
                    if (store.OpeningTime.HasValue && store.ClosingTime.HasValue && store.OpeningTime > store.ClosingTime)
                    {
                        var temp = store.OpeningTime;
                        store.OpeningTime = store.ClosingTime;
                        store.ClosingTime = temp;
                        modified = true;
                    }
                }
                if (modified)
                {
                    await ctx.SaveChangesAsync();
                }
            }
            catch { }
        }
    }
}
