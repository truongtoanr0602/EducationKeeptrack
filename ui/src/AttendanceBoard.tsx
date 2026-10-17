import { useCallback, useEffect, useState } from "react";
import { api, ApiError } from "./api";
import {
  type Attendance,
  type AttendanceStatus,
  type ClassSession,
} from "./session-api";

function AttendanceRow({
  row,
  path,
  canWrite,
  onSaved,
  onDenied,
}: {
  row: Attendance;
  path: string;
  canWrite: boolean;
  onSaved: () => void;
  onDenied: (error: ApiError) => void;
}) {
  const [status, setStatus] = useState<AttendanceStatus>(row.status);
  const [reason, setReason] = useState(row.reason ?? "");
  const [excused, setExcused] = useState(row.excused);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  async function save() {
    setBusy(true);
    setError("");
    try {
      await api(`${path}/${row.studentId}`, "PUT", {
        status,
        reason: status === "absent" ? reason : null,
        excused: status === "absent" && excused,
        version: row.version,
      });
      onSaved();
    } catch (e) {
      setError(
        e instanceof ApiError
          ? Object.values(e.errors ?? {})
              .flat()
              .join(" ") || e.message
          : "Không thể lưu điểm danh. Vui lòng thử lại.",
      );
      if (e instanceof ApiError) onDenied(e);
    } finally {
      setBusy(false);
    }
  }
  return (
    <tr className={`attendance-row ${row.status}`}>
      <td>
        {row.name}
        <strong className="attendance-label">
          {row.status === "present"
            ? "Có mặt"
            : row.status === "absent"
              ? row.excused
                ? "Vắng · Nghỉ có phép"
                : "Vắng"
              : "Chưa điểm danh"}
        </strong>
      </td>
      <td>
        <select
          aria-label={`Điểm danh ${row.name}`}
          value={status}
          disabled={busy || !canWrite}
          onChange={(e) => setStatus(e.target.value as AttendanceStatus)}
        >
          <option value="unmarked">Chưa điểm danh</option>
          <option value="present">Có mặt</option>
          <option value="absent">Vắng</option>
        </select>
        {status === "absent" && (
          <>
            <input
              aria-label={`Lý do vắng ${row.name}`}
              placeholder="Lý do vắng (tùy chọn)"
              maxLength={1000}
              value={reason}
              disabled={busy || !canWrite}
              onChange={(e) => setReason(e.target.value)}
            />
            <label className="checkbox-label">
              <input
                type="checkbox"
                aria-label={`Nghỉ có phép ${row.name}`}
                checked={excused}
                disabled={busy || !canWrite}
                onChange={(e) => setExcused(e.target.checked)}
              />
              Nghỉ có phép
            </label>
          </>
        )}
      </td>
      <td>
        <button disabled={busy || !canWrite} onClick={() => void save()}>
          {busy ? "Đang lưu…" : "Lưu điểm danh"}
        </button>
        {error && (
          <p role="alert" className="error">
            {error}
          </p>
        )}
      </td>
    </tr>
  );
}
export function AttendanceBoard({
  classId,
  session,
  canWrite,
  onChanged,
  onDenied,
}: {
  classId: string;
  session: ClassSession;
  canWrite: boolean;
  onChanged: () => void;
  onDenied: (error: ApiError) => void;
}) {
  const [rows, setRows] = useState<Attendance[]>([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const path = `/classes/${classId}/sessions/${session.id}/attendance`;
  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      setRows(await api<Attendance[]>(path));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Không thể tải điểm danh.");
      if (e instanceof ApiError) onDenied(e);
    } finally {
      setLoading(false);
    }
  }, [path, onDenied]);
  useEffect(() => {
    void load();
  }, [load, session.version]);
  function saved() {
    void load();
    onChanged();
  }
  return (
    <div className="attendance-board">
      <div className="roster-actions">
        <h4>Điểm danh học sinh</h4>
        <button disabled={loading} onClick={() => void load()}>
          Tải lại điểm danh
        </button>
      </div>
      {session.status !== "held" && (
        <p className="notice">
          {session.status === "cancelled"
            ? "Buổi đã hủy: chỉ xem điểm danh cũ."
            : "Ghi nhận buổi đã diễn ra trước khi điểm danh."}
        </p>
      )}
      {loading && <p role="status">Đang tải điểm danh…</p>}
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      <table aria-label="Điểm danh học sinh">
        <thead>
          <tr>
            <th>Học sinh / đã lưu</th>
            <th>Thay đổi điểm danh</th>
            <th>Lưu</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <AttendanceRow
              key={row.studentId}
              row={row}
              path={path}
              canWrite={canWrite && session.status === "held"}
              onSaved={saved}
              onDenied={onDenied}
            />
          ))}
        </tbody>
      </table>
      {!loading && !error && rows.length === 0 && (
        <p>Không có học sinh đăng ký trong ngày buổi học.</p>
      )}
    </div>
  );
}
