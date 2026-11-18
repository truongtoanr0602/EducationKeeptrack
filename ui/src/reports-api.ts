import { api } from "./api";
import type { DebtClass } from "./billing-api";

export type AttendanceMonth = {
  month: string;
  held: number;
  planned: number;
  cancelled: number;
  present: number;
  absent: number;
  attendanceRate: number | null;
};
export type MoneyMonth = {
  month: string;
  feesVnd: string;
  receivedVnd: string;
  debtVnd: string;
  classes: DebtClass[];
};

export const loadMonth = (month: string) =>
  Promise.all([
    api<AttendanceMonth>(`/reports/attendance?month=${month}`),
    api<MoneyMonth>(`/billing/summary?month=${month}`),
  ]);
