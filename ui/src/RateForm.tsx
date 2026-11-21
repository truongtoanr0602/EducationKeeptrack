import { useEffect, useState, type FormEvent } from "react";
import { api, ApiError } from "./api";
import { today } from "./roster-api";
import { type RatePlan } from "./tuition-api";
import {
  apiFieldErrors,
  useFieldErrors,
  type FieldErrors,
} from "./ui/field-errors";

export function RateForm({
  classId,
  plan,
  canWrite,
  onSaved,
  onLockChange,
  onDenied,
}: {
  classId: string;
  plan: RatePlan;
  canWrite: boolean;
  onSaved: (plan: RatePlan) => void;
  onLockChange?: (locked: boolean) => void;
  onDenied: (error: ApiError) => void;
}) {
  const [date, setDate] = useState(today);
  const [amount, setAmount] = useState("");
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    onLockChange?.(busy);
    return () => onLockChange?.(false);
  }, [busy, onLockChange]);
  const [error, setError] = useState("");
  const fields = useFieldErrors();
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    setError("");
    fields.clear();
    if (!date || !/^\d{1,18}$/.test(amount)) {
      const invalid: FieldErrors = {};
      if (!date) invalid.date = "Chọn ngày bắt đầu áp dụng đơn giá.";
      if (!/^\d{1,18}$/.test(amount))
        invalid.amount = "Nhập số đồng không âm, tối đa 18 chữ số.";
      fields.show(invalid, form);
      setError(
        "Vui lòng nhập ngày hiệu lực và số nguyên đồng không âm, tối đa 18 chữ số.",
      );
      return;
    }
    setBusy(true);
    try {
      onSaved(
        await api<RatePlan>(`/billing/classes/${classId}/rates`, "POST", {
          effectiveFrom: date,
          unitPriceVnd: amount,
          version: plan.version,
        }),
      );
    } catch (e) {
      fields.show(
        apiFieldErrors(e, { effectiveFrom: "date", unitPriceVnd: "amount" }),
        form,
      );
      setError(
        e instanceof ApiError
          ? Object.values(e.errors ?? {})
              .flat()
              .join(" ") || e.message
          : "Không thể lưu đơn giá. Vui lòng thử lại.",
      );
      if (e instanceof ApiError) onDenied(e);
    } finally {
      setBusy(false);
    }
  }
  return (
    <form className="roster-form" onSubmit={submit} noValidate aria-busy={busy}>
      <h3>Đơn giá theo ngày hiệu lực</h3>
      <p>
        Đơn giá buổi đã diễn ra được giữ nguyên. Đơn giá mới không ghi đè lịch
        sử.
      </p>
      <div className="field">
        <label htmlFor="tuition-price-date">Đơn giá áp dụng từ ngày</label>
        <input
          id="tuition-price-date"
          type="date"
          disabled={busy}
          value={date}
          onChange={(e) => setDate(e.target.value)}
          {...fields.props("date")}
        />
        {fields.message("date")}
      </div>
      <div className="field">
        <label htmlFor="tuition-price-amount">Đơn giá mỗi buổi (đồng)</label>
        <input
          id="tuition-price-amount"
          inputMode="numeric"
          maxLength={18}
          placeholder="150000"
          disabled={busy}
          value={amount}
          onChange={(e) => setAmount(e.target.value)}
          {...fields.props("amount")}
        />
        {fields.message("amount")}
        <small>
          Nhập số đồng, không dùng dấu chấm/phẩy. Có thể nhập 0 cho buổi miễn
          phí.
        </small>
      </div>
      {error && (
        <p className="error" role="alert">
          {error}
        </p>
      )}
      <button className="primary" disabled={busy || !canWrite}>
        {busy ? "Đang lưu…" : "Lưu đơn giá"}
      </button>
    </form>
  );
}
