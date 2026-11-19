import { useMemo, useRef, useState } from "react";
import { api, ApiError } from "./api";
import { today, type Student } from "./roster-api";
import {
  ImportError,
  runImport,
  type ImportApi,
  type ImportRow,
  type Stop,
} from "./student-import";
import { MAX_ROWS, parsePaste } from "./student-paste";
import "./paste-students.css";

// What the server said, in the two kinds the engine needs to tell apart: it certainly did not do the work (a 4xx
// answer) or it may have (no answer, a 5xx, a timeout).
function toImportError(e: unknown): ImportError {
  if (e instanceof ApiError) {
    const message =
      Object.values(e.errors ?? {})
        .flat()
        .join(" ") || e.message;
    const definite = e.status >= 400 && e.status < 500 && e.status !== 408;
    return new ImportError(message, {
      definite,
      status: e.status,
      field: e.status === 400 && !!e.errors && Object.keys(e.errors).length > 0,
    });
  }
  return new ImportError("Mất kết nối.", { definite: false });
}

const pending = (row: ImportRow) =>
  row.state.phase !== "done" && row.state.phase !== "rejected";
const lineOf = (row: { name: string; phone: string }) =>
  row.phone ? `${row.name}, ${row.phone}` : row.name;

/**
 * Adds many students to one class from pasted text. It uses the same two requests as the single form (create the
 * profile, enroll it) one student at a time, so a failure never leaves more than one student in doubt.
 */
export function PasteStudents({
  classId,
  students,
  enrolledIds,
  canWrite,
  onChanged,
  onClose,
  onDenied,
}: {
  classId: string;
  students: Student[];
  enrolledIds: string[];
  canWrite: boolean;
  /** Reload the roster behind the panel; the panel stays open. */
  onChanged: () => void;
  onClose: () => void;
  onDenied: (error: ApiError) => void;
}) {
  const [text, setText] = useState("");
  const [startDate, setStartDate] = useState(today);
  const [committed, setCommitted] = useState<ImportRow[]>([]);
  const [stop, setStop] = useState<Stop | null>(null);
  const [busy, setBusy] = useState(false);
  const nextId = useRef(1);
  // Profiles that existed before the first save: a retry never mistakes one of them for a new profile.
  const baseline = useRef<Set<string> | null>(null);

  const known = useMemo(() => {
    const enrolled = new Set(enrolledIds);
    return students.map((s) => ({
      name: s.name,
      enrolled: enrolled.has(s.id),
    }));
  }, [students, enrolledIds]);
  const parsed = useMemo(() => parsePaste(text, known), [text, known]);
  const valid = parsed.rows.filter((r) => !r.error);
  const invalid = parsed.rows.length - valid.length;
  const waiting = committed.filter(pending);
  const done = committed.length - waiting.length;

  const importApi: ImportApi = {
    async create({ name, phone }) {
      try {
        return await api<Student>("/students", "POST", { name, phone });
      } catch (e) {
        throw toImportError(e);
      }
    },
    async enroll(studentId, date) {
      try {
        await api(`/classes/${classId}/enrollments`, "POST", {
          studentId,
          startDate: date,
        });
      } catch (e) {
        throw toImportError(e);
      }
    },
    async listStudents() {
      try {
        return await api<Student[]>("/students");
      } catch (e) {
        throw toImportError(e);
      }
    },
    async enrolledIds() {
      try {
        return (
          await api<{ studentId: string }[]>(`/classes/${classId}/enrollments`)
        ).map((item) => item.studentId);
      } catch (e) {
        throw toImportError(e);
      }
    },
  };

  async function start(retryOnly: boolean) {
    setBusy(true);
    setStop(null);
    baseline.current ??= new Set(students.map((s) => s.id));
    let rows = committed;
    if (!retryOnly) {
      const fresh: ImportRow[] = valid.map((row) => ({
        id: nextId.current++,
        name: row.name,
        phone: row.phone,
        state: { phase: "todo" },
      }));
      rows = [...committed, ...fresh];
      // Only the lines that still need the teacher's attention stay in the box.
      setText(
        parsed.rows
          .filter((r) => r.error)
          .map((r) => r.raw)
          .join("\n"),
      );
    }
    const working = rows.map((r) => ({ ...r }));
    setCommitted(working);
    const result = await runImport({
      rows: working,
      startDate,
      api: importApi,
      baseline: baseline.current,
      update: (id, patch) =>
        setCommitted((current) =>
          current.map((r) => (r.id === id ? { ...r, ...patch } : r)),
        ),
    });
    // Lines the server refused go back to the box to be corrected; nothing about them was saved.
    const rejected = working.filter((r) => r.state.phase === "rejected");
    if (rejected.length)
      setText(
        (previous) =>
          `${previous ? `${previous}\n` : ""}${rejected.map(lineOf).join("\n")}`,
      );
    setCommitted(working.filter((r) => r.state.phase !== "rejected"));
    setStop(result.stopped);
    setBusy(false);
    onChanged();
    if (result.stopped?.reason === "auth")
      onDenied(new ApiError(401, result.stopped.message));
  }

  const rejectedNote = stop === null && invalid > 0;
  return (
    <section
      className="roster-form paste-students"
      aria-label="Dán danh sách học sinh"
      aria-busy={busy}
    >
      <h3>Dán danh sách học sinh</h3>
      <p>
        Mỗi dòng một học sinh: “Nguyễn An” hoặc “Nguyễn An, 0901234567” (số điện
        thoại phụ huynh). Dán từ Excel hoặc Zalo đều được; số thứ tự đầu dòng
        được tự bỏ. Thông tin khác bổ sung sau ở hồ sơ từng em.
      </p>
      <div className="field">
        <label htmlFor="paste-list">Danh sách học sinh</label>
        <textarea
          id="paste-list"
          rows={8}
          value={text}
          disabled={busy || waiting.length > 0}
          placeholder={"Nguyễn An\nTrần Bình, 0901234567\nLê Chi"}
          onChange={(e) => setText(e.target.value)}
        />
      </div>
      <div className="field">
        <label htmlFor="paste-start">Ngày bắt đầu học</label>
        <input
          id="paste-start"
          type="date"
          value={startDate}
          disabled={busy}
          onChange={(e) => setStartDate(e.target.value)}
        />
      </div>

      {parsed.rows.length > 0 && (
        <>
          <p role="status">
            {parsed.overLimit
              ? `Tối đa ${MAX_ROWS} học sinh mỗi lần dán; bạn đã dán ${parsed.rows.length}.`
              : `${valid.length} học sinh sẽ được thêm${invalid ? `, ${invalid} dòng cần sửa` : ""}.`}
          </p>
          <table aria-label="Xem trước danh sách học sinh">
            <thead>
              <tr>
                <th scope="col">Dòng</th>
                <th scope="col">Họ tên</th>
                <th scope="col">Điện thoại</th>
                <th scope="col">Ghi chú</th>
              </tr>
            </thead>
            <tbody>
              {parsed.rows.map((row) => (
                <tr key={row.line} className={row.error ? "bad" : undefined}>
                  <td>{row.line}</td>
                  <td>{row.name}</td>
                  <td>{row.phone}</td>
                  <td>
                    {row.error ? (
                      <span className="error">{row.error}</span>
                    ) : (
                      <>
                        <span>Sẽ thêm</span>
                        {row.warnings.map((w) => (
                          <span className="warn" key={w}>
                            {w}
                          </span>
                        ))}
                      </>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}

      {committed.length > 0 && (
        <div className="paste-result">
          <p role="status">
            {busy
              ? `Đang lưu ${done}/${committed.length}…`
              : waiting.length
                ? `Đã thêm ${done}/${committed.length}; còn ${waiting.length} học sinh chưa hoàn tất.`
                : `Đã thêm ${done} học sinh vào lớp.`}
          </p>
          <ul aria-label="Kết quả từng học sinh">
            {committed.map((row) => (
              <li key={row.id}>
                {row.name}
                {": "}
                {row.state.phase === "done"
                  ? "đã thêm vào lớp"
                  : row.state.phase === "unknown-create" ||
                      row.state.phase === "unknown-enroll"
                    ? "chưa chắc đã lưu, sẽ kiểm tra khi thử lại"
                    : row.state.phase === "created"
                      ? "đã tạo hồ sơ, chờ đăng ký vào lớp"
                      : "đang chờ"}
              </li>
            ))}
          </ul>
        </div>
      )}
      {stop && (
        <p className="error" role="alert">
          {stop.reason === "auth"
            ? "Phiên đã hết hạn. Đăng nhập lại rồi bấm “Thử lại”; những học sinh đã thêm được giữ nguyên."
            : stop.message}
        </p>
      )}
      {rejectedNote && (
        <p className="muted">
          Các dòng có lỗi vẫn nằm trong ô trên để bạn sửa; những dòng hợp lệ
          được thêm bình thường.
        </p>
      )}

      <div className="roster-actions">
        <button type="button" disabled={busy} onClick={onClose}>
          {committed.length > 0 && waiting.length === 0 ? "Đóng" : "Hủy"}
        </button>
        {waiting.length > 0 && (
          <button
            type="button"
            className="primary"
            disabled={busy || !canWrite}
            onClick={() => void start(true)}
          >
            Thử lại
          </button>
        )}
        {waiting.length === 0 && (
          <button
            type="button"
            className="primary"
            disabled={
              busy || !canWrite || valid.length === 0 || parsed.overLimit
            }
            onClick={() => void start(false)}
          >
            {busy ? "Đang lưu…" : `Thêm ${valid.length} học sinh`}
          </button>
        )}
      </div>
    </section>
  );
}
