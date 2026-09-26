using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RWPM.Migrations
{
    public partial class AddStoreExtendedTancaFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "ClosingTime",
                table: "Store",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ManagerId",
                table: "Store",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "OpeningTime",
                table: "Store",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QrCodeKey",
                table: "Store",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WifiBSSID",
                table: "Store",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Store_ManagerId",
                table: "Store",
                column: "ManagerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Store_Employee_ManagerId",
                table: "Store",
                column: "ManagerId",
                principalTable: "Employee",
                principalColumn: "EmployeeId",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Store_Employee_ManagerId",
                table: "Store");

            migrationBuilder.DropIndex(
                name: "IX_Store_ManagerId",
                table: "Store");

            migrationBuilder.DropColumn(
                name: "ClosingTime",
                table: "Store");

            migrationBuilder.DropColumn(
                name: "ManagerId",
                table: "Store");

            migrationBuilder.DropColumn(
                name: "OpeningTime",
                table: "Store");

            migrationBuilder.DropColumn(
                name: "QrCodeKey",
                table: "Store");

            migrationBuilder.DropColumn(
                name: "WifiBSSID",
                table: "Store");
        }
    }
}
