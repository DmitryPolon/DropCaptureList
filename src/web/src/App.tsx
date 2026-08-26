import { useEffect, useMemo, useState } from "react";
import "./App.css";

type HouseholdBrand = {
  name: string;
  motto: string;
  logoLetter: string;
};

export default function App() {
  const [households, setHouseholds] = useState<HouseholdBrand[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    fetch("/api/households")
      .then(async (response) => {
        if (!response.ok) {
          const body = await response.json().catch(() => ({}));
          throw new Error(body.detail ?? body.title ?? `Could not load households (${response.status}).`);
        }
        return response.json() as Promise<HouseholdBrand[]>;
      })
      .then((list) => {
        if (cancelled) {
          return;
        }
        setHouseholds(list);
        const fromQuery = new URLSearchParams(window.location.search).get("household");
        const match = list.find((h) => h.name.toLowerCase() === fromQuery?.toLowerCase());
        setSelected(match?.name ?? list[0]?.name ?? null);
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : "Could not load households.");
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const current = useMemo(
    () => households.find((h) => h.name === selected) ?? null,
    [households, selected]
  );

  return (
    <main className="page">
      <p className="eyebrow">DropCaptureList</p>
      {loading ? <p className="hint">Loading household mark…</p> : null}
      {error ? <p className="error">{error}</p> : null}
      {current ? (
        <section className="brand" aria-label={`${current.name} household mark`}>
          <div className="mark" aria-hidden="true">
            {current.logoLetter}
          </div>
          <div>
            <h1 className="household">{current.name}</h1>
            {current.motto ? (
              <p className="motto">{current.motto}</p>
            ) : (
              <p className="empty-motto">No motto yet. Set it in the Windows admin window.</p>
            )}
          </div>
        </section>
      ) : null}
      {households.length > 1 ? (
        <nav className="picker" aria-label="Households">
          {households.map((household) => (
            <button
              key={household.name}
              type="button"
              aria-current={household.name === selected ? "true" : undefined}
              onClick={() => setSelected(household.name)}
            >
              {household.name}
            </button>
          ))}
        </nav>
      ) : null}
      <p className="hint">The live list comes next. Excel capture stays on the Windows app.</p>
    </main>
  );
}
