BEGIN TRANSACTION;
GO


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

GO

ALTER TABLE [Shift] DROP CONSTRAINT [FK_Shift_Store_StoreId];
GO

DROP INDEX [IX_Shift_ShiftCode] ON [Shift];
GO

DROP INDEX [IX_Shift_StoreId] ON [Shift];
GO

ALTER TABLE [Shift] ADD [EndDayOffset] tinyint NOT NULL DEFAULT CAST(0 AS tinyint);
GO

ALTER TABLE [Shift] ADD [BreakStartTime] time(0) NULL;
GO

ALTER TABLE [Shift] ADD [BreakEndTime] time(0) NULL;
GO

ALTER TABLE [Shift] ADD [BreakStartDayOffset] tinyint NULL;
GO

ALTER TABLE [Shift] ADD [BreakEndDayOffset] tinyint NULL;
GO

CREATE TABLE [StoreShift] (
    [StoreId] int NOT NULL,
    [ShiftId] int NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedDate] datetime2(0) NOT NULL,
    [CreatedBy] nvarchar(30) NOT NULL,
    [UpdatedDate] datetime2(0) NULL,
    [UpdatedBy] nvarchar(30) NULL,
    CONSTRAINT [PK_StoreShift] PRIMARY KEY ([StoreId], [ShiftId]),
    CONSTRAINT [FK_StoreShift_Store_StoreId] FOREIGN KEY ([StoreId]) REFERENCES [Store] ([StoreId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StoreShift_Shift_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shift] ([ShiftId]) ON DELETE NO ACTION
);
GO


UPDATE s SET EndDayOffset=CASE WHEN s.EndTime<s.StartTime THEN 1 ELSE 0 END,
    BreakStartTime=CASE WHEN s.BreakMinutes>0 AND s.IsBreakPaid=0 THEN m.BreakStartTime ELSE NULL END,
    BreakEndTime=CASE WHEN s.BreakMinutes>0 AND s.IsBreakPaid=0 THEN m.BreakEndTime ELSE NULL END,
    BreakStartDayOffset=CASE WHEN s.BreakMinutes>0 AND s.IsBreakPaid=0 THEN m.BreakStartDayOffset ELSE NULL END,
    BreakEndDayOffset=CASE WHEN s.BreakMinutes>0 AND s.IsBreakPaid=0 THEN m.BreakEndDayOffset ELSE NULL END
FROM dbo.Shift s LEFT JOIN dbo.ShiftBreakConversionMapping m ON m.ShiftId=s.ShiftId;
INSERT dbo.StoreShift (StoreId,ShiftId,IsActive,CreatedDate,CreatedBy)
SELECT st.StoreId,s.ShiftId,1,SYSDATETIME(),'_migration_'
FROM dbo.Shift s JOIN dbo.Store st ON s.StoreId IS NULL OR s.StoreId=st.StoreId;

GO

DECLARE @var0 sysname;
SELECT @var0 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Shift]') AND [c].[name] = N'Type');
IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Shift] DROP CONSTRAINT [' + @var0 + '];');
ALTER TABLE [Shift] DROP COLUMN [Type];
GO

DECLARE @var1 sysname;
SELECT @var1 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Shift]') AND [c].[name] = N'ShiftCode');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Shift] DROP CONSTRAINT [' + @var1 + '];');
ALTER TABLE [Shift] DROP COLUMN [ShiftCode];
GO

DECLARE @var2 sysname;
SELECT @var2 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Shift]') AND [c].[name] = N'StoreId');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Shift] DROP CONSTRAINT [' + @var2 + '];');
ALTER TABLE [Shift] DROP COLUMN [StoreId];
GO

DECLARE @var3 sysname;
SELECT @var3 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Shift]') AND [c].[name] = N'IsTemplate');
IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Shift] DROP CONSTRAINT [' + @var3 + '];');
ALTER TABLE [Shift] DROP COLUMN [IsTemplate];
GO

DECLARE @var4 sysname;
SELECT @var4 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Shift]') AND [c].[name] = N'BreakMinutes');
IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [Shift] DROP CONSTRAINT [' + @var4 + '];');
ALTER TABLE [Shift] DROP COLUMN [BreakMinutes];
GO

DECLARE @var5 sysname;
SELECT @var5 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Shift]') AND [c].[name] = N'IsBreakPaid');
IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [Shift] DROP CONSTRAINT [' + @var5 + '];');
ALTER TABLE [Shift] DROP COLUMN [IsBreakPaid];
GO

DECLARE @var6 sysname;
SELECT @var6 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Shift]') AND [c].[name] = N'AllowOutsideStoreHours');
IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [Shift] DROP CONSTRAINT [' + @var6 + '];');
ALTER TABLE [Shift] DROP COLUMN [AllowOutsideStoreHours];
GO

DECLARE @var7 sysname;
SELECT @var7 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Shift]') AND [c].[name] = N'OutsideHoursReason');
IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [Shift] DROP CONSTRAINT [' + @var7 + '];');
ALTER TABLE [Shift] DROP COLUMN [OutsideHoursReason];
GO

DECLARE @var8 sysname;
SELECT @var8 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Shift]') AND [c].[name] = N'DefaultRequiredHeadcount');
IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [Shift] DROP CONSTRAINT [' + @var8 + '];');
ALTER TABLE [Shift] DROP COLUMN [DefaultRequiredHeadcount];
GO

DECLARE @var9 sysname;
SELECT @var9 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Shift]') AND [c].[name] = N'DefaultMaximumHeadcount');
IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [Shift] DROP CONSTRAINT [' + @var9 + '];');
ALTER TABLE [Shift] DROP COLUMN [DefaultMaximumHeadcount];
GO

DECLARE @var10 sysname;
SELECT @var10 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Shift]') AND [c].[name] = N'EarlyCheckInMinutes');
IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [Shift] DROP CONSTRAINT [' + @var10 + '];');
ALTER TABLE [Shift] DROP COLUMN [EarlyCheckInMinutes];
GO

DECLARE @var11 sysname;
SELECT @var11 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Shift]') AND [c].[name] = N'LateThresholdMinutes');
IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [Shift] DROP CONSTRAINT [' + @var11 + '];');
ALTER TABLE [Shift] DROP COLUMN [LateThresholdMinutes];
GO

DECLARE @var12 sysname;
SELECT @var12 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Shift]') AND [c].[name] = N'StartTime');
IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [Shift] DROP CONSTRAINT [' + @var12 + '];');
ALTER TABLE [Shift] ALTER COLUMN [StartTime] time(0) NOT NULL;
GO

DECLARE @var13 sysname;
SELECT @var13 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Shift]') AND [c].[name] = N'EndTime');
IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [Shift] DROP CONSTRAINT [' + @var13 + '];');
ALTER TABLE [Shift] ALTER COLUMN [EndTime] time(0) NOT NULL;
GO

DECLARE @var14 sysname;
SELECT @var14 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Shift]') AND [c].[name] = N'Description');
IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [Shift] DROP CONSTRAINT [' + @var14 + '];');
ALTER TABLE [Shift] ALTER COLUMN [Description] nvarchar(255) NULL;
GO

ALTER TABLE [Shift] ADD CONSTRAINT [CK_Shift_BreakFields] CHECK (([BreakStartTime] IS NULL AND [BreakEndTime] IS NULL AND [BreakStartDayOffset] IS NULL AND [BreakEndDayOffset] IS NULL) OR ([BreakStartTime] IS NOT NULL AND [BreakEndTime] IS NOT NULL AND [BreakStartDayOffset] IS NOT NULL AND [BreakEndDayOffset] IS NOT NULL));
GO

ALTER TABLE [Shift] ADD CONSTRAINT [CK_Shift_DayOffsets] CHECK ([EndDayOffset] IN (0,1) AND ([BreakStartDayOffset] IS NULL OR [BreakStartDayOffset] IN (0,1)) AND ([BreakEndDayOffset] IS NULL OR [BreakEndDayOffset] IN (0,1)));
GO

ALTER TABLE [Shift] ADD CONSTRAINT [CK_Shift_EffectiveDates] CHECK ([EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]);
GO

CREATE INDEX [IX_StoreShift_ShiftId] ON [StoreShift] ([ShiftId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261001110125_ReplaceShiftWithStoreAssignments', N'6.0.0');
GO

COMMIT;
GO

