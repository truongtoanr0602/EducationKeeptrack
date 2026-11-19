import { useState, type FormEvent, type KeyboardEvent } from "react";
import { api, ApiError } from "./api";
import { today } from "./roster-api";
import { weekDays, type WeeklySchedule } from "./session-api";
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
  sortSlots,
  weekOrder,
  type Slot,
} from "./schedule-calendar";

const matchDuration = (start: string, end: string) =>
  durations.find((minutes) => endFor(start, minutes) === end) ?? null;

export function ScheduleForm({
  classId,
  schedule,
  canWrite,
  onSaved,
  onCancel,
  onDenied,
}: {
  classId: string;
  schedule: WeeklySchedule;
  canWrite: boolean;
  onSaved: () => void;
  onCancel: () => void;
  onDenied: (error: ApiError) => void;
}) {
  const [slots, setSlots] = useState<Slot[]>(() =>
    sortSlots(
      schedule.version
        ? schedule.slots.map((s) => ({
            ...s,
            startTime: hhmm(s.startTime),
            endTime: hhmm(s.endTime),
          }))
        : [],
    ),
  );
  const [effectiveFrom, setEffectiveFrom] = useState(
    schedule.effectiveFrom ?? today(),
  );
  // Quick entry: the chosen days share one start and end; a duration chip only fills the end time in.
  const [days, setDays] = useState<number[]>([]);
  const [start, setStart] = useState("18:00");
  const [end, setEnd] = useState("19:30");
  const [duration, setDuration] = useState<number | null>(90);
  const [editing, setEditing] = useState<number | null>(null);
  const [entryError, setEntryError] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

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
    setSlots((current) => current.filter((_, i) => i !== index));
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
    setSlots(result.slots);
    setEditing(null);
    setDays([]);
    setEntryError("");
  }
  function onEntryKey(event: KeyboardEvent<HTMLFieldSetElement>) {
    // Enter inside the entry fields adds the slot instead of saving the whole schedule.
    if (event.key === "Enter" && event.target instanceof HTMLInputElement) {
      event.preventDefault();
      applyEntry();
    }
  }
  function drawn(day: number, from: string, to: string) {
    const result = addSlots(slots, [day], from, to);
    if ("error" in result) setEntryError(result.error);
    else {
      setSlots(result.slots);
      setEntryError("");
    }
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError("");
    if (!effectiveFrom) {
      setError("Vui lòng nhập ngày lịch bắt đầu áp dụng.");
      return;
    }
    if (editing !== null || days.length > 0) {
      setError(
        "Bạn đang soạn một khung giờ chưa thêm vào lịch. Bấm “Thêm vào lịch” (hoặc “Cập nhật khung giờ”) trước khi lưu.",
      );
      return;
    }
    setBusy(true);
    try {
      await api(`/classes/${classId}/schedule`, "PUT", {
        version: schedule.version,
        effectiveFrom,
        slots: slots.map((s) => ({
          ...s,
          startTime: `${s.startTime}:00`,
          endTime: `${s.endTime}:00`,
        })),
      });
      onSaved();
    } catch (e) {
      setError(
        e instanceof ApiError
          ? Object.values(e.errors ?? {})
              .flat()
              .join(" ") || e.message
          : "Không thể lưu lịch tuần. Vui lòng thử lại.",
      );
      if (e instanceof ApiError) onDenied(e);
    } finally {
      setBusy(false);
    }
  }
  return (
    <form
      className="roster-form schedule-form"
      onSubmit={submit}
      noValidate
      aria-busy={busy}
    >
      <h3>Thiết lập lịch tuần</h3>
      <p>
        Lịch mới chỉ dùng khi tạo thêm buổi. Các buổi đã lưu được giữ nguyên.
        Xóa mọi khung giờ để ngừng lịch tuần từ ngày hiệu lực.
      </p>
      <div className="field">
        <label htmlFor="schedule-effective">Lịch áp dụng từ ngày</label>
        <input
          id="schedule-effective"
          type="date"
          value={effectiveFrom}
          disabled={busy}
          onChange={(e) => setEffectiveFrom(e.target.value)}
        />
      </div>

      <fieldset
        className="schedule-entry"
        disabled={busy}
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
        disabled={busy}
      />
      {slots.length === 0 ? (
        <p className="muted">
          Chưa có khung giờ nào. Lưu lịch trống sẽ ngừng lịch tuần từ ngày hiệu
          lực.
        </p>
      ) : (
        <ul className="slot-list" aria-label="Khung giờ trong lịch tuần">
          {slots.map((slot, index) => (
            <li key={`${slot.dayOfWeek}-${slot.startTime}`}>
              <span>{slotLabel(slot)}</span>
              <button
                type="button"
                disabled={busy}
                aria-label={`Sửa ${slotLabel(slot)}`}
                onClick={() => startEditing(index)}
              >
                Sửa
              </button>
              <button
                type="button"
                disabled={busy}
                aria-label={`Xóa ${slotLabel(slot)}`}
                onClick={() => removeSlot(index)}
              >
                Xóa
              </button>
            </li>
          ))}
        </ul>
      )}
      <MonthPreview slots={slots} effectiveFrom={effectiveFrom} />

      {error && (
        <p className="error" role="alert">
          {error}
        </p>
      )}
      <div className="roster-actions">
        <button type="button" disabled={busy} onClick={onCancel}>
          Hủy thiết lập lịch
        </button>
        <button className="primary" disabled={busy || !canWrite}>
          {busy ? "Đang lưu…" : "Lưu lịch tuần"}
        </button>
      </div>
    </form>
  );
}
