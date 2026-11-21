import { useRef, useState, type FormEvent } from "react";
import { Popup } from "./ui/popup";
import { toast } from "sonner";
import { api, ApiError } from "./api";
import { money } from "./tuition-api";
import type { Receipt, ReceiptState } from "./payments-api";
import {
  apiFieldErrors,
  useFieldErrors,
  type FieldErrors,
} from "./ui/field-errors";
import { formatDate, formatMonth } from "./format-date";

type Command = {
  action: "cancel" | "replace";
  reason: string;
  idempotencyKey: string;
  version: number;
  amountVnd?: string;
  receivedOn?: string;
  note?: string;
};
const statuses = {
  active: "Có hiệu lực",
  cancelled: "Đã hủy",
  replaced: "Đã thay thế",
};
function describe(state: ReceiptState) {
  return `${statuses[state.status]} · ${formatDate(state.receivedOn)} · ${money(state.amountVnd)}${state.note ? ` · ${state.note}` : ""} · Phân bổ: ${state.allocations.map((a) => `${formatMonth(a.month)}: ${money(a.amountVnd)}`).join("; ") || "Chưa phân bổ"} · Chưa phân bổ: ${money(state.unallocatedVnd)}`;
}

export function ReceiptCorrection({
  receipt,
  classId,
  version,
  canWrite,
  canReplay,
  onSaved,
  onDenied,
}: {
  receipt: Receipt;
  classId: string;
  version: number;
  canWrite: boolean;
  canReplay: boolean;
  onSaved: () => void;
  onDenied: (error: ApiError) => void;
}) {
  const [mode, setMode] = useState<"cancel" | "replace" | null>(null);
  const [amount, setAmount] = useState(receipt.amountVnd);
  const [date, setDate] = useState(receipt.receivedOn);
  const [note, setNote] = useState(receipt.note ?? "");
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [uncertain, setUncertain] = useState(false);
  const [error, setError] = useState("");
  const [history, setHistory] = useState(false);
  const fields = useFieldErrors();
  const pending = useRef<Command | null>(null);
  const submitting = useRef(false);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    if (submitting.current || !mode || !(canWrite || (uncertain && canReplay)))
      return;
    setError("");
    fields.clear();
    if (!pending.current) {
      const invalid: FieldErrors = {};
      if (!reason.trim()) invalid.reason = "Nhập lý do sửa hoặc hủy.";
      if (mode === "replace") {
        if (!/^\d{1,18}$/.test(amount) || BigInt(amount) <= 0n)
          invalid.amount = "Nhập số tiền là số nguyên đồng dương.";
        if (!date) invalid.date = "Chọn ngày thực nhận.";
      }
      if (Object.keys(invalid).length > 0) {
        fields.show(invalid, form);
        setError(
          invalid.reason
            ? "Vui lòng nhập lý do sửa/hủy."
            : "Nhập số nguyên đồng dương và ngày thực nhận.",
        );
        return;
      }
      pending.current = {
        action: mode,
        reason,
        idempotencyKey: crypto.randomUUID(),
        version,
        ...(mode === "replace"
          ? { amountVnd: amount, receivedOn: date, note }
          : {}),
      };
    }
    submitting.current = true;
    setBusy(true);
    try {
      await api(
        `/billing/classes/${classId}/payments/${receipt.id}/corrections`,
        "POST",
        pending.current,
      );
      pending.current = null;
      setUncertain(false);
      setMode(null);
      setReason("");
      toast.success("Khoản nhận tiền đã được cập nhật.");
      onSaved();
    } catch (e) {
      if (e instanceof ApiError && e.status < 500) {
        pending.current = null;
        setUncertain(false);
        fields.show(
          apiFieldErrors(e, {
            reason: "reason",
            amountVnd: "amount",
            receivedOn: "date",
          }),
          form,
        );
        setError(
          Object.values(e.errors ?? {})
            .flat()
            .join(" ") || e.message,
        );
        onDenied(e);
      } else {
        setUncertain(true);
        setError(
          "Kết quả lưu chưa rõ. Thử lại sửa/hủy để đối chiếu, tránh xử lý hai lần.",
        );
      }
    } finally {
      submitting.current = false;
      setBusy(false);
    }
  }
  const correction = receipt.correction;
  return (
    <>
      <p>
        Trạng thái: {statuses[receipt.status]} · Ghi nhận:{" "}
        {new Date(receipt.recordedAt).toLocaleString("vi-VN")}
      </p>
      {correction && (
        <>
          <p>
            Lý do: {correction.reason} · Người sửa: {correction.recordedBy} ·
            Thời điểm sửa:{" "}
            {new Date(correction.recordedAt).toLocaleString("vi-VN")}
          </p>
          {correction.replacementId && (
            <p>Khoản thay thế: {correction.replacementId}</p>
          )}
          <button
            type="button"
            onClick={() => setHistory(!history)}
            aria-expanded={history}
          >
            Xem trước/sau
          </button>
          {history && (
            <div className="tuition-table-wrap" tabIndex={0}>
              <table aria-label="Đối chiếu trước/sau">
                <thead>
                  <tr>
                    <th>Khoản nhận</th>
                    <th>Trước</th>
                    <th>Sau · phiên bản {correction.changes.version}</th>
                  </tr>
                </thead>
                <tbody>
                  {correction.changes.after.map((after) => {
                    const before = correction.changes.before.find(
                      (x) => x.id === after.id,
                    );
                    return (
                      <tr key={after.id}>
                        <td>{after.id}</td>
                        <td>{before ? describe(before) : "Chưa có"}</td>
                        <td>{describe(after)}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
      {receipt.status === "active" && !mode && (
        <div className="form-actions">
          <button
            type="button"
            disabled={!canWrite}
            onClick={() => setMode("replace")}
          >
            Sửa khoản nhận tiền
          </button>
          <button
            type="button"
            disabled={!canWrite}
            onClick={() => setMode("cancel")}
          >
            Hủy khoản nhận tiền
          </button>
        </div>
      )}
      {mode && (
        <Popup
          open
          title={
            mode === "cancel" ? "Hủy khoản nhận tiền" : "Sửa khoản nhận tiền"
          }
          locked={busy || uncertain}
          onOpenChange={(next) => {
            if (!next) {
              setMode(null);
              setError("");
            }
          }}
        >
          <form
            className="roster-form"
            aria-label="Sửa hoặc hủy khoản nhận tiền"
            onSubmit={submit}
            noValidate
            aria-busy={busy}
          >
            <p>
              {mode === "cancel"
                ? "Hủy ghi nhận và khôi phục công nợ. Thao tác này không hoàn tiền."
                : "Giữ khoản gốc và tạo khoản thay thế; ngày thực nhận khác thời điểm sửa."}
            </p>
            {mode === "replace" && (
              <>
                <label className="field">
                  Số tiền thay thế (đồng)
                  <input
                    inputMode="numeric"
                    maxLength={18}
                    value={amount}
                    disabled={busy || uncertain}
                    onChange={(e) => setAmount(e.target.value)}
                    {...fields.props("amount")}
                  />
                </label>
                {fields.message("amount")}
                <label className="field">
                  Ngày thực nhận thay thế
                  <input
                    type="date"
                    value={date}
                    disabled={busy || uncertain}
                    onChange={(e) => setDate(e.target.value)}
                    {...fields.props("date")}
                  />
                </label>
                {fields.message("date")}
                <label className="field">
                  Ghi chú thay thế
                  <textarea
                    maxLength={1000}
                    value={note}
                    disabled={busy || uncertain}
                    onChange={(e) => setNote(e.target.value)}
                  />
                </label>
              </>
            )}
            <label className="field">
              Lý do sửa/hủy
              <textarea
                maxLength={1000}
                value={reason}
                disabled={busy || uncertain}
                onChange={(e) => setReason(e.target.value)}
                {...fields.props("reason")}
              />
            </label>
            {fields.message("reason")}
            {error && (
              <p role="alert" className="error">
                {error}
              </p>
            )}
            <div className="form-actions">
              <button
                className="primary"
                disabled={busy || !(canWrite || (uncertain && canReplay))}
              >
                {busy
                  ? "Đang lưu…"
                  : uncertain
                    ? "Thử lại sửa/hủy"
                    : mode === "cancel"
                      ? "Xác nhận hủy khoản nhận tiền"
                      : "Lưu sửa khoản nhận tiền"}
              </button>
              <button
                type="button"
                disabled={busy || uncertain}
                onClick={() => {
                  setMode(null);
                  setError("");
                }}
              >
                Đóng
              </button>
            </div>
          </form>
        </Popup>
      )}
    </>
  );
}
