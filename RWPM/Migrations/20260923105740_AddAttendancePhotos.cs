using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RWPM.Migrations
{
    public partial class AddAttendancePhotos : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CheckInPhotoPath",
                table: "AttendanceRecord",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckOutPhotoPath",
                table: "AttendanceRecord",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheckInPhotoPath",
                table: "AttendanceRecord");

            migrationBuilder.DropColumn(
                name: "CheckOutPhotoPath",
                table: "AttendanceRecord");
        }
    }
}
