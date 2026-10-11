using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace RWPM.Migrations;

public partial class ReplaceShiftWithStoreAssignments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Preserve exact legacy values. Never guess a break interval from its duration.
        migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.ShiftBreakConversionMapping', N'U') IS NULL
    CREATE TABLE dbo.ShiftBreakConversionMapping (
        ShiftId int NOT NULL PRIMARY KEY,
        BreakStartTime time(0) NULL, BreakEndTime time(0) NULL,
        BreakStartDayOffset tinyint NULL, BreakEndDayOffset tinyint NULL,
        Confirmed bit NOT NULL DEFAULT 0);
IF EXISTS (SELECT 1 FROM dbo.Shift s LEFT JOIN dbo.ShiftBreakConversionMapping m ON m.ShiftId=s.ShiftId
    WHERE s.BreakMinutes > 0 AND (m.ShiftId IS NULL OR m.Confirmed=0))
    THROW 51000, 'Review legacy breaks using scripts/prepare-shift-breaks.sql before updating the database.', 1;
IF EXISTS (SELECT 1 FROM dbo.Shift s JOIN dbo.ShiftBreakConversionMapping m ON m.ShiftId=s.ShiftId
    WHERE s.BreakMinutes > 0 AND s.IsBreakPaid=0 AND
    (m.BreakStartTime IS NULL OR m.BreakEndTime IS NULL OR m.BreakStartDayOffset IS NULL OR m.BreakEndDayOffset IS NULL
     OR m.BreakStartDayOffset NOT IN (0,1) OR m.BreakEndDayOffset NOT IN (0,1)
     OR DATEDIFF(SECOND, CAST('00:00' AS time), m.BreakStartTime) + 86400*m.BreakStartDayOffset < DATEDIFF(SECOND, CAST('00:00' AS time), s.StartTime)
     OR DATEDIFF(SECOND, CAST('00:00' AS time), m.BreakEndTime) + 86400*m.BreakEndDayOffset > DATEDIFF(SECOND, CAST('00:00' AS time), s.EndTime) + CASE WHEN s.EndTime<s.StartTime THEN 86400 ELSE 0 END
     OR DATEDIFF(SECOND, CAST('00:00' AS time), m.BreakEndTime) + 86400*m.BreakEndDayOffset
        - DATEDIFF(SECOND, CAST('00:00' AS time), m.BreakStartTime) - 86400*m.BreakStartDayOffset <> 60*s.BreakMinutes))
    THROW 51001, 'Break mapping must be inside the shift and preserve the original unpaid duration.', 1;
IF OBJECT_ID(N'dbo.ShiftLegacyArchive_20261001', N'U') IS NOT NULL
    THROW 51002, 'Legacy archive already exists. Review the previous conversion before proceeding.', 1;
SELECT * INTO dbo.ShiftLegacyArchive_20261001 FROM dbo.Shift;
");
        migrationBuilder.DropForeignKey("FK_Shift_Store_StoreId", "Shift");
        migrationBuilder.DropIndex("IX_Shift_ShiftCode", "Shift");
        migrationBuilder.DropIndex("IX_Shift_StoreId", "Shift");
        migrationBuilder.AddColumn<byte>("EndDayOffset", "Shift", "tinyint", nullable: false, defaultValue: (byte)0);
        migrationBuilder.AddColumn<TimeSpan>("BreakStartTime", "Shift", "time(0)", nullable: true);
        migrationBuilder.AddColumn<TimeSpan>("BreakEndTime", "Shift", "time(0)", nullable: true);
        migrationBuilder.AddColumn<byte>("BreakStartDayOffset", "Shift", "tinyint", nullable: true);
        migrationBuilder.AddColumn<byte>("BreakEndDayOffset", "Shift", "tinyint", nullable: true);
        migrationBuilder.CreateTable("StoreShift", table => new
        {
            StoreId = table.Column<int>("int", nullable: false),
            ShiftId = table.Column<int>("int", nullable: false),
            IsActive = table.Column<bool>("bit", nullable: false),
            CreatedDate = table.Column<DateTime>("datetime2(0)", precision: 0, nullable: false),
            CreatedBy = table.Column<string>("nvarchar(30)", maxLength: 30, nullable: false),
            UpdatedDate = table.Column<DateTime>("datetime2(0)", precision: 0, nullable: true),
            UpdatedBy = table.Column<string>("nvarchar(30)", maxLength: 30, nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_StoreShift", x => new { x.StoreId, x.ShiftId });
            table.ForeignKey("FK_StoreShift_Store_StoreId", x => x.StoreId, "Store", "StoreId", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_StoreShift_Shift_ShiftId", x => x.ShiftId, "Shift", "ShiftId", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.Sql(@"
UPDATE s SET EndDayOffset=CASE WHEN s.EndTime<s.StartTime THEN 1 ELSE 0 END,
    BreakStartTime=CASE WHEN s.BreakMinutes>0 AND s.IsBreakPaid=0 THEN m.BreakStartTime ELSE NULL END,
    BreakEndTime=CASE WHEN s.BreakMinutes>0 AND s.IsBreakPaid=0 THEN m.BreakEndTime ELSE NULL END,
    BreakStartDayOffset=CASE WHEN s.BreakMinutes>0 AND s.IsBreakPaid=0 THEN m.BreakStartDayOffset ELSE NULL END,
    BreakEndDayOffset=CASE WHEN s.BreakMinutes>0 AND s.IsBreakPaid=0 THEN m.BreakEndDayOffset ELSE NULL END
FROM dbo.Shift s LEFT JOIN dbo.ShiftBreakConversionMapping m ON m.ShiftId=s.ShiftId;
INSERT dbo.StoreShift (StoreId,ShiftId,IsActive,CreatedDate,CreatedBy)
SELECT st.StoreId,s.ShiftId,1,SYSDATETIME(),'_migration_'
FROM dbo.Shift s JOIN dbo.Store st ON s.StoreId IS NULL OR s.StoreId=st.StoreId;
");
        foreach (var name in new[] { "Type", "ShiftCode", "StoreId", "IsTemplate", "BreakMinutes", "IsBreakPaid",
            "AllowOutsideStoreHours", "OutsideHoursReason", "DefaultRequiredHeadcount", "DefaultMaximumHeadcount",
            "EarlyCheckInMinutes", "LateThresholdMinutes" })
            migrationBuilder.DropColumn(name, "Shift");
        migrationBuilder.AlterColumn<TimeSpan>("StartTime", "Shift", "time(0)", nullable: false, oldClrType: typeof(TimeSpan), oldType: "time");
        migrationBuilder.AlterColumn<TimeSpan>("EndTime", "Shift", "time(0)", nullable: false, oldClrType: typeof(TimeSpan), oldType: "time");
        migrationBuilder.AlterColumn<string>("Description", "Shift", "nvarchar(255)", maxLength: 255, nullable: true,
            oldClrType: typeof(string), oldType: "nvarchar(255)", oldMaxLength: 255);
        migrationBuilder.AddCheckConstraint("CK_Shift_BreakFields", "Shift", "([BreakStartTime] IS NULL AND [BreakEndTime] IS NULL AND [BreakStartDayOffset] IS NULL AND [BreakEndDayOffset] IS NULL) OR ([BreakStartTime] IS NOT NULL AND [BreakEndTime] IS NOT NULL AND [BreakStartDayOffset] IS NOT NULL AND [BreakEndDayOffset] IS NOT NULL)");
        migrationBuilder.AddCheckConstraint("CK_Shift_DayOffsets", "Shift", "[EndDayOffset] IN (0,1) AND ([BreakStartDayOffset] IS NULL OR [BreakStartDayOffset] IN (0,1)) AND ([BreakEndDayOffset] IS NULL OR [BreakEndDayOffset] IN (0,1))");
        migrationBuilder.AddCheckConstraint("CK_Shift_EffectiveDates", "Shift", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
        migrationBuilder.CreateIndex("IX_StoreShift_ShiftId", "StoreShift", "ShiftId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Many-to-many assignments cannot be faithfully restored into one nullable StoreId.
        throw new NotSupportedException("Restore the pre-conversion database backup to roll back this migration. The legacy archive is available for reconciliation; automatic rollback would lose store assignments.");
    }
}
