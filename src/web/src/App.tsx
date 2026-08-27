import { FormEvent, useEffect, useMemo, useState } from "react";
import { apiUrl } from "./api";
import { connectListHub } from "./live";
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
  if (item.isCompleted) {
    return { color: "#94a3b8", background: "#e2e8f0" };
  }

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
  const [busy, setBusy] = useState(false);
  const layout = useMemo(() => sheetsFromItems(items), [items]);

  useEffect(() => {
    if (!session) {
      return;
    }
    const current = session;
    let cancelled = false;

    function load(silent: boolean) {
      if (!silent) {
        setBusy(true);
      }
      fetch(apiUrl(`/api/households/${encodeURIComponent(current.household)}/items`))
        .then(async (response) => {
          if (!response.ok) {
            const body = await response.json().catch(() => ({}));
            throw new Error(problemMessage(body, "Could not load the list."));
          }
          return response.json() as Promise<ListItem[]>;
        })
        .then((list) => {
          if (!cancelled) {
            setItems(list);
            setError(null);
          }
        })
        .catch((err: unknown) => {
          if (!cancelled && !silent) {
            setError(err instanceof Error ? err.message : "Could not load the list.");
          }
        })
        .finally(() => {
          if (!cancelled && !silent) {
            setBusy(false);
          }
        });
    }

    load(false);
    const disconnect = connectListHub(current.email, current.household, () => load(true));
    return () => {
      cancelled = true;
      disconnect();
    };
  }, [session]);

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
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Could not sign in.");
    } finally {
      setBusy(false);
    }
  }

  async function postList(path: string, extra?: Record<string, string>) {
    if (!session) {
      return false;
    }
    setBusy(true);
    setError(null);
    try {
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
      setItems(body as ListItem[]);
      return true;
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Could not update the list.");
      return false;
    } finally {
      setBusy(false);
    }
  }

  async function addItem(event: FormEvent) {
    event.preventDefault();
    const text = draft.trim();
    if (!session || !text) {
      return;
    }
    const ok = await postList(`/api/households/${encodeURIComponent(session.household)}/items`, {
      text
    });
    if (ok) {
      setDraft("");
    }
  }

  function toggle(item: ListItem) {
    void postList(
      `/api/households/${encodeURIComponent(session!.household)}/items/${item.id}/toggle`
    );
  }

  function removeItem(item: ListItem) {
    void postList(
      `/api/households/${encodeURIComponent(session!.household)}/items/${item.id}/remove`
    );
  }

  function clearCompleted() {
    if (!session || !items.some((item) => item.isCompleted)) {
      return;
    }
    if (!window.confirm("Remove completed items from the live list? They stay in the database.")) {
      return;
    }
    void postList(`/api/households/${encodeURIComponent(session.household)}/completed/clear`);
  }

  if (!session) {
    return (
      <main className="page">
        <p className="eyebrow">DropCaptureList</p>
        <h1 className="household">Household list</h1>
        <p className="hint login-hint">
          Sign in with the email an app admin registered, and the household name (not your nickname).
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
        <p className="eyebrow">DropCaptureList</p>
        <div className="top-actions">
          {items.some((item) => item.isCompleted) ? (
            <button type="button" className="text-button" onClick={clearCompleted} disabled={busy}>
              Clear completed
            </button>
          ) : null}
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
            <p className="empty-motto">No motto yet. Set it in the Windows admin window.</p>
          )}
          <p className="who">{session.nickname}</p>
        </div>
      </section>
      {error ? <p className="error">{error}</p> : null}

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
                    className={cell.item.isCompleted ? "sheet-cell done" : "sheet-cell"}
                    style={cellStyle(cell.item)}
                    disabled={busy}
                    onSwipeRight={() => removeItem(cell.item!)}
                  >
                    <label>
                      <input
                        type="checkbox"
                        checked={cell.item.isCompleted}
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
            <li key={item.id} className={item.isCompleted ? "done" : undefined}>
              <SwipeAway disabled={busy} onSwipeRight={() => removeItem(item)}>
                <label>
                  <input
                    type="checkbox"
                    checked={item.isCompleted}
                    disabled={busy}
                    onChange={() => toggle(item)}
                  />
                  <span>
                    <span className="item-text">{item.text}</span>
                    <span className="meta">
                      {item.isCompleted
                        ? `${item.completedByNickname ?? "someone"} · ${item.completedAt ? new Date(item.completedAt).toLocaleString() : ""}`
                        : `${item.nickname} · ${new Date(item.createdAt).toLocaleString()}`}
                    </span>
                  </span>
                </label>
              </SwipeAway>
            </li>
          ))}
        </ul>
      ) : null}

      {!busy && items.length === 0 ? (
        <p className="hint">No items yet. Add a task below, or capture cells from Excel on Windows.</p>
      ) : (
        <p className="hint">Swipe right on an item to remove it. Check the box when it is done.</p>
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
