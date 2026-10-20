using Microsoft.EntityFrameworkCore;
using RWPM.Infrastructure.Data;

namespace RWPM.Infrastructure.Seeder
{
    public static class StoreTimeFixSeeder
    {
        public static async Task FixStoreTimesAsync(DefaultDatabaseContext ctx)
        {
            // Closing before opening can represent overnight operation.
            // Startup must not silently rewrite valid store configuration.
            await Task.CompletedTask;
        }
    }
}
