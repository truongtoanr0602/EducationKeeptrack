import { useEffect, useRef, useState, type FormEvent } from "react";
import { api, ApiError } from "./api";

export type TokenLink = { action: string; token: string };

export function readTokenLink(): TokenLink | null {
  const params = new URLSearchParams(window.location.search);
  const action = params.get("action");
  const token = params.get("token");
  return action && token ? { action, token } : null;
}

export function clearTokenLink() {
  window.history.replaceState(null, "", window.location.pathname);
}

const titles: Record<string, string> = {
  "verify-email": "Xác minh email",
  "confirm-email": "Xác nhận email mới",
  "reset-password": "Đặt mật khẩu mới",
};
const autoPost: Record<string, { path: string; done: string }> = {
  "verify-email": {
    path: "/auth/verify-email",
    done: "Đã xác minh email. Đăng nhập để gửi thông tin đối chiếu thanh toán.",
  },
  "confirm-email": {
    path: "/auth/confirm-email",
    done: "Đã đổi email. Các phiên cũ đã bị thu hồi; vui lòng đăng nhập bằng email mới.",
  },
};

// Handles one-time links from email. The token is removed from the address bar once used.
export function TokenAction({
  link,
  onDone,
}: {
  link: TokenLink;
  onDone: (message: string) => void;
}) {
  const [error, setError] = useState("");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [busy, setBusy] = useState(false);
  const started = useRef(false);
  const auto = autoPost[link.action];
  useEffect(() => {
    if (!auto || started.current) return;
    started.current = true;
    api(auto.path, "POST", { token: link.token })
      .then(() => {
        clearTokenLink();
        onDone(auto.done);
      })
      .catch((e: unknown) => {
        clearTokenLink();
        setError(e instanceof Error ? e.message : "Liên kết không hợp lệ.");
      });
  }, [auto, link, onDone]);
  async function reset(event: FormEvent) {
    event.preventDefault();
    setError("");
    if (password !== confirm) {
      setError("Mật khẩu nhập lại không khớp.");
      return;
    }
    setBusy(true);
    try {
      await api("/auth/reset-password", "POST", {
        token: link.token,
        password,
      });
      clearTokenLink();
      onDone(
        "Đã đặt mật khẩu mới. Mọi phiên đăng nhập cũ đã bị thu hồi; vui lòng đăng nhập lại.",
      );
    } catch (e) {
      const details =
        e instanceof ApiError && e.errors
          ? Object.values(e.errors).flat().join(" ")
          : "";
      setError(
        details || (e instanceof Error ? e.message : "Không thể đặt lại."),
      );
    } finally {
      setBusy(false);
    }
  }
  return (
    <main className="login">
      <section className="login-form">
        <h2>{titles[link.action] ?? "Liên kết"}</h2>
        {link.action === "reset-password" ? (
          <form onSubmit={reset} aria-busy={busy}>
            <label htmlFor="new-password">Mật khẩu mới</label>
            <input
              id="new-password"
              type="password"
              autoComplete="new-password"
              maxLength={1024}
              required
              value={password}
              disabled={busy}
              onChange={(e) => setPassword(e.target.value)}
            />
            <label htmlFor="new-password-confirm">Nhập lại mật khẩu mới</label>
            <input
              id="new-password-confirm"
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
              {busy ? "Đang lưu…" : "Đặt mật khẩu mới"}
            </button>
            <button
              type="button"
              className="full"
              onClick={() => {
                clearTokenLink();
                onDone("");
              }}
            >
              Về trang đăng nhập
            </button>
          </form>
        ) : !auto ? (
          <>
            <p role="alert" className="error">
              Liên kết không được hỗ trợ.
            </p>
            <button
              className="primary full"
              onClick={() => {
                clearTokenLink();
                onDone("");
              }}
            >
              Về trang đăng nhập
            </button>
          </>
        ) : error ? (
          <>
            <p role="alert" className="error">
              {error}
            </p>
            <button className="primary full" onClick={() => onDone("")}>
              Về trang đăng nhập
            </button>
          </>
        ) : (
          <p role="status">Đang xử lý…</p>
        )}
      </section>
    </main>
  );
}
