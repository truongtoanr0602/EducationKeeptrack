import type { Access } from "./api";
import type { QuotaBalance } from "./QuotaTable";

export type ServiceStatus = Access["status"];
export type TeacherSummary = {
  id: string;
  email: string;
  emailConfirmed: boolean;
  isBlocked: boolean;
  registeredAt: string;
  status: ServiceStatus;
  openRequest?: boolean;
  accessEndsAt?: string | null;
};

export const statusLabels: Record<ServiceStatus, string> = {
  blocked: "Đã khóa",
  unverified: "Chưa xác minh email",
  pending: "Chờ duyệt",
  rejected: "Đã từ chối",
  active: "Còn hạn",
  expired: "Hết hạn",
};

export const vnDateTime = (value: string) =>
  new Date(value).toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" });

export type AdminRequest = {
  id: string;
  transactionCode: string;
  note: string;
  submittedAt: string;
  status: "submitted" | "approved" | "rejected";
  verificationNote: string | null;
  decisionReason: string | null;
  decidedBy: string | null;
  decidedAt: string | null;
  version: number;
};
export type AdminPeriod = {
  id: string;
  startsAt: string;
  endsAt: string;
  label: string;
  reason: string | null;
  current: boolean;
  upcoming: boolean;
  version: number;
  gradingQuota: number | null;
  commentQuota: number | null;
  planningQuota: number | null;
};
export type HistoryView = {
  action: string;
  actor: string;
  reason: string | null;
  details: string | null;
  at: string;
};
export type TeacherDetailView = TeacherSummary & {
  aiEnabled: boolean;
  accountVersion: number;
  quotas: QuotaBalance[];
  requests: AdminRequest[];
  periods: AdminPeriod[];
  history: HistoryView[];
};
export type Instructions = {
  bankDetails: string;
  contact: string;
  version: number;
  updatedAt: string | null;
  updatedBy: string | null;
};
