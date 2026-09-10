# DropCaptureList

Household shared list. **Windows** captures highlighted Excel cells (one cell = one row). **React** is the phone/web list: check off items, swipe to remove, add a task, household motto.

Live site: **https://droplist.azpcloud.com**.

Open `DropCaptureList.slnx` in Visual Studio.

## What works today

- WPF, .NET 9. Sign in with **email**, **household name**, and a **four-digit household PIN**. Session is stored on this PC (DPAPI) until **Sign out**.
- Shared data is a **file store** on the API (JSON). There is no Azure SQL in the app.
- Excel capture via COM against the running Excel app (`ExcelSelectionCapture`). Empty cells skipped. Merged ranges count as one record. Capture stays on this PC until **Save**.
- Phone add / check / swipe write immediately. Check and swipe **delete** the row (nothing is archived).
- **Household** on Windows and the web: any member can add or remove members by email. An **app admin** can create a household (with a first member email), delete a household, or change that household’s PIN.
- SignalR keeps phones and Windows in sync after a write.

Word and Notepad capture are not in this build.

## File store

On Azure App Service the files live under `/home/droplist` (set `DataDirectory` locally if you want a custom folder).

- `/home/droplist/users.json` — emails and app-admin flags
- `/home/droplist/households/{household-name}/household.json` — members, motto, items
- `/home/droplist/mode.json` — leftover from the old Azure/File switch; unused now

Those files are the database. They are not in git. If they are missing on the API the site still starts, but **sign-in fails** until an app admin creates a household (first member by email), which writes `users.json` and the household folder.

Windows and the phone talk to whatever `ApiBase` / `VITE_API_BASE` points at. Point them at the hosted API and they use `/home/droplist`. Point them at a local `dotnet run` API and they use `%LocalAppData%\DropCaptureList\file-store` (or `DataDirectory` in `src/api/appsettings.Local.json`). Those two folders are not the same store.

Writes use a temp file then `File.Move` so a crash mid-write does not leave a half JSON file.

## SignalR

The hub is `/hubs/list`. After a client joins with email, household, and PIN, it sits in a group for that household.

Whenever the API writes the household file (add, check, swipe, Save from Windows, clear), it sends `listChanged` to that group. The phone and Windows reload the list. There is no push of the list payload — only a “reload” ping.

A connected tab or Windows window also keeps the F1 App Service from sleeping. After ~20 minutes idle the API can still go cold; the next HTTP or reconnect wakes it.

## Locking and concurrency

`FileDirectory` takes a **single in-process lock** around load and save. That serializes requests on **one** API instance.

This is not a cross-process or cross-machine file lock. The hosted API is one Linux F1 instance. Do not scale out to multiple instances against the same folder: two processes could interleave reads and writes and last write would win.

Windows **Save** sends the whole in-memory capture in one bulk request, so that write is one locked update. Phone edits are one item at a time. Two people checking different rows is fine. Two people editing the same row at the same instant: the later write wins.

## Run locally

1. Copy `appsettings.Local.json.example` to `appsettings.Local.json` next to the Windows project (set `ApiBase`) and optionally under `src/api` (`DataDirectory` and `Household:DefaultPin`). Do not commit those files.
2. `dotnet run --project src/api --launch-profile http`
3. `npm install` then `npm run dev` in `src/web`
4. http://localhost:5173 (same Wi‑Fi: Vite prints a LAN URL; `host: true` is on)

The first household on an empty store: call create-household with the first member email (that person becomes app admin). After that, only an app admin can create or delete households.

## Hosting and CI/CD

| Piece | Where | Cost |
| --- | --- | --- |
| React | Azure Static Web Apps Free → `droplist.azpcloud.com` | $0 |
| API | App Service Linux F1 `droplist-azpcloud-api` | $0 (sleeps when idle) |
| Telemetry | Application Insights in the web resource group (connection string on the App Service, not in git) | free tier unless you exceed the included volume |
| Data | JSON files on the API disk | included |

GitHub Actions:

- `.github/workflows/ci.yml` — build API + web on push/PR to `main`
- `.github/workflows/deploy.yml` — deploy web (and PR preview URLs) + API on `main`. Web upload is `src/web/dist` after `npm run build` (not the Vite source `index.html`).

Secrets: `AZURE_STATIC_WEB_APPS_API_TOKEN`, `AZURE_WEBAPP_PUBLISH_PROFILE`, `VITE_API_BASE`.

On the API App Service, set application setting `Household__DefaultPin` (four digits). The same value lives in gitignored `src/api/appsettings.Local.json` for local runs. An app admin sees that initial PIN in the Household panel after sign-in. It is not in the git repo.

The API does not keep a login session store. Each request sends **email + household + PIN**. The browser keeps `localStorage`; Windows keeps `session.bin` (DPAPI). The PIN is hashed (salt + SHA-256) in `household.json`; it is not stored as plain text on the server.

Application Insights: portal **Live Metrics**, **Failures**, **Performance**, **Logs**. Local `dotnet run` does not send telemetry unless you add that setting to gitignored `appsettings.Local.json`.

The `database/` folder is leftover Azure SQL scripts. The app does not use them.

See [PLAN.md](PLAN.md).

## Requirements

- Windows, .NET 9 SDK, Node.js 22+, Excel for capture
- API reachable from the phone and from Windows (`ApiBase`)
