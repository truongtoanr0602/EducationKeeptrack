import { useState } from "react";
import { api, ApiError } from "./api";
import type { ReportDraft } from "./reporting-api";

export function ReportDraftForm({
  draft,
  path,
  disabled,
  dirty,
  onChange,
  onSave,
}: {
  draft: ReportDraft;
  path?: string;
  disabled: boolean;
  dirty: boolean;
  onChange: (value: ReportDraft) => void;
  onSave: () => void;
}) {
  const [aiBusy, setAiBusy] = useState(false);
  const [aiMessage, setAiMessage] = useState("");
  const [aiError, setAiError] = useState("");

  async function generateAiComment() {
    if (!path) return;
    setAiBusy(true);
    setAiError("");
    setAiMessage("AI đang tổng hợp điểm danh và kết quả học tập trong tháng...");
    try {
      const res = await api<{ comment: string; roadmap: string }>(
        `${path}/ai-comment`,
        "POST",
        {},
      );
      onChange({
        ...draft,
        comment: res.comment,
        roadmap: res.roadmap,
      });
      setAiMessage("AI đã soạn xong nhận xét và lộ trình. Bạn có thể chỉnh sửa trước khi lưu.");
    } catch (e) {
      setAiError(
        e instanceof ApiError
          ? Object.values(e.errors ?? {}).flat().join(" ") || e.message
          : e instanceof Error
            ? e.message
            : "Không thể tạo nhận xét bằng AI lúc này.",
      );
    } finally {
      setAiBusy(false);
    }
  }

  return (
    <form
      className="report-draft"
      onSubmit={(e) => {
        e.preventDefault();
        onSave();
      }}
    >
      <fieldset disabled={disabled || aiBusy}>
        <legend>Nội dung báo cáo</legend>
        <p>
          Các tên và liên hệ bên dưới chỉ thay đổi cách hiển thị trên báo cáo.
          Học phí lấy từ dữ liệu đã đối chiếu.
        </p>

        {path && (
          <div
            style={{
              marginBottom: "16px",
              padding: "10px",
              background: "#f0f6ff",
              border: "1px solid #1a73e8",
              borderRadius: "6px",
            }}
          >
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
              }}
            >
              <span style={{ fontWeight: 600, color: "#1a73e8" }}>
                🤖 Soạn nhận xét tự động bằng AI
              </span>
              <button
                type="button"
                className="primary"
                disabled={disabled || aiBusy}
                onClick={() => void generateAiComment()}
              >
                {aiBusy ? "Đang xử lý..." : "AI soạn nhận xét & lộ trình"}
              </button>
            </div>
            {aiMessage && (
              <p
                style={{
                  margin: "6px 0 0 0",
                  fontSize: "13px",
                  color: "green",
                }}
              >
                {aiMessage}
              </p>
            )}
            {aiError && (
              <p style={{ margin: "6px 0 0 0", fontSize: "13px", color: "red" }}>
                {aiError}
              </p>
            )}
          </div>
        )}

        <label>
          Tên hiển thị trên báo cáo
          <input
            maxLength={160}
            value={draft.displayName ?? ""}
            onChange={(e) =>
              onChange({ ...draft, displayName: e.target.value })
            }
            placeholder="Dùng tên trong hồ sơ"
          />
        </label>
        <label>
          Tên phụ huynh hiển thị
          <input
            maxLength={160}
            value={draft.parentDisplayName ?? ""}
            onChange={(e) =>
              onChange({ ...draft, parentDisplayName: e.target.value })
            }
          />
        </label>
        <label>
          Liên hệ hiển thị
          <textarea
            maxLength={500}
            value={draft.contactDisplay ?? ""}
            onChange={(e) =>
              onChange({ ...draft, contactDisplay: e.target.value })
            }
          />
        </label>
        <label>
          Nhận xét báo cáo
          <textarea
            rows={5}
            maxLength={10000}
            value={draft.comment}
            onChange={(e) => onChange({ ...draft, comment: e.target.value })}
          />
        </label>
        <label>
          Lộ trình báo cáo
          <textarea
            rows={5}
            maxLength={10000}
            value={draft.roadmap}
            onChange={(e) => onChange({ ...draft, roadmap: e.target.value })}
          />
        </label>
        <label>
          Biểu tượng báo cáo
          <select
            value={draft.emoji}
            onChange={(e) => onChange({ ...draft, emoji: e.target.value })}
          >
            <option value="none">Không dùng</option>
            <option value="🙂">🙂 Khích lệ</option>
            <option value="⭐">⭐ Ngôi sao</option>
            <option value="📘">📘 Sách</option>
          </select>
        </label>
        <button disabled={!dirty} type="submit">
          Lưu bản nháp báo cáo
        </button>
      </fieldset>
    </form>
  );
}
