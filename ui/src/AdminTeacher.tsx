import { useCallback, useEffect, useState } from "react";
import { api, ApiError } from "./api";
import { statusLabels, vnDateTime, type TeacherSummary } from "./admin-api";

export function TeacherDetail({
  id,
  onClose,
  onExpired,
}: {
  id: string;
  onClose: () => void;
  onChanged: () => void;
  onExpired: () => void;
}) {
  const [teacher, setTeacher] = useState<TeacherSummary | null>(null);
  const [error, setError] = useState("");
  const load = useCallback(async () => {
    setError("");
    try {
      setTeacher(await api<TeacherSummary>(`/admin/teachers/${id}`));
    } catch (e) {
      if (e instanceof ApiError && e.status === 401) onExpired();
      else setError(e instanceof Error ? e.message : "Không thể tải hồ sơ.");
    }
  }, [id, onExpired]);
  useEffect(() => {
    void load();
  }, [load]);
  return (
    <section className="class-card admin-detail" aria-label="Hồ sơ giáo viên">
      <div className="dialog-top">
        <h2>{teacher?.email ?? "Hồ sơ giáo viên"}</h2>
        <button onClick={onClose}>Đóng</button>
      </div>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      {teacher && (
        <dl>
          <dt>Email</dt>
          <dd>
            {teacher.email} ·{" "}
            {teacher.emailConfirmed ? "đã xác minh" : "chưa xác minh"}
          </dd>
          <dt>Trạng thái</dt>
          <dd>
            {teacher.isBlocked ? "Đã khóa" : statusLabels[teacher.status]}
          </dd>
          <dt>Đăng ký lúc</dt>
          <dd>{vnDateTime(teacher.registeredAt)}</dd>
        </dl>
      )}
    </section>
  );
}
