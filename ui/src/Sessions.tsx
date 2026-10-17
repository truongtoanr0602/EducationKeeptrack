import { useCallback, useEffect, useMemo, useState } from "react";
import { api, ApiError, type Me, type TeacherClass } from "./api";
import { today } from "./roster-api";
import { AttendanceBoard } from "./AttendanceBoard";
import { ScheduleForm } from "./ScheduleForm";
import { SessionForm } from "./SessionForm";
import { SessionStatusForm } from "./SessionStatusForm";
import { Login } from "./Login";
import {
  weekDays,
  sessionLabel,
  type WeeklySchedule,
  type ClassSession,
  type MissingAttendance,
  type DateRange,
} from "./session-api";

function monthRange(): DateRange {
  const date = today();
  const year = Number(date.slice(0, 4));
  const month = Number(date.slice(5, 7));
  return {
    from: `${date.slice(0, 7)}-01`,
    to: `${date.slice(0, 7)}-${new Date(Date.UTC(year, month, 0)).getUTCDate()}`,
  };
}
export function Sessions({
  classroom,
  me,
  onChanged,
}: {
  classroom: TeacherClass;
  me: Me;
  onChanged?: () => void;
}) {
  const initialRange = useMemo(monthRange, []);
  const [range, setRange] = useState(initialRange);
  const [appliedRange, setAppliedRange] = useState(initialRange);
  const [schedule, setSchedule] = useState<WeeklySchedule>({
    version: 0,
    effectiveFrom: null,
    slots: [],
  });
  const [sessions, setSessions] = useState<ClassSession[]>([]);
  const [missing, setMissing] = useState<MissingAttendance[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [form, setForm] = useState<
    { kind: "schedule" } | { kind: "session"; source?: ClassSession } | null
  >(null);
  const [access, setAccess] = useState(me.access);
  const [sessionExpired, setSessionExpired] = useState(false);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const path = `/classes/${classroom.id}`;
  const denied = useCallback((e: ApiError) => {
    if (e.status === 401) setSessionExpired(true);
    if (e.status === 403)
      void api<Me>("/auth/me")
        .then((user) => setAccess(user.access))
        .catch(() =>
          setAccess((current) => ({
            ...current,
            canRead: false,
            canWrite: false,
          })),
        );
  }, []);
  const load = useCallback(
    async (dates: DateRange) => {
      setLoading(true);
      setError("");
      try {
        const query = `from=${encodeURIComponent(dates.from)}&to=${encodeURIComponent(dates.to)}`;
        const [weekly, items, incomplete] = await Promise.all([
          api<WeeklySchedule>(`${path}/schedule`),
          api<ClassSession[]>(`${path}/sessions?${query}`),
          api<MissingAttendance[]>(`${path}/attendance-missing?${query}`),
        ]);
        setSchedule(weekly);
        setSessions(items);
        setMissing(incomplete);
        setAppliedRange(dates);
      } catch (e) {
        setError(
          e instanceof ApiError
            ? Object.values(e.errors ?? {})
                .flat()
                .join(" ") || e.message
            : "Không thể tải lịch và buổi học.",
        );
        if (e instanceof ApiError) denied(e);
      } finally {
        setLoading(false);
      }
    },
    [path, denied],
  );
  useEffect(() => {
    void load(initialRange);
  }, [load, initialRange]);
  async function generate() {
    setBusy(true);
    setError("");
    try {
      await api(`${path}/sessions/generate`, "POST", {
        ...range,
        scheduleVersion: schedule.version,
      });
      await load(range);
      onChanged?.();
    } catch (e) {
      setError(
        e instanceof ApiError
          ? Object.values(e.errors ?? {})
              .flat()
              .join(" ") || e.message
          : "Không thể tạo buổi học. Vui lòng thử lại.",
      );
      if (e instanceof ApiError) denied(e);
    } finally {
      setBusy(false);
    }
  }
  function saved() {
    setForm(null);
    void load(appliedRange);
    onChanged?.();
  }
  async function resume() {
    const user = await api<Me>("/auth/me");
    if (user.email !== me.email) {
      window.location.assign("/");
      return;
    }
    setAccess(user.access);
    setSessionExpired(false);
    await load(appliedRange);
  }
  const selected = sessions.find((s) => s.id === selectedId);
  return (
    <section
      className="sessions-workspace roster"
      aria-label="Lịch và điểm danh"
    >
      {sessionExpired && (
        <>
          <p className="notice">
            Phiên đã hết hạn. Đăng nhập lại cùng tài khoản để tiếp tục; nội dung
            nhập được giữ trong trang này.
          </p>
          <Login onLogin={resume} />
        </>
      )}
      <div hidden={sessionExpired || !access.canRead}>
        <div className="page-heading">
          <div>
            <span className="eyebrow">LỊCH VÀ BUỔI HỌC</span>
            <h2>Lịch và điểm danh · {classroom.name}</h2>
            <p>Ngày và giờ buổi học theo Asia/Ho_Chi_Minh (UTC+7).</p>
          </div>
          <div className="roster-actions">
            <button
              disabled={!access.canWrite || !!form || loading}
              onClick={() => setForm({ kind: "schedule" })}
            >
              Thiết lập lịch tuần
            </button>
            <button
              disabled={!access.canWrite || !!form}
              onClick={() => setForm({ kind: "session" })}
            >
              Tạo buổi riêng / học bù
            </button>
            <button disabled={loading || busy} onClick={() => void load(range)}>
              Tải lại lịch và buổi
            </button>
          </div>
        </div>
        {!access.canWrite && (
          <p className="notice">
            Quyền sử dụng đã hết hạn. Chỉ xem lịch và điểm danh đã lưu; nội dung
            nhập chưa lưu vẫn được giữ.
          </p>
        )}
        <ul aria-label="Lịch tuần đã lưu">
          {schedule.slots.map((slot, index) => (
            <li key={index}>
              {weekDays[slot.dayOfWeek]} · {slot.startTime.slice(0, 5)}–
              {slot.endTime.slice(0, 5)}
            </li>
          ))}
        </ul>
        {schedule.effectiveFrom && (
          <p>
            Lịch phiên bản {schedule.version}, áp dụng từ{" "}
            {schedule.effectiveFrom}. Buổi cũ giữ nguyên khi đổi lịch.
          </p>
        )}
        {form?.kind === "schedule" && (
          <ScheduleForm
            classId={classroom.id}
            schedule={schedule}
            canWrite={access.canWrite}
            onSaved={saved}
            onCancel={() => setForm(null)}
            onDenied={denied}
          />
        )}
        {form?.kind === "session" && (
          <SessionForm
            classId={classroom.id}
            sessions={sessions}
            source={form.source}
            canWrite={access.canWrite}
            onSaved={saved}
            onCancel={() => setForm(null)}
            onDenied={denied}
          />
        )}
        <div className="session-range">
          <label>
            Xem từ ngày
            <input
              type="date"
              value={range.from}
              disabled={busy}
              onChange={(e) => setRange({ ...range, from: e.target.value })}
            />
          </label>
          <label>
            Xem đến ngày
            <input
              type="date"
              value={range.to}
              disabled={busy}
              onChange={(e) => setRange({ ...range, to: e.target.value })}
            />
          </label>
          <button
            disabled={loading || busy}
            onClick={() => {
              setSelectedId(null);
              void load(range);
            }}
          >
            Xem buổi
          </button>
          <button
            className="primary"
            disabled={!access.canWrite || loading || busy || !schedule.version}
            onClick={() => void generate()}
          >
            {busy ? "Đang tạo buổi…" : "Tạo buổi theo lịch"}
          </button>
        </div>
        {error && (
          <p role="alert" className="error">
            {error}
          </p>
        )}
        {loading && <p role="status">Đang tải lịch và buổi…</p>}
        <aside
          className="missing-attendance"
          aria-label="Nhắc điểm danh trước tổng kết"
        >
          <p>
            {missing.length
              ? `Còn ${missing.length} lượt chưa điểm danh trước tổng kết.`
              : "Không còn điểm danh thiếu ở các buổi đã diễn ra."}
          </p>
          <ul>
            {missing.map((m) => (
              <li key={`${m.sessionId}-${m.studentId}`}>
                <button onClick={() => setSelectedId(m.sessionId)}>
                  {m.date} · {m.startTime.slice(0, 5)} · {m.name} · Chưa điểm
                  danh
                </button>
              </li>
            ))}
          </ul>
        </aside>
        <ul className="session-list" aria-label="Buổi học đã lưu">
          {sessions.map((s) => (
            <li key={s.id}>
              <button
                aria-pressed={selectedId === s.id}
                onClick={() => setSelectedId(s.id)}
              >
                {sessionLabel(s)}
              </button>
              {s.kind === "makeup" && <span>Học bù</span>}
              {s.reason && <p>{s.reason}</p>}
            </li>
          ))}
        </ul>
        {!loading && !error && sessions.length === 0 && (
          <p>Chưa có buổi trong khoảng ngày đã chọn.</p>
        )}
        {selected && (
          <section className="selected-session">
            <h3>
              {sessionLabel(selected)}
              {selected.kind === "makeup" ? " · Học bù" : ""}
            </h3>
            <SessionStatusForm
              key={selected.id}
              classId={classroom.id}
              session={selected}
              canWrite={access.canWrite}
              onSaved={() => {
                void load(appliedRange);
                onChanged?.();
              }}
              onDenied={denied}
            />
            {selected.status === "cancelled" && (
              <button
                disabled={!access.canWrite || !!form}
                onClick={() => setForm({ kind: "session", source: selected })}
              >
                Tạo học bù cho buổi này
              </button>
            )}
            <AttendanceBoard
              key={selected.id}
              classId={classroom.id}
              session={selected}
              canWrite={access.canWrite}
              onChanged={() => {
                void load(appliedRange);
                onChanged?.();
              }}
              onDenied={denied}
            />
          </section>
        )}
      </div>
      {!access.canRead && !sessionExpired && (
        <p role="alert" className="error">
          Tài khoản hiện không được truy cập lịch và điểm danh.
        </p>
      )}
    </section>
  );
}
