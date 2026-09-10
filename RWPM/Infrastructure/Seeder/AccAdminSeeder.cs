using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RWPM.Common.Enums;
using RWPM.Common.Helper;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;

namespace RWPM.Infrastructure.Seeder
{
    class AccAdminSeeder
    {
        public async Task SeedAsync(DefaultDatabaseContext databaseContext)
        {
            var hasher = new PasswordHasher<Acc>();

            if (!await databaseContext.Acc.AnyAsync())
            {
                await databaseContext.Acc.AddRangeAsync(new List<Acc>()
                {
                    new Acc
                    {
                        Username = AccountHelper.ADMIN_USERNAME,
                        Password = hasher.HashPassword(null!, "1"),
                        FullName = "Quản trị viên",
                        Email = "admin@gmail.com",
                        Role = AccountRole.Admin,
                        CreatedDate = DateTime.Now,
                        CreatedBy = AccountHelper.SEEDER,
                    }
                });

                await databaseContext.SaveChangesAsync();
            }

            //if (!await databaseContext.Acc.AnyAsync(x => x.Username == AccountHelper.ANONYMOUS_USERNAME))
            //{
            //    await databaseContext.Acc.AddRangeAsync(new List<Acc>()
            //    {
            //        new Acc
            //        {
            //            Username = AccountHelper.ANONYMOUS_USERNAME,
            //            Password = hasher.HashPassword(null!, "4n329087d490-mjru390ur-24u9r-3iu290-"),
            //            FullName = "ANONYMOUS_USERNAME",
            //            Email = "anonymous_sumi@gmail.com",
            //            Role = AccountRole.User,
            //            CreatedDate = DateTime.Now,
            //            CreatedBy = AccountHelper.SEEDER,
            //        }
            //    });

            //    await databaseContext.SaveChangesAsync();
            //}
        }
    }
}
