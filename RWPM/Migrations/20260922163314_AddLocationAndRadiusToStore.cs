using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RWPM.Migrations
{
    public partial class AddLocationAndRadiusToStore : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Store]') AND name = 'AllowedRadiusMeters')
                BEGIN
                    ALTER TABLE [Store] ADD [AllowedRadiusMeters] int NOT NULL DEFAULT 0;
                END

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Store]') AND name = 'Latitude')
                BEGIN
                    ALTER TABLE [Store] ADD [Latitude] float NULL;
                END

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Store]') AND name = 'Longitude')
                BEGIN
                    ALTER TABLE [Store] ADD [Longitude] float NULL;
                END

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Store]') AND name = 'MinAllowedDistanceMeters')
                BEGIN
                    ALTER TABLE [Store] ADD [MinAllowedDistanceMeters] int NOT NULL DEFAULT 0;
                END

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[AttendanceRecord]') AND name = 'CheckInLatitude')
                BEGIN
                    ALTER TABLE [AttendanceRecord] ADD [CheckInLatitude] float NULL;
                END

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[AttendanceRecord]') AND name = 'CheckInLongitude')
                BEGIN
                    ALTER TABLE [AttendanceRecord] ADD [CheckInLongitude] float NULL;
                END

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[AttendanceRecord]') AND name = 'CheckOutLatitude')
                BEGIN
                    ALTER TABLE [AttendanceRecord] ADD [CheckOutLatitude] float NULL;
                END

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[AttendanceRecord]') AND name = 'CheckOutLongitude')
                BEGIN
                    ALTER TABLE [AttendanceRecord] ADD [CheckOutLongitude] float NULL;
                END

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[AttendanceRecord]') AND name = 'DistanceMeters')
                BEGIN
                    ALTER TABLE [AttendanceRecord] ADD [DistanceMeters] float NULL;
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowedRadiusMeters",
                table: "Store");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Store");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Store");

            migrationBuilder.DropColumn(
                name: "MinAllowedDistanceMeters",
                table: "Store");

            migrationBuilder.DropColumn(
                name: "CheckInLatitude",
                table: "AttendanceRecord");

            migrationBuilder.DropColumn(
                name: "CheckInLongitude",
                table: "AttendanceRecord");

            migrationBuilder.DropColumn(
                name: "CheckOutLatitude",
                table: "AttendanceRecord");

            migrationBuilder.DropColumn(
                name: "CheckOutLongitude",
                table: "AttendanceRecord");

            migrationBuilder.DropColumn(
                name: "DistanceMeters",
                table: "AttendanceRecord");
        }
    }
}
