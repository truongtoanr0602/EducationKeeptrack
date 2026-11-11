import { useState, type FormEvent } from "react";
import { api, ApiError } from "./api";

export function Login({
  onLogin,
  onRegister,
  onForgot,
}: {
  onLogin: () => Promise<void>;
  onRegister?: () => void;
  onForgot?: () => void;
}) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [code, setCode] = useState("");
  const [needsCode, setNeedsCode] = useState(false);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent) {
    event.preventDefault();
    setError("");
    setBusy(true);
    try {
      await api("/auth/login", "POST", {
        email,
        password,
        code: needsCode ? code : undefined,
      });
      setPassword("");
      setCode("");
      await onLogin();
    } catch (e) {
      if (e instanceof ApiError && e.data.requiresTotp === true) {
        setNeedsCode(true);
        setCode("");
      }
      setError(e instanceof Error ? e.message : "Không thể đăng nhập.");
    } finally {
      setBusy(false);
    }
  }
  return (
    <main className="login">
      <section className="login-intro">
        <span className="eyebrow">EDUCATION KEEPTRACK</span>
        <h1>
          Mỗi lớp học,
          <br />
          một hành trình.
        </h1>
        <p>Không gian quản lý lớp học dành cho giáo viên và gia sư.</p>
        <div className="book-mark" aria-hidden="true">
          ▤
        </div>
      </section>
      <section className="login-form">
        <span className="eyebrow">CHÀO MỪNG TRỞ LẠI</span>
        <h2>Đăng nhập</h2>
        <p>Truy cập lớp học của bạn.</p>
        <form onSubmit={submit} aria-busy={busy}>
          <label htmlFor="email">Email</label>
          <input
            id="email"
            type="email"
            autoComplete="username"
            maxLength={256}
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            disabled={busy}
          />
          <label htmlFor="password">Mật khẩu</label>
          <input
            id="password"
            type="password"
            autoComplete="current-password"
            maxLength={1024}
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            disabled={busy}
          />
          {needsCode && (
            <>
              <label htmlFor="totp">Mã xác thực (6 số)</label>
              <input
                id="totp"
                inputMode="numeric"
                autoComplete="one-time-code"
                pattern="[0-9 ]{6,7}"
                maxLength={7}
                required
                value={code}
                onChange={(e) => setCode(e.target.value)}
                disabled={busy}
              />
            </>
          )}
          {error && (
            <p role="alert" className="error">
              {error}
            </p>
          )}
          <button disabled={busy} className="primary full">
            {busy ? (
              <>
                <span className="spinner" /> Đang đăng nhập…
              </>
            ) : (
              "Đăng nhập"
            )}
          </button>
        </form>
        <div className="login-links">
          {onRegister && (
            <button type="button" onClick={onRegister} disabled={busy}>
              Đăng ký tài khoản giáo viên
            </button>
          )}
          {onForgot && (
            <button type="button" onClick={onForgot} disabled={busy}>
              Quên mật khẩu?
            </button>
          )}
        </div>
      </section>
    </main>
  );
}
