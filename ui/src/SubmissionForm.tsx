import { useState } from "react";
import { api, ApiError } from "./api";
import {
  assessmentStates,
  resultText,
  type AssessmentState,
  type Submission,
} from "./learning-api";

function Levels({
  label,
  value,
  disabled,
  onChange,
}: {
  label: string;
  value: number | null;
  disabled: boolean;
  onChange: (value: number) => void;
}) {
  return (
    <fieldset disabled={disabled} className="assessment-levels">
      <legend>{label}</legend>
      {[25, 50, 75, 100].map((level) => (
        <button
          type="button"
          key={level}
          aria-pressed={value === level}
          onClick={() => onChange(level)}
        >
          {level}%
        </button>
      ))}
    </fieldset>
  );
}

export function SubmissionForm({
  initial,
  path,
  canWrite,
  onDenied,
}: {
  initial: Submission;
  path: string;
  canWrite: boolean;
  onDenied: (e: ApiError) => void;
}) {
  const [saved, setSaved] = useState(initial);
  const [status, setStatus] = useState(initial.status);
  const [completion, setCompletion] = useState(initial.completion);
  const [correctness, setCorrectness] = useState(initial.correctness);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [conflict, setConflict] = useState(false);
  const disabled = !canWrite || busy || conflict;
  const complete =
    status === "assessed"
      ? completion !== null && correctness !== null
      : status === "no_correct"
        ? completion !== null
        : true;
  function changeState(next: AssessmentState) {
    setStatus(next);
    setMessage("");
    // Special-state zeros never become a normal level selection.
    setCompletion(null);
    setCorrectness(null);
  }
  async function save(confirm: boolean) {
    setBusy(true);
    setError("");
    setMessage("");
    try {
      const result = await api<Submission>(path, "PUT", {
        status,
        completion:
          status === "assessed" || status === "no_correct" ? completion : null,
        correctness: status === "assessed" ? correctness : null,
        version: saved.version,
        confirm,
      });
      setSaved(result);
      setMessage(confirm ? "Đã xác nhận kết quả." : "Đã lưu bản nháp.");
    } catch (e) {
      setError(
        e instanceof ApiError
          ? Object.values(e.errors ?? {})
              .flat()
              .join(" ") || e.message
          : "Không thể lưu. Vui lòng thử lại.",
      );
      if (e instanceof ApiError) {
        if (e.status === 409) setConflict(true);
        onDenied(e);
      }
    } finally {
      setBusy(false);
    }
  }
  async function reloadVersion() {
    setBusy(true);
    setError("");
    try {
      const assessment = await api<{ submissions: Submission[] }>(
        path.slice(0, path.indexOf("/submissions/")),
      );
      const current = assessment.submissions.find(
        (x) => x.studentId === saved.studentId,
      );
      if (!current) throw new Error("missing submission");
      setSaved(current);
      setConflict(false);
      setMessage(
        "Đã tải kết quả mới nhất. Lựa chọn đang nhập được giữ; kiểm tra trước khi lưu lại.",
      );
    } catch (e) {
      setError("Không thể tải kết quả mới nhất. Vui lòng thử lại.");
      if (e instanceof ApiError) onDenied(e);
    } finally {
      setBusy(false);
    }
  }
  return (
    <article
      className="assessment-submission"
      aria-label={`Bài của ${saved.studentName}`}
    >
      <h3>{saved.studentName}</h3>
      <label>
        Cách chấm bài này
        <select
          value={saved.method}
          disabled={
            disabled || saved.history.some((x) => x.action !== "method_changed")
          }
          onChange={() => {}}
        >
          <option value="manual">Tự chấm</option>
          <option value="ai" disabled>
            AI chưa khả dụng
          </option>
        </select>
      </label>
      <label>
        Trạng thái đánh giá
        <select
          value={status}
          disabled={disabled}
          onChange={(e) => changeState(e.target.value as AssessmentState)}
        >
          {Object.entries(assessmentStates).map(([value, label]) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
        </select>
      </label>
      <div className="assessment-controls">
        <Levels
          label="Lượng bài hoàn thành"
          value={completion}
          disabled={
            disabled || !(status === "assessed" || status === "no_correct")
          }
          onChange={setCompletion}
        />
        <Levels
          label="Kết quả làm đúng"
          value={correctness}
          disabled={disabled || status !== "assessed"}
          onChange={setCorrectness}
        />
      </div>
      {status === "not_done" && <p>Không làm bài: cả hai chỉ số bằng 0.</p>}
      {status === "no_correct" && (
        <p>
          Không có câu đúng: kết quả làm đúng bằng 0; chọn lượng bài hoàn thành.
        </p>
      )}
      {(status === "unassigned" || status === "unassessed") && (
        <p>Hai chỉ số để trống, không tính vào trung bình/cảnh báo.</p>
      )}
      <p role="status" aria-label="Kết quả cuối">
        {saved.finalResult
          ? resultText(saved.finalResult)
          : "Chưa xác nhận kết quả cuối."}
      </p>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      {message && (
        <p role="status" className="notice">
          {message}
        </p>
      )}
      <div className="assessment-actions">
        <button
          disabled={disabled || !complete}
          onClick={() => void save(false)}
        >
          Lưu bản nháp
        </button>
        <button
          className="primary"
          disabled={disabled || !complete}
          onClick={() => void save(true)}
        >
          Xác nhận kết quả
        </button>
        {conflict && (
          <button disabled={busy} onClick={() => void reloadVersion()}>
            Tải kết quả mới nhất
          </button>
        )}
      </div>
      <details>
        <summary>Lịch sử chỉnh sửa</summary>
        {saved.history.length ? (
          <ol aria-label="Lịch sử kết quả">
            {saved.history.map((revision) => (
              <li key={revision.id}>
                Phiên bản {revision.version} ·{" "}
                {revision.action === "confirmed"
                  ? "Đã xác nhận"
                  : revision.action === "edited"
                    ? "Bản nháp"
                    : "Đổi cách chấm"}{" "}
                · {resultText(revision)} ·{" "}
                {new Date(revision.recordedAt).toLocaleString("vi-VN", {
                  timeZone: "Asia/Ho_Chi_Minh",
                })}
              </li>
            ))}
          </ol>
        ) : (
          <p>Chưa có lịch sử chỉnh sửa.</p>
        )}
      </details>
    </article>
  );
}
