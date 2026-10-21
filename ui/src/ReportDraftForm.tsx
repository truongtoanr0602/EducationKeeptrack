import type { ReportDraft } from "./reporting-api";

export function ReportDraftForm({ draft, disabled, dirty, onChange, onSave }: {
  draft: ReportDraft; disabled: boolean; dirty: boolean;
  onChange: (value: ReportDraft) => void; onSave: () => void;
}) {
  return <form className="report-draft" onSubmit={e => { e.preventDefault(); onSave(); }}>
    <fieldset disabled={disabled}>
      <legend>Nội dung báo cáo</legend>
      <p>Các tên và liên hệ bên dưới chỉ thay đổi cách hiển thị trên báo cáo. Học phí lấy từ dữ liệu đã đối chiếu.</p>
      <label>Tên hiển thị trên báo cáo<input maxLength={160} value={draft.displayName ?? ""} onChange={e => onChange({ ...draft, displayName: e.target.value })} placeholder="Dùng tên trong hồ sơ" /></label>
      <label>Tên phụ huynh hiển thị<input maxLength={160} value={draft.parentDisplayName ?? ""} onChange={e => onChange({ ...draft, parentDisplayName: e.target.value })} /></label>
      <label>Liên hệ hiển thị<textarea maxLength={500} value={draft.contactDisplay ?? ""} onChange={e => onChange({ ...draft, contactDisplay: e.target.value })} /></label>
      <label>Nhận xét báo cáo<textarea rows={5} maxLength={10000} value={draft.comment} onChange={e => onChange({ ...draft, comment: e.target.value })} /></label>
      <label>Lộ trình báo cáo<textarea rows={5} maxLength={10000} value={draft.roadmap} onChange={e => onChange({ ...draft, roadmap: e.target.value })} /></label>
      <label>Biểu tượng báo cáo<select value={draft.emoji} onChange={e => onChange({ ...draft, emoji: e.target.value })}>
        <option value="none">Không dùng</option><option value="🙂">🙂 Khích lệ</option><option value="⭐">⭐ Ngôi sao</option><option value="📘">📘 Sách</option>
      </select></label>
      <button disabled={!dirty} type="submit">Lưu bản nháp báo cáo</button>
    </fieldset>
  </form>;
}
