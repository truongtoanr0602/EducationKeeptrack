import { useEffect, useRef, useState, type FormEvent } from "react";
import {
  api,
  ApiError,
  limits,
  type ClassDraft,
  type TeacherClass,
} from "./api";

export function CreateClass({
  initial,
  onClose,
  onSaved,
  onExpired,
}: {
  initial: ClassDraft;
  onClose: () => void;
  onSaved: (item: TeacherClass) => void;
  onExpired: (draft: ClassDraft) => void;
}) {
  const dialog = useRef<HTMLDialogElement>(null);
  const [draft, setDraft] = useState(initial);
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    dialog.current?.showModal();
  }, []);
  async function submit(event: FormEvent) {
    event.preventDefault();
    const validation: Record<string, string[]> = {};
    if (!draft.name.trim()) validation.name = ["Vui lòng nhập tên lớp."];
    if (!draft.subject.trim()) validation.subject = ["Vui lòng nhập môn học."];
    setErrors(validation);
    setError("");
    if (Object.keys(validation).length) return;
    setBusy(true);
    try {
      onSaved(await api<TeacherClass>("/classes", "POST", draft));
    } catch (e) {
      if (e instanceof ApiError && e.status === 401) {
        onExpired(draft);
        return;
      }
      if (e instanceof ApiError && e.errors) setErrors(e.errors);
      setError(
        e instanceof Error ? e.message : "Không thể lưu lớp. Vui lòng thử lại.",
      );
    } finally {
      setBusy(false);
    }
  }
  return (
    <dialog
      ref={dialog}
      onCancel={(e) => {
        e.preventDefault();
        if (!busy) onClose();
      }}
      aria-labelledby="create-title"
    >
      <form noValidate onSubmit={submit} aria-busy={busy}>
        <div className="dialog-top">
          <span className="eyebrow">LỚP HỌC</span>
          <button
            type="button"
            className="icon-button"
            aria-label="Đóng"
            disabled={busy}
            onClick={onClose}
          >
            ×
          </button>
        </div>
        <h2 id="create-title">Tạo lớp mới</h2>
        <p>Sắp xếp lớp học theo cách của bạn.</p>
        {(["name", "subject", "description"] as const).map((field) => (
          <div key={field} className="field">
            <label htmlFor={field}>
              {field === "name"
                ? "Tên lớp"
                : field === "subject"
                  ? "Môn học"
                  : "Mô tả (tùy chọn)"}
            </label>
            {field === "description" ? (
              <textarea
                id={field}
                rows={4}
                maxLength={limits[field]}
                value={draft[field]}
                disabled={busy}
                aria-invalid={!!errors[field]}
                aria-describedby={`${field}-help`}
                onChange={(e) =>
                  setDraft({ ...draft, [field]: e.target.value })
                }
              />
            ) : (
              <input
                autoFocus={field === "name"}
                id={field}
                required
                maxLength={limits[field]}
                value={draft[field]}
                disabled={busy}
                aria-invalid={!!errors[field]}
                aria-describedby={`${field}-help`}
                onChange={(e) =>
                  setDraft({ ...draft, [field]: e.target.value })
                }
              />
            )}
            <small
              id={`${field}-help`}
              className={errors[field] ? "error" : "hint"}
            >
              {errors[field]?.join(" ") ?? `Tối đa ${limits[field]} ký tự.`}
            </small>
          </div>
        ))}
        {error && (
          <p role="alert" className="error">
            {error}
          </p>
        )}
        <div className="dialog-actions">
          <button type="button" disabled={busy} onClick={onClose}>
            Hủy
          </button>
          <button className="primary" disabled={busy}>
            {busy ? (
              <>
                <span className="spinner" /> Đang lưu…
              </>
            ) : (
              "Lưu lớp"
            )}
          </button>
        </div>
      </form>
    </dialog>
  );
}
