import { useCallback, useEffect, useState } from "react";
import { api, ApiError } from "./api";
import { classHref } from "./router";
import {
  errorText,
  learningPath,
  skillResultStates,
  type AssessmentSkills,
  type Curriculum,
  type SkillResultRow,
  type SkillResultStatus,
} from "./personalization-api";

// T29: per-skill results of an assessment, kept separate from the two overall indicators.
export function SkillResults({
  classId,
  assessmentId,
  canWrite,
  onDenied,
}: {
  classId: string;
  assessmentId: string;
  canWrite: boolean;
  onDenied: (e: ApiError) => void;
}) {
  const base = learningPath(classId);
  const [data, setData] = useState<AssessmentSkills | null>(null);
  const [curriculum, setCurriculum] = useState<Curriculum | null>(null);
  const [map, setMap] = useState<{ skillId: string; taskCount: number }[]>([]);
  const [editingMap, setEditingMap] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const denied = useCallback((e: unknown) => e instanceof ApiError && onDenied(e), [onDenied]);
  const load = useCallback(async () => {
    setError("");
    try {
      const [skills, program] = await Promise.all([
        api<AssessmentSkills>(`${base}/assessments/${assessmentId}/skills`),
        api<Curriculum>(`${base}/curriculum`),
      ]);
      setData(skills);
      setCurriculum(program);
      setMap(skills.skills.map((s) => ({ skillId: s.skillId, taskCount: s.taskCount })));
    } catch (e) {
      setError(errorText(e, "Không thể tải kết quả theo kỹ năng."));
      denied(e);
    }
  }, [base, assessmentId, denied]);
  useEffect(() => {
    void load();
  }, [load]);
  async function saveMap() {
    if (!data) return;
    setBusy(true);
    setError("");
    try {
      const value = await api<AssessmentSkills>(`${base}/assessments/${assessmentId}/skills`, "PUT", { skills: map, version: data.version });
      setData(value);
      setMap(value.skills.map((s) => ({ skillId: s.skillId, taskCount: s.taskCount })));
      setEditingMap(false);
    } catch (e) {
      setError(
        e instanceof ApiError && e.status === 409
          ? "Danh sách kỹ năng của bài đã thay đổi ở phiên khác. Tải lại để xem bản mới; lựa chọn đang nhập được giữ nguyên."
          : errorText(e, "Không thể lưu kỹ năng của bài đánh giá."),
      );
      denied(e);
    } finally {
      setBusy(false);
    }
  }
  if (error && !data)
    return (
      <p className="error" role="alert">
        {error} <button onClick={() => void load()}>Tải lại</button>
      </p>
    );
  if (!data || !curriculum) return <p role="status">Đang tải kết quả theo kỹ năng…</p>;
  if (!curriculum.configured)
    return (
      <section className="card skill-results">
        <h3>Kết quả theo kỹ năng</h3>
        <p className="hint">
          Cấu hình chương trình lớp và kỹ năng để ghi số câu đúng theo từng kỹ năng.{" "}
          <a href={classHref(classId, "personalization", "curriculum")}>Mở chương trình lớp</a>
        </p>
      </section>
    );
  const active = curriculum.skills.filter((s) => !s.archived || map.some((m) => m.skillId === s.id));
  const students = [...new Map(data.results.map((r) => [r.studentId, r.studentName])).entries()];
  return (
    <section className="card skill-results" aria-label="Kết quả theo kỹ năng">
      <div className="card-heading">
        <h3>Kết quả theo kỹ năng</h3>
        {!editingMap && (
          <button disabled={!canWrite || busy} onClick={() => setEditingMap(true)}>
            Gắn kỹ năng cho bài
          </button>
        )}
      </div>
      <p className="hint">
        Nhập số nhiệm vụ và số câu đúng cho từng kỹ năng. Chỉ kết quả đã xác nhận dùng cho hồ sơ năng lực; không suy ra từ hai chỉ số tổng thể.
      </p>
      {editingMap && (
        <fieldset className="skill-map">
          <legend>Kỹ năng bài đánh giá đo</legend>
          {active.map((skill) => {
            const entry = map.find((m) => m.skillId === skill.id);
            const locked = data.skills.find((s) => s.skillId === skill.id)?.locked ?? false;
            return (
              <div key={skill.id} className="skill-map-row">
                <label className="checkbox-label">
                  <input
                    type="checkbox"
                    checked={!!entry}
                    disabled={busy || locked}
                    onChange={(e) =>
                      setMap((list) => (e.target.checked ? [...list, { skillId: skill.id, taskCount: 5 }] : list.filter((m) => m.skillId !== skill.id)))
                    }
                  />
                  {skill.code} · {skill.name}
                  {!skill.taught && <span className="muted"> (chưa dạy)</span>}
                </label>
                {entry && (
                  <label className="inline">
                    Số nhiệm vụ
                    <input
                      type="number"
                      min={1}
                      max={100}
                      value={entry.taskCount}
                      disabled={busy || locked}
                      onChange={(e) => setMap((list) => list.map((m) => (m.skillId === skill.id ? { ...m, taskCount: Number(e.target.value) } : m)))}
                    />
                  </label>
                )}
                {locked && <span className="muted">Đã có kết quả — không đổi</span>}
              </div>
            );
          })}
          <div className="form-actions">
            <button className="primary" disabled={busy} onClick={() => void saveMap()}>
              Lưu kỹ năng của bài
            </button>
            <button
              disabled={busy}
              onClick={() => {
                setEditingMap(false);
                setMap(data.skills.map((s) => ({ skillId: s.skillId, taskCount: s.taskCount })));
              }}
            >
              Huỷ
            </button>
            <button disabled={busy} onClick={() => void load()}>
              Tải lại
            </button>
          </div>
        </fieldset>
      )}
      {error && data && <p className="error" role="alert">{error}</p>}
      {data.skills.length === 0 && !editingMap && <p className="muted">Bài đánh giá chưa gắn kỹ năng.</p>}
      {data.skills.length > 0 &&
        students.map(([studentId, name]) => (
          <div key={studentId} className="skill-student">
            <h4>{name}</h4>
            {data.skills.map((skill) => {
              const row = data.results.find((r) => r.studentId === studentId && r.skillId === skill.skillId);
              return row ? (
                <SkillResultForm
                  key={`${row.studentId}/${row.skillId}/${row.version}`}
                  label={`${skill.code} · ${skill.name}`}
                  taskCount={skill.taskCount}
                  initial={row}
                  path={`${base}/assessments/${assessmentId}/skill-results/${row.studentId}/${row.skillId}`}
                  canWrite={canWrite}
                  onDenied={denied}
                  reloadRow={async () => (await api<AssessmentSkills>(`${base}/assessments/${assessmentId}/skills`)).results.find((r) => r.studentId === row.studentId && r.skillId === row.skillId)}
                  onSaved={(value) => setData((d) => (d ? { ...d, skills: d.skills.map((s) => (s.skillId === value.skillId ? { ...s, locked: true } : s)), results: d.results.map((r) => (r.studentId === value.studentId && r.skillId === value.skillId ? value : r)) } : d))}
                />
              ) : null;
            })}
          </div>
        ))}
    </section>
  );
}

function SkillResultForm({
  label,
  taskCount,
  initial,
  path,
  canWrite,
  onDenied,
  onSaved,
  reloadRow,
}: {
  label: string;
  taskCount: number;
  initial: SkillResultRow;
  path: string;
  canWrite: boolean;
  onDenied: (e: unknown) => void;
  onSaved: (value: SkillResultRow) => void;
  reloadRow: () => Promise<SkillResultRow | undefined>;
}) {
  const [status, setStatus] = useState<SkillResultStatus>(initial.status);
  const [correct, setCorrect] = useState(initial.correct?.toString() ?? "");
  const [note, setNote] = useState(initial.note ?? "");
  const [version, setVersion] = useState(initial.version);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [conflict, setConflict] = useState(false);
  async function refresh() {
    try {
      const row = await reloadRow();
      if (row) setVersion(row.version);
      setConflict(false);
      setError("");
    } catch (e) {
      setError(errorText(e, "Không thể tải lại kết quả."));
      onDenied(e);
    }
  }
  async function send(confirm: boolean) {
    setBusy(true);
    setError("");
    try {
      const value = await api<SkillResultRow>(path, "PUT", {
        status,
        correct: status === "assessed" && correct !== "" ? Number(correct) : null,
        note: note || null,
        version,
        confirm,
      });
      onSaved(value);
    } catch (e) {
      if (e instanceof ApiError && e.status === 409) {
        setConflict(true);
      } else setError(errorText(e, "Không thể lưu kết quả kỹ năng. Nội dung đang nhập được giữ nguyên."));
      onDenied(e);
    } finally {
      setBusy(false);
    }
  }
  const final = initial.finalResult;
  return (
    <div className="skill-result">
      <div className="skill-result-head">
        <strong>{label}</strong>
        <span className="muted">
          {final
            ? `Đã xác nhận: ${final.status === "assessed" ? `${final.correct}/${final.taskCount}` : skillResultStates[final.status]}`
            : "Chưa xác nhận"}
        </span>
      </div>
      <div className="skill-result-fields">
        <label>
          Trạng thái
          <select value={status} disabled={!canWrite || busy} onChange={(e) => setStatus(e.target.value as SkillResultStatus)}>
            {Object.entries(skillResultStates).map(([id, text]) => (
              <option key={id} value={id}>
                {text}
              </option>
            ))}
          </select>
        </label>
        <label>
          Số câu đúng / {taskCount}
          <input
            type="number"
            min={0}
            max={taskCount}
            value={correct}
            disabled={!canWrite || busy || status !== "assessed"}
            onChange={(e) => setCorrect(e.target.value)}
          />
        </label>
        <label className="grow">
          Ghi chú lỗi quan sát được
          <input maxLength={500} value={note} disabled={!canWrite || busy} onChange={(e) => setNote(e.target.value)} />
        </label>
      </div>
      <div className="form-actions">
        <button disabled={!canWrite || busy} onClick={() => void send(false)}>
          Lưu nháp
        </button>
        <button className="primary" disabled={!canWrite || busy} onClick={() => void send(true)}>
          Xác nhận kết quả
        </button>
      </div>
      {conflict && (
        <p className="notice" role="alert">
          Kết quả đã thay đổi ở phiên khác. Nội dung đang nhập được giữ nguyên.{" "}
          <button onClick={() => void refresh()}>Tải lại phiên bản kết quả</button>
        </p>
      )}
      {error && <p className="error" role="alert">{error}</p>}
    </div>
  );
}
