# DropCaptureList — product plan

Household shared list. A Windows app captures highlighted Excel cells into the tenant list. A mobile web app lets household members check items off together.

## Domain

- A **tenant** is a household.
- A **user** can belong to many tenants.
- Display name on the list is a per-tenant **nickname** (for example `mom`, `dad`), not the login name.

## Live list vs completed (later)

Two different removals:

- **Delete** — cell was never meant to be on the list. Remove it. Not completed work.
- **Clear / complete** — the to-do is done. It must not show on the live list.

**This build (your use):** Delete mistakes (hard delete). Clear list marks items **completed**; they stay in the table and show gray. Weekly history report still later.

**Later (presentation / others):** keep completed rows with an **active** flag (or `CompletedAt`). Live list shows `Active = 1` only. A weekly report reads completed items. Do not add that until needed.


## Windows app (priority)

- Authenticate once; remember the session on this machine.
- Capture **text from highlighted Excel cells**.
- Persist **1 cell = 1 record**, with:
  - cell text
  - user
  - tenant
  - created timestamp
- Excel is the important capture source.

### Later (not priority)

- Word / Notepad: capture **per line break**.
- Treat as a **different capture class** from Excel cells. Do not mix the two sources in one capture path.

## Mobile web app (React)

- Authenticate once; **one session**. Returning to the site should still know who the user is.
- Task list with checkboxes. Checking an item marks it complete for the **whole tenant**.
- Other users in the tenant see the list and **live checkbox updates**.
- **Swipe left** hides an item **for that user only**. Other members still see it.
- **Remove completed** hides finished items from the live list (later: keep them for a weekly report via an active/completed flag; this build may delete them).
- Attribution:
  - Unchecked: `Milk` — nickname + created timestamp
  - Checked: `Milk` — completer nickname + completed timestamp
- Admin (web app):
  - add users
  - create tenants
  - remove a user from a household
  - reports

## Out of scope for first build (next steps)

- Telemetry and observability
- Azure App Service / container hosting
- Migration to Azure SQL

## Suggested first slice

1. Auth + tenant membership + nicknames
2. Create list items (API + React list)
3. Shared complete / uncomplete with live updates
4. Per-user hide (swipe)
5. Remove completed
6. Windows Excel capture into the same item API
7. Admin: users, tenants, reports
8. Azure hosting, SQL migration, telemetry
