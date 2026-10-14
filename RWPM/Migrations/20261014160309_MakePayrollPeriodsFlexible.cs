using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RWPM.Migrations
{
    public partial class MakePayrollPeriodsFlexible : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payroll_PeriodMonth_PeriodYear",
                table: "Payroll");

            migrationBuilder.CreateIndex(
                name: "IX_Payroll_StartDate_EndDate",
                table: "Payroll",
                columns: new[] { "StartDate", "EndDate" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payroll_StartDate_EndDate",
                table: "Payroll");

            migrationBuilder.CreateIndex(
                name: "IX_Payroll_PeriodMonth_PeriodYear",
                table: "Payroll",
                columns: new[] { "PeriodMonth", "PeriodYear" },
                unique: true);
        }
    }
}
