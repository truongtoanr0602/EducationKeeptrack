import { useCallback, useEffect, useState } from "react";
import { api, ApiError, upload, type TeacherClass } from "./api";
import type { ImportReport, LessonPlan } from "./bank-api";
import { errorText, learningPath, viDate, type Curriculum } from "./personalization-api";
import "./bank.css";

type Props = { classroom: TeacherClass; curriculum: Curriculum; canWrite: boolean; denied: (e: unknown) => void; onImported: () => void };

// "Buổi chương trình" (#78): the Excel template, import with preview, copy from another class, and the lesson list
// paired with the class's dated sessions.
export function LessonsTab({ classroom, curriculum, canWrite, denied, onImported }: Props) {
  const [plan, setPlan] = useState<LessonPlan | null>(null);
  const [error, setError] = useState("");
  const load = useCallback(async () => {
    try {
      setPlan(await api<LessonPlan>(`${learningPath(classroom.id)}/lessons`));
      setError("");
    } catch (e) {
      setError(errorText(e, "Không thể tải các buổi chương trình."));
      denied(e);
    }
  }, [classroom.id, denied]);
  useEffect(() => {
    void load();
  }, [load]);
  return (
    <div className="stack">
      <ImportCard classroom={classroom} canWrite={canWrite} denied={denied} onDone={() => { onImported(); void load(); }} />
      {error && <p className="error" role="alert">{error} <button onClick={() => void load()}>Tải lại</button></p>}
      {plan && <LessonTable classroom={classroom} plan={plan} curriculum={curriculum} canWrite={canWrite} denied={denied} onPlan={setPlan} />}
    </div>
  );
}

function ImportCard({ classroom, canWrite, denied, onDone }: { classroom: TeacherClass; canWrite: boolean; denied: (e: unknown) => void; onDone: () => void }) {
  const [workbook, setWorkbook] = useState<File | null>(null);
  const [images, setImages] = useState<File[]>([]);
  const [report, setReport] = useState<ImportReport | null>(null);
  const [busy, setBusy] = useState("");
  const [error, setError] = useState("");
  const [classes, setClasses] = useState<TeacherClass[]>([]);
  const [source, setSource] = useState("");
  const [copying, setCopying] = useState(false);
  useEffect(() => {
    api<TeacherClass[]>("/classes").then((list) => setClasses(list.filter((x) => x.id !== classroom.id)), () => undefined);
  }, [classroom.id]);

  async function run(commit: boolean) {
    if (!workbook) return;
    setError("");
    try {
      for (const [i, image] of images.entries()) {
        setBusy(`Đang tải ảnh ${i + 1}/${images.length}…`);
        await upload(`${learningPath(classroom.id)}/question-images`, image, image.name);
      }
      setImages([]);
      setBusy(commit ? "Đang ghi dữ liệu…" : "Đang đọc file…");
      const value = await upload<ImportReport>(`${learningPath(classroom.id)}/imports?commit=${commit}`, workbook, workbook.name);
      setReport(value);
      if (value.committed) onDone();
    } catch (e) {
      setError(errorText(e, "Không thể đọc file. Vui lòng kiểm tra lại."));
      denied(e);
    } finally {
      setBusy("");
    }
  }
  async function copy(commit: boolean) {
    if (!source) return;
    setError("");
    setBusy(commit ? "Đang sao chép…" : "Đang xem trước…");
    try {
      const value = await api<ImportReport>(`${learningPath(classroom.id)}/copy-from/${source}?commit=${commit}`, "POST", {});
      setReport(value);
      setCopying(true);
      if (value.committed) onDone();
    } catch (e) {
      setError(errorText(e, "Không thể sao chép từ lớp đã chọn."));
      denied(e);
    } finally {
      setBusy("");
    }
  }
  const ready = report && !report.committed && report.issues.length === 0;
  return (
    <section className="card" aria-label="Tải chương trình và ngân hàng câu hỏi">
      <h2>Tải lên từ Excel</h2>
      <ol className="steps">
        <li>
          <a className="button-link" href={`/api${learningPath(classroom.id)}/import-template`} download>
            Tải file mẫu (.xlsx)
          </a>{" "}
          <span className="hint">Đã điền sẵn Toán 2 – HK1 (30 buổi, kỹ năng E1–E9, câu hỏi mẫu) kèm sheet Hướng dẫn.</span>
        </li>
        <li>Sửa hoặc thêm dòng trong các sheet Kỹ năng, Buổi, Câu hỏi.</li>
        <li>Chọn file (và các ảnh được nhắc trong cột Ảnh), bấm Xem trước rồi Ghi vào lớp.</li>
      </ol>
      <div className="import-pickers">
        <label>
          File Excel
          <input type="file" accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" disabled={!canWrite || !!busy}
            onChange={(e) => { setWorkbook(e.target.files?.[0] ?? null); setReport(null); setCopying(false); }} />
        </label>
        <label>
          Ảnh câu hỏi (không bắt buộc)
          <input type="file" accept="image/png,image/jpeg,image/webp,image/gif" multiple disabled={!canWrite || !!busy}
            onChange={(e) => { setImages([...(e.target.files ?? [])]); setReport(null); }} />
        </label>
      </div>
      <div className="form-actions">
        <button disabled={!canWrite || !workbook || !!busy} onClick={() => void run(false)}>Xem trước</button>
        <button className="primary" disabled={!canWrite || !workbook || !!busy || !ready || copying} onClick={() => void run(true)}>Ghi vào lớp</button>
        {busy && <span role="status">{busy}</span>}
      </div>
      {classes.length > 0 && (
        <details className="copy-from">
          <summary>Hoặc sao chép từ lớp khác</summary>
          <div className="form-actions">
            <label>
              Lớp nguồn
              <select value={source} onChange={(e) => { setSource(e.target.value); setReport(null); }} disabled={!canWrite || !!busy}>
                <option value="">Chọn lớp…</option>
                {classes.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
              </select>
            </label>
            <button disabled={!canWrite || !source || !!busy} onClick={() => void copy(false)}>Xem trước</button>
            <button className="primary" disabled={!canWrite || !source || !!busy || !ready || !copying} onClick={() => void copy(true)}>Sao chép vào lớp</button>
          </div>
        </details>
      )}
      {error && <p className="error" role="alert">{error}</p>}
      {report && <ReportView report={report} />}
    </section>
  );
}

function ReportView({ report }: { report: ImportReport }) {
  const row = (label: string, part: { added: number; updated: number }) => (
    <li>{label}: <strong>{part.added}</strong> thêm mới, <strong>{part.updated}</strong> cập nhật</li>
  );
  return (
    <div className={report.issues.length ? "import-report has-issues" : "import-report"} role="status">
      <p>
        {report.committed ? "Đã ghi vào lớp." : report.issues.length ? `Có ${report.issues.length} lỗi cần sửa; chưa ghi gì.` : "Xem trước: chưa ghi gì. Bấm Ghi vào lớp để lưu."}
        {report.curriculumCreated && " Lớp được tạo chương trình mới từ sheet Thông tin chung."}
      </p>
      <ul>
        {row("Kỹ năng", report.skills)}
        {row("Buổi", report.lessons)}
        {row("Câu hỏi", report.questions)}
      </ul>
      {report.missingImages.length > 0 && (
        <p className="error">Thiếu ảnh: {report.missingImages.join(", ")}. Chọn các ảnh này ở ô "Ảnh câu hỏi" rồi xem trước lại.</p>
      )}
      {report.issues.length > 0 && (
        <div className="table-wrap" tabIndex={0}>
          <table className="data-table">
            <thead><tr><th scope="col">Sheet</th><th scope="col">Dòng</th><th scope="col">Cột</th><th scope="col">Lỗi</th></tr></thead>
            <tbody>
              {report.issues.map((x, i) => (
                <tr key={i}><td>{x.sheet || "—"}</td><td>{x.row || "—"}</td><td>{x.column || "—"}</td><td>{x.message}</td></tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function LessonTable({ classroom, plan, curriculum, canWrite, denied, onPlan }: { classroom: TeacherClass; plan: LessonPlan; curriculum: Curriculum; canWrite: boolean; denied: (e: unknown) => void; onPlan: (p: LessonPlan) => void }) {
  const [open, setOpen] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  async function link(lessonId: string, sessionId: string | null) {
    setBusy(true);
    setError("");
    try {
      onPlan(await api<LessonPlan>(`${learningPath(classroom.id)}/lessons/${lessonId}/session`, "PUT", { sessionId }));
    } catch (e) {
      setError(e instanceof ApiError && e.status === 409 ? "Dữ liệu đã thay đổi. Tải lại trang rồi thử lại." : errorText(e, "Không thể đổi buổi học."));
      denied(e);
    } finally {
      setBusy(false);
    }
  }
  if (!plan.configured || plan.lessons.length === 0)
    return (
      <div className="empty">
        <h2>Chưa có buổi chương trình</h2>
        <p>Tải file mẫu, sửa theo lớp rồi tải lên để có danh sách buổi, chuẩn đầu ra và kỹ năng của từng buổi.</p>
      </div>
    );
  const skillName = (id: string) => curriculum.skills.find((s) => s.id === id);
  return (
    <section className="card" aria-label="Danh sách buổi chương trình">
      <h2>Buổi chương trình ({plan.lessons.length})</h2>
      <p className="hint">
        Buổi học thứ N của lớp (không tính buổi đã hủy) được tự gắn với Buổi N. Buổi đã diễn ra thì các kỹ năng của buổi được tính là đã dạy.
      </p>
      {error && <p className="error" role="alert">{error}</p>}
      <ol className="lesson-list">
        {plan.lessons.map((lesson) => (
          <li key={lesson.id} className={lesson.taught ? "taught" : undefined}>
            <button className="lesson-head" aria-expanded={open === lesson.id} onClick={() => setOpen(open === lesson.id ? null : lesson.id)}>
              <span className="lesson-no">Buổi {lesson.no}</span>
              <span className="lesson-title">{lesson.title}</span>
              <span className="lesson-codes">{lesson.skillCodes.join(", ")}</span>
              <span className="lesson-date">
                {lesson.session ? `${viDate(lesson.session.date)}${lesson.taught ? " · đã học" : ""}` : "Chưa có buổi học"}
              </span>
            </button>
            {open === lesson.id && (
              <div className="lesson-body">
                {lesson.topic && <p className="muted">{lesson.topic}</p>}
                <dl>
                  {lesson.knowledge && (<><dt>Kiến thức</dt><dd>{lesson.knowledge}</dd></>)}
                  {lesson.skillsText && (<><dt>Kỹ năng cần đạt</dt><dd>{lesson.skillsText}</dd></>)}
                  {lesson.outcomes && (<><dt>Chuẩn đầu ra</dt><dd>{lesson.outcomes}</dd></>)}
                  {lesson.competencies && (<><dt>Năng lực – thái độ</dt><dd>{lesson.competencies}</dd></>)}
                  <dt>Kỹ năng (mã)</dt>
                  <dd>{lesson.skillIds.map((id) => { const s = skillName(id); return s ? `${s.code} – ${s.name}` : ""; }).filter(Boolean).join("; ") || "—"}</dd>
                </dl>
                <label>
                  Buổi học của lớp
                  <select value={lesson.session?.id ?? ""} disabled={!canWrite || busy}
                    onChange={(e) => void link(lesson.id, e.target.value || null)}>
                    <option value="">Tự xếp theo thứ tự</option>
                    {plan.sessions.map((s) => (
                      <option key={s.id} value={s.id}>{viDate(s.date)} {s.startTime}{s.status === "held" ? " · đã học" : ""}</option>
                    ))}
                  </select>
                </label>
                {lesson.manual && <p className="hint">Đã gắn tay. Chọn "Tự xếp theo thứ tự" để bỏ.</p>}
              </div>
            )}
          </li>
        ))}
      </ol>
    </section>
  );
}
