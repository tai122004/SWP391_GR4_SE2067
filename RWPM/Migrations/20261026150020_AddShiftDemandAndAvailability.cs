using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RWPM.Migrations
{
    public partial class AddShiftDemandAndAvailability : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeAvailability",
                columns: table => new
                {
                    EmployeeAvailabilityId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeAvailability", x => x.EmployeeAvailabilityId);
                    table.CheckConstraint("CK_EmployeeAvailability_DayOfWeek", "[DayOfWeek] BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_EmployeeAvailability_Times", "[StartTime] <> [EndTime]");
                    table.ForeignKey(
                        name: "FK_EmployeeAvailability_Employee_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employee",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeUnavailability",
                columns: table => new
                {
                    EmployeeUnavailabilityId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeUnavailability", x => x.EmployeeUnavailabilityId);
                    table.ForeignKey(
                        name: "FK_EmployeeUnavailability_Employee_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employee",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShiftDemand",
                columns: table => new
                {
                    ShiftDemandId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    ShiftId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: true),
                    Date = table.Column<DateTime>(type: "date", nullable: true),
                    RequiredCount = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftDemand", x => x.ShiftDemandId);
                    table.CheckConstraint("CK_ShiftDemand_DayOfWeek", "[DayOfWeek] IS NULL OR [DayOfWeek] BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_ShiftDemand_Kind", "([DayOfWeek] IS NULL AND [Date] IS NOT NULL) OR ([DayOfWeek] IS NOT NULL AND [Date] IS NULL)");
                    table.CheckConstraint("CK_ShiftDemand_RequiredCount", "[RequiredCount] BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_ShiftDemand_Shift_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shift",
                        principalColumn: "ShiftId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShiftDemand_Store_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Store",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAvailability_EmployeeId_DayOfWeek_Status",
                table: "EmployeeAvailability",
                columns: new[] { "EmployeeId", "DayOfWeek", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeUnavailability_EmployeeId_Date",
                table: "EmployeeUnavailability",
                columns: new[] { "EmployeeId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftDemand_ShiftId",
                table: "ShiftDemand",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftDemand_StoreId_ShiftId_Date",
                table: "ShiftDemand",
                columns: new[] { "StoreId", "ShiftId", "Date" },
                unique: true,
                filter: "[Date] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftDemand_StoreId_ShiftId_DayOfWeek",
                table: "ShiftDemand",
                columns: new[] { "StoreId", "ShiftId", "DayOfWeek" },
                unique: true,
                filter: "[DayOfWeek] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeAvailability");

            migrationBuilder.DropTable(
                name: "EmployeeUnavailability");

            migrationBuilder.DropTable(
                name: "ShiftDemand");
        }
    }
}
