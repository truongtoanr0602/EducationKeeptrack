import { api } from "./api";

export type DebtItem = {
  classId: string;
  className: string;
  subject: string;
  studentId: string;
  name: string;
  totalDebtVnd: string;
  currentDebtVnd: string;
  oldDebtVnd: string;
  version: number;
};
export type DebtClass = {
  classId: string;
  className: string;
  status: "ready" | "pending" | "missing-rate" | "needs-review";
  version: number;
};
export type DebtsView = {
  month: string;
  totalDebtVnd: string;
  count: number;
  page: number;
  pageSize: number;
  classes: DebtClass[];
  items: DebtItem[];
};

export const loadDebts = (
  month: string,
  classId: string,
  page: number,
  pageSize: number,
) =>
  api<DebtsView>(
    `/billing/debts?month=${month}&page=${page}&pageSize=${pageSize}${
      classId ? `&classId=${classId}` : ""
    }`,
  );
