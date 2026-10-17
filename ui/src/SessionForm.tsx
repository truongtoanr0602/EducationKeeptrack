import { useState, type FormEvent } from "react";
import { api, ApiError } from "./api";
import { today } from "./roster-api";
import { sessionLabel, type ClassSession } from "./session-api";

export function SessionForm({
  classId,
  sessions,
  source,
  canWrite,
  onSaved,
  onCancel,
  onDenied,
}: {
  classId: string;
  sessions: ClassSession[];
  source?: ClassSession;
  canWrite: boolean;
  onSaved: () => void;
  onCancel: () => void;
  onDenied: (error: ApiError) => void;
}) {
  const [date, setDate] = useState(today);
  const [startTime, setStartTime] = useState(
    source?.startTime.slice(0, 5) ?? "18:00",
  );
  const [endTime, setEndTime] = useState(
    source?.endTime.slice(0, 5) ?? "19:30",
  );
  const [kind, setKind] = useState(source ? "makeup" : "regular");
  const [replacesSessionId, setSource] = useState(source?.id ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  async function submit(event: FormEvent) {
    event.preventDefault();
    setError("");
    if (!date || !startTime || !endTime || startTime >= endTime) {
      setError("Vui lòng nhập ngày và khung giờ hợp lệ trong cùng ngày.");
      return;
    }
    setBusy(true);
    try {
      await api(`/classes/${classId}/sessions`, "POST", {
        date,
        startTime: `${startTime}:00`,
        endTime: `${endTime}:00`,
        kind,
        replacesSessionId:
          kind === "makeup" && replacesSessionId ? replacesSessionId : null,
      });
      onSaved();
    } catch (e) {
      setError(
        e instanceof ApiError
          ? Object.values(e.errors ?? {})
              .flat()
              .join(" ") || e.message
          : "Không thể tạo buổi học. Vui lòng thử lại.",
      );
      if (e instanceof ApiError) onDenied(e);
    } finally {
      setBusy(false);
    }
  }
  return (
    <form className="roster-form" onSubmit={submit} noValidate aria-busy={busy}>
      <h3>Tạo buổi riêng / học bù</h3>
      <div className="field">
        <label htmlFor="session-date">Ngày buổi học</label>
        <input
          id="session-date"
          type="date"
          value={date}
          disabled={busy}
          onChange={(e) => setDate(e.target.value)}
        />
      </div>
      <div className="field">
        <label htmlFor="session-start">Giờ bắt đầu buổi</label>
        <input
          id="session-start"
          type="time"
          value={startTime}
          disabled={busy}
          onChange={(e) => setStartTime(e.target.value)}
        />
      </div>
      <div className="field">
        <label htmlFor="session-end">Giờ kết thúc buổi</label>
        <input
          id="session-end"
          type="time"
          value={endTime}
          disabled={busy}
          onChange={(e) => setEndTime(e.target.value)}
        />
      </div>
      <div className="field">
        <label htmlFor="session-kind">Loại buổi</label>
        <select
          id="session-kind"
          value={kind}
          disabled={busy}
          onChange={(e) => setKind(e.target.value)}
        >
          <option value="regular">Buổi thường</option>
          <option value="makeup">Học bù</option>
        </select>
      </div>
      {kind === "makeup" && (
        <div className="field">
          <label htmlFor="session-source">Thay buổi đã hủy (tùy chọn)</label>
          <select
            id="session-source"
            value={replacesSessionId}
            disabled={busy}
            onChange={(e) => setSource(e.target.value)}
          >
            <option value="">Không gắn buổi gốc</option>
            {sessions
              .filter((s) => s.status === "cancelled")
              .map((s) => (
                <option key={s.id} value={s.id}>
                  {sessionLabel(s)}
                </option>
              ))}
          </select>
        </div>
      )}
      {error && (
        <p className="error" role="alert">
          {error}
        </p>
      )}
      <div className="roster-actions">
        <button type="button" disabled={busy} onClick={onCancel}>
          Hủy tạo buổi
        </button>
        <button className="primary" disabled={busy || !canWrite}>
          {busy ? "Đang lưu…" : "Lưu buổi học"}
        </button>
      </div>
    </form>
  );
}
