import { api } from "./api";
import { dayShort, hhmm, overlaps, type Slot } from "./schedule-calendar";

// Overlap between a class's slots and the weekly schedules of the teacher's other classes (UX8-7). It only warns:
// the server never refuses an overlapping schedule, because a teacher may teach two groups together.

export type OtherSchedule = {
  classId: string;
  className: string;
  effectiveFrom: string;
  slots: Slot[];
};
/** One slot of another class, with the class it belongs to. */
export type OtherSlot = Slot & { classId: string; className: string };

export type Clash = { className: string; other: Slot };

export const otherSlots = (others: OtherSchedule[]): OtherSlot[] =>
  others.flatMap((o) =>
    o.slots.map((s) => ({ ...s, classId: o.classId, className: o.className })),
  );

/** The other-class slots that overlap one slot (same day, start < other end and other start < end). */
export const clashesOf = (slot: Slot, others: OtherSlot[]): Clash[] =>
  others
    .filter((o) => overlaps(slot, o))
    .map((o) => ({ className: o.className, other: o }));

export const slotText = (slot: Slot) =>
  `${dayShort[slot.dayOfWeek]} ${hhmm(slot.startTime)}–${hhmm(slot.endTime)}`;

/** "Trùng với lớp X (T2 18:00–19:30)": the same text on screen and for a screen reader, never just a colour. */
export const clashText = (clash: Clash) =>
  `Trùng với lớp ${clash.className} (${slotText(clash.other)})`;

/** Every clash of the slot, one sentence each, joined by "; ". Empty when there is none. */
export const clashSentence = (slot: Slot, others: OtherSlot[]) =>
  clashesOf(slot, others).map(clashText).join("; ");

/** Which slots of a schedule clash with another class (indexes into `slots`). */
export const clashingIndexes = (slots: Slot[], others: OtherSlot[]) =>
  new Set(
    slots.flatMap((slot, index) =>
      clashesOf(slot, others).length > 0 ? [index] : [],
    ),
  );

/** Current schedules of the other classes; the class being edited (when it already exists) is left out. */
export async function loadOtherSchedules(
  exceptClassId?: string | null,
): Promise<OtherSchedule[]> {
  const all = await api<OtherSchedule[]>(
    exceptClassId
      ? `/schedules?exceptClassId=${encodeURIComponent(exceptClassId)}`
      : "/schedules",
  );
  return all.filter((x) => x.classId !== exceptClassId);
}
