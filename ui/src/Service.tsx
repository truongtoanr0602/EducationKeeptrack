import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type FormEvent,
} from "react";
import { api, ApiError, type Access } from "./api";
import { vnDateTime } from "./admin-api";
import { QuotaTable } from "./QuotaTable";
import { ChangeEmail } from "./AccountSettings";
import { href } from "./router";
import {
  newKey,
  requestStatusLabels,
  type ServiceProfile,
} from "./service-api";

export const statusText: Record<Access["status"], string> = {
  blocked: "Tài khoản đã bị khóa.",
  unverified:
    "Email chưa được xác minh. Mở liên kết trong email để tiếp tục đăng ký.",
  pending:
    "Đang chờ Admin đối chiếu thanh toán và cấp quyền sử dụng. Bạn chưa dùng được các chức năng lớp học.",
  rejected:
    "Yêu cầu gần nhất bị từ chối. Xem lý do bên dưới và gửi lại thông tin đối chiếu nếu cần.",
  active: "Quyền sử dụng đang còn hạn.",
  expired:
    "Quyền sử dụng đã hết hạn. Bạn vẫn xem và xuất được dữ liệu cũ nhưng không thể thêm hoặc sửa.",
};
export const statusBadge: Record<Access["status"], string> = {
  blocked: "Đã khóa",
  unverified: "Chưa xác minh",
  pending: "Chờ duyệt",
  rejected: "Bị từ chối",
  active: "Còn hạn",
  expired: "Hết hạn",
};
export const badgeClass: Record<Access["status"], string> = {
  blocked: "not_mastered",
  unverified: "insufficient",
  pending: "insufficient",
  rejected: "not_mastered",
  active: "mastered",
  expired: "insufficient",
};

export type Milestone = 1 | 2 | 3;
export const milestones = [
  "Xác minh email",
  "Chuyển phí dịch vụ và gửi mã giao dịch",
  "Chờ Admin duyệt",
] as const;
export const POLL_MS = 30_000;

/** Which of the three steps a teacher who cannot use the app yet is on; null once access was granted (or blocked). */
export function currentMilestone(
  status: Access["status"],
  hasOpenRequest: boolean,
): Milestone | null {
  if (status === "unverified") return 1;
  if (status === "pending" || status === "rejected")
    return hasOpenRequest ? 3 : 2;
  return null;
}

// Banner shown in the workspace while the account cannot write; links to the account screen.
export function ServiceNotice({ access }: { access: Access }) {
  if (access.canWrite) return null;
  return (
    <p className="notice">
      {statusText[access.status]}{" "}
      <a href={href({ name: "account" })}>Xem tài khoản & dịch vụ</a>
    </p>
  );
}

// Teacher service profile: status, period, AI quota, payment reconciliation and login email.
export function AccountScreen({
  onExpired,
  onChanged,
  onActivated,
}: {
  onExpired: () => void;
  onChanged?: (profile: ServiceProfile) => void;
  /** Access was granted while this page was open and waiting. */
  onActivated?: () => void;
}) {
  const [profile, setProfile] = useState<ServiceProfile | null>(null);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [code, setCode] = useState("");
  const [note, setNote] = useState("");
  const [key, setKey] = useState(newKey);
  const [busy, setBusy] = useState(false);
  const codeInput = useRef<HTMLInputElement>(null);
  const waiting = useRef(false);
  const fail = useCallback(
    (e: unknown, fallback: string) => {
      if (e instanceof ApiError && e.status === 401) onExpired();
      else setError(e instanceof Error ? e.message : fallback);
    },
    [onExpired],
  );
  // `quiet` is for the background check: a failed round trip is not worth an error message, only a lost session is.
  const load = useCallback(
    async (quiet = false) => {
      try {
        const value = await api<ServiceProfile>("/service");
        const was = waiting.current;
        waiting.current = currentMilestone(value.status, false) !== null;
        setProfile(value);
        onChanged?.(value);
        if (was && value.status === "active") onActivated?.();
      } catch (e) {
        if (quiet && !(e instanceof ApiError && e.status === 401)) return;
        fail(e, "Không thể tải thông tin dịch vụ.");
      }
    },
    [fail, onChanged, onActivated],
  );
  useEffect(() => {
    void load();
  }, [load]);
  // While approval is pending the page checks again every 30 seconds and when the tab comes back to the front.
  const stillWaiting =
    !!profile && currentMilestone(profile.status, false) !== null;
  useEffect(() => {
    if (!stillWaiting) return;
    const check = () => {
      if (document.visibilityState === "visible") void load(true);
    };
    const timer = window.setInterval(check, POLL_MS);
    document.addEventListener("visibilitychange", check);
    return () => {
      window.clearInterval(timer);
      document.removeEventListener("visibilitychange", check);
    };
  }, [stillWaiting, load]);
  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    setMessage("");
    try {
      await api("/service/payment-requests", "POST", {
        transactionCode: code,
        note,
        idempotencyKey: key,
      });
      setCode("");
      setNote("");
      setKey(newKey());
      setMessage(
        "Đã gửi thông tin đối chiếu. Admin sẽ kiểm tra khoản chuyển rồi cấp quyền; gửi chưa có nghĩa là đã được duyệt.",
      );
      await load();
    } catch (e) {
      fail(e, "Không thể gửi yêu cầu.");
    } finally {
      setBusy(false);
    }
  }
  async function resend() {
    setBusy(true);
    setError("");
    try {
      await api("/auth/resend-verification", "POST", {});
      setMessage("Đã gửi lại email xác minh.");
    } catch (e) {
      fail(e, "Không thể gửi lại email.");
    } finally {
      setBusy(false);
    }
  }
  if (!profile)
    return error ? (
      <p role="alert" className="error">
        {error}
      </p>
    ) : (
      <p role="status">Đang tải…</p>
    );
  const open = profile.requests.some((r) => r.status === "submitted");
  const current = profile.periods.find((p) => p.current);
  const upcoming = profile.periods.filter((p) => p.upcoming);
  const showAi = profile.status === "active" || profile.status === "expired";
  const milestone = currentMilestone(profile.status, open);
  return (
    <section className="panel stack" aria-label="Tài khoản và dịch vụ">
      {message && (
        <p role="status" className="notice success">
          {message}
        </p>
      )}
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      <article className="card">
        <div className="card-heading">
          <h2>Quyền sử dụng</h2>
          <span className={`state ${badgeClass[profile.status]}`}>
            {statusBadge[profile.status]}
          </span>
        </div>
        <p>{statusText[profile.status]}</p>
        {current && (
          <p>
            Kỳ hiện tại: <strong>{vnDateTime(current.startsAt)}</strong> –{" "}
            <strong>{vnDateTime(current.endsAt)}</strong>
            {current.label && ` · ${current.label}`}
          </p>
        )}
        {upcoming.map((p) => (
          <p key={p.id} className="muted">
            Kỳ đã gia hạn: {vnDateTime(p.startsAt)} – {vnDateTime(p.endsAt)}
            {p.label && ` · ${p.label}`}
          </p>
        ))}
      </article>
      {milestone && (
        <article className="card" aria-label="Tiến trình kích hoạt">
          <h2>Các bước để bắt đầu</h2>
          <ol className="service-steps">
            {milestones.map((text, index) => {
              const n = (index + 1) as Milestone;
              const state =
                n < milestone ? "done" : n === milestone ? "current" : "todo";
              return (
                <li
                  key={text}
                  className={state}
                  aria-current={state === "current" ? "step" : undefined}
                >
                  <span className="mark" aria-hidden="true">
                    {state === "done" ? "✓" : n}
                  </span>
                  <div>
                    <strong>{text}</strong>
                    <span className="sr-only">
                      {state === "done"
                        ? " (đã xong)"
                        : state === "current"
                          ? " (bước hiện tại)"
                          : " (chưa tới)"}
                    </span>
                    {state === "current" && (
                      <div className="row-actions">
                        {n === 1 && (
                          <button
                            className="primary"
                            disabled={busy}
                            onClick={() => void resend()}
                          >
                            Gửi lại email xác minh
                          </button>
                        )}
                        {n === 2 && (
                          <button
                            className="primary"
                            onClick={() => codeInput.current?.focus()}
                          >
                            {profile.status === "rejected"
                              ? "Gửi lại thông tin đối chiếu"
                              : "Nhập mã giao dịch"}
                          </button>
                        )}
                        {n === 3 && (
                          <>
                            <button
                              className="primary"
                              onClick={() => void load()}
                            >
                              Kiểm tra lại
                            </button>
                            <span className="muted">
                              Trang tự cập nhật mỗi 30 giây.
                            </span>
                          </>
                        )}
                      </div>
                    )}
                  </div>
                </li>
              );
            })}
          </ol>
        </article>
      )}
      {showAi && (
        <article className="card">
          <div className="card-heading">
            <h2>Trợ lý AI</h2>
            <span
              className={`state ${profile.ai.canUseAi ? "mastered" : "insufficient"}`}
            >
              {profile.ai.canUseAi ? "Dùng được" : "Không khả dụng"}
            </span>
          </div>
          {!profile.ai.canUseAi && (
            <p className="muted">
              {profile.ai.enabled
                ? "Kỳ sử dụng không còn hiệu lực nên không nhận yêu cầu AI mới."
                : "Tài khoản chưa được bật AI."}{" "}
              Bạn vẫn tự chấm, viết nhận xét và lập kế hoạch thủ công khi kỳ còn
              hạn; kết quả AI cũ được giữ nguyên.
            </p>
          )}
          <QuotaTable quotas={profile.ai.quotas} />
        </article>
      )}
      {profile.status !== "unverified" && (
        <article className="card">
          <h2>Thanh toán phí dịch vụ</h2>
          <p className="muted">
            Phí dịch vụ là khoản bạn trả để dùng phần mềm, tách riêng với học
            phí học sinh.
          </p>
          {profile.instructions.bankDetails ? (
            <p className="pre">{profile.instructions.bankDetails}</p>
          ) : (
            <p className="muted">Admin chưa cấu hình hướng dẫn chuyển khoản.</p>
          )}
          {profile.instructions.contact && (
            <p>Liên hệ: {profile.instructions.contact}</p>
          )}
          {open ? (
            <p className="notice">Bạn có một yêu cầu đang chờ Admin xử lý.</p>
          ) : (
            <form onSubmit={submit} className="form-grid" aria-busy={busy}>
              <label>
                Mã giao dịch chuyển khoản
                <input
                  ref={codeInput}
                  required
                  maxLength={100}
                  value={code}
                  disabled={busy}
                  onChange={(e) => setCode(e.target.value)}
                />
              </label>
              <label>
                Ghi chú (tùy chọn)
                <input
                  maxLength={500}
                  value={note}
                  disabled={busy}
                  onChange={(e) => setNote(e.target.value)}
                />
              </label>
              <div className="row-actions full-row">
                <button className="primary" disabled={busy}>
                  {busy ? "Đang gửi…" : "Gửi đối chiếu"}
                </button>
              </div>
            </form>
          )}
          {profile.requests.length > 0 && (
            <div className="table-wrap">
              <table className="data-table" aria-label="Lịch sử yêu cầu">
                <thead>
                  <tr>
                    <th>Mã giao dịch</th>
                    <th>Gửi lúc</th>
                    <th>Trạng thái</th>
                    <th>Lý do</th>
                  </tr>
                </thead>
                <tbody>
                  {profile.requests.map((r) => (
                    <tr key={r.id}>
                      <td>{r.transactionCode}</td>
                      <td>{vnDateTime(r.submittedAt)}</td>
                      <td>{requestStatusLabels[r.status]}</td>
                      <td>{r.decisionReason ?? ""}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </article>
      )}
      <ChangeEmail onExpired={onExpired} />
    </section>
  );
}
