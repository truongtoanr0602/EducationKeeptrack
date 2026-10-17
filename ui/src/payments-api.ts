export type Receipt = {
  id: string;
  studentId: string;
  amountVnd: string;
  receivedOn: string;
  note: string | null;
  recordedAt: string;
  unallocatedVnd: string;
  allocations: { month: string; amountVnd: string }[];
};
export type StudentPayments = {
  studentId: string;
  name: string;
  oldDebtVnd: string;
  currentDebtVnd: string;
  totalDebtVnd: string;
  unallocatedVnd: string;
  allocationExcessVnd: string;
  periods: {
    month: string;
    feesVnd: string;
    paidVnd: string;
    debtVnd: string;
    status: "unpaid" | "partial" | "paid";
    allocationExcessVnd: string;
  }[];
  receipts: Receipt[];
};
export type PaymentsReport = {
  month: string;
  version: number;
  status: "ready" | "pending" | "missing-rate" | "needs-review";
  totalDebtVnd: string;
  students: StudentPayments[];
};
export type ReceiptDraft = {
  studentId: string;
  amountVnd: string;
  receivedOn: string;
  note: string;
  idempotencyKey: string;
  version: number;
};
export const paymentStatus = {
  unpaid: "Chưa trả",
  partial: "Trả một phần",
  paid: "Đã trả đủ",
};
