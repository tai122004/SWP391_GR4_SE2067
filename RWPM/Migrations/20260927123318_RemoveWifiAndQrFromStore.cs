using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RWPM.Migrations
{
    public partial class RemoveWifiAndQrFromStore : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QrCodeKey",
                table: "Store");

            migrationBuilder.DropColumn(
                name: "WifiBSSID",
                table: "Store");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}
