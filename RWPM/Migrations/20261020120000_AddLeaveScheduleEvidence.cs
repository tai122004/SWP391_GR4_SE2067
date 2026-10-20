using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RWPM.Infrastructure.Data;

namespace RWPM.Migrations;

[DbContext(typeof(DefaultDatabaseContext))]
[Migration("20261020120000_AddLeaveScheduleEvidence")]
public partial class AddLeaveScheduleEvidence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Stop with an actionable message instead of deleting/merging historical accounts.
        migrationBuilder.Sql(@"
IF EXISTS (SELECT LOWER(LTRIM(RTRIM([Email]))) FROM [Acc]
    WHERE LTRIM(RTRIM([Email])) <> N'' GROUP BY LOWER(LTRIM(RTRIM([Email]))) HAVING COUNT(*) > 1)
    THROW 50001, N'Email hiện có bị trùng. Rà soát tài khoản trước khi cập nhật ràng buộc; không có dữ liệu nào được tự xóa.', 1;
IF EXISTS (SELECT LTRIM(RTRIM([PhoneNumber])) FROM [Acc]
    WHERE [PhoneNumber] IS NOT NULL AND LTRIM(RTRIM([PhoneNumber])) <> N''
    GROUP BY LTRIM(RTRIM([PhoneNumber])) HAVING COUNT(*) > 1)
    THROW 50002, N'Số điện thoại hiện có bị trùng. Rà soát tài khoản trước khi cập nhật ràng buộc; không có dữ liệu nào được tự xóa.', 1;");
        migrationBuilder.AddColumn<string>(name: "ScheduleSnapshotJson", table: "LeaveRequest", type: "nvarchar(max)", nullable: true);
        migrationBuilder.DropIndex(name: "IX_Acc_Email", table: "Acc");
        migrationBuilder.CreateIndex(name: "IX_Acc_Email", table: "Acc", column: "Email", unique: true, filter: "[Email] <> N''");
        migrationBuilder.CreateIndex(name: "IX_Acc_PhoneNumber", table: "Acc", column: "PhoneNumber", unique: true, filter: "[PhoneNumber] IS NOT NULL AND [PhoneNumber] <> N''");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Acc_Email", table: "Acc");
        migrationBuilder.DropIndex(name: "IX_Acc_PhoneNumber", table: "Acc");
        migrationBuilder.CreateIndex(name: "IX_Acc_Email", table: "Acc", column: "Email");
        migrationBuilder.DropColumn(name: "ScheduleSnapshotJson", table: "LeaveRequest");
    }
}
