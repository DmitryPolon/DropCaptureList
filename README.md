# DropCaptureList

**Version 0.1.0** (27 Aug 2026).

Household shared list. **Windows** captures highlighted Excel cells (one cell = one record). **React** is the phone/web list: check off items, swipe to remove, household motto.

Live site: **https://droplist.azpcloud.com**.

Open `DropCaptureList.slnx` in Visual Studio.

## What works today

- WPF, .NET 9. Sign in with **email** and **household name** (not nickname). Session is stored on this PC (DPAPI) until **Sign out**.
- Azure SQL with Microsoft Entra (no SQL password in the app). First Continue may show a Windows account picker; later launches reuse a cached token.
- Excel capture via COM against the running Excel app (`ExcelSelectionCapture`). Empty cells skipped. Merged ranges count as one record.
- **Delete selected** (Windows): hard delete, for cells that should never have been captured.
- **Clear list** (Windows): mark remaining items completed; they stay in SQL and show gray.
- Temporary WPF **Admin** (app admins): add user, create household, set motto, remove from household.
- Web: letter mark + **bold motto**, Excel column layout, checkboxes, **swipe right** to remove, **Add** from the phone, **Save**, **Refresh**, **Clear completed**. There is no live SignalR feed; opening SQL is on purpose when you tap Save or Refresh.

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
| Telemetry | Application Insights in the web resource group (connection string on the App Service, not in git) | free tier unless you exceed the included volume |
| Data | Your existing Azure SQL | existing |

GitHub Actions:

- `.github/workflows/ci.yml` — build API + web on push/PR to `main`
- `.github/workflows/deploy.yml` — deploy web (and PR preview URLs) + API on `main`. Web upload is `src/web/dist` after `npm run build` (not the Vite source `index.html`).

Secrets: `AZURE_STATIC_WEB_APPS_API_TOKEN`, `AZURE_WEBAPP_PUBLISH_PROFILE`, `VITE_API_BASE`. SQL names stay in App Service settings. In Azure the API uses **Managed Identity** (run `database/07_GrantApiManagedIdentity.sql` as Entra SQL admin).

### Save / refresh, stateless API, observability

These are three different pieces:

- **Save / refresh.** There is no live SignalR feed. Windows **Save** / **Refresh** and the phone **Save** / **Refresh** buttons open SQL. Capture, Add, check, and swipe stay on the device until Save. Refresh loads the live list; completed items stay in SQL and drop off the screen.
- **Stateless HTTP.** The API does not keep a login session store. Each request sends **email + household** and checks SQL. List data is in Azure SQL. The browser keeps `localStorage`; Windows keeps `session.bin` (DPAPI).
- **Observability.** Application Insights on the App Service (connection string in app settings, not in git). Portal: the Insights resource in the same web resource group — **Live Metrics**, **Failures**, **Performance**, **Logs**. Local `dotnet run` does not send telemetry unless you add that setting to gitignored `appsettings.Local.json`.

## Database scripts

Create the database in the portal (or `database/CreateAzureSql.ps1` with names at runtime — do not commit those names). Connect with Microsoft Entra:

1. `02_CreateTables.sql`
2. `04_AddUserEmailAndAppAdmin.sql` if needed
3. `05_AddItemDisplayFormat.sql` if needed
4. `06_AddTenantMotto.sql` if needed
5. `07_GrantApiManagedIdentity.sql` for the hosted API
6. `08_AddTenantLastClearedAt.sql` so Admin can show the last Windows **Clear list** time

`01` is optional LocalDB. `03_SeedDev.sql` is fake `mom`/`dad`/`Home` — do not run on a shared production database.

`Users.IsAppAdmin` is not the same as household `Memberships.Role`.

## Not built yet

- Web admin
- Word / Notepad capture
- Weekly history report UI

See [PLAN.md](PLAN.md).

## Requirements

- Windows, .NET 9 SDK, Node.js 22+, Excel for capture
- Azure SQL with Entra-only auth
