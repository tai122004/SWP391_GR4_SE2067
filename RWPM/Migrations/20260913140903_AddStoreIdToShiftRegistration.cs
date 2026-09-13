using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RWPM.Migrations
{
    public partial class AddStoreIdToShiftRegistration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM ShiftRegistration");

            migrationBuilder.AddColumn<int>(
                name: "StoreId",
                table: "ShiftRegistration",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftRegistration_StoreId",
                table: "ShiftRegistration",
                column: "StoreId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftRegistration_Store_StoreId",
                table: "ShiftRegistration",
                column: "StoreId",
                principalTable: "Store",
                principalColumn: "StoreId",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShiftRegistration_Store_StoreId",
                table: "ShiftRegistration");

            migrationBuilder.DropIndex(
                name: "IX_ShiftRegistration_StoreId",
                table: "ShiftRegistration");

            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "ShiftRegistration");
        }
    }
}
