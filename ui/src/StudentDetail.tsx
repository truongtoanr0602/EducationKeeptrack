import { useEffect, useState } from "react";
import { api, ApiError, type Me } from "./api";
import { aiVisible } from "./access";
import { statusNames, type EnrollmentStatus, type Student } from "./roster-api";
import { classHref, href } from "./router";

type StudentClass = {
  classId: string;
  className: string;
  subject: string;
  status: EnrollmentStatus;
  startDate: string;
  endDate: string | null;
};

// One student across every class: contact details and, per class, the shortcuts the teacher needs
// (attendance, tuition, assessments). AI entries appear only when the account has AI permission.
export function StudentDetail({
  me,
  studentId,
}: {
  me: Me;
  studentId: string;
}) {
  const [student, setStudent] = useState<Student | null>(null);
  const [classes, setClasses] = useState<StudentClass[]>([]);
  const [error, setError] = useState("");
  useEffect(() => {
    let live = true;
    setStudent(null);
    setError("");
    Promise.all([
      api<Student>(`/students/${studentId}`),
      api<StudentClass[]>(`/students/${studentId}/classes`),
    ])
      .then(([s, c]) => {
        if (!live) return;
        setStudent(s);
        setClasses(c);
      })
      .catch((e) => {
        if (live)
          setError(
            e instanceof ApiError && e.status === 404
              ? "Không tìm thấy học sinh."
              : "Không thể tải học sinh. Vui lòng thử lại.",
          );
      });
    return () => {
      live = false;
    };
  }, [studentId]);
  const ai = aiVisible(me.access);
  return (
    <>
      <p>
        <a href={href({ name: "classes" })}>← Lớp & học sinh</a>
      </p>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      {!student && !error && <p role="status">Đang tải…</p>}
      {student && (
        <>
          <div className="page-heading compact">
            <div>
              <span className="eyebrow">HỌC SINH</span>
              <h1>{student.name}</h1>
            </div>
          </div>
          <section className="panel student-contact" aria-label="Liên hệ">
            {student.parentName && <p>Phụ huynh: {student.parentName}</p>}
            {student.phone && (
              <p>
                Điện thoại: <a href={`tel:${student.phone}`}>{student.phone}</a>
              </p>
            )}
            {student.email && <p>Email: {student.email}</p>}
            {student.notes && <p>Ghi chú: {student.notes}</p>}
            {!student.parentName &&
              !student.phone &&
              !student.email &&
              !student.notes && <p>Chưa có thông tin liên hệ.</p>}
            {classes[0] && (
              <a href={classHref(classes[0].classId, "students")}>
                Sửa hồ sơ trong danh sách lớp
              </a>
            )}
          </section>
          <h2 className="report-sub">Các lớp của học sinh</h2>
          {classes.length === 0 && <p>Học sinh chưa ghi danh lớp nào.</p>}
          <ul className="cross-class" aria-label="Lớp của học sinh">
            {classes.map((c) => (
              <li key={c.classId} className="billing-card">
                <div className="billing-row">
                  <span className="billing-who">
                    <strong>{c.className}</strong>
                    <span>
                      {c.subject} · {statusNames[c.status]} · từ {c.startDate}
                      {c.endDate ? ` đến ${c.endDate}` : ""}
                    </span>
                  </span>
                </div>
                <div className="billing-actions">
                  <a href={classHref(c.classId, "sessions")}>Điểm danh</a>
                  <a href={classHref(c.classId, "tuition")}>Học phí</a>
                  <a href={classHref(c.classId, "assessments")}>Đánh giá</a>
                  {ai && (
                    <a
                      href={classHref(
                        c.classId,
                        "personalization",
                        "competency",
                      )}
                    >
                      Hồ sơ năng lực
                    </a>
                  )}
                </div>
              </li>
            ))}
          </ul>
        </>
      )}
    </>
  );
}
