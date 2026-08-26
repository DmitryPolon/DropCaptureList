-- Short household tagline shown next to the name (the tenant "logo" in this build).

IF COL_LENGTH(N'dbo.Tenants', N'Motto') IS NULL
BEGIN
    ALTER TABLE [dbo].[Tenants] ADD [Motto] NVARCHAR(120) NULL;
END
GO
