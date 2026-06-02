USE HospitalAssetDB;
GO

/****** Object:  StoredProcedure [dbo].[sp_GetInventoryReport] ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

ALTER PROCEDURE [dbo].[sp_GetInventoryReport]
    @DepartmentId INT = NULL,
    @GroupId INT = NULL,
    @Year INT = NULL,
    @Status VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

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
        CASE 
            WHEN EXISTS (SELECT 1 FROM dbo.MaintenanceLogs ml WHERE ml.InventoryId = inv.Id AND ml.Status IN ('open', 'in_progress') AND (ml.Vendor IS NULL OR LTRIM(RTRIM(ml.Vendor)) = '')) THEN 'maintenance_bv'
            WHEN EXISTS (SELECT 1 FROM dbo.MaintenanceLogs ml WHERE ml.InventoryId = inv.Id AND ml.Status IN ('open', 'in_progress') AND ml.Vendor IS NOT NULL AND LTRIM(RTRIM(ml.Vendor)) <> '') THEN 'maintenance_hang'
            ELSE inv.LifeStatus 
        END AS LifeStatus
    FROM dbo.Inventory inv
    JOIN dbo.Items it ON inv.ItemId = it.Id
    JOIN dbo.Groups gr ON it.GroupId = gr.Id
    LEFT JOIN dbo.Departments dep ON inv.DepartmentId = dep.Id
    LEFT JOIN dbo.Locations loc ON inv.LocationId = loc.Id
    WHERE inv.ApprovalStatus = 'approved'
      AND (@DepartmentId IS NULL OR inv.DepartmentId = @DepartmentId)
      AND (@GroupId IS NULL OR it.GroupId = @GroupId)
      AND (@Year IS NULL OR YEAR(inv.ImportDate) = @Year)
      AND (
            -- Trường hợp 1: Không chọn bộ lọc trạng thái (Xem tất cả tổng tài sản)
            @Status IS NULL OR LTRIM(RTRIM(@Status)) = '' 
            
            -- Trường hợp 2: Click chọn thẻ "Thiết bị đang bảo trì" tổng hợp (Bao gồm cả BV và Hãng bảo trì)
            OR (@Status = 'maintenance_any' AND EXISTS (SELECT 1 FROM dbo.MaintenanceLogs ml WHERE ml.InventoryId = inv.Id AND ml.Status IN ('open', 'in_progress')))
            
            -- Trường hợp 3: Bệnh viện tự bảo trì
            OR (@Status = 'maintenance_bv' AND EXISTS (SELECT 1 FROM dbo.MaintenanceLogs ml WHERE ml.InventoryId = inv.Id AND ml.Status IN ('open', 'in_progress') AND (ml.Vendor IS NULL OR LTRIM(RTRIM(ml.Vendor)) = '')))
            
            -- Trường hợp 4: Hãng bảo trì
            OR (@Status = 'maintenance_hang' AND EXISTS (SELECT 1 FROM dbo.MaintenanceLogs ml WHERE ml.InventoryId = inv.Id AND ml.Status IN ('open', 'in_progress') AND ml.Vendor IS NOT NULL AND LTRIM(RTRIM(ml.Vendor)) <> ''))
            
            -- Trường hợp 5: Khớp chính xác trạng thái đơn lẻ ('active', 'suspended')
            OR inv.LifeStatus = LTRIM(RTRIM(@Status))
          )
    ORDER BY DepartmentName ASC, inv.AssetCode ASC; 
END;
GO
