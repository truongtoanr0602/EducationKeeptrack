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
    <details className="class-card">
      <summary>Hướng dẫn thanh toán phí dịch vụ</summary>
      <form onSubmit={save} className="roster-form" aria-busy={busy}>
        <label htmlFor="bank-details">Thông tin chuyển khoản</label>
        <textarea
          id="bank-details"
          maxLength={1000}
          required
          value={bank}
          onChange={(e) => setBank(e.target.value)}
        />
        <label htmlFor="contact">Liên hệ hỗ trợ</label>
        <input
          id="contact"
          maxLength={500}
          value={contact}
          onChange={(e) => setContact(e.target.value)}
        />
        {status && (
          <p role="status" className="notice success">
            {status}
          </p>
        )}
        {error && (
          <p role="alert" className="error">
            {error}
          </p>
        )}
        <button className="primary" disabled={busy || !value}>
          Lưu hướng dẫn
        </button>
      </form>
    </details>
  );
}
