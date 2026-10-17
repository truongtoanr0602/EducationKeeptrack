import { useEffect, useState, type FormEvent } from "react";
import { api, ApiError } from "./api";
import { today, type Student, type StudentDraft } from "./roster-api";

export function StudentForm({
  student,
  classId,
  canWrite,
  onSaved,
  onCancel,
  onDenied,
}: {
  student?: Student;
  classId: string;
  canWrite: boolean;
  onSaved: () => void;
  onCancel: () => void;
  onDenied: (error: ApiError) => void;
}) {
  const [draft, setDraft] = useState<StudentDraft>({
    name: student?.name ?? "",
    parentName: student?.parentName ?? "",
    phone: student?.phone ?? "",
    email: student?.email ?? "",
    notes: student?.notes ?? "",
  });
  const [savedProfile, setSavedProfile] = useState(student);
  useEffect(() => {
    if (student) setSavedProfile(student);
  }, [student]);
  const [startDate, setStartDate] = useState(today);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!draft.name.trim() || (!student && !startDate)) {
      setError("Vui lòng nhập tên học sinh và ngày bắt đầu.");
      return;
    }
    setBusy(true);
    setError("");
    try {
      const saved = await api<Student>(
        savedProfile ? `/students/${savedProfile.id}` : "/students",
        savedProfile ? "PUT" : "POST",
        { ...draft, version: savedProfile?.version },
      );
      // A failed enrollment must retry this profile, rather than create a second student.
      setSavedProfile(saved);
      if (!student)
        await api(`/classes/${classId}/enrollments`, "POST", {
          studentId: saved.id,
          startDate,
        });
      onSaved();
    } catch (e) {
      setError(
        e instanceof ApiError
          ? Object.values(e.errors ?? {})
              .flat()
              .join(" ") || e.message
          : "Không thể lưu học sinh. Vui lòng thử lại.",
      );
      if (e instanceof ApiError && (e.status === 401 || e.status === 403))
        onDenied(e);
    } finally {
      setBusy(false);
    }
  }
  const labels: Record<keyof StudentDraft, string> = {
    name: "Tên học sinh",
    parentName: "Tên phụ huynh",
    phone: "Điện thoại liên hệ",
    email: "Email liên hệ",
    notes: "Ghi chú",
  };
  const limits = {
    name: 120,
    parentName: 120,
    phone: 30,
    email: 254,
    notes: 1000,
  };
  return (
    <form className="roster-form" onSubmit={submit} noValidate aria-busy={busy}>
      <h3>{student ? "Sửa hồ sơ học sinh" : "Thêm học sinh mới"}</h3>
      {savedProfile && !student && (
        <p className="notice">
          Hồ sơ đã lưu. Tiếp tục lưu để hoàn tất đăng ký lớp.
        </p>
      )}
      {(Object.keys(labels) as (keyof StudentDraft)[]).map((field) => (
        <div className="field" key={field}>
          <label htmlFor={`student-${field}`}>{labels[field]}</label>
          <input
            id={`student-${field}`}
            type={
              field === "email" ? "email" : field === "phone" ? "tel" : "text"
            }
            maxLength={limits[field]}
            required={field === "name"}
            value={draft[field]}
            disabled={busy}
            onChange={(e) => setDraft({ ...draft, [field]: e.target.value })}
          />
        </div>
      ))}
      {!student && (
        <div className="field">
          <label htmlFor="student-start">Ngày bắt đầu</label>
          <input
            id="student-start"
            type="date"
            value={startDate}
            disabled={busy}
            onChange={(e) => setStartDate(e.target.value)}
          />
        </div>
      )}
      {error && (
        <p className="error" role="alert">
          {error}
        </p>
      )}
      <div className="roster-actions">
        <button type="button" disabled={busy} onClick={onCancel}>
          Hủy
        </button>
        <button className="primary" disabled={busy || !canWrite}>
          {busy ? "Đang lưu…" : "Lưu học sinh"}
        </button>
      </div>
    </form>
  );
}
