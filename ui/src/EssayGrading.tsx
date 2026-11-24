import { useCallback, useEffect, useMemo, useState } from "react";
import { api, ApiError } from "./api";
import { pointsText } from "./bank-api";
import "./bank.css";

type Response = {
  studentId: string;
  studentName: string;
  answer: string;
  points: number | null;
  comment: string | null;
  gradedAt: string | null;
};
type Essay = {
  order: number;
  code: string;
  stem: string;
  stemImageUrl: string | null;
  points: number;
  sampleAnswer: string | null;
  responses: Response[];
};
type Board = {
  pending: number;
  essays: Essay[];
  students: {
    studentId: string;
    studentName: string;
    score: number;
    maxScore: number;
    pendingEssays: number;
  }[];
};
type Saved = {
  studentId: string;
  score: number;
  maxScore: number;
  pendingEssays: number;
};

const fail = (e: unknown) =>
  e instanceof ApiError && e.errors
    ? Object.values(e.errors).flat().join(" ")
    : e instanceof ApiError && e.status === 409
      ? "Bài đã đổi ở phiên khác. Tải lại rồi chấm tiếp."
      : "Không lưu được điểm. Thử lại nhé.";

/** The points the teacher can give: 0 to the question's points in steps of a quarter. */
const options = (max: number) =>
  Array.from({ length: Math.round(max / 0.25) + 1 }, (_, i) => i * 0.25);

/**
 * Scoring of the essays of one exam (ADR 0008). By question is the default: the same question across the class, one answer at a time,
 * "Lưu và sang bài kế" goes on to the next answer still without points. By student shows one child's essays together.
 */
export function EssayGrading({
  path,
  onGraded,
}: {
  path: string;
  onGraded: () => void;
}) {
  const [board, setBoard] = useState<Board | null>(null);
  const [error, setError] = useState("");
  const [mode, setMode] = useState<"question" | "student">("question");
  const [orderOf, setOrderOf] = useState<number | null>(null);
  const [student, setStudent] = useState<string | null>(null);
  const [at, setAt] = useState(0);
  const load = useCallback(async () => {
    try {
      const next = await api<Board>(`${path}/grading`);
      setBoard(next);
      setOrderOf((o) => o ?? next.essays[0]?.order ?? null);
      setStudent((s) => s ?? next.students[0]?.studentId ?? null);
      setError("");
    } catch {
      setError("Không tải được các bài tự luận. Bấm Tải lại.");
    }
  }, [path]);
  useEffect(() => {
    void load();
  }, [load]);

  async function save(
    order: number,
    studentId: string,
    points: number,
    comment: string,
  ) {
    const saved = await api<Saved>(
      `${path}/seats/${studentId}/grades/${order}`,
      "PUT",
      { points, comment },
    );
    setBoard(
      (b) =>
        b && {
          ...b,
          essays: b.essays.map((e) =>
            e.order !== order
              ? e
              : {
                  ...e,
                  responses: e.responses.map((r) =>
                    r.studentId === studentId
                      ? {
                          ...r,
                          points,
                          comment: comment || null,
                          gradedAt: new Date().toISOString(),
                        }
                      : r,
                  ),
                },
          ),
          students: b.students.map((s) =>
            s.studentId === studentId
              ? {
                  ...s,
                  score: saved.score,
                  maxScore: saved.maxScore,
                  pendingEssays: saved.pendingEssays,
                }
              : s,
          ),
          pending: b.students.reduce(
            (sum, s) =>
              sum +
              (s.studentId === studentId
                ? saved.pendingEssays
                : s.pendingEssays),
            0,
          ),
        },
    );
    onGraded();
  }

  const essay = useMemo(
    () => board?.essays.find((e) => e.order === orderOf) ?? null,
    [board, orderOf],
  );
  if (error)
    return (
      <p className="error" role="alert">
        {error} <button onClick={() => void load()}>Tải lại</button>
      </p>
    );
  if (!board) return <p role="status">Đang tải…</p>;
  if (board.essays.length === 0)
    return <p className="muted">Đề này không có câu tự luận.</p>;
  const responses = essay?.responses ?? [];
  const current = responses[Math.min(at, Math.max(0, responses.length - 1))];
  const nextTodo = (from: number) => {
    const order = [...responses.keys()];
    const after = order.filter((i) => i > from && responses[i].points === null);
    const before = order.filter(
      (i) => i < from && responses[i].points === null,
    );
    return after[0] ?? before[0] ?? Math.min(from + 1, responses.length - 1);
  };
  return (
    <div className="grading">
      <div className="form-actions" role="group" aria-label="Cách chấm">
        <button
          aria-pressed={mode === "question"}
          className={mode === "question" ? "primary" : undefined}
          onClick={() => setMode("question")}
        >
          Chấm theo câu
        </button>
        <button
          aria-pressed={mode === "student"}
          className={mode === "student" ? "primary" : undefined}
          onClick={() => setMode("student")}
        >
          Chấm theo học sinh
        </button>
        <span className="hint">
          {board.pending > 0
            ? `Còn ${board.pending} bài chưa chấm.`
            : "Đã chấm hết."}
        </span>
      </div>
      {mode === "question" ? (
        <>
          {board.essays.length > 1 && (
            <label>
              Câu tự luận
              <select
                value={orderOf ?? ""}
                onChange={(e) => {
                  setOrderOf(Number(e.target.value));
                  setAt(0);
                }}
              >
                {board.essays.map((e) => (
                  <option key={e.order} value={e.order}>
                    Câu {e.order} · {e.code} (
                    {e.responses.filter((r) => r.points === null).length} chờ)
                  </option>
                ))}
              </select>
            </label>
          )}
          {essay && (
            <div className="grading-question">
              <p className="question-meta">
                <strong>Câu {essay.order}</strong>
                <span>{essay.code}</span>
                <span>{pointsText(essay.points)} điểm</span>
              </p>
              <p className="question-stem">{essay.stem}</p>
              {essay.stemImageUrl && (
                <img
                  className="question-image"
                  src={essay.stemImageUrl}
                  alt="Ảnh đề"
                />
              )}
              {essay.sampleAnswer && (
                <p className="answer-line">
                  Đáp án mẫu: <strong>{essay.sampleAnswer}</strong>
                </p>
              )}
            </div>
          )}
          {responses.length === 0 ? (
            <p className="muted">Chưa có học sinh nào nộp câu này.</p>
          ) : (
            current &&
            essay && (
              <>
                <nav className="form-actions" aria-label="Chọn bài">
                  <button
                    onClick={() => setAt(Math.max(0, at - 1))}
                    disabled={at === 0}
                  >
                    ← Bài trước
                  </button>
                  <span role="status">
                    Bài {at + 1}/{responses.length}
                  </span>
                  <button
                    onClick={() =>
                      setAt(Math.min(responses.length - 1, at + 1))
                    }
                    disabled={at >= responses.length - 1}
                  >
                    Bài sau →
                  </button>
                </nav>
                <GradeForm
                  key={`${essay.order}-${current.studentId}`}
                  essay={essay}
                  response={current}
                  last={at >= responses.length - 1 && nextTodo(at) === at}
                  onSave={(p, c) => save(essay.order, current.studentId, p, c)}
                  onNext={() => setAt(nextTodo(at))}
                />
              </>
            )
          )}
        </>
      ) : (
        <>
          <label>
            Học sinh
            <select
              value={student ?? ""}
              onChange={(e) => setStudent(e.target.value)}
            >
              {board.students.map((s) => (
                <option key={s.studentId} value={s.studentId}>
                  {s.studentName}
                  {s.pendingEssays > 0 ? ` (${s.pendingEssays} chờ)` : ""}
                </option>
              ))}
            </select>
          </label>
          {board.essays.map((e) => {
            const r = e.responses.find((x) => x.studentId === student);
            return (
              <div key={e.order} className="grading-question">
                <p className="question-meta">
                  <strong>Câu {e.order}</strong>
                  <span>{e.code}</span>
                  <span>{pointsText(e.points)} điểm</span>
                </p>
                <p className="question-stem">{e.stem}</p>
                {e.sampleAnswer && (
                  <p className="answer-line">
                    Đáp án mẫu: <strong>{e.sampleAnswer}</strong>
                  </p>
                )}
                {r ? (
                  <GradeForm
                    key={`${e.order}-${r.studentId}`}
                    essay={e}
                    response={r}
                    last
                    onSave={(p, c) => save(e.order, r.studentId, p, c)}
                    onNext={() => undefined}
                  />
                ) : (
                  <p className="muted">Em chưa trả lời câu này.</p>
                )}
              </div>
            );
          })}
        </>
      )}
    </div>
  );
}

function GradeForm({
  essay,
  response,
  last,
  onSave,
  onNext,
}: {
  essay: Essay;
  response: Response;
  last: boolean;
  onSave: (points: number, comment: string) => Promise<void>;
  onNext: () => void;
}) {
  const [points, setPoints] = useState(
    response.points === null ? "" : String(response.points),
  );
  const [comment, setComment] = useState(response.comment ?? "");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState(
    response.points === null ? "" : "Đã chấm",
  );
  const [error, setError] = useState("");
  async function submit(next: boolean) {
    if (points === "") return setError("Chọn điểm cho bài này.");
    setBusy(true);
    setError("");
    try {
      await onSave(Number(points), comment.trim());
      setMessage("Đã lưu ✓");
      if (next) onNext();
    } catch (e) {
      setError(fail(e));
    } finally {
      setBusy(false);
    }
  }
  return (
    <form
      className="grade-form"
      aria-label={`Chấm bài của ${response.studentName}`}
      onSubmit={(e) => {
        e.preventDefault();
        void submit(!last);
      }}
    >
      <p className="grade-student">{response.studentName}</p>
      <blockquote className="grade-answer" aria-label="Bài làm của học sinh">
        {response.answer || "(để trống)"}
      </blockquote>
      <div className="form-row">
        <label>
          Điểm (0–{pointsText(essay.points)})
          <select
            value={points}
            onChange={(e) => {
              setPoints(e.target.value);
              setMessage("");
            }}
          >
            <option value="">Chọn…</option>
            {options(essay.points).map((p) => (
              <option key={p} value={p}>
                {pointsText(p)}
              </option>
            ))}
          </select>
        </label>
        <span className="grade-quick">
          <button type="button" onClick={() => setPoints("0")}>
            0
          </button>
          <button
            type="button"
            onClick={() => setPoints(String(essay.points / 2))}
          >
            ½
          </button>
          <button type="button" onClick={() => setPoints(String(essay.points))}>
            Tối đa
          </button>
        </span>
      </div>
      <label>
        Nhận xét{" "}
        <span className="hint">
          — không bắt buộc, học sinh thấy khi được xem kết quả
        </span>
        <textarea
          rows={2}
          maxLength={500}
          value={comment}
          onChange={(e) => {
            setComment(e.target.value);
            setMessage("");
          }}
        />
      </label>
      {error && (
        <p className="error" role="alert">
          {error}
        </p>
      )}
      <div className="form-actions">
        <button className="primary" disabled={busy}>
          {last ? "Lưu" : "Lưu và sang bài kế"}
        </button>
        {!last && (
          <button
            type="button"
            disabled={busy}
            onClick={() => void submit(false)}
          >
            Chỉ lưu
          </button>
        )}
        <span role="status" className="hint">
          {message}
        </span>
      </div>
    </form>
  );
}
