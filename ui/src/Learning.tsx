import { useCallback, useEffect, useRef, useState } from "react";
import { api, ApiError, type Me, type TeacherClass } from "./api";
import { today } from "./roster-api";
import { Login } from "./Login";
import { SubmissionForm } from "./SubmissionForm";
import { SkillResults } from "./SkillResults";
import { Progress } from "./Progress";
import type { Assessment, AssessmentSummary } from "./learning-api";
import { formatDate } from "./format-date";

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
  const [defaultMethod, setDefaultMethod] = useState("manual");
  const [rubricInput, setRubricInput] = useState("");
  const [rubricEditing, setRubricEditing] = useState("");
  const [rubricMessage, setRubricMessage] = useState("");
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
      setRubricEditing(detail?.rubric ?? "");
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
        defaultMethod,
        rubric: rubricInput,
      });
      setItems((current) => [item, ...current]);
      setSelected(item.id);
      setAssessment(item);
      setRubricEditing(item.rubric ?? "");
      setTitle("");
      setRubricInput("");
    } catch (e) {
      report(e);
    } finally {
      setBusy(false);
    }
  }

  async function saveRubric() {
    if (!assessment) return;
    setBusy(true);
    setError("");
    setRubricMessage("");
    try {
      const updated = await api<Assessment>(
        `${path}/${assessment.id}/rubric`,
        "PUT",
        {
          rubric: rubricEditing,
        },
      );
      setAssessment(updated);
      setRubricEditing(updated.rubric ?? "");
      setRubricMessage("Đã lưu hướng dẫn chấm.");
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
        Chọn hai mức độc lập cho từng học sinh. Hỗ trợ tải ảnh bài nộp, AI chấm
        bài đề xuất và giáo viên xác nhận kết quả.
      </p>
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
            value={defaultMethod}
            disabled={busy || !access.canWrite || expired}
            onChange={(e) => setDefaultMethod(e.target.value)}
          >
            <option value="manual">Tự chấm</option>
            <option value="ai">AI chấm bài</option>
          </select>
        </label>
        <label style={{ gridColumn: "1 / -1" }}>
          Đề bài / Hướng dẫn chấm ban đầu (tùy chọn)
          <textarea
            rows={2}
            maxLength={5000}
            placeholder="Nhập tiêu chí chấm, đáp án mẫu để AI đối chiếu..."
            value={rubricInput}
            disabled={busy || !access.canWrite || expired}
            onChange={(e) => setRubricInput(e.target.value)}
          />
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
              {formatDate(item.date)} · {item.title}
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

          {/* Rubric editor section */}
          <section
            style={{
              margin: "12px 0 20px 0",
              padding: "12px",
              border: "1px solid #ccc",
              borderRadius: "6px",
              background: "#fafafa",
            }}
          >
            <h4 style={{ margin: "0 0 6px 0" }}>
              📖 Hướng dẫn chấm bài (Phiên bản {assessment.rubricVersion ?? 1})
            </h4>
            <p style={{ margin: "0 0 8px 0", fontSize: "13px", color: "#666" }}>
              Cung cấp đáp án hoặc tiêu chí chấm để AI phân tích và đề xuất kết
              quả cho từng bài nộp.
            </p>
            <textarea
              rows={3}
              maxLength={5000}
              placeholder="Nhập hướng dẫn chấm, biểu điểm hoặc đáp án..."
              value={rubricEditing}
              disabled={busy || !access.canWrite || expired}
              onChange={(e) => setRubricEditing(e.target.value)}
              style={{ width: "100%", boxSizing: "border-box" }}
            />
            <div
              style={{
                marginTop: "6px",
                display: "flex",
                gap: "10px",
                alignItems: "center",
              }}
            >
              <button
                type="button"
                disabled={
                  busy ||
                  !access.canWrite ||
                  expired ||
                  rubricEditing === (assessment.rubric ?? "")
                }
                onClick={() => void saveRubric()}
              >
                Lưu hướng dẫn chấm
              </button>
              {rubricMessage && (
                <span style={{ color: "green", fontSize: "13px" }}>
                  {rubricMessage}
                </span>
              )}
            </div>
          </section>

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
          <Progress
            classId={classroom.id}
            students={assessment.submissions.map((x) => ({
              id: x.studentId,
              name: x.studentName,
            }))}
            onDenied={denied}
          />
          <SkillResults
            classId={classroom.id}
            assessmentId={assessment.id}
            canWrite={access.canWrite && !expired && !busy}
            onDenied={denied}
          />
        </div>
      )}
    </section>
  );
}
