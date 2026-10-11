using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RWPM.Migrations
{
    public partial class UpdateDatabase : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "ShiftRegistration",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                table: "ShiftRegistration",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "ShiftRegistration",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ShiftSwapRequest",
                columns: table => new
                {
                    ShiftSwapRequestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequesterEmployeeId = table.Column<int>(type: "int", nullable: false),
                    SourceRegistrationId = table.Column<int>(type: "int", nullable: false),
                    TargetEmployeeId = table.Column<int>(type: "int", nullable: true),
                    TargetRegistrationId = table.Column<int>(type: "int", nullable: true),
                    TargetShiftId = table.Column<int>(type: "int", nullable: true),
                    TargetWorkDate = table.Column<DateTime>(type: "date", nullable: true),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
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
                    table.PrimaryKey("PK_ShiftSwapRequest", x => x.ShiftSwapRequestId);
                    table.ForeignKey(
                        name: "FK_ShiftSwapRequest_Employee_RequesterEmployeeId",
                        column: x => x.RequesterEmployeeId,
                        principalTable: "Employee",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftSwapRequest_Employee_TargetEmployeeId",
                        column: x => x.TargetEmployeeId,
                        principalTable: "Employee",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftSwapRequest_Shift_TargetShiftId",
                        column: x => x.TargetShiftId,
                        principalTable: "Shift",
                        principalColumn: "ShiftId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftSwapRequest_ShiftRegistration_SourceRegistrationId",
                        column: x => x.SourceRegistrationId,
                        principalTable: "ShiftRegistration",
                        principalColumn: "ShiftRegistrationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftSwapRequest_ShiftRegistration_TargetRegistrationId",
                        column: x => x.TargetRegistrationId,
                        principalTable: "ShiftRegistration",
                        principalColumn: "ShiftRegistrationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftSwapRequest_Store_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Store",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSwapRequest_RequesterEmployeeId",
                table: "ShiftSwapRequest",
                column: "RequesterEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSwapRequest_SourceRegistrationId",
                table: "ShiftSwapRequest",
                column: "SourceRegistrationId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSwapRequest_StoreId",
                table: "ShiftSwapRequest",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSwapRequest_TargetEmployeeId",
                table: "ShiftSwapRequest",
                column: "TargetEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSwapRequest_TargetRegistrationId",
                table: "ShiftSwapRequest",
                column: "TargetRegistrationId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSwapRequest_TargetShiftId",
                table: "ShiftSwapRequest",
                column: "TargetShiftId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShiftSwapRequest");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "ShiftRegistration");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "ShiftRegistration");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "ShiftRegistration");
        }
    }
}
