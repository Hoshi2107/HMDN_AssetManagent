USE HospitalAssetDB;
GO

-- 1. DROP EXISTING PROCEDURES IF THEY EXIST
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_RunAlertDiagnostics]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[sp_RunAlertDiagnostics];
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_GetAlertList]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[sp_GetAlertList];
GO

-- 2. CREATE PROCEDURE: sp_RunAlertDiagnostics (With Performance Optimization and Strict Aggregation)
CREATE PROCEDURE [dbo].[sp_RunAlertDiagnostics]
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @Today DATE = CAST(GETDATE() AS DATE);
    
    -- ── AUTO-PURGE RETENTION POLICY FOR "INFO" ALERTS ──
    -- Hard-delete any alert in the "info" category older than 7 days (resolved or unresolved)
    DELETE FROM Alerts 
    WHERE Severity = 'info' 
      AND CreatedAt < DATEADD(DAY, -7, GETDATE());

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

        -- Auto-Dismiss Superseded Reminders for these devices:
        -- Automatically mark all older, unhandled alerts of the same type for these devices as Resolved
        UPDATE a
        SET a.IsResolved = 1,
            a.ResolvedAt = GETDATE(),
            a.ResolvedBy = 1
        FROM Alerts a
        JOIN #FaultyDevices fd ON a.InventoryId = fd.InventoryId
        WHERE a.AlertRuleId = @MultiFaultRuleId AND a.IsResolved = 0;
        
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

    -- ── RULE 4: CHECKLIST OVERDUE (WITH STRICT CYCLETYPE-ONLY AGGREGATION) ──
    DECLARE @ChecklistRuleId INT, @ChecklistActive BIT, @ChecklistThreshold INT;
    SELECT 
        @ChecklistRuleId = Id, 
        @ChecklistActive = IsActive,
        @ChecklistThreshold = ThresholdDays
    FROM AlertRules 
    WHERE Code = 'CHECKLIST_OVERDUE';
    
    IF @ChecklistActive = 1
    BEGIN
        -- Find the most recent overdue pending schedule per device
        SELECT cs.InventoryId, cs.CycleType, cs.DueDate
        INTO #LatestOverdue
        FROM (
            SELECT InventoryId, CycleType, DueDate,
                   ROW_NUMBER() OVER (PARTITION BY InventoryId ORDER BY DueDate DESC) as rn
            FROM ChecklistSchedules
            WHERE Status = 'pending' 
              AND DueDate <= DATEADD(DAY, -ISNULL(@ChecklistThreshold, 0), @Today)
        ) cs
        WHERE cs.rn = 1;

        -- Auto-Dismiss Superseded Reminders for these devices:
        -- Automatically mark all older, unhandled alerts of the same type for these devices as Resolved
        UPDATE a
        SET a.IsResolved = 1,
            a.ResolvedAt = GETDATE(),
            a.ResolvedBy = 1
        FROM Alerts a
        JOIN #LatestOverdue lo ON a.InventoryId = lo.InventoryId
        WHERE a.AlertRuleId = @ChecklistRuleId
          AND a.IsResolved = 0;

        -- Strictly aggregate by CycleType globally across all groups/devices
        SELECT 
            lo.CycleType,
            MIN(lo.DueDate) AS MinDueDate,
            COUNT(*) AS OverdueCount,
            MIN(lo.InventoryId) AS RepInventoryId
        INTO #AggregatedOverdue
        FROM #LatestOverdue lo
        GROUP BY lo.CycleType;

        -- Insert aggregated alerts for count > 1 (e.g., "Quá hạn checklist bảo trì (Hàng tháng) - 100 thiết bị")
        INSERT INTO Alerts (AlertRuleId, InventoryId, Title, Body, Severity, IsResolved, IsNotified, CreatedAt)
        SELECT
            @ChecklistRuleId,
            ao.RepInventoryId,
            N'Quá hạn checklist bảo trì (' + 
                CASE ao.CycleType 
                    WHEN 'monthly' THEN N'Hàng tháng'
                    WHEN 'yearly' THEN N'Hàng năm'
                    WHEN 'weekly' THEN N'Hàng tuần'
                    ELSE N'Hàng ngày' 
                END + N')',
            N'Hiện tại có ' + CAST(ao.OverdueCount AS NVARCHAR(10)) + N' thiết bị đang quá hạn checklist bảo trì định kỳ ' + 
                CASE ao.CycleType 
                    WHEN 'monthly' THEN N'hàng tháng'
                    WHEN 'yearly' THEN N'hàng năm'
                    WHEN 'weekly' THEN N'hàng tuần'
                    ELSE N'hàng ngày' 
                END + N' từ ngày ' + CONVERT(NVARCHAR(10), ao.MinDueDate, 103) + N'. Vui lòng kiểm tra và thực hiện.',
            'info',
            0,
            0,
            GETDATE()
        FROM #AggregatedOverdue ao
        WHERE ao.OverdueCount > 1
          AND NOT EXISTS (
              SELECT 1 FROM Alerts a 
              WHERE a.AlertRuleId = @ChecklistRuleId AND a.IsResolved = 0 
                AND a.Title LIKE N'Quá hạn checklist bảo trì (' + 
                    CASE ao.CycleType 
                        WHEN 'monthly' THEN N'Hàng tháng'
                        WHEN 'yearly' THEN N'Hàng năm'
                        WHEN 'weekly' THEN N'Hàng tuần'
                        ELSE N'Hàng ngày' 
                    END + N')%'
          );

        -- Insert detailed alerts for count = 1
        INSERT INTO Alerts (AlertRuleId, InventoryId, Title, Body, Severity, IsResolved, IsNotified, CreatedAt)
        SELECT
            @ChecklistRuleId,
            ao.RepInventoryId,
            N'Quá hạn checklist bảo trì',
            N'Lịch bảo trì định kỳ ' + 
                CASE ao.CycleType 
                    WHEN 'monthly' THEN N'hàng tháng'
                    WHEN 'yearly' THEN N'hàng năm'
                    WHEN 'weekly' THEN N'hàng tuần'
                    ELSE N'hàng ngày' 
                END + N' của thiết bị ' + it.Name + N' (Mã: ' + inv.AssetCode + N') đã quá hạn từ ngày ' + CONVERT(NVARCHAR(10), ao.MinDueDate, 103) + N'. Yêu cầu bộ phận kỹ thuật thực hiện kiểm tra khẩn cấp.',
            'info',
            0,
            0,
            GETDATE()
        FROM #AggregatedOverdue ao
        JOIN Inventory inv ON ao.RepInventoryId = inv.Id
        JOIN Items it ON inv.ItemId = it.Id
        WHERE ao.OverdueCount = 1
          AND NOT EXISTS (
              SELECT 1 FROM Alerts a 
              WHERE a.AlertRuleId = @ChecklistRuleId AND a.InventoryId = ao.RepInventoryId AND a.IsResolved = 0
          );

        DROP TABLE #LatestOverdue;
        DROP TABLE #AggregatedOverdue;
    END

    -- ── RULE 4B: CHECKLIST DUE (CYCLE-AWARE WARNINGS & STRICT AGGREGATION) ──
    DECLARE @ChecklistDueRuleId INT, @ChecklistDueActive BIT;
    SELECT 
        @ChecklistDueRuleId = Id, 
        @ChecklistDueActive = IsActive
    FROM AlertRules 
    WHERE Code = 'CHECKLIST_DUE_3D';
    
    IF @ChecklistDueActive = 1
    BEGIN
        -- Find the most imminent pending schedule per device
        SELECT cs.InventoryId, cs.CycleType, cs.DueDate
        INTO #LatestDue
        FROM (
            SELECT InventoryId, CycleType, DueDate,
                   ROW_NUMBER() OVER (PARTITION BY InventoryId ORDER BY DueDate ASC) as rn
            FROM ChecklistSchedules
            WHERE Status = 'pending' 
              AND (
                  -- Daily: Warn on the exact same day
                  (CycleType IN ('daily', 'Daily') AND DueDate = @Today)
                  
                  -- Weekly: Warn 1 day in advance
                  OR (CycleType IN ('weekly', 'Weekly') AND DueDate >= @Today AND DueDate <= DATEADD(DAY, 1, @Today))
                  
                  -- Monthly/Quarterly: Warn 3 days in advance
                  OR (CycleType IN ('monthly', 'Monthly', 'quarterly', 'Quarterly') AND DueDate >= @Today AND DueDate <= DATEADD(DAY, 3, @Today))
                  
                  -- Yearly: Warn 7 days in advance
                  OR (CycleType IN ('yearly', 'Yearly') AND DueDate >= @Today AND DueDate <= DATEADD(DAY, 7, @Today))
              )
        ) cs
        WHERE cs.rn = 1;

        -- Auto-Dismiss Superseded Reminders for these devices:
        -- Automatically mark all older, unhandled alerts of the same type for these devices as Resolved
        UPDATE a
        SET a.IsResolved = 1,
            a.ResolvedAt = GETDATE(),
            a.ResolvedBy = 1
        FROM Alerts a
        JOIN #LatestDue ld ON a.InventoryId = ld.InventoryId
        WHERE a.AlertRuleId = @ChecklistDueRuleId
          AND a.IsResolved = 0;

        -- Strictly aggregate by CycleType globally across all groups/devices
        SELECT 
            ld.CycleType,
            MIN(ld.DueDate) AS MinDueDate,
            COUNT(*) AS DueCount,
            MIN(ld.InventoryId) AS RepInventoryId
        INTO #AggregatedDue
        FROM #LatestDue ld
        GROUP BY ld.CycleType;

        -- Insert aggregated alerts for count > 1
        INSERT INTO Alerts (AlertRuleId, InventoryId, Title, Body, Severity, IsResolved, IsNotified, CreatedAt)
        SELECT
            @ChecklistDueRuleId,
            ad.RepInventoryId,
            N'Lịch kiểm tra sắp đến hạn (' + 
                CASE ad.CycleType 
                    WHEN 'monthly' THEN N'Hàng tháng'
                    WHEN 'yearly' THEN N'Hàng năm'
                    WHEN 'weekly' THEN N'Hàng tuần'
                    ELSE N'Hàng ngày' 
                END + N')',
            N'Có ' + CAST(ad.DueCount AS NVARCHAR(10)) + N' thiết bị sắp đến hạn kiểm tra/bảo trì định kỳ ' + 
                CASE ad.CycleType 
                    WHEN 'monthly' THEN N'hàng tháng'
                    WHEN 'yearly' THEN N'hàng năm'
                    WHEN 'weekly' THEN N'hàng tuần'
                    ELSE N'hàng ngày' 
                END + N' vào ngày ' + CONVERT(NVARCHAR(10), ad.MinDueDate, 103) + N'. Vui lòng chuẩn bị kiểm định/bảo dưỡng.',
            'info',
            0,
            0,
            GETDATE()
        FROM #AggregatedDue ad
        WHERE ad.DueCount > 1
          AND NOT EXISTS (
              SELECT 1 FROM Alerts a 
              WHERE a.AlertRuleId = @ChecklistDueRuleId AND a.IsResolved = 0
                AND a.Title LIKE N'Lịch kiểm tra sắp đến hạn (' + 
                    CASE ad.CycleType 
                        WHEN 'monthly' THEN N'Hàng tháng'
                        WHEN 'yearly' THEN N'Hàng năm'
                        WHEN 'weekly' THEN N'Hàng tuần'
                        ELSE N'Hàng ngày' 
                    END + N')%'
          );

        -- Insert detailed alerts for count = 1
        INSERT INTO Alerts (AlertRuleId, InventoryId, Title, Body, Severity, IsResolved, IsNotified, CreatedAt)
        SELECT
            @ChecklistDueRuleId,
            ad.RepInventoryId,
            N'Lịch kiểm tra sắp đến hạn',
            N'Lịch kiểm tra/bảo trì định kỳ ' + 
                CASE ad.CycleType 
                    WHEN 'monthly' THEN N'hàng tháng'
                    WHEN 'yearly' THEN N'hàng năm'
                    WHEN 'weekly' THEN N'hàng tuần'
                    ELSE N'hàng ngày' 
                END + N' của thiết bị ' + it.Name + N' (Mã: ' + inv.AssetCode + N') sắp đến hạn vào ngày ' + CONVERT(NVARCHAR(10), ad.MinDueDate, 103) + N'. Vui lòng chuẩn bị kiểm định/bảo dưỡng.',
            'info',
            0,
            0,
            GETDATE()
        FROM #AggregatedDue ad
        JOIN Inventory inv ON ad.RepInventoryId = inv.Id
        JOIN Items it ON inv.ItemId = it.Id
        WHERE ad.DueCount = 1
          AND NOT EXISTS (
              SELECT 1 FROM Alerts a 
              WHERE a.AlertRuleId = @ChecklistDueRuleId AND a.InventoryId = ad.RepInventoryId AND a.IsResolved = 0
          );

        DROP TABLE #LatestDue;
        DROP TABLE #AggregatedDue;
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

    -- ── RULE 7: CHECKLIST FAIL ──
    DECLARE @ChecklistFailRuleId INT, @ChecklistFailActive BIT;
    SELECT 
        @ChecklistFailRuleId = Id, 
        @ChecklistFailActive = IsActive
    FROM AlertRules 
    WHERE Code = 'CHECKLIST_FAIL';
    
    IF @ChecklistFailActive = 1
    BEGIN
        -- Auto-Dismiss Superseded Reminders of CHECKLIST_FAIL for the same devices
        UPDATE a
        SET a.IsResolved = 1,
            a.ResolvedAt = GETDATE(),
            a.ResolvedBy = 1
        FROM Alerts a
        JOIN (
            SELECT DISTINCT InventoryId FROM ChecklistLogs WHERE OverallResult = 'fail'
        ) cl ON a.InventoryId = cl.InventoryId
        WHERE a.AlertRuleId = @ChecklistFailRuleId AND a.IsResolved = 0;

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

-- 3. CREATE PROCEDURE: sp_GetAlertList (With Server-Side Pagination support)
CREATE PROCEDURE [dbo].[sp_GetAlertList]
    @Tab VARCHAR(20) = 'all',
    @Page INT = 1,
    @PageSize INT = 20
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Offset INT = (@Page - 1) * @PageSize;

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
    ORDER BY a.CreatedAt DESC
    OFFSET @Offset ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END;
GO

PRINT 'SQL Migration completed: Stored procedures created/updated successfully.';
