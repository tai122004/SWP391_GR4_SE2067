using Microsoft.EntityFrameworkCore;
using RWPM.Common.Enums;
using RWPM.Common.Helper;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;

namespace RWPM.Infrastructure.Seeder
{
    class ShiftSeeder
    {
        public async Task SeedAsync(DefaultDatabaseContext databaseContext)
        {
            if (!await databaseContext.Shift.AnyAsync())
            {
                await databaseContext.Shift.AddRangeAsync(new List<Shift>()
                {
                    new Shift
                    {
                        ShiftCode = "S",
                        ShiftName = "Ca Sáng",
                        Type = ShiftType.Morning,
                        StartTime = new TimeSpan(8, 30, 0),
                        EndTime = new TimeSpan(16, 0, 0),
                        BreakMinutes = 30,
                        Description = "Ca sáng: 08:30 - 16:00",
                        IsActive = true,
                        CreatedDate = DateTime.Now,
                        CreatedBy = AccountHelper.SEEDER,
                    },
                    new Shift
                    {
                        ShiftCode = "C",
                        ShiftName = "Ca Chiều Tối",
                        Type = ShiftType.Afternoon,
                        StartTime = new TimeSpan(15, 0, 0),
                        EndTime = new TimeSpan(22, 30, 0),
                        BreakMinutes = 30,
                        Description = "Ca chiều tối: 15:00 - 22:30",
                        IsActive = true,
                        CreatedDate = DateTime.Now,
                        CreatedBy = AccountHelper.SEEDER,
                    },
                    new Shift
                    {
                        ShiftCode = "CDT",
                        ShiftName = "Ca Cao Điểm Trưa",
                        Type = ShiftType.Peak,
                        StartTime = new TimeSpan(10, 0, 0),
                        EndTime = new TimeSpan(14, 0, 0),
                        BreakMinutes = 0,
                        Description = "Ca cao điểm trưa (Part-time): 10:00 - 14:00",
                        IsActive = true,
                        CreatedDate = DateTime.Now,
                        CreatedBy = AccountHelper.SEEDER,
                    },
                    new Shift
                    {
                        ShiftCode = "CDTOI",
                        ShiftName = "Ca Cao Điểm Tối",
                        Type = ShiftType.Peak,
                        StartTime = new TimeSpan(18, 0, 0),
                        EndTime = new TimeSpan(22, 0, 0),
                        BreakMinutes = 0,
                        Description = "Ca cao điểm tối (Part-time): 18:00 - 22:00",
                        IsActive = true,
                        CreatedDate = DateTime.Now,
                        CreatedBy = AccountHelper.SEEDER,
                    },
                    new Shift
                    {
                        ShiftCode = "HC",
                        ShiftName = "Ca Hành Chính",
                        Type = ShiftType.FullDay,
                        StartTime = new TimeSpan(8, 30, 0),
                        EndTime = new TimeSpan(17, 30, 0),
                        BreakMinutes = 60,
                        Description = "Ca hành chính full-time: 08:30 - 17:30",
                        IsActive = true,
                        CreatedDate = DateTime.Now,
                        CreatedBy = AccountHelper.SEEDER,
                    }
                });

                await databaseContext.SaveChangesAsync();
            }
        }
    }
}
