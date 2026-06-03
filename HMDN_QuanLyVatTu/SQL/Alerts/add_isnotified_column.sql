-- ============================================================
-- SQL Migration Script: Add IsNotified and Seed AlertRules
-- ============================================================

USE HospitalAssetDB;
GO

-- 1. Add IsNotified column to Alerts table if not exists
IF NOT EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Alerts' AND COLUMN_NAME = 'IsNotified'
)
BEGIN
    ALTER TABLE Alerts ADD IsNotified BIT NOT NULL DEFAULT 0;
    PRINT 'Added IsNotified column to Alerts table successfully.';
END
ELSE
BEGIN
    PRINT 'IsNotified column already exists in Alerts table.';
END
GO

-- 2. Seed default AlertRules if empty
IF (SELECT COUNT(*) FROM AlertRules) = 0
BEGIN
    INSERT INTO AlertRules (Code, Name, AlertType, ThresholdDays, ThresholdCount, ThresholdPeriodDays, IsActive) VALUES
    ('CHECKLIST_DUE_3D',    N'Đến hạn kiểm tra (3 ngày)',     'checklist_due',      3,    NULL, NULL, 1),
    ('CHECKLIST_OVERDUE',   N'Quá hạn checklist',              'checklist_overdue',  0,    NULL, NULL, 1),
    ('MULTI_FAULT_3X',      N'Lỗi ≥ 3 lần trong 30 ngày',     'multi_fault',        NULL, 3,    30,   1),
    ('DEPRECIATION_END_30D',N'Gần hết khấu hao (30 ngày)',     'depreciation_end',   30,   NULL, NULL, 1),
    ('EXPIRY_SOON_60D',     N'Gần hết hạn sử dụng (60 ngày)', 'expiry_soon',        60,   NULL, NULL, 1),
    ('WARRANTY_EXPIRY_30D', N'Gần hết bảo hành (30 ngày)',     'warranty_expiry',    30,   NULL, NULL, 1);
    PRINT 'Seeded default AlertRules successfully.';
END
ELSE
BEGIN
    PRINT 'AlertRules table is not empty. Skipping seeding.';
END
GO
