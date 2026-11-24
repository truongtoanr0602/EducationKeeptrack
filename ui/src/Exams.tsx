import { useCallback, useEffect, useState } from "react";
import { api, ApiError, type TeacherClass } from "./api";
import {
  attemptStates, examLink, examStates, levelLabel, levels, pointsText, skillBand,
  type Blueprint, type Exam, type ExamPreview, type ExamSummary, type Question,
} from "./bank-api";
import { EssayGrading } from "./EssayGrading";
import { QuestionPreview } from "./QuestionBank";
import { errorText, learningPath, viDate, type Curriculum } from "./personalization-api";
import "./bank.css";

type Props = { classroom: TeacherClass; curriculum: Curriculum; canWrite: boolean; denied: (e: unknown) => void };

const fail = (e: unknown, fallback: string) =>
  e instanceof ApiError && e.status === 409 ? "Đề đã thay đổi ở phiên khác. Bấm Tải lại rồi thử lại." :
  e instanceof ApiError && e.errors ? Object.values(e.errors).flat().join(" ") : errorText(e, fallback);

// "Đề kiểm tra" (#80–#82): structure → preview → draft → open → close → confirmed results by skill.
export function ExamsTab({ classroom, curriculum, canWrite, denied }: Props) {
  const [list, setList] = useState<ExamSummary[] | null>(null);
  const [selected, setSelected] = useState<string | null>(() => new URLSearchParams(window.location.search).get("exam"));
  const [building, setBuilding] = useState(false);
  const [error, setError] = useState("");
  const load = useCallback(async () => {
    try {
      setList(await api<ExamSummary[]>(`${learningPath(classroom.id)}/exams`));
      setError("");
    } catch (e) {
      setError(errorText(e, "Không thể tải danh sách đề."));
      denied(e);
    }
  }, [classroom.id, denied]);
  useEffect(() => {
    void load();
  }, [load]);
  if (selected)
    return <ExamDetail classroom={classroom} examId={selected} canWrite={canWrite} denied={denied} onBack={() => { setSelected(null); void load(); }} />;
  return (
    <div className="stack">
      {building ? (
        <ExamBuilder classroom={classroom} curriculum={curriculum} denied={denied} onCancel={() => setBuilding(false)}
          onCreated={(id) => { setBuilding(false); setSelected(id); }} />
      ) : (
        <div className="form-actions">
          <button className="primary" disabled={!canWrite} onClick={() => setBuilding(true)}>Tạo đề kiểm tra</button>
        </div>
      )}
      {error && <p className="error" role="alert">{error}</p>}
      {list && list.length === 0 && !building && (
        <div className="empty"><h2>Chưa có đề kiểm tra</h2><p>Chọn số câu theo từng mức và phạm vi buổi; hệ thống bốc câu từ ngân hàng, phủ đều các kỹ năng.</p></div>
      )}
      {list && list.length > 0 && (
        <section className="card" aria-label="Danh sách đề kiểm tra">
          <h2>Đề kiểm tra</h2>
          <ul className="exam-list">
            {list.map((x) => (
              <li key={x.id}>
                <button className="exam-row" onClick={() => setSelected(x.id)}>
                  <strong>{x.title}</strong>
                  <span className={`status-chip status-${x.status}`}>{examStates[x.status]}</span>
                  <span>{x.questionCount} câu{x.durationMinutes ? ` · ${x.durationMinutes} phút` : ""}</span>
                  {x.status !== "draft" && <span>Đã nộp {x.submitted}/{x.seats}</span>}
                  <span className="muted">{viDate(x.createdAt.slice(0, 10))}</span>
                </button>
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}

function ExamBuilder({ classroom, curriculum, denied, onCancel, onCreated }: { classroom: TeacherClass; curriculum: Curriculum; denied: (e: unknown) => void; onCancel: () => void; onCreated: (id: string) => void }) {
  const [title, setTitle] = useState("Kiểm tra nhanh");
  const [counts, setCounts] = useState([4, 3, 2, 1]);
  const [mode, setMode] = useState<"lessons" | "skills">("lessons");
  const [from, setFrom] = useState(1);
  const [to, setTo] = useState(10);
  const [skillIds, setSkillIds] = useState<string[]>([]);
  const [duration, setDuration] = useState("30");
  const [blueprints, setBlueprints] = useState<Blueprint[]>([]);
  const [blueprintName, setBlueprintName] = useState("");
  const [preview, setPreview] = useState<ExamPreview | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [saved, setSaved] = useState("");
  const skills = curriculum.skills.filter((s) => !s.archived);
  useEffect(() => {
    api<Blueprint[]>(`${learningPath(classroom.id)}/exam-blueprints`).then(setBlueprints, () => undefined);
  }, [classroom.id]);
  const scope = mode === "lessons" ? { lessonFrom: from, lessonTo: to, skillIds: [] as string[] } : { lessonFrom: null, lessonTo: null, skillIds };
  const total = counts.reduce((a, b) => a + b, 0);
  async function draw() {
    setBusy(true);
    setError("");
    try {
      setPreview(await api<ExamPreview>(`${learningPath(classroom.id)}/exams/preview`, "POST", { counts, ...scope }));
    } catch (e) {
      setPreview(null);
      setError(fail(e, "Không sinh được đề."));
      denied(e);
    } finally {
      setBusy(false);
    }
  }
  async function create() {
    if (!preview) return;
    setBusy(true);
    setError("");
    try {
      const exam = await api<Exam>(`${learningPath(classroom.id)}/exams`, "POST", {
        title, counts, ...scope, durationMinutes: duration ? Number(duration) : null, questionIds: preview.questions.map((q) => q.id),
      });
      onCreated(exam.id);
    } catch (e) {
      setError(fail(e, "Không tạo được đề."));
      denied(e);
    } finally {
      setBusy(false);
    }
  }
  async function saveBlueprint() {
    setError("");
    try {
      setBlueprints(await api<Blueprint[]>(`${learningPath(classroom.id)}/exam-blueprints`, "POST", { name: blueprintName, counts, ...scope }));
      setSaved(`Đã lưu cấu trúc "${blueprintName}".`);
    } catch (e) {
      setError(fail(e, "Không lưu được cấu trúc đề."));
    }
  }
  function applyBlueprint(id: string) {
    const b = blueprints.find((x) => x.id === id);
    if (!b) return;
    setCounts(b.counts);
    setBlueprintName(b.name);
    if (b.skillIds.length > 0) { setMode("skills"); setSkillIds(b.skillIds); }
    else { setMode("lessons"); setFrom(b.lessonFrom ?? 1); setTo(b.lessonTo ?? 1); }
    setPreview(null);
  }
  return (
    <section className="card" aria-label="Tạo đề kiểm tra">
      <h2>Tạo đề kiểm tra</h2>
      <div className="form-row">
        <label className="grow">Tên đề<input required maxLength={160} value={title} onChange={(e) => setTitle(e.target.value)} /></label>
        <label>Thời lượng (phút)<input type="number" min={1} max={180} value={duration} onChange={(e) => setDuration(e.target.value)} placeholder="Không giới hạn" /></label>
        {blueprints.length > 0 && (
          <label>Cấu trúc đã lưu
            <select value="" onChange={(e) => applyBlueprint(e.target.value)}>
              <option value="">Chọn…</option>
              {blueprints.map((b) => <option key={b.id} value={b.id}>{b.name} ({b.counts.join("-")})</option>)}
            </select>
          </label>
        )}
      </div>
      <fieldset className="level-counts">
        <legend>Số câu theo mức · tổng {total} câu</legend>
        {levels.map((l, i) => (
          <label key={l.code}>{l.label}
            <input type="number" min={0} max={20} value={counts[i]} onChange={(e) => { setCounts(counts.map((c, j) => (j === i ? Number(e.target.value) : c))); setPreview(null); }} />
          </label>
        ))}
      </fieldset>
      <fieldset>
        <legend>Phạm vi</legend>
        <div className="form-row">
          <label className="checkbox-label"><input type="radio" checked={mode === "lessons"} onChange={() => { setMode("lessons"); setPreview(null); }} /> Theo buổi</label>
          <label className="checkbox-label"><input type="radio" checked={mode === "skills"} onChange={() => { setMode("skills"); setPreview(null); }} /> Theo kỹ năng</label>
        </div>
        {mode === "lessons" ? (
          <div className="form-row">
            <label>Từ buổi<input type="number" min={1} value={from} onChange={(e) => { setFrom(Number(e.target.value)); setPreview(null); }} /></label>
            <label>Đến buổi<input type="number" min={1} value={to} onChange={(e) => { setTo(Number(e.target.value)); setPreview(null); }} /></label>
          </div>
        ) : (
          <div className="skill-checks">
            {skills.map((s) => (
              <label key={s.id} className="checkbox-label">
                <input type="checkbox" checked={skillIds.includes(s.id)} onChange={(e) => { setSkillIds(e.target.checked ? [...skillIds, s.id] : skillIds.filter((x) => x !== s.id)); setPreview(null); }} />
                {s.code} – {s.name}
              </label>
            ))}
          </div>
        )}
      </fieldset>
      <div className="form-row">
        <label className="grow">Lưu cấu trúc này với tên<input maxLength={120} value={blueprintName} onChange={(e) => setBlueprintName(e.target.value)} placeholder="Ví dụ: Test nhanh 10 câu" /></label>
        <button type="button" disabled={!blueprintName.trim()} onClick={() => void saveBlueprint()}>Lưu cấu trúc</button>
      </div>
      {saved && <p role="status" className="success-text">{saved}</p>}
      <div className="form-actions">
        <button className="primary" disabled={busy || total === 0} onClick={() => void draw()}>{preview ? "Sinh lại" : "Sinh đề"}</button>
        <button type="button" onClick={onCancel} disabled={busy}>Huỷ</button>
      </div>
      {error && <p className="error" role="alert">{error}</p>}
      {preview && (
        <div className="exam-preview">
          <h3>Xem trước {preview.questions.length} câu</h3>
          <SkillCoverage questions={preview.questions} skills={preview.skills} />
          <ol className="question-list">
            {preview.questions.map((q) => (
              <li key={q.id}>
                <div className="question-meta"><strong>{q.code}</strong><span className={`level-chip level-${q.level}`}>{levelLabel(q.level)}</span>
                  <span>{preview.skills.find((s) => s.id === q.primarySkillId)?.code}</span></div>
                <QuestionPreview question={q} />
              </li>
            ))}
          </ol>
          <div className="form-actions">
            <button className="primary" disabled={busy || !title.trim()} onClick={() => void create()}>Tạo đề (bản nháp)</button>
          </div>
        </div>
      )}
    </section>
  );
}

function SkillCoverage({ questions, skills }: { questions: Question[]; skills: { id: string; code: string }[] }) {
  return (
    <p className="chip-row" aria-label="Số câu theo kỹ năng">
      {skills.map((s) => {
        const n = questions.filter((q) => q.primarySkillId === s.id).length;
        return <span key={s.id} className={n ? "chip" : "chip muted"}>{s.code}: {n}</span>;
      })}
    </p>
  );
}

function ExamDetail({ classroom, examId, canWrite, denied, onBack }: { classroom: TeacherClass; examId: string; canWrite: boolean; denied: (e: unknown) => void; onBack: () => void }) {
  const [exam, setExam] = useState<Exam | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [copied, setCopied] = useState(false);
  const path = `${learningPath(classroom.id)}/exams/${examId}`;
  const load = useCallback(async () => {
    try {
      setExam(await api<Exam>(path));
      setError("");
    } catch (e) {
      setError(fail(e, "Không tải được đề."));
      denied(e);
    }
  }, [path, denied]);
  useEffect(() => {
    void load();
  }, [load]);
  // While the exam is open the board refreshes itself, so the teacher sees who has joined and submitted.
  useEffect(() => {
    if (exam?.status !== "open") return;
    const timer = window.setInterval(() => void load(), 10000);
    return () => window.clearInterval(timer);
  }, [exam?.status, load]);
  async function act(action: string, body: Record<string, unknown>, method = "POST") {
    if (!exam) return;
    setBusy(true);
    setError("");
    try {
      setExam(await api<Exam>(`${path}/${action}`, method, { ...body, version: exam.version }));
    } catch (e) {
      setError(fail(e, "Không thực hiện được."));
      denied(e);
    } finally {
      setBusy(false);
    }
  }
  if (!exam) return <div className="stack"><button onClick={onBack}>← Danh sách đề</button>{error ? <p className="error" role="alert">{error}</p> : <p role="status">Đang tải…</p>}</div>;
  const link = examLink(exam.publicToken);
  const done = exam.seats.filter((s) => s.attemptStatus === "submitted" || s.attemptStatus === "auto_submitted").length;
  const waiting = exam.seats.reduce((sum, s) => sum + s.pendingEssays, 0);
  const hasEssay = exam.items.some((x) => x.type === "essay");
  return (
    <div className="stack exam-detail">
      <div className="form-actions no-print">
        <button onClick={onBack}>← Danh sách đề</button>
        <button onClick={() => void load()} disabled={busy}>Tải lại</button>
      </div>
      <section className="card">
        <div className="card-heading">
          <h2>{exam.title}</h2>
          <span className={`status-chip status-${exam.status}`}>{examStates[exam.status]}</span>
        </div>
        <p className="muted">
          {exam.items.length} câu ({exam.counts.map((n, i) => (n ? `${n} ${levels[i].short}` : "")).filter(Boolean).join(", ")})
          {exam.durationMinutes ? ` · ${exam.durationMinutes} phút` : " · không giới hạn thời gian"}
          {exam.lessonFrom ? ` · buổi ${exam.lessonFrom}–${exam.lessonTo}` : ""}
        </p>
        {error && <p className="error" role="alert">{error}</p>}
        {exam.status === "draft" && (
          <div className="form-actions no-print">
            <button className="primary" disabled={!canWrite || busy} onClick={() => void act("open", {})}>Mở đề cho học sinh làm</button>
            <span className="hint">Khi mở, mỗi học sinh nhận một mã 4 số. Sau khi mở thì không đổi câu được nữa.</span>
          </div>
        )}
        {exam.status === "open" && (
          <>
            <div className="exam-link no-print">
              <label className="grow">Đường link làm bài<input readOnly value={link} onFocus={(e) => e.target.select()} /></label>
              <button onClick={() => void navigator.clipboard?.writeText(link).then(() => setCopied(true))}>{copied ? "Đã chép" : "Chép link"}</button>
              <button onClick={() => window.print()}>In bảng mã</button>
            </div>
            <p>Đã nộp {done}/{exam.seats.length}. Bảng tự cập nhật mỗi 10 giây.</p>
            <div className="form-actions no-print">
              <button className="primary" disabled={!canWrite || busy}
                onClick={() => { if (window.confirm("Đóng bài? Bài đang làm sẽ được nộp tự động.")) void act("close", {}); }}>Đóng bài</button>
            </div>
          </>
        )}
        {(exam.status === "closed" || exam.status === "confirmed") && (
          <div className="form-actions no-print">
            <label className="checkbox-label">
              <input type="checkbox" checked={exam.showResults} disabled={!canWrite || busy} onChange={(e) => void act("show-results", { show: e.target.checked }, "PUT")} />
              Cho học sinh xem kết quả và đáp án
            </label>
            {exam.status === "closed" && (
              <button className="primary" disabled={!canWrite || busy || waiting > 0} aria-describedby={waiting > 0 ? "confirm-waits" : undefined}
                onClick={() => { if (window.confirm("Xác nhận kết quả? Kết quả sẽ vào bài đánh giá, kết quả theo kỹ năng và hồ sơ năng lực.")) void act("confirm", {}); }}>
                Xác nhận kết quả
              </button>
            )}
            {exam.status === "closed" && waiting > 0 && <span id="confirm-waits" className="hint">Chấm xong {waiting} bài tự luận rồi mới xác nhận được.</span>}
            {exam.status === "confirmed" && <span className="success-text">Kết quả đã vào bài đánh giá và hồ sơ năng lực.</span>}
          </div>
        )}
      </section>
      {hasEssay && (exam.status === "open" || exam.status === "closed") && done > 0 && (
        <section className="card no-print" aria-label="Chấm tự luận">
          <h2>Chấm tự luận{waiting > 0 ? ` (còn ${waiting} bài chờ chấm)` : ""}</h2>
          <EssayGrading path={path} onGraded={() => void load()} />
        </section>
      )}
      {exam.status !== "draft" && <SeatsBoard exam={exam} canWrite={canWrite} busy={busy} onReset={(studentId) => void act(`seats/${studentId}/reset`, {})} />}
      <section className="card no-print" aria-label="Câu hỏi của đề">
        <h2>Câu hỏi</h2>
        <ol className="question-list">
          {exam.items.map((item) => (
            <li key={item.order}>
              <div className="question-meta">
                <strong>Câu {item.order}</strong><span>{item.code}</span>
                <span className={`level-chip level-${item.level}`}>{levelLabel(item.level)}</span><span>{item.skillCode}</span>
                {exam.status === "draft" && (
                  <button disabled={!canWrite || busy} onClick={() => void act("swap", { order: item.order })}>Đổi câu khác</button>
                )}
              </div>
              <QuestionPreview question={item} />
            </li>
          ))}
        </ol>
      </section>
    </div>
  );
}

function SeatsBoard({ exam, canWrite, busy, onReset }: { exam: Exam; canWrite: boolean; busy: boolean; onReset: (studentId: string) => void }) {
  const finished = exam.status === "closed" || exam.status === "confirmed";
  return (
    <section className="card seats-board" aria-label="Học sinh và kết quả">
      <h2>{finished ? "Kết quả theo kỹ năng" : "Bảng mã vào bài"}</h2>
      {finished && (
        <p className="hint">
          Mỗi ô là số câu đúng tương đương / số câu của kỹ năng chính (điểm đạt / điểm tối đa, làm tròn theo số câu). <span className="band strong">xanh</span> ≥ 80%, <span className="band weak">đỏ</span> dưới 50%.
        </p>
      )}
      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              <th scope="col">Học sinh</th>
              {!finished && <th scope="col">Mã</th>}
              <th scope="col">Trạng thái</th>
              <th scope="col">Đúng</th>
              {finished && <th scope="col">Điểm</th>}
              {finished && exam.skills.map((s) => <th key={s.id} scope="col">{s.code} <span className="muted">({s.tasks})</span></th>)}
              {!finished && <th scope="col" className="no-print">Thao tác</th>}
            </tr>
          </thead>
          <tbody>
            {exam.seats.map((seat) => (
              <tr key={seat.studentId}>
                <th scope="row">{seat.studentName}</th>
                {!finished && <td className="seat-code">{seat.code}</td>}
                <td>{seat.locked ? "Khoá mã" : attemptStates[seat.attemptStatus]}{seat.attemptStatus === "in_progress" && ` · ${seat.answered}/${seat.total}`}</td>
                <td>{seat.attemptStatus === "submitted" || seat.attemptStatus === "auto_submitted" ? `${seat.correct}/${seat.total}` : "—"}</td>
                {finished && (
                  <td>
                    {seat.attemptStatus === "submitted" || seat.attemptStatus === "auto_submitted" ? `${pointsText(seat.score)}/${pointsText(seat.maxScore)}` : "—"}
                    {seat.pendingEssays > 0 && <span className="badge"> chờ chấm {seat.pendingEssays}</span>}
                  </td>
                )}
                {finished && exam.skills.map((s) => {
                  const cell = seat.skills?.find((x) => x.skillId === s.id);
                  return <td key={s.id} className={cell ? `band ${skillBand(cell.correct, cell.tasks)}` : undefined}>{cell ? `${cell.correct}/${cell.tasks}` : "—"}</td>;
                })}
                {!finished && (
                  <td className="no-print">
                    {(seat.locked || seat.attemptStatus !== "none") && (
                      <button disabled={!canWrite || busy}
                        onClick={() => { if (window.confirm(`Cho ${seat.studentName} làm lại từ đầu? Bài đang có sẽ bị xoá.`)) onReset(seat.studentId); }}>
                        {seat.locked ? "Mở khoá" : "Cho làm lại"}
                      </button>
                    )}
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
