using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RWPM.Migrations
{
    public partial class UpgradeShiftEntity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EarlyCheckInMinutes",
                table: "Shift",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GracePeriodMinutes",
                table: "Shift",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTemplate",
                table: "Shift",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "LateThresholdMinutes",
                table: "Shift",
                type: "int",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EarlyCheckInMinutes",
                table: "Shift");

            migrationBuilder.DropColumn(
                name: "GracePeriodMinutes",
                table: "Shift");

            migrationBuilder.DropColumn(
                name: "IsTemplate",
                table: "Shift");

            migrationBuilder.DropColumn(
                name: "LateThresholdMinutes",
                table: "Shift");
        }
    }
}
