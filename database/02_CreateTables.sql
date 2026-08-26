-- Four tables: Users, Tenants, Memberships, Items.
-- Nickname lives on Memberships (same person can be "mom" in one household and "Alex" in another).
-- 1 Excel cell = 1 Items row. Completed is shared for the household.
-- Per-user swipe-hide is not in this schema (add ItemHides later if needed).

-- Run while connected to the target database (Azure SQL or local).
-- Do not include CREATE DATABASE here — create the database in the portal or CLI first.

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Users]
    (
        [UserId]     UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_Users] PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        [LoginName]  NVARCHAR(100)    NOT NULL,
        [Email]      NVARCHAR(256)    NULL,
        [IsAppAdmin] BIT              NOT NULL CONSTRAINT [DF_Users_IsAppAdmin] DEFAULT (0),
        [CreatedAt]  DATETIMEOFFSET(7) NOT NULL CONSTRAINT [DF_Users_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [UQ_Users_LoginName] UNIQUE ([LoginName])
    );
END
GO

IF OBJECT_ID(N'dbo.Tenants', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Tenants]
    (
        [TenantId]   UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_Tenants] PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        [Name]       NVARCHAR(100)    NOT NULL,
        [CreatedAt]  DATETIMEOFFSET(7) NOT NULL CONSTRAINT [DF_Tenants_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [UQ_Tenants_Name] UNIQUE ([Name])
    );
END
GO

IF OBJECT_ID(N'dbo.Memberships', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Memberships]
    (
        [UserId]     UNIQUEIDENTIFIER NOT NULL,
        [TenantId]   UNIQUEIDENTIFIER NOT NULL,
        [Nickname]   NVARCHAR(50)     NOT NULL,
        [Role]       NVARCHAR(20)     NOT NULL CONSTRAINT [DF_Memberships_Role] DEFAULT N'Member',
        [CreatedAt]  DATETIMEOFFSET(7) NOT NULL CONSTRAINT [DF_Memberships_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [PK_Memberships] PRIMARY KEY ([UserId], [TenantId]),
        CONSTRAINT [FK_Memberships_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([UserId]),
        CONSTRAINT [FK_Memberships_Tenants] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([TenantId]),
        CONSTRAINT [CK_Memberships_Role] CHECK ([Role] IN (N'Member', N'Admin')),
        CONSTRAINT [UQ_Memberships_Tenant_Nickname] UNIQUE ([TenantId], [Nickname])
    );
END
GO

IF OBJECT_ID(N'dbo.Items', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Items]
    (
        [ItemId]             UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_Items] PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        [TenantId]           UNIQUEIDENTIFIER NOT NULL,
        [Text]               NVARCHAR(500)    NOT NULL,
        [Source]             NVARCHAR(20)     NOT NULL CONSTRAINT [DF_Items_Source] DEFAULT N'ExcelCell',
        [ExcelAddress]       NVARCHAR(32)     NULL,
        [ExcelRow]           INT              NULL,
        [ExcelColumn]        INT              NULL,
        [IsBold]             BIT              NOT NULL CONSTRAINT [DF_Items_IsBold] DEFAULT (0),
        [FontColor]          NVARCHAR(9)      NULL,
        [FillColor]          NVARCHAR(9)      NULL,
        [CreatedByUserId]    UNIQUEIDENTIFIER NOT NULL,
        [CreatedAt]          DATETIMEOFFSET(7) NOT NULL CONSTRAINT [DF_Items_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
        [CompletedByUserId]  UNIQUEIDENTIFIER NULL,
        [CompletedAt]        DATETIMEOFFSET(7) NULL,
        [IsDeleted]          BIT              NOT NULL CONSTRAINT [DF_Items_IsDeleted] DEFAULT (0),
        CONSTRAINT [FK_Items_Tenants] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([TenantId]),
        CONSTRAINT [FK_Items_CreatedBy] FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[Users] ([UserId]),
        CONSTRAINT [FK_Items_CompletedBy] FOREIGN KEY ([CompletedByUserId]) REFERENCES [dbo].[Users] ([UserId]),
        CONSTRAINT [CK_Items_Source] CHECK ([Source] IN (N'ExcelCell', N'TextLine')),
        CONSTRAINT [CK_Items_Completed] CHECK (
            ([CompletedAt] IS NULL AND [CompletedByUserId] IS NULL)
            OR ([CompletedAt] IS NOT NULL AND [CompletedByUserId] IS NOT NULL)
        )
    );

    CREATE INDEX [IX_Items_Tenant_CreatedAt] ON [dbo].[Items] ([TenantId], [CreatedAt] DESC)
        WHERE [IsDeleted] = 0;
END
GO
