using Microsoft.EntityFrameworkCore;
using RWPM.Common.Enums;
using RWPM.Common.Helper;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;

namespace RWPM.Infrastructure.Seeder
{
    class ShiftRegistrationSeeder
    {
        public async Task SeedAsync(DefaultDatabaseContext databaseContext)
        {
            var shifts = await databaseContext.Shift.ToListAsync();
            var shiftSang = shifts.FirstOrDefault(x => x.ShiftCode == "S");
            var shiftToi = shifts.FirstOrDefault(x => x.ShiftCode == "C");

            if (shiftSang == null || shiftToi == null) return;

            var employee = await databaseContext.Employee.FirstOrDefaultAsync();

            // Create a dummy employee if none exists
            if (employee == null)
            {
                var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<Acc>();
                var acc = new Acc
                {
                    Username = "nv01",
                    Password = hasher.HashPassword(null!, "1"),
                    FullName = "Nguyễn Văn A",
                    Role = AccountRole.SalesStaff,
                    CreatedDate = DateTime.Now,
                    CreatedBy = AccountHelper.SEEDER
                };
                if (!await databaseContext.Acc.AnyAsync(a => a.Username == "nv01"))
                {
                    await databaseContext.Acc.AddAsync(acc);
                    await databaseContext.SaveChangesAsync();
                }

                var store = await databaseContext.Store.FirstOrDefaultAsync();
                if (store == null)
                {
                    store = new Store
                    {
                        StoreCode = "CH01",
                        StoreName = "Cửa hàng mẫu",
                        Address = "123 Đường ABC",
                        IsActive = true,
                        CreatedDate = DateTime.Now,
                        CreatedBy = AccountHelper.SEEDER
                    };
                    await databaseContext.Store.AddAsync(store);
                    await databaseContext.SaveChangesAsync();
                }

                employee = new Employee
                {
                    EmployeeCode = "NV01",
                    Username = "nv01",
                    StoreId = store.StoreId,
                    IsActive = true,
                    CreatedDate = DateTime.Now,
                    CreatedBy = AccountHelper.SEEDER
                };
                await databaseContext.Employee.AddAsync(employee);
                await databaseContext.SaveChangesAsync();
            }

            var date13 = new DateTime(2026, 9, 13);
            var date14 = new DateTime(2026, 9, 14);

            var existing13 = await databaseContext.ShiftRegistration.AnyAsync(x => x.EmployeeId == employee.EmployeeId && x.WorkDate == date13 && x.ShiftId == shiftToi.ShiftId);
            if (!existing13)
            {
                await databaseContext.ShiftRegistration.AddAsync(new ShiftRegistration
                {
                    EmployeeId = employee.EmployeeId,
                    ShiftId = shiftToi.ShiftId,
                    StoreId = employee.StoreId,
                    WorkDate = date13,
                    Status = RegistrationStatus.Approved,
                    Note = "Ca tối ngày 13",
                    CreatedDate = DateTime.Now,
                    CreatedBy = AccountHelper.SEEDER
                });
            }

            var existing14 = await databaseContext.ShiftRegistration.AnyAsync(x => x.EmployeeId == employee.EmployeeId && x.WorkDate == date14 && x.ShiftId == shiftSang.ShiftId);
            if (!existing14)
            {
                await databaseContext.ShiftRegistration.AddAsync(new ShiftRegistration
                {
                    EmployeeId = employee.EmployeeId,
                    ShiftId = shiftSang.ShiftId,
                    StoreId = employee.StoreId,
                    WorkDate = date14,
                    Status = RegistrationStatus.Pending,
                    Note = "Ca sáng ngày 14",
                    CreatedDate = DateTime.Now,
                    CreatedBy = AccountHelper.SEEDER
                });
            }

            await databaseContext.SaveChangesAsync();
        }
    }
}
