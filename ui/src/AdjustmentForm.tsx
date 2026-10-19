import { useRef, useState, type FormEvent } from "react";
import { api, ApiError } from "./api";

type Draft = {
  studentId: string;
  month: string;
  direction: string;
  amountVnd: string;
  reason: string;
  idempotencyKey: string;
  version: number;
};

export function AdjustmentForm({
  classId,
  studentId,
  month,
  version,
  canWrite,
  canReplay,
  onSaved,
  onDenied,
}: {
  classId: string;
  studentId: string;
  month: string;
  version: number;
  canWrite: boolean;
  canReplay: boolean;
  onSaved: () => void;
  onDenied: (error: ApiError) => void;
}) {
  const [direction, setDirection] = useState("increase");
  const [amount, setAmount] = useState("");
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [uncertain, setUncertain] = useState(false);
  const [error, setError] = useState("");
  const [saved, setSaved] = useState(false);
  const pending = useRef<Draft | null>(null);
  const submitting = useRef(false);
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (submitting.current || !(canWrite || (uncertain && canReplay))) return;
    setError("");
    setSaved(false);
    if (
      !pending.current &&
      (!/^\d{1,18}$/.test(amount) || BigInt(amount) <= 0n || !reason.trim())
    ) {
      setError(
        "Nhập số nguyên đồng dương, tối đa 18 chữ số và lý do điều chỉnh.",
      );
      return;
    }
    pending.current ??= {
      studentId,
      month,
      direction,
      amountVnd: amount,
      reason,
      idempotencyKey: crypto.randomUUID(),
      version,
    };
    submitting.current = true;
    setBusy(true);
    try {
      await api(
        `/billing/classes/${classId}/adjustments`,
        "POST",
        pending.current,
      );
      pending.current = null;
      setUncertain(false);
      setAmount("");
      setReason("");
      setSaved(true);
      onSaved();
    } catch (e) {
      if (e instanceof ApiError && e.status < 500) {
        pending.current = null;
        setUncertain(false);
        setError(
          Object.values(e.errors ?? {})
            .flat()
            .join(" ") || e.message,
        );
        onDenied(e);
      } else {
        setUncertain(true);
        setError(
          "Kết quả lưu chưa rõ. Thử lại điều chỉnh này để tránh ghi trùng.",
        );
      }
    } finally {
      submitting.current = false;
      setBusy(false);
    }
  }
  return (
    <form
      className="roster-form"
      aria-label="Điều chỉnh học phí"
      onSubmit={submit}
      noValidate
      aria-busy={busy}
    >
      <h4>Điều chỉnh học phí tháng {pending.current?.month ?? month}</h4>
      <label className="field">
        Chiều điều chỉnh
        <select
          value={direction}
          disabled={busy || uncertain}
          onChange={(e) => setDirection(e.target.value)}
        >
          <option value="increase">Tăng học phí</option>
          <option value="decrease">Giảm học phí</option>
        </select>
      </label>
      <label className="field">
        Số tiền điều chỉnh (đồng)
        <input
          inputMode="numeric"
          maxLength={18}
          value={amount}
          disabled={busy || uncertain}
          onChange={(e) => setAmount(e.target.value)}
        />
      </label>
      <label className="field">
        Lý do điều chỉnh
        <textarea
          maxLength={1000}
          value={reason}
          disabled={busy || uncertain}
          onChange={(e) => setReason(e.target.value)}
        />
      </label>
      <p>
        Khoản giảm tối đa bằng nghĩa vụ của kỳ. Tiền đã phân bổ vượt phí được
        giữ riêng để đối chiếu.
      </p>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      {saved && (
        <p role="status" className="notice success">
          Đã lưu điều chỉnh.
        </p>
      )}
      <button
        className="primary"
        disabled={busy || !(canWrite || (uncertain && canReplay))}
      >
        {busy
          ? "Đang lưu…"
          : uncertain
            ? "Thử lại điều chỉnh"
            : "Lưu điều chỉnh"}
      </button>
    </form>
  );
}
