import { useEffect, useState, type KeyboardEvent, type ReactNode } from "react";
import { weekDays } from "./session-api";
import {
  clashSentence,
  clashingIndexes,
  loadOtherSchedules,
  otherSlots,
  slotText,
  type OtherSchedule,
} from "./schedule-overlap";
import { MonthPreview } from "./MonthPreview";
import { WeekCalendar } from "./WeekCalendar";
import {
  addSlots,
  dayShort,
  durations,
  endFor,
  hhmm,
  replaceSlot,
  slotLabel,
  weekOrder,
  type Slot,
} from "./schedule-calendar";

const matchDuration = (start: string, end: string) =>
  durations.find((minutes) => endFor(start, minutes) === end) ?? null;

/**
 * The weekly schedule editor: start date, quick entry (days, start, duration), week grid, text list and the month
 * preview. The slots and the start date belong to the parent, so the same editor serves the schedule form of an
 * existing class and the class creation flow. `onPendingChange` tells the parent when the teacher has picked days
 * or is editing a slot without having added it yet, so the parent can refuse to save until that is settled.
 */
export function ScheduleEditor({
  slots,
  onSlotsChange,
  effectiveFrom,
  onEffectiveFromChange,
  disabled,
  weeks,
  emptyHint,
  onPendingChange,
  exceptClassId,
  children,
}: {
  slots: Slot[];
  onSlotsChange: (slots: Slot[]) => void;
  effectiveFrom: string;
  onEffectiveFromChange: (date: string) => void;
  disabled?: boolean;
  /** How many weeks the month preview shows; the creation flow passes the number of weeks it will generate. */
  weeks?: number;
  emptyHint: string;
  onPendingChange?: (pending: boolean) => void;
  /** The class being edited, when it already exists: its own schedule is not shown as "another class". */
  exceptClassId?: string | null;
  /** Extra fields shown between the slot list and the preview. */
  children?: ReactNode;
}) {
  // Quick entry: the chosen days share one start and end; a duration chip only fills the end time in.
  const [days, setDays] = useState<number[]>([]);
  const [start, setStart] = useState("18:00");
  const [end, setEnd] = useState("19:30");
  const [duration, setDuration] = useState<number | null>(90);
  const [editing, setEditing] = useState<number | null>(null);
  const [entryError, setEntryError] = useState("");
  const pending = editing !== null || days.length > 0;
  // The other classes' schedules are only a hint: when they cannot be read the editor works without them.
  const [others, setOthers] = useState<OtherSchedule[]>([]);
  const [othersFailed, setOthersFailed] = useState(false);
  useEffect(() => {
    let active = true;
    loadOtherSchedules(exceptClassId).then(
      (list) => {
        if (!active) return;
        setOthers(list);
        setOthersFailed(false);
      },
      () => active && setOthersFailed(true),
    );
    return () => {
      active = false;
    };
  }, [exceptClassId]);
  const otherList = otherSlots(others);
  const clashing = clashingIndexes(slots, otherList);
  // The slot being typed, checked before it is added.
  const entryClash = days
    .map((day) =>
      clashSentence(
        { dayOfWeek: day, startTime: start, endTime: end },
        otherList,
      ),
    )
    .filter(Boolean);
  useEffect(() => {
    onPendingChange?.(pending);
  }, [pending, onPendingChange]);

  function toggleDay(day: number) {
    setEntryError("");
    // While editing one slot a day replaces the day; otherwise days are a multi-select.
    setDays((current) =>
      editing !== null
        ? [day]
        : current.includes(day)
          ? current.filter((d) => d !== day)
          : [...current, day],
    );
  }
  function changeStart(value: string) {
    setStart(value);
    setEntryError("");
    if (duration !== null) setEnd(endFor(value, duration) ?? "");
  }
  function pickDuration(minutes: number) {
    setDuration(minutes);
    setEntryError("");
    setEnd(endFor(start, minutes) ?? "");
  }
  function changeEnd(value: string) {
    setEnd(value);
    setEntryError("");
    setDuration(matchDuration(start, value));
  }
  function stopEditing() {
    setEditing(null);
    setDays([]);
    setEntryError("");
  }
  function startEditing(index: number) {
    const slot = slots[index];
    setEditing(index);
    setDays([slot.dayOfWeek]);
    setStart(hhmm(slot.startTime));
    setEnd(hhmm(slot.endTime));
    setDuration(matchDuration(hhmm(slot.startTime), hhmm(slot.endTime)));
    setEntryError("");
  }
  function removeSlot(index: number) {
    onSlotsChange(slots.filter((_, i) => i !== index));
    if (editing === index) stopEditing();
    else if (editing !== null && index < editing) setEditing(editing - 1);
  }
  function applyEntry() {
    const result =
      editing === null
        ? addSlots(slots, days, start, end)
        : replaceSlot(slots, editing, {
            dayOfWeek: days[0],
            startTime: start,
            endTime: end,
          });
    if ("error" in result) {
      setEntryError(result.error);
      return;
    }
    onSlotsChange(result.slots);
    setEditing(null);
    setDays([]);
    setEntryError("");
  }
  function onEntryKey(event: KeyboardEvent<HTMLFieldSetElement>) {
    // Enter inside the entry fields adds the slot instead of submitting the surrounding form.
    if (event.key === "Enter" && event.target instanceof HTMLInputElement) {
      event.preventDefault();
      applyEntry();
    }
  }
  function drawn(day: number, from: string, to: string) {
    const result = addSlots(slots, [day], from, to);
    if ("error" in result) setEntryError(result.error);
    else {
      onSlotsChange(result.slots);
      setEntryError("");
    }
  }

  return (
    <>
      <div className="field">
        <label htmlFor="schedule-effective">Lịch áp dụng từ ngày</label>
        <input
          id="schedule-effective"
          type="date"
          value={effectiveFrom}
          disabled={disabled}
          onChange={(e) => onEffectiveFromChange(e.target.value)}
        />
      </div>

      <fieldset
        className="schedule-entry"
        disabled={disabled}
        onKeyDown={onEntryKey}
      >
        <legend>{editing === null ? "Thêm khung giờ" : "Sửa khung giờ"}</legend>
        <div className="day-chips" role="group" aria-label="Ngày trong tuần">
          {weekOrder.map((day) => (
            <button
              type="button"
              key={day}
              className="chip"
              aria-pressed={days.includes(day)}
              aria-label={weekDays[day]}
              onClick={() => toggleDay(day)}
            >
              {dayShort[day]}
            </button>
          ))}
        </div>
        <div className="time-row">
          <label>
            Giờ bắt đầu
            <input
              type="time"
              step={900}
              value={start}
              onChange={(e) => changeStart(e.target.value)}
            />
          </label>
          <label>
            Giờ kết thúc
            <input
              type="time"
              step={900}
              value={end}
              onChange={(e) => changeEnd(e.target.value)}
            />
          </label>
        </div>
        <div className="duration-chips" role="group" aria-label="Thời lượng">
          {durations.map((minutes) => (
            <button
              type="button"
              key={minutes}
              className="chip"
              aria-pressed={duration === minutes}
              onClick={() => pickDuration(minutes)}
            >
              {minutes} phút
            </button>
          ))}
        </div>
        {entryError && (
          <p className="error" role="alert">
            {entryError}
          </p>
        )}
        {entryClash.length > 0 && (
          <p className="warn" role="status">
            {entryClash.join("; ")}. Vẫn lưu được nếu bạn dạy ghép.
          </p>
        )}
        <div className="roster-actions">
          <button type="button" className="primary" onClick={applyEntry}>
            {editing === null ? "Thêm vào lịch" : "Cập nhật khung giờ"}
          </button>
          {editing !== null && (
            <button type="button" onClick={stopEditing}>
              Hủy sửa
            </button>
          )}
        </div>
      </fieldset>

      <WeekCalendar
        slots={slots}
        selected={editing}
        onSelect={startEditing}
        onDraw={drawn}
        disabled={disabled}
        others={otherList}
        clashing={clashing}
      />
      {othersFailed && (
        <p className="muted">
          Không tải được lịch các lớp khác nên chưa kiểm tra được trùng giờ.
        </p>
      )}
      {others.length > 0 && (
        <details className="other-schedules">
          <summary>
            Lịch các lớp khác ({others.length} lớp, hiện màu xám trên lưới)
          </summary>
          <ul>
            {others.map((o) => (
              <li key={o.classId}>
                <strong>{o.className}</strong>:{" "}
                {o.slots.map(slotText).join(", ")}
              </li>
            ))}
          </ul>
        </details>
      )}
      {slots.length === 0 ? (
        <p className="muted">{emptyHint}</p>
      ) : (
        <ul className="slot-list" aria-label="Khung giờ trong lịch tuần">
          {slots.map((slot, index) => (
            <li
              key={`${slot.dayOfWeek}-${slot.startTime}`}
              className={clashing.has(index) ? "clash" : undefined}
            >
              <span>{slotLabel(slot)}</span>
              {clashing.has(index) && (
                <span className="warn clash-text">
                  {clashSentence(slot, otherList)}
                </span>
              )}
              <button
                type="button"
                disabled={disabled}
                aria-label={`Sửa ${slotLabel(slot)}`}
                onClick={() => startEditing(index)}
              >
                Sửa
              </button>
              <button
                type="button"
                disabled={disabled}
                aria-label={`Xóa ${slotLabel(slot)}`}
                onClick={() => removeSlot(index)}
              >
                Xóa
              </button>
            </li>
          ))}
        </ul>
      )}
      {children}
      <MonthPreview slots={slots} effectiveFrom={effectiveFrom} weeks={weeks} />
    </>
  );
}
