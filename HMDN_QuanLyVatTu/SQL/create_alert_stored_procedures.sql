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
    DECLARE @ChecklistRuleId INT, @ChecklistActive BIT;
    SELECT 
        @ChecklistRuleId = Id, 
        @ChecklistActive = IsActive
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
        WHERE cs.Status = 'pending' AND cs.DueDate < @Today
          AND NOT EXISTS (
              SELECT 1 FROM Alerts a 
              WHERE a.AlertRuleId = @ChecklistRuleId AND a.InventoryId = cs.InventoryId AND a.IsResolved = 0
          );
    END

    -- ── RULE 5: CONSUMABLES LOW ──
    DECLARE @ConsumablesRuleId INT;
    SELECT @ConsumablesRuleId = Id FROM AlertRules WHERE Code = 'CONSUMABLES_LOW';
    
    INSERT INTO Alerts (AlertRuleId, InventoryId, Title, Body, Severity, IsResolved, IsNotified, CreatedAt)
    SELECT
        ISNULL(@ConsumablesRuleId, 7),
        inv.Id,
        N'Vật tư tiêu hao sắp hết',
        N'Lượng vật tư ' + it.Name + N' (Mã: ' + inv.AssetCode + N') trong kho đang dưới mức tối thiểu. Tồn kho hiện tại: ' + CAST(inv.Quantity AS NVARCHAR(10)) + N' bộ (Mức tối thiểu quy định: ' + CAST(CASE gr.Code WHEN 'PRINTER' THEN 10 ELSE 5 END AS NVARCHAR(10)) + N' bộ).',
        'info',
        0,
        0,
        GETDATE()
    FROM Inventory inv
    JOIN Items it ON inv.ItemId = it.Id
    JOIN Groups gr ON it.GroupId = gr.Id
    WHERE inv.LifeStatus = 'active' AND inv.ApprovalStatus = 'approved'
      AND (
          (gr.Code = 'PRINTER' AND inv.Quantity < 10) OR
          (gr.Code = 'UPS' AND inv.Quantity < 5)
      )
      AND NOT EXISTS (
          SELECT 1 FROM Alerts a 
          WHERE a.InventoryId = inv.Id AND a.Title = N'Vật tư tiêu hao sắp hết' AND a.IsResolved = 0
      );
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
