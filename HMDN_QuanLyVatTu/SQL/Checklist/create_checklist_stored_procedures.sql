-- ============================================================
-- SQL Script: Create Stored Procedures for Checklist Module
-- Version 3: Safe bounded tally (no overflow)
-- ============================================================

USE HospitalAssetDB;
GO

-- 1. DROP EXISTING PROCEDURES IF THEY EXIST
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_GetChecklistForInventory]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[sp_GetChecklistForInventory];
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_GenerateChecklistSchedules]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[sp_GenerateChecklistSchedules];
GO

-- 2. CREATE PROCEDURE: sp_GetChecklistForInventory
CREATE PROCEDURE [dbo].[sp_GetChecklistForInventory]
    @InventoryId INT,
    @CycleType   VARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ItemId INT, @GroupId INT;
    
    SELECT @ItemId = inv.ItemId, @GroupId = gr.Id
    FROM Inventory inv
        JOIN Items  it ON inv.ItemId = it.Id
        JOIN Groups gr ON it.GroupId = gr.Id
    WHERE inv.Id = @InventoryId;

    SELECT cd.*
    FROM ChecklistDefinitions cd
    WHERE cd.IsActive = 1
      AND (cd.CycleType IS NULL OR @CycleType IS NULL OR cd.CycleType = @CycleType)
      AND (
            (cd.Scope = 'global')
         OR (cd.Scope = 'group' AND cd.GroupId = @GroupId)
         OR (cd.Scope = 'item'  AND cd.ItemId  = @ItemId)
          )
    ORDER BY cd.Scope, cd.SortOrder;
END;
GO

-- 3. CREATE PROCEDURE: sp_GenerateChecklistSchedules
-- Safe version: uses a bounded numbers table (max 366 rows) to avoid overflow.
-- For monthly/quarterly/yearly cycles, uses a small loop approach.
CREATE PROCEDURE [dbo].[sp_GenerateChecklistSchedules]
    @FromDate DATE,
    @ToDate   DATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Safety check
    IF @FromDate IS NULL OR @ToDate IS NULL OR @FromDate > @ToDate
    BEGIN
        RAISERROR('Invalid date range.', 16, 1);
        RETURN;
    END;

    -- Step 1: Build a bounded numbers table (0..365, enough for 1 year of daily)
    DECLARE @Numbers TABLE (N INT PRIMARY KEY);
    DECLARE @i INT = 0;
    DECLARE @maxDays INT = DATEDIFF(DAY, @FromDate, @ToDate);
    IF @maxDays > 366 SET @maxDays = 366;  -- cap at 1 year
    
    WHILE @i <= @maxDays
    BEGIN
        INSERT INTO @Numbers (N) VALUES (@i);
        SET @i = @i + 1;
    END;

    -- Step 2: Build a temp table of all candidate (InventoryId, ScheduledDate, CycleType) pairs
    DECLARE @Candidates TABLE (
        InventoryId INT,
        ScheduledDate DATE,
        CycleType VARCHAR(20)
    );

    -- DAILY: one row per day in range
    INSERT INTO @Candidates (InventoryId, ScheduledDate, CycleType)
    SELECT inv.Id, DATEADD(DAY, n.N, @FromDate), cy.CycleType
    FROM Inventory inv
        JOIN CheckCycles cy ON inv.CheckCycleId = cy.Id
        CROSS JOIN @Numbers n
    WHERE inv.LifeStatus = 'active'
      AND inv.ApprovalStatus = 'approved'
      AND cy.CycleType = 'daily'
      AND DATEADD(DAY, n.N, @FromDate) <= @ToDate;

    -- WEEKLY: every Monday in range
    INSERT INTO @Candidates (InventoryId, ScheduledDate, CycleType)
    SELECT inv.Id, DATEADD(DAY, n.N, @FromDate), cy.CycleType
    FROM Inventory inv
        JOIN CheckCycles cy ON inv.CheckCycleId = cy.Id
        CROSS JOIN @Numbers n
    WHERE inv.LifeStatus = 'active'
      AND inv.ApprovalStatus = 'approved'
      AND cy.CycleType = 'weekly'
      AND DATEADD(DAY, n.N, @FromDate) <= @ToDate
      AND DATEPART(WEEKDAY, DATEADD(DAY, n.N, @FromDate)) = 
          CASE @@DATEFIRST WHEN 1 THEN 1 WHEN 7 THEN 2 ELSE 2 END;  -- Monday

    -- MONTHLY: 1st of each month in range
    INSERT INTO @Candidates (InventoryId, ScheduledDate, CycleType)
    SELECT inv.Id, candidate, cy.CycleType
    FROM Inventory inv
        JOIN CheckCycles cy ON inv.CheckCycleId = cy.Id
        CROSS APPLY (
            SELECT DATEADD(MONTH, n.N, DATEFROMPARTS(YEAR(@FromDate), MONTH(@FromDate), 1)) AS candidate
            FROM @Numbers n
            WHERE n.N <= 12
              AND DATEADD(MONTH, n.N, DATEFROMPARTS(YEAR(@FromDate), MONTH(@FromDate), 1)) >= @FromDate
              AND DATEADD(MONTH, n.N, DATEFROMPARTS(YEAR(@FromDate), MONTH(@FromDate), 1)) <= @ToDate
        ) dates
    WHERE inv.LifeStatus = 'active'
      AND inv.ApprovalStatus = 'approved'
      AND cy.CycleType = 'monthly';

    -- QUARTERLY: 1st of each quarter start month in range
    INSERT INTO @Candidates (InventoryId, ScheduledDate, CycleType)
    SELECT inv.Id, candidate, cy.CycleType
    FROM Inventory inv
        JOIN CheckCycles cy ON inv.CheckCycleId = cy.Id
        CROSS APPLY (
            SELECT DATEADD(QUARTER, n.N, 
                   DATEFROMPARTS(YEAR(@FromDate), (((MONTH(@FromDate)-1)/3)*3)+1, 1)) AS candidate
            FROM @Numbers n
            WHERE n.N <= 4
              AND DATEADD(QUARTER, n.N, 
                  DATEFROMPARTS(YEAR(@FromDate), (((MONTH(@FromDate)-1)/3)*3)+1, 1)) >= @FromDate
              AND DATEADD(QUARTER, n.N, 
                  DATEFROMPARTS(YEAR(@FromDate), (((MONTH(@FromDate)-1)/3)*3)+1, 1)) <= @ToDate
        ) dates
    WHERE inv.LifeStatus = 'active'
      AND inv.ApprovalStatus = 'approved'
      AND cy.CycleType = 'quarterly';

    -- YEARLY: Jan 1st of each year in range
    INSERT INTO @Candidates (InventoryId, ScheduledDate, CycleType)
    SELECT inv.Id, candidate, cy.CycleType
    FROM Inventory inv
        JOIN CheckCycles cy ON inv.CheckCycleId = cy.Id
        CROSS APPLY (
            SELECT DATEFROMPARTS(YEAR(@FromDate) + n.N, 1, 1) AS candidate
            FROM @Numbers n
            WHERE n.N <= 2
              AND DATEFROMPARTS(YEAR(@FromDate) + n.N, 1, 1) >= @FromDate
              AND DATEFROMPARTS(YEAR(@FromDate) + n.N, 1, 1) <= @ToDate
        ) dates
    WHERE inv.LifeStatus = 'active'
      AND inv.ApprovalStatus = 'approved'
      AND cy.CycleType = 'yearly';

    -- Step 3: INSERT into ChecklistSchedules, avoiding duplicates
    INSERT INTO ChecklistSchedules (InventoryId, ScheduledDate, CycleType, DueDate, Status)
    SELECT 
        c.InventoryId,
        c.ScheduledDate,
        c.CycleType,
        DATEADD(DAY,
            CASE c.CycleType
                WHEN 'daily'     THEN 1
                WHEN 'weekly'    THEN 2
                WHEN 'monthly'   THEN 5
                WHEN 'quarterly' THEN 7
                WHEN 'yearly'    THEN 14
                ELSE 3
            END, c.ScheduledDate) AS DueDate,
        'pending'
    FROM @Candidates c
    WHERE NOT EXISTS (
        SELECT 1 FROM ChecklistSchedules cs2
        WHERE cs2.InventoryId   = c.InventoryId
          AND cs2.CycleType     = c.CycleType
          AND cs2.ScheduledDate = c.ScheduledDate
    );

    SELECT @@ROWCOUNT AS GeneratedCount;
END;
GO

PRINT 'Checklist stored procedures created successfully (V3 - safe bounded).';
