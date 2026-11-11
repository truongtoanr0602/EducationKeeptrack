import type { Access } from "./api";

export type PaymentRequestView = {
  id: string;
  transactionCode: string;
  note: string;
  submittedAt: string;
  status: "submitted" | "approved" | "rejected";
  decisionReason: string | null;
  decidedAt: string | null;
};
export type PeriodView = {
  id: string;
  startsAt: string;
  endsAt: string;
  label: string;
  current: boolean;
  upcoming: boolean;
};
export type ServiceProfile = {
  status: Access["status"];
  access: Access;
  instructions: { bankDetails: string; contact: string };
  requests: PaymentRequestView[];
  periods: PeriodView[];
};

export const requestStatusLabels: Record<PaymentRequestView["status"], string> =
  {
    submitted: "Chờ Admin đối chiếu",
    approved: "Đã duyệt",
    rejected: "Đã từ chối",
  };

export const newKey = () =>
  typeof crypto.randomUUID === "function"
    ? crypto.randomUUID()
    : `${Date.now()}-${Math.random().toString(16).slice(2)}`;
