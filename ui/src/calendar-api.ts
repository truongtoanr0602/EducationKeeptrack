import { api } from "./api";
import { shiftDate, type TodaySession } from "./today-api";

/** A weekly-schedule slot that has no session yet ("Chưa tạo buổi"). It is not a Buổi học (GLOSSARY). */
export type UnplannedSlot = {
  classId: string;
  className: string;
  subject: string;
  date: string;
  startTime: string;
  endTime: string;
};
export type RangeView = {
  from: string;
  to: string;
  sessions: TodaySession[];
  unplanned: UnplannedSlot[];
};

export const MAX_RANGE_DAYS = 42;
/** Sessions still planned after this many days are no longer called overdue (keeps a restored or old class quiet). */
export const OVERDUE_DAYS = 30;

export const loadRange = (from: string, to: string) =>
  api<RangeView>(`/today/range?from=${from}&to=${to}`);

const weekday = (date: string) => new Date(`${date}T00:00:00Z`).getUTCDay();
/** Monday of the week that holds `date` (weeks run Monday to Sunday). */
export const weekStart = (date: string) =>
  shiftDate(date, -((weekday(date) + 6) % 7));
export const weekDays = (date: string) =>
  Array.from({ length: 7 }, (_, i) => shiftDate(weekStart(date), i));
/** Six Monday-first weeks that cover the month of `date`: always 42 days, the most the range endpoint accepts. */
export const monthDays = (date: string) =>
  Array.from({ length: MAX_RANGE_DAYS }, (_, i) =>
    shiftDate(weekStart(`${date.slice(0, 7)}-01`), i),
  );
export function shiftMonth(date: string, months: number) {
  const d = new Date(`${date}T00:00:00Z`);
  const day = d.getUTCDate();
  d.setUTCDate(1);
  d.setUTCMonth(d.getUTCMonth() + months);
  const last = new Date(
    Date.UTC(d.getUTCFullYear(), d.getUTCMonth() + 1, 0),
  ).getUTCDate();
  d.setUTCDate(Math.min(day, last));
  return d.toISOString().slice(0, 10);
}

export type DayEntry =
  | { kind: "session"; key: string; item: TodaySession }
  | { kind: "unplanned"; key: string; slot: UnplannedSlot };
export const startOf = (e: DayEntry) =>
  e.kind === "session" ? e.item.session.startTime : e.slot.startTime;
export const classOf = (e: DayEntry) =>
  e.kind === "session" ? e.item.classId : e.slot.classId;
export const nameOf = (e: DayEntry) =>
  e.kind === "session" ? e.item.className : e.slot.className;

/** Sessions and unplanned slots by date, each day sorted by start time then class name. */
export function groupByDay(view: RangeView | undefined) {
  const days = new Map<string, DayEntry[]>();
  const push = (date: string, entry: DayEntry) =>
    days.set(date, [...(days.get(date) ?? []), entry]);
  for (const item of view?.sessions ?? [])
    push(item.session.date, { kind: "session", key: item.session.id, item });
  for (const slot of view?.unplanned ?? [])
    push(slot.date, {
      kind: "unplanned",
      key: `${slot.classId}:${slot.date}:${slot.startTime}`,
      slot,
    });
  for (const [date, list] of days)
    days.set(
      date,
      [...list].sort(
        (a, b) =>
          startOf(a).localeCompare(startOf(b)) ||
          nameOf(a).localeCompare(nameOf(b)),
      ),
    );
  return days;
}

/** Held but not every student marked (Chưa điểm danh). */
export const isMissing = (e: DayEntry) =>
  e.kind === "session" &&
  e.item.session.status === "held" &&
  e.item.present + e.item.absent < e.item.students;
/** Planned, already past, and within the last OVERDUE_DAYS days (Quá hạn). */
export const isOverdue = (e: DayEntry, today: string) =>
  e.kind === "session" &&
  e.item.session.status === "planned" &&
  e.item.session.date < today &&
  e.item.session.date >= shiftDate(today, -OVERDUE_DAYS);
export const needsAttention = (e: DayEntry, today: string) =>
  isMissing(e) || isOverdue(e, today);

export function attention(
  days: Map<string, DayEntry[]>,
  dates: string[],
  today: string,
) {
  let missing = 0,
    overdue = 0,
    oldest: string | undefined;
  for (const date of dates)
    for (const e of days.get(date) ?? []) {
      if (isMissing(e)) missing++;
      else if (isOverdue(e, today)) overdue++;
      else continue;
      if (!oldest || date < oldest) oldest = date;
    }
  return { missing, overdue, oldest };
}

const palette = [
  "#2f5bd8",
  "#c2410c",
  "#15803d",
  "#a21caf",
  "#0e7490",
  "#b45309",
  "#be123c",
  "#4d7c0f",
];
/** A fixed colour per class from its id, so nothing needs to be stored. Colour only tells classes apart. */
export function classColor(classId: string) {
  let h = 0;
  for (const c of classId) h = (h * 31 + c.charCodeAt(0)) >>> 0;
  return palette[h % palette.length];
}

const names = [
  "Chủ nhật",
  "Thứ Hai",
  "Thứ Ba",
  "Thứ Tư",
  "Thứ Năm",
  "Thứ Sáu",
  "Thứ Bảy",
];
export const dayName = (date: string) => names[weekday(date)];
export const ddmm = (date: string) => `${date.slice(8)}/${date.slice(5, 7)}`;

/** Accessible name of a day cell, for example "Thứ Ba 24/11, 2 buổi, 1 chưa điểm danh, 1 chưa tạo buổi". */
export function dayLabel(date: string, list: DayEntry[], today: string) {
  const sessions = list.filter((e) => e.kind === "session").length;
  const attn = list.filter((e) => needsAttention(e, today)).length;
  const unplanned = list.length - sessions;
  return [
    `${dayName(date)} ${ddmm(date)}`,
    `${sessions} buổi`,
    attn ? `${attn} chưa điểm danh` : "",
    unplanned ? `${unplanned} chưa tạo buổi` : "",
  ]
    .filter(Boolean)
    .join(", ");
}
