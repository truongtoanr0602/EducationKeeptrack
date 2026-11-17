import { useEffect, useMemo, useState } from "react";
import { api } from "./api";
import type { Student } from "./roster-api";
import { href } from "./router";

// Every student of the teacher in one searchable list (profiles are shared between classes).
export function StudentList() {
  const [students, setStudents] = useState<Student[] | null>(null);
  const [error, setError] = useState("");
  const [query, setQuery] = useState("");
  useEffect(() => {
    let live = true;
    api<Student[]>("/students")
      .then((items) => live && setStudents(items))
      .catch(() => live && setError("Không thể tải danh sách học sinh."));
    return () => {
      live = false;
    };
  }, []);
  const shown = useMemo(() => {
    const q = query.trim().toLocaleLowerCase("vi");
    return (students ?? []).filter(
      (s) =>
        !q ||
        s.name.toLocaleLowerCase("vi").includes(q) ||
        (s.parentName ?? "").toLocaleLowerCase("vi").includes(q),
    );
  }, [students, query]);
  return (
    <section aria-label="Tất cả học sinh">
      <h2 className="report-sub">Tất cả học sinh</h2>
      <label className="field">
        Tìm học sinh
        <input
          type="search"
          value={query}
          placeholder="Tên học sinh hoặc phụ huynh"
          onChange={(e) => setQuery(e.target.value)}
        />
      </label>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      {!students && !error && <p role="status">Đang tải…</p>}
      {students && shown.length === 0 && <p>Không có học sinh phù hợp.</p>}
      <ul className="cross-class" aria-label="Danh sách học sinh">
        {shown.map((s) => (
          <li key={s.id}>
            <a
              className="cross-class-link"
              href={href({ name: "student", studentId: s.id })}
            >
              <strong>{s.name}</strong>
              {s.parentName && <span>Phụ huynh: {s.parentName}</span>}
            </a>
          </li>
        ))}
      </ul>
    </section>
  );
}
