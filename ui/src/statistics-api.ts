import type { Receipt } from "./payments-api";

export type CashflowReport = {
  from: string;
  to: string;
  timeZone: string;
  version: number;
  status: "ready" | "pending" | "missing-rate" | "needs-review";
  totalReceivedVnd: string;
  totalUnallocatedVnd: string;
  receipts: { studentName: string; receipt: Receipt }[];
};
export type PeriodStudent = {
  studentId: string;
  name: string;
  feesVnd: string;
  allocatedVnd: string;
  paidVnd: string;
  heldVnd: string;
  debtVnd: string;
  status: "unpaid" | "partial" | "paid";
  allocations: {
    receiptId: string;
    receivedOn: string;
    amountVnd: string;
    note: string | null;
  }[];
};
export type PeriodReport = {
  month: string;
  version: number;
  status: CashflowReport["status"];
  totalFeesVnd: string;
  totalPaidVnd: string;
  totalHeldVnd: string;
  totalDebtVnd: string;
  students: PeriodStudent[];
};
