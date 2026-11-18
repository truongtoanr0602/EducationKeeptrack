import { useCallback, useEffect, useState } from "react";
import { api, ApiError } from "./api";
import {
  saveAttendance,
  type Attendance,
  type AttendanceStatus,
  type ClassSession,
} from "./session-api";

type Draft = { status: AttendanceStatus; reason: string; excused: boolean };
type Board = { rows: Attendance[]; drafts: Record<string, Draft> };

const choices: [AttendanceStatus, string][] = [
  ["present", "Có mặt"],
  ["absent", "Vắng"],
  ["unmarked", "Chưa điểm danh"],
];
const savedLabel = (row: Attendance) =>
  row.status === "present"
    ? "Có mặt"
    : row.status === "absent"
      ? row.excused
        ? "Vắng · Nghỉ có phép"
        : "Vắng"
      : "Chưa điểm danh";
const toDraft = (row: Attendance): Draft => ({
  status: row.status,
  reason: row.reason ?? "",
  excused: row.excused,
});
// Reason and excused only count for an absent mark; the server drops them otherwise.
const isDirty = (row: Attendance, draft: Draft | undefined) =>
  !!draft &&
  (draft.status !== row.status ||
    (draft.status === "absent" &&
      (draft.excused !== row.excused ||
        (draft.reason.trim() || null) !== row.reason)));
const message = (e: unknown) =>
  e instanceof ApiError
    ? Object.values(e.errors ?? {})
        .flat()
        .join(" ") || e.message
    : "Không thể lưu điểm danh. Vui lòng thử lại.";

function AttendanceRow({
  row,
  draft,
  sessionId,
  disabled,
  onChange,
}: {
  row: Attendance;
  draft: Draft;
  sessionId: string;
  disabled: boolean;
  onChange: (draft: Draft) => void;
}) {
  const dirty = isDirty(row, draft);
  return (
    <tr className={`attendance-row ${row.status}${dirty ? " dirty" : ""}`}>
      <td>
        {row.name}
        <strong className="attendance-label">{savedLabel(row)}</strong>
        {dirty && <small className="attendance-dirty">Chưa lưu</small>}
      </td>
      <td>
        <div
          role="radiogroup"
          aria-label={`Điểm danh ${row.name}`}
          className="attendance-choices"
        >
          {choices.map(([value, label]) => (
            <label key={value} className={`attendance-choice ${value}`}>
              <input
                type="radio"
                name={`attendance-${sessionId}-${row.studentId}`}
                value={value}
                checked={draft.status === value}
                disabled={disabled}
                onChange={() =>
                  onChange({
                    ...draft,
                    status: value,
                    excused: value === "absent" && draft.excused,
                  })
                }
              />
              <span>{label}</span>
            </label>
          ))}
        </div>
        {draft.status === "absent" && (
          <div className="attendance-absence">
            <input
              aria-label={`Lý do vắng ${row.name}`}
              placeholder="Lý do vắng (tùy chọn)"
              maxLength={1000}
              value={draft.reason}
              disabled={disabled}
              onChange={(e) => onChange({ ...draft, reason: e.target.value })}
            />
            <label className="checkbox-label">
              <input
                type="checkbox"
                aria-label={`Nghỉ có phép ${row.name}`}
                checked={draft.excused}
                disabled={disabled}
                onChange={(e) =>
                  onChange({ ...draft, excused: e.target.checked })
                }
              />
              Nghỉ có phép
            </label>
          </div>
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
  const [board, setBoard] = useState<Board>({ rows: [], drafts: {} });
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const path = `/classes/${classId}/sessions/${session.id}/attendance`;
  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const data = await api<Attendance[]>(path);
      // Reloading refreshes saved rows and versions but never discards a mark the teacher has not saved yet.
      setBoard((old) => ({
        rows: data,
        drafts: Object.fromEntries(
          data.map((row) => {
            const before = old.rows.find((r) => r.studentId === row.studentId);
            const draft = old.drafts[row.studentId];
            return [
              row.studentId,
              before && isDirty(before, draft) ? draft : toDraft(row),
            ];
          }),
        ),
      }));
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

  const { rows, drafts } = board;
  const writable = canWrite && session.status === "held";
  const changed = rows.filter((row) => isDirty(row, drafts[row.studentId]));
  const setDraft = (studentId: string, draft: Draft) => {
    setNotice("");
    setBoard((b) => ({ ...b, drafts: { ...b.drafts, [studentId]: draft } }));
  };
  const allPresent = () => {
    setNotice("");
    setBoard((b) => ({
      ...b,
      drafts: Object.fromEntries(
        b.rows.map((row) => [
          row.studentId,
          {
            ...(b.drafts[row.studentId] ?? toDraft(row)),
            status: "present",
            excused: false,
          },
        ]),
      ),
    }));
  };
  const discard = () => {
    setNotice("");
    setError("");
    setBoard((b) => ({
      ...b,
      drafts: Object.fromEntries(b.rows.map((r) => [r.studentId, toDraft(r)])),
    }));
  };

  async function save() {
    setBusy(true);
    setError("");
    setNotice("");
    try {
      const saved = await saveAttendance(
        classId,
        session.id,
        changed.map((row) => {
          const d = drafts[row.studentId];
          return {
            studentId: row.studentId,
            status: d.status,
            reason: d.status === "absent" ? d.reason : null,
            excused: d.status === "absent" && d.excused,
            version: row.version,
          };
        }),
      );
      setBoard({
        rows: saved,
        drafts: Object.fromEntries(saved.map((r) => [r.studentId, toDraft(r)])),
      });
      setNotice(`Đã lưu điểm danh (${changed.length} học sinh).`);
      onChanged();
    } catch (e) {
      setError(message(e));
      if (e instanceof ApiError) onDenied(e);
    } finally {
      setBusy(false);
    }
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
      <table aria-label="Điểm danh học sinh">
        <thead>
          <tr>
            <th>Học sinh / đã lưu</th>
            <th>Điểm danh</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <AttendanceRow
              key={row.studentId}
              row={row}
              draft={drafts[row.studentId] ?? toDraft(row)}
              sessionId={session.id}
              disabled={busy || !writable}
              onChange={(d) => setDraft(row.studentId, d)}
            />
          ))}
        </tbody>
      </table>
      {!loading && !error && rows.length === 0 && (
        <p>Không có học sinh đăng ký trong ngày buổi học.</p>
      )}
      {(rows.length > 0 || error) && (
        <div className="attendance-actions">
          {error && (
            <p role="alert" className="error">
              {error}
            </p>
          )}
          {notice && <p role="status">{notice}</p>}
          <div className="attendance-buttons">
            <button
              disabled={busy || !writable || rows.length === 0}
              onClick={allPresent}
            >
              Tất cả có mặt
            </button>
            <button disabled={busy || changed.length === 0} onClick={discard}>
              Bỏ thay đổi
            </button>
            <button
              className="primary"
              disabled={busy || !writable || changed.length === 0}
              onClick={() => void save()}
            >
              {busy
                ? "Đang lưu…"
                : changed.length
                  ? `Lưu điểm danh (${changed.length} thay đổi)`
                  : "Lưu điểm danh"}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
