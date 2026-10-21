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
export type MonthlyExport = { id: string; state: string; createdAt: string; byteLength: number; sha256: string | null; draftVersion: number; sourceStamp: string; warnings: string[] };
export type ExportCommand = { previewId: string; draftVersion: number; idempotencyKey: string };
export function reportPath(classId: string, studentId: string, month: string) {
  return `/reporting/classes/${classId}/students/${studentId}/months/${month}`;
}
