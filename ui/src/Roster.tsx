import { useCallback, useEffect, useState } from "react";
import { api, ApiError, type Me, type TeacherClass } from "./api";
import { EnrollmentForm } from "./EnrollmentForm";
import { StudentForm } from "./StudentForm";
import { Login } from "./Login";
import { MonthlyReports } from "./MonthlyReports";
import { statusNames, type Enrollment, type Student } from "./roster-api";

export function Roster({ classroom, me }: { classroom: TeacherClass; me: Me }) {
  const [items, setItems] = useState<Enrollment[]>([]);
  const [students, setStudents] = useState<Student[]>([]);
  const [access, setAccess] = useState(me.access);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [sessionExpired, setSessionExpired] = useState(false);
  const [form, setForm] = useState<
    | { kind: "student"; student?: Student }
    | { kind: "enrollment"; item?: Enrollment }
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
  function saved() {
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
        <div className="page-heading">
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
        {form?.kind === "student" && (
          <StudentForm
            key={form.student?.id ?? "new"}
            student={form.student}
            classId={classroom.id}
            canWrite={access.canWrite}
            onSaved={saved}
            onCancel={() => setForm(null)}
            onDenied={denied}
          />
        )}
        {form?.kind === "enrollment" && (
          <EnrollmentForm
            key={form.item?.id ?? "new"}
            item={form.item}
            students={students}
            classId={classroom.id}
            canWrite={access.canWrite}
            onSaved={saved}
            onCancel={() => setForm(null)}
            onDenied={denied}
          />
        )}
        {loading && <p role="status">Đang tải danh sách…</p>}
        {!loading && !error && items.length === 0 && (
          <p>Chưa có học sinh trong lớp này.</p>
        )}
        <div className="roster-list">
          {items.map((item) => (
            <article className="class-card" key={item.id}>
              <span className={`enrollment-status ${item.status}`}>
                {statusNames[item.status]}
              </span>
              <h3>{item.student.name}</h3>
              <p>Phụ huynh: {item.student.parentName ?? "Chưa có"}</p>
              {item.student.phone && <p>Điện thoại: {item.student.phone}</p>}
              {item.student.email && <p>Email: {item.student.email}</p>}
              {item.student.notes && <p>{item.student.notes}</p>}
              <p>
                Bắt đầu: {item.startDate}
                {item.endDate && ` · Kết thúc: ${item.endDate}`}
              </p>
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
              <details>
                <summary>Lịch sử theo học</summary>
                <ol>
                  {item.history.map((h) => (
                    <li key={h.version}>
                      {h.effectiveDate} · {statusNames[h.status]}
                    </li>
                  ))}
                </ol>
              </details>
            </article>
          ))}
        </div>
      </div>
      <div hidden={sessionExpired || !access.canRead}>
        <MonthlyReports classId={classroom.id} students={items.map(item => item.student)} canWrite={access.canWrite} onDenied={denied} />
      </div>
      {!access.canRead && !sessionExpired && (
        <p className="error" role="alert">
          Tài khoản hiện không được truy cập danh sách học sinh.
        </p>
      )}
    </section>
  );
}
