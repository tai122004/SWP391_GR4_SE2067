using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RWPM.Migrations
{
    public partial class Add_StoreId_To_Shift : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StoreId",
                table: "Shift",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shift_StoreId",
                table: "Shift",
                column: "StoreId");

            migrationBuilder.AddForeignKey(
                name: "FK_Shift_Store_StoreId",
                table: "Shift",
                column: "StoreId",
                principalTable: "Store",
                principalColumn: "StoreId",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Shift_Store_StoreId",
                table: "Shift");

            migrationBuilder.DropIndex(
                name: "IX_Shift_StoreId",
                table: "Shift");

            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "Shift");
        }
    }
}
