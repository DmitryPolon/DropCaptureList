# DropCaptureList

Shared household list. A Windows app captures highlighted Excel cells (one cell = one record). A React mobile web app will check items off together. That web app is not in this repository yet.

Open `DropCaptureList.slnx` in Visual Studio, or `src/windows/DropCaptureList.Windows/DropCaptureList.Windows.csproj`.

## What works today

- WPF on .NET 9.
- Sign in with **email** and **household name** (not nickname). Nickname is the display name on a household.
- Session is remembered on this PC (DPAPI). **Sign out** clears it.
- Azure SQL with Microsoft Entra (no SQL password in the app). First Continue may show a Windows account picker; later launches reuse a cached token until sign-out.
- Excel capture: highlight cells, then **Capture selected Excel cells**. Empty cells are skipped. Merged ranges count as one record.
- **Delete selected** removes a row that should never have been captured.
- **Clear list** marks remaining items completed. They stay in the database and show gray. History reporting is later.
- Temporary WPF admin (app admins only): add a user, create a household, remove someone from a household. The long-term admin UI is the React app.

Word and Notepad line capture are not in this build.

## Local SQL settings (keep out of git)

Copy `src/windows/DropCaptureList.Windows/appsettings.Local.json.example` to `appsettings.Local.json` (same folder) on your machine only. Fill in:

- `Server` — Azure SQL host
- `Database` — database name
- `UserId` — Entra email used to get a token
- `TenantId` — Entra tenant id (so Hotmail is not sent to the wrong Microsoft tenant)

`appsettings.Local.json` is gitignored. Never commit real server names, database names, emails, or tenant ids. `appsettings.json` in git stays empty.

Until that file is present, the app can still run against a local JSON store.

## Database scripts

Create the Azure SQL server and database in the portal (or pass names into `database/CreateAzureSql.ps1` at runtime — do not put those names in the repo). Then connect with Microsoft Entra and run, in order:

1. `database/02_CreateTables.sql`
2. `database/04_AddUserEmailAndAppAdmin.sql` if the database predates email / app-admin columns
3. `database/05_AddItemDisplayFormat.sql` if the database predates Excel layout columns

`database/01_CreateDatabase.sql` is only for optional LocalDB (`sqlcmd -v DatabaseName=...`). `database/03_SeedDev.sql` is sample `mom` / `dad` / `Home` data for local use — do not run it against a shared production database.

App admin is `Users.IsAppAdmin`. Household role is `Memberships.Role`. Those are different.

## Not in this repo

- React list and admin
- REST API
- Application Insights
- Word / Notepad capture
- Weekly history report UI

See [PLAN.md](PLAN.md) for the longer product sketch. Some items there (Azure SQL) are already started in the Windows app.

## Requirements

- Windows
- .NET 9 SDK
- Excel for capture
- An Azure SQL database with Entra-only auth, and your Entra user as a SQL admin or contained user
