-- Run while connected as a Microsoft Entra SQL admin.
-- Creates a contained user for the App Service managed identity (droplist-azpcloud-api).

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'droplist-azpcloud-api')
BEGIN
    CREATE USER [droplist-azpcloud-api] FROM EXTERNAL PROVIDER;
END
GO

ALTER ROLE db_datareader ADD MEMBER [droplist-azpcloud-api];
ALTER ROLE db_datawriter ADD MEMBER [droplist-azpcloud-api];
GO
