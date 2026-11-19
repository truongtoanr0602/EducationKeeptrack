import { test, expect } from "@playwright/test";
import {
  addDays,
  addSlots,
  blockPosition,
  dayShort,
  endFor,
  gridHours,
  previewWeeks,
  replaceSlot,
  slotLabel,
  sortSlots,
  weekdayOf,
  weekOrder,
} from "../src/schedule-calendar";
import type { WeeklySlot } from "../src/session-api";

const slot = (
  dayOfWeek: number,
  startTime: string,
  endTime: string,
): WeeklySlot => ({ dayOfWeek, startTime, endTime });

test.describe("weekly schedule logic (no browser, no server)", () => {
  test("the grid is Monday-first while dayOfWeek keeps the server's 0 = Chủ nhật", () => {
    expect(weekOrder.map((d) => dayShort[d])).toEqual([
      "T2",
      "T3",
      "T4",
      "T5",
      "T6",
      "T7",
      "CN",
    ]);
    expect(dayShort[1]).toBe("T2");
    expect(dayShort[0]).toBe("CN");
  });

  test("T2-T4-T6 at 18:00 for 90 minutes becomes three slots", () => {
    const result = addSlots([], [1, 3, 5], "18:00", "19:30");
    expect("slots" in result && result.slots).toEqual([
      slot(1, "18:00", "19:30"),
      slot(3, "18:00", "19:30"),
      slot(5, "18:00", "19:30"),
    ]);
  });

  test("a duration that would cross midnight has no end time and is refused", () => {
    expect(endFor("18:00", 90)).toBe("19:30");
    expect(endFor("23:00", 90)).toBeNull();
    expect(endFor("22:30", 90)).toBeNull(); // would end exactly at midnight, which a TimeOnly cannot hold
    expect(endFor("22:00", 90)).toBe("23:30");
    expect(endFor("", 90)).toBeNull();
  });

  test("invalid input gives a Vietnamese message, never a slot", () => {
    expect(addSlots([], [], "18:00", "19:30")).toEqual({
      error: "Chọn ít nhất một ngày trong tuần.",
    });
    expect(addSlots([], [1], "", "19:30")).toEqual({
      error: "Nhập giờ bắt đầu và giờ kết thúc hợp lệ.",
    });
    expect(addSlots([], [1], "19:30", "19:30")).toEqual({
      error: "Giờ kết thúc phải sau giờ bắt đầu trong cùng ngày.",
    });
    expect(addSlots([], [1], "20:00", "19:30")).toEqual({
      error: "Giờ kết thúc phải sau giờ bắt đầu trong cùng ngày.",
    });
  });

  test("overlap uses the server rule: same day and start < other end && other start < end", () => {
    const monday = [slot(1, "18:00", "19:30")];
    expect(addSlots(monday, [1], "19:00", "20:00")).toEqual({
      error: `Trùng giờ với ${slotLabel(monday[0])}.`,
    });
    expect(addSlots(monday, [1], "18:00", "19:30")).toEqual({
      error: `Trùng giờ với ${slotLabel(monday[0])}.`,
    });
    // Touching boundaries and other days are fine.
    expect("slots" in addSlots(monday, [1], "19:30", "20:30")).toBe(true);
    expect("slots" in addSlots(monday, [2], "18:00", "19:30")).toBe(true);
    // Two new slots from one request must not overlap each other either (same day cannot happen, so use a second call).
    expect("slots" in addSlots(monday, [1, 2], "20:00", "21:00")).toBe(true);
  });

  test("the 100-slot cap of the server is kept", () => {
    const full = Array.from({ length: 100 }, (_, i) =>
      slot(
        i % 7,
        `${String(6 + Math.floor(i / 7)).padStart(2, "0")}:00`,
        `${String(6 + Math.floor(i / 7)).padStart(2, "0")}:30`,
      ),
    );
    expect(addSlots(full, [1], "05:00", "05:30")).toEqual({
      error: "Tối đa 100 khung giờ.",
    });
  });

  test("editing a slot checks only the others, and keeps odd stored times", () => {
    const stored = [slot(1, "18:10", "18:55"), slot(3, "18:00", "19:30")];
    const edited = replaceSlot(stored, 0, slot(1, "18:10", "19:00"));
    expect("slots" in edited && edited.slots[0]).toEqual(
      slot(1, "18:10", "19:00"),
    );
    expect(replaceSlot(stored, 1, slot(1, "18:30", "19:00"))).toEqual({
      error: `Trùng giờ với ${slotLabel(stored[0])}.`,
    });
  });

  test("slots sort Monday first and Chủ nhật last, then by start", () => {
    const sorted = sortSlots([
      slot(0, "09:00", "10:00"),
      slot(3, "18:00", "19:00"),
      slot(1, "19:00", "20:00"),
      slot(1, "08:00", "09:00"),
    ]);
    expect(
      sorted.map((s) => `${dayShort[s.dayOfWeek]} ${s.startTime}`),
    ).toEqual(["T2 08:00", "T2 19:00", "T4 18:00", "CN 09:00"]);
  });

  test("labels keep the format the saved list already uses", () => {
    expect(slotLabel(slot(1, "18:00:00", "19:30:00"))).toBe(
      "Thứ Hai · 18:00–19:30",
    );
    expect(slotLabel(slot(0, "08:00", "09:00"))).toBe("Chủ nhật · 08:00–09:00");
  });

  test("the visible hours widen to show slots outside 06:00-22:00", () => {
    expect(gridHours([])).toEqual({ fromHour: 6, toHour: 22 });
    expect(gridHours([slot(1, "18:00", "19:30")])).toEqual({
      fromHour: 6,
      toHour: 22,
    });
    expect(
      gridHours([slot(1, "05:30", "07:00"), slot(2, "21:00", "22:30")]),
    ).toEqual({ fromHour: 5, toHour: 23 });
  });

  test("blocks are placed by minutes, not by 15-minute cells", () => {
    const range = { fromHour: 6, toHour: 22 };
    const block = blockPosition(slot(1, "18:10", "19:00"), range);
    expect(block.top).toBeCloseTo(
      ((18 * 60 + 10 - 6 * 60) / (16 * 60)) * 100,
      5,
    );
    expect(block.height).toBeCloseTo((50 / (16 * 60)) * 100, 5);
  });

  test("date arithmetic is calendar based, so UTC+7 midnight never shifts a day", () => {
    expect(addDays("2026-12-31", 1)).toBe("2027-01-01");
    expect(addDays("2026-03-01", -1)).toBe("2026-02-28");
    expect(weekdayOf("2026-11-18")).toBe(3); // Wednesday
    expect(weekdayOf("2027-01-01")).toBe(5); // Friday
  });

  test("the preview starts at the later of today and the effective date and lists the dates the server would create", () => {
    const monday = [slot(1, "18:00", "19:30")];
    const weeks = previewWeeks({
      slots: monday,
      effectiveFrom: "2026-10-01",
      today: "2026-11-18",
      weeks: 5,
    });
    const cells = weeks.flat();
    // Range is 2026-11-18 .. 2026-12-22; the grid starts on the Monday of that first week.
    expect(cells[0].date).toBe("2026-11-16");
    expect(cells[0].inRange).toBe(false);
    expect(cells.find((c) => c.date === "2026-11-18")?.inRange).toBe(true);
    expect(cells.find((c) => c.date === "2026-12-22")?.inRange).toBe(true);
    expect(cells.find((c) => c.date === "2026-12-23")?.inRange).toBe(false);
    expect(cells.filter((c) => c.slots.length).map((c) => c.date)).toEqual([
      "2026-11-23",
      "2026-11-30",
      "2026-12-07",
      "2026-12-14",
      "2026-12-21",
    ]);
    // A Monday before today is shown dimmed and never counted.
    expect(cells.find((c) => c.date === "2026-11-16")?.slots).toEqual([]);
    expect(weeks.every((w) => w.length === 7)).toBe(true);
  });

  test("a future effective date moves the preview start, and no slots means no sessions", () => {
    const tuesday = [slot(2, "17:00", "18:00")];
    const weeks = previewWeeks({
      slots: tuesday,
      effectiveFrom: "2026-12-01",
      today: "2026-11-18",
      weeks: 4,
    });
    const dates = weeks
      .flat()
      .filter((c) => c.slots.length)
      .map((c) => c.date);
    expect(dates).toEqual([
      "2026-12-01",
      "2026-12-08",
      "2026-12-15",
      "2026-12-22",
    ]);
    expect(
      previewWeeks({
        slots: [],
        effectiveFrom: "2026-12-01",
        today: "2026-11-18",
        weeks: 4,
      })
        .flat()
        .some((c) => c.slots.length),
    ).toBe(false);
  });

  test("an empty or malformed effective date falls back to today", () => {
    const weeks = previewWeeks({
      slots: [slot(3, "18:00", "19:00")],
      effectiveFrom: "",
      today: "2026-11-18",
      weeks: 1,
    });
    expect(
      weeks
        .flat()
        .filter((c) => c.slots.length)
        .map((c) => c.date),
    ).toEqual(["2026-11-18"]);
  });
});
