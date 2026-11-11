import { useEffect, useRef, useState } from "react";
import { api } from "./api";

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

// Handles one-time links from email. The token is removed from the address bar once used.
export function TokenAction({
  link,
  onDone,
}: {
  link: TokenLink;
  onDone: (message: string) => void;
}) {
  const [error, setError] = useState("");
  const started = useRef(false);
  useEffect(() => {
    if (link.action !== "verify-email" || started.current) return;
    started.current = true;
    api("/auth/verify-email", "POST", { token: link.token })
      .then(() => {
        clearTokenLink();
        onDone(
          "Đã xác minh email. Đăng nhập để gửi thông tin đối chiếu thanh toán.",
        );
      })
      .catch((e: unknown) => {
        clearTokenLink();
        setError(e instanceof Error ? e.message : "Liên kết không hợp lệ.");
      });
  }, [link, onDone]);
  return (
    <main className="login">
      <section className="login-form">
        <h2>Xác minh email</h2>
        {error ? (
          <>
            <p role="alert" className="error">
              {error}
            </p>
            <button className="primary full" onClick={() => onDone("")}>
              Về trang đăng nhập
            </button>
          </>
        ) : (
          <p role="status">Đang xác minh…</p>
        )}
      </section>
    </main>
  );
}
