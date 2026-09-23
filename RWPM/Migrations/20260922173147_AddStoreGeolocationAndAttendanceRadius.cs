using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RWPM.Migrations
{
    public partial class AddStoreGeolocationAndAttendanceRadius : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AllowedRadiusMeters",
                table: "Store",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Store",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Store",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinAllowedDistanceMeters",
                table: "Store",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "CheckInLatitude",
                table: "AttendanceRecord",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckInLongitude",
                table: "AttendanceRecord",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckOutLatitude",
                table: "AttendanceRecord",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckOutLongitude",
                table: "AttendanceRecord",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DistanceMeters",
                table: "AttendanceRecord",
                type: "float",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowedRadiusMeters",
                table: "Store");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Store");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Store");

            migrationBuilder.DropColumn(
                name: "MinAllowedDistanceMeters",
                table: "Store");

            migrationBuilder.DropColumn(
                name: "CheckInLatitude",
                table: "AttendanceRecord");

            migrationBuilder.DropColumn(
                name: "CheckInLongitude",
                table: "AttendanceRecord");

            migrationBuilder.DropColumn(
                name: "CheckOutLatitude",
                table: "AttendanceRecord");

            migrationBuilder.DropColumn(
                name: "CheckOutLongitude",
                table: "AttendanceRecord");

            migrationBuilder.DropColumn(
                name: "DistanceMeters",
                table: "AttendanceRecord");
        }
    }
}
