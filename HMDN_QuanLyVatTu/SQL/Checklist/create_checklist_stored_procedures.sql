-- ============================================================
-- SQL Script: Create Stored Procedures for Checklist Module
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
-- Description: Retrieves all applicable checklist definitions for a given asset (merging global, group, and item-specific definition scopes).
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
-- Description: Generates checklist schedules for active and approved assets based on their check cycles for a specific date range.
CREATE PROCEDURE [dbo].[sp_GenerateChecklistSchedules]
    @FromDate DATE,
    @ToDate   DATE
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Only generate for active, approved assets that have check cycles defined
    INSERT INTO ChecklistSchedules (InventoryId, ScheduledDate, CycleType, DueDate, Status)
    SELECT
        inv.Id,
        gen.ScheduledDate,
        cy.CycleType,
        DATEADD(DAY, 3, gen.ScheduledDate) AS DueDate,  -- Due date is scheduled date + 3 days
        'pending'
    FROM Inventory inv
        JOIN CheckCycles cy ON inv.CheckCycleId = cy.Id
        -- Generate daily sequences in range using tally table
        CROSS APPLY (
            SELECT DATEADD(DAY, n.N, @FromDate) AS ScheduledDate
            FROM (
                SELECT ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS N
                FROM sys.all_objects
            ) n
            WHERE DATEADD(DAY, n.N, @FromDate) <= @ToDate
        ) gen
    WHERE inv.LifeStatus = 'active'
      AND inv.ApprovalStatus = 'approved'
      -- Avoid duplicating schedules
      AND NOT EXISTS (
          SELECT 1 FROM ChecklistSchedules cs2
          WHERE cs2.InventoryId   = inv.Id
            AND cs2.ScheduledDate = gen.ScheduledDate
      );
END;
GO

PRINT 'Checklist stored procedures created successfully.';
