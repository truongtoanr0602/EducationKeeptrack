import { weekDays, type WeeklySlot } from "./session-api";

// Pure logic of the weekly calendar. Slots keep the server's `dayOfWeek` (0 = Chủ nhật, 1 = Thứ Hai ...) and the
// times as "HH:mm"; only the display order is Monday-first. Dates are "YYYY-MM-DD" strings handled as calendar dates
// (never through the local time zone), so UTC+7 midnight cannot shift a day.

export type Slot = WeeklySlot;
export const weekOrder = [1, 2, 3, 4, 5, 6, 0] as const;
export const dayShort = ["CN", "T2", "T3", "T4", "T5", "T6", "T7"] as const;
export const durations = [60, 90, 120] as const;
export const MAX_SLOTS = 100;
export const STEP_MINUTES = 15;

const MINUTES_PER_DAY = 24 * 60;

export const hhmm = (time: string) => time.slice(0, 5);

export function toMinutes(time: string): number | null {
  const match = /^(\d{2}):(\d{2})/.exec(time);
  if (!match) return null;
  const hours = Number(match[1]);
  const minutes = Number(match[2]);
  return hours < 24 && minutes < 60 ? hours * 60 + minutes : null;
}

export const toTime = (minutes: number) =>
  `${String(Math.floor(minutes / 60)).padStart(2, "0")}:${String(minutes % 60).padStart(2, "0")}`;

/** End time for a start and a duration; null when it would reach midnight or the start is not a time. */
export function endFor(start: string, minutes: number): string | null {
  const from = toMinutes(start);
  if (from === null || from + minutes >= MINUTES_PER_DAY) return null;
  return toTime(from + minutes);
}

export const slotLabel = (slot: Slot) =>
  `${weekDays[slot.dayOfWeek]} · ${hhmm(slot.startTime)}–${hhmm(slot.endTime)}`;

const order = (day: number) =>
  weekOrder.indexOf(day as (typeof weekOrder)[number]);

export const sortSlots = (slots: Slot[]) =>
  [...slots].sort(
    (a, b) =>
      order(a.dayOfWeek) - order(b.dayOfWeek) ||
      hhmm(a.startTime).localeCompare(hhmm(b.startTime)),
  );

/** The server's rule: same day and start < other end && other start < end (touching slots are fine). */
export function overlaps(a: Slot, b: Slot) {
  const [as, ae, bs, be] = [a.startTime, a.endTime, b.startTime, b.endTime].map(
    (t) => toMinutes(t) ?? 0,
  );
  return a.dayOfWeek === b.dayOfWeek && as < be && bs < ae;
}

type Failure = { error: string };
type Slots = { slots: Slot[] };

function validTimes(start: string, end: string): Failure | null {
  const from = toMinutes(start);
  const to = toMinutes(end);
  if (from === null || to === null)
    return { error: "Nhập giờ bắt đầu và giờ kết thúc hợp lệ." };
  if (to <= from)
    return { error: "Giờ kết thúc phải sau giờ bắt đầu trong cùng ngày." };
  return null;
}

/** Quick entry: every chosen day gets the same start and end. */
export function addSlots(
  current: Slot[],
  days: number[],
  start: string,
  end: string,
): Slots | Failure {
  if (days.length === 0) return { error: "Chọn ít nhất một ngày trong tuần." };
  const invalid = validTimes(start, end);
  if (invalid) return invalid;
  if (current.length + days.length > MAX_SLOTS)
    return { error: `Tối đa ${MAX_SLOTS} khung giờ.` };
  const added = days.map((dayOfWeek) => ({
    dayOfWeek,
    startTime: start,
    endTime: end,
  }));
  for (const slot of added) {
    const clash = current.find((other) => overlaps(slot, other));
    if (clash) return { error: `Trùng giờ với ${slotLabel(clash)}.` };
  }
  return { slots: sortSlots([...current, ...added]) };
}

/** Editing one slot: only the other slots can clash with it. */
export function replaceSlot(
  current: Slot[],
  index: number,
  slot: Slot,
): Slots | Failure {
  const invalid = validTimes(slot.startTime, slot.endTime);
  if (invalid) return invalid;
  const clash = current.find(
    (other, i) => i !== index && overlaps(slot, other),
  );
  if (clash) return { error: `Trùng giờ với ${slotLabel(clash)}.` };
  return { slots: sortSlots(current.map((s, i) => (i === index ? slot : s))) };
}

/** Visible hours of the grid: 06:00-22:00, widened so stored slots outside that range are never hidden. */
export function gridHours(slots: Slot[]) {
  let fromHour = 6;
  let toHour = 22;
  for (const slot of slots) {
    fromHour = Math.min(
      fromHour,
      Math.floor((toMinutes(slot.startTime) ?? 0) / 60),
    );
    toHour = Math.max(toHour, Math.ceil((toMinutes(slot.endTime) ?? 0) / 60));
  }
  return { fromHour: Math.max(0, fromHour), toHour: Math.min(24, toHour) };
}

/** Position of a block inside a column, as percentages of the visible range (by minutes, not by cells). */
export function blockPosition(
  slot: Slot,
  range: { fromHour: number; toHour: number },
) {
  const total = (range.toHour - range.fromHour) * 60;
  const start = (toMinutes(slot.startTime) ?? 0) - range.fromHour * 60;
  const end = (toMinutes(slot.endTime) ?? 0) - range.fromHour * 60;
  return { top: (start / total) * 100, height: ((end - start) / total) * 100 };
}

const DAY_MS = 24 * 60 * 60 * 1000;
const isDate = (value: string) => /^\d{4}-\d{2}-\d{2}$/.test(value);
const parseDate = (date: string) => {
  const [year, month, day] = date.split("-").map(Number);
  return Date.UTC(year, month - 1, day);
};
export const addDays = (date: string, days: number) =>
  new Date(parseDate(date) + days * DAY_MS).toISOString().slice(0, 10);
export const weekdayOf = (date: string) =>
  new Date(parseDate(date)).getUTCDay();

export type PreviewCell = {
  date: string;
  /** Day of the month, for display. */
  day: number;
  /** False for days before the start of the preview and after its end. */
  inRange: boolean;
  slots: Slot[];
};

/**
 * Month-style preview of the sessions `/sessions/generate` would create from these slots: it starts at the later of
 * today and the effective date (the server applies the latest schedule whose EffectiveFrom is not after the date)
 * and covers `weeks` weeks. Rows are Monday-first and run from the first week to the week of the last day.
 */
export function previewWeeks({
  slots,
  effectiveFrom,
  today,
  weeks = 5,
}: {
  slots: Slot[];
  effectiveFrom: string;
  today: string;
  weeks?: number;
}): PreviewCell[][] {
  const start =
    isDate(effectiveFrom) && effectiveFrom > today ? effectiveFrom : today;
  const end = addDays(start, weeks * 7 - 1);
  const first = addDays(start, -((weekdayOf(start) + 6) % 7));
  const rows = Math.floor((parseDate(end) - parseDate(first)) / DAY_MS / 7) + 1;
  const sorted = sortSlots(slots);
  return Array.from({ length: rows }, (_, row) =>
    Array.from({ length: 7 }, (_, column) => {
      const date = addDays(first, row * 7 + column);
      const inRange = date >= start && date <= end;
      return {
        date,
        day: Number(date.slice(8, 10)),
        inRange,
        slots: inRange
          ? sorted.filter((s) => s.dayOfWeek === weekdayOf(date))
          : [],
      };
    }),
  );
}

export const previewCount = (rows: PreviewCell[][]) =>
  rows.flat().reduce((sum, cell) => sum + cell.slots.length, 0);
