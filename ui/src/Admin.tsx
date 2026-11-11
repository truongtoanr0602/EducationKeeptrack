import { useCallback, useEffect, useState } from "react";
import { api, ApiError, type Me } from "./api";
import { vnDateTime, type TeacherSummary } from "./admin-api";
import { TeacherDetail } from "./AdminTeacher";
import { AdminInstructions } from "./AdminInstructions";
import { badgeClass, statusBadge } from "./Service";
import { href, navigate, type Route } from "./router";

type AdminRoute = Extract<Route, { name: "admin-teachers" | "admin-settings" }>;

// Admin workspace: service access only. There is deliberately no navigation into classes,
// students, photos, PDFs or tuition.
export function Admin({
  me,
  route,
  onLogout,
  onExpired,
}: {
  me: Me;
  route: AdminRoute;
  onLogout: () => Promise<void>;
  onExpired: () => void;
}) {
  const [menuOpen, setMenuOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [refresh, setRefresh] = useState(0);
  useEffect(() => {
    setMenuOpen(false);
    document.getElementById("screen")?.focus({ preventScroll: true });
    window.scrollTo({ top: 0 });
  }, [route.name, route.name === "admin-teachers" ? route.teacherId : ""]);
  const logout = () => {
    setBusy(true);
    void onLogout().finally(() => setBusy(false));
  };
  const title =
    route.name === "admin-settings"
      ? "Hướng dẫn thanh toán"
      : "Hồ sơ giáo viên";
  return (
    <div className={`app-shell${menuOpen ? " menu-open" : ""}`}>
      <a className="skip-link" href="#screen">
        Bỏ qua điều hướng
      </a>
      <aside className="sidebar" aria-label="Điều hướng quản trị">
        <a className="brand" href={href({ name: "admin-teachers" })}>
          Education Keeptrack
        </a>
        <nav aria-label="Quản lý dịch vụ">
          <ul className="section-nav">
            <li>
              <a
                className="nav-link"
                href={href({ name: "admin-teachers" })}
                aria-current={
                  route.name === "admin-teachers" ? "page" : undefined
                }
              >
                <span aria-hidden="true">◎</span> Hồ sơ giáo viên
              </a>
            </li>
            <li>
              <a
                className="nav-link"
                href={href({ name: "admin-settings" })}
                aria-current={
                  route.name === "admin-settings" ? "page" : undefined
                }
              >
                <span aria-hidden="true">₫</span> Hướng dẫn thanh toán
              </a>
            </li>
          </ul>
        </nav>
        <div className="sidebar-account">
          <span>{me.email} · Admin</span>
          <button disabled={busy} onClick={logout}>
            Đăng xuất
          </button>
        </div>
      </aside>
      <div className="shell-main">
        <header className="topbar">
          <button
            className="menu-button"
            aria-expanded={menuOpen}
            aria-label="Mở menu"
            onClick={() => setMenuOpen((open) => !open)}
          >
            ☰
          </button>
          <nav aria-label="Vị trí hiện tại" className="breadcrumb">
            <a href={href({ name: "admin-teachers" })}>Quản lý dịch vụ</a>
            <span aria-hidden="true">/</span>
            <span aria-current="page">{title}</span>
          </nav>
          <div className="topbar-account">
            <button disabled={busy} onClick={logout}>
              Đăng xuất
            </button>
          </div>
        </header>
        <main className="workspace" id="screen" tabIndex={-1}>
          <div className="page-heading compact">
            <div>
              <span className="eyebrow">QUẢN LÝ DỊCH VỤ</span>
              <h1>{title}</h1>
            </div>
          </div>
          {route.name === "admin-settings" && (
            <AdminInstructions onExpired={onExpired} />
          )}
          {route.name === "admin-teachers" && !route.teacherId && (
            <TeacherList refresh={refresh} onExpired={onExpired} />
          )}
          {route.name === "admin-teachers" && route.teacherId && (
            <TeacherDetail
              key={route.teacherId}
              id={route.teacherId}
              onClose={() => navigate({ name: "admin-teachers" })}
              onChanged={() => setRefresh((value) => value + 1)}
              onExpired={onExpired}
            />
          )}
        </main>
      </div>
      {menuOpen && (
        <button
          className="menu-backdrop"
          aria-label="Đóng menu"
          onClick={() => setMenuOpen(false)}
        />
      )}
    </div>
  );
}

type Filter = "all" | "open" | "pending" | "active" | "expired";
const filters: { id: Filter; label: string }[] = [
  { id: "all", label: "Tất cả" },
  { id: "open", label: "Chờ đối chiếu" },
  { id: "pending", label: "Chưa có quyền" },
  { id: "active", label: "Còn hạn" },
  { id: "expired", label: "Hết hạn" },
];

function TeacherList({
  refresh,
  onExpired,
}: {
  refresh: number;
  onExpired: () => void;
}) {
  const [teachers, setTeachers] = useState<TeacherSummary[]>([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState<Filter>("all");
  const [query, setQuery] = useState("");
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
  }, [load, refresh]);
  const shown = teachers.filter((t) => {
    if (query && !t.email.toLowerCase().includes(query.trim().toLowerCase()))
      return false;
    if (filter === "open") return t.openRequest;
    if (filter === "pending")
      return ["pending", "rejected", "unverified"].includes(t.status);
    if (filter === "active") return t.status === "active";
    if (filter === "expired") return t.status === "expired";
    return true;
  });
  const openCount = teachers.filter((t) => t.openRequest).length;
  return (
    <section className="panel stack" aria-label="Danh sách giáo viên">
      <p className="lead">
        Đối chiếu thanh toán phí dịch vụ và cấp quyền sử dụng. Admin không xem
        dữ liệu lớp, học sinh hay học phí của giáo viên.
      </p>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      <article className="card">
        <div className="card-heading">
          <h2>
            Giáo viên{" "}
            {openCount > 0 && (
              <span className="state insufficient">
                {openCount} yêu cầu chờ đối chiếu
              </span>
            )}
          </h2>
          <label>
            Tìm email
            <input
              type="search"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
            />
          </label>
        </div>
        <div className="chips" role="group" aria-label="Lọc theo trạng thái">
          {filters.map((f) => (
            <button
              key={f.id}
              aria-pressed={filter === f.id}
              onClick={() => setFilter(f.id)}
            >
              {f.label}
            </button>
          ))}
        </div>
        {loading ? (
          <p role="status">Đang tải…</p>
        ) : shown.length === 0 ? (
          <p className="muted">Không có hồ sơ phù hợp.</p>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Email</th>
                  <th>Trạng thái</th>
                  <th>Chờ đối chiếu</th>
                  <th>Quyền đến</th>
                  <th>Đăng ký lúc</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {shown.map((t) => {
                  const status = t.isBlocked ? "blocked" : t.status;
                  return (
                    <tr key={t.id}>
                      <td>
                        {t.email}
                        {!t.emailConfirmed && (
                          <div className="muted">Chưa xác minh email</div>
                        )}
                      </td>
                      <td>
                        <span className={`state ${badgeClass[status]}`}>
                          {statusBadge[status]}
                        </span>
                      </td>
                      <td>{t.openRequest ? "Có" : ""}</td>
                      <td>
                        {t.accessEndsAt ? vnDateTime(t.accessEndsAt) : ""}
                      </td>
                      <td>{vnDateTime(t.registeredAt)}</td>
                      <td>
                        <a
                          className="button-link"
                          href={href({
                            name: "admin-teachers",
                            teacherId: t.id,
                          })}
                        >
                          Xem hồ sơ
                        </a>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </article>
    </section>
  );
}
