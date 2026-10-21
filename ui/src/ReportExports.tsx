import type { MonthlyExport } from "./reporting-api";

export function ReportExports({ items, busy, onDownload }: { items: MonthlyExport[]; busy: boolean; onDownload: (id: string) => void }) {
  return <div className="report-exports"><h4>Các bản xuất đã lưu</h4>
    {!items.length && <p>Chưa có bản xuất trong tháng này.</p>}
    {items.map(item => <article key={item.id}>
      <p>{new Date(item.createdAt).toLocaleString("vi-VN")} · Bản nháp {item.draftVersion} · {item.state === "ready" ? "Sẵn sàng" : item.state === "pending" ? "Đang xử lý" : "Chưa hoàn tất"}</p>
      {item.warnings.map(w => <p className="notice" key={w}>{w}</p>)}
      {item.state === "ready" && <button disabled={busy} onClick={() => onDownload(item.id)}>Tải PDF</button>}
    </article>)}
  </div>;
}
