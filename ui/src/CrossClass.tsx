import type { TeacherClass } from "./api";
import { classHref, type Section } from "./router";

const copy = {
  today: {
    title: "Hôm nay",
    eyebrow: "BUỔI DẠY",
    hint: "Chọn lớp để xem buổi học và điểm danh.",
    action: "Điểm danh",
    section: "sessions" as Section,
  },
  billing: {
    title: "Hóa đơn",
    eyebrow: "HỌC PHÍ",
    hint: "Chọn lớp để xem học phí còn nợ và ghi nhận thu tiền.",
    action: "Học phí & thu tiền",
    section: "tuition" as Section,
  },
  reports: {
    title: "Báo cáo",
    eyebrow: "BÁO CÁO THÁNG",
    hint: "Chọn lớp để xem và xuất báo cáo tháng.",
    action: "Báo cáo tháng",
    section: "reports" as Section,
  },
};

// Interim cross-class entry points: each tab lists the classes and deep-links into the existing
// class screen. The dedicated aggregate screens replace the body of this component later (#44, #45, #47).
export function CrossClass({
  kind,
  classes,
  bare,
}: {
  kind: keyof typeof copy;
  classes: TeacherClass[];
  bare?: boolean;
}) {
  const c = copy[kind];
  return (
    <>
      {!bare && (
        <div className="page-heading compact">
          <div>
            <span className="eyebrow">{c.eyebrow}</span>
            <h1>{c.title}</h1>
            <p>{c.hint}</p>
          </div>
        </div>
      )}
      {classes.length ? (
        <ul className="cross-class" aria-label="Danh sách lớp">
          {classes.map((item) => (
            <li key={item.id}>
              <a
                className="cross-class-link"
                href={classHref(item.id, c.section)}
              >
                <strong>{item.name}</strong>
                <span>{item.subject}</span>
                <span className="cross-class-action">{c.action} →</span>
              </a>
            </li>
          ))}
        </ul>
      ) : (
        <section className="empty">
          <h2>Chưa có lớp nào</h2>
          <p>Tạo lớp trong mục Lớp & học sinh để bắt đầu.</p>
        </section>
      )}
    </>
  );
}
