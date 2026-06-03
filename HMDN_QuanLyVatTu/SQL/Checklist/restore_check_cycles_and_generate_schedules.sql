USE HospitalAssetDB;
GO

SET IDENTITY_INSERT dbo.CheckCycles ON;

-- 1. Insert/Restore Id = 1 (Kiểm tra hàng ngày - Chung)
IF NOT EXISTS (SELECT 1 FROM dbo.CheckCycles WHERE Id = 1)
BEGIN
    INSERT INTO dbo.CheckCycles (Id, Name, CycleType, RepeatOn, IsRepeat, RepeatCount, EndDate, Description, IsActive)
    VALUES (1, N'Kiểm tra hàng ngày', 'daily', 'All', 1, 365, '2027-05-16', N'Kiểm tra mỗi ngày làm việc', 1);
END;

-- 2. Insert/Restore Id = 2 (Kiểm tra hàng tuần - T2, T6)
IF NOT EXISTS (SELECT 1 FROM dbo.CheckCycles WHERE Id = 2)
BEGIN
    INSERT INTO dbo.CheckCycles (Id, Name, CycleType, RepeatOn, IsRepeat, RepeatCount, EndDate, Description, IsActive)
    VALUES (2, N'Kiểm tra hàng tuần (T2,T6)', 'weekly', '[2,6]', 1, 52, '2027-05-16', N'Mỗi thứ 2 và thứ 6', 1);
END;

-- 3. Insert/Restore Id = 3 (Kiểm tra hàng ngày (Thiết bị))
IF NOT EXISTS (SELECT 1 FROM dbo.CheckCycles WHERE Id = 3)
BEGIN
    INSERT INTO dbo.CheckCycles (Id, Name, CycleType, RepeatOn, IsRepeat, RepeatCount, EndDate, Description, IsActive)
    VALUES (3, N'Kiểm tra hàng ngày (Thiết bị)', 'daily', 'All', 1, 365, '2027-05-16', N'Kiểm tra tình trạng thiết bị hàng ngày', 1);
END;

-- 4. Insert/Restore Id = 4 (Kiểm định 3 tháng)
IF NOT EXISTS (SELECT 1 FROM dbo.CheckCycles WHERE Id = 4)
BEGIN
    INSERT INTO dbo.CheckCycles (Id, Name, CycleType, RepeatOn, IsRepeat, RepeatCount, EndDate, Description, IsActive)
    VALUES (4, N'Kiểm định 3 tháng', 'quarterly', '15', 1, 4, '2027-05-16', N'Tháng 1, 4, 7, 10', 1);
END;

SET IDENTITY_INSERT dbo.CheckCycles OFF;
GO

-- 5. Regenerate checklist schedules for the month of June 2026
DECLARE @Start DATE = '2026-06-01';
DECLARE @End DATE = '2026-06-30';
EXEC dbo.sp_GenerateChecklistSchedules @FromDate = @Start, @ToDate = @End;
GO

-- 6. Run Alert Diagnostics to sync notifications
EXEC dbo.sp_RunAlertDiagnostics;
GO

PRINT 'CheckCycles restored, schedules regenerated, and alerts synchronized successfully.';
