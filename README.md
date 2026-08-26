# DropCaptureList

Shared household list. A Windows app captures highlighted Excel cells (one cell = one record). The **mobile web app is React** (Vite) so phones can check items off in the browser. That is the stack to keep using.

Open `DropCaptureList.slnx` in Visual Studio.

## What works today

- WPF on .NET 9 for Excel capture.
- Sign in with **email** and **household name** (not nickname). Nickname is the display name on a household.
- Session is remembered on this PC (DPAPI). **Sign out** clears it.
- Azure SQL with Microsoft Entra (no SQL password in the app). First Continue may show a Windows account picker; later launches reuse a cached token until sign-out.
- Excel capture: highlight cells, then **Capture selected Excel cells**. Empty cells are skipped. Merged ranges count as one record.
- **Delete selected** removes a row that should never have been captured.
- **Clear list** marks remaining items completed. They stay in the database and show gray. History reporting is later.
- Temporary WPF **Admin** (app admins only): add a user, create a household, set a household motto, remove someone from a household. Full admin will move to the web later.
- Each household can have an optional **motto**. Edit it in Windows Admin. The React app displays the letter mark and motto. Custom image logos are not in this build.

Word and Notepad line capture are not in this build.

## Run the mobile web app

1. Copy `src/api/appsettings.Local.json.example` to `src/api/appsettings.Local.json` and fill in the same SQL values you use for the Windows app (that file is gitignored).
2. Run `database/06_AddTenantMotto.sql` if you have not already.
3. `dotnet run --project src/api --launch-profile http`
4. In another terminal: `npm install` then `npm run dev` in `src/web`
5. Open http://localhost:5173 on the PC or phone on the same network (use the Vite URL). Sign in with **email** and **household name**. Checking a box marks that item complete for everyone in the household.

Set a motto in Windows Admin; it shows under the household name on the web. Capture still happens in Excel on Windows.

## Local SQL settings (keep out of git)

Copy `appsettings.Local.json.example` to `appsettings.Local.json` next to the Windows project and the API project, on your machine only. Fill in:

- `Server` — Azure SQL host
- `Database` — database name
- `UserId` — Entra email used to get a token
- `TenantId` — Entra tenant id (so Hotmail is not sent to the wrong Microsoft tenant)

Never commit real server names, database names, emails, or tenant ids.

Until the Windows local file is present, the Windows app can still run against a local JSON store.

## Database scripts

Create the Azure SQL server and database in the portal (or pass names into `database/CreateAzureSql.ps1` at runtime — do not put those names in the repo). Then connect with Microsoft Entra and run, in order:

1. `database/02_CreateTables.sql`
2. `database/04_AddUserEmailAndAppAdmin.sql` if the database predates email / app-admin columns
3. `database/05_AddItemDisplayFormat.sql` if the database predates Excel layout columns
4. `database/06_AddTenantMotto.sql` if the database predates household mottos

`database/01_CreateDatabase.sql` is only for optional LocalDB (`sqlcmd -v DatabaseName=...`). `database/03_SeedDev.sql` is sample `mom` / `dad` / `Home` data for local use — do not run it against a shared production database.

App admin is `Users.IsAppAdmin`. Household role is `Memberships.Role`. Those are different.

## Not built yet

- Live updates while another person checks a box (refresh for now)
- Web admin
- Application Insights
- Word / Notepad capture
- Weekly history report UI

See [PLAN.md](PLAN.md) for the longer product sketch.

## Requirements

- Windows
- .NET 9 SDK
- Node.js 22+ for the web app
- Excel for capture
- An Azure SQL database with Entra-only auth, and your Entra user as a SQL admin or contained user
