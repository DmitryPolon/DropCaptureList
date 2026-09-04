import { FormEvent, useEffect, useMemo, useRef, useState } from "react";
import { apiUrl } from "./api";
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

function liveItems(list: ListItem[]) {
  return list.filter((item) => !item.isCompleted);
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
  const [status, setStatus] = useState("Tap Refresh to load the list. Add, check, and swipe stay on this phone until Save.");
  const [busy, setBusy] = useState(false);
  const [fileMode, setFileMode] = useState(false);
  const baselineRef = useRef<ListItem[]>([]);
  const pendingCompleteRef = useRef(new Set<string>());
  const pendingRemoveRef = useRef(new Set<string>());
  const layout = useMemo(() => sheetsFromItems(items), [items]);

  useEffect(() => {
    let cancelled = false;
    async function readMode() {
      try {
        const response = await fetch(apiUrl("/api/storage-mode"));
        const body = (await response.json()) as { mode?: string };
        if (!cancelled) {
          setFileMode(body.mode === "File");
        }
      } catch {
        if (!cancelled) {
          setFileMode(false);
        }
      }
    }
    void readMode();
    const onVis = () => {
      if (document.visibilityState === "visible") {
        void readMode();
      }
    };
    document.addEventListener("visibilitychange", onVis);
    return () => {
      cancelled = true;
      document.removeEventListener("visibilitychange", onVis);
    };
  }, []);

  useEffect(() => {
    if (!fileMode || !session) {
      return;
    }
    setStatus("File mode. The list is live — add, check, and swipe update everyone.");
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
  }, [fileMode, session?.household]);

  function resetPending() {
    pendingCompleteRef.current = new Set();
    pendingRemoveRef.current = new Set();
  }

  function signOut() {
    localStorage.removeItem(sessionKey);
    setSession(null);
    setItems([]);
    baselineRef.current = [];
    resetPending();
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
      baselineRef.current = [];
      resetPending();
      setStatus("Signed in. Tap Refresh to load the list.");
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
      const list = liveItems(body as ListItem[]);
      setItems(list);
      baselineRef.current = list;
      resetPending();
      setStatus(
        list.length === 0
          ? "Live list is empty. Completed items stay in the database and are not shown."
          : `Loaded ${list.length} live items. Completed rows are not shown.`
      );
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Could not load the list.");
    } finally {
      setBusy(false);
    }
  }

  async function save() {
    if (!session) {
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const baseline = baselineRef.current;
      const baselineIds = new Set(baseline.map((item) => item.id));
      let added = 0;
      let completed = 0;
      let removed = 0;

      for (const item of items) {
        if (!baselineIds.has(item.id) && !item.isCompleted) {
          await postAction(`/api/households/${encodeURIComponent(session.household)}/items`, {
            text: item.text
          });
          added++;
        }
      }

      for (const id of pendingCompleteRef.current) {
        if (baselineIds.has(id) && !pendingRemoveRef.current.has(id)) {
          await postAction(
            `/api/households/${encodeURIComponent(session.household)}/items/${id}/toggle`
          );
          completed++;
        }
      }

      for (const id of pendingRemoveRef.current) {
        if (baselineIds.has(id)) {
          await postAction(
            `/api/households/${encodeURIComponent(session.household)}/items/${id}/remove`
          );
          removed++;
        }
      }

      const response = await fetch(
        apiUrl(`/api/households/${encodeURIComponent(session.household)}/items`)
      );
      const body = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(problemMessage(body, "Could not load the list."));
      }
      const list = liveItems(body as ListItem[]);
      setItems(list);
      baselineRef.current = list;
      resetPending();
      setStatus(`Saved. Added ${added}, completed ${completed}, removed ${removed}.`);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Could not save the list.");
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
    const now = new Date().toISOString();
    setItems((current) => [
      ...current,
      {
        id: crypto.randomUUID(),
        text,
        nickname: session.nickname,
        createdAt: now,
        isCompleted: false,
        completedByNickname: null,
        completedAt: null,
        excelRow: 0,
        excelColumn: 0,
        isBold: false,
        fontColor: null,
        fillColor: null
      }
    ]);
    setDraft("");
    if (fileMode) {
      void postAction(`/api/households/${encodeURIComponent(session.household)}/items`, { text }).then(
        () => setStatus("Added. Other phones update live."),
        (err: unknown) => setError(err instanceof Error ? err.message : "Could not add the task.")
      );
      return;
    }

    setStatus("Added locally. Save to write the database.");
  }

  function toggle(item: ListItem) {
    const completing = !item.isCompleted;
    setItems((current) =>
      current.map((row) =>
        row.id === item.id ? { ...row, isCompleted: completing } : row
      )
    );
    if (completing) {
      pendingCompleteRef.current.add(item.id);
    } else {
      pendingCompleteRef.current.delete(item.id);
    }
    if (fileMode && completing && session) {
      void postAction(`/api/households/${encodeURIComponent(session.household)}/items/${item.id}/toggle`).then(
        () => {
          setItems((current) => current.filter((row) => row.id !== item.id));
          setStatus("Done — removed for everyone.");
        },
        (err: unknown) => setError(err instanceof Error ? err.message : "Could not update the item.")
      );
      return;
    }

    setStatus("Check stays on this phone until Save.");
  }

  function removeItem(item: ListItem) {
    pendingRemoveRef.current.add(item.id);
    pendingCompleteRef.current.delete(item.id);
    setItems((current) => current.filter((row) => row.id !== item.id));
    if (fileMode && session) {
      void postAction(`/api/households/${encodeURIComponent(session.household)}/items/${item.id}/remove`).then(
        () => setStatus("Removed for everyone."),
        (err: unknown) => setError(err instanceof Error ? err.message : "Could not remove the item.")
      );
      return;
    }

    setStatus("Removed locally. Save to write the database.");
  }

  function clearCompleted() {
    if (!items.some((item) => item.isCompleted)) {
      return;
    }
    if (!window.confirm("Hide completed items on this phone? Save writes them as done in the database.")) {
      return;
    }
    const baselineIds = new Set(baselineRef.current.map((item) => item.id));
    for (const item of items) {
      if (item.isCompleted && baselineIds.has(item.id)) {
        pendingCompleteRef.current.add(item.id);
      }
    }
    setItems((current) => current.filter((item) => !item.isCompleted));
    setStatus("Completed items hidden here. Save to write the database.");
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
        <p className="eyebrow">{fileMode ? "DropCaptureList · File live" : "DropCaptureList"}</p>
        <div className="top-actions">
          <button type="button" className="text-button" onClick={() => void refresh()} disabled={busy}>
            Refresh
          </button>
          {fileMode ? null : (
          <button type="button" className="text-button" onClick={() => void save()} disabled={busy}>
            Save
          </button>
          )}
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
        <p className="hint">No items on this phone yet. Refresh to load, or Add a task and Save.</p>
      ) : (
        <p className="hint">Swipe right to remove. Check the box when it is done. Save writes the database.</p>
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
        <button type="button" disabled={busy} onClick={() => void save()}>
          Save
        </button>
      </form>
    </main>
  );
}
