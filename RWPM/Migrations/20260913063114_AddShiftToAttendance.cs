using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RWPM.Migrations
{
    public partial class AddShiftToAttendance : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ShiftId",
                table: "AttendanceRecord",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecord_ShiftId",
                table: "AttendanceRecord",
                column: "ShiftId");

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceRecord_Shift_ShiftId",
                table: "AttendanceRecord",
                column: "ShiftId",
                principalTable: "Shift",
                principalColumn: "ShiftId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceRecord_Shift_ShiftId",
                table: "AttendanceRecord");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecord_ShiftId",
                table: "AttendanceRecord");

            migrationBuilder.DropColumn(
                name: "ShiftId",
                table: "AttendanceRecord");
        }
    }
}
