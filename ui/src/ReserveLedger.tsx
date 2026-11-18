import { useRef, useState, type FormEvent } from "react";
import { api, ApiError } from "./api";
import { money } from "./tuition-api";
import type { ReserveRequest, StudentReserve } from "./reserve-api";

const text = (e: unknown) =>
  e instanceof ApiError
    ? Object.values(e.errors ?? {})
        .flat()
        .join(" ") || e.message
    : "Không thể hoàn tất thao tác. Vui lòng thử lại.";

/** Reserve balance, the month ledger (opening / newly approved / used / carried), deduction and reversal. */
export function ReserveLedgerBlock({
  classId,
  student,
  version,
  canWrite,
  onDenied,
  onChanged,
}: {
  classId: string;
  student: StudentReserve;
  version: number;
  canWrite: boolean;
  onDenied: (error: ApiError) => void;
  onChanged: () => void;
}) {
  const { reserve, movements } = student;
  const hasReserve =
    BigInt(reserve.approvedVnd) > 0n || student.uses.length > 0;
  if (!hasReserve) return null;
  const payable = student.periods.filter((p) => BigInt(p.debtVnd) > 0n);
  const excess = student.periods.filter((p) => BigInt(p.reserveExcessVnd) > 0n);
  return (
    <div
      className="reserve-ledger"
      aria-label={`Sổ bảo lưu của ${student.name}`}
    >
      <h5>Sổ bảo lưu</h5>
      <p data-testid={`reserve-balance-${student.studentId}`}>
        Đã duyệt {money(reserve.approvedVnd)} · Đã khấu trừ vào học phí{" "}
        {money(reserve.usedVnd)} · Còn lại {money(reserve.remainingVnd)}
      </p>
      <p data-testid={`reserve-movements-${student.studentId}`}>
        Tháng {movements.month}: đầu kỳ {money(movements.openingVnd)} · mới
        duyệt {money(movements.approvedVnd)} · đã dùng{" "}
        {money(movements.usedVnd)}
        {BigInt(movements.cancelledVnd) > 0n
          ? ` · đã hủy ${money(movements.cancelledVnd)}`
          : ""}{" "}
        · chuyển sang kỳ sau {money(movements.carriedVnd)}
      </p>
      {excess.map((p) => (
        <p key={p.month} className="notice" role="status">
          Kỳ {p.month}: bảo lưu đã dùng vượt nghĩa vụ{" "}
          {money(p.reserveExcessVnd)} (học phí đã giảm). Hãy hủy lần khấu trừ
          tương ứng; hệ thống không tự dùng lại.
        </p>
      ))}
      {canWrite && BigInt(reserve.remainingVnd) > 0n && payable.length > 0 && (
        <UseForm
          classId={classId}
          studentId={student.studentId}
          version={version}
          periods={payable}
          remaining={reserve.remainingVnd}
          onDenied={onDenied}
          onSaved={onChanged}
        />
      )}
      {student.uses.length > 0 && (
        <ul aria-label="Các lần khấu trừ bảo lưu">
          {student.uses.map((u) => (
            <li key={u.id} data-status={u.status}>
              Kỳ {u.month} · {money(u.amountVnd)} · ngày {u.usedOn} ·{" "}
              {u.status === "active"
                ? "Đang áp dụng"
                : `Đã hủy: ${u.reversalReason ?? ""}`}
              {u.status === "active" && canWrite && (
                <ReverseUse
                  classId={classId}
                  useId={u.id}
                  onDenied={onDenied}
                  onDone={onChanged}
                />
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function UseForm({
  classId,
  studentId,
  version,
  periods,
  remaining,
  onDenied,
  onSaved,
}: {
  classId: string;
  studentId: string;
  version: number;
  periods: StudentReserve["periods"];
  remaining: string;
  onDenied: (error: ApiError) => void;
  onSaved: () => void;
}) {
  const [month, setMonth] = useState(periods[0].month);
  const [amount, setAmount] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [uncertain, setUncertain] = useState(false);
  // The exact command is kept after a transport failure: it may already have committed.
  const pending = useRef<{
    studentId: string;
    month: string;
    amountVnd: string;
    idempotencyKey: string;
    version: number;
  } | null>(null);
  const chosen = periods.find((p) => p.month === month) ?? periods[0];
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (busy) return;
    setError("");
    if (!pending.current) {
      if (!/^\d{1,18}$/.test(amount) || BigInt(amount) <= 0n) {
        setError("Nhập số tiền nguyên đồng dương.");
        return;
      }
      pending.current = {
        studentId,
        month: chosen.month,
        amountVnd: amount,
        idempotencyKey: crypto.randomUUID(),
        version,
      };
    }
    setBusy(true);
    try {
      await api(
        `/billing/classes/${classId}/reserve/uses`,
        "POST",
        pending.current,
      );
      pending.current = null;
      setUncertain(false);
      setAmount("");
      onSaved();
    } catch (e) {
      if (e instanceof ApiError && e.status < 500) {
        pending.current = null;
        setUncertain(false);
        setError(text(e));
        onDenied(e);
        if (e.status === 409) onSaved();
      } else {
        setUncertain(true);
        setError(
          "Kết quả chưa rõ. Thử lại khấu trừ này để đối chiếu, tránh khấu trừ trùng.",
        );
      }
    } finally {
      setBusy(false);
    }
  }
  const locked = busy || uncertain;
  return (
    <form
      className="roster-form"
      aria-label="Khấu trừ bảo lưu vào học phí"
      onSubmit={submit}
      noValidate
    >
      <h5>Khấu trừ bảo lưu vào học phí</h5>
      <p>
        Còn {money(remaining)} trong bảo lưu. Chỉ khấu trừ tối đa phần học phí
        kỳ đó còn phải trả.
      </p>
      <label className="field">
        Kỳ học phí
        <select
          value={chosen.month}
          disabled={locked}
          onChange={(e) => setMonth(e.target.value)}
        >
          {periods.map((p) => (
            <option key={p.month} value={p.month}>
              {p.month} · còn phải trả {money(p.debtVnd)}
            </option>
          ))}
        </select>
      </label>
      <label className="field">
        Số tiền khấu trừ (đồng)
        <input
          inputMode="numeric"
          maxLength={18}
          value={amount}
          disabled={locked}
          onChange={(e) => setAmount(e.target.value)}
        />
      </label>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      <button className="primary" disabled={busy}>
        {busy ? "Đang lưu…" : uncertain ? "Thử lại khấu trừ" : "Khấu trừ"}
      </button>
    </form>
  );
}

function ReverseUse({
  classId,
  useId,
  onDenied,
  onDone,
}: {
  classId: string;
  useId: string;
  onDenied: (error: ApiError) => void;
  onDone: () => void;
}) {
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [uncertain, setUncertain] = useState(false);
  const pending = useRef<{ reason: string; idempotencyKey: string } | null>(
    null,
  );
  async function submit() {
    if (busy) return;
    setError("");
    if (!pending.current) {
      if (!reason.trim()) {
        setError("Nhập lý do hủy lần khấu trừ.");
        return;
      }
      pending.current = { reason, idempotencyKey: crypto.randomUUID() };
    }
    setBusy(true);
    try {
      await api(
        `/billing/classes/${classId}/reserve/uses/${useId}/reversal`,
        "POST",
        pending.current,
      );
      pending.current = null;
      setUncertain(false);
      onDone();
    } catch (e) {
      if (e instanceof ApiError && e.status < 500) {
        pending.current = null;
        setUncertain(false);
        setError(text(e));
        onDenied(e);
        if (e.status === 409) onDone();
      } else {
        setUncertain(true);
        setError("Kết quả chưa rõ. Bấm lại để đối chiếu, tránh hủy trùng.");
      }
    } finally {
      setBusy(false);
    }
  }
  // Rendered inside a list item after the use's text, so it always starts on its own line.
  if (!open)
    return (
      <div className="form-actions">
        <button onClick={() => setOpen(true)}>Hủy lần khấu trừ</button>
      </div>
    );
  return (
    <div className="reserve-inline-form">
      <label className="field">
        Lý do hủy khấu trừ
        <input
          maxLength={1000}
          value={reason}
          disabled={busy || uncertain}
          onChange={(e) => setReason(e.target.value)}
        />
      </label>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      <div className="form-actions">
        <button disabled={busy} onClick={() => void submit()}>
          Xác nhận hủy khấu trừ
        </button>
      </div>
    </div>
  );
}

/** Cancels an approved reserve. The server refuses while any use is still active, so what was used is reconciled first. */
export function CancelReserve({
  classId,
  request,
  onDenied,
  onDone,
}: {
  classId: string;
  request: ReserveRequest;
  onDenied: (error: ApiError) => void;
  onDone: () => void;
}) {
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [uncertain, setUncertain] = useState(false);
  const pending = useRef<{
    reason: string;
    idempotencyKey: string;
    version: number;
  } | null>(null);
  async function submit() {
    if (busy) return;
    setError("");
    if (!pending.current) {
      if (!reason.trim()) {
        setError("Nhập lý do hủy bảo lưu.");
        return;
      }
      pending.current = {
        reason,
        idempotencyKey: crypto.randomUUID(),
        version: request.version,
      };
    }
    setBusy(true);
    try {
      await api(
        `/billing/classes/${classId}/reserve/requests/${request.id}/cancellation`,
        "POST",
        pending.current,
      );
      pending.current = null;
      setUncertain(false);
      onDone();
    } catch (e) {
      if (e instanceof ApiError && e.status < 500) {
        pending.current = null;
        setUncertain(false);
        setError(text(e));
        onDenied(e);
      } else {
        setUncertain(true);
        setError("Kết quả chưa rõ. Bấm lại để đối chiếu, tránh hủy trùng.");
      }
    } finally {
      setBusy(false);
    }
  }
  if (!open)
    return (
      <div className="form-actions">
        <button onClick={() => setOpen(true)}>Hủy bảo lưu</button>
      </div>
    );
  return (
    <div className="reserve-inline-form">
      <label className="field">
        Lý do hủy bảo lưu
        <input
          maxLength={1000}
          value={reason}
          disabled={busy || uncertain}
          onChange={(e) => setReason(e.target.value)}
        />
      </label>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      <div className="form-actions">
        <button disabled={busy} onClick={() => void submit()}>
          Xác nhận hủy bảo lưu
        </button>
      </div>
    </div>
  );
}
