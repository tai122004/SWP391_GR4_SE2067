using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RWPM.Migrations
{
    public partial class RequireExplicitShiftTolerances : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Freeze the currently inherited policy; preserve explicit overrides, including zero.
            migrationBuilder.Sql("UPDATE dbo.Shift SET GracePeriodMinutes=15 WHERE GracePeriodMinutes IS NULL; UPDATE dbo.Shift SET EarlyCheckOutMinutes=15 WHERE EarlyCheckOutMinutes IS NULL;");
            migrationBuilder.AlterColumn<int>(
                name: "GracePeriodMinutes",
                table: "Shift",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "EarlyCheckOutMinutes",
                table: "Shift",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "GracePeriodMinutes",
                table: "Shift",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "EarlyCheckOutMinutes",
                table: "Shift",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}
