-- ============================================================
-- SQL Script: Initialize Checklist Database Tables & Procedures
-- ============================================================

USE HospitalAssetDB;
GO

-- 1. Create tables if they do not exist
-- CheckCycles
IF OBJECT_ID('dbo.CheckCycles', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CheckCycles](
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [Name] [nvarchar](200) NOT NULL,
        [CycleType] [varchar](20) NOT NULL,
        [RepeatOn] [varchar](200) NULL,
        [IsRepeat] [bit] NOT NULL CONSTRAINT [DF_CheckCycles_IsRepeat] DEFAULT ((1)),
        [RepeatCount] [int] NULL,
        [EndDate] [date] NULL,
        [Description] [nvarchar](500) NULL,
        [IsActive] [bit] NOT NULL CONSTRAINT [DF_CheckCycles_IsActive] DEFAULT ((1)),
        [CreatedAt] [datetime2](7) NOT NULL CONSTRAINT [DF_CheckCycles_CreatedAt] DEFAULT (getdate()),
        CONSTRAINT [PK_CheckCycles] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    ALTER TABLE [dbo].[CheckCycles] WITH CHECK ADD CONSTRAINT [CK_CheckCycles_CycleType] 
    CHECK (([CycleType]='Yearly' OR [CycleType]='Quarterly' OR [CycleType]='Monthly' OR [CycleType]='Weekly' OR [CycleType]='Daily' OR [CycleType]='yearly' OR [CycleType]='quarterly' OR [CycleType]='monthly' OR [CycleType]='weekly' OR [CycleType]='daily'));
END
GO

-- ChecklistDefinitions
IF OBJECT_ID('dbo.ChecklistDefinitions', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ChecklistDefinitions](
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [Scope] [varchar](10) NOT NULL,
        [GroupId] [int] NULL,
        [ItemId] [int] NULL,
        [CycleType] [varchar](20) NULL,
        [CheckName] [nvarchar](300) NOT NULL,
        [Description] [nvarchar](500) NULL,
        [IsRequired] [bit] NOT NULL CONSTRAINT [DF_ChecklistDefinitions_IsRequired] DEFAULT ((1)),
        [SortOrder] [int] NOT NULL CONSTRAINT [DF_ChecklistDefinitions_SortOrder] DEFAULT ((0)),
        [IsActive] [bit] NOT NULL CONSTRAINT [DF_ChecklistDefinitions_IsActive] DEFAULT ((1)),
        [CreatedAt] [datetime2](7) NOT NULL CONSTRAINT [DF_ChecklistDefinitions_CreatedAt] DEFAULT (getdate()),
        CONSTRAINT [PK_ChecklistDefinitions] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ChecklistDefinitions_Groups] FOREIGN KEY([GroupId]) REFERENCES [dbo].[Groups] ([Id]),
        CONSTRAINT [FK_ChecklistDefinitions_Items] FOREIGN KEY([ItemId]) REFERENCES [dbo].[Items] ([Id]),
        CONSTRAINT [CK_ChecklistDefinitions_CycleType] CHECK (([CycleType]=NULL OR [CycleType]='yearly' OR [CycleType]='monthly' OR [CycleType]='weekly' OR [CycleType]='daily')),
        CONSTRAINT [CK_ChecklistDefinitions_Scope] CHECK (([Scope]='item' OR [Scope]='group' OR [Scope]='global'))
    );
END
GO

-- ChecklistSchedules
IF OBJECT_ID('dbo.ChecklistSchedules', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ChecklistSchedules](
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [InventoryId] [int] NOT NULL,
        [ScheduledDate] [date] NOT NULL,
        [CycleType] [varchar](20) NOT NULL,
        [Status] [varchar](20) NOT NULL CONSTRAINT [DF_ChecklistSchedules_Status] DEFAULT ('pending'),
        [DueDate] [date] NOT NULL,
        [AssignedTo] [int] NULL,
        [CreatedAt] [datetime2](7) NOT NULL CONSTRAINT [DF_ChecklistSchedules_CreatedAt] DEFAULT (getdate()),
        CONSTRAINT [PK_ChecklistSchedules] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ChecklistSchedules_Users] FOREIGN KEY([AssignedTo]) REFERENCES [dbo].[Users] ([Id]),
        CONSTRAINT [FK_ChecklistSchedules_Inventory] FOREIGN KEY([InventoryId]) REFERENCES [dbo].[Inventory] ([Id]),
        CONSTRAINT [CK_ChecklistSchedules_Status] CHECK (([Status]='skipped' OR [Status]='overdue' OR [Status]='done' OR [Status]='pending' OR [Status]='completed'))
    );
END
GO

-- ChecklistLogs
IF OBJECT_ID('dbo.ChecklistLogs', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ChecklistLogs](
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [ScheduleId] [int] NULL,
        [InventoryId] [int] NOT NULL,
        [CheckedBy] [int] NOT NULL,
        [CheckedAt] [datetime2](7) NOT NULL CONSTRAINT [DF_ChecklistLogs_CheckedAt] DEFAULT (getdate()),
        [CycleType] [varchar](20) NOT NULL,
        [OverallResult] [varchar](20) NOT NULL CONSTRAINT [DF_ChecklistLogs_OverallResult] DEFAULT ('pass'),
        [Note] [nvarchar](2000) NULL,
        [QrScannedAt] [datetime2](7) NULL,
        [QrLocation] [nvarchar](200) NULL,
        [ImageUrls] [nvarchar](2000) NULL,
        CONSTRAINT [PK_ChecklistLogs] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ChecklistLogs_Users] FOREIGN KEY([CheckedBy]) REFERENCES [dbo].[Users] ([Id]),
        CONSTRAINT [FK_ChecklistLogs_Inventory] FOREIGN KEY([InventoryId]) REFERENCES [dbo].[Inventory] ([Id]),
        CONSTRAINT [FK_ChecklistLogs_Schedules] FOREIGN KEY([ScheduleId]) REFERENCES [dbo].[ChecklistSchedules] ([Id]),
        CONSTRAINT [CK_ChecklistLogs_OverallResult] CHECK (([OverallResult]='partial' OR [OverallResult]='fail' OR [OverallResult]='pass'))
    );
END
GO

-- ChecklistLogItems
IF OBJECT_ID('dbo.ChecklistLogItems', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ChecklistLogItems](
        [Id] [bigint] IDENTITY(1,1) NOT NULL,
        [LogId] [int] NOT NULL,
        [DefinitionId] [int] NOT NULL,
        [IsPassed] [bit] NOT NULL CONSTRAINT [DF_ChecklistLogItems_IsPassed] DEFAULT ((1)),
        [Note] [nvarchar](500) NULL,
        CONSTRAINT [PK_ChecklistLogItems] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ChecklistLogItems_Definitions] FOREIGN KEY([DefinitionId]) REFERENCES [dbo].[ChecklistDefinitions] ([Id]),
        CONSTRAINT [FK_ChecklistLogItems_Logs] FOREIGN KEY([LogId]) REFERENCES [dbo].[ChecklistLogs] ([Id]) ON DELETE CASCADE
    );
END
GO

-- 2. Seed default data if empty
-- CheckCycles
IF NOT EXISTS (SELECT 1 FROM [dbo].[CheckCycles])
BEGIN
    INSERT INTO CheckCycles (Name, CycleType, RepeatOn, IsRepeat, Description) VALUES
    (N'Kiểm tra hàng ngày',             'daily',   NULL,       1, N'Kiểm tra mỗi ngày làm việc'),
    (N'Kiểm tra hàng tuần (T2,T6)',     'weekly',  '[2,6]',    1, N'Mỗi thứ 2 và thứ 6'),
    (N'Bảo trì hàng tháng',             'monthly', '[1]',      1, N'Ngày đầu mỗi tháng'),
    (N'Kiểm định 3 tháng',              'monthly', '[1,4,7,10]',1,N'Tháng 1,4,7,10'),
    (N'Bảo dưỡng 6 tháng',              'monthly', '[3,9]',    1, N'Tháng 3 và tháng 9'),
    (N'Kiểm định năm',                  'yearly',  '[1]',      1, N'Tháng 1 hàng năm'),
    (N'Thay mực/vật tư định kỳ',        'monthly', '[1,15]',   1, N'Ngày 1 và 15 hàng tháng');
    PRINT 'CheckCycles seeded successfully.';
END
GO

-- ChecklistDefinitions
IF NOT EXISTS (SELECT 1 FROM [dbo].[ChecklistDefinitions])
BEGIN
    INSERT INTO ChecklistDefinitions (Scope, CycleType, CheckName, SortOrder) VALUES
    ('global', NULL,      N'Kiểm tra ngoại quan, trầy xước, hư hỏng vật lý', 1),
    ('global', NULL,      N'Kiểm tra nguồn điện và đèn báo hiệu',             2),
    ('global', NULL,      N'Kiểm tra nhãn mã tài sản còn nguyên vẹn',         3),
    ('global', 'monthly', N'Vệ sinh thiết bị',                                 4),
    ('global', 'yearly',  N'Kiểm định định kỳ theo quy định bệnh viện',        5);
    PRINT 'ChecklistDefinitions seeded successfully.';
END
GO

-- 3. Stored Procedures
-- sp_GetChecklistForInventory
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_GetChecklistForInventory]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[sp_GetChecklistForInventory];
GO

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

-- sp_GenerateChecklistSchedules
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_GenerateChecklistSchedules]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[sp_GenerateChecklistSchedules];
GO

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

PRINT 'Checklist system setup executed successfully.';
