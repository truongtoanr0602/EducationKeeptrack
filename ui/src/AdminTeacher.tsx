import { useCallback, useEffect, useState, type FormEvent } from "react";
import { api, ApiError } from "./api";
import {
  vnDateTime,
  type TeacherDetailView,
  type AdminRequest,
} from "./admin-api";
import { requestStatusLabels } from "./service-api";
import { AdminPeriods } from "./AdminPeriods";
import { AdminAi } from "./AdminAi";
import { QuotaInputs, quotaBody } from "./QuotaTable";
import { badgeClass, statusBadge } from "./Service";

const actionLabels: Record<string, string> = {
  "request.verified": "Ghi thông tin đối chiếu",
  "request.approved": "Duyệt yêu cầu, cấp kỳ",
  "request.rejected": "Từ chối yêu cầu",
  "period.granted": "Cấp/gia hạn kỳ",
  "period.updated": "Sửa kỳ",
  "account.locked": "Khóa tài khoản",
  "account.unlocked": "Mở khóa tài khoản",
  "ai.enabled": "Bật AI",
  "ai.disabled": "Tắt AI",
  "quota.updated": "Điều chỉnh hạn mức",
  "instructions.updated": "Cập nhật hướng dẫn thanh toán",
};

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
      setMessage("");
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
  const status = teacher
    ? teacher.isBlocked
      ? "blocked"
      : teacher.status
    : null;
  return (
    <section className="panel stack" aria-label="Hồ sơ giáo viên">
      <div className="row-actions">
        <button onClick={onClose}>← Về danh sách</button>
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
      {!teacher && !error && <p role="status">Đang tải…</p>}
      {teacher && status && (
        <>
          <article className="card">
            <div className="card-heading">
              <h2>{teacher.email}</h2>
              <span className={`state ${badgeClass[status]}`}>
                {statusBadge[status]}
              </span>
            </div>
            <p className="muted">
              {teacher.emailConfirmed
                ? "Đã xác minh email"
                : "Chưa xác minh email"}{" "}
              · Đăng ký lúc {vnDateTime(teacher.registeredAt)}
            </p>
          </article>
          <article className="card">
            <h2>Yêu cầu đối chiếu thanh toán dịch vụ</h2>
            {teacher.requests.length === 0 ? (
              <p className="muted">Chưa có yêu cầu.</p>
            ) : (
              <ul className="item-list">
                {teacher.requests.map((r) => (
                  <RequestItem
                    key={r.id}
                    request={r}
                    teacher={teacher}
                    fail={fail}
                    done={done}
                  />
                ))}
              </ul>
            )}
          </article>
          <article className="card">
            <h2>Kỳ quyền sử dụng</h2>
            {teacher.periods.length === 0 ? (
              <p className="muted">Chưa được cấp kỳ nào.</p>
            ) : (
              <div className="table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Bắt đầu</th>
                      <th>Kết thúc</th>
                      <th>Gói</th>
                      <th>Hạn mức (chấm/nhận xét/kế hoạch)</th>
                      <th>Trạng thái</th>
                    </tr>
                  </thead>
                  <tbody>
                    {teacher.periods.map((p) => (
                      <tr key={p.id}>
                        <td>{vnDateTime(p.startsAt)}</td>
                        <td>{vnDateTime(p.endsAt)}</td>
                        <td>{p.label}</td>
                        <td>
                          {[p.gradingQuota, p.commentQuota, p.planningQuota]
                            .map((q) => q ?? "–")
                            .join(" / ")}
                        </td>
                        <td>
                          {p.current ? (
                            <span className="state mastered">
                              Đang hiệu lực
                            </span>
                          ) : p.upcoming ? (
                            <span className="state insufficient">Sắp tới</span>
                          ) : (
                            <span className="state">Đã qua</span>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            <AdminPeriods
              key={`${teacher.periods.length}-${teacher.isBlocked}-${teacher.periods.at(-1)?.version ?? 0}`}
              teacher={teacher}
              fail={fail}
              done={done}
            />
          </article>
          <article className="card">
            <h2>Quyền AI và hạn mức</h2>
            <AdminAi
              key={`${teacher.aiEnabled}-${teacher.periods.find((p) => p.current)?.version ?? 0}`}
              teacher={teacher}
              fail={fail}
              done={done}
            />
          </article>
          <article className="card">
            <h2>Lịch sử thao tác</h2>
            {teacher.history.length === 0 ? (
              <p className="muted">Chưa có thao tác.</p>
            ) : (
              <div className="table-wrap">
                <table className="data-table" aria-label="Lịch sử thao tác">
                  <thead>
                    <tr>
                      <th>Thời điểm</th>
                      <th>Người thực hiện</th>
                      <th>Thao tác</th>
                      <th>Lý do</th>
                    </tr>
                  </thead>
                  <tbody>
                    {teacher.history.map((h, index) => (
                      <tr key={index}>
                        <td>{vnDateTime(h.at)}</td>
                        <td>{h.actor}</td>
                        <td>{actionLabels[h.action] ?? h.action}</td>
                        <td>{h.reason ?? ""}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </article>
        </>
      )}
    </section>
  );
}

function RequestItem({
  request,
  teacher,
  fail,
  done,
}: {
  request: AdminRequest;
  teacher: TeacherDetailView;
  fail: (e: unknown, fallback: string) => void;
  done: (text: string) => Promise<void>;
}) {
  const [note, setNote] = useState(request.verificationNote ?? "");
  const [days, setDays] = useState("");
  const [label, setLabel] = useState("");
  const [reason, setReason] = useState("");
  const [quotas, setQuotas] = useState<Record<string, string>>({});
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
        await api(`/admin/teachers/${teacher.id}/period-preview`, "POST", {
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
          ...quotaBody(quotas),
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
  const open = request.status === "submitted";
  return (
    <li className="item request-item" aria-busy={busy}>
      <div>
        <h3>
          {request.transactionCode}{" "}
          <span
            className={`state ${open ? "insufficient" : request.status === "approved" ? "mastered" : "not_mastered"}`}
          >
            {requestStatusLabels[request.status]}
          </span>
        </h3>
        <p className="muted">
          Gửi lúc {vnDateTime(request.submittedAt)}
          {request.decidedAt &&
            ` · Xử lý lúc ${vnDateTime(request.decidedAt)} bởi ${request.decidedBy ?? ""}`}
        </p>
        {request.note && <p>Ghi chú giáo viên: {request.note}</p>}
        {request.verificationNote && (
          <p>Thông tin đối chiếu: {request.verificationNote}</p>
        )}
        {request.decisionReason && <p>Lý do: {request.decisionReason}</p>}
        {open && (
          <div className="stack">
            <form onSubmit={verify} className="form-grid">
              <label className="full-row">
                Ghi thông tin đối chiếu (số tiền, thời điểm thấy trong sao kê…)
                <textarea
                  maxLength={500}
                  required
                  value={note}
                  onChange={(e) => setNote(e.target.value)}
                />
              </label>
              <div className="row-actions full-row">
                <button disabled={busy}>Lưu thông tin đối chiếu</button>
              </div>
            </form>
            <form onSubmit={showPreview} className="form-grid">
              <label>
                Số ngày của kỳ
                <input
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
              </label>
              <label>
                Tên gói (tùy chọn)
                <input
                  maxLength={100}
                  value={label}
                  onChange={(e) => setLabel(e.target.value)}
                />
              </label>
              <QuotaInputs
                prefix={`approve-${request.id}`}
                values={quotas}
                onChange={setQuotas}
              />
              <div className="row-actions full-row">
                <button disabled={busy}>Xem trước kỳ</button>
              </div>
              {preview && (
                <div className="notice full-row">
                  <p>
                    Kỳ sẽ bắt đầu{" "}
                    <strong>{vnDateTime(preview.startsAt)}</strong> và kết thúc{" "}
                    <strong>{vnDateTime(preview.endsAt)}</strong> (giờ Việt
                    Nam). Không tự suy gói từ số tiền.
                  </p>
                  <button
                    type="button"
                    className="primary"
                    disabled={busy}
                    onClick={approve}
                  >
                    Duyệt và cấp kỳ
                  </button>
                </div>
              )}
            </form>
            <form onSubmit={reject} className="form-grid">
              <label className="full-row">
                Lý do từ chối
                <input
                  maxLength={500}
                  required
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                />
              </label>
              <div className="row-actions full-row">
                <button disabled={busy}>Từ chối</button>
              </div>
            </form>
          </div>
        )}
      </div>
    </li>
  );
}
