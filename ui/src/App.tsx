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

const emptyDraft: ClassDraft = { name: "", subject: "", description: "" };
export function App() {
  const [me, setMe] = useState<Me | null>(null);
  const [loading, setLoading] = useState(true);
  const [classes, setClasses] = useState<TeacherClass[]>([]);
  const [creating, setCreating] = useState(false);
  const [draft, setDraft] = useState(emptyDraft);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const [loggingOut, setLoggingOut] = useState(false);
  const [resume, setResume] = useState(false);
  const [selectedId, setSelectedId] = useState(() =>
    window.location.hash.slice(1),
  );
  useEffect(() => {
    const change = () => setSelectedId(window.location.hash.slice(1));
    window.addEventListener("hashchange", change);
    return () => window.removeEventListener("hashchange", change);
  }, []);
  const load = useCallback(async () => {
    const user = await api<Me>("/auth/me");
    const items = user.access.canRead
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
      window.location.hash = "";
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
        <Login onLogin={login} />
      </>
    );
  return (
    <>
      <header>
        <a className="brand" href="/">
          Education Keeptrack
        </a>
        <div className="account">
          <span>{me.email}</span>
          <button disabled={loggingOut} onClick={() => void logout()}>
            Đăng xuất
          </button>
        </div>
      </header>
      <main className="workspace">
        <div className="page-heading">
          <div>
            <span className="eyebrow">KHÔNG GIAN GIẢNG DẠY</span>
            <h1>Lớp học của tôi</h1>
            <p>Quản lý những lớp học của bạn.</p>
          </div>
          <button
            className="primary"
            disabled={!me.access.canWrite}
            onClick={() => {
              setDraft(emptyDraft);
              setCreating(true);
            }}
          >
            <span aria-hidden="true">＋ </span>Tạo lớp mới
          </button>
        </div>
        {!me.access.canWrite && (
          <p className="notice">
            {me.access.status === "expired"
              ? "Quyền sử dụng đã hết hạn. Bạn vẫn có thể xem lớp đã lưu."
              : me.access.status === "unverified"
                ? "Tài khoản chưa xác minh email."
                : "Tài khoản đang chờ duyệt quyền sử dụng."}
          </p>
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
        {classes.length ? (
          <section className="class-grid" aria-label="Danh sách lớp">
            {classes.map((item) => (
              <article key={item.id} className="class-card">
                <span className="subject">{item.subject}</span>
                <h2>{item.name}</h2>
                {item.description && <p>{item.description}</p>}
                <button
                  onClick={() => {
                    window.location.hash = item.id;
                  }}
                >
                  Danh sách học sinh
                </button>
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
        {classes
          .filter((item) => item.id === selectedId)
          .map((item) => (
            <div key={item.id}>
              <Roster classroom={item} me={me} />
              <Sessions classroom={item} me={me} />
            </div>
          ))}
      </main>
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
    </>
  );
}
