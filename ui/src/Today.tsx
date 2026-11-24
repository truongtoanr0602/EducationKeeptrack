import { lazy, Suspense, useCallback, useEffect, useState } from "react";
import { SetupCard } from "./SetupCard";
import { api, ApiError, type Me } from "./api";
import { useAccess } from "./access";
import {
  type Attendance,
  type AttendanceStatus,
  type ClassSession,
  saveAttendance,
  sessionStatusNames,
} from "./session-api";
import { today as todayDate } from "./roster-api";
import { useSwr } from "./swr";
import {
  loadToday,
  shiftDate,
  type TodaySession,
  type TodayView,
} from "./today-api";

const TodayCalendar = lazy(() => import("./TodayCalendar"));

type Mark = { status: "present" | "absent"; excused: boolean };
type Undo = { studentId: string; before: Attendance; version: number }[];

const hhmm = (t: string) => t.slice(0, 5);
const message = (e: unknown) =>
  e instanceof ApiError
    ? Object.values(e.errors ?? {})
        .flat()
        .join(" ") || e.message
    : "Không thể lưu. Vui lòng thử lại.";

// Everyone starts as present; only exceptions need a tap (ADR 0005).
const defaults = (data: Attendance[]): Record<string, Mark> =>
  Object.fromEntries(
    data.map((r) => [
      r.studentId,
      {
        status: r.status === "absent" ? "absent" : "present",
        excused: r.excused,
      },
    ]),
  );

function SessionCard({
  item,
  canWrite,
  onChanged,
  onDenied,
}: {
  item: TodaySession;
  canWrite: boolean;
  onChanged: () => void;
  onDenied: (e: unknown) => void;
}) {
  const { session } = item;
  const base = `/classes/${item.classId}/sessions/${session.id}`;
  const [open, setOpen] = useState(false);
  const [rows, setRows] = useState<Attendance[]>([]);
  const [marks, setMarks] = useState<Record<string, Mark>>({});
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [undo, setUndo] = useState<Undo | null>(null);
  const [notice, setNotice] = useState("");
  const [version, setVersion] = useState(session.version);
  const [status, setStatus] = useState(session.status);
  useEffect(() => {
    setVersion(session.version);
    setStatus(session.status);
  }, [session.version, session.status]);

  const load = useCallback(async () => {
    setError("");
    try {
      const data = await api<Attendance[]>(`${base}/attendance`);
      setRows(data);
      setMarks(defaults(data));
    } catch (e) {
      setError(message(e));
      onDenied(e);
    }
  }, [base, onDenied]);
  useEffect(() => {
    if (open) void load();
  }, [open, load]);

  const toggle = (id: string) =>
    setMarks((m) => ({
      ...m,
      [id]: {
        status: m[id].status === "present" ? "absent" : "present",
        excused: false,
      },
    }));
  const writable = canWrite && status !== "cancelled";
  const unmarked = rows.some((r) => r.status === "unmarked");
  const dirty = rows.some((r) => {
    const m = marks[r.studentId];
    return m && (m.status !== r.status || m.excused !== r.excused);
  });

  const mark = (
    row: Attendance,
    to: AttendanceStatus,
    excused: boolean,
    v: number,
  ) => ({
    studentId: row.studentId,
    status: to,
    reason: to === "absent" ? row.reason : null,
    excused: to === "absent" && excused,
    version: v,
  });
  async function save() {
    setBusy(true);
    setError("");
    setNotice("");
    try {
      if (status === "planned") {
        const held = await api<ClassSession>(base, "PUT", {
          status: "held",
          version,
        });
        setVersion(held.version);
        setStatus("held");
      }
      const changed = rows
        .map((row) => ({ row, m: marks[row.studentId] }))
        .filter(
          ({ row, m }) =>
            m && (m.status !== row.status || m.excused !== row.excused),
        );
      // Optimistic: the card shows the new marks at once; a failure below reloads the saved ones.
      setRows((all) =>
        all.map((row) => {
          const m = marks[row.studentId];
          return m ? { ...row, status: m.status, excused: m.excused } : row;
        }),
      );
      // One request for the whole card: the server saves every change or none.
      const next = changed.length
        ? await saveAttendance(
            item.classId,
            session.id,
            changed.map(({ row, m }) =>
              mark(row, m.status, m.excused, row.version),
            ),
          )
        : rows;
      const done: Undo = changed.map(({ row }) => ({
        studentId: row.studentId,
        before: row,
        version: next.find((r) => r.studentId === row.studentId)!.version,
      }));
      setRows(next);
      setUndo(done.length ? done : null);
      setNotice(
        done.length
          ? `Đã lưu điểm danh (${done.length} thay đổi).`
          : "Không có thay đổi.",
      );
      onChanged();
    } catch (e) {
      setError(message(e));
      onDenied(e);
      await load();
    } finally {
      setBusy(false);
    }
  }
  async function revert() {
    if (!undo) return;
    setBusy(true);
    setError("");
    try {
      await saveAttendance(
        item.classId,
        session.id,
        undo.map((step) =>
          mark(
            step.before,
            step.before.status,
            step.before.excused,
            step.version,
          ),
        ),
      );
      setUndo(null);
      setNotice("Đã hoàn tác.");
      await load();
      onChanged();
    } catch (e) {
      setError(message(e));
      onDenied(e);
      await load();
    } finally {
      setBusy(false);
    }
  }

  const marked = item.present + item.absent;
  return (
    <li className={`today-card status-${status}`}>
      <button
        className="today-head"
        aria-expanded={open}
        onClick={() => setOpen((o) => !o)}
      >
        <span className="today-time">
          {hhmm(session.startTime)}–{hhmm(session.endTime)}
        </span>
        <span className="today-title">
          <strong>{item.className}</strong>
          <span>
            {item.subject}
            {session.kind === "makeup" ? " · Học bù" : ""}
          </span>
        </span>
        <span className="today-meta">
          <span className="chip">{sessionStatusNames[status]}</span>
          <span>
            {marked}/{item.students} đã điểm danh
          </span>
        </span>
      </button>
      {open && (
        <div className="today-body">
          {status === "cancelled" && (
            <p className="notice">Buổi đã hủy: chỉ xem điểm danh cũ.</p>
          )}
          {error && (
            <p role="alert" className="error">
              {error}
            </p>
          )}
          {notice && <p role="status">{notice}</p>}
          <ul
            className="today-students"
            aria-label={`Điểm danh ${item.className}`}
          >
            {rows.map((r) => {
              const m = marks[r.studentId] ?? {
                status: "present",
                excused: false,
              };
              return (
                <li key={r.studentId} className={`today-student ${m.status}`}>
                  <button
                    aria-pressed={m.status === "absent"}
                    aria-label={`${r.name}: ${m.status === "present" ? "Có mặt" : "Vắng"}. Chạm để đổi`}
                    disabled={busy || !writable}
                    onClick={() => toggle(r.studentId)}
                  >
                    <span>{r.name}</span>
                    <strong>
                      {m.status === "present" ? "Có mặt" : "Vắng"}
                    </strong>
                  </button>
                  {m.status === "absent" && (
                    <label className="checkbox-label">
                      <input
                        type="checkbox"
                        checked={m.excused}
                        disabled={busy || !writable}
                        onChange={(e) =>
                          setMarks((all) => ({
                            ...all,
                            [r.studentId]: { ...m, excused: e.target.checked },
                          }))
                        }
                      />
                      Nghỉ có phép
                    </label>
                  )}
                </li>
              );
            })}
          </ul>
          {rows.length === 0 && !error && (
            <p>Không có học sinh đăng ký trong ngày này.</p>
          )}
          {writable && rows.length > 0 && (
            <div className="today-actions">
              <button
                className="primary"
                disabled={busy || (!dirty && !unmarked && status === "held")}
                onClick={() => void save()}
              >
                {busy ? "Đang lưu…" : "Lưu điểm danh"}
              </button>
              {undo && (
                <button disabled={busy} onClick={() => void revert()}>
                  Hoàn tác
                </button>
              )}
            </div>
          )}
        </div>
      )}
    </li>
  );
}

export function Today({ me }: { me: Me }) {
  const { canWrite, denied } = useAccess(me);
  const [date, setDate] = useState<string | undefined>();
  const {
    data: view,
    error: failure,
    reload: refresh,
  } = useSwr<TodayView>(`today:${date ?? ""}`, () => loadToday(date));
  const error = failure
    ? failure instanceof Error
      ? failure.message
      : "Không thể tải buổi dạy."
    : "";
  useEffect(() => {
    if (failure) denied(failure);
  }, [failure, denied]);
  const current = view?.date;
  const [refreshKey, setRefreshKey] = useState(0);
  const now = todayDate();
  return (
    <>
      <div className="page-heading compact">
        <div>
          <span className="eyebrow">BUỔI DẠY</span>
          <h1>Hôm nay</h1>
          {current && <p>{current}</p>}
        </div>
      </div>
      <div className="today-nav" role="group" aria-label="Chọn ngày">
        <button
          disabled={!current}
          onClick={() => current && setDate(shiftDate(current, -1))}
        >
          ← Hôm trước
        </button>
        <button
          disabled={date === undefined}
          onClick={() => setDate(undefined)}
        >
          Hôm nay
        </button>
        <button
          disabled={!current}
          onClick={() => current && setDate(shiftDate(current, 1))}
        >
          Hôm sau →
        </button>
      </div>
      <SetupCard me={me} />
      <div className="today-layout">
        <div>
          <Suspense fallback={null}>
            <TodayCalendar
              selected={current ?? date ?? now}
              today={now}
              refreshKey={refreshKey}
              canWrite={canWrite}
              onCreated={() => void refresh()}
              onSelect={(d) => setDate(d === now ? undefined : d)}
            />
          </Suspense>
        </div>
        <aside aria-label="Buổi học của ngày đang chọn">
          {error && (
            <p role="alert" className="error">
              {error}
            </p>
          )}
          {!view && !error && <p role="status">Đang tải…</p>}
          {view && view.sessions.length === 0 && (
            <section className="empty">
              <h2>Không có buổi học nào</h2>
              <p>
                Tạo lịch tuần trong chi tiết lớp để các buổi xuất hiện ở đây.
              </p>
            </section>
          )}
          {view && view.sessions.length > 0 && (
            <ul className="today-list" aria-label="Buổi học trong ngày">
              {view.sessions.map((item) => (
                <SessionCard
                  key={item.session.id}
                  item={item}
                  canWrite={canWrite}
                  onChanged={() => {
                    void refresh();
                    setRefreshKey((k) => k + 1);
                  }}
                  onDenied={denied}
                />
              ))}
            </ul>
          )}
        </aside>
      </div>
    </>
  );
}
