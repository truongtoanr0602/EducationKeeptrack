import { useEffect, useState, type FormEvent } from "react";
import { api, ApiError } from "./api";
import { today } from "./roster-api";
import { sessionLabel, type ClassSession } from "./session-api";
import {
  apiFieldErrors,
  useFieldErrors,
  type FieldErrors,
} from "./ui/field-errors";

export function SessionForm({
  classId,
  sessions,
  source,
  canWrite,
  onSaved,
  onCancel,
  onLockChange,
  onDenied,
}: {
  classId: string;
  sessions: ClassSession[];
  source?: ClassSession;
  canWrite: boolean;
  onSaved: () => void;
  onCancel: () => void;
  onLockChange?: (locked: boolean) => void;
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
  useEffect(() => {
    onLockChange?.(busy);
    return () => onLockChange?.(false);
  }, [busy, onLockChange]);
  const [error, setError] = useState("");
  const fields = useFieldErrors();
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    setError("");
    fields.clear();
    if (!date || !startTime || !endTime || startTime >= endTime) {
      const invalid: FieldErrors = {};
      if (!date) invalid.date = "Chọn ngày buổi học.";
      if (!startTime) invalid.startTime = "Chọn giờ bắt đầu.";
      if (!endTime) invalid.endTime = "Chọn giờ kết thúc.";
      else if (startTime && startTime >= endTime)
        invalid.endTime = "Giờ kết thúc phải sau giờ bắt đầu.";
      fields.show(invalid, form);
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
      fields.show(
        apiFieldErrors(e, { date: "date", time: "startTime", kind: "kind" }),
        form,
      );
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
          {...fields.props("date")}
        />
        {fields.message("date")}
      </div>
      <div className="field">
        <label htmlFor="session-start">Giờ bắt đầu buổi</label>
        <input
          id="session-start"
          type="time"
          value={startTime}
          disabled={busy}
          onChange={(e) => setStartTime(e.target.value)}
          {...fields.props("startTime")}
        />
        {fields.message("startTime")}
      </div>
      <div className="field">
        <label htmlFor="session-end">Giờ kết thúc buổi</label>
        <input
          id="session-end"
          type="time"
          value={endTime}
          disabled={busy}
          onChange={(e) => setEndTime(e.target.value)}
          {...fields.props("endTime")}
        />
        {fields.message("endTime")}
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
