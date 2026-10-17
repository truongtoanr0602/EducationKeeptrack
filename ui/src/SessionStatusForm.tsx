import { useState, type FormEvent } from "react";
import { api, ApiError } from "./api";
import {
  sessionStatusNames,
  type ClassSession,
  type SessionStatus,
} from "./session-api";

export function SessionStatusForm({
  classId,
  session,
  canWrite,
  onSaved,
  onDenied,
}: {
  classId: string;
  session: ClassSession;
  canWrite: boolean;
  onSaved: () => void;
  onDenied: (error: ApiError) => void;
}) {
  const [status, setStatus] = useState(session.status);
  const [reason, setReason] = useState(session.reason ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  async function submit(event: FormEvent) {
    event.preventDefault();
    setError("");
    if (status === "cancelled" && !reason.trim()) {
      setError("Vui lòng nhập lý do hủy buổi.");
      return;
    }
    setBusy(true);
    try {
      await api(`/classes/${classId}/sessions/${session.id}`, "PUT", {
        status,
        reason,
        version: session.version,
      });
      onSaved();
    } catch (e) {
      setError(
        e instanceof ApiError
          ? Object.values(e.errors ?? {})
              .flat()
              .join(" ") || e.message
          : "Không thể lưu trạng thái buổi.",
      );
      if (e instanceof ApiError) onDenied(e);
    } finally {
      setBusy(false);
    }
  }
  return (
    <form
      className="session-status-form"
      onSubmit={submit}
      noValidate
      aria-busy={busy}
    >
      <label>
        Trạng thái buổi
        <select
          value={status}
          disabled={busy || !canWrite || session.status === "cancelled"}
          onChange={(e) => setStatus(e.target.value as SessionStatus)}
        >
          {(Object.keys(sessionStatusNames) as SessionStatus[]).map((s) => (
            <option value={s} key={s}>
              {sessionStatusNames[s]}
            </option>
          ))}
        </select>
      </label>
      <label>
        Lý do thay đổi / hủy buổi
        <input
          value={reason}
          maxLength={1000}
          disabled={busy || !canWrite || session.status === "cancelled"}
          onChange={(e) => setReason(e.target.value)}
        />
      </label>
      <button
        disabled={
          busy ||
          !canWrite ||
          session.status === "cancelled" ||
          session.status === status
        }
      >
        {busy ? "Đang lưu…" : "Lưu trạng thái buổi"}
      </button>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
    </form>
  );
}
