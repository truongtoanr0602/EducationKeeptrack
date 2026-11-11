import type { Access } from "./api";

export type ServiceStatus = Access["status"];
export type TeacherSummary = {
  id: string;
  email: string;
  emailConfirmed: boolean;
  isBlocked: boolean;
  registeredAt: string;
  status: ServiceStatus;
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
