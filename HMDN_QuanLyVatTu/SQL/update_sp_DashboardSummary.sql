USE HospitalAssetDB;
GO

-- UPDATE PROCEDURE: sp_DashboardSummary
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_DashboardSummary]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[sp_DashboardSummary];
GO

CREATE PROCEDURE [dbo].[sp_DashboardSummary]
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Active INT = 0;
    DECLARE @Suspended INT = 0;
    DECLARE @HospitalMaintenanceCount INT = 0;
    DECLARE @VendorMaintenanceCount INT = 0;

    -- Temporary CTE to resolve the dynamic status of each approved asset, matching sp_GetInventoryReport
    WITH AssetStatuses AS (
        SELECT 
            inv.Id,
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
                WHEN inv.LifeStatus = 'maintenance_bv' THEN 'maintenance_bv'
                WHEN inv.LifeStatus = 'maintenance_hang' THEN 'maintenance_hang'
                ELSE inv.LifeStatus
            END AS ResolvedStatus
        FROM dbo.Inventory inv
        WHERE inv.ApprovalStatus = 'approved'
    )
    SELECT
        @Active = SUM(CASE WHEN ResolvedStatus = 'active' THEN 1 ELSE 0 END),
        @Suspended = SUM(CASE WHEN ResolvedStatus = 'suspended' THEN 1 ELSE 0 END),
        @HospitalMaintenanceCount = SUM(CASE WHEN ResolvedStatus = 'maintenance_bv' THEN 1 ELSE 0 END),
        @VendorMaintenanceCount = SUM(CASE WHEN ResolvedStatus = 'maintenance_hang' THEN 1 ELSE 0 END)
    FROM AssetStatuses;

    -- Return standard DTO format mapped by C#
    SELECT 
        (ISNULL(@Active, 0) + ISNULL(@Suspended, 0) + ISNULL(@HospitalMaintenanceCount, 0) + ISNULL(@VendorMaintenanceCount, 0)) AS TotalAssets,
        ISNULL(@Active, 0) AS OperatingWell,
        ISNULL(@Suspended, 0) AS BrokenAssets,
        CASE 
            WHEN (ISNULL(@Active, 0) + ISNULL(@Suspended, 0)) > 0 
            THEN (CAST(ISNULL(@Active, 0) AS FLOAT) / (ISNULL(@Active, 0) + ISNULL(@Suspended, 0))) * 100.0 
            ELSE 0.0 
        END AS ActivePercentage,
        CASE 
            WHEN (ISNULL(@Active, 0) + ISNULL(@Suspended, 0)) > 0 
            THEN (CAST(ISNULL(@Suspended, 0) AS FLOAT) / (ISNULL(@Active, 0) + ISNULL(@Suspended, 0))) * 100.0 
            ELSE 0.0 
        END AS BrokenPercentage,
        @HospitalMaintenanceCount AS HospitalMaintenanceCount,
        @VendorMaintenanceCount AS VendorMaintenanceCount;
END;
GO

PRINT 'sp_DashboardSummary updated successfully with aligned metrics.';
