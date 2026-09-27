using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RWPM.Migrations
{
    public partial class AddAttendanceAdjustment : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ShiftCode",
                table: "Shift",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<bool>(
                name: "IsAdjusted",
                table: "AttendanceRecord",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AttendanceAdjustmentHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AttendanceRecordId = table.Column<int>(type: "int", nullable: false),
                    OldCheckInTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    OldCheckOutTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    NewCheckInTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    NewCheckOutTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceAdjustmentHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceAdjustmentHistory_AttendanceRecord_AttendanceRecordId",
                        column: x => x.AttendanceRecordId,
                        principalTable: "AttendanceRecord",
                        principalColumn: "AttendanceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceAdjustmentHistory_AttendanceRecordId",
                table: "AttendanceAdjustmentHistory",
                column: "AttendanceRecordId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceAdjustmentHistory");

            migrationBuilder.DropColumn(
                name: "IsAdjusted",
                table: "AttendanceRecord");

            migrationBuilder.AlterColumn<string>(
                name: "ShiftCode",
                table: "Shift",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);
        }
    }
}
