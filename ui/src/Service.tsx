import { useCallback, useEffect, useState, type FormEvent } from "react";
import { api, ApiError, type Access } from "./api";
import { vnDateTime } from "./admin-api";
import { QuotaTable } from "./QuotaTable";
import {
  newKey,
  requestStatusLabels,
  type ServiceProfile,
} from "./service-api";

const statusText: Record<Access["status"], string> = {
  blocked: "Tài khoản đã bị khóa.",
  unverified:
    "Email chưa được xác minh. Mở liên kết trong email để tiếp tục đăng ký.",
  pending:
    "Tài khoản đang chờ Admin đối chiếu thanh toán và cấp quyền sử dụng. Bạn chưa thể dùng các chức năng lớp học.",
  rejected:
    "Yêu cầu gần nhất bị từ chối. Xem lý do bên dưới và gửi lại thông tin đối chiếu nếu cần.",
  active: "Quyền sử dụng đang còn hạn.",
  expired:
    "Quyền sử dụng đã hết hạn. Bạn vẫn xem và xuất được dữ liệu cũ nhưng không thể thêm hoặc sửa.",
};

// Teacher service profile: status, period, payment instructions and reconciliation requests.
export function Service({
  onExpired,
  onChanged,
}: {
  onExpired: () => void;
  onChanged?: (profile: ServiceProfile) => void;
}) {
  const [profile, setProfile] = useState<ServiceProfile | null>(null);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [code, setCode] = useState("");
  const [note, setNote] = useState("");
  const [key, setKey] = useState(newKey);
  const [busy, setBusy] = useState(false);
  const fail = useCallback(
    (e: unknown, fallback: string) => {
      if (e instanceof ApiError && e.status === 401) onExpired();
      else setError(e instanceof Error ? e.message : fallback);
    },
    [onExpired],
  );
  const load = useCallback(async () => {
    try {
      const value = await api<ServiceProfile>("/service");
      setProfile(value);
      onChanged?.(value);
    } catch (e) {
      fail(e, "Không thể tải thông tin dịch vụ.");
    }
  }, [fail, onChanged]);
  useEffect(() => {
    void load();
  }, [load]);
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
        "Đã gửi thông tin đối chiếu. Admin sẽ kiểm tra khoản chuyển và cấp quyền; việc gửi chưa có nghĩa là đã được duyệt.",
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
    ) : null;
  const open = profile.requests.some((r) => r.status === "submitted");
  const current = profile.periods.find((p) => p.current);
  const upcoming = profile.periods.filter((p) => p.upcoming);
  const canRequest = profile.status !== "unverified";
  return (
    <section className="class-card service-panel" aria-label="Quyền sử dụng">
      <span className="subject">QUYỀN SỬ DỤNG</span>
      <h2>{statusText[profile.status]}</h2>
      {current && (
        <p>
          Kỳ hiện tại: {vnDateTime(current.startsAt)} –{" "}
          {vnDateTime(current.endsAt)}
          {current.label && ` (${current.label})`}
        </p>
      )}
      {upcoming.map((p) => (
        <p key={p.id}>
          Kỳ đã gia hạn bắt đầu {vnDateTime(p.startsAt)}, kết thúc{" "}
          {vnDateTime(p.endsAt)}.
        </p>
      ))}
      {(profile.status === "active" || profile.status === "expired") && (
        <details>
          <summary>
            Trợ lý AI:{" "}
            {profile.ai.canUseAi ? "đang dùng được" : "không khả dụng"}
          </summary>
          {!profile.ai.canUseAi && (
            <p className="hint">
              {profile.ai.enabled
                ? "Kỳ sử dụng không còn hiệu lực nên không nhận yêu cầu AI mới."
                : "Tài khoản chưa được bật AI."}{" "}
              Bạn vẫn tự chấm, viết nhận xét và lập kế hoạch thủ công khi kỳ còn
              hạn; kết quả AI cũ được giữ nguyên.
            </p>
          )}
          <QuotaTable quotas={profile.ai.quotas} />
        </details>
      )}
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
      {profile.status === "unverified" && (
        <button disabled={busy} onClick={() => void resend()}>
          Gửi lại email xác minh
        </button>
      )}
      {canRequest && (
        <details
          open={profile.status === "pending" || profile.status === "rejected"}
        >
          <summary>Hướng dẫn thanh toán phí dịch vụ</summary>
          <p className="hint">
            Phí dịch vụ là khoản bạn trả để dùng phần mềm, tách riêng với học
            phí học sinh.
          </p>
          {profile.instructions.bankDetails ? (
            <pre className="instructions">
              {profile.instructions.bankDetails}
            </pre>
          ) : (
            <p>Admin chưa cấu hình hướng dẫn chuyển khoản.</p>
          )}
          {profile.instructions.contact && (
            <p>Liên hệ: {profile.instructions.contact}</p>
          )}
          {open ? (
            <p className="notice">Bạn có một yêu cầu đang chờ Admin xử lý.</p>
          ) : (
            <form onSubmit={submit} className="roster-form" aria-busy={busy}>
              <label htmlFor="transaction-code">
                Mã giao dịch chuyển khoản
              </label>
              <input
                id="transaction-code"
                required
                maxLength={100}
                value={code}
                disabled={busy}
                onChange={(e) => setCode(e.target.value)}
              />
              <label htmlFor="transaction-note">Ghi chú (tùy chọn)</label>
              <textarea
                id="transaction-note"
                maxLength={500}
                value={note}
                disabled={busy}
                onChange={(e) => setNote(e.target.value)}
              />
              <button className="primary" disabled={busy}>
                {busy ? "Đang gửi…" : "Gửi đối chiếu"}
              </button>
            </form>
          )}
          {profile.requests.length > 0 && (
            <ul className="request-list" aria-label="Lịch sử yêu cầu">
              {profile.requests.map((r) => (
                <li key={r.id}>
                  <strong>{r.transactionCode}</strong> ·{" "}
                  {vnDateTime(r.submittedAt)} · {requestStatusLabels[r.status]}
                  {r.decisionReason && <> — Lý do: {r.decisionReason}</>}
                </li>
              ))}
            </ul>
          )}
        </details>
      )}
    </section>
  );
}
