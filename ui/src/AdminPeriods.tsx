import { useState, type FormEvent } from "react";
import { api } from "./api";
import { vnDateTime, type TeacherDetailView } from "./admin-api";
import { QuotaInputs, quotaBody } from "./QuotaTable";

type Props = {
  teacher: TeacherDetailView;
  fail: (e: unknown, fallback: string) => void;
  done: (text: string) => Promise<void>;
};

// Converts an ISO instant to the value of a datetime-local input in Vietnam time.
function toVnInput(iso: string) {
  const vn = new Date(new Date(iso).getTime() + 7 * 3600_000);
  return vn.toISOString().slice(0, 16);
}
function fromVnInput(value: string) {
  return new Date(`${value}:00+07:00`).toISOString();
}

export function AdminPeriods({ teacher, fail, done }: Props) {
  const [busy, setBusy] = useState(false);
  const [days, setDays] = useState("");
  const [label, setLabel] = useState("");
  const [reason, setReason] = useState("");
  const [quotas, setQuotas] = useState<Record<string, string>>({});
  const [preview, setPreview] = useState<{
    startsAt: string;
    endsAt: string;
  } | null>(null);
  const latest = teacher.periods[teacher.periods.length - 1];
  const [endsAt, setEndsAt] = useState(latest ? toVnInput(latest.endsAt) : "");
  const [editReason, setEditReason] = useState("");
  const [lockReason, setLockReason] = useState("");
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
  const grant = () =>
    void run(async () => {
      try {
        await api(`/admin/teachers/${teacher.id}/periods`, "POST", {
          days: Number(days),
          label,
          reason,
          expectedStartsAt: preview!.startsAt,
          ...quotaBody(quotas),
        });
        setPreview(null);
        setReason("");
        await done("Đã cấp/gia hạn kỳ quyền sử dụng.");
      } catch (e) {
        setPreview(null);
        throw e;
      }
    }, "Không thể gia hạn.");
  const edit = (event: FormEvent) => {
    event.preventDefault();
    void run(async () => {
      await api(`/admin/periods/${latest.id}`, "PUT", {
        endsAt: fromVnInput(endsAt),
        label: latest.label,
        reason: editReason,
        version: latest.version,
      });
      setEditReason("");
      await done("Đã sửa kỳ hiện tại.");
    }, "Không thể sửa kỳ.");
  };
  const lock = (event: FormEvent) => {
    event.preventDefault();
    const path = teacher.isBlocked ? "unlock" : "lock";
    void run(async () => {
      await api(`/admin/teachers/${teacher.id}/${path}`, "POST", {
        reason: lockReason,
      });
      setLockReason("");
      await done(
        teacher.isBlocked
          ? "Đã mở khóa. Kỳ sử dụng không được tự gia hạn."
          : "Đã khóa tài khoản và thu hồi mọi phiên.",
      );
    }, "Không thể cập nhật trạng thái khóa.");
  };
  return (
    <div className="stack admin-actions" aria-busy={busy}>
      {!teacher.isBlocked && teacher.emailConfirmed && (
        <form onSubmit={showPreview} className="form-grid">
          <h3 className="full-row">Cấp hoặc gia hạn trực tiếp</h3>
          <label>
            Số ngày
            <input
              id="grant-days"
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
              id="grant-label"
              maxLength={100}
              value={label}
              onChange={(e) => setLabel(e.target.value)}
            />
          </label>
          <label className="full-row">
            Lý do
            <input
              id="grant-reason"
              maxLength={500}
              required
              value={reason}
              onChange={(e) => setReason(e.target.value)}
            />
          </label>
          <QuotaInputs prefix="grant" values={quotas} onChange={setQuotas} />
          <div className="row-actions full-row">
            <button disabled={busy}>Xem trước kỳ</button>
          </div>
          {preview && (
            <div className="notice full-row">
              <p>
                Kỳ mới: <strong>{vnDateTime(preview.startsAt)}</strong> –{" "}
                <strong>{vnDateTime(preview.endsAt)}</strong>. Còn hạn thì nối
                tiếp cuối kỳ hiện tại; đã hết hạn thì bắt đầu lúc duyệt. Không
                tự hoàn tiền.
              </p>
              <button
                type="button"
                className="primary"
                disabled={busy}
                onClick={grant}
              >
                Xác nhận cấp kỳ
              </button>
            </div>
          )}
        </form>
      )}
      {latest && (
        <form onSubmit={edit} className="form-grid">
          <h3 className="full-row">Sửa ngày kết thúc kỳ cuối</h3>
          <label>
            Kết thúc (giờ Việt Nam)
            <input
              id="edit-ends"
              type="datetime-local"
              required
              value={endsAt}
              onChange={(e) => setEndsAt(e.target.value)}
            />
          </label>
          <label>
            Lý do sửa
            <input
              id="edit-reason"
              maxLength={500}
              required
              value={editReason}
              onChange={(e) => setEditReason(e.target.value)}
            />
          </label>
          <div className="row-actions full-row">
            <button disabled={busy}>Lưu thay đổi kỳ</button>
          </div>
        </form>
      )}
      <form onSubmit={lock} className="form-grid">
        <h3 className="full-row">
          {teacher.isBlocked ? "Mở khóa tài khoản" : "Khóa tài khoản"}
        </h3>
        <label className="full-row">
          Lý do
          <input
            id="lock-reason"
            maxLength={500}
            required
            value={lockReason}
            onChange={(e) => setLockReason(e.target.value)}
          />
        </label>
        {!teacher.isBlocked && (
          <p className="muted full-row">
            Khóa sẽ thu hồi mọi phiên, chặn cả dữ liệu cũ và hủy các lượt AI
            đang giữ. Mở khóa không tự gia hạn kỳ.
          </p>
        )}
        <div className="row-actions full-row">
          <button disabled={busy}>
            {teacher.isBlocked ? "Mở khóa" : "Khóa và thu hồi phiên"}
          </button>
        </div>
      </form>
    </div>
  );
}
