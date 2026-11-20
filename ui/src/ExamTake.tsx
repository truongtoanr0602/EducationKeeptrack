import { useCallback, useEffect, useMemo, useState } from "react";
import { api, ApiError } from "./api";
import { stemParts, type QuestionType } from "./bank-api";
import "./bank.css";

// The student's exam page (ADR 0006): reached by link, no account. Big targets, one question per screen.
type Entry = { title: string; status: string; durationMinutes: number | null; questionCount: number; students: { id: string; name: string }[] };
type TakeOption = { key: string; text: string; imageUrl: string | null };
type TakeItem = { order: number; number: number; type: QuestionType; stem: string; stemImageUrl: string | null; blanks: number; options: TakeOption[] };
type ResultItem = { number: number; type: QuestionType; stem: string; stemImageUrl: string | null; given: string[] | null; correct: boolean; answer: string[] };
type Attempt = {
  title: string;
  examStatus: string;
  attemptStatus: "none" | "in_progress" | "submitted" | "auto_submitted";
  studentName: string;
  deadline: string | null;
  now: string;
  total: number;
  answered: number;
  items: TakeItem[] | null;
  answers: Record<string, string[]> | null;
  result: { correct: number; total: number; items: ResultItem[] } | null;
};

const message = (e: unknown, fallback: string) => {
  if (e instanceof ApiError) return e.errors ? Object.values(e.errors).flat()[0] ?? e.message : e.message;
  return fallback;
};

export function ExamTake({ token }: { token: string }) {
  const base = `/exam-take/${token}`;
  const [entry, setEntry] = useState<Entry | null>(null);
  const [attempt, setAttempt] = useState<Attempt | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const load = useCallback(async () => {
    setLoading(true);
    try {
      setEntry(await api<Entry>(base));
      try {
        setAttempt(await api<Attempt>(`${base}/attempt`));
      } catch (e) {
        if (!(e instanceof ApiError && e.status === 401)) throw e;
        setAttempt(null);
      }
      setError("");
    } catch (e) {
      setError(e instanceof ApiError && e.status === 404 ? "Không tìm thấy bài kiểm tra. Em kiểm tra lại đường link nhé." : message(e, "Không tải được bài. Em thử lại nhé."));
    } finally {
      setLoading(false);
    }
  }, [base]);
  useEffect(() => {
    document.title = "Làm bài kiểm tra";
    void load();
  }, [load]);

  return (
    <main className="take" lang="vi">
      <header className="take-header">
        <span className="take-brand" aria-hidden="true">✎</span>
        <h1>{attempt?.title ?? entry?.title ?? "Bài kiểm tra"}</h1>
      </header>
      {loading && <p role="status">Đang tải…</p>}
      {error && (
        <div className="take-card">
          <p className="error" role="alert">{error}</p>
          <button className="primary" onClick={() => void load()}>Thử lại</button>
        </div>
      )}
      {!loading && !error && entry && !attempt && <Join base={base} entry={entry} onJoined={setAttempt} />}
      {!loading && !error && attempt && <AttemptView base={base} attempt={attempt} onChange={setAttempt} onExpired={() => void load()} />}
    </main>
  );
}

function Join({ base, entry, onJoined }: { base: string; entry: Entry; onJoined: (a: Attempt) => void }) {
  const [student, setStudent] = useState<{ id: string; name: string } | null>(null);
  const [code, setCode] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  if (entry.status !== "open")
    return (
      <div className="take-card">
        <p>Bài kiểm tra đã đóng. Nếu em đã làm bài, em vào lại bằng tên và mã để xem kết quả khi thầy cô cho phép.</p>
      </div>
    );
  async function join() {
    if (!student) return;
    setBusy(true);
    setError("");
    try {
      onJoined(await api<Attempt>(`${base}/join`, "POST", { studentId: student.id, code }));
    } catch (e) {
      setError(message(e, "Chưa vào được bài. Em thử lại nhé."));
      setCode("");
    } finally {
      setBusy(false);
    }
  }
  if (!student)
    return (
      <section className="take-card" aria-label="Chọn tên">
        <p className="take-lead">
          {entry.questionCount} câu{entry.durationMinutes ? ` · ${entry.durationMinutes} phút` : ""}. Em bấm vào tên của mình nhé.
        </p>
        <ul className="take-names">
          {entry.students.map((s) => (
            <li key={s.id}>
              <button onClick={() => setStudent(s)}>{s.name}</button>
            </li>
          ))}
        </ul>
      </section>
    );
  return (
    <form className="take-card" aria-label="Nhập mã vào bài" onSubmit={(e) => { e.preventDefault(); void join(); }}>
      <p className="take-lead">Chào <strong>{student.name}</strong>! Em nhập mã 4 số thầy cô đưa cho em.</p>
      <label className="take-code">
        Mã vào bài
        <input inputMode="numeric" autoComplete="one-time-code" pattern="[0-9]{4}" maxLength={4} required autoFocus
          value={code} onChange={(e) => setCode(e.target.value.replace(/\D/g, "").slice(0, 4))} disabled={busy} />
      </label>
      {error && <p className="error" role="alert">{error}</p>}
      <div className="take-actions">
        <button type="button" onClick={() => { setStudent(null); setCode(""); setError(""); }} disabled={busy}>Không phải em</button>
        <button className="primary" disabled={busy || code.length !== 4}>Vào làm bài</button>
      </div>
    </form>
  );
}

function useCountdown(deadline: string | null, serverNow: string) {
  const offset = useMemo(() => Date.parse(serverNow) - Date.now(), [serverNow]);
  const [left, setLeft] = useState<number | null>(null);
  useEffect(() => {
    if (!deadline) return;
    const tick = () => setLeft(Math.max(0, Date.parse(deadline) - (Date.now() + offset)));
    tick();
    const timer = window.setInterval(tick, 1000);
    return () => window.clearInterval(timer);
  }, [deadline, offset]);
  return left;
}

function AttemptView({ base, attempt, onChange, onExpired }: { base: string; attempt: Attempt; onChange: (a: Attempt) => void; onExpired: () => void }) {
  const running = attempt.attemptStatus === "in_progress" && attempt.items !== null;
  const items = attempt.items ?? [];
  const [index, setIndex] = useState(0);
  const [answers, setAnswers] = useState<Record<string, string[]>>(() => attempt.answers ?? {});
  const [saving, setSaving] = useState<"" | "saving" | "saved" | "error">("");
  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState(false);
  const left = useCountdown(attempt.deadline, attempt.now);
  useEffect(() => {
    if (running && left === 0) onExpired();
  }, [running, left, onExpired]);

  if (!running)
    return (
      <section className="take-card take-done" aria-label="Đã nộp bài">
        <p className="take-big" aria-hidden="true">🌟</p>
        <h2>Em đã nộp bài, giỏi lắm {attempt.studentName}!</h2>
        {attempt.attemptStatus === "auto_submitted" && <p>Bài được nộp tự động khi hết giờ hoặc khi thầy cô đóng bài.</p>}
        {!attempt.result && <p>Thầy cô sẽ xem bài và cho em biết kết quả sau nhé.</p>}
        {attempt.result && <ResultList result={attempt.result} />}
      </section>
    );

  const item = items[index];
  const answered = items.filter((x) => (answers[String(x.order)] ?? []).some((v) => v)).length;
  async function save(order: number, values: string[]) {
    setAnswers((a) => ({ ...a, [String(order)]: values }));
    setSaving("saving");
    try {
      const saved = await api<{ attemptStatus: string }>(`${base}/attempt/answers/${order}`, "PUT", { values });
      setSaving(saved.attemptStatus === "in_progress" ? "saved" : "error");
      if (saved.attemptStatus !== "in_progress") onExpired();
    } catch (e) {
      setSaving("error");
      if (e instanceof ApiError && (e.status === 401 || e.status === 400)) onExpired();
    }
  }
  async function submit() {
    setBusy(true);
    try {
      onChange(await api<Attempt>(`${base}/attempt/submit`, "POST", {}));
    } catch {
      setSaving("error");
    } finally {
      setBusy(false);
      setConfirming(false);
    }
  }
  const minutes = left === null ? null : Math.floor(left / 60000);
  const seconds = left === null ? null : Math.floor((left % 60000) / 1000);
  return (
    <section className="take-card" aria-label="Làm bài">
      <div className="take-status">
        <span>{attempt.studentName}</span>
        <span>Đã làm {answered}/{items.length}</span>
        {left !== null && (
          <span className={left < 60000 ? "take-timer warn" : "take-timer"} role="timer" aria-live="off">
            ⏱ {minutes}:{String(seconds).padStart(2, "0")}
          </span>
        )}
      </div>
      <nav className="take-dots" aria-label="Chọn câu">
        {items.map((x, i) => (
          <button key={x.order} aria-current={i === index ? "step" : undefined}
            className={(answers[String(x.order)] ?? []).some((v) => v) ? "done" : undefined} onClick={() => setIndex(i)}>
            {x.number}
          </button>
        ))}
      </nav>
      {item && (
        <QuestionView key={item.order} item={item} value={answers[String(item.order)] ?? []} onChange={(values) => void save(item.order, values)} />
      )}
      <p className="take-saving" role="status">
        {saving === "saving" ? "Đang lưu…" : saving === "saved" ? "Đã lưu ✓" : saving === "error" ? "Chưa lưu được, em bấm lại đáp án nhé." : ""}
      </p>
      <div className="take-actions">
        <button disabled={index === 0} onClick={() => setIndex(index - 1)}>← Câu trước</button>
        {index < items.length - 1 ? (
          <button className="primary" onClick={() => setIndex(index + 1)}>Câu sau →</button>
        ) : (
          <button className="primary" onClick={() => setConfirming(true)}>Nộp bài</button>
        )}
      </div>
      {confirming && (
        <div className="take-confirm" role="alertdialog" aria-label="Nộp bài">
          <p>
            {answered < items.length ? `Em còn ${items.length - answered} câu chưa làm. ` : "Em đã làm hết các câu. "}
            Em nộp bài nhé? Nộp rồi thì không sửa được nữa.
          </p>
          <div className="take-actions">
            <button onClick={() => setConfirming(false)} disabled={busy}>Làm tiếp</button>
            <button className="primary" onClick={() => void submit()} disabled={busy}>Nộp bài</button>
          </div>
        </div>
      )}
    </section>
  );
}

function QuestionView({ item, value, onChange }: { item: TakeItem; value: string[]; onChange: (values: string[]) => void }) {
  const [slot, setSlot] = useState(() => Math.max(0, value.findIndex((x) => !x)));
  if (item.type === "single_choice")
    return (
      <div className="take-question">
        <p className="take-number">Câu {item.number}</p>
        <p className="take-stem">{item.stem}</p>
        {item.stemImageUrl && <img className="take-image" src={item.stemImageUrl} alt="Hình của câu hỏi" />}
        <div className="take-options" role="radiogroup" aria-label={`Câu ${item.number}`}>
          {item.options.map((o) => (
            <button key={o.key} role="radio" aria-checked={value[0] === o.key} className={value[0] === o.key ? "chosen" : undefined}
              onClick={() => onChange([o.key])}>
              {o.imageUrl ? <img src={o.imageUrl} alt={o.text || `Phương án ${o.key}`} /> : o.text}
            </button>
          ))}
        </div>
      </div>
    );
  const parts = stemParts(item.stem);
  const filled = Array.from({ length: item.blanks }, (_, i) => value[i] ?? "");
  function put(chip: string) {
    const next = [...filled];
    next[slot] = chip;
    onChange(next);
    const empty = next.findIndex((x, i) => !x && i !== slot);
    if (empty >= 0) setSlot(empty);
  }
  return (
    <div className="take-question">
      <p className="take-number">Câu {item.number}</p>
      {item.stemImageUrl && <img className="take-image" src={item.stemImageUrl} alt="Hình của câu hỏi" />}
      <p className="take-stem take-fill">
        {parts.map((part, i) => (
          <span key={i}>
            {part}
            {i < parts.length - 1 && (
              <button className={`take-blank${i === slot ? " selected" : ""}${filled[i] ? " filled" : ""}`}
                aria-label={filled[i] ? `Ô ${i + 1}: ${filled[i]}. Bấm để xoá` : `Ô ${i + 1} trống`}
                onClick={() => {
                  if (filled[i] && i === slot) { const next = [...filled]; next[i] = ""; onChange(next); }
                  setSlot(i);
                }}>
                {filled[i] || "?"}
              </button>
            )}
          </span>
        ))}
      </p>
      <p className="take-hint">Bấm vào ô trống rồi chọn số hoặc dấu bên dưới.</p>
      <div className="take-chips">
        {item.options.map((o) => (
          <button key={o.key} onClick={() => put(o.text)}>{o.text}</button>
        ))}
      </div>
    </div>
  );
}

function ResultList({ result }: { result: NonNullable<Attempt["result"]> }) {
  return (
    <div className="take-result">
      <p className="take-score">Em làm đúng <strong>{result.correct}</strong> / {result.total} câu.</p>
      <ol>
        {result.items.map((x) => (
          <li key={x.number} className={x.correct ? "right" : "wrong"}>
            <span aria-hidden="true">{x.correct ? "✓" : "✗"}</span> Câu {x.number}: {x.stem}
            {!x.correct && <span className="take-key"> · Đáp án: {x.answer.join(" ; ")}</span>}
          </li>
        ))}
      </ol>
    </div>
  );
}
