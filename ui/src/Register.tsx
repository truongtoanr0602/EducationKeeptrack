import { useState, type FormEvent } from "react";
import { api, ApiError } from "./api";

// Public sign-up always creates a teacher account that must verify email and await approval.
export function Register({ onBack }: { onBack: () => void }) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState("");
  const [done, setDone] = useState("");
  const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent) {
    event.preventDefault();
    setError("");
    if (password !== confirm) {
      setError("Mật khẩu nhập lại không khớp.");
      return;
    }
    setBusy(true);
    try {
      const result = await api<{ message: string }>("/auth/register", "POST", {
        email,
        password,
      });
      setPassword("");
      setConfirm("");
      setDone(result.message);
    } catch (e) {
      const details =
        e instanceof ApiError && e.errors
          ? Object.values(e.errors).flat().join(" ")
          : "";
      setError(
        details || (e instanceof Error ? e.message : "Không thể đăng ký."),
      );
    } finally {
      setBusy(false);
    }
  }
  return (
    <main className="login">
      <section className="login-intro">
        <span className="eyebrow">EDUCATION KEEPTRACK</span>
        <h1>Đăng ký tài khoản giáo viên</h1>
        <p>
          Sau khi xác minh email, gửi thông tin chuyển khoản phí dịch vụ để
          Admin đối chiếu và cấp quyền sử dụng.
        </p>
      </section>
      <section className="login-form">
        <h2>Đăng ký</h2>
        {done ? (
          <>
            <p role="status" className="notice success">
              {done}
            </p>
            <button className="primary full" onClick={onBack}>
              Về trang đăng nhập
            </button>
          </>
        ) : (
          <form onSubmit={submit} aria-busy={busy}>
            <label htmlFor="register-email">Email</label>
            <input
              id="register-email"
              type="email"
              autoComplete="email"
              maxLength={256}
              required
              value={email}
              disabled={busy}
              onChange={(e) => setEmail(e.target.value)}
            />
            <label htmlFor="register-password">Mật khẩu</label>
            <input
              id="register-password"
              type="password"
              autoComplete="new-password"
              maxLength={1024}
              required
              value={password}
              disabled={busy}
              onChange={(e) => setPassword(e.target.value)}
            />
            <label htmlFor="register-confirm">Nhập lại mật khẩu</label>
            <input
              id="register-confirm"
              type="password"
              autoComplete="new-password"
              maxLength={1024}
              required
              value={confirm}
              disabled={busy}
              onChange={(e) => setConfirm(e.target.value)}
            />
            {error && (
              <p role="alert" className="error">
                {error}
              </p>
            )}
            <button className="primary full" disabled={busy}>
              {busy ? "Đang gửi…" : "Đăng ký"}
            </button>
            <button type="button" className="full" onClick={onBack}>
              Đã có tài khoản? Đăng nhập
            </button>
          </form>
        )}
      </section>
    </main>
  );
}
