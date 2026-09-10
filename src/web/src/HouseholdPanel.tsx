import { FormEvent, useEffect, useState } from "react";
import { apiUrl } from "./api";
import type { Member, Session } from "./types";

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
  const [email, setEmail] = useState("");
  const [nickname, setNickname] = useState("");
  const [motto, setMotto] = useState(session.motto);
  const [houseName, setHouseName] = useState("");
  const [houseMotto, setHouseMotto] = useState("");
  const [firstEmail, setFirstEmail] = useState("");
  const [firstNickname, setFirstNickname] = useState("");
  const [deleteName, setDeleteName] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (open) {
      void loadMembers();
    }
  }, [open, session.household]);

  async function loadMembers() {
    try {
      const response = await fetch(
        apiUrl(
          `/api/members?email=${encodeURIComponent(session.email)}&household=${encodeURIComponent(session.household)}`
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
      const body = await post("/api/members", {
        actorEmail: session.email,
        household: session.household,
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
      const body = await post("/api/members/remove", {
        actorEmail: session.email,
        household: session.household,
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
        motto: houseMotto,
        memberEmail: firstEmail,
        memberNickname: firstNickname
      });
      setHouseName("");
      setHouseMotto("");
      setFirstEmail("");
      setFirstNickname("");
      onStatus("Household created with the first member.");
    } catch (err: unknown) {
      onError(err instanceof Error ? err.message : "Could not create the household.");
    } finally {
      setBusy(false);
    }
  }

  async function deleteHousehold(event: FormEvent) {
    event.preventDefault();
    if (!window.confirm(`Delete household “${deleteName}”? The list and members are removed.`)) {
      return;
    }
    setBusy(true);
    onError(null);
    try {
      await post("/api/admin/households/delete", {
        actorEmail: session.email,
        name: deleteName
      });
      setDeleteName("");
      onStatus("Household deleted.");
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
          <p className="hint">
            Anyone here can add or remove members. Sign-in is email plus the household name.
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
          {session.isAppAdmin ? (
            <>
              <h2 className="admin-heading">App admin</h2>
              <form className="admin-stack" onSubmit={createHousehold}>
                <input value={houseName} onChange={(event) => setHouseName(event.target.value)} placeholder="New household name" required />
                <input value={houseMotto} onChange={(event) => setHouseMotto(event.target.value)} placeholder="Motto (optional)" />
                <input type="email" value={firstEmail} onChange={(event) => setFirstEmail(event.target.value)} placeholder="First member email" required />
                <input value={firstNickname} onChange={(event) => setFirstNickname(event.target.value)} placeholder="First member nickname" required />
                <button type="submit" disabled={busy}>
                  Create household
                </button>
              </form>
              <form className="admin-row" onSubmit={deleteHousehold}>
                <input value={deleteName} onChange={(event) => setDeleteName(event.target.value)} placeholder="Household to delete" required />
                <button type="submit" disabled={busy}>
                  Delete
                </button>
              </form>
            </>
          ) : null}
        </div>
      ) : null}
    </section>
  );
}
