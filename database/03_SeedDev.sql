-- Optional local sample. Do not run in production.
-- Run while connected to the target database.

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [LoginName] = N'mom')
BEGIN
    DECLARE @mom UNIQUEIDENTIFIER = NEWID();
    DECLARE @dad UNIQUEIDENTIFIER = NEWID();
    DECLARE @home UNIQUEIDENTIFIER = NEWID();

    INSERT INTO [dbo].[Users] ([UserId], [LoginName]) VALUES
        (@mom, N'mom'),
        (@dad, N'dad');

    INSERT INTO [dbo].[Tenants] ([TenantId], [Name]) VALUES
        (@home, N'Home');

    INSERT INTO [dbo].[Memberships] ([UserId], [TenantId], [Nickname], [Role]) VALUES
        (@mom, @home, N'mom', N'Admin'),
        (@dad, @home, N'dad', N'Member');

    INSERT INTO [dbo].[Items] ([ItemId], [TenantId], [Text], [Source], [CreatedByUserId]) VALUES
        (NEWID(), @home, N'Milk', N'ExcelCell', @mom);
END
GO
