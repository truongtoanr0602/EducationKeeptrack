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
  const [previewPhoto, setPreviewPhoto] = useState<string | null>(null);

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

  async function uploadPhoto(file: File) {
    if (!file) return;
    setBusy(true);
    setError("");
    setMessage("");
    try {
      const form = new FormData();
      form.append("file", file);
      const res = await fetch(`/api${path}/photos`, {
        method: "POST",
        body: form,
        headers: {
          "X-Requested-With": "XMLHttpRequest",
        },
      });
      if (!res.ok) {
        const data = await res.json().catch(() => null);
        throw new Error(
          data?.message ||
            data?.errors?.file?.[0] ||
            "Không thể tải ảnh. Vui lòng kiểm tra định dạng và kích thước.",
        );
      }
      await reloadVersion();
      setMessage("Đã tải ảnh bài nộp thành công.");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Lỗi khi tải ảnh.");
    } finally {
      setBusy(false);
    }
  }

  async function deletePhoto(photoId: string) {
    setBusy(true);
    setError("");
    try {
      const res = await api<Submission>(`${path}/photos/${photoId}`, "DELETE");
      setSaved(res);
      setMessage("Đã xóa ảnh bài nộp.");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Không thể xóa ảnh.");
    } finally {
      setBusy(false);
    }
  }

  async function movePhoto(index: number, direction: "up" | "down") {
    const photos = saved.photos ? [...saved.photos] : [];
    const target = direction === "up" ? index - 1 : index + 1;
    if (target < 0 || target >= photos.length) return;
    const temp = photos[index];
    photos[index] = photos[target];
    photos[target] = temp;
    setBusy(true);
    setError("");
    try {
      const res = await api<Submission>(`${path}/photos/reorder`, "PUT", {
        photoIds: photos.map((p) => p.id),
      });
      setSaved(res);
      setMessage("Đã cập nhật thứ tự các trang ảnh.");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Không thể đổi thứ tự ảnh.");
    } finally {
      setBusy(false);
    }
  }

  async function changeMethod(newMethod: string) {
    setBusy(true);
    setError("");
    try {
      const res = await api<Submission>(`${path}/method`, "PUT", {
        method: newMethod,
        version: saved.version,
      });
      setSaved(res);
      setMessage(
        `Đã chuyển cách chấm sang ${newMethod === "ai" ? "AI chấm bài" : "Tự chấm"}.`,
      );
    } catch (e) {
      setError(e instanceof Error ? e.message : "Không thể đổi cách chấm.");
    } finally {
      setBusy(false);
    }
  }

  async function runAiGrading() {
    setBusy(true);
    setError("");
    setMessage("AI đang phân tích ảnh bài nộp và đối chiếu hướng dẫn chấm...");
    try {
      const res = await api<Submission>(`${path}/ai-grade`, "POST");
      setSaved(res);
      if (res.aiCompletion !== null && res.aiCompletion !== undefined) {
        setCompletion(res.aiCompletion);
      }
      if (res.aiCorrectness !== null && res.aiCorrectness !== undefined) {
        setCorrectness(res.aiCorrectness);
      }
      setStatus("assessed");
      setMessage("AI đã chấm bài xong. Kiểm tra đề xuất và xác nhận kết quả.");
    } catch (e) {
      setError(
        e instanceof ApiError
          ? Object.values(e.errors ?? {}).flat().join(" ") || e.message
          : e instanceof Error
            ? e.message
            : "AI không thể xử lý bài này lúc này.",
      );
    } finally {
      setBusy(false);
    }
  }

  async function runAiFallback() {
    setBusy(true);
    setError("");
    try {
      const res = await api<Submission>(`${path}/ai-fallback`, "POST");
      setSaved(res);
      setMessage("Đã chuyển bài này về chế độ tự chấm an toàn.");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Không thể chuyển về tự chấm.");
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
          onChange={(e) => void changeMethod(e.target.value)}
        >
          <option value="manual">Tự chấm</option>
          <option value="ai">AI chấm bài</option>
        </select>
      </label>

      {/* Submission Photos Section */}
      <section className="submission-photos-section" style={{ margin: "12px 0", padding: "10px", background: "var(--surface-color, #f8f9fa)", borderRadius: "6px" }}>
        <h4 style={{ margin: "0 0 8px 0" }}>Ảnh bài nộp ({saved.photos?.length ?? 0} trang)</h4>
        <div style={{ display: "flex", gap: "8px", alignItems: "center", marginBottom: "8px" }}>
          <input
            type="file"
            accept="image/jpeg,image/png,image/webp"
            disabled={disabled || (saved.photos?.length ?? 0) >= 10}
            onChange={(e) => {
              if (e.target.files?.[0]) {
                void uploadPhoto(e.target.files[0]);
                e.target.value = "";
              }
            }}
          />
          <small style={{ color: "#666" }}>Tối đa 10 trang, JPG/PNG/WebP ≤ 5MB/ảnh</small>
        </div>

        {saved.photos && saved.photos.length > 0 && (
          <ul style={{ listStyle: "none", padding: 0, margin: "8px 0", display: "flex", flexWrap: "wrap", gap: "10px" }}>
            {saved.photos.map((photo, index) => (
              <li
                key={photo.id}
                style={{
                  border: "1px solid #ddd",
                  borderRadius: "4px",
                  padding: "6px",
                  background: "#fff",
                  display: "flex",
                  flexDirection: "column",
                  alignItems: "center",
                  width: "120px",
                }}
              >
                <span style={{ fontWeight: 600, fontSize: "12px", marginBottom: "4px" }}>
                  Trang {photo.sequenceIndex}
                </span>
                <button
                  type="button"
                  style={{ fontSize: "11px", padding: "2px 6px", marginBottom: "4px" }}
                  onClick={async () => {
                    try {
                      const res = await api<{ downloadUrl: string }[]>(`${path}/photos`);
                      const found = res.find((p) => p.id === photo.id);
                      if (found?.downloadUrl) setPreviewPhoto(found.downloadUrl);
                    } catch {
                      setError("Không thể tải link xem ảnh.");
                    }
                  }}
                >
                  Xem ảnh
                </button>
                <div style={{ display: "flex", gap: "2px" }}>
                  <button
                    type="button"
                    title="Chuyển lên trước"
                    disabled={disabled || index === 0}
                    style={{ fontSize: "10px", padding: "1px 4px" }}
                    onClick={() => void movePhoto(index, "up")}
                  >
                    ▲
                  </button>
                  <button
                    type="button"
                    title="Chuyển xuống sau"
                    disabled={disabled || index === (saved.photos?.length ?? 0) - 1}
                    style={{ fontSize: "10px", padding: "1px 4px" }}
                    onClick={() => void movePhoto(index, "down")}
                  >
                    ▼
                  </button>
                  <button
                    type="button"
                    title="Xóa trang này"
                    disabled={disabled}
                    style={{ fontSize: "10px", padding: "1px 4px", color: "red" }}
                    onClick={() => void deletePhoto(photo.id)}
                  >
                    ✕
                  </button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>

      {/* Preview Photo Modal */}
      {previewPhoto && (
        <div
          role="dialog"
          aria-label="Xem ảnh bài nộp"
          style={{
            position: "fixed",
            top: 0,
            left: 0,
            width: "100%",
            height: "100%",
            backgroundColor: "rgba(0,0,0,0.7)",
            display: "flex",
            justifyContent: "center",
            alignItems: "center",
            zIndex: 9999,
          }}
          onClick={() => setPreviewPhoto(null)}
        >
          <div
            style={{
              backgroundColor: "#fff",
              padding: "16px",
              borderRadius: "8px",
              maxWidth: "90%",
              maxHeight: "90%",
              overflow: "auto",
              textAlign: "center",
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <img
              src={previewPhoto}
              alt="Ảnh bài nộp"
              style={{ maxWidth: "100%", maxHeight: "75vh", objectFit: "contain" }}
            />
            <div style={{ marginTop: "12px" }}>
              <button type="button" onClick={() => setPreviewPhoto(null)}>
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}

      {/* AI Grading Proposal Panel */}
      {saved.method === "ai" && (
        <div
          style={{
            margin: "12px 0",
            padding: "12px",
            border: "1px solid #1a73e8",
            borderRadius: "6px",
            background: "#f0f6ff",
          }}
        >
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <h4 style={{ margin: 0, color: "#1a73e8" }}>🤖 Trợ lý AI chấm bài</h4>
            <div style={{ display: "flex", gap: "8px" }}>
              <button
                type="button"
                className="primary"
                disabled={disabled || !(saved.photos && saved.photos.length > 0)}
                onClick={() => void runAiGrading()}
              >
                {saved.aiStatus === "proposed" ? "Chấm lại bằng AI" : "Bắt đầu AI chấm bài"}
              </button>
              <button
                type="button"
                disabled={disabled}
                onClick={() => void runAiFallback()}
              >
                Chuyển về tự chấm
              </button>
            </div>
          </div>

          {saved.aiStatus === "proposed" && (
            <div style={{ marginTop: "10px" }}>
              <p style={{ margin: "4px 0", fontWeight: 600 }}>
                💡 Đề xuất AI: Hoàn thành {saved.aiCompletion}% · Làm đúng {saved.aiCorrectness}%
                {saved.aiConfidence && (
                  <span style={{ fontSize: "12px", color: "#555", marginLeft: "6px" }}>
                    (Độ tin cậy: {Math.round(saved.aiConfidence * 100)}%)
                  </span>
                )}
              </p>
              {saved.aiReasoning && (
                <p style={{ margin: "4px 0", fontSize: "13px", color: "#333", fontStyle: "italic" }}>
                  {saved.aiReasoning}
                </p>
              )}
              {saved.aiWarning && (
                <p style={{ margin: "4px 0", fontSize: "12px", color: "#b06000" }}>
                  ⚠️ {saved.aiWarning}
                </p>
              )}
            </div>
          )}
        </div>
      )}

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
