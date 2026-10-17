import { useState, type FormEvent } from "react";
import { api, ApiError } from "./api";
import { today } from "./roster-api";
import { weekDays, type WeeklySchedule, type WeeklySlot } from "./session-api";

const defaultSlot = () => ({
  dayOfWeek: 1,
  startTime: "18:00",
  endTime: "19:30",
});
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
  const [slots, setSlots] = useState<WeeklySlot[]>(() =>
    schedule.version
      ? schedule.slots.map((s) => ({
          ...s,
          startTime: s.startTime.slice(0, 5),
          endTime: s.endTime.slice(0, 5),
        }))
      : [defaultSlot()],
  );
  const [effectiveFrom, setEffectiveFrom] = useState(
    schedule.effectiveFrom ?? today(),
  );
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  function change(index: number, draft: Partial<WeeklySlot>) {
    setSlots((current) =>
      current.map((s, i) => (i === index ? { ...s, ...draft } : s)),
    );
  }
  async function submit(event: FormEvent) {
    event.preventDefault();
    setError("");
    if (
      !effectiveFrom ||
      slots.some((s) => !s.startTime || !s.endTime || s.startTime >= s.endTime)
    ) {
      setError(
        "Vui lòng nhập ngày hiệu lực và khung giờ hợp lệ trong cùng ngày.",
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
      {slots.map((slot, index) => (
        <fieldset className="schedule-slot" key={index} disabled={busy}>
          <legend>Khung giờ {index + 1}</legend>
          <label>
            Ngày trong tuần {index + 1}
            <select
              value={slot.dayOfWeek}
              onChange={(e) =>
                change(index, { dayOfWeek: Number(e.target.value) })
              }
            >
              {weekDays.map((name, i) => (
                <option value={i} key={name}>
                  {name}
                </option>
              ))}
            </select>
          </label>
          <label>
            Giờ bắt đầu {index + 1}
            <input
              type="time"
              value={slot.startTime}
              onChange={(e) => change(index, { startTime: e.target.value })}
            />
          </label>
          <label>
            Giờ kết thúc {index + 1}
            <input
              type="time"
              value={slot.endTime}
              onChange={(e) => change(index, { endTime: e.target.value })}
            />
          </label>
          <button
            type="button"
            onClick={() =>
              setSlots((current) => current.filter((_, i) => i !== index))
            }
          >
            Xóa khung giờ {index + 1}
          </button>
        </fieldset>
      ))}
      <button
        type="button"
        disabled={busy || slots.length >= 100}
        onClick={() => setSlots((current) => [...current, defaultSlot()])}
      >
        Thêm khung giờ
      </button>
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
