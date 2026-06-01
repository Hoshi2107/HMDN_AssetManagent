-- ============================================================
-- SQL Script: Create Stored Procedures for Alerts
-- ============================================================

USE HospitalAssetDB;
GO

-- 1. DROP EXISTING PROCEDURES IF THEY EXIST
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_RunAlertDiagnostics]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[sp_RunAlertDiagnostics];
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_GetAlertList]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[sp_GetAlertList];
GO

-- 2. CREATE PROCEDURE: sp_RunAlertDiagnostics
CREATE PROCEDURE [dbo].[sp_RunAlertDiagnostics]
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @Today DATE = CAST(GETDATE() AS DATE);
    
    -- ── RULE 1: MULTI_FAULT ──
    DECLARE @MultiFaultRuleId INT, @MultiFaultActive BIT, @MultiFaultCount INT, @MultiFaultPeriod INT;
    SELECT 
        @MultiFaultRuleId = Id, 
        @MultiFaultActive = IsActive, 
        @MultiFaultCount = ThresholdCount, 
        @MultiFaultPeriod = ThresholdPeriodDays
    FROM AlertRules 
    WHERE Code = 'MULTI_FAULT_3X';
    
    IF @MultiFaultActive = 1
    BEGIN
        DECLARE @CutoffDate DATETIME2 = DATEADD(DAY, -ISNULL(@MultiFaultPeriod, 30), GETDATE());
        
        -- Temporary table for assets violating threshold
        SELECT InventoryId, COUNT(*) AS FaultCount
        INTO #FaultyDevices
        FROM MaintenanceLogs
        WHERE CreatedAt >= @CutoffDate
        GROUP BY InventoryId
        HAVING COUNT(*) >= ISNULL(@MultiFaultCount, 3);
        
        -- Insert new alerts
        INSERT INTO Alerts (AlertRuleId, InventoryId, Title, Body, Severity, IsResolved, IsNotified, CreatedAt)
        SELECT 
            @MultiFaultRuleId,
            fd.InventoryId,
            N'Lỗi lặp lại nhiều lần (Multi-fault)',
            N'Thiết bị ' + it.Name + N' (Mã: ' + inv.AssetCode + N') đã bị hỏng hóc/báo sửa chữa ' + CAST(fd.FaultCount AS NVARCHAR(10)) + N' lần trong ' + CAST(ISNULL(@MultiFaultPeriod, 30) AS NVARCHAR(10)) + N' ngày qua. Khuyến nghị: Hệ thống tự động gợi ý nên làm thủ tục thanh lý thay vì tiếp tục chi trả chi phí sửa chữa.',
            'danger',
            0,
            0,
            GETDATE()
        FROM #FaultyDevices fd
        JOIN Inventory inv ON fd.InventoryId = inv.Id
        JOIN Items it ON inv.ItemId = it.Id
        WHERE NOT EXISTS (
            SELECT 1 FROM Alerts a 
            WHERE a.AlertRuleId = @MultiFaultRuleId AND a.InventoryId = fd.InventoryId AND a.IsResolved = 0
        );
        
        DROP TABLE #FaultyDevices;
    END

    -- ── RULE 2: WARRANTY EXPIRES ──
    DECLARE @WarrantyRuleId INT, @WarrantyActive BIT, @WarrantyThreshold INT;
    SELECT 
        @WarrantyRuleId = Id, 
        @WarrantyActive = IsActive, 
        @WarrantyThreshold = ThresholdDays
    FROM AlertRules 
    WHERE Code = 'WARRANTY_EXPIRY_30D';
    
    IF @WarrantyActive = 1
    BEGIN
        DECLARE @WarrantyTarget DATE = DATEADD(DAY, ISNULL(@WarrantyThreshold, 30), @Today);
        
        INSERT INTO Alerts (AlertRuleId, InventoryId, Title, Body, Severity, IsResolved, IsNotified, CreatedAt)
        SELECT
            @WarrantyRuleId,
            inv.Id,
            N'Sắp hết hạn bảo hành',
            N'Hợp đồng bảo hành chính hãng thiết bị ' + it.Name + N' (Mã: ' + inv.AssetCode + N') sẽ hết hạn sau ' + CAST(DATEDIFF(DAY, @Today, inv.WarrantyExpiry) AS NVARCHAR(10)) + N' ngày (hết hạn ngày ' + CONVERT(NVARCHAR(10), inv.WarrantyExpiry, 103) + N'). Vui lòng liên hệ nhà cung cấp để gia hạn.',
            'warning',
            0,
            0,
            GETDATE()
        FROM Inventory inv
        JOIN Items it ON inv.ItemId = it.Id
        WHERE inv.LifeStatus = 'active' 
          AND inv.ApprovalStatus = 'approved' 
          AND inv.WarrantyExpiry IS NOT NULL 
          AND inv.WarrantyExpiry >= @Today 
          AND inv.WarrantyExpiry <= @WarrantyTarget
          AND NOT EXISTS (
              SELECT 1 FROM Alerts a 
              WHERE a.AlertRuleId = @WarrantyRuleId AND a.InventoryId = inv.Id AND a.IsResolved = 0
          );
    END

    -- ── RULE 3: EXPIRY SOON ──
    DECLARE @ExpiryRuleId INT, @ExpiryActive BIT, @ExpiryThreshold INT;
    SELECT 
        @ExpiryRuleId = Id, 
        @ExpiryActive = IsActive, 
        @ExpiryThreshold = ThresholdDays
    FROM AlertRules 
    WHERE Code = 'EXPIRY_SOON_60D';
    
    IF @ExpiryActive = 1
    BEGIN
        DECLARE @ExpiryTarget DATE = DATEADD(DAY, ISNULL(@ExpiryThreshold, 60), @Today);
        
        INSERT INTO Alerts (AlertRuleId, InventoryId, Title, Body, Severity, IsResolved, IsNotified, CreatedAt)
        SELECT
            @ExpiryRuleId,
            inv.Id,
            N'Thiết bị sắp hết hạn sử dụng',
            N'Thiết bị y tế ' + it.Name + N' (Mã: ' + inv.AssetCode + N') sắp hết hạn sử dụng sau ' + CAST(DATEDIFF(DAY, @Today, inv.ExpiryDate) AS NVARCHAR(10)) + N' ngày (ngày hết hạn: ' + CONVERT(NVARCHAR(10), inv.ExpiryDate, 103) + N'). Vui lòng làm thủ tục gia hạn kiểm định hoặc mua sắm mới thay thế.',
            'warning',
            0,
            0,
            GETDATE()
        FROM Inventory inv
        JOIN Items it ON inv.ItemId = it.Id
        WHERE inv.LifeStatus = 'active' 
          AND inv.ApprovalStatus = 'approved' 
          AND inv.ExpiryDate IS NOT NULL 
          AND inv.ExpiryDate >= @Today 
          AND inv.ExpiryDate <= @ExpiryTarget
          AND NOT EXISTS (
              SELECT 1 FROM Alerts a 
              WHERE a.AlertRuleId = @ExpiryRuleId AND a.InventoryId = inv.Id AND a.IsResolved = 0
          );
    END

    -- ── RULE 4: CHECKLIST OVERDUE ──
    DECLARE @ChecklistRuleId INT, @ChecklistActive BIT, @ChecklistThreshold INT;
    SELECT 
        @ChecklistRuleId = Id, 
        @ChecklistActive = IsActive,
        @ChecklistThreshold = ThresholdDays
    FROM AlertRules 
    WHERE Code = 'CHECKLIST_OVERDUE';
    
    IF @ChecklistActive = 1
    BEGIN
        INSERT INTO Alerts (AlertRuleId, InventoryId, Title, Body, Severity, IsResolved, IsNotified, CreatedAt)
        SELECT
            @ChecklistRuleId,
            cs.InventoryId,
            N'Quá hạn checklist bảo trì',
            N'Lịch bảo trì định kỳ (' + 
                CASE cs.CycleType 
                    WHEN 'monthly' THEN N'Hàng tháng'
                    WHEN 'yearly' THEN N'Hàng năm'
                    WHEN 'weekly' THEN N'Hàng tuần'
                    ELSE N'Hàng ngày' 
                END + N') của thiết bị ' + it.Name + N' (Mã: ' + inv.AssetCode + N') đã quá hạn từ ngày ' + CONVERT(NVARCHAR(10), cs.DueDate, 103) + N'. Yêu cầu bộ phận kỹ thuật thực hiện kiểm tra khẩn cấp.',
            'info',
            0,
            0,
            GETDATE()
        FROM ChecklistSchedules cs
        JOIN Inventory inv ON cs.InventoryId = inv.Id
        JOIN Items it ON inv.ItemId = it.Id
        WHERE cs.Status = 'pending' 
          AND cs.DueDate <= DATEADD(DAY, -ISNULL(@ChecklistThreshold, 0), @Today)
          AND NOT EXISTS (
              SELECT 1 FROM Alerts a 
              WHERE a.AlertRuleId = @ChecklistRuleId AND a.InventoryId = cs.InventoryId AND a.IsResolved = 0
          );
    END

    -- ── RULE 4B: CHECKLIST DUE ──
    DECLARE @ChecklistDueRuleId INT, @ChecklistDueActive BIT, @ChecklistDueThreshold INT;
    SELECT 
        @ChecklistDueRuleId = Id, 
        @ChecklistDueActive = IsActive,
        @ChecklistDueThreshold = ThresholdDays
    FROM AlertRules 
    WHERE Code = 'CHECKLIST_DUE_3D';
    
    IF @ChecklistDueActive = 1
    BEGIN
        INSERT INTO Alerts (AlertRuleId, InventoryId, Title, Body, Severity, IsResolved, IsNotified, CreatedAt)
        SELECT
            @ChecklistDueRuleId,
            cs.InventoryId,
            N'Lịch kiểm tra sắp đến hạn',
            N'Lịch kiểm tra/bảo trì định kỳ (' + 
                CASE cs.CycleType 
                    WHEN 'monthly' THEN N'Hàng tháng'
                    WHEN 'yearly' THEN N'Hàng năm'
                    WHEN 'weekly' THEN N'Hàng tuần'
                    ELSE N'Hàng ngày' 
                END + N') của thiết bị ' + it.Name + N' (Mã: ' + inv.AssetCode + N') sắp đến hạn vào ngày ' + CONVERT(NVARCHAR(10), cs.DueDate, 103) + N'. Vui lòng chuẩn bị kiểm định/bảo dưỡng.',
            'info',
            0,
            0,
            GETDATE()
        FROM ChecklistSchedules cs
        JOIN Inventory inv ON cs.InventoryId = inv.Id
        JOIN Items it ON inv.ItemId = it.Id
        WHERE cs.Status = 'pending' 
          AND cs.DueDate >= @Today 
          AND cs.DueDate <= DATEADD(DAY, ISNULL(@ChecklistDueThreshold, 3), @Today)
          AND NOT EXISTS (
              SELECT 1 FROM Alerts a 
              WHERE a.AlertRuleId = @ChecklistDueRuleId AND a.InventoryId = cs.InventoryId AND a.IsResolved = 0
          );
    END

    -- ── RULE 5: CONSUMABLES LOW ──
    DECLARE @ConsumablesRuleId INT, @ConsumablesActive BIT, @ConsumablesDesc NVARCHAR(MAX);
    DECLARE @PrinterMin INT, @UpsMin INT, @OfficeMin INT, @CdhaMin INT, @HsccMin INT, @PhongmoMin INT, @XetnghiemMin INT;
    
    SELECT 
        @ConsumablesRuleId = Id,
        @ConsumablesActive = IsActive,
        @ConsumablesDesc = Description,
        @PrinterMin = ThresholdCount,
        @UpsMin = ThresholdDays
    FROM AlertRules 
    WHERE Code = 'CONSUMABLES_LOW';
    
    IF @ConsumablesActive = 1
    BEGIN
        -- Default fallbacks
        SET @PrinterMin = ISNULL(@PrinterMin, 10);
        SET @UpsMin = ISNULL(@UpsMin, 5);
        SET @OfficeMin = 15;
        SET @CdhaMin = 20;
        SET @HsccMin = 8;
        SET @PhongmoMin = 12;
        SET @XetnghiemMin = 25;

        -- Extract from JSON Description if valid
        IF @ConsumablesDesc IS NOT NULL AND ISJSON(@ConsumablesDesc) = 1
        BEGIN
            DECLARE @JsonPrinter NVARCHAR(50) = JSON_VALUE(@ConsumablesDesc, '$.thresholds.PRINTER');
            DECLARE @JsonUps NVARCHAR(50) = JSON_VALUE(@ConsumablesDesc, '$.thresholds.UPS');
            DECLARE @JsonOffice NVARCHAR(50) = JSON_VALUE(@ConsumablesDesc, '$.thresholds.OFFICE');
            DECLARE @JsonCdha NVARCHAR(50) = JSON_VALUE(@ConsumablesDesc, '$.thresholds.CDHA');
            DECLARE @JsonHscc NVARCHAR(50) = JSON_VALUE(@ConsumablesDesc, '$.thresholds.HSCC');
            DECLARE @JsonPhongmo NVARCHAR(50) = JSON_VALUE(@ConsumablesDesc, '$.thresholds.PHONGMO');
            DECLARE @JsonXetnghiem NVARCHAR(50) = JSON_VALUE(@ConsumablesDesc, '$.thresholds.XETNGHIEM');

            IF @JsonPrinter IS NOT NULL SET @PrinterMin = TRY_CAST(@JsonPrinter AS INT);
            IF @JsonUps IS NOT NULL SET @UpsMin = TRY_CAST(@JsonUps AS INT);
            IF @JsonOffice IS NOT NULL SET @OfficeMin = TRY_CAST(@JsonOffice AS INT);
            IF @JsonCdha IS NOT NULL SET @CdhaMin = TRY_CAST(@JsonCdha AS INT);
            IF @JsonHscc IS NOT NULL SET @HsccMin = TRY_CAST(@JsonHscc AS INT);
            IF @JsonPhongmo IS NOT NULL SET @PhongmoMin = TRY_CAST(@JsonPhongmo AS INT);
            IF @JsonXetnghiem IS NOT NULL SET @XetnghiemMin = TRY_CAST(@JsonXetnghiem AS INT);
        END

        INSERT INTO Alerts (AlertRuleId, InventoryId, Title, Body, Severity, IsResolved, IsNotified, CreatedAt)
        SELECT
            @ConsumablesRuleId,
            inv.Id,
            N'Vật tư tiêu hao sắp hết',
            N'Lượng vật tư ' + it.Name + N' (Mã: ' + inv.AssetCode + N') trong kho đang dưới mức tối thiểu. Tồn kho hiện tại: ' + CAST(inv.Quantity AS NVARCHAR(10)) + N' bộ/cái (Mức tối thiểu quy định: ' + 
            CAST(
                CASE gr.Code 
                    WHEN 'PRINTER' THEN @PrinterMin
                    WHEN 'UPS' THEN @UpsMin
                    WHEN 'OFFICE' THEN @OfficeMin
                    WHEN 'CDHA' THEN @CdhaMin
                    WHEN 'HSCC' THEN @HsccMin
                    WHEN 'PHONGMO' THEN @PhongmoMin
                    WHEN 'XETNGHIEM' THEN @XetnghiemMin
                    ELSE 5
                END 
            AS NVARCHAR(10)) + N' bộ/cái/cuộn).',
            'info',
            0,
            0,
            GETDATE()
        FROM Inventory inv
        JOIN Items it ON inv.ItemId = it.Id
        JOIN Groups gr ON it.GroupId = gr.Id
        WHERE inv.LifeStatus = 'active' AND inv.ApprovalStatus = 'approved'
          AND (
              (gr.Code = 'PRINTER' AND inv.Quantity < @PrinterMin) OR
              (gr.Code = 'UPS' AND inv.Quantity < @UpsMin) OR
              (gr.Code = 'OFFICE' AND inv.Quantity < @OfficeMin) OR
              (gr.Code = 'CDHA' AND inv.Quantity < @CdhaMin) OR
              (gr.Code = 'HSCC' AND inv.Quantity < @HsccMin) OR
              (gr.Code = 'PHONGMO' AND inv.Quantity < @PhongmoMin) OR
              (gr.Code = 'XETNGHIEM' AND inv.Quantity < @XetnghiemMin)
          )
          AND NOT EXISTS (
              SELECT 1 FROM Alerts a 
              WHERE a.InventoryId = inv.Id AND a.Title = N'Vật tư tiêu hao sắp hết' AND a.IsResolved = 0
          );
    END

    -- ── RULE 6: DEPRECIATION END ──
    DECLARE @DeprecRuleId INT, @DeprecActive BIT, @DeprecThreshold INT;
    SELECT 
        @DeprecRuleId = Id, 
        @DeprecActive = IsActive, 
        @DeprecThreshold = ThresholdDays
    FROM AlertRules 
    WHERE Code = 'DEPRECIATION_END_30D';
    
    IF @DeprecActive = 1
    BEGIN
        INSERT INTO Alerts (AlertRuleId, InventoryId, Title, Body, Severity, IsResolved, IsNotified, CreatedAt)
        SELECT
            @DeprecRuleId,
            inv.Id,
            N'Thiết bị sắp hết khấu hao',
            N'Thiết bị y tế ' + it.Name + N' (Mã: ' + inv.AssetCode + N') sắp kết thúc chu kỳ khấu hao sau ' + 
                CAST(DATEDIFF(DAY, @Today, DATEADD(YEAR, ISNULL(inv.DepreciationYears, 5), inv.ImportDate)) AS NVARCHAR(10)) + 
                N' ngày (ngày hoàn thành khấu hao dự kiến: ' + 
                CONVERT(NVARCHAR(10), DATEADD(YEAR, ISNULL(inv.DepreciationYears, 5), inv.ImportDate), 103) + 
                N'). Khuyến nghị: Thực hiện kiểm định lại hiệu năng để đưa ra quyết định thanh lý hoặc tái cấu trúc sử dụng.',
            'info',
            0,
            0,
            GETDATE()
        FROM Inventory inv
        JOIN Items it ON inv.ItemId = it.Id
        WHERE inv.LifeStatus = 'active' 
          AND inv.ApprovalStatus = 'approved' 
          AND inv.DepreciationYears IS NOT NULL 
          AND inv.DepreciationYears > 0
          AND DATEADD(YEAR, inv.DepreciationYears, inv.ImportDate) >= @Today
          AND DATEADD(YEAR, inv.DepreciationYears, inv.ImportDate) <= DATEADD(DAY, ISNULL(@DeprecThreshold, 30), @Today)
          AND NOT EXISTS (
              SELECT 1 FROM Alerts a 
              WHERE a.AlertRuleId = @DeprecRuleId AND a.InventoryId = inv.Id AND a.IsResolved = 0
          );
    END
END;
GO

-- 3. CREATE PROCEDURE: sp_GetAlertList
CREATE PROCEDURE [dbo].[sp_GetAlertList]
    @Tab VARCHAR(20) = 'all'
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        a.Id,
        a.AlertRuleId,
        r.Code AS RuleCode,
        r.AlertType AS RuleType,
        a.InventoryId,
        inv.AssetCode,
        it.Name AS ItemName,
        it.Brand,
        it.Model,
        a.Title,
        a.Body,
        a.Severity,
        a.IsResolved,
        ISNULL(loc.Name, 'N/A') AS LocationName,
        ISNULL(dep.Name, 'N/A') AS DepartmentName,
        ISNULL(CONVERT(VARCHAR(10), inv.WarrantyExpiry, 103), '') AS WarrantyExpiryDate,
        a.CreatedAt
    FROM Alerts a
    JOIN AlertRules r ON a.AlertRuleId = r.Id
    JOIN Inventory inv ON a.InventoryId = inv.Id
    JOIN Items it ON inv.ItemId = it.Id
    LEFT JOIN Locations loc ON inv.LocationId = loc.Id
    LEFT JOIN Departments dep ON inv.DepartmentId = dep.Id
    WHERE a.IsResolved = 0
      AND (
          @Tab = 'all' OR
          (@Tab = 'danger' AND a.Severity = 'danger') OR
          (@Tab = 'warning' AND a.Severity = 'warning') OR
          (@Tab = 'info' AND a.Severity = 'info')
      )
    ORDER BY a.CreatedAt DESC;
END;
GO
