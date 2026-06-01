USE HospitalAssetDB;
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[InventoryAttachments]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[InventoryAttachments](
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [InventoryId] [int] NOT NULL,
        [FileName] [nvarchar](200) NOT NULL,
        [FileType] [nvarchar](50) NULL,
        [FileSize] [bigint] NOT NULL,
        [FileData] [varbinary](max) NOT NULL,
        [UploadedBy] [int] NULL,
        [UploadedAt] [datetime2](7) NOT NULL DEFAULT GETDATE(),
        CONSTRAINT [PK_InventoryAttachments] PRIMARY KEY CLUSTERED 
        (
            [Id] ASC
        )
    );

    ALTER TABLE [dbo].[InventoryAttachments]  WITH CHECK ADD  CONSTRAINT [FK_InventoryAttachments_Inventory] FOREIGN KEY([InventoryId])
    REFERENCES [dbo].[Inventory] ([Id])
    ON DELETE CASCADE;

    ALTER TABLE [dbo].[InventoryAttachments] CHECK CONSTRAINT [FK_InventoryAttachments_Inventory];
    
    PRINT 'Table InventoryAttachments created successfully.';
END
ELSE
BEGIN
    PRINT 'Table InventoryAttachments already exists.';
END
GO
