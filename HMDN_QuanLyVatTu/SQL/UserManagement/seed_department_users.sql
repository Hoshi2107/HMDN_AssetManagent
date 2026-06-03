-- SQL Script: Seed realistic user accounts for hospital departments with distinct strong passwords and role-based permissions
-- This script also updates existing legacy accounts with strong passwords.

SET NOCOUNT ON;

-- 1. Create a temporary table containing the user data to seed
IF OBJECT_ID('tempdb..#SeedUsers') IS NOT NULL DROP TABLE #SeedUsers;
CREATE TABLE #SeedUsers (
    Username NVARCHAR(50),
    PasswordHash NVARCHAR(100),
    FullName NVARCHAR(100),
    Email NVARCHAR(100),
    Phone NVARCHAR(20),
    DepartmentCode NVARCHAR(50),
    RoleCode NVARCHAR(50)
);

INSERT INTO #SeedUsers (Username, PasswordHash, FullName, Email, Phone, DepartmentCode, RoleCode) VALUES
('ktv.cntt', 'KtvCntt@2026', N'Nguyễn Anh Tuấn (KTV CNTT)', 'tuan.na@benhvien.vn', '0902000001', 'CNTT', 'technician'),
('head.cntt', 'HeadCntt@2026', N'Trần Quốc Bảo (Trưởng phòng CNTT)', 'bao.tq@benhvien.vn', '0902000002', 'CNTT', 'manager'),
('ktv.vattu', 'KtvVattu@2026', N'Nguyễn Hoàng Nam (KTV Vật tư)', 'nam.nh@benhvien.vn', '0903000001', 'VATTU', 'technician'),
('manager.hc', 'ManagerHc@2026', N'Bùi Thị Mai (Trưởng phòng Hành chính)', 'mai.bt@benhvien.vn', '0904000001', 'HC', 'manager'),
('staff.hc', 'StaffHc@2026', N'Lê Thị Đào (NV Hành chính)', 'dao.lt@benhvien.vn', '0904000002', 'HC', 'viewer'),
('head.noitru', 'HeadNoitru@2026', N'Nguyễn Văn An (Trưởng khoa Nội trú)', 'an.nv@benhvien.vn', '0905000001', 'NOITRU', 'approver'),
('staff.noitru', 'StaffNoitru@2026', N'Trần Thị Bình (Điều dưỡng Nội trú)', 'binh.tt@benhvien.vn', '0905000002', 'NOITRU', 'viewer'),
('head.ngoai', 'HeadNgoai@2026', N'Lê Văn Chung (Trưởng khoa Ngoại)', 'chung.lv@benhvien.vn', '0906000001', 'NGOAI', 'approver'),
('staff.ngoai', 'StaffNgoai@2026', N'Đỗ Thị Dung (Điều dưỡng Ngoại)', 'dung.dt@benhvien.vn', '0906000002', 'NGOAI', 'viewer'),
('head.capcuu', 'HeadCapcuu@2026', N'Phạm Văn Giang (Trưởng khoa Cấp cứu)', 'giang.pv@benhvien.vn', '0907000001', 'CAPCUU', 'approver'),
('staff.capcuu', 'StaffCapcuu@2026', N'Hoàng Thị Hương (Điều dưỡng Cấp cứu)', 'huong.ht@benhvien.vn', '0907000002', 'CAPCUU', 'viewer'),
('head.xn', 'HeadXn@2026', N'Vũ Văn Khánh (Trưởng khoa Xét nghiệm)', 'khanh.vv@benhvien.vn', '0908000001', 'XN', 'approver'),
('staff.xn', 'StaffXn@2026', N'Ngô Thị Lan (KTV Xét nghiệm)', 'lan.nt@benhvien.vn', '0908000002', 'XN', 'viewer'),
('head.cdha', 'HeadCdha@2026', N'Đặng Văn Minh (Trưởng khoa CĐHA)', 'minh.dv@benhvien.vn', '0909000001', 'CDHA', 'approver'),
('staff.cdha', 'StaffCdha@2026', N'Phan Thị Ngọc (KTV CĐHA)', 'ngoc.pt@benhvien.vn', '0909000002', 'CDHA', 'viewer'),
('manager.qlcl', 'ManagerQlcl@2026', N'Hồ Hoàng Yến (Trưởng phòng QLCL)', 'yen.hh@benhvien.vn', '0910000001', 'QLCL', 'manager'),
('staff.qlcl', 'StaffQlcl@2026', N'Nguyễn Văn Sơn (NV QLCL)', 'son.nv@benhvien.vn', '0910000002', 'QLCL', 'viewer');

-- 2. Process seeding in a loop to ensure idempotency and accurate mapping
DECLARE @Username NVARCHAR(50), @PasswordHash NVARCHAR(100), @FullName NVARCHAR(100), @Email NVARCHAR(100), @Phone NVARCHAR(20), @DeptCode NVARCHAR(50), @RoleCode NVARCHAR(50);
DECLARE @DeptId INT, @RoleId INT, @UserId INT;

DECLARE user_cursor CURSOR FOR 
SELECT Username, PasswordHash, FullName, Email, Phone, DepartmentCode, RoleCode FROM #SeedUsers;

OPEN user_cursor;
FETCH NEXT FROM user_cursor INTO @Username, @PasswordHash, @FullName, @Email, @Phone, @DeptCode, @RoleCode;

WHILE @@FETCH_STATUS = 0
BEGIN
    -- Get Department ID
    SET @DeptId = (SELECT TOP 1 Id FROM Departments WHERE Code = @DeptCode);
    
    -- Get Role ID
    SET @RoleId = (SELECT TOP 1 Id FROM Roles WHERE LOWER(Code) = LOWER(@RoleCode));

    IF @DeptId IS NOT NULL AND @RoleId IS NOT NULL
    BEGIN
        -- Insert user if they don't already exist
        IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = @Username)
        BEGIN
            INSERT INTO Users (Username, PasswordHash, FullName, Email, Phone, DepartmentId, IsActive, CreatedAt)
            VALUES (@Username, @PasswordHash, @FullName, @Email, @Phone, @DeptId, 1, GETDATE());
            
            PRINT 'Created User: ' + @Username + ' (' + @FullName + ')';
        END
        ELSE
        BEGIN
            -- Ensure active and update details if they already exist
            UPDATE Users 
            SET PasswordHash = @PasswordHash, FullName = @FullName, Email = @Email, Phone = @Phone, DepartmentId = @DeptId, IsActive = 1
            WHERE Username = @Username;
            
            PRINT 'Updated User: ' + @Username;
        END

        -- Get User ID
        SET @UserId = (SELECT TOP 1 Id FROM Users WHERE Username = @Username);

        -- Map to role in UserRoles table
        IF NOT EXISTS (SELECT 1 FROM UserRoles WHERE UserId = @UserId AND RoleId = @RoleId)
        BEGIN
            INSERT INTO UserRoles (UserId, RoleId) VALUES (@UserId, @RoleId);
            PRINT 'Mapped ' + @Username + ' to role: ' + @RoleCode;
        END
    END
    ELSE
    BEGIN
        PRINT 'Warning: Could not map ' + @Username + ' due to missing DepartmentCode (' + ISNULL(@DeptCode, 'NULL') + ') or RoleCode (' + ISNULL(@RoleCode, 'NULL') + ')';
    END

    FETCH NEXT FROM user_cursor INTO @Username, @PasswordHash, @FullName, @Email, @Phone, @DeptCode, @RoleCode;
END;

CLOSE user_cursor;
DEALLOCATE user_cursor;

DROP TABLE #SeedUsers;

-- 3. Update existing legacy users with strong, realistic passwords as well
PRINT 'Updating legacy users passwords...';

UPDATE Users SET PasswordHash = 'Admin@2026' WHERE Username = 'admin';
UPDATE Users SET PasswordHash = 'ManagerVattu@2026' WHERE Username = 'manager.vattu';
UPDATE Users SET PasswordHash = 'KtvLe@2026' WHERE Username = 'ktv.le';
UPDATE Users SET PasswordHash = 'ApproverPham@2026' WHERE Username = 'approver.pham';
UPDATE Users SET PasswordHash = 'ViewerHoang@2026' WHERE Username = 'viewer.hoang';

PRINT 'User seeding and password configuration completed successfully.';
