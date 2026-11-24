import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import { createPortal } from "react-dom";
import { api, ApiError } from "./api";
import { stemParts, type FillMode, type QuestionType } from "./bank-api";
import { sanitizeStem } from "./rich-stem";
import "./bank.css";
import "./rich-stem.css";

// The student's exam page (ADR 0006): reached by link, no account. Big targets, one question per screen.
type Entry = {
  title: string;
  status: string;
  durationMinutes: number | null;
  questionCount: number;
  students: { id: string; name: string }[];
};
type TakeOption = { key: string; text: string; imageUrl: string | null };
type TakeItem = {
  order: number;
  number: number;
  type: QuestionType;
  stem: string;
  stemImageUrl: string | null;
  blanks: number;
  options: TakeOption[];
  points: number;
  fillMode: FillMode;
  /** How many options a multiple-choice question wants ticked; the keys themselves are never sent. */
  choose: number | null;
  stemHtml: string;
  audioUrl: string | null;
};
type ResultItem = {
  number: number;
  type: QuestionType;
  stem: string;
  stemImageUrl: string | null;
  given: string[] | null;
  correct: boolean;
  answer: string[];
  points: number;
  score: number;
};
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
  result: {
    correct: number;
    total: number;
    score: number;
    maxScore: number;
    pendingEssays: number;
    items: ResultItem[];
  } | null;
};

const message = (e: unknown, fallback: string) => {
  if (e instanceof ApiError)
    return e.errors
      ? (Object.values(e.errors).flat()[0] ?? e.message)
      : e.message;
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
      setError(
        e instanceof ApiError && e.status === 404
          ? "Không tìm thấy bài kiểm tra. Em kiểm tra lại đường link nhé."
          : message(e, "Không tải được bài. Em thử lại nhé."),
      );
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
        <span className="take-brand" aria-hidden="true">
          ✎
        </span>
        <h1>{attempt?.title ?? entry?.title ?? "Bài kiểm tra"}</h1>
      </header>
      {loading && <p role="status">Đang tải…</p>}
      {error && (
        <div className="take-card">
          <p className="error" role="alert">
            {error}
          </p>
          <button className="primary" onClick={() => void load()}>
            Thử lại
          </button>
        </div>
      )}
      {!loading && !error && entry && !attempt && (
        <Join base={base} entry={entry} onJoined={setAttempt} />
      )}
      {!loading && !error && attempt && (
        <AttemptView
          base={base}
          attempt={attempt}
          onChange={setAttempt}
          onExpired={() => void load()}
        />
      )}
    </main>
  );
}

function Join({
  base,
  entry,
  onJoined,
}: {
  base: string;
  entry: Entry;
  onJoined: (a: Attempt) => void;
}) {
  const [student, setStudent] = useState<{ id: string; name: string } | null>(
    null,
  );
  const [code, setCode] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const open = entry.status === "open";
  if (!open && entry.students.length === 0)
    return (
      <div className="take-card">
        <p>
          Bài kiểm tra đã đóng. Khi thầy cô cho xem kết quả, em mở lại đường
          link này, chọn tên và nhập mã để xem nhé.
        </p>
      </div>
    );
  async function join() {
    if (!student) return;
    setBusy(true);
    setError("");
    try {
      onJoined(
        await api<Attempt>(`${base}/join`, "POST", {
          studentId: student.id,
          code,
        }),
      );
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
          {open
            ? `${entry.questionCount} câu${entry.durationMinutes ? ` · ${entry.durationMinutes} phút` : ""}. Em bấm vào tên của mình nhé.`
            : "Thầy cô đã cho xem kết quả. Em bấm vào tên của mình rồi nhập mã nhé."}
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
    <form
      className="take-card"
      aria-label="Nhập mã vào bài"
      onSubmit={(e) => {
        e.preventDefault();
        void join();
      }}
    >
      <p className="take-lead">
        Chào <strong>{student.name}</strong>! Em nhập mã 4 số thầy cô đưa cho
        em.
      </p>
      <label className="take-code">
        Mã vào bài
        <input
          inputMode="numeric"
          autoComplete="one-time-code"
          pattern="[0-9]{4}"
          maxLength={4}
          required
          autoFocus
          value={code}
          onChange={(e) =>
            setCode(e.target.value.replace(/\D/g, "").slice(0, 4))
          }
          disabled={busy}
        />
      </label>
      {error && (
        <p className="error" role="alert">
          {error}
        </p>
      )}
      <div className="take-actions">
        <button
          type="button"
          onClick={() => {
            setStudent(null);
            setCode("");
            setError("");
          }}
          disabled={busy}
        >
          Không phải em
        </button>
        <button className="primary" disabled={busy || code.length !== 4}>
          {open ? "Vào làm bài" : "Xem kết quả"}
        </button>
      </div>
    </form>
  );
}

function useCountdown(deadline: string | null, serverNow: string) {
  const offset = useMemo(() => Date.parse(serverNow) - Date.now(), [serverNow]);
  const [left, setLeft] = useState<number | null>(null);
  useEffect(() => {
    if (!deadline) return;
    const tick = () =>
      setLeft(Math.max(0, Date.parse(deadline) - (Date.now() + offset)));
    tick();
    const timer = window.setInterval(tick, 1000);
    return () => window.clearInterval(timer);
  }, [deadline, offset]);
  return left;
}

function AttemptView({
  base,
  attempt,
  onChange,
  onExpired,
}: {
  base: string;
  attempt: Attempt;
  onChange: (a: Attempt) => void;
  onExpired: () => void;
}) {
  const running =
    attempt.attemptStatus === "in_progress" && attempt.items !== null;
  const items = attempt.items ?? [];
  const [index, setIndex] = useState(0);
  const [answers, setAnswers] = useState<Record<string, string[]>>(
    () => attempt.answers ?? {},
  );
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
        <p className="take-big" aria-hidden="true">
          🌟
        </p>
        <h2>Em đã nộp bài, giỏi lắm {attempt.studentName}!</h2>
        {attempt.attemptStatus === "auto_submitted" && (
          <p>Bài được nộp tự động khi hết giờ hoặc khi thầy cô đóng bài.</p>
        )}
        {!attempt.result && (
          <p>Thầy cô sẽ xem bài và cho em biết kết quả sau nhé.</p>
        )}
        {attempt.result && <ResultList result={attempt.result} />}
      </section>
    );

  const item = items[index];
  const answered = items.filter((x) =>
    (answers[String(x.order)] ?? []).some((v) => v),
  ).length;
  async function save(order: number, values: string[]) {
    setAnswers((a) => ({ ...a, [String(order)]: values }));
    setSaving("saving");
    try {
      const saved = await api<{ attemptStatus: string }>(
        `${base}/attempt/answers/${order}`,
        "PUT",
        { values },
      );
      setSaving(saved.attemptStatus === "in_progress" ? "saved" : "error");
      if (saved.attemptStatus !== "in_progress") onExpired();
    } catch (e) {
      setSaving("error");
      if (e instanceof ApiError && (e.status === 401 || e.status === 400))
        onExpired();
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
        <span>
          Đã làm {answered}/{items.length}
        </span>
        {left !== null && (
          <span
            className={left < 60000 ? "take-timer warn" : "take-timer"}
            role="timer"
            aria-live="off"
          >
            ⏱ {minutes}:{String(seconds).padStart(2, "0")}
          </span>
        )}
      </div>
      <nav className="take-dots" aria-label="Chọn câu">
        {items.map((x, i) => (
          <button
            key={x.order}
            aria-current={i === index ? "step" : undefined}
            className={
              (answers[String(x.order)] ?? []).some((v) => v)
                ? "done"
                : undefined
            }
            onClick={() => setIndex(i)}
          >
            {x.number}
          </button>
        ))}
      </nav>
      {item && (
        <QuestionView
          key={item.order}
          item={item}
          value={answers[String(item.order)] ?? []}
          onChange={(values) => void save(item.order, values)}
        />
      )}
      <p className="take-saving" role="status">
        {saving === "saving"
          ? "Đang lưu…"
          : saving === "saved"
            ? "Đã lưu ✓"
            : saving === "error"
              ? "Chưa lưu được, em bấm lại đáp án nhé."
              : ""}
      </p>
      <div className="take-actions">
        <button disabled={index === 0} onClick={() => setIndex(index - 1)}>
          ← Câu trước
        </button>
        {index < items.length - 1 ? (
          <button className="primary" onClick={() => setIndex(index + 1)}>
            Câu sau →
          </button>
        ) : (
          <button className="primary" onClick={() => setConfirming(true)}>
            Nộp bài
          </button>
        )}
      </div>
      {confirming && (
        <div className="take-confirm" role="alertdialog" aria-label="Nộp bài">
          <p>
            {answered < items.length
              ? `Em còn ${items.length - answered} câu chưa làm. `
              : "Em đã làm hết các câu. "}
            Em nộp bài nhé? Nộp rồi thì không sửa được nữa.
          </p>
          <div className="take-actions">
            <button onClick={() => setConfirming(false)} disabled={busy}>
              Làm tiếp
            </button>
            <button
              className="primary"
              onClick={() => void submit()}
              disabled={busy}
            >
              Nộp bài
            </button>
          </div>
        </div>
      )}
    </section>
  );
}

const points = (value: number) =>
  value.toLocaleString("vi-VN", { maximumFractionDigits: 2 });

/** Saves what the child types a moment after the last key, and at once when the field is left or the question changes. */
function useLateSave(onChange: (values: string[]) => void) {
  const latest = useRef<string[] | null>(null);
  const timer = useRef<number | null>(null);
  const send = useRef(onChange);
  send.current = onChange;
  const flush = useCallback(() => {
    if (timer.current !== null) window.clearTimeout(timer.current);
    timer.current = null;
    if (latest.current) send.current(latest.current);
    latest.current = null;
  }, []);
  useEffect(() => flush, [flush]);
  return {
    queue(values: string[]) {
      latest.current = values;
      if (timer.current !== null) window.clearTimeout(timer.current);
      timer.current = window.setTimeout(flush, 600);
    },
    flush,
  };
}

const escapeHtml = (text: string) =>
  text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
/** A stem kept as plain text: line breaks and {{n}} markers become the same markup a rich stem has. */
const plainToHtml = (text: string) =>
  escapeHtml(text)
    .replace(/\n/g, "<br>")
    .replace(
      /\{\{(\d+)\}\}/g,
      '<span class="blank" data-blank="$1">{{$1}}</span>',
    );

/** The stem of a typed fill-in with a real input in place of each blank. The text keeps its formatting; only the blanks are replaced. */
function TypedStem({
  item,
  value,
  onType,
  onLeave,
}: {
  item: TakeItem;
  value: string[];
  onType: (n: number, text: string) => void;
  onLeave: () => void;
}) {
  const root = useRef<HTMLDivElement>(null);
  const [slots, setSlots] = useState<{ n: number; el: HTMLElement }[]>([]);
  const html = useMemo(
    () => sanitizeStem(item.stemHtml || plainToHtml(item.stem)),
    [item.stemHtml, item.stem],
  );
  useLayoutEffect(() => {
    const el = root.current;
    if (!el) return;
    el.innerHTML = html;
    const found = [...el.querySelectorAll<HTMLElement>("span[data-blank]")].map(
      (span) => {
        span.textContent = "";
        span.classList.add("blank-slot");
        return { n: Number(span.dataset.blank), el: span };
      },
    );
    setSlots(found);
  }, [html]);
  return (
    <>
      <div className="take-stem rich-stem" ref={root} />
      {slots.map(({ n, el }) =>
        createPortal(
          <input
            className="blank-input"
            aria-label={`Chỗ trống ${n}`}
            maxLength={100}
            autoComplete="off"
            autoCapitalize="off"
            spellCheck={false}
            value={value[n - 1] ?? ""}
            onChange={(e) => onType(n, e.target.value)}
            onBlur={onLeave}
          />,
          el,
          `blank-${n}`,
        ),
      )}
    </>
  );
}

function QuestionView({
  item,
  value,
  onChange,
}: {
  item: TakeItem;
  value: string[];
  onChange: (values: string[]) => void;
}) {
  const [slot, setSlot] = useState(() =>
    Math.max(
      0,
      value.findIndex((x) => !x),
    ),
  );
  // What the child is typing is kept here, so the field answers at once while the save waits for a pause.
  const [typed, setTyped] = useState<string[]>(value);
  const late = useLateSave(onChange);
  const header = (
    <>
      <p className="take-number">
        Câu {item.number}{" "}
        <span className="take-points">({points(item.points)} điểm)</span>
      </p>
      {item.audioUrl && (
        <p className="take-audio">
          <audio
            controls
            preload="metadata"
            src={item.audioUrl}
            aria-label="Nghe đoạn âm thanh của câu này"
          />
        </p>
      )}
    </>
  );
  if (item.type === "single_choice")
    return (
      <div className="take-question">
        {header}
        <p className="take-stem">{item.stem}</p>
        {item.stemImageUrl && (
          <img
            className="take-image"
            src={item.stemImageUrl}
            alt="Hình của câu hỏi"
          />
        )}
        <div
          className="take-options"
          role="radiogroup"
          aria-label={`Câu ${item.number}`}
        >
          {item.options.map((o) => (
            <button
              key={o.key}
              role="radio"
              aria-checked={value[0] === o.key}
              className={value[0] === o.key ? "chosen" : undefined}
              onClick={() => onChange([o.key])}
            >
              {o.imageUrl ? (
                <img src={o.imageUrl} alt={o.text || `Phương án ${o.key}`} />
              ) : (
                o.text
              )}
            </button>
          ))}
        </div>
      </div>
    );
  if (item.type === "multi_choice") {
    const chosen = new Set(value.filter(Boolean));
    const toggle = (key: string) =>
      onChange(
        item.options
          .map((o) => o.key)
          .filter((k) => (k === key ? !chosen.has(k) : chosen.has(k))),
      );
    return (
      <div className="take-question">
        {header}
        <p className="take-stem">{item.stem}</p>
        {item.stemImageUrl && (
          <img
            className="take-image"
            src={item.stemImageUrl}
            alt="Hình của câu hỏi"
          />
        )}
        {item.choose !== null && (
          <p className="take-hint" id={`choose-${item.order}`}>
            Chọn {item.choose} đáp án.
          </p>
        )}
        <div
          className="take-options"
          role="group"
          aria-label={`Câu ${item.number}`}
          aria-describedby={
            item.choose !== null ? `choose-${item.order}` : undefined
          }
        >
          {item.options.map((o) => (
            <button
              key={o.key}
              role="checkbox"
              aria-checked={chosen.has(o.key)}
              className={chosen.has(o.key) ? "chosen" : undefined}
              onClick={() => toggle(o.key)}
            >
              {o.imageUrl ? (
                <img src={o.imageUrl} alt={o.text || `Phương án ${o.key}`} />
              ) : (
                o.text
              )}
            </button>
          ))}
        </div>
      </div>
    );
  }
  if (item.type === "essay")
    return (
      <div className="take-question">
        {header}
        <p className="take-stem">{item.stem}</p>
        {item.stemImageUrl && (
          <img
            className="take-image"
            src={item.stemImageUrl}
            alt="Hình của câu hỏi"
          />
        )}
        <label className="take-essay">
          Bài làm của em
          <textarea
            rows={8}
            maxLength={2000}
            value={typed[0] ?? ""}
            onChange={(e) => {
              setTyped([e.target.value]);
              late.queue([e.target.value]);
            }}
            onBlur={late.flush}
          />
          <span className="take-hint">
            {(typed[0] ?? "").length}/2000 ký tự
          </span>
        </label>
      </div>
    );
  if (item.fillMode === "type") {
    const type = (n: number, text: string) => {
      const next = Array.from({ length: item.blanks }, (_, i) =>
        i === n - 1 ? text : (typed[i] ?? ""),
      );
      setTyped(next);
      late.queue(next);
    };
    return (
      <div className="take-question">
        {header}
        {item.stemImageUrl && (
          <img
            className="take-image"
            src={item.stemImageUrl}
            alt="Hình của câu hỏi"
          />
        )}
        <TypedStem
          item={item}
          value={typed}
          onType={type}
          onLeave={late.flush}
        />
        <p className="take-hint">Em gõ câu trả lời vào từng chỗ trống.</p>
      </div>
    );
  }
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
      {header}
      {item.stemImageUrl && (
        <img
          className="take-image"
          src={item.stemImageUrl}
          alt="Hình của câu hỏi"
        />
      )}
      <p className="take-stem take-fill">
        {parts.map((part, i) => (
          <span key={i}>
            {part}
            {i < parts.length - 1 && (
              <button
                className={`take-blank${i === slot ? " selected" : ""}${filled[i] ? " filled" : ""}`}
                aria-label={
                  filled[i]
                    ? `Ô ${i + 1}: ${filled[i]}. Bấm để xoá`
                    : `Ô ${i + 1} trống`
                }
                onClick={() => {
                  if (filled[i] && i === slot) {
                    const next = [...filled];
                    next[i] = "";
                    onChange(next);
                  }
                  setSlot(i);
                }}
              >
                {filled[i] || "?"}
              </button>
            )}
          </span>
        ))}
      </p>
      <p className="take-hint">
        Bấm vào ô trống rồi chọn số hoặc dấu bên dưới.
      </p>
      <div className="take-chips">
        {item.options.map((o) => (
          <button key={o.key} onClick={() => put(o.text)}>
            {o.text}
          </button>
        ))}
      </div>
    </div>
  );
}

function ResultList({ result }: { result: NonNullable<Attempt["result"]> }) {
  const tenth =
    result.maxScore > 0
      ? Math.round((result.score / result.maxScore) * 1000) / 100
      : 0;
  return (
    <div className="take-result">
      <p className="take-score">
        Em được <strong>{points(result.score)}</strong> /{" "}
        {points(result.maxScore)} điểm (thang 10:{" "}
        <strong>{points(tenth)}</strong>).
        <span className="take-hint">
          {" "}
          Làm đúng hết {result.correct}/{result.total} câu.
        </span>
      </p>
      {result.pendingEssays > 0 && (
        <p className="take-hint" role="status">
          Chưa gồm phần tự luận: thầy cô sẽ chấm rồi cho em biết điểm cuối cùng.
        </p>
      )}
      <ol>
        {result.items.map((x) => {
          const essay = x.type === "essay";
          const partial = !x.correct && x.score > 0;
          return (
            <li
              key={x.number}
              className={
                essay
                  ? "pending"
                  : x.correct
                    ? "right"
                    : partial
                      ? "partial"
                      : "wrong"
              }
            >
              <span aria-hidden="true">
                {essay ? "✎" : x.correct ? "✓" : partial ? "◐" : "✗"}
              </span>{" "}
              Câu {x.number}: {x.stem}
              <span className="take-key">
                {" "}
                ·{" "}
                {essay
                  ? "Chờ thầy cô chấm"
                  : `${points(x.score)}/${points(x.points)} điểm`}
              </span>
              {!x.correct && !essay && (
                <span className="take-key">
                  {" "}
                  · Đáp án: {x.answer.join(" ; ")}
                </span>
              )}
            </li>
          );
        })}
      </ol>
    </div>
  );
}
