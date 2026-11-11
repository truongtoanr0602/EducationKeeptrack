import { useEffect, useState } from "react";
import { api, type Me, type TeacherClass } from "./api";
import { useAccess } from "./access";
import { Login } from "./Login";
import { MonthlyReports } from "./MonthlyReports";
import type { Enrollment, Student } from "./roster-api";

// Monthly PDF reports used to live under the roster; it is now its own screen.
export function ReportsScreen({ classroom, me }: { classroom: TeacherClass; me: Me }) {
  const { access, expired, denied, relogin } = useAccess(me);
  const [students, setStudents] = useState<Student[] | null>(null);
  const [error, setError] = useState("");
  useEffect(() => {
    let active = true;
    api<Enrollment[]>(`/classes/${classroom.id}/enrollments`)
      .then((items) => active && setStudents(items.map((item) => item.student)))
      .catch((e) => {
        if (!active) return;
        setError("Không thể tải danh sách học sinh để lập báo cáo. Vui lòng thử lại.");
        denied(e);
      });
    return () => {
      active = false;
    };
  }, [classroom.id, denied]);
  if (expired) return <Login onLogin={relogin} />;
  if (!access.canRead)
    return (
      <p className="error" role="alert">
        Tài khoản hiện không được truy cập báo cáo.
      </p>
    );
  return (
    <section className="panel">
      {error && (
        <p className="error" role="alert">
          {error}
        </p>
      )}
      {students === null && !error && <p role="status">Đang tải…</p>}
      {students && (
        <MonthlyReports
          classId={classroom.id}
          students={students}
          canWrite={access.canWrite}
          onDenied={denied}
        />
      )}
    </section>
  );
}
