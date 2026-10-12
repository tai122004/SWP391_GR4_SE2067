using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RWPM.Migrations
{
    public partial class AddAttendanceAdjustmentRequest : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceAdjustmentRequest",
                columns: table => new
                {
                    AttendanceAdjustmentRequestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AttendanceRecordId = table.Column<int>(type: "int", nullable: true),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    WorkDate = table.Column<DateTime>(type: "date", nullable: false),
                    ShiftId = table.Column<int>(type: "int", nullable: true),
                    CurrentCheckInTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    CurrentCheckOutTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    RequestedCheckInTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    RequestedCheckOutTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RejectionReason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceAdjustmentRequest", x => x.AttendanceAdjustmentRequestId);
                    table.ForeignKey(
                        name: "FK_AttendanceAdjustmentRequest_AttendanceRecord_AttendanceRecordId",
                        column: x => x.AttendanceRecordId,
                        principalTable: "AttendanceRecord",
                        principalColumn: "AttendanceId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AttendanceAdjustmentRequest_Employee_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employee",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceAdjustmentRequest_Shift_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shift",
                        principalColumn: "ShiftId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceAdjustmentRequest_Store_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Store",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceAdjustmentRequest_AttendanceRecordId",
                table: "AttendanceAdjustmentRequest",
                column: "AttendanceRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceAdjustmentRequest_EmployeeId",
                table: "AttendanceAdjustmentRequest",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceAdjustmentRequest_ShiftId",
                table: "AttendanceAdjustmentRequest",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceAdjustmentRequest_StoreId",
                table: "AttendanceAdjustmentRequest",
                column: "StoreId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceAdjustmentRequest");
        }
    }
}
