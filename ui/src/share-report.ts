import { api } from "./api";
import {
  reportPath,
  type MonthlyExport,
  type MonthlyPreview,
} from "./reporting-api";

export type ShareResult = "shared" | "downloaded" | "cancelled";

// A saved export can be sent as-is only while it is ready and its source data has not changed.
export const isSendable = (item: MonthlyExport) =>
  item.state === "ready" && item.freshness?.state === "current";

async function currentExport(path: string, create: boolean) {
  const items = await api<MonthlyExport[]>(`${path}/exports`);
  const found = items.find(isSendable);
  if (found || !create) return found;
  // No up-to-date PDF: capture the source as it is now and store a new export. The saved draft is
  // used unchanged; nothing is sent to parents until the teacher confirms in the share sheet.
  const preview = await api<MonthlyPreview>(`${path}/previews`, "POST", {});
  return api<MonthlyExport>(`${path}/exports`, "POST", {
    previewId: preview.id,
    draftVersion: preview.draftVersion,
    idempotencyKey: crypto.randomUUID(),
  });
}

// Reuses the monthly report PDF of one student (ADR 0005): the "Hóa đơn" screen has no separate receipt.
export async function shareMonthlyReport(
  classId: string,
  studentId: string,
  month: string,
  studentName: string,
): Promise<ShareResult> {
  const path = reportPath(classId, studentId, month);
  const item = await currentExport(path, true);
  if (!item || item.state !== "ready")
    throw new Error("PDF chưa sẵn sàng. Vui lòng thử lại sau ít phút.");
  const link = await api<{ url: string }>(
    `${path}/exports/${item.id}/download-link`,
    "POST",
    {},
  );
  const response = await fetch(link.url, { credentials: "same-origin" });
  if (!response.ok) throw new Error("Không thể tải file PDF.");
  const file = new File(
    [await response.blob()],
    `bao-cao-${month}-${studentName.replace(/\s+/g, "-")}.pdf`,
    { type: "application/pdf" },
  );
  const data = {
    files: [file],
    title: `Báo cáo tháng ${month} · ${studentName}`,
  };
  if (navigator.canShare?.(data)) {
    try {
      await navigator.share(data);
      return "shared";
    } catch (e) {
      if (e instanceof DOMException && e.name === "AbortError")
        return "cancelled";
      throw e;
    }
  }
  // Desktop browsers without file sharing: save the file instead.
  const url = URL.createObjectURL(file);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = file.name;
  document.body.append(anchor);
  anchor.click();
  anchor.remove();
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
  return "downloaded";
}
