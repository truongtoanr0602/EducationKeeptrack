import { useCallback, useEffect, useState } from "react";
import { api, ApiError, type Me } from "./api";
import { statusLabels, vnDateTime, type TeacherSummary } from "./admin-api";
import { TeacherDetail } from "./AdminTeacher";
import { AdminInstructions } from "./AdminInstructions";

// Admin manages service access only; no navigation into classes, students or tuition.
export function Admin({
  me,
  onLogout,
  onExpired,
}: {
  me: Me;
  onLogout: () => Promise<void>;
  onExpired: () => void;
}) {
  const [teachers, setTeachers] = useState<TeacherSummary[]>([]);
  const [selected, setSelected] = useState<string | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      setTeachers(await api<TeacherSummary[]>("/admin/teachers"));
    } catch (e) {
      if (e instanceof ApiError && e.status === 401) onExpired();
      else setError(e instanceof Error ? e.message : "Không thể tải hồ sơ.");
    } finally {
      setLoading(false);
    }
  }, [onExpired]);
  useEffect(() => {
    void load();
  }, [load]);
  return (
    <>
      <header>
        <span className="brand">Education Keeptrack · Admin</span>
        <div className="account">
          <span>{me.email}</span>
          <button
            disabled={busy}
            onClick={() => {
              setBusy(true);
              void onLogout().finally(() => setBusy(false));
            }}
          >
            Đăng xuất
          </button>
        </div>
      </header>
      <main className="workspace admin">
        <div className="page-heading">
          <div>
            <span className="eyebrow">QUẢN LÝ DỊCH VỤ</span>
            <h1>Hồ sơ giáo viên</h1>
            <p>
              Đối chiếu thanh toán dịch vụ và cấp quyền sử dụng. Admin không xem
              dữ liệu lớp, học sinh hay học phí.
            </p>
          </div>
        </div>
        <AdminInstructions onExpired={onExpired} />
        {error && (
          <p role="alert" className="error">
            {error}
          </p>
        )}
        {loading ? (
          <p role="status">Đang tải…</p>
        ) : teachers.length === 0 ? (
          <section className="empty">
            <h2>Chưa có hồ sơ giáo viên</h2>
          </section>
        ) : (
          <div className="tuition-table-wrap">
            <table aria-label="Danh sách giáo viên">
              <thead>
                <tr>
                  <th>Email</th>
                  <th>Xác minh</th>
                  <th>Trạng thái</th>
                  <th>Chờ đối chiếu</th>
                  <th>Quyền đến</th>
                  <th>Đăng ký lúc</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {teachers.map((t) => (
                  <tr key={t.id} aria-selected={t.id === selected}>
                    <td>{t.email}</td>
                    <td>{t.emailConfirmed ? "Đã xác minh" : "Chưa"}</td>
                    <td>{t.isBlocked ? "Đã khóa" : statusLabels[t.status]}</td>
                    <td>{t.openRequest ? "Có" : ""}</td>
                    <td>{t.accessEndsAt ? vnDateTime(t.accessEndsAt) : ""}</td>
                    <td>{vnDateTime(t.registeredAt)}</td>
                    <td>
                      <button onClick={() => setSelected(t.id)}>
                        Xem hồ sơ
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {selected && (
          <TeacherDetail
            key={selected}
            id={selected}
            onClose={() => setSelected(null)}
            onChanged={() => void load()}
            onExpired={onExpired}
          />
        )}
      </main>
    </>
  );
}
