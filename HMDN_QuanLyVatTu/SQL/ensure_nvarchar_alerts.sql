USE HospitalAssetDB;
GO

-- 1. Ensure Alerts table columns use NVARCHAR
IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Alerts' AND COLUMN_NAME = 'Title' AND DATA_TYPE = 'varchar'
)
BEGIN
    ALTER TABLE Alerts ALTER COLUMN Title NVARCHAR(300) NOT NULL;
    PRINT 'Altered Alerts.Title to NVARCHAR(300).';
END
ELSE
BEGIN
    PRINT 'Alerts.Title is already NVARCHAR or doesn''t exist.';
END
GO

IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Alerts' AND COLUMN_NAME = 'Body' AND DATA_TYPE = 'varchar'
)
BEGIN
    ALTER TABLE Alerts ALTER COLUMN Body NVARCHAR(1000) NULL;
    PRINT 'Altered Alerts.Body to NVARCHAR(1000).';
END
ELSE
BEGIN
    PRINT 'Alerts.Body is already NVARCHAR or doesn''t exist.';
END
GO

-- 2. Ensure AlertRules table columns use NVARCHAR
IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'AlertRules' AND COLUMN_NAME = 'Name' AND DATA_TYPE = 'varchar'
)
BEGIN
    ALTER TABLE AlertRules ALTER COLUMN Name NVARCHAR(300) NOT NULL;
    PRINT 'Altered AlertRules.Name to NVARCHAR(300).';
END
ELSE
BEGIN
    PRINT 'AlertRules.Name is already NVARCHAR or doesn''t exist.';
END
GO

IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'AlertRules' AND COLUMN_NAME = 'Description' AND DATA_TYPE = 'varchar'
)
BEGIN
    ALTER TABLE AlertRules ALTER COLUMN Description NVARCHAR(500) NULL;
    PRINT 'Altered AlertRules.Description to NVARCHAR(500).';
END
ELSE
BEGIN
    PRINT 'AlertRules.Description is already NVARCHAR or doesn''t exist.';
END
GO
