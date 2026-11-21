import { useCallback, useEffect, useMemo, useState } from "react";
import { api, ApiError, type Me, type TeacherClass } from "./api";
import { EnrollmentForm } from "./EnrollmentForm";
import { PasteStudents } from "./PasteStudents";
import { StudentForm } from "./StudentForm";
import { Popup } from "./ui/popup";
import { toast } from "sonner";
import { Login } from "./Login";
import { statusNames, type Enrollment, type Student } from "./roster-api";
import { formatDate } from "./format-date";

export function Roster({ classroom, me }: { classroom: TeacherClass; me: Me }) {
  const [items, setItems] = useState<Enrollment[]>([]);
  const [students, setStudents] = useState<Student[]>([]);
  const [access, setAccess] = useState(me.access);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [query, setQuery] = useState("");
  const [formLocked, setFormLocked] = useState(false);
  const [sessionExpired, setSessionExpired] = useState(false);
  const [form, setForm] = useState<
    | { kind: "student"; student?: Student }
    | { kind: "enrollment"; item?: Enrollment }
    | { kind: "paste" }
    | null
  >(null);
  const denied = useCallback(
    (e: ApiError) => {
      if (e.status === 401) setSessionExpired(true);
      if (e.status === 403) {
        void api<Me>("/auth/me")
          .then((user) => setAccess(user.access))
          .catch(() => {
            setAccess({
              ...me.access,
              canRead: false,
              canWrite: false,
              status: "blocked",
            });
          });
      }
    },
    [me.access],
  );
  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const [roster, profiles] = await Promise.all([
        api<Enrollment[]>(`/classes/${classroom.id}/enrollments`),
        api<Student[]>("/students"),
      ]);
      setItems(roster);
      setStudents(profiles);
      // Refresh expected versions without remounting the forms or replacing their drafts.
      setForm((current) => {
        if (current?.kind === "student" && current.student)
          return {
            ...current,
            student:
              profiles.find((s) => s.id === current.student?.id) ??
              current.student,
          };
        if (current?.kind === "enrollment" && current.item)
          return {
            ...current,
            item: roster.find((r) => r.id === current.item?.id) ?? current.item,
          };
        return current;
      });
    } catch (e) {
      setError(
        e instanceof Error ? e.message : "Không thể tải danh sách học sinh.",
      );
      if (e instanceof ApiError) denied(e);
    } finally {
      setLoading(false);
    }
  }, [classroom.id, denied]);
  useEffect(() => {
    void load();
  }, [load]);
  const shown = useMemo(() => {
    const q = query.trim().toLocaleLowerCase("vi");
    return items.filter(
      (i) =>
        !q ||
        i.student.name.toLocaleLowerCase("vi").includes(q) ||
        (i.student.parentName ?? "").toLocaleLowerCase("vi").includes(q),
    );
  }, [items, query]);
  function saved() {
    toast.success(
      form?.kind === "enrollment"
        ? "Tình trạng học đã được lưu."
        : "Hồ sơ học sinh đã được lưu.",
    );
    setForm(null);
    void load();
  }
  async function resume() {
    const user = await api<Me>("/auth/me");
    if (user.email !== me.email) {
      window.location.assign("/");
      return;
    }
    setAccess(user.access);
    setSessionExpired(false);
    await load();
  }
  return (
    <section className="roster" aria-label={`Học sinh lớp ${classroom.name}`}>
      {sessionExpired && (
        <>
          <p className="notice">
            Phiên đã hết hạn. Đăng nhập lại cùng tài khoản để tiếp tục; nội dung
            nhập được giữ trong trang này.
          </p>
          <Login onLogin={resume} />
        </>
      )}
      <div hidden={sessionExpired || !access.canRead}>
        <div className="page-heading sticky-heading">
          <div>
            <span className="eyebrow">DANH SÁCH THEO HỌC</span>
            <h2>{classroom.name}</h2>
            <p>{classroom.subject}</p>
          </div>
          <div className="roster-actions">
            <button
              disabled={!access.canWrite || !!form}
              onClick={() => setForm({ kind: "student" })}
            >
              Thêm học sinh mới
            </button>
            <button
              disabled={!access.canWrite || !!form}
              onClick={() => setForm({ kind: "paste" })}
            >
              Dán danh sách
            </button>
            <button
              disabled={!access.canWrite || !!form}
              onClick={() => setForm({ kind: "enrollment" })}
            >
              Chọn học sinh đã có
            </button>
            <button disabled={loading} onClick={() => void load()}>
              Tải lại danh sách
            </button>
          </div>
        </div>
        {!access.canWrite && (
          <p className="notice">
            Quyền sử dụng đã hết hạn. Bạn chỉ có thể xem dữ liệu đã lưu; nội
            dung nhập chưa lưu vẫn được giữ.
          </p>
        )}
        {error && (
          <p className="error" role="alert">
            {error}
          </p>
        )}
        {form && (
          <Popup
            open
            variant="side"
            title={
              form.kind === "paste"
                ? "Dán danh sách học sinh"
                : form.kind === "enrollment"
                  ? form.item
                    ? `Đổi tình trạng: ${form.item.student.name}`
                    : "Chọn học sinh đã có"
                  : form.student
                    ? `Sửa hồ sơ: ${form.student.name}`
                    : "Thêm học sinh mới"
            }
            onOpenChange={(next) => {
              if (!next) setForm(null);
            }}
            locked={formLocked}
            suspended={sessionExpired || !access.canRead}
            footer={
              <button
                type="button"
                onClick={() => void load()}
                disabled={loading}
              >
                Tải lại danh sách
              </button>
            }
          >
            {form.kind === "student" && (
              <StudentForm
                key={form.student?.id ?? "new"}
                student={form.student}
                classId={classroom.id}
                canWrite={access.canWrite}
                onSaved={saved}
                onCancel={() => setForm(null)}
                onDenied={denied}
                onLockChange={setFormLocked}
              />
            )}
            {form.kind === "paste" && (
              <PasteStudents
                classId={classroom.id}
                students={students}
                enrolledIds={items.map((item) => item.studentId)}
                canWrite={access.canWrite}
                onChanged={() => void load()}
                onClose={() => setForm(null)}
                onDenied={denied}
                onLockChange={setFormLocked}
              />
            )}
            {form.kind === "enrollment" && (
              <EnrollmentForm
                key={form.item?.id ?? "new"}
                item={form.item}
                students={students}
                classId={classroom.id}
                canWrite={access.canWrite}
                onSaved={saved}
                onCancel={() => setForm(null)}
                onDenied={denied}
                onLockChange={setFormLocked}
              />
            )}
          </Popup>
        )}
        {items.length > 5 && (
          <div className="list-search">
            <input
              type="search"
              aria-label="Tìm trong lớp"
              value={query}
              placeholder="Tìm theo tên học sinh hoặc phụ huynh"
              onChange={(e) => setQuery(e.target.value)}
            />
            <span className="muted">
              {shown.length}/{items.length} học sinh
            </span>
          </div>
        )}
        {loading && <p role="status">Đang tải danh sách…</p>}
        {!loading && !error && items.length === 0 && (
          <p>Chưa có học sinh trong lớp này.</p>
        )}
        <div className="roster-list compact">
          {shown.map((item) => (
            <article className="class-card" key={item.id}>
              <div className="roster-main">
                <h3>{item.student.name}</h3>
                <span className={`enrollment-status ${item.status}`}>
                  {statusNames[item.status]}
                </span>
              </div>
              <div className="roster-info">
                <p>Phụ huynh: {item.student.parentName ?? "Chưa có"}</p>
                {item.student.phone && <p>Điện thoại: {item.student.phone}</p>}
                {item.student.email && <p>Email: {item.student.email}</p>}
                {item.student.notes && <p>{item.student.notes}</p>}
                <p>
                  Bắt đầu: {formatDate(item.startDate)}
                  {item.endDate && ` · Kết thúc: ${formatDate(item.endDate)}`}
                </p>
                <details>
                  <summary>Lịch sử theo học</summary>
                  <ol>
                    {item.history.map((h) => (
                      <li key={h.version}>
                        {formatDate(h.effectiveDate)} · {statusNames[h.status]}
                      </li>
                    ))}
                  </ol>
                </details>
              </div>
              <div className="roster-actions">
                <button
                  disabled={!access.canWrite || !!form}
                  onClick={() =>
                    setForm({ kind: "student", student: item.student })
                  }
                >
                  Sửa hồ sơ
                </button>
                <button
                  disabled={
                    !access.canWrite || !!form || item.status === "stopped"
                  }
                  onClick={() => setForm({ kind: "enrollment", item })}
                >
                  Đổi tình trạng
                </button>
              </div>
            </article>
          ))}
        </div>
      </div>
      {!access.canRead && !sessionExpired && (
        <p className="error" role="alert">
          Tài khoản hiện không được truy cập danh sách học sinh.
        </p>
      )}
    </section>
  );
}
