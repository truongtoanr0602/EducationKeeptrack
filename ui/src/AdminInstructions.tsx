import { useEffect, useState, type FormEvent } from "react";
import { api, ApiError } from "./api";
import type { Instructions } from "./admin-api";

export function AdminInstructions({ onExpired }: { onExpired: () => void }) {
  const [value, setValue] = useState<Instructions | null>(null);
  const [bank, setBank] = useState("");
  const [contact, setContact] = useState("");
  const [status, setStatus] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const apply = (item: Instructions) => {
    setValue(item);
    setBank(item.bankDetails);
    setContact(item.contact);
  };
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
