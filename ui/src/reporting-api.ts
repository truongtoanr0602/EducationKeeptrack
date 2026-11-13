export type ReportDraft = {
  displayName: string | null;
  parentDisplayName: string | null;
  contactDisplay: string | null;
  comment: string;
  roadmap: string;
  emoji: string;
  version: number;
};
export type MonthlyPreview = { id: string; draftVersion: number; sourceStamp: string; expiresAt: string; totalDueVnd: string; warnings: string[] };
export type FreshnessChange = { area: "draft" | "attendance" | "results" | "fees" | "payment-info"; label: string; before: string; after: string };
export type ExportFreshness = { state: "current" | "outdated" | "checking" | "unknown"; changes: FreshnessChange[] };
export type MonthlyExport = { id: string; state: string; createdAt: string; byteLength: number; sha256: string | null; draftVersion: number; sourceStamp: string; warnings: string[]; freshness?: ExportFreshness };
export type ExportCommand = { previewId: string; draftVersion: number; idempotencyKey: string };
export function reportPath(classId: string, studentId: string, month: string) {
  return `/reporting/classes/${classId}/students/${studentId}/months/${month}`;
}
