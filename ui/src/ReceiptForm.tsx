import { useRef, useState, type FormEvent } from "react";
import { api, ApiError } from "./api";
import { today } from "./roster-api";
import type { Receipt, ReceiptDraft } from "./payments-api";

export function ReceiptForm({
  classId,
  studentId,
  version,
  canWrite,
  canReplay,
  onSaved,
  onDenied,
}: {
  classId: string;
  studentId: string;
  version: number;
  canWrite: boolean;
  canReplay: boolean;
  onSaved: () => void;
  onDenied: (error: ApiError) => void;
}) {
  const [amount, setAmount] = useState("");
  const [date, setDate] = useState(today);
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [uncertain, setUncertain] = useState(false);
  const [saved, setSaved] = useState(false);
  // Keep the exact request after a transport/server failure: it may already have committed.
  const pending = useRef<ReceiptDraft | null>(null);
  const submitting = useRef(false);
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (submitting.current || !(canWrite || (uncertain && canReplay))) return;
    setError("");
    setSaved(false);
    if (
      !pending.current &&
      (!date || !/^\d{1,18}$/.test(amount) || BigInt(amount) <= 0n)
    ) {
      setError("Nhập ngày nhận và số nguyên đồng dương, tối đa 18 chữ số.");
      return;
    }
    pending.current ??= {
      studentId,
      amountVnd: amount,
      receivedOn: date,
      note,
      idempotencyKey: crypto.randomUUID(),
      version,
    };
    submitting.current = true;
    setBusy(true);
    try {
      await api<Receipt>(
        `/billing/classes/${classId}/payments`,
        "POST",
        pending.current,
      );
      pending.current = null;
      setUncertain(false);
      setAmount("");
      setNote("");
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
          "Kết quả lưu chưa rõ. Thử lại khoản nhận tiền này để đối chiếu, tránh ghi trùng.",
        );
      }
    } finally {
      submitting.current = false;
      setBusy(false);
    }
  }
  const locked = busy || uncertain;
  return (
    <form
      className="roster-form"
      aria-label="Ghi nhận tiền"
      onSubmit={submit}
      noValidate
      aria-busy={busy}
    >
      <h4>Nhận tiền học phí</h4>
      <label className="field">
        Số tiền nhận (đồng)
        <input
          inputMode="numeric"
          maxLength={18}
          value={amount}
          disabled={locked}
          onChange={(e) => setAmount(e.target.value)}
        />
      </label>
      <label className="field">
        Ngày nhận tiền
        <input
          type="date"
          value={date}
          disabled={locked}
          onChange={(e) => setDate(e.target.value)}
        />
      </label>
      <label className="field">
        Ghi chú nhận tiền
        <textarea
          maxLength={1000}
          value={note}
          disabled={locked}
          onChange={(e) => setNote(e.target.value)}
        />
      </label>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      {saved && (
        <p role="status" className="notice success">
          Đã ghi nhận tiền.
        </p>
      )}
      <button
        className="primary"
        disabled={busy || !(canWrite || (uncertain && canReplay))}
      >
        {busy
          ? "Đang ghi nhận…"
          : uncertain
            ? "Thử lại khoản nhận tiền"
            : "Ghi nhận tiền"}
      </button>
    </form>
  );
}
