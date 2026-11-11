import { useCallback, useEffect, useState } from "react";
import {
  api,
  ApiError,
  type ClassDraft,
  type Me,
  type TeacherClass,
} from "./api";
import { Login } from "./Login";
import { CreateClass } from "./CreateClass";
import { Roster } from "./Roster";
import { Sessions } from "./Sessions";
import { Tuition } from "./Tuition";
import { Learning } from "./Learning";
import { ReportsScreen } from "./ReportsScreen";
import { Personalization } from "./Personalization";
import { Admin } from "./Admin";
import { AccountScreen, ServiceNotice } from "./Service";
import { Register } from "./Register";
import { ForgotPassword } from "./AccountSettings";
import { readTokenLink, TokenAction } from "./TokenAction";
import type { ServiceProfile } from "./service-api";
import {
  classHref,
  href,
  navigate,
  sections,
  useRoute,
  type Route,
} from "./router";

const emptyDraft: ClassDraft = { name: "", subject: "", description: "" };

export function App() {
  const route = useRoute();
  const [me, setMe] = useState<Me | null>(null);
  const [loading, setLoading] = useState(true);
  const [classes, setClasses] = useState<TeacherClass[]>([]);
  const [creating, setCreating] = useState(false);
  const [draft, setDraft] = useState(emptyDraft);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const [loggingOut, setLoggingOut] = useState(false);
  const [resume, setResume] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);
  const [screen, setScreen] = useState<"login" | "register" | "forgot">(
    "login",
  );
  const [link, setLink] = useState(readTokenLink);
  const load = useCallback(async () => {
    const user = await api<Me>("/auth/me");
    const items =
      user.role !== "Admin" && user.access.canRead
        ? await api<TeacherClass[]>("/classes")
        : [];
    setMe(user);
    setClasses(items);
    setError("");
  }, []);
  useEffect(() => {
    load()
      .catch((e) => {
        if (!(e instanceof ApiError && e.status === 401))
          setError("Không thể kết nối máy chủ. Vui lòng thử lại.");
      })
      .finally(() => setLoading(false));
  }, [load]);
  // A user input, rather than a timer/poll, renews idle activity at most once/minute.
  useEffect(() => {
    if (!me) return;
    let last = 0;
    const activity = () => {
      if (Date.now() - last < 60_000) return;
      last = Date.now();
      void api("/auth/activity", "POST").catch((e) => {
        if (e instanceof ApiError && e.status === 401)
          setMessage(
            "Phiên đã hết hạn. Vui lòng đăng nhập lại khi tiếp tục lưu.",
          );
      });
    };
    window.addEventListener("pointerdown", activity);
    window.addEventListener("keydown", activity);
    return () => {
      window.removeEventListener("pointerdown", activity);
      window.removeEventListener("keydown", activity);
    };
  }, [me]);
  // Close the mobile menu and move focus to the new screen after navigation.
  useEffect(() => {
    setMenuOpen(false);
    document.getElementById("screen")?.focus({ preventScroll: true });
    window.scrollTo({ top: 0 });
  }, [
    route.name,
    route.name === "class" ? route.classId : "",
    route.name === "class" ? route.section : "",
    route.name === "class" ? route.tab : "",
  ]);
  // A class created in another tab (or via a link) is not in the list loaded at login:
  // refresh once before concluding the class does not exist.
  const [checked, setChecked] = useState<string | null>(null);
  const routeClass = route.name === "class" ? route.classId : null;
  const missing =
    !!me && !!routeClass && !classes.some((item) => item.id === routeClass);
  useEffect(() => {
    if (!missing || !me?.access.canRead || checked === routeClass) return;
    let active = true;
    api<TeacherClass[]>("/classes")
      .then((items) => active && setClasses(items))
      .catch(() => undefined)
      .finally(() => active && setChecked(routeClass));
    return () => {
      active = false;
    };
  }, [missing, me, routeClass, checked]);
  // Admin and teacher areas never share routes: send each role to its own workspace.
  useEffect(() => {
    if (!me) return;
    const adminRoute =
      route.name === "admin-teachers" || route.name === "admin-settings";
    if (me.role === "Admin" && !adminRoute)
      navigate({ name: "admin-teachers" });
    if (me.role !== "Admin" && adminRoute) navigate({ name: "classes" });
  }, [me, route.name]);
  // Keep the shell's access in sync when the account screen reloads the service profile.
  const syncAccess = useCallback((profile: ServiceProfile) => {
    setMe((current) =>
      current &&
      (current.access.status !== profile.access.status ||
        current.access.canWrite !== profile.access.canWrite ||
        current.access.canUseAi !== profile.access.canUseAi)
        ? { ...current, access: profile.access }
        : current,
    );
  }, []);
  const sessionEnded = useCallback(() => {
    setMe(null);
    setClasses([]);
    setMessage("Phiên đã hết hạn hoặc đã bị thu hồi. Vui lòng đăng nhập lại.");
  }, []);
  async function login() {
    await load();
    if (resume) {
      setCreating(true);
      setResume(false);
    }
  }
  async function logout() {
    setLoggingOut(true);
    setError("");
    try {
      await api("/auth/logout", "POST");
      setMe(null);
      setClasses([]);
      setDraft(emptyDraft);
      setCreating(false);
      setResume(false);
      setMessage("");
      navigate({ name: "classes" });
    } catch (e) {
      if (e instanceof ApiError && e.status === 401) {
        setMe(null);
        setClasses([]);
      } else setError("Không thể đăng xuất. Vui lòng thử lại.");
    } finally {
      setLoggingOut(false);
    }
  }
  if (loading)
    return (
      <main className="loading" role="status">
        Đang tải…
      </main>
    );
  // One-time email links work whether or not this browser is signed in; afterwards the session is
  // re-read because confirming an email or resetting a password revokes existing sessions.
  if (link)
    return (
      <TokenAction
        link={link}
        onDone={(text) => {
          setLink(null);
          setMessage(text);
          setScreen("login");
          if (me)
            load().catch(() => {
              setMe(null);
              setClasses([]);
            });
        }}
      />
    );
  if (!me && screen === "register")
    return <Register onBack={() => setScreen("login")} />;
  if (!me && screen === "forgot")
    return <ForgotPassword onBack={() => setScreen("login")} />;
  if (!me)
    return (
      <>
        {message && (
          <div className="notice" role="status">
            {message}
          </div>
        )}
        {error && (
          <p className="error" role="alert">
            {error}
          </p>
        )}
        <Login
          onLogin={login}
          onRegister={() => {
            setMessage("");
            setScreen("register");
          }}
          onForgot={() => {
            setMessage("");
            setScreen("forgot");
          }}
        />
      </>
    );
  if (me.role === "Admin")
    return route.name === "admin-teachers" ||
      route.name === "admin-settings" ? (
      <Admin me={me} route={route} onLogout={logout} onExpired={sessionEnded} />
    ) : (
      <main className="loading" role="status">
        Đang chuyển tới khu vực quản trị…
      </main>
    );
  const current =
    route.name === "class"
      ? classes.find((item) => item.id === route.classId)
      : undefined;
  const sectionLabel =
    route.name === "class"
      ? sections.find((x) => x.id === route.section)?.label
      : undefined;
  return (
    <div className={`app-shell${menuOpen ? " menu-open" : ""}`}>
      <a className="skip-link" href="#screen">
        Bỏ qua điều hướng
      </a>
      <aside className="sidebar" aria-label="Điều hướng chính">
        <a className="brand" href={href({ name: "classes" })}>
          Education Keeptrack
        </a>
        <nav aria-label="Không gian giảng dạy">
          <a
            className="nav-link"
            href={href({ name: "classes" })}
            aria-current={route.name === "classes" ? "page" : undefined}
          >
            <span aria-hidden="true">⌂</span> Lớp học của tôi
          </a>
          <a
            className="nav-link"
            href={href({ name: "account" })}
            aria-current={route.name === "account" ? "page" : undefined}
          >
            <span aria-hidden="true">◈</span> Tài khoản & dịch vụ
          </a>
        </nav>
        {route.name === "class" && current && (
          <div className="sidebar-class">
            <label>
              Lớp đang mở
              <select
                value={current.id}
                onChange={(e) =>
                  navigate({
                    name: "class",
                    classId: e.target.value,
                    section: route.section,
                    tab: route.tab,
                  })
                }
              >
                {classes.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.name}
                  </option>
                ))}
              </select>
            </label>
            <SectionNav route={route} label="Màn hình của lớp" />
          </div>
        )}
        <div className="sidebar-account">
          <span>{me.email}</span>
          <button disabled={loggingOut} onClick={() => void logout()}>
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
            {route.name === "account" ? (
              <span aria-current="page">Tài khoản & dịch vụ</span>
            ) : (
              <a href={href({ name: "classes" })}>Lớp học</a>
            )}
            {current && (
              <>
                <span aria-hidden="true">/</span>
                <a href={classHref(current.id, "students")}>{current.name}</a>
                <span aria-hidden="true">/</span>
                <span aria-current="page">{sectionLabel}</span>
              </>
            )}
          </nav>
          <div className="topbar-account">
            <button disabled={loggingOut} onClick={() => void logout()}>
              Đăng xuất
            </button>
          </div>
        </header>
        {route.name === "class" && current && (
          <div className="section-tabs">
            <SectionNav route={route} label="Chuyển màn hình lớp" />
          </div>
        )}
        <main className="workspace" id="screen" tabIndex={-1}>
          {route.name !== "account" && <ServiceNotice access={me.access} />}
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
          {route.name === "account" && (
            <>
              <div className="page-heading compact">
                <div>
                  <span className="eyebrow">{me.email.toUpperCase()}</span>
                  <h1>Tài khoản & dịch vụ</h1>
                </div>
              </div>
              <AccountScreen onExpired={sessionEnded} onChanged={syncAccess} />
            </>
          )}
          {route.name === "classes" && (
            <ClassesScreen
              me={me}
              classes={classes}
              onCreate={() => {
                setDraft(emptyDraft);
                setCreating(true);
              }}
            />
          )}
          {route.name === "class" &&
            !current &&
            checked !== route.classId &&
            me.access.canRead && <p role="status">Đang tải lớp…</p>}
          {route.name === "class" &&
            !current &&
            (checked === route.classId || !me.access.canRead) && (
              <section className="empty">
                <h1>Không tìm thấy lớp</h1>
                <p>Lớp không tồn tại hoặc không thuộc tài khoản của bạn.</p>
                <a className="button-link" href={href({ name: "classes" })}>
                  Về danh sách lớp
                </a>
              </section>
            )}
          {route.name === "class" && current && (
            <ClassScreen
              key={`${current.id}/${route.section}`}
              route={route}
              classroom={current}
              me={me}
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
      {creating && (
        <CreateClass
          initial={draft}
          onClose={() => setCreating(false)}
          onSaved={(item) => {
            setClasses((items) => [...items, item]);
            setCreating(false);
            setDraft(emptyDraft);
            setMessage("Đã tạo lớp thành công.");
          }}
          onExpired={(saved) => {
            setDraft(saved);
            setCreating(false);
            setResume(true);
            setMe(null);
            setMessage(
              "Phiên đã hết hạn. Đăng nhập lại để tiếp tục; thông tin lớp được giữ trong trang này.",
            );
          }}
        />
      )}
    </div>
  );
}

function SectionNav({
  route,
  label,
}: {
  route: Extract<Route, { name: "class" }>;
  label: string;
}) {
  return (
    <nav aria-label={label}>
      <ul className="section-nav">
        {sections.map((section) => (
          <li key={section.id}>
            <a
              className="nav-link"
              href={classHref(route.classId, section.id)}
              aria-current={route.section === section.id ? "page" : undefined}
            >
              <span aria-hidden="true">{section.icon}</span> {section.label}
            </a>
          </li>
        ))}
      </ul>
    </nav>
  );
}

function ClassesScreen({
  me,
  classes,
  onCreate,
}: {
  me: Me;
  classes: TeacherClass[];
  onCreate: () => void;
}) {
  return (
    <>
      <div className="page-heading">
        <div>
          <span className="eyebrow">KHÔNG GIAN GIẢNG DẠY</span>
          <h1>Lớp học của tôi</h1>
          <p>Quản lý những lớp học của bạn.</p>
        </div>
        <button
          className="primary"
          disabled={!me.access.canWrite}
          onClick={onCreate}
        >
          <span aria-hidden="true">＋ </span>Tạo lớp mới
        </button>
      </div>
      {classes.length ? (
        <section className="class-grid" aria-label="Danh sách lớp">
          {classes.map((item) => (
            <article key={item.id} className="class-card">
              <span className="subject">{item.subject}</span>
              <h2>{item.name}</h2>
              {item.description && <p>{item.description}</p>}
              <div className="card-links">
                <button
                  onClick={() =>
                    navigate({
                      name: "class",
                      classId: item.id,
                      section: "students",
                    })
                  }
                >
                  Danh sách học sinh
                </button>
                <a href={classHref(item.id, "sessions")}>Lịch học</a>
                <a href={classHref(item.id, "tuition")}>Học phí</a>
                <a href={classHref(item.id, "personalization")}>Cá nhân hoá</a>
              </div>
            </article>
          ))}
        </section>
      ) : (
        me.access.canRead && (
          <section className="empty">
            <span aria-hidden="true">▤</span>
            <h2>Bắt đầu với lớp học đầu tiên</h2>
            <p>Thêm tên lớp và môn học để tổ chức công việc giảng dạy.</p>
          </section>
        )
      )}
    </>
  );
}

function ClassScreen({
  route,
  classroom,
  me,
}: {
  route: Extract<Route, { name: "class" }>;
  classroom: TeacherClass;
  me: Me;
}) {
  const section = sections.find((x) => x.id === route.section)!;
  return (
    <>
      <div className="page-heading compact">
        <div>
          <span className="eyebrow">
            {classroom.subject.toUpperCase()} · {classroom.name.toUpperCase()}
          </span>
          <h1>{section.label}</h1>
        </div>
      </div>
      {route.section === "students" && <Roster classroom={classroom} me={me} />}
      {route.section === "sessions" && (
        <Sessions classroom={classroom} me={me} />
      )}
      {route.section === "tuition" && (
        <Tuition classroom={classroom} me={me} refreshToken={0} />
      )}
      {route.section === "assessments" && (
        <Learning classroom={classroom} me={me} />
      )}
      {route.section === "personalization" && (
        <Personalization
          classroom={classroom}
          me={me}
          tab={route.tab ?? "curriculum"}
        />
      )}
      {route.section === "reports" && (
        <ReportsScreen classroom={classroom} me={me} />
      )}
    </>
  );
}
