import { useCallback, useEffect, useRef, useState } from "react";
import { api, ApiError, type Me, type TeacherClass } from "./api";
import { today } from "./roster-api";
import { Login } from "./Login";
import { SubmissionForm } from "./SubmissionForm";
import type { Assessment, AssessmentSummary } from "./learning-api";

export function Learning({
  classroom,
  me,
}: {
  classroom: TeacherClass;
  me: Me;
}) {
  const path = `/learning/classes/${classroom.id}/assessments`;
  const [items, setItems] = useState<AssessmentSummary[]>([]);
  const [selected, setSelected] = useState(
    () => new URLSearchParams(window.location.search).get("assessment") ?? "",
  );
  const [assessment, setAssessment] = useState<Assessment | null>(null);
  const [title, setTitle] = useState("");
  const [date, setDate] = useState(today());
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [access, setAccess] = useState(me.access);
  const [expired, setExpired] = useState(false);
  const request = useRef(0);
  const denied = useCallback((e: ApiError) => {
    if (e.status === 401) setExpired(true);
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
  function report(e: unknown) {
    setError(
      e instanceof ApiError
        ? Object.values(e.errors ?? {})
            .flat()
            .join(" ") || e.message
        : "Không thể tải hoặc lưu đánh giá. Vui lòng thử lại.",
    );
    if (e instanceof ApiError) denied(e);
  }
  const load = useCallback(async () => {
    const current = ++request.current;
    setBusy(true);
    setError("");
    try {
      const list = await api<AssessmentSummary[]>(path);
      const id = list.some((x) => x.id === selected) ? selected : list[0]?.id;
      const detail = id ? await api<Assessment>(`${path}/${id}`) : null;
      if (current !== request.current) return;
      setItems(list);
      setAssessment(detail);
      if (id && id !== selected) setSelected(id);
    } catch (e) {
      if (current !== request.current) return;
      setError(e instanceof Error ? e.message : "Không thể tải đánh giá.");
      if (e instanceof ApiError) denied(e);
    } finally {
      if (current === request.current) setBusy(false);
    }
  }, [path, selected, denied]);
  useEffect(() => {
    void load();
    return () => {
      request.current++;
    };
  }, [load]);
  useEffect(() => {
    const url = new URL(window.location.href);
    if (selected) url.searchParams.set("assessment", selected);
    else url.searchParams.delete("assessment");
    window.history.replaceState(null, "", url);
  }, [selected]);
  async function create(event: React.FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      const item = await api<Assessment>(path, "POST", {
        title,
        date,
        defaultMethod: "manual",
      });
      setItems((current) => [item, ...current]);
      setSelected(item.id);
      setAssessment(item);
      setTitle("");
    } catch (e) {
      report(e);
    } finally {
      setBusy(false);
    }
  }
  return (
    <section aria-label="Đánh giá học tập" className="learning-panel">
      <h2>Đánh giá học tập</h2>
      <p>
        Chọn hai mức độc lập cho từng học sinh. Chỉ kết quả đã xác nhận dùng
        trong báo cáo.
      </p>
      <p>AI chưa khả dụng</p>
      {!access.canWrite && (
        <p className="notice">
          Chỉ xem đánh giá đã lưu; cần quyền sử dụng còn hạn để sửa hoặc xác
          nhận.
        </p>
      )}
      {expired && (
        <Login
          onLogin={async () => {
            const user = await api<Me>("/auth/me");
            setAccess(user.access);
            setExpired(false);
          }}
        />
      )}
      <form
        onSubmit={(event) => void create(event)}
        className="assessment-create"
      >
        <label>
          Tên bài đánh giá
          <input
            required
            maxLength={160}
            value={title}
            disabled={busy || !access.canWrite || expired}
            onChange={(e) => setTitle(e.target.value)}
          />
        </label>
        <label>
          Ngày đánh giá
          <input
            required
            type="date"
            value={date}
            disabled={busy || !access.canWrite || expired}
            onChange={(e) => setDate(e.target.value)}
          />
        </label>
        <label>
          Cách chấm mặc định
          <select
            defaultValue="manual"
            disabled={busy || !access.canWrite || expired}
          >
            <option value="manual">Tự chấm</option>
            <option value="ai" disabled>
              AI chưa khả dụng
            </option>
          </select>
        </label>
        <button
          className="primary"
          disabled={busy || !access.canWrite || expired}
        >
          Tạo bài đánh giá
        </button>
      </form>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      <label>
        Bài đánh giá
        <select
          disabled={busy || !items.length}
          value={selected}
          onChange={(e) => {
            setAssessment(null);
            setSelected(e.target.value);
          }}
        >
          {!items.length && <option value="">Chưa có bài đánh giá</option>}
          {items.map((item) => (
            <option key={item.id} value={item.id}>
              {item.date} · {item.title}
            </option>
          ))}
        </select>
      </label>
      <button disabled={busy} onClick={() => void load()}>
        Tải lại đánh giá
      </button>
      {busy && <p role="status">Đang tải…</p>}
      {access.canRead && assessment && (
        <div key={assessment.id}>
          <h3>{assessment.title}</h3>
          {!assessment.submissions.length && (
            <p>Không có học sinh theo học trong ngày đánh giá này.</p>
          )}
          {assessment.submissions.map((submission) => (
            <SubmissionForm
              key={`${submission.studentId}:${submission.version}`}
              initial={submission}
              path={`${path}/${assessment.id}/submissions/${submission.studentId}`}
              canWrite={access.canWrite && !expired && !busy}
              onDenied={denied}
            />
          ))}
        </div>
      )}
    </section>
  );
}
