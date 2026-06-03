USE HospitalAssetDB;
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ChecklistLogs]') AND name = 'ApprovalStatus')
BEGIN
    ALTER TABLE ChecklistLogs ADD ApprovalStatus VARCHAR(20) NOT NULL DEFAULT 'Pending';
END
GO

-- Mark all existing checklist logs as Approved so we don't break existing history
UPDATE ChecklistLogs SET ApprovalStatus = 'Approved';
GO
