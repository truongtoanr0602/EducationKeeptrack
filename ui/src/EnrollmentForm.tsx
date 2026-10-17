import { useState, type FormEvent } from "react";
import { api, ApiError } from "./api";
import {
  statusNames,
  today,
  type Student,
  type Enrollment,
  type EnrollmentStatus,
} from "./roster-api";

export function EnrollmentForm({
  item,
  students,
  classId,
  canWrite,
  onSaved,
  onCancel,
  onDenied,
}: {
  item?: Enrollment;
  students: Student[];
  classId: string;
  canWrite: boolean;
  onSaved: () => void;
  onCancel: () => void;
  onDenied: (error: ApiError) => void;
}) {
  const [studentId, setStudentId] = useState("");
  const [date, setDate] = useState(today);
  const [status, setStatus] = useState<EnrollmentStatus>(
    item?.status === "leave" ? "active" : "leave",
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!date || (!item && !studentId)) {
      setError("Vui lòng chọn học sinh và ngày hiệu lực.");
      return;
    }
    setBusy(true);
    setError("");
    try {
      await api(
        `/classes/${classId}/enrollments${item ? `/${item.id}` : ""}`,
        item ? "PUT" : "POST",
        item
          ? { status, effectiveDate: date, version: item.version }
          : { studentId, startDate: date },
      );
      onSaved();
    } catch (e) {
      setError(
        e instanceof ApiError
          ? Object.values(e.errors ?? {})
              .flat()
              .join(" ") || e.message
          : "Không thể lưu đăng ký. Vui lòng thử lại.",
      );
      if (e instanceof ApiError && (e.status === 401 || e.status === 403))
        onDenied(e);
    } finally {
      setBusy(false);
    }
  }
  return (
    <form className="roster-form" onSubmit={submit} noValidate aria-busy={busy}>
      <h3>
        {item ? `Đổi tình trạng: ${item.student.name}` : "Chọn học sinh đã có"}
      </h3>
      {item ? (
        <div className="field">
          <label htmlFor="enrollment-status">Tình trạng mới</label>
          <select
            id="enrollment-status"
            value={status}
            disabled={busy}
            onChange={(e) => setStatus(e.target.value as EnrollmentStatus)}
          >
            {(Object.keys(statusNames) as EnrollmentStatus[])
              .filter((s) => s !== item.status)
              .map((s) => (
                <option value={s} key={s}>
                  {statusNames[s]}
                </option>
              ))}
          </select>
        </div>
      ) : (
        <div className="field">
          <label htmlFor="enrollment-student">Học sinh</label>
          <select
            id="enrollment-student"
            value={studentId}
            disabled={busy}
            onChange={(e) => setStudentId(e.target.value)}
          >
            <option value="">Chọn học sinh của bạn</option>
            {students.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
                {s.phone ? ` · ${s.phone}` : ""} · {s.id.slice(0, 8)}
              </option>
            ))}
          </select>
        </div>
      )}
      <div className="field">
        <label htmlFor="enrollment-date">
          {item ? "Ngày hiệu lực" : "Ngày bắt đầu"}
        </label>
        <input
          id="enrollment-date"
          type="date"
          value={date}
          min={item?.history.at(-1)?.effectiveDate}
          disabled={busy}
          onChange={(e) => setDate(e.target.value)}
        />
      </div>
      {status === "stopped" && item && (
        <p>
          Ngày hiệu lực là ngày kết thúc. Khi học lại, tạo đăng ký mới để giữ
          lịch sử.
        </p>
      )}
      {error && (
        <p className="error" role="alert">
          {error}
        </p>
      )}
      <div className="roster-actions">
        <button type="button" disabled={busy} onClick={onCancel}>
          Hủy
        </button>
        <button className="primary" disabled={busy || !canWrite}>
          {busy ? "Đang lưu…" : item ? "Lưu tình trạng" : "Lưu đăng ký"}
        </button>
      </div>
    </form>
  );
}
