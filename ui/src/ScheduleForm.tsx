import { useEffect, useState, type FormEvent } from "react";
import { api, ApiError } from "./api";
import { today } from "./roster-api";
import { type WeeklySchedule } from "./session-api";
import { ScheduleEditor } from "./ScheduleEditor";
import { hhmm, sortSlots, type Slot } from "./schedule-calendar";

export function ScheduleForm({
  classId,
  schedule,
  canWrite,
  onSaved,
  onCancel,
  onLockChange,
  onDenied,
}: {
  classId: string;
  schedule: WeeklySchedule;
  canWrite: boolean;
  onSaved: () => void;
  onCancel: () => void;
  onLockChange?: (locked: boolean) => void;
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
  const [pending, setPending] = useState(false);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    onLockChange?.(busy);
    return () => onLockChange?.(false);
  }, [busy, onLockChange]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError("");
    if (!effectiveFrom) {
      setError("Vui lòng nhập ngày lịch bắt đầu áp dụng.");
      return;
    }
    if (pending) {
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
      <ScheduleEditor
        slots={slots}
        onSlotsChange={setSlots}
        effectiveFrom={effectiveFrom}
        onEffectiveFromChange={setEffectiveFrom}
        disabled={busy}
        emptyHint="Chưa có khung giờ nào. Lưu lịch trống sẽ ngừng lịch tuần từ ngày hiệu lực."
        onPendingChange={setPending}
        exceptClassId={classId}
      />
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
