import type { MonthlyExport } from "./reporting-api";

const freshnessText = {
  current: "Còn mới: khớp dữ liệu hiện tại",
  outdated: "Đã lỗi thời: dữ liệu đã thay đổi sau khi xuất",
  checking: "Đang cập nhật số liệu, chưa thể kết luận bản này còn mới",
  unknown: "",
} as const;

export function ReportExports({ items, busy, canUpdate, onDownload, onRecheck, onUpdate }: {
  items: MonthlyExport[]; busy: boolean; canUpdate: boolean;
  onDownload: (id: string) => void; onRecheck: () => void; onUpdate: () => void;
}) {
  const outdated = items.filter(x => x.freshness?.state === "outdated");
  return <div className="report-exports"><h4>Các bản xuất đã lưu</h4>
    {!items.length && <p>Chưa có bản xuất trong tháng này.</p>}
    {outdated.length > 0 && <p className="notice" role="status" data-testid="outdated-banner">
      Có {outdated.length} bản PDF đã lỗi thời. File đã xuất không bị sửa; muốn có bản mới hãy xem trước và xuất lại. Hệ thống không tự gửi cho phụ huynh.
      {" "}<button disabled={busy || !canUpdate} onClick={onUpdate}>Cập nhật báo cáo</button>
    </p>}
    {items.length > 0 && <button disabled={busy} onClick={onRecheck}>Kiểm tra lại bản xuất</button>}
    {items.map(item => {
      const state = item.freshness?.state ?? "unknown";
      return <article key={item.id} data-freshness={state}>
        <p>{new Date(item.createdAt).toLocaleString("vi-VN")} · Bản nháp {item.draftVersion} · {item.state === "ready" ? "Sẵn sàng" : item.state === "pending" ? "Đang xử lý" : "Chưa hoàn tất"}</p>
        {item.state === "ready" && state !== "unknown" && <p className={state === "outdated" ? "notice" : undefined} data-testid="freshness">{freshnessText[state]}</p>}
        {item.freshness?.changes.length ? <details><summary>Xem thay đổi ({item.freshness.changes.length})</summary>
          <ul>{item.freshness.changes.map((c, i) => <li key={i}><strong>{c.label}</strong>: {c.before} → {c.after}</li>)}</ul>
        </details> : null}
        {item.warnings.map(w => <p className="notice" key={w}>{w}</p>)}
        {item.state === "ready" && <button disabled={busy} onClick={() => onDownload(item.id)}>Tải PDF</button>}
      </article>;
    })}
  </div>;
}
