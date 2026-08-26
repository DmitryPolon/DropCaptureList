# Creates an Azure SQL logical server and database.
# Pass names at runtime. Do not commit real resource names.
# Product tables: connect to the database and run 02_CreateTables.sql.
# Prefer the portal free offer when it applies; this CLI path is General Purpose serverless.

param(
    [Parameter(Mandatory = $true)]
    [string] $ResourceGroup,

    [Parameter(Mandatory = $true)]
    [string] $Location,

    [Parameter(Mandatory = $true)]
    [string] $ServerName,

    [Parameter(Mandatory = $true)]
    [string] $DatabaseName
)

$ErrorActionPreference = "Stop"

az group create --name $ResourceGroup --location $Location | Out-Null

$adminName = az account show --query user.name -o tsv
$adminSid = az ad signed-in-user show --query id -o tsv

az sql server create `
    --resource-group $ResourceGroup `
    --name $ServerName `
    --location $Location `
    --enable-ad-only-auth `
    --external-admin-principal-type User `
    --external-admin-name $adminName `
    --external-admin-sid $adminSid

az sql db create `
    --resource-group $ResourceGroup `
    --server $ServerName `
    --name $DatabaseName `
    --edition GeneralPurpose `
    --family Gen5 `
    --capacity 1 `
    --compute-model Serverless `
    --auto-pause-delay 60 `
    --min-capacity 0.5

$myIp = (Invoke-RestMethod -Uri "https://api.ipify.org").Trim()
az sql server firewall-rule create `
    --resource-group $ResourceGroup `
    --server $ServerName `
    --name ClientIp `
    --start-ip-address $myIp `
    --end-ip-address $myIp | Out-Null

Write-Host "Created. Connect with Microsoft Entra and run database\02_CreateTables.sql against the new database."
