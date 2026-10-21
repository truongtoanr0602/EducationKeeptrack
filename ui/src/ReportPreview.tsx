import type { MonthlyPreview } from "./reporting-api";

export function ReportPreview({ preview, path }: { preview: MonthlyPreview; path: string }) {
  return <div className="report-preview">
    <p>Tổng cần thu: {BigInt(preview.totalDueVnd).toLocaleString("vi-VN")} đồng</p>
    <p>Bản nháp {preview.draftVersion} · Nguồn đối chiếu {preview.sourceStamp.slice(0, 12)} · Xem trước đến {new Date(preview.expiresAt).toLocaleString("vi-VN")}</p>
    <iframe title="Xem trước báo cáo tháng" sandbox="" referrerPolicy="no-referrer" src={`/api${path}/previews/${preview.id}/html`} />
  </div>;
}
