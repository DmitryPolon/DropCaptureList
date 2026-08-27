# DropCaptureList

Household shared list. **Windows** captures highlighted Excel cells (one cell = one record). **React** is the phone/web list: check off items, swipe to remove, household motto.

Live site: **https://droplist.azpcloud.com** (not `azpcloud.com` — that root is unused).

Open `DropCaptureList.slnx` in Visual Studio.

## What works today

- WPF, .NET 9. Sign in with **email** and **household name** (not nickname). Session is stored on this PC (DPAPI) until **Sign out**.
- Azure SQL with Microsoft Entra (no SQL password in the app). First Continue may show a Windows account picker; later launches reuse a cached token.
- Excel capture via COM against the running Excel app (`ExcelSelectionCapture`). Empty cells skipped. Merged ranges count as one record.
- **Delete selected** (Windows): hard delete, for cells that should never have been captured.
- **Clear list** (Windows): mark remaining items completed; they stay in SQL and show gray.
- Temporary WPF **Admin** (app admins): add user, create household, set motto, remove from household.
- Web: letter mark + **bold motto**, Excel column layout, checkboxes (complete for the whole household), **swipe right** = soft delete (`IsDeleted`), **Clear completed**.

Word and Notepad capture are not in this build.

## Run locally

1. Copy `appsettings.Local.json.example` to `appsettings.Local.json` next to the Windows project and `src/api` (gitignored). Fill in Server, Database, UserId, TenantId.
2. Run SQL scripts as needed (`02`, then `04`–`07` if those columns/users are missing).
3. `dotnet run --project src/api --launch-profile http`
4. `npm install` then `npm run dev` in `src/web`
5. http://localhost:5173 (same Wi‑Fi: Vite prints a LAN URL; `host: true` is on)

## Hosting and CI/CD

| Piece | Where | Cost |
| --- | --- | --- |
| React | Azure Static Web Apps Free → `droplist.azpcloud.com` | $0 |
| API | App Service Linux F1 `droplist-azpcloud-api` | $0 (sleeps when idle) |
| Data | Your existing Azure SQL | existing |

GitHub Actions:

- `.github/workflows/ci.yml` — build API + web on push/PR to `main`
- `.github/workflows/deploy.yml` — deploy web (and PR preview URLs) + API on `main`

Secrets: `AZURE_STATIC_WEB_APPS_API_TOKEN`, `AZURE_WEBAPP_PUBLISH_PROFILE`, `VITE_API_BASE`. SQL names stay in App Service settings. In Azure the API uses **Managed Identity** (run `database/07_GrantApiManagedIdentity.sql` as Entra SQL admin).

### GoDaddy (already done)

CNAME **Name** `droplist` → `witty-beach-05c8f2e1e.7.azurestaticapps.net`. Do not use Forwarding. Leave `@` / `www` alone.

## Database scripts

Create the database in the portal (or `database/CreateAzureSql.ps1` with names at runtime — do not commit those names). Connect with Microsoft Entra:

1. `02_CreateTables.sql`
2. `04_AddUserEmailAndAppAdmin.sql` if needed
3. `05_AddItemDisplayFormat.sql` if needed
4. `06_AddTenantMotto.sql` if needed
5. `07_GrantApiManagedIdentity.sql` for the hosted API

`01` is optional LocalDB. `03_SeedDev.sql` is fake `mom`/`dad`/`Home` — do not run on a shared production database.

`Users.IsAppAdmin` is not the same as household `Memberships.Role`.

## Not built yet

- Live updates without refresh
- Web admin
- Application Insights
- Word / Notepad capture
- Weekly history report UI

See [PLAN.md](PLAN.md).

## Requirements

- Windows, .NET 9 SDK, Node.js 22+, Excel for capture
- Azure SQL with Entra-only auth
