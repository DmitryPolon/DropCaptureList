# DropCaptureList — plan

Household shared list. Windows captures highlighted Excel cells. Phone/web checks items off, swipes them away, and adds tasks. Data is JSON on the API. SignalR reloads everyone after a write.

This is the product. Once the bugs are out and that loop works in real use, we are done.

## What it is

- A **household** has a name, motto, members, and one live list.
- Sign-in is **email + household name**. Display name on the list is the **nickname**.
- An **app admin** creates or deletes households (first member by email). Household members add and remove members themselves.
- Capture: one Excel cell = one row. Stays on the PC until **Save**.
- Phone: add, check, and swipe write immediately and **delete** the row. Nothing is archived.
- File store on the API (`/home/droplist` on Azure). One F1 instance. In-process lock.

## Remaining

- Fix bugs.
- Confirm Windows capture → Save, phone add/check/swipe, and live SignalR updates all work together on a real shop trip.

No Word/Notepad capture, no weekly history, no Azure SQL, no extra admin reports.
