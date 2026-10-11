using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RWPM.Migrations
{
    public partial class AddPayrollModule : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Payroll",
                columns: table => new
                {
                    PayrollId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PeriodMonth = table.Column<int>(type: "int", nullable: false),
                    PeriodYear = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    TotalEmployees = table.Column<int>(type: "int", nullable: false),
                    TotalWorkHours = table.Column<double>(type: "float", nullable: false),
                    TotalGrossSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LockedBy = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    LockedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payroll", x => x.PayrollId);
                });

            migrationBuilder.CreateTable(
                name: "PayrollDetail",
                columns: table => new
                {
                    PayrollDetailId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    EmployeeCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EmployeeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    StoreName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    EmploymentType = table.Column<byte>(type: "tinyint", nullable: false),
                    HourlyRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalStandardHours = table.Column<double>(type: "float", nullable: false),
                    TotalWorkHours = table.Column<double>(type: "float", nullable: false),
                    TotalSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ApprovedLeaveDays = table.Column<int>(type: "int", nullable: false),
                    MissingAttendanceCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    WarningNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollDetail", x => x.PayrollDetailId);
                    table.ForeignKey(
                        name: "FK_PayrollDetail_Employee_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employee",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollDetail_Payroll_PayrollId",
                        column: x => x.PayrollId,
                        principalTable: "Payroll",
                        principalColumn: "PayrollId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PayrollDetail_Store_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Store",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payroll_PeriodMonth_PeriodYear",
                table: "Payroll",
                columns: new[] { "PeriodMonth", "PeriodYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollDetail_EmployeeId",
                table: "PayrollDetail",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollDetail_PayrollId_EmployeeId",
                table: "PayrollDetail",
                columns: new[] { "PayrollId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollDetail_StoreId",
                table: "PayrollDetail",
                column: "StoreId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollDetail");

            migrationBuilder.DropTable(
                name: "Payroll");
        }
    }
}
