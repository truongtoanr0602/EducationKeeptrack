import { useState } from "react";
import { api, ApiError } from "./api";
import { money } from "./tuition-api";

type Sample = {
  amountVnd: string;
  content: string;
  qrKind: "vietqr" | "static" | "manual";
  qrDataUrl: string | null;
};

function message(e: unknown) {
  return e instanceof ApiError
    ? Object.values(e.errors ?? {})
        .flat()
        .join(" ") || e.message
    : "Không thể kết nối. Vui lòng thử lại.";
}

/**
 * A QR for the saved account and an amount the teacher types, to check the account is right. It belongs to no
 * student and records nothing. `suggestedAmount` lets a screen that knows the class fee start from it.
 */
export function SampleQr({
  disabled,
  reason,
  suggestedAmount = "10000",
  onDenied,
}: {
  disabled: boolean;
  /** Why the button is off, shown to the teacher. */
  reason?: string;
  suggestedAmount?: string;
  onDenied?: (e: ApiError) => void;
}) {
  const [amount, setAmount] = useState(suggestedAmount);
  const [sample, setSample] = useState<Sample | null>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  async function create() {
    setBusy(true);
    setError("");
    try {
      setSample(
        await api<Sample>("/billing/recipient/sample-qr", "POST", {
          amountVnd: amount,
        }),
      );
    } catch (e) {
      setSample(null);
      setError(message(e));
      if (e instanceof ApiError) onDenied?.(e);
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="sample-qr" aria-label="QR mẫu">
      <h5>QR mẫu</h5>
      <p className="muted">
        Tạo thử một mã QR từ tài khoản đã lưu để kiểm tra đúng người nhận. Mã
        này không gắn với học sinh nào và không ghi nhận khoản thu.
      </p>
      <label htmlFor="sample-qr-amount">Số tiền mẫu (đồng)</label>
      <input
        id="sample-qr-amount"
        inputMode="numeric"
        autoComplete="off"
        maxLength={13}
        value={amount}
        disabled={busy}
        onChange={(e) => setAmount(e.target.value.replace(/\D/g, ""))}
      />
      <button
        type="button"
        disabled={disabled || busy || !amount}
        onClick={() => void create()}
      >
        {busy ? "Đang tạo…" : "Tạo QR mẫu"}
      </button>
      {disabled && reason && <p className="muted">{reason}</p>}
      {error && <p role="alert">{error}</p>}
      {sample?.qrKind === "vietqr" && sample.qrDataUrl && (
        <figure>
          <img
            className="payment-qr"
            src={sample.qrDataUrl}
            alt={`QR mẫu chuyển ${money(sample.amountVnd)} đồng`}
          />
          <figcaption>
            Chuyển {money(sample.amountVnd)} đồng, nội dung “{sample.content}”.
            Chỉ để kiểm tra, đừng chuyển tiền thật.
          </figcaption>
        </figure>
      )}
      {sample?.qrKind === "static" && sample.qrDataUrl && (
        <figure>
          <img
            className="payment-qr"
            src={sample.qrDataUrl}
            alt="QR tĩnh của ngân hàng"
          />
          <figcaption role="status">
            Chưa tạo được QR theo số tiền, nên đây là QR tĩnh bạn đã tải lên,{" "}
            <strong>không kèm số tiền</strong>.
          </figcaption>
        </figure>
      )}
      {sample?.qrKind === "manual" && (
        <p role="status">
          Chưa tạo được QR lúc này. Kiểm tra lại thông tin tài khoản hoặc thử
          lại sau; hóa đơn vẫn dùng hướng dẫn chuyển khoản thủ công.
        </p>
      )}
    </section>
  );
}
