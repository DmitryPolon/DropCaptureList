import { FormEvent, useEffect, useState } from "react";
import { apiUrl } from "./api";
import type { HouseholdDirectory, Member, Session } from "./types";

function problemMessage(body: { detail?: string; title?: string }, fallback: string) {
  return body.detail ?? body.title ?? fallback;
}

type Props = {
  session: Session;
  onMotto: (motto: string) => void;
  onError: (message: string | null) => void;
  onStatus: (message: string) => void;
};

export function HouseholdPanel({ session, onMotto, onError, onStatus }: Props) {
  const [open, setOpen] = useState(false);
  const [members, setMembers] = useState<Member[]>([]);
  const [houses, setHouses] = useState<HouseholdDirectory[]>([]);
  const [selectedHouse, setSelectedHouse] = useState("");
  const [email, setEmail] = useState("");
  const [nickname, setNickname] = useState("");
  const [motto, setMotto] = useState(session.motto);
  const [houseName, setHouseName] = useState("");
  const [firstEmail, setFirstEmail] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (open) {
      if (session.isAppAdmin) {
        void loadHouses();
      }
      void loadMembers(selectedHouse || session.household);
    }
  }, [open, session.household, session.isAppAdmin, selectedHouse]);

  async function loadHouses() {
    try {
      const response = await fetch(
        apiUrl(`/api/admin/directory?email=${encodeURIComponent(session.email)}`)
      );
      const body = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(problemMessage(body, "Could not load households."));
      }
      setHouses(body as HouseholdDirectory[]);
    } catch (err: unknown) {
      onError(err instanceof Error ? err.message : "Could not load households.");
    }
  }

  async function loadMembers(household: string) {
    try {
      const response = await fetch(
        apiUrl(
          `/api/members?email=${encodeURIComponent(session.email)}&household=${encodeURIComponent(household)}`
        )
      );
      const body = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(problemMessage(body, "Could not load members."));
      }
      setMembers(body as Member[]);
    } catch (err: unknown) {
      onError(err instanceof Error ? err.message : "Could not load members.");
    }
  }

  async function post(path: string, payload: Record<string, unknown>) {
    const response = await fetch(apiUrl(path), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    const body = await response.json().catch(() => ({}));
    if (!response.ok) {
      throw new Error(problemMessage(body, "Could not update household."));
    }
    return body;
  }

  async function addMember(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    onError(null);
    try {
      const house = selectedHouse || session.household;
      const body = await post("/api/members", {
        actorEmail: session.email,
        household: house,
        email,
        nickname
      });
      setMembers(body as Member[]);
      setEmail("");
      setNickname("");
      onStatus("Member added. They sign in with that email and this household name.");
    } catch (err: unknown) {
      onError(err instanceof Error ? err.message : "Could not add the member.");
    } finally {
      setBusy(false);
    }
  }

  async function removeMember(member: Member) {
    if (!window.confirm(`Remove ${member.nickname || member.email} from this household?`)) {
      return;
    }
    setBusy(true);
    onError(null);
    try {
      const house = selectedHouse || session.household;
      const body = await post("/api/members/remove", {
        actorEmail: session.email,
        household: house,
        userId: member.userId
      });
      setMembers(body as Member[]);
      onStatus("Removed from this household.");
    } catch (err: unknown) {
      onError(err instanceof Error ? err.message : "Could not remove the member.");
    } finally {
      setBusy(false);
    }
  }

  async function saveMotto(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    onError(null);
    try {
      await post("/api/admin/motto", {
        actorEmail: session.email,
        household: session.household,
        motto
      });
      onMotto(motto);
      onStatus(motto.trim() ? "Motto saved." : "Motto cleared.");
    } catch (err: unknown) {
      onError(err instanceof Error ? err.message : "Could not save the motto.");
    } finally {
      setBusy(false);
    }
  }

  async function createHousehold(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    onError(null);
    try {
      await post("/api/admin/households", {
        actorEmail: session.email,
        name: houseName,
        motto: "",
        memberEmail: firstEmail,
        memberNickname: ""
      });
      setHouseName("");
      setFirstEmail("");
      onStatus("Household created. That email can sign in with the household name.");
      await loadHouses();
    } catch (err: unknown) {
      onError(err instanceof Error ? err.message : "Could not create the household.");
    } finally {
      setBusy(false);
    }
  }

  async function addFirstToSelected(event: FormEvent) {
    event.preventDefault();
    const house = selectedHouse || houses[0]?.name;
    if (!house) {
      onError("Select a household.");
      return;
    }
    setBusy(true);
    onError(null);
    try {
      await post("/api/members", {
        actorEmail: session.email,
        household: house,
        email: firstEmail,
        nickname: ""
      });
      setFirstEmail("");
      onStatus(`Added to ${house}.`);
      await loadMembers(house);
    } catch (err: unknown) {
      onError(err instanceof Error ? err.message : "Could not add the member.");
    } finally {
      setBusy(false);
    }
  }

  async function deleteHousehold() {
    const house = selectedHouse;
    if (!house) {
      onError("Select a household to delete.");
      return;
    }
    if (!window.confirm(`Delete household “${house}”? The list and members are removed.`)) {
      return;
    }
    setBusy(true);
    onError(null);
    try {
      await post("/api/admin/households/delete", {
        actorEmail: session.email,
        name: house
      });
      setSelectedHouse("");
      onStatus("Household deleted.");
      await loadHouses();
    } catch (err: unknown) {
      onError(err instanceof Error ? err.message : "Could not delete the household.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="household-admin">
      <button type="button" className="text-button" onClick={() => setOpen((value) => !value)}>
        {open ? "Hide household" : "Household"}
      </button>
      {open ? (
        <div className="admin-card">
          {session.isAppAdmin ? (
            <>
              <h2 className="admin-heading">Households</h2>
              <ul className="member-list">
                {houses.map((house) => (
                  <li key={house.name}>
                    <button
                      type="button"
                      className={selectedHouse === house.name ? "house-pick on" : "house-pick"}
                      onClick={() => setSelectedHouse(house.name)}
                    >
                      {house.name}
                      {house.emails ? ` · ${house.emails}` : ""}
                    </button>
                  </li>
                ))}
              </ul>
              <form className="admin-row" onSubmit={createHousehold}>
                <input value={houseName} onChange={(event) => setHouseName(event.target.value)} placeholder="Household name" required />
                <input
                  type="email"
                  value={firstEmail}
                  onChange={(event) => setFirstEmail(event.target.value)}
                  placeholder="First member email"
                  required
                />
                <button type="submit" disabled={busy}>
                  Create
                </button>
              </form>
              <div className="admin-row">
                <button type="button" disabled={busy} onClick={(event) => void addFirstToSelected(event)}>
                  Add email to selected
                </button>
                <button type="button" disabled={busy} onClick={() => void deleteHousehold()}>
                  Delete selected
                </button>
              </div>
            </>
          ) : null}
          <p className="hint">
            Emails in {selectedHouse || session.household}. Anyone in that household can add or remove members.
          </p>
          <form className="admin-row" onSubmit={saveMotto}>
            <input value={motto} onChange={(event) => setMotto(event.target.value)} placeholder="Motto" maxLength={120} />
            <button type="submit" disabled={busy}>
              Save motto
            </button>
          </form>
          <form className="admin-row" onSubmit={addMember}>
            <input type="email" value={email} onChange={(event) => setEmail(event.target.value)} placeholder="Member email" required />
            <input value={nickname} onChange={(event) => setNickname(event.target.value)} placeholder="Nickname" required />
            <button type="submit" disabled={busy}>
              Add
            </button>
          </form>
          <ul className="member-list">
            {members.map((member) => (
              <li key={member.userId}>
                <span>
                  {member.nickname}
                  {member.email ? ` · ${member.email}` : ""}
                  {member.isAppAdmin ? " · app admin" : ""}
                </span>
                <button type="button" className="text-button" disabled={busy} onClick={() => void removeMember(member)}>
                  Remove
                </button>
              </li>
            ))}
          </ul>
        </div>
      ) : null}
    </section>
  );
}
