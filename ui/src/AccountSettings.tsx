import { useState, type FormEvent } from "react";
import { api, ApiError } from "./api";

export function ForgotPassword({ onBack }: { onBack: () => void }) {
  const [email, setEmail] = useState("");
  const [done, setDone] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      const result = await api<{ message: string }>(
        "/auth/forgot-password",
        "POST",
        { email },
      );
      setDone(result.message);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Không thể gửi yêu cầu.");
    } finally {
      setBusy(false);
    }
  }
  return (
    <main className="login">
      <section className="login-form">
        <h2>Quên mật khẩu</h2>
        {done ? (
          <p role="status" className="notice success">
            {done}
          </p>
        ) : (
          <form onSubmit={submit} aria-busy={busy}>
            <label htmlFor="forgot-email">Email đăng nhập</label>
            <input
              id="forgot-email"
              type="email"
              maxLength={256}
              required
              value={email}
              disabled={busy}
              onChange={(e) => setEmail(e.target.value)}
            />
            {error && (
              <p role="alert" className="error">
                {error}
              </p>
            )}
            <button className="primary full" disabled={busy}>
              Gửi liên kết đặt lại
            </button>
          </form>
        )}
        <button type="button" className="full" onClick={onBack}>
          Về trang đăng nhập
        </button>
      </section>
    </main>
  );
}

// Changing email keeps the same account, period and quota; it applies only after the new address is verified.
export function ChangeEmail({ onExpired }: { onExpired: () => void }) {
  const [newEmail, setNewEmail] = useState("");
  const [password, setPassword] = useState("");
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    setMessage("");
    try {
      const result = await api<{ message: string }>(
        "/auth/change-email",
        "POST",
        { newEmail, currentPassword: password },
      );
      setPassword("");
      setMessage(result.message);
    } catch (e) {
      if (e instanceof ApiError && e.status === 401) onExpired();
      else {
        const details =
          e instanceof ApiError && e.errors
            ? Object.values(e.errors).flat().join(" ")
            : "";
        setError(
          details || (e instanceof Error ? e.message : "Không thể gửi."),
        );
      }
    } finally {
      setBusy(false);
    }
  }
  return (
    <article className="card">
      <h2>Đổi email đăng nhập</h2>
      <p className="muted">
        Email chỉ đổi sau khi bạn xác minh địa chỉ mới. Tài khoản, kỳ sử dụng và
        hạn mức được giữ nguyên; các phiên đăng nhập cũ sẽ bị thu hồi.
      </p>
      <form onSubmit={submit} className="form-grid" aria-busy={busy}>
        <label>
          Email mới
          <input
            id="change-email"
            type="email"
            maxLength={256}
            required
            value={newEmail}
            disabled={busy}
            onChange={(e) => setNewEmail(e.target.value)}
          />
        </label>
        <label>
          Mật khẩu hiện tại
          <input
            id="change-email-password"
            type="password"
            autoComplete="current-password"
            maxLength={1024}
            required
            value={password}
            disabled={busy}
            onChange={(e) => setPassword(e.target.value)}
          />
        </label>
        {message && (
          <p role="status" className="notice success full-row">
            {message}
          </p>
        )}
        {error && (
          <p role="alert" className="error full-row">
            {error}
          </p>
        )}
        <div className="row-actions full-row">
          <button className="primary" disabled={busy}>
            Gửi xác minh email mới
          </button>
        </div>
      </form>
    </article>
  );
}
