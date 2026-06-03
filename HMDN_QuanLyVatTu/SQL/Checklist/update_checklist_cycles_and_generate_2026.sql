USE HospitalAssetDB;
GO

-- 1. Assign EL-2023-001 (Id = 27) and EL-2023-002 (Id = 28) to yearly cycle "Bảo dưỡng điều hòa" (Id = 8)
-- This ensures that yearly checklist schedules are generated and tracked in the system.
UPDATE dbo.Inventory
SET CheckCycleId = 8, -- Bảo dưỡng điều hòa (yearly)
    UpdatedAt = GETDATE()
WHERE AssetCode IN ('EL-2023-001', 'EL-2023-002');
GO

-- 2. Regenerate checklist schedules for the entire year of 2026
-- This covers daily, weekly, monthly, quarterly, and yearly schedules, providing rich data for dashboard analysis.
DECLARE @Start DATE = '2026-01-01';
DECLARE @End DATE = '2026-12-31';

EXEC dbo.sp_GenerateChecklistSchedules @FromDate = @Start, @ToDate = @End;
GO

-- 3. Synchronize alert notifications for any overdue checklists
EXEC dbo.sp_RunAlertDiagnostics;
GO

PRINT 'Checklist cycles updated and schedules generated for the entire year of 2026 successfully.';
