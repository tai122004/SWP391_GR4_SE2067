using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RWPM.Migrations
{
    public partial class AddTancaEmployeeAndAccountFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AnnualLeaveBalance",
                table: "Employee",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseSalary",
                table: "Employee",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CitizenId",
                table: "Employee",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "EmploymentType",
                table: "Employee",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<decimal>(
                name: "HourlyRate",
                table: "Employee",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Position",
                table: "Employee",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResignDate",
                table: "Employee",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResignReason",
                table: "Employee",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Status",
                table: "Employee",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "Acc",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth",
                table: "Acc",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Gender",
                table: "Acc",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "Acc",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnnualLeaveBalance",
                table: "Employee");

            migrationBuilder.DropColumn(
                name: "BaseSalary",
                table: "Employee");

            migrationBuilder.DropColumn(
                name: "CitizenId",
                table: "Employee");

            migrationBuilder.DropColumn(
                name: "EmploymentType",
                table: "Employee");

            migrationBuilder.DropColumn(
                name: "HourlyRate",
                table: "Employee");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "Employee");

            migrationBuilder.DropColumn(
                name: "ResignDate",
                table: "Employee");

            migrationBuilder.DropColumn(
                name: "ResignReason",
                table: "Employee");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Employee");

            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "Acc");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "Acc");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Acc");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "Acc");
        }
    }
}
