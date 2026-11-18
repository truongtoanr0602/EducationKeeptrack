export type MoneySource = {
  receiptId: string;
  receivedOn: string;
  note: string | null;
  collectedVnd: string;
  usedVnd: string;
  unusedVnd: string;
  reservedVnd: string;
  availableVnd: string;
};
export type ReserveEvent = {
  version: number;
  action: "requested" | "approved" | "rejected" | "cancelled";
  reason: string;
  recordedBy: string;
  amountVnd: string;
  at: string;
};
export type ReserveRequest = {
  id: string;
  absentFrom: string;
  absentTo: string;
  reason: string;
  evidenceNote: string | null;
  requestedVnd: string;
  status: "pending" | "approved" | "rejected" | "cancelled";
  version: number;
  decisionReason: string | null;
  createdAt: string;
  decidedAt: string | null;
  sources: { receiptId: string; receivedOn: string; amountVnd: string }[];
  history: ReserveEvent[];
  usedVnd: string;
  remainingVnd: string;
  canCancel: boolean;
};
export type ReserveBalance = {
  approvedVnd: string;
  usedVnd: string;
  remainingVnd: string;
};
export type ReserveUse = {
  id: string;
  month: string;
  usedOn: string;
  amountVnd: string;
  status: "active" | "reversed";
  reversalReason: string | null;
  createdAt: string;
  reversedAt: string | null;
};
export type ReservePeriod = {
  month: string;
  debtVnd: string;
  reserveAppliedVnd: string;
  reserveExcessVnd: string;
};
export type ReserveMovements = {
  month: string;
  openingVnd: string;
  approvedVnd: string;
  usedVnd: string;
  cancelledVnd: string;
  carriedVnd: string;
};
export type StudentReserve = {
  studentId: string;
  name: string;
  collectedVnd: string;
  usedVnd: string;
  unusedVnd: string;
  reservedVnd: string;
  availableVnd: string;
  sources: MoneySource[];
  requests: ReserveRequest[];
  reserve: ReserveBalance;
  uses: ReserveUse[];
  periods: ReservePeriod[];
  movements: ReserveMovements;
};
export type ReserveReport = {
  version: number;
  policy: { status: "none"; message: string };
  students: StudentReserve[];
};
export type ReserveDraft = {
  studentId: string;
  absentFrom: string;
  absentTo: string;
  reason: string;
  evidenceNote: string;
  amountVnd: string;
  idempotencyKey: string;
};
export const reserveStatus: Record<ReserveRequest["status"], string> = {
  pending: "Chờ quyết định",
  approved: "Đã duyệt bảo lưu",
  rejected: "Đã từ chối",
  cancelled: "Đã hủy bảo lưu",
};
