USE HospitalAssetDB;
GO

-- 1. Ensure NVARCHAR columns on AlertRules and Alerts
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AlertRules' AND COLUMN_NAME = 'Name' AND DATA_TYPE = 'varchar')
BEGIN
    ALTER TABLE AlertRules ALTER COLUMN Name NVARCHAR(300) NOT NULL;
    PRINT 'Altered AlertRules.Name to NVARCHAR(300)';
END
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AlertRules' AND COLUMN_NAME = 'Description' AND DATA_TYPE = 'varchar')
BEGIN
    ALTER TABLE AlertRules ALTER COLUMN Description NVARCHAR(500) NULL;
    PRINT 'Altered AlertRules.Description to NVARCHAR(500)';
END
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Alerts' AND COLUMN_NAME = 'Title' AND DATA_TYPE = 'varchar')
BEGIN
    ALTER TABLE Alerts ALTER COLUMN Title NVARCHAR(300) NOT NULL;
    PRINT 'Altered Alerts.Title to NVARCHAR(300)';
END
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Alerts' AND COLUMN_NAME = 'Body' AND DATA_TYPE = 'varchar')
BEGIN
    ALTER TABLE Alerts ALTER COLUMN Body NVARCHAR(1000) NULL;
    PRINT 'Altered Alerts.Body to NVARCHAR(1000)';
END
GO

-- 2. Seed or update CONSUMABLES_LOW alert rule
IF NOT EXISTS (SELECT 1 FROM AlertRules WHERE Code = 'CONSUMABLES_LOW')
BEGIN
    INSERT INTO AlertRules (Code, Name, AlertType, ThresholdDays, ThresholdCount, ThresholdPeriodDays, IsActive, Description)
    VALUES (
        'CONSUMABLES_LOW', 
        N'Vật tư tiêu hao sắp hết', 
        'consumables_low', 
        NULL, 
        10, 
        NULL, 
        1, 
        N'{"text": "Cảnh báo khi lượng tồn kho vật tư tiêu hao xuống dưới ngưỡng tối thiểu.", "thresholds": {"PRINTER": 10, "UPS": 5, "OFFICE": 15, "CDHA": 20, "HSCC": 8, "PHONGMO": 12, "XETNGHIEM": 25}}'
    );
    PRINT 'Seeded CONSUMABLES_LOW alert rule successfully.';
END
ELSE
BEGIN
    UPDATE AlertRules
    SET Name = N'Vật tư tiêu hao sắp hết',
        Description = N'{"text": "Cảnh báo khi lượng tồn kho vật tư tiêu hao xuống dưới ngưỡng tối thiểu.", "thresholds": {"PRINTER": 10, "UPS": 5, "OFFICE": 15, "CDHA": 20, "HSCC": 8, "PHONGMO": 12, "XETNGHIEM": 25}}'
    WHERE Code = 'CONSUMABLES_LOW';
    PRINT 'Updated CONSUMABLES_LOW alert rule successfully.';
END
GO

-- 3. Update existing AlertRules to correct UTF-8 representations
UPDATE AlertRules SET Name = N'Đến hạn kiểm tra (3 ngày)', Description = N'{"text": "Cảnh báo nhắc lịch bảo trì sắp đến hạn kiểm tra định kỳ.", "overrides": []}' WHERE Code = 'CHECKLIST_DUE_3D';
UPDATE AlertRules SET Name = N'Quá hạn checklist', Description = N'{"text": "Cảnh báo khi kế hoạch checklist bảo trì của thiết bị quá hạn.", "overrides": []}' WHERE Code = 'CHECKLIST_OVERDUE';
UPDATE AlertRules SET Name = N'Lỗi ≥ 3 lần trong 30 ngày', Description = N'{"text": "Cảnh báo tự động đề xuất thanh lý khi thiết bị hỏng vượt ngưỡng tần suất.", "overrides": []}' WHERE Code = 'MULTI_FAULT_3X';
UPDATE AlertRules SET Name = N'Gần hết khấu hao (30 ngày)', Description = N'{"text": "Cảnh báo khi chu kỳ khấu hao của thiết bị sắp kết thúc.", "overrides": []}' WHERE Code = 'DEPRECIATION_END_30D';
UPDATE AlertRules SET Name = N'Gần hết hạn sử dụng (60 ngày)', Description = N'{"text": "Cảnh báo thiết bị y tế gần hết thời hạn sử dụng hữu ích.", "overrides": []}' WHERE Code = 'EXPIRY_SOON_60D';
UPDATE AlertRules SET Name = N'Gần hết bảo hành (30 ngày)', Description = N'{"text": "Cảnh báo thời gian bảo hành chính hãng sắp hết hạn.", "overrides": []}' WHERE Code = 'WARRANTY_EXPIRY_30D';
UPDATE AlertRules SET Name = N'Kiểm tra không đạt chuẩn', Description = N'{"text": "Cảnh báo khi kết quả thực hiện checklist của thiết bị ghi nhận lỗi/hỏng (không đạt).", "overrides": []}' WHERE Code = 'CHECKLIST_FAIL';
GO

-- 4. Correct historical Alerts that map incorrectly
DECLARE @ConsumablesId INT;
SELECT @ConsumablesId = Id FROM AlertRules WHERE Code = 'CONSUMABLES_LOW';

IF @ConsumablesId IS NOT NULL
BEGIN
    UPDATE Alerts
    SET AlertRuleId = @ConsumablesId
    WHERE Title = N'Vật tư tiêu hao sắp hết' OR Body LIKE N'%Mức tối thiểu%';
    PRINT 'Fixed historical alerts to refer to CONSUMABLES_LOW rule.';
END
GO

PRINT 'seed_alert_rules_utf8.sql executed successfully.';
