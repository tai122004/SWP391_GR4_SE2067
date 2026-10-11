using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RWPM.Migrations
{
    public partial class ExtendShiftTemplates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowOutsideStoreHours",
                table: "Shift",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "DefaultMaximumHeadcount",
                table: "Shift",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultRequiredHeadcount",
                table: "Shift",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveFrom",
                table: "Shift",
                type: "date",
                nullable: false,
                defaultValue: new DateTime(2000, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveTo",
                table: "Shift",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBreakPaid",
                table: "Shift",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OutsideHoursReason",
                table: "Shift",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowOutsideStoreHours",
                table: "Shift");

            migrationBuilder.DropColumn(
                name: "DefaultMaximumHeadcount",
                table: "Shift");

            migrationBuilder.DropColumn(
                name: "DefaultRequiredHeadcount",
                table: "Shift");

            migrationBuilder.DropColumn(
                name: "EffectiveFrom",
                table: "Shift");

            migrationBuilder.DropColumn(
                name: "EffectiveTo",
                table: "Shift");

            migrationBuilder.DropColumn(
                name: "IsBreakPaid",
                table: "Shift");

            migrationBuilder.DropColumn(
                name: "OutsideHoursReason",
                table: "Shift");
        }
    }
}
