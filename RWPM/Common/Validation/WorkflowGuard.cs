using Microsoft.EntityFrameworkCore;
using RWPM.Common.Enums;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;

namespace RWPM.Common.Validation;

public static class WorkflowGuard
{
    public static async Task<Acc> ActorAsync(DefaultDatabaseContext db, string username)
    {
        var actor = await db.Acc.AsNoTracking().SingleOrDefaultAsync(x => x.Username == username && x.IsActive);
        return actor ?? throw new UnauthorizedAccessException(InputRules.Text("Tài khoản không có quyền thực hiện.", "Account is not authorized."));
    }
    public static async Task EmployeeAsync(DefaultDatabaseContext db, string username, int employeeId, bool approval = false)
    {
        var actor = await ActorAsync(db, username);
        var target = await db.Employee.AsNoTracking().Include(x => x.Account).Include(x => x.Store)
            .SingleOrDefaultAsync(x => x.EmployeeId == employeeId);
        InputRules.Require(target != null, "EmployeeId", "Không tìm thấy nhân viên.", "Employee not found.");
        if (!approval && target!.Username == username) return;
        if (actor.Role is AccountRole.Admin or AccountRole.HR) return;
        if (actor.Role == AccountRole.StoreManager)
        {
            var store = await db.Employee.AsNoTracking().Where(x => x.Username == username && x.IsActive && x.Store != null && x.Store.IsActive)
                .Select(x => x.StoreId).SingleOrDefaultAsync();
            if (store.HasValue && store == target!.StoreId) return;
        }
        throw new UnauthorizedAccessException(InputRules.Text("Bạn không có quyền xử lý nhân viên/chi nhánh này.", "You cannot access this employee/store."));
    }
    public static async Task UnlockedPeriodAsync(DefaultDatabaseContext db, DateTime start, DateTime end)
    {
        InputRules.Require(!await db.Payroll.AnyAsync(x => x.Status == PayrollStatus.Locked && x.StartDate <= end && x.EndDate >= start),
            "WorkDate", "Khoảng ngày thuộc kỳ lương đã chốt. Cần mở khóa kỳ lương trước khi thay đổi lịch/nghỉ phép.",
            "These dates belong to a locked payroll. Unlock it before changing schedules or leave.");
    }
}
