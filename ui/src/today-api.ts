import { api } from "./api";
import type { ClassSession } from "./session-api";

export type TodaySession = {
  classId: string;
  className: string;
  subject: string;
  session: ClassSession;
  students: number;
  present: number;
  absent: number;
};
export type TodayView = { date: string; sessions: TodaySession[] };

export const loadToday = (date?: string) =>
  api<TodayView>(`/today${date ? `?date=${date}` : ""}`);

// Calendar-date arithmetic without time zones: dates are Vietnam calendar days (yyyy-MM-dd).
export function shiftDate(date: string, days: number) {
  const d = new Date(`${date}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
}
