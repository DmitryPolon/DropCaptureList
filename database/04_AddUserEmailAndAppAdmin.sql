-- App-level admin is on Users, not only household Memberships.Role.
-- Email is the sign-in identity (Entra). Nickname stays on Memberships.

IF COL_LENGTH(N'dbo.Users', N'Email') IS NULL
BEGIN
    ALTER TABLE [dbo].[Users] ADD [Email] NVARCHAR(256) NULL;
END
GO

IF COL_LENGTH(N'dbo.Users', N'IsAppAdmin') IS NULL
BEGIN
    ALTER TABLE [dbo].[Users] ADD [IsAppAdmin] BIT NOT NULL
        CONSTRAINT [DF_Users_IsAppAdmin] DEFAULT (0);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UQ_Users_Email' AND object_id = OBJECT_ID(N'dbo.Users'))
BEGIN
    CREATE UNIQUE INDEX [UQ_Users_Email]
        ON [dbo].[Users] ([Email])
        WHERE [Email] IS NOT NULL;
END
GO
