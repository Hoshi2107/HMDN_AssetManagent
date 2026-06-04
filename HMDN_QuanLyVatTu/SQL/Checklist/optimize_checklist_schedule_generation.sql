USE HospitalAssetDB;
GO

-- ============================================================
-- SQL Script: Optimize Checklist Schedule Generation Rules
-- ============================================================

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_GenerateChecklistSchedules]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[sp_GenerateChecklistSchedules];
GO

CREATE PROCEDURE [dbo].[sp_GenerateChecklistSchedules]
    @FromDate DATE,
    @ToDate   DATE
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Clean up future pending schedules in the range to allow correct re-generation
    DELETE FROM ChecklistSchedules 
    WHERE ScheduledDate >= @FromDate 
      AND ScheduledDate <= @ToDate 
      AND Status = 'pending';

    -- CTE to find valid candidates and rank them chronologically by ScheduledDate
    WITH CandidateSchedules AS (
        SELECT
            inv.Id AS InventoryId,
            gen.ScheduledDate,
            cy.CycleType,
            CASE 
                WHEN LOWER(cy.CycleType) = 'daily' THEN gen.ScheduledDate
                WHEN LOWER(cy.CycleType) = 'weekly' THEN DATEADD(DAY, 2, gen.ScheduledDate)
                WHEN LOWER(cy.CycleType) = 'monthly' THEN DATEADD(DAY, 5, gen.ScheduledDate)
                WHEN LOWER(cy.CycleType) = 'quarterly' THEN DATEADD(DAY, 7, gen.ScheduledDate)
                WHEN LOWER(cy.CycleType) = 'yearly' THEN DATEADD(DAY, 15, gen.ScheduledDate)
                ELSE DATEADD(DAY, 3, gen.ScheduledDate)
            END AS DueDate,
            ROW_NUMBER() OVER (PARTITION BY inv.Id, cy.CycleType ORDER BY gen.ScheduledDate ASC) AS RowNum
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
          -- Date filter based on CycleType and RepeatOn
          AND (
              -- Daily: every day
              (LOWER(cy.CycleType) = 'daily')
              
              -- Weekly: match weekday index or name
              OR (
                  LOWER(cy.CycleType) = 'weekly' AND (
                      (cy.RepeatOn = 'Monday' AND ((DATEPART(dw, gen.ScheduledDate) + @@DATEFIRST - 1) % 7) + 1 = 2) OR
                      (cy.RepeatOn = 'Tuesday' AND ((DATEPART(dw, gen.ScheduledDate) + @@DATEFIRST - 1) % 7) + 1 = 3) OR
                      (cy.RepeatOn = 'Wednesday' AND ((DATEPART(dw, gen.ScheduledDate) + @@DATEFIRST - 1) % 7) + 1 = 4) OR
                      (cy.RepeatOn = 'Thursday' AND ((DATEPART(dw, gen.ScheduledDate) + @@DATEFIRST - 1) % 7) + 1 = 5) OR
                      (cy.RepeatOn = 'Friday' AND ((DATEPART(dw, gen.ScheduledDate) + @@DATEFIRST - 1) % 7) + 1 = 6) OR
                      (cy.RepeatOn = 'Saturday' AND ((DATEPART(dw, gen.ScheduledDate) + @@DATEFIRST - 1) % 7) + 1 = 7) OR
                      (cy.RepeatOn = 'Sunday' AND ((DATEPART(dw, gen.ScheduledDate) + @@DATEFIRST - 1) % 7) + 1 = 1) OR
                      (
                          LEFT(LTRIM(cy.RepeatOn), 1) = '[' AND 
                          ISJSON(cy.RepeatOn) = 1 AND 
                          EXISTS (
                              SELECT 1 FROM OPENJSON(CASE WHEN LEFT(LTRIM(cy.RepeatOn), 1) = '[' AND ISJSON(cy.RepeatOn) = 1 THEN cy.RepeatOn ELSE '[]' END) 
                              WHERE CAST(value AS INT) = ((DATEPART(dw, gen.ScheduledDate) + @@DATEFIRST - 1) % 7) + 1
                          )
                      ) OR
                      (
                          NOT (LEFT(LTRIM(cy.RepeatOn), 1) = '[') AND 
                          TRY_CAST(cy.RepeatOn AS INT) = ((DATEPART(dw, gen.ScheduledDate) + @@DATEFIRST - 1) % 7) + 1
                      )
                  )
              )
              
              -- Monthly: match day of month, excluding quarterly/semi-annual logic
              OR (
                  LOWER(cy.CycleType) = 'monthly' AND NOT (cy.Name LIKE N'%3 tháng%' OR cy.Name LIKE N'%6 tháng%') AND (
                      (
                          LEFT(LTRIM(cy.RepeatOn), 1) = '[' AND 
                          ISJSON(cy.RepeatOn) = 1 AND 
                          EXISTS (
                              SELECT 1 FROM OPENJSON(CASE WHEN LEFT(LTRIM(cy.RepeatOn), 1) = '[' AND ISJSON(cy.RepeatOn) = 1 THEN cy.RepeatOn ELSE '[]' END) 
                              WHERE CAST(value AS INT) = DAY(gen.ScheduledDate)
                          )
                      ) OR
                      (
                          NOT (LEFT(LTRIM(cy.RepeatOn), 1) = '[') AND 
                          TRY_CAST(cy.RepeatOn AS INT) = DAY(gen.ScheduledDate)
                      ) OR
                      (cy.RepeatOn IS NULL AND DAY(gen.ScheduledDate) = 1)
                  )
              )
              
              -- Quarterly / 3-Month / 6-Month: match month sequence
              OR (
                  (LOWER(cy.CycleType) = 'quarterly' OR cy.Name LIKE N'%3 tháng%' OR cy.Name LIKE N'%6 tháng%') AND (
                      -- For 3-month (quarterly)
                      (
                          (LOWER(cy.CycleType) = 'quarterly' OR cy.Name LIKE N'%3 tháng%') AND 
                          (MONTH(gen.ScheduledDate) - 1) % 3 = 0 AND 
                          DAY(gen.ScheduledDate) = ISNULL(TRY_CAST(cy.RepeatOn AS INT), 15)
                      )
                      -- For 6-month (semi-annual)
                      OR (
                          cy.Name LIKE N'%6 tháng%' AND 
                          (MONTH(gen.ScheduledDate) - 3) % 6 = 0 AND 
                          DAY(gen.ScheduledDate) = ISNULL(TRY_CAST(cy.RepeatOn AS INT), 1)
                      )
                      -- Fallback if RepeatOn is JSON array of months
                      OR (
                          LEFT(LTRIM(cy.RepeatOn), 1) = '[' AND 
                          ISJSON(cy.RepeatOn) = 1 AND 
                          EXISTS (
                              SELECT 1 FROM OPENJSON(CASE WHEN LEFT(LTRIM(cy.RepeatOn), 1) = '[' AND ISJSON(cy.RepeatOn) = 1 THEN cy.RepeatOn ELSE '[]' END) 
                              WHERE CAST(value AS INT) = MONTH(gen.ScheduledDate)
                          ) AND 
                          DAY(gen.ScheduledDate) = 1
                      )
                  )
              )
              
              -- Yearly: Month 1, Day specified or Day 1
              OR (
                  LOWER(cy.CycleType) = 'yearly' AND (
                      MONTH(gen.ScheduledDate) = 1 AND 
                      DAY(gen.ScheduledDate) = ISNULL(TRY_CAST(cy.RepeatOn AS INT), 1)
                  )
              )
          )
          -- Avoid duplicating schedules
          AND NOT EXISTS (
              SELECT 1 FROM ChecklistSchedules cs2
              WHERE cs2.InventoryId   = inv.Id
                AND cs2.ScheduledDate = gen.ScheduledDate
          )
          -- Do not generate next scheduled task if there is already an unresolved or overdue one
          AND NOT EXISTS (
              SELECT 1 FROM ChecklistSchedules cs3
              WHERE cs3.InventoryId = inv.Id
                AND LOWER(cs3.CycleType) = LOWER(cy.CycleType)
                AND cs3.Status IN ('pending', 'overdue')
          )
    )
    INSERT INTO ChecklistSchedules (InventoryId, ScheduledDate, CycleType, DueDate, Status)
    SELECT InventoryId, ScheduledDate, CycleType, DueDate, 'pending'
    FROM CandidateSchedules
    WHERE RowNum = 1;
END;
GO

PRINT 'Stored procedure sp_GenerateChecklistSchedules optimized successfully.';
