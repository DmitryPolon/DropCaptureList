-- Optional local SQL Server / LocalDB only.
-- Intended home for tables is Azure SQL (see database\CreateAzureSql.ps1).
-- Do not use this database for app logs — those go to Application Insights.
--
-- sqlcmd -S "(localdb)\MSSQLLocalDB" -v DatabaseName="YourDatabase" -i 01_CreateDatabase.sql
-- Or enable SQLCMD mode in SSMS and set DatabaseName.

IF DB_ID(N'$(DatabaseName)') IS NULL
BEGIN
    CREATE DATABASE [$(DatabaseName)];
END
GO
