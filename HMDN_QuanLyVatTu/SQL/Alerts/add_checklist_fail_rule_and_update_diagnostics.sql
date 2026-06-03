USE HospitalAssetDB;
GO

-- 1. Seed CHECKLIST_FAIL alert rule if not exists
IF NOT EXISTS (SELECT 1 FROM AlertRules WHERE Code = 'CHECKLIST_FAIL')
BEGIN
    INSERT INTO AlertRules (Code, Name, AlertType, ThresholdDays, ThresholdCount, ThresholdPeriodDays, IsActive, Description) VALUES
    ('CHECKLIST_FAIL', N'Kiểm tra không đạt chuẩn', 'checklist_fail', NULL, NULL, NULL, 1, N'{"text": "Cảnh báo khi kết quả thực hiện checklist của thiết bị ghi nhận lỗi/hỏng (không đạt).", "overrides": []}');
    PRINT 'Seeded CHECKLIST_FAIL alert rule successfully.';
END
ELSE
BEGIN
    PRINT 'CHECKLIST_FAIL alert rule already exists.';
END
GO

-- 2. Drop existing sp_RunAlertDiagnostics
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_RunAlertDiagnostics]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[sp_RunAlertDiagnostics];
GO

-- 3. Create updated sp_RunAlertDiagnostics
CREATE PROCEDURE [dbo].[sp_RunAlertDiagnostics]
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @Today DATE = CAST(GETDATE() AS DATE);
    
    -- ── AUTO-RESOLVE COMPLETED CHECKLIST ALERTS ──
    -- Resolve CHECKLIST_OVERDUE if there are no pending schedules with DueDate <= @Today
    UPDATE a
    SET a.IsResolved = 1,
        a.ResolvedAt = GETDATE(),
        a.ResolvedBy = 1 -- System
    FROM Alerts a
    JOIN AlertRules r ON a.AlertRuleId = r.Id
    WHERE r.Code = 'CHECKLIST_OVERDUE'
      AND a.IsResolved = 0
      AND NOT EXISTS (
          SELECT 1 FROM ChecklistSchedules cs 
          WHERE cs.InventoryId = a.InventoryId 
            AND cs.Status = 'pending'
            AND cs.DueDate <= @Today
      );

    -- Resolve CHECKLIST_DUE_3D if there are no pending schedules currently due
    UPDATE a
    SET a.IsResolved = 1,
        a.ResolvedAt = GETDATE(),
        a.ResolvedBy = 1 -- System
    FROM Alerts a
    JOIN AlertRules r ON a.AlertRuleId = r.Id
    WHERE r.Code = 'CHECKLIST_DUE_3D'
      AND a.IsResolved = 0
      AND NOT EXISTS (
          SELECT 1 FROM ChecklistSchedules cs 
          WHERE cs.InventoryId = a.InventoryId 
            AND cs.Status = 'pending'
            AND (
                (cs.CycleType IN ('daily', 'Daily') AND cs.DueDate = @Today) OR
                (cs.CycleType IN ('weekly', 'Weekly') AND cs.DueDate >= @Today AND cs.DueDate <= DATEADD(DAY, 1, @Today)) OR
                (cs.CycleType IN ('monthly', 'Monthly', 'quarterly', 'Quarterly') AND cs.DueDate >= @Today AND cs.DueDate <= DATEADD(DAY, 3, @Today)) OR
                (cs.CycleType IN ('yearly', 'Yearly') AND cs.DueDate >= @Today AND cs.DueDate <= DATEADD(DAY, 7, @Today))
            )
      );

    -- Resolve CHECKLIST_FAIL if the most recent checklist log for this asset is a 'pass'
    UPDATE a
    SET a.IsResolved = 1,
        a.ResolvedAt = GETDATE(),
        a.ResolvedBy = 1 -- System
    FROM Alerts a
    JOIN AlertRules r ON a.AlertRuleId = r.Id
    WHERE r.Code = 'CHECKLIST_FAIL'
      AND a.IsResolved = 0
      AND EXISTS (
          SELECT 1 FROM (
              SELECT InventoryId, CheckedAt, OverallResult,
                     ROW_NUMBER() OVER (PARTITION BY InventoryId ORDER BY CheckedAt DESC) as rn
              FROM ChecklistLogs
          ) cl
          WHERE cl.InventoryId = a.InventoryId
            AND cl.rn = 1
            AND cl.OverallResult = 'pass'
      );

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

    -- ── RULE 4B: CHECKLIST DUE (CYCLE-AWARE WARNINGS) ──
    DECLARE @ChecklistDueRuleId INT, @ChecklistDueActive BIT;
    SELECT 
        @ChecklistDueRuleId = Id, 
        @ChecklistDueActive = IsActive
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
          AND (
              -- Daily: Warn on the exact same day
              (cs.CycleType IN ('daily', 'Daily') AND cs.DueDate = @Today)
              
              -- Weekly: Warn 1 day in advance
              OR (cs.CycleType IN ('weekly', 'Weekly') AND cs.DueDate >= @Today AND cs.DueDate <= DATEADD(DAY, 1, @Today))
              
              -- Monthly/Quarterly: Warn 3 days in advance
              OR (cs.CycleType IN ('monthly', 'Monthly', 'quarterly', 'Quarterly') AND cs.DueDate >= @Today AND cs.DueDate <= DATEADD(DAY, 3, @Today))
              
              -- Yearly: Warn 7 days in advance
              OR (cs.CycleType IN ('yearly', 'Yearly') AND cs.DueDate >= @Today AND cs.DueDate <= DATEADD(DAY, 7, @Today))
          )
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
              WHERE a.InventoryId = inv.Id AND a.IsResolved = 0
          );
    END

    -- ── RULE 7: CHECKLIST FAIL ──
    DECLARE @ChecklistFailRuleId INT, @ChecklistFailActive BIT;
    SELECT 
        @ChecklistFailRuleId = Id, 
        @ChecklistFailActive = IsActive
    FROM AlertRules 
    WHERE Code = 'CHECKLIST_FAIL';
    
    IF @ChecklistFailActive = 1
    BEGIN
        INSERT INTO Alerts (AlertRuleId, InventoryId, Title, Body, Severity, IsResolved, IsNotified, CreatedAt)
        SELECT
            @ChecklistFailRuleId,
            cl.InventoryId,
            N'Kiểm tra định kỳ không đạt chuẩn',
            N'Thiết bị ' + it.Name + N' (Mã: ' + inv.AssetCode + N') vừa được kiểm tra checklist định kỳ vào lúc ' + 
                CONVERT(NVARCHAR(16), cl.CheckedAt, 120) + N' bởi nhân viên ' + ISNULL(u.FullName, N'Kỹ thuật') + 
                N' và cho kết quả KHÔNG ĐẠT CHUẨN (Lỗi/Hỏng). Chi tiết ghi chú: ' + ISNULL(cl.Note, N'Không có') + N'. Khuyến nghị: Chuyển ngay thiết bị sang trạng thái sửa chữa.',
            'danger',
            0,
            0,
            cl.CheckedAt
        FROM ChecklistLogs cl
        JOIN Inventory inv ON cl.InventoryId = inv.Id
        JOIN Items it ON inv.ItemId = it.Id
        LEFT JOIN Users u ON cl.CheckedBy = u.Id
        WHERE cl.OverallResult = 'fail'
          AND NOT EXISTS (
              SELECT 1 FROM Alerts a 
              WHERE a.AlertRuleId = @ChecklistFailRuleId AND a.InventoryId = cl.InventoryId AND a.IsResolved = 0
          )
          -- Only create alerts for failures that occurred after the last resolved alert of this type
          AND cl.CheckedAt > ISNULL((
              SELECT MAX(a2.ResolvedAt) 
              FROM Alerts a2 
              WHERE a2.AlertRuleId = @ChecklistFailRuleId AND a2.InventoryId = cl.InventoryId AND a2.IsResolved = 1
          ), '1900-01-01');
    END
END;
GO

PRINT 'sp_RunAlertDiagnostics updated successfully with CHECKLIST_FAIL alert rule checking.';
