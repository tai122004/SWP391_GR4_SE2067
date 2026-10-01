-- Run on the OLD schema, before starting the new application (Program auto-migrates).
-- This creates a review worksheet; it does not guess times or confirm any row.
IF OBJECT_ID(N'dbo.ShiftBreakConversionMapping', N'U') IS NULL
    CREATE TABLE dbo.ShiftBreakConversionMapping (
        ShiftId int NOT NULL PRIMARY KEY,
        BreakStartTime time(0) NULL, BreakEndTime time(0) NULL,
        BreakStartDayOffset tinyint NULL, BreakEndDayOffset tinyint NULL,
        Confirmed bit NOT NULL DEFAULT 0);
INSERT dbo.ShiftBreakConversionMapping (ShiftId)
SELECT s.ShiftId FROM dbo.Shift s
WHERE s.BreakMinutes>0 AND NOT EXISTS (SELECT 1 FROM dbo.ShiftBreakConversionMapping m WHERE m.ShiftId=s.ShiftId);
SELECT s.ShiftId,s.ShiftName,s.StartTime,s.EndTime,s.BreakMinutes,s.IsBreakPaid,
    m.BreakStartTime,m.BreakEndTime,m.BreakStartDayOffset,m.BreakEndDayOffset,m.Confirmed
FROM dbo.Shift s JOIN dbo.ShiftBreakConversionMapping m ON m.ShiftId=s.ShiftId
ORDER BY s.ShiftId;
-- Example ONLY: use the actual ShiftId and reviewed break interval.
-- UPDATE dbo.ShiftBreakConversionMapping SET BreakStartTime='12:00',BreakEndTime='12:30',
--     BreakStartDayOffset=0,BreakEndDayOffset=0,Confirmed=1 WHERE ShiftId=<actual-id>;
-- For paid breaks: confirm explicitly; new unpaid interval remains NULL to preserve paid hours.
-- UPDATE dbo.ShiftBreakConversionMapping SET Confirmed=1 WHERE ShiftId=<reviewed-paid-break-id>;
