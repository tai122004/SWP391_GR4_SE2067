using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RWPM.Common.Enums;
using RWPM.Common.Helper;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;

namespace RWPM.Infrastructure.Seeder;

/// <summary>
/// Creates isolated demo data for exercising payroll calculations in Development.
/// </summary>
internal sealed class PayrollAttendanceDemoSeeder
{
    private const string DemoUsername = "payroll.demo";
    private const string DemoEmployeeCode = "DEMO-PAYROLL-01";
    private const string DemoPassword = "Demo@12345";
    private const int WorkdayCount = 10;

    public async Task SeedAsync(DefaultDatabaseContext context)
    {
        var today = DateTime.Today;
        // Seed the current payroll period so it is immediately visible when
        // the payroll screen is opened for the current month.
        var periodStart = new DateTime(today.Year, today.Month, 1);
        var periodEnd = today;

        var storeShift = await context.StoreShift
            .Include(link => link.Store)
            .Include(link => link.Shift)
            .Where(link => link.IsActive && link.Store.IsActive && link.Shift.IsActive
                && link.Shift.EffectiveFrom <= periodEnd
                && (!link.Shift.EffectiveTo.HasValue || link.Shift.EffectiveTo.Value >= periodStart))
            .OrderBy(link => link.StoreId)
            .ThenBy(link => link.Shift.StartTime)
            .FirstOrDefaultAsync();

        if (storeShift == null)
        {
            // There is no active store/shift combination to attach valid attendance to.
            return;
        }

        var account = await context.Acc.FirstOrDefaultAsync(x => x.Username == DemoUsername);
        if (account == null)
        {
            var passwordHasher = new PasswordHasher<Acc>();
            account = new Acc
            {
                Username = DemoUsername,
                Password = passwordHasher.HashPassword(null!, DemoPassword),
                FullName = "Demo Payroll Employee",
                Email = "payroll.demo@rwpm.local",
                Role = AccountRole.Employee,
                IsActive = true,
                CreatedDate = DateTime.Now,
                CreatedBy = AccountHelper.SEEDER
            };
            context.Acc.Add(account);
            await context.SaveChangesAsync();
        }

        var employee = await context.Employee.FirstOrDefaultAsync(x => x.Username == DemoUsername);
        if (employee == null)
        {
            employee = new Employee
            {
                EmployeeCode = DemoEmployeeCode,
                Username = DemoUsername,
                StoreId = storeShift.StoreId,
                JoinDate = periodStart.AddYears(-1),
                EmploymentType = EmploymentType.FullTime,
                Status = EmploymentStatus.Official,
                HourlyRate = 50_000m,
                AnnualLeaveBalance = 12,
                IsActive = true,
                CreatedDate = DateTime.Now,
                CreatedBy = AccountHelper.SEEDER
            };
            context.Employee.Add(employee);
            await context.SaveChangesAsync();
        }

        var existingDates = await context.AttendanceRecord
            .Where(record => record.Username == DemoUsername
                && record.Date >= periodStart && record.Date <= periodEnd)
            .Select(record => record.Date)
            .ToListAsync();
        var existingRegistrationDates = await context.ShiftRegistration
            .Where(registration => registration.EmployeeId == employee.EmployeeId
                && registration.WorkDate >= periodStart && registration.WorkDate <= periodEnd)
            .Select(registration => registration.WorkDate)
            .ToListAsync();

        var attendanceDates = existingDates.Select(date => date.Date).ToHashSet();
        var registrationDates = existingRegistrationDates.Select(date => date.Date).ToHashSet();
        var insertedCount = 0;

        for (var date = periodStart; date <= periodEnd && insertedCount < WorkdayCount; date = date.AddDays(1))
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
                || attendanceDates.Contains(date.Date)
                || registrationDates.Contains(date.Date)
                || !storeShift.Shift.IsAvailableOn(date))
            {
                continue;
            }

            context.ShiftRegistration.Add(new ShiftRegistration
            {
                EmployeeId = employee.EmployeeId,
                ShiftId = storeShift.ShiftId,
                StoreId = storeShift.StoreId,
                WorkDate = date,
                Status = RegistrationStatus.Approved,
                Note = "Demo data for payroll testing",
                ApprovedBy = AccountHelper.ADMIN_USERNAME,
                ApprovedAt = date.AddDays(-1).AddHours(12),
                CreatedDate = date.AddDays(-2),
                CreatedBy = AccountHelper.SEEDER
            });

            var checkIn = storeShift.Shift.StartTime.Add(TimeSpan.FromMinutes(5));
            var checkOut = storeShift.Shift.EndTime.Subtract(TimeSpan.FromMinutes(5));
            if (checkOut < TimeSpan.Zero)
            {
                checkOut = TimeSpan.Zero;
            }

            context.AttendanceRecord.Add(new AttendanceRecord
            {
                Username = DemoUsername,
                Date = date,
                ShiftId = storeShift.ShiftId,
                CheckInTime = checkIn,
                CheckOutTime = checkOut,
                IsAdjusted = false
            });

            insertedCount++;
        }

        await context.SaveChangesAsync();

        // Also seed existing employees requested for payroll testing.
        foreach (var employeeCode in new[] { "NV2610142217", "NV2610142152" })
        {
            var existingEmployee = await context.Employee
                .FirstOrDefaultAsync(x => x.EmployeeCode == employeeCode && x.IsActive);
            if (existingEmployee != null)
            {
                await SeedAttendanceForEmployeeAsync(context, existingEmployee, storeShift.ShiftId,
                    existingEmployee.StoreId ?? storeShift.StoreId, periodStart, periodEnd);
            }
        }
    }

    private static async Task SeedAttendanceForEmployeeAsync(
        DefaultDatabaseContext context,
        Employee employee,
        int defaultShiftId,
        int storeId,
        DateTime periodStart,
        DateTime periodEnd)
    {
        var shift = await context.Shift.FirstOrDefaultAsync(x => x.ShiftId == defaultShiftId && x.IsActive);
        if (shift == null || employee.StoreId == null)
        {
            return;
        }

        var attendanceDates = (await context.AttendanceRecord
            .Where(x => x.Username == employee.Username && x.Date >= periodStart && x.Date <= periodEnd)
            .Select(x => x.Date)
            .ToListAsync()).Select(x => x.Date).ToHashSet();
        var registrationDates = (await context.ShiftRegistration
            .Where(x => x.EmployeeId == employee.EmployeeId && x.WorkDate >= periodStart && x.WorkDate <= periodEnd)
            .Select(x => x.WorkDate)
            .ToListAsync()).Select(x => x.Date).ToHashSet();

        var created = 0;
        for (var date = periodStart; date <= periodEnd && created < WorkdayCount; date = date.AddDays(1))
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
                || attendanceDates.Contains(date.Date)
                || registrationDates.Contains(date.Date)
                || !shift.IsAvailableOn(date))
            {
                continue;
            }

            context.ShiftRegistration.Add(new ShiftRegistration
            {
                EmployeeId = employee.EmployeeId,
                ShiftId = shift.ShiftId,
                StoreId = storeId,
                WorkDate = date,
                Status = RegistrationStatus.Approved,
                Note = "Demo data for payroll testing",
                ApprovedBy = AccountHelper.ADMIN_USERNAME,
                ApprovedAt = date.AddDays(-1).AddHours(12),
                CreatedDate = date.AddDays(-2),
                CreatedBy = AccountHelper.SEEDER
            });

            context.AttendanceRecord.Add(new AttendanceRecord
            {
                Username = employee.Username,
                Date = date,
                ShiftId = shift.ShiftId,
                CheckInTime = shift.StartTime.Add(TimeSpan.FromMinutes(5)),
                CheckOutTime = shift.EndTime.Subtract(TimeSpan.FromMinutes(5)),
                IsAdjusted = false
            });
            created++;
        }

        await context.SaveChangesAsync();
    }
}
