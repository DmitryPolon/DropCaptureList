-- Stamp when Windows "Clear list" marks a household complete.

IF COL_LENGTH(N'dbo.Tenants', N'LastClearedAt') IS NULL
BEGIN
    ALTER TABLE [dbo].[Tenants] ADD [LastClearedAt] DATETIMEOFFSET(7) NULL;
END
GO
