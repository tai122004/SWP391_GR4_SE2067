using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RWPM.Migrations
{
    public partial class AddPayrollSnapshotAndAudit : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PayrollDetail_PayrollId_EmployeeId",
                table: "PayrollDetail");

            migrationBuilder.AddColumn<double>(
                name: "PaidLeaveHours",
                table: "PayrollDetail",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "UnpaidLeaveDays",
                table: "PayrollDetail",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "PayrollAuditLog",
                columns: table => new
                {
                    PayrollAuditLogId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FromStatus = table.Column<byte>(type: "tinyint", nullable: true),
                    ToStatus = table.Column<byte>(type: "tinyint", nullable: true),
                    OperatorUsername = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollAuditLog", x => x.PayrollAuditLogId);
                    table.ForeignKey(
                        name: "FK_PayrollAuditLog_Payroll_PayrollId",
                        column: x => x.PayrollId,
                        principalTable: "Payroll",
                        principalColumn: "PayrollId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PayrollItemSnapshot",
                columns: table => new
                {
                    PayrollItemSnapshotId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollDetailId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    ShiftId = table.Column<int>(type: "int", nullable: true),
                    ShiftName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    CheckInTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    CheckOutTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    ActualWorkHours = table.Column<double>(type: "float", nullable: false),
                    PaidLeaveHours = table.Column<double>(type: "float", nullable: false),
                    TotalPayableHours = table.Column<double>(type: "float", nullable: false),
                    HourlyRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollItemSnapshot", x => x.PayrollItemSnapshotId);
                    table.ForeignKey(
                        name: "FK_PayrollItemSnapshot_PayrollDetail_PayrollDetailId",
                        column: x => x.PayrollDetailId,
                        principalTable: "PayrollDetail",
                        principalColumn: "PayrollDetailId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PayrollItemSnapshot_Shift_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shift",
                        principalColumn: "ShiftId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollDetail_PayrollId_EmployeeId",
                table: "PayrollDetail",
                columns: new[] { "PayrollId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollAuditLog_PayrollId",
                table: "PayrollAuditLog",
                column: "PayrollId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollItemSnapshot_PayrollDetailId",
                table: "PayrollItemSnapshot",
                column: "PayrollDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollItemSnapshot_ShiftId",
                table: "PayrollItemSnapshot",
                column: "ShiftId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollAuditLog");

            migrationBuilder.DropTable(
                name: "PayrollItemSnapshot");

            migrationBuilder.DropIndex(
                name: "IX_PayrollDetail_PayrollId_EmployeeId",
                table: "PayrollDetail");

            migrationBuilder.DropColumn(
                name: "PaidLeaveHours",
                table: "PayrollDetail");

            migrationBuilder.DropColumn(
                name: "UnpaidLeaveDays",
                table: "PayrollDetail");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollDetail_PayrollId_EmployeeId",
                table: "PayrollDetail",
                columns: new[] { "PayrollId", "EmployeeId" });
        }
    }
}
