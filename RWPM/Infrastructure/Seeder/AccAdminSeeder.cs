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
                        // Mật khẩu khởi tạo yếu -> bắt buộc đổi ở lần đăng nhập đầu tiên
                        MustChangePassword = true,
                        CreatedDate = DateTime.Now,
                        CreatedBy = AccountHelper.SEEDER,
                    }
                });

                await databaseContext.SaveChangesAsync();
            }
        }
    }
}
