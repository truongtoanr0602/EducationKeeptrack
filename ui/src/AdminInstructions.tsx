import { useEffect, useState, type FormEvent } from "react";
import { api, ApiError } from "./api";
import type { Instructions } from "./admin-api";

// The API body is capped at 128 KB and base64 adds a third, so the server accepts at most 80 KB of image.
const MAX_QR_BYTES = 80 * 1024;

export function AdminInstructions({ onExpired }: { onExpired: () => void }) {
  const [value, setValue] = useState<Instructions | null>(null);
  const [bank, setBank] = useState("");
  const [contact, setContact] = useState("");
  // null keeps the saved QR, "" removes it, base64 replaces it (the server's contract).
  const [qrChange, setQrChange] = useState<string | null>(null);
  const [fileKey, setFileKey] = useState(0);
  const [status, setStatus] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const apply = (item: Instructions) => {
    setValue(item);
    setBank(item.bankDetails);
    setContact(item.contact);
    setQrChange(null);
    setFileKey((k) => k + 1);
  };
  const preview =
    qrChange === null
      ? (value?.staticQrDataUrl ?? null)
      : qrChange === ""
        ? null
        : `data:image/png;base64,${qrChange}`;
  async function pick(file: File | undefined, input: HTMLInputElement) {
    if (!file) return;
    if (file.type !== "image/png" || file.size > MAX_QR_BYTES) {
      setError("Chọn ảnh QR dạng PNG, tối đa 80 KB.");
      input.value = "";
      return;
    }
    try {
      const data = await new Promise<string>((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(String(reader.result).split(",")[1]);
        reader.onerror = reject;
        reader.readAsDataURL(file);
      });
      setQrChange(data);
      setError("");
    } catch {
      setError("Không thể đọc ảnh.");
    }
  }
  useEffect(() => {
    api<Instructions>("/admin/payment-instructions")
      .then(apply)
      .catch((e: unknown) => {
        if (e instanceof ApiError && e.status === 401) onExpired();
        else setError("Không thể tải hướng dẫn thanh toán.");
      });
  }, [onExpired]);
  async function save(event: FormEvent) {
    event.preventDefault();
    if (!value) return;
    setBusy(true);
    setError("");
    setStatus("");
    try {
      apply(
        await api<Instructions>("/admin/payment-instructions", "PUT", {
          bankDetails: bank,
          contact,
          staticQrBase64: qrChange,
          version: value.version,
        }),
      );
      setStatus("Đã lưu hướng dẫn thanh toán.");
    } catch (e) {
      if (e instanceof ApiError && e.status === 401) onExpired();
      else setError(e instanceof Error ? e.message : "Không thể lưu.");
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="panel stack" aria-label="Hướng dẫn thanh toán">
      <p className="lead">
        Giáo viên chờ duyệt thấy hướng dẫn này để chuyển khoản phí dịch vụ và
        gửi mã giao dịch. Không có cổng thanh toán hay tự cấp quyền.
      </p>
      <article className="card">
        <h2>Thông tin chuyển khoản</h2>
        <form onSubmit={save} className="form-grid" aria-busy={busy}>
          <label className="full-row">
            Ngân hàng, số tài khoản, chủ tài khoản, nội dung chuyển khoản
            <textarea
              id="bank-details"
              maxLength={1000}
              required
              rows={5}
              value={bank}
              onChange={(e) => setBank(e.target.value)}
            />
          </label>
          <label className="full-row">
            Liên hệ hỗ trợ
            <input
              id="contact"
              maxLength={500}
              value={contact}
              onChange={(e) => setContact(e.target.value)}
            />
          </label>
          <label className="full-row">
            Mã QR chuyển khoản (PNG, tối đa 80 KB)
            <input
              key={fileKey}
              id="static-qr"
              type="file"
              accept="image/png"
              disabled={busy}
              onChange={(e) => void pick(e.target.files?.[0], e.target)}
            />
          </label>
          {preview && (
            <div className="full-row">
              <img
                className="payment-qr"
                src={preview}
                alt="Xem trước mã QR chuyển khoản"
              />
              <button
                type="button"
                disabled={busy}
                onClick={() => {
                  setQrChange("");
                  setFileKey((k) => k + 1);
                }}
              >
                Xóa mã QR
              </button>
            </div>
          )}
          {qrChange !== null && (
            <p className="muted full-row">
              {qrChange === ""
                ? "Mã QR sẽ bị xóa khi bạn lưu."
                : "Mã QR mới sẽ được lưu khi bạn bấm Lưu hướng dẫn."}
            </p>
          )}
          {value?.updatedAt && (
            <p className="muted full-row">
              Cập nhật lần cuối{" "}
              {new Date(value.updatedAt).toLocaleString("vi-VN", {
                timeZone: "Asia/Ho_Chi_Minh",
              })}
              {value.updatedBy && ` bởi ${value.updatedBy}`}
            </p>
          )}
          {status && (
            <p role="status" className="notice success full-row">
              {status}
            </p>
          )}
          {error && (
            <p role="alert" className="error full-row">
              {error}
            </p>
          )}
          <div className="row-actions full-row">
            <button className="primary" disabled={busy || !value}>
              Lưu hướng dẫn
            </button>
          </div>
        </form>
      </article>
    </section>
  );
}
