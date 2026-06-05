USE HospitalAssetDB;
GO

-- ALTER PROCEDURE: sp_GetInventoryReport
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_GetInventoryReport]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[sp_GetInventoryReport];
GO

CREATE PROCEDURE [dbo].[sp_GetInventoryReport]
    @DepartmentId INT = NULL,
    @GroupId INT = NULL,
    @Year INT = NULL,
    @Status VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    WITH ResolvedInventory AS (
        SELECT 
            inv.Id, 
            inv.AssetCode, 
            it.Name AS ItemName, 
            it.GroupId,                            
            gr.Name AS GroupName,
            inv.DepartmentId,                      
            ISNULL(dep.Name, N'Kho trung tâm / Chưa bàn giao') AS DepartmentName, 
            loc.Name AS LocationName, 
            inv.Quantity, 
            inv.UnitPrice, 
            inv.TotalPrice,
            inv.ImportDate,
            (
                SELECT TOP 1 ml.Vendor 
                FROM dbo.MaintenanceLogs ml 
                WHERE ml.InventoryId = inv.Id 
                  AND ml.Status IN ('open', 'in_progress')
                ORDER BY ml.Id DESC
            ) AS MaintenanceVendor,
            CASE 
                -- 1. Active maintenance log with no vendor (Hospital Maintenance)
                WHEN EXISTS (
                    SELECT 1 FROM dbo.MaintenanceLogs ml 
                    WHERE ml.InventoryId = inv.Id 
                      AND ml.Status IN ('open', 'in_progress') 
                      AND (ml.Vendor IS NULL OR LTRIM(RTRIM(ml.Vendor)) = '')
                ) THEN 'maintenance_bv'
                
                -- 2. Active maintenance log with vendor (Vendor Maintenance)
                WHEN EXISTS (
                    SELECT 1 FROM dbo.MaintenanceLogs ml 
                    WHERE ml.InventoryId = inv.Id 
                      AND ml.Status IN ('open', 'in_progress') 
                      AND ml.Vendor IS NOT NULL AND LTRIM(RTRIM(ml.Vendor)) <> ''
                ) THEN 'maintenance_hang'
                
                -- 3. LifeStatus column fallback values
                WHEN inv.LifeStatus = 'active' THEN 'active'
                WHEN inv.LifeStatus = 'suspended' THEN 'suspended'
                ELSE inv.LifeStatus
            END AS ResolvedStatus
        FROM dbo.Inventory inv
        JOIN dbo.Items it ON inv.ItemId = it.Id
        JOIN dbo.Groups gr ON it.GroupId = gr.Id
        LEFT JOIN dbo.Departments dep ON inv.DepartmentId = dep.Id
        LEFT JOIN dbo.Locations loc ON inv.LocationId = loc.Id
        WHERE inv.ApprovalStatus = 'approved'
    )
    SELECT 
        Id, 
        AssetCode, 
        ItemName, 
        GroupId,                            
        GroupName,
        DepartmentId,                      
        DepartmentName, 
        LocationName, 
        Quantity, 
        UnitPrice, 
        TotalPrice, 
        ResolvedStatus AS LifeStatus,
        MaintenanceVendor
    FROM ResolvedInventory
    WHERE (@DepartmentId IS NULL OR DepartmentId = @DepartmentId)
      AND (@GroupId IS NULL OR GroupId = @GroupId)
      AND (@Year IS NULL OR YEAR(ImportDate) = @Year)
      AND (
            -- Case 1: No status filter
            @Status IS NULL OR LTRIM(RTRIM(@Status)) = '' 
            
            -- Case 2: Broad maintenance filter
            OR (@Status = 'maintenance_any' AND ResolvedStatus IN ('maintenance_bv', 'maintenance_hang'))
            
            -- Case 3: Exact status filter
            OR ResolvedStatus = LTRIM(RTRIM(@Status))
          )
    ORDER BY DepartmentName ASC, AssetCode ASC;
END;
GO

PRINT 'sp_GetInventoryReport updated successfully with dynamic status resolution.';
