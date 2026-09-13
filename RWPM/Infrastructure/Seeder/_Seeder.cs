using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using RWPM.Infrastructure.Data;

namespace RWPM.Infrastructure.Seeder
{
    public class _Seeder
    {
        private readonly DefaultDatabaseContext _ctx;

        public _Seeder(DefaultDatabaseContext databaseContext)
        {
            _ctx = databaseContext;
        }

        public async Task SeedAsync()
        {
            using (var transaction = await _ctx.Database.BeginTransactionAsync())
            {
                try
                {
                    //await new FactoryCatSeeder().SeedAsync(_ctx);
                    //await new DeptCatSeeder().SeedAsync(_ctx);
                    //await new AccSeeder().SeedAsync(_ctx);
                    //await new DocGrpCatSeeder().SeedAsync(_ctx);
                    //await new DocTypeCatSeeder().SeedAsync(_ctx);
                    //await new LeaveJobReasonSeeder().SeedAsync(_ctx);
                    
                    await new ShiftRegistrationSeeder().SeedAsync(_ctx);

                    await transaction.CommitAsync();
                }
                catch 
                { 
                    await transaction.RollbackAsync();
                }
            }

        }
    }

}
