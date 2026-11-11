import { useCallback, useEffect, useState, type FormEvent } from "react";
import { api, ApiError } from "./api";
import {
  statusLabels,
  vnDateTime,
  type TeacherDetailView,
  type AdminRequest,
} from "./admin-api";
import { requestStatusLabels } from "./service-api";

export function TeacherDetail({
  id,
  onClose,
  onChanged,
  onExpired,
}: {
  id: string;
  onClose: () => void;
  onChanged: () => void;
  onExpired: () => void;
}) {
  const [teacher, setTeacher] = useState<TeacherDetailView | null>(null);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const fail = useCallback(
    (e: unknown, fallback: string) => {
      if (e instanceof ApiError && e.status === 401) onExpired();
      else setError(e instanceof Error ? e.message : fallback);
    },
    [onExpired],
  );
  const load = useCallback(async () => {
    try {
      setTeacher(await api<TeacherDetailView>(`/admin/teachers/${id}`));
    } catch (e) {
      fail(e, "Không thể tải hồ sơ.");
    }
  }, [id, fail]);
  useEffect(() => {
    void load();
  }, [load]);
  const done = useCallback(
    async (text: string) => {
      setError("");
      setMessage(text);
      await load();
      onChanged();
    },
    [load, onChanged],
  );
  const context = { teacher, fail, done, setError, setMessage };
  return (
    <section className="class-card admin-detail" aria-label="Hồ sơ giáo viên">
      <div className="dialog-top">
        <h2>{teacher?.email ?? "Hồ sơ giáo viên"}</h2>
        <button onClick={onClose}>Đóng</button>
      </div>
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
      {teacher && (
        <>
          <dl>
            <dt>Email</dt>
            <dd>
              {teacher.email} ·{" "}
              {teacher.emailConfirmed ? "đã xác minh" : "chưa xác minh"}
            </dd>
            <dt>Trạng thái</dt>
            <dd>
              {teacher.isBlocked ? "Đã khóa" : statusLabels[teacher.status]}
            </dd>
            <dt>Đăng ký lúc</dt>
            <dd>{vnDateTime(teacher.registeredAt)}</dd>
          </dl>
          <h3>Yêu cầu đối chiếu thanh toán dịch vụ</h3>
          {teacher.requests.length === 0 && <p>Chưa có yêu cầu.</p>}
          {teacher.requests.map((r) => (
            <RequestCard key={r.id} request={r} {...context} />
          ))}
          <h3>Kỳ quyền sử dụng</h3>
          {teacher.periods.length === 0 ? (
            <p>Chưa được cấp kỳ nào.</p>
          ) : (
            <ul className="request-list">
              {teacher.periods.map((p) => (
                <li key={p.id}>
                  {vnDateTime(p.startsAt)} – {vnDateTime(p.endsAt)}
                  {p.label && ` · ${p.label}`}
                  {p.current && " · đang hiệu lực"}
                  {p.upcoming && " · sắp tới"}
                </li>
              ))}
            </ul>
          )}
          <h3>Lịch sử thao tác</h3>
          <ul className="request-list" aria-label="Lịch sử thao tác">
            {teacher.history.map((h, index) => (
              <li key={index}>
                {vnDateTime(h.at)} · {h.actor} · {h.action}
                {h.reason && ` — ${h.reason}`}
              </li>
            ))}
          </ul>
        </>
      )}
    </section>
  );
}

type Context = {
  teacher: TeacherDetailView | null;
  fail: (e: unknown, fallback: string) => void;
  done: (text: string) => Promise<void>;
  setError: (value: string) => void;
};

function RequestCard({
  request,
  teacher,
  fail,
  done,
}: Context & { request: AdminRequest }) {
  const [note, setNote] = useState(request.verificationNote ?? "");
  const [days, setDays] = useState("");
  const [label, setLabel] = useState("");
  const [reason, setReason] = useState("");
  const [preview, setPreview] = useState<{
    startsAt: string;
    endsAt: string;
  } | null>(null);
  const [busy, setBusy] = useState(false);
  async function run(action: () => Promise<void>, fallback: string) {
    setBusy(true);
    try {
      await action();
    } catch (e) {
      fail(e, fallback);
    } finally {
      setBusy(false);
    }
  }
  const verify = (event: FormEvent) => {
    event.preventDefault();
    void run(async () => {
      await api(`/admin/payment-requests/${request.id}/verification`, "POST", {
        note,
        version: request.version,
      });
      await done("Đã ghi thông tin đối chiếu.");
    }, "Không thể lưu thông tin đối chiếu.");
  };
  const showPreview = (event: FormEvent) => {
    event.preventDefault();
    void run(async () => {
      setPreview(
        await api(`/admin/teachers/${teacher!.id}/period-preview`, "POST", {
          days: Number(days),
        }),
      );
    }, "Không thể xem trước kỳ.");
  };
  const approve = () =>
    void run(async () => {
      try {
        await api(`/admin/payment-requests/${request.id}/approve`, "POST", {
          days: Number(days),
          label,
          expectedStartsAt: preview!.startsAt,
          version: request.version,
        });
        await done("Đã duyệt và cấp kỳ quyền sử dụng.");
      } catch (e) {
        setPreview(null);
        throw e;
      }
    }, "Không thể duyệt yêu cầu.");
  const reject = (event: FormEvent) => {
    event.preventDefault();
    void run(async () => {
      await api(`/admin/payment-requests/${request.id}/reject`, "POST", {
        reason,
        version: request.version,
      });
      await done("Đã từ chối yêu cầu. Tài khoản vẫn được giữ.");
    }, "Không thể từ chối yêu cầu.");
  };
  return (
    <article className="request-card" aria-busy={busy}>
      <p>
        <strong>{request.transactionCode}</strong> ·{" "}
        {vnDateTime(request.submittedAt)} ·{" "}
        {requestStatusLabels[request.status]}
      </p>
      {request.note && <p>Ghi chú giáo viên: {request.note}</p>}
      {request.verificationNote && (
        <p>Thông tin đối chiếu: {request.verificationNote}</p>
      )}
      {request.decisionReason && <p>Lý do: {request.decisionReason}</p>}
      {request.status === "submitted" && (
        <div className="roster-form">
          <form onSubmit={verify}>
            <label htmlFor={`verify-${request.id}`}>
              Ghi thông tin đối chiếu (số tiền, thời điểm thấy trong sao kê…)
            </label>
            <textarea
              id={`verify-${request.id}`}
              maxLength={500}
              required
              value={note}
              onChange={(e) => setNote(e.target.value)}
            />
            <button disabled={busy}>Lưu thông tin đối chiếu</button>
          </form>
          <form onSubmit={showPreview}>
            <label htmlFor={`days-${request.id}`}>Số ngày của kỳ</label>
            <input
              id={`days-${request.id}`}
              type="number"
              min={1}
              max={3660}
              required
              value={days}
              onChange={(e) => {
                setDays(e.target.value);
                setPreview(null);
              }}
            />
            <label htmlFor={`label-${request.id}`}>Tên gói (tùy chọn)</label>
            <input
              id={`label-${request.id}`}
              maxLength={100}
              value={label}
              onChange={(e) => setLabel(e.target.value)}
            />
            <button disabled={busy}>Xem trước kỳ</button>
          </form>
          {preview && (
            <p className="notice">
              Kỳ sẽ bắt đầu {vnDateTime(preview.startsAt)} và kết thúc{" "}
              {vnDateTime(preview.endsAt)} (giờ Việt Nam).{" "}
              <button className="primary" disabled={busy} onClick={approve}>
                Duyệt và cấp kỳ
              </button>
            </p>
          )}
          <form onSubmit={reject}>
            <label htmlFor={`reject-${request.id}`}>Lý do từ chối</label>
            <input
              id={`reject-${request.id}`}
              maxLength={500}
              required
              value={reason}
              onChange={(e) => setReason(e.target.value)}
            />
            <button disabled={busy}>Từ chối</button>
          </form>
        </div>
      )}
    </article>
  );
}
