import { FormEvent, useEffect, useMemo, useState } from "react";
import { apiUrl } from "./api";
import { HouseholdPanel } from "./HouseholdPanel";
import { connectList } from "./live";
import { sheetsFromItems } from "./replica";
import { SwipeAway } from "./SwipeAway";
import type { ListItem, Session } from "./types";
import "./App.css";

const sessionKey = "dropcapturelist.web.session";

function loadSession(): Session | null {
  try {
    const raw = localStorage.getItem(sessionKey);
    return raw ? (JSON.parse(raw) as Session) : null;
  } catch {
    return null;
  }
}

function problemMessage(body: { detail?: string; title?: string }, fallback: string) {
  return body.detail ?? body.title ?? fallback;
}

function cellStyle(item: ListItem) {
  return {
    color: item.fontColor && item.fontColor !== "#000000" ? item.fontColor : "#0f172a",
    background: item.fillColor && item.fillColor !== "#FFFFFF" ? item.fillColor : "#ffffff",
    fontWeight: item.isBold ? 700 : 400
  };
}

export default function App() {
  const [session, setSession] = useState<Session | null>(() => loadSession());
  const [email, setEmail] = useState("");
  const [household, setHousehold] = useState("");
  const [draft, setDraft] = useState("");
  const [items, setItems] = useState<ListItem[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [status, setStatus] = useState("The list is live — add, check, and swipe update everyone.");
  const [busy, setBusy] = useState(false);
  const layout = useMemo(() => sheetsFromItems(items), [items]);

  useEffect(() => {
    if (!session) {
      return;
    }
    void refresh();
    let stop = false;
    let connection: { stop: () => Promise<void> } | null = null;
    connectList(session.household, () => {
      if (!stop) {
        void refresh();
      }
    })
      .then((hub) => {
        connection = hub;
      })
      .catch(() => {
        /* Cold API — Refresh still works. */
      });
    return () => {
      stop = true;
      void connection?.stop();
    };
  }, [session?.household]);

  function signOut() {
    localStorage.removeItem(sessionKey);
    setSession(null);
    setItems([]);
    setError(null);
  }

  async function signIn(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const response = await fetch(apiUrl("/api/session"), {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, household })
      });
      const body = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(problemMessage(body, "Could not sign in."));
      }
      const next = body as Session;
      localStorage.setItem(sessionKey, JSON.stringify(next));
      setSession(next);
      setItems([]);
      setStatus("Signed in. The list is live.");
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Could not sign in.");
    } finally {
      setBusy(false);
    }
  }

  async function postAction(path: string, extra?: Record<string, string>) {
    if (!session) {
      throw new Error("Sign in first.");
    }
    const response = await fetch(apiUrl(path), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        email: session.email,
        household: session.household,
        ...extra
      })
    });
    const body = await response.json().catch(() => ({}));
    if (!response.ok) {
      throw new Error(problemMessage(body, "Could not update the list."));
    }
  }

  async function refresh() {
    if (!session) {
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const response = await fetch(
        apiUrl(`/api/households/${encodeURIComponent(session.household)}/items`)
      );
      const body = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(problemMessage(body, "Could not load the list."));
      }
      const list = (body as ListItem[]).filter((item) => !item.isCompleted);
      setItems(list);
      setStatus(
        list.length === 0 ? "Live list is empty." : `Loaded ${list.length} live items.`
      );
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Could not load the list.");
    } finally {
      setBusy(false);
    }
  }

  function addItem(event: FormEvent) {
    event.preventDefault();
    const text = draft.trim();
    if (!session || !text) {
      return;
    }
    if (items.some((item) => item.text.trim().toLowerCase() === text.toLowerCase())) {
      setStatus("Duplicate records are not saved.");
      setDraft("");
      return;
    }
    setDraft("");
    void postAction(`/api/households/${encodeURIComponent(session.household)}/items`, { text }).then(
      () => setStatus("Added. Other phones update live."),
      (err: unknown) => setError(err instanceof Error ? err.message : "Could not add the task.")
    );
  }

  function toggle(item: ListItem) {
    if (!session) {
      return;
    }
    setItems((current) => current.filter((row) => row.id !== item.id));
    void postAction(`/api/households/${encodeURIComponent(session.household)}/items/${item.id}/toggle`).then(
      () => setStatus("Done — removed for everyone."),
      (err: unknown) => setError(err instanceof Error ? err.message : "Could not update the item.")
    );
  }

  function removeItem(item: ListItem) {
    if (!session) {
      return;
    }
    setItems((current) => current.filter((row) => row.id !== item.id));
    void postAction(`/api/households/${encodeURIComponent(session.household)}/items/${item.id}/remove`).then(
      () => setStatus("Removed for everyone."),
      (err: unknown) => setError(err instanceof Error ? err.message : "Could not remove the item.")
    );
  }

  if (!session) {
    return (
      <main className="page">
        <p className="eyebrow">DropCaptureList</p>
        <h1 className="household">Household list</h1>
        <p className="hint login-hint">
          Sign in with the email registered for you, and the household name (not your nickname).
        </p>
        <form className="login" onSubmit={signIn}>
          <label>
            Email
            <input
              type="email"
              autoComplete="username"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              required
            />
          </label>
          <label>
            Household
            <input
              value={household}
              onChange={(event) => setHousehold(event.target.value)}
              required
            />
          </label>
          {error ? <p className="error">{error}</p> : null}
          <button type="submit" disabled={busy}>
            Continue
          </button>
        </form>
      </main>
    );
  }

  return (
    <main className="page">
      <header className="top">
        <p className="eyebrow">DropCaptureList · live</p>
        <div className="top-actions">
          <button type="button" className="text-button" onClick={() => void refresh()} disabled={busy}>
            Refresh
          </button>
          <button type="button" className="text-button" onClick={signOut}>
            Sign out
          </button>
        </div>
      </header>
      <section className="brand" aria-label={`${session.household} household mark`}>
        <div className="mark" aria-hidden="true">
          {session.logoLetter}
        </div>
        <div>
          <h1 className="household">{session.household}</h1>
          {session.motto ? (
            <p className="motto">{session.motto}</p>
          ) : (
            <p className="empty-motto">No motto yet. Set it under Household.</p>
          )}
          <p className="who">{session.nickname}</p>
        </div>
      </section>
      <HouseholdPanel
        session={session}
        onMotto={(motto) => {
          const next = { ...session, motto };
          localStorage.setItem(sessionKey, JSON.stringify(next));
          setSession(next);
        }}
        onError={setError}
        onStatus={setStatus}
      />
      {error ? <p className="error">{error}</p> : null}
      <p className="hint">{status}</p>

      {layout.sheets.map((sheet) => (
        <div
          key={sheet.createdAt}
          className="sheet"
          style={{ ["--cols" as string]: sheet.columnCount }}
        >
          {sheet.rows.map((row, rowIndex) => (
            <div className="sheet-row" key={`${sheet.createdAt}-${rowIndex}`}>
              {row.map((cell, colIndex) =>
                cell.item ? (
                  <SwipeAway
                    key={cell.item.id}
                    className="sheet-cell"
                    style={cellStyle(cell.item)}
                    disabled={busy}
                    onSwipeRight={() => removeItem(cell.item!)}
                  >
                    <label>
                      <input
                        type="checkbox"
                        checked={false}
                        disabled={busy}
                        onChange={() => toggle(cell.item!)}
                      />
                      <span className="item-text">{cell.item.text}</span>
                    </label>
                  </SwipeAway>
                ) : (
                  <div className="sheet-cell empty" key={`empty-${rowIndex}-${colIndex}`} />
                )
              )}
            </div>
          ))}
        </div>
      ))}

      {layout.leftover.length > 0 ? (
        <ul className="list">
          {layout.leftover.map((item) => (
            <li key={item.id}>
              <SwipeAway disabled={busy} onSwipeRight={() => removeItem(item)}>
                <label>
                  <input
                    type="checkbox"
                    checked={false}
                    disabled={busy}
                    onChange={() => toggle(item)}
                  />
                  <span>
                    <span className="item-text">{item.text}</span>
                    <span className="meta">
                      {`${item.nickname} · ${new Date(item.createdAt).toLocaleString()}`}
                    </span>
                  </span>
                </label>
              </SwipeAway>
            </li>
          ))}
        </ul>
      ) : null}

      {!busy && items.length === 0 ? (
        <p className="hint">No items yet. Add a task — it is written for everyone.</p>
      ) : (
        <p className="hint">Swipe right to remove. Check the box when it is done. Changes are live.</p>
      )}

      <form className="composer" onSubmit={addItem}>
        <label className="composer-field">
          <span className="visually-hidden">New task</span>
          <input
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            placeholder="Add a task"
            maxLength={500}
            enterKeyHint="send"
            autoComplete="off"
            disabled={busy}
          />
        </label>
        <button type="submit" disabled={busy || !draft.trim()}>
          Add
        </button>
      </form>
    </main>
  );
}
