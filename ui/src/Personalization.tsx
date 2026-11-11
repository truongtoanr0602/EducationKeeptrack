import { useCallback, useEffect, useState, type FormEvent } from "react";
import { api, ApiError, type Me, type TeacherClass } from "./api";
import { useAccess } from "./access";
import { Login } from "./Login";
import { ReviewPlans } from "./ReviewPlans";
import { classHref, personalTabs, type PersonalTab } from "./router";
import {
  competencyStates,
  difficulties,
  errorText,
  learningPath,
  viDate,
  type CompetencyProfile,
  type Curriculum,
  type Material,
} from "./personalization-api";

type Shared = {
  classroom: TeacherClass;
  curriculum: Curriculum;
  canWrite: boolean;
  denied: (e: unknown) => void;
  reload: () => Promise<Curriculum | null>;
  onCurriculum: (value: Curriculum) => void;
};

export function Personalization({ classroom, me, tab }: { classroom: TeacherClass; me: Me; tab: PersonalTab }) {
  const { access, expired, denied, relogin, canWrite } = useAccess(me);
  const [curriculum, setCurriculum] = useState<Curriculum | null>(null);
  const [error, setError] = useState("");
  const reload = useCallback(async () => {
    try {
      const value = await api<Curriculum>(`${learningPath(classroom.id)}/curriculum`);
      setCurriculum(value);
      setError("");
      return value;
    } catch (e) {
      setError(errorText(e, "Không thể tải chương trình lớp. Vui lòng thử lại."));
      denied(e);
      return null;
    }
  }, [classroom.id, denied]);
  useEffect(() => {
    void reload();
  }, [reload]);
  return (
    <section className="panel personalization" aria-label="Cá nhân hoá học tập">
      <p className="lead">
        Theo dõi kỹ năng của từng học sinh bằng kết quả đã xác nhận, rồi lập kế hoạch ôn tập tuần do giáo viên duyệt.
      </p>
      <nav aria-label="Mục cá nhân hoá">
        <ul className="tabs">
          {personalTabs.map((item) => (
            <li key={item.id}>
              <a href={classHref(classroom.id, "personalization", item.id)} aria-current={tab === item.id ? "page" : undefined}>
                {item.label}
              </a>
            </li>
          ))}
        </ul>
      </nav>
      {!access.canWrite && access.canRead && (
        <p className="notice">Chỉ xem dữ liệu đã lưu; cần quyền sử dụng còn hạn để thêm hoặc sửa.</p>
      )}
      {expired && <Login onLogin={relogin} />}
      {error && (
        <p className="error" role="alert">
          {error} <button onClick={() => void reload()}>Tải lại</button>
        </p>
      )}
      {!curriculum && !error && <p role="status">Đang tải…</p>}
      {curriculum && (
        <div className="tab-panel">
          {tab === "curriculum" && (
            <CurriculumTab classroom={classroom} curriculum={curriculum} canWrite={canWrite} denied={denied} reload={reload} onCurriculum={setCurriculum} />
          )}
          {tab !== "curriculum" && !curriculum.configured && (
            <div className="empty">
              <h2>Chưa có chương trình lớp</h2>
              <p>Cấu hình bộ sách, quy tắc mức đạt và kỹ năng trước khi dùng mục này.</p>
              <a className="button-link primary" href={classHref(classroom.id, "personalization", "curriculum")}>
                Cấu hình chương trình lớp
              </a>
            </div>
          )}
          {tab === "materials" && curriculum.configured && (
            <MaterialsTab classroom={classroom} curriculum={curriculum} canWrite={canWrite} denied={denied} reload={reload} onCurriculum={setCurriculum} />
          )}
          {tab === "competency" && curriculum.configured && <CompetencyTab classroom={classroom} denied={denied} />}
          {tab === "plans" && curriculum.configured && (
            <ReviewPlans classroom={classroom} curriculum={curriculum} canWrite={canWrite} denied={denied} />
          )}
        </div>
      )}
    </section>
  );
}

function ConflictNotice({ onReload, label }: { onReload: () => void; label: string }) {
  return (
    <p className="notice" role="alert">
      Dữ liệu đã thay đổi ở phiên khác. Nội dung đang nhập được giữ nguyên.{" "}
      <button onClick={onReload}>{label}</button>
    </p>
  );
}

// ---- T27 ----
function CurriculumTab({ classroom, curriculum, canWrite, denied, reload, onCurriculum }: Shared) {
  const fromServer = (c: Curriculum) => ({
    subject: c.configured ? c.subject : classroom.subject,
    grade: c.grade,
    term: c.term,
    textbook: c.textbook,
    masteryThreshold: c.rule.threshold,
    minTasks: c.rule.minTasks,
    requiredAttempts: c.rule.requiredAttempts,
  });
  const [draft, setDraft] = useState(() => fromServer(curriculum));
  const [version, setVersion] = useState(curriculum.version);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [conflict, setConflict] = useState(false);
  const [saved, setSaved] = useState("");
  async function save(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    setSaved("");
    try {
      const value = await api<Curriculum>(`${learningPath(classroom.id)}/curriculum`, "PUT", { ...draft, version });
      onCurriculum(value);
      setVersion(value.version);
      setConflict(false);
      setSaved("Đã lưu chương trình lớp.");
    } catch (e) {
      if (e instanceof ApiError && e.status === 409) setConflict(true);
      else setError(errorText(e, "Không thể lưu chương trình lớp. Nội dung đang nhập được giữ nguyên."));
      denied(e);
    } finally {
      setBusy(false);
    }
  }
  const field = (key: keyof typeof draft, label: string, props: Record<string, unknown> = {}) => (
    <label>
      {label}
      <input
        value={draft[key]}
        disabled={!canWrite || busy}
        onChange={(e) =>
          setDraft((d) => ({ ...d, [key]: typeof d[key] === "number" ? Number(e.target.value) : e.target.value }))
        }
        {...props}
      />
    </label>
  );
  return (
    <div className="stack">
      <form className="card form-grid" onSubmit={(e) => void save(e)} aria-label="Cấu hình chương trình lớp">
        <h2>Chương trình lớp</h2>
        <p className="hint full-row">
          Bộ sách và quy tắc mức đạt áp dụng riêng cho lớp này. Mỗi lần lưu tạo một phiên bản mới; kết quả cũ vẫn giữ căn cứ.
        </p>
        {field("subject", "Môn học", { required: true, maxLength: 80 })}
        {field("grade", "Khối/lớp", { required: true, maxLength: 40, placeholder: "Ví dụ: Lớp 2" })}
        {field("term", "Học kỳ", { required: true, maxLength: 40, placeholder: "Ví dụ: Học kỳ 1" })}
        {field("textbook", "Bộ sách áp dụng", { required: true, maxLength: 160 })}
        <fieldset className="full-row rule-fields">
          <legend>Quy tắc mức đạt {curriculum.configured && <span className="muted">· phiên bản {curriculum.rule.version}</span>}</legend>
          {field("masteryThreshold", "Ngưỡng đạt (%)", { type: "number", min: 1, max: 100, required: true })}
          {field("minTasks", "Số nhiệm vụ tối thiểu mỗi lần", { type: "number", min: 1, max: 50, required: true })}
          {field("requiredAttempts", "Số lần đánh giá cần có", { type: "number", min: 1, max: 5, required: true })}
          <p className="hint">
            Đạt khi mỗi lần trong {draft.requiredAttempts} lần gần nhất có ít nhất {draft.minTasks} nhiệm vụ và đúng từ {draft.masteryThreshold}% trở lên.
          </p>
        </fieldset>
        <div className="full-row form-actions">
          <button className="primary" disabled={!canWrite || busy}>
            {curriculum.configured ? "Lưu chương trình lớp" : "Tạo chương trình lớp"}
          </button>
          {saved && <span role="status" className="success-text">{saved}</span>}
        </div>
        {conflict && (
          <div className="full-row">
            <ConflictNotice
              label="Tải lại chương trình lớp"
              onReload={() =>
                void reload().then((value) => {
                  if (value) setVersion(value.version);
                  setConflict(false);
                })
              }
            />
          </div>
        )}
        {error && <p className="error full-row" role="alert">{error}</p>}
      </form>
      {curriculum.configured && (
        <SkillsEditor classroom={classroom} curriculum={curriculum} canWrite={canWrite} denied={denied} reload={reload} onCurriculum={onCurriculum} />
      )}
    </div>
  );
}

type SkillDraft = { code: string; name: string; strand: string; taught: boolean; archived: boolean };
const emptySkill: SkillDraft = { code: "", name: "", strand: "", taught: false, archived: false };

function SkillsEditor({ classroom, curriculum, canWrite, denied, reload, onCurriculum }: Shared) {
  const [editing, setEditing] = useState<string | null>(null);
  const [draft, setDraft] = useState<SkillDraft>(emptySkill);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [conflict, setConflict] = useState(false);
  async function send(skillId: string | null, body: SkillDraft) {
    setBusy(true);
    setError("");
    try {
      const path = `${learningPath(classroom.id)}/curriculum/skills${skillId ? `/${skillId}` : ""}`;
      const value = await api<Curriculum>(path, skillId ? "PUT" : "POST", { ...body, version: curriculum.version });
      onCurriculum(value);
      setConflict(false);
      return true;
    } catch (e) {
      if (e instanceof ApiError && e.status === 409) setConflict(true);
      else setError(errorText(e, "Không thể lưu kỹ năng. Nội dung đang nhập được giữ nguyên."));
      denied(e);
      return false;
    } finally {
      setBusy(false);
    }
  }
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (await send(editing, draft)) {
      setEditing(null);
      setDraft(emptySkill);
    }
  }
  const strands = [...new Set(curriculum.skills.map((s) => s.strand))];
  return (
    <section className="card" aria-label="Kỹ năng của lớp">
      <h2>Kỹ năng</h2>
      <p className="hint">
        Tách mạch nội dung thành kỹ năng có thể đánh giá riêng (ví dụ "Cộng qua 10 trong phạm vi 20"). Kỹ năng chưa dạy không bị kết luận là chưa đạt.
      </p>
      <form className="skill-form" onSubmit={(e) => void submit(e)} aria-label={editing ? "Sửa kỹ năng" : "Thêm kỹ năng"}>
        <label>
          Mã
          <input required maxLength={20} value={draft.code} disabled={!canWrite || busy} onChange={(e) => setDraft({ ...draft, code: e.target.value })} />
        </label>
        <label className="grow">
          Tên kỹ năng
          <input required maxLength={200} value={draft.name} disabled={!canWrite || busy} onChange={(e) => setDraft({ ...draft, name: e.target.value })} />
        </label>
        <label>
          Mạch nội dung
          <input maxLength={120} list="strands" value={draft.strand} disabled={!canWrite || busy} onChange={(e) => setDraft({ ...draft, strand: e.target.value })} />
          <datalist id="strands">
            {strands.map((s) => (
              <option key={s} value={s} />
            ))}
          </datalist>
        </label>
        <label className="checkbox-label">
          <input type="checkbox" checked={draft.taught} disabled={!canWrite || busy} onChange={(e) => setDraft({ ...draft, taught: e.target.checked })} />
          Đã dạy
        </label>
        <div className="form-actions">
          <button className="primary" disabled={!canWrite || busy}>
            {editing ? "Lưu kỹ năng" : "Thêm kỹ năng"}
          </button>
          {editing && (
            <button type="button" onClick={() => { setEditing(null); setDraft(emptySkill); }}>
              Huỷ sửa
            </button>
          )}
        </div>
      </form>
      {conflict && (
        <ConflictNotice label="Tải lại kỹ năng" onReload={() => void reload().then(() => setConflict(false))} />
      )}
      {error && <p className="error" role="alert">{error}</p>}
      {curriculum.skills.length === 0 ? (
        <p className="muted">Chưa có kỹ năng nào.</p>
      ) : (
        <div className="table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th scope="col">Mã</th>
                <th scope="col">Kỹ năng</th>
                <th scope="col">Mạch nội dung</th>
                <th scope="col">Tiến độ</th>
                <th scope="col">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {curriculum.skills.map((skill) => (
                <tr key={skill.id} className={skill.archived ? "archived" : undefined}>
                  <td>{skill.code}</td>
                  <td>{skill.name}{skill.archived && <span className="badge"> Ngừng dùng</span>}</td>
                  <td>{skill.strand || "—"}</td>
                  <td>{skill.taught ? `Đã dạy${skill.taughtOn ? ` · ${viDate(skill.taughtOn)}` : ""}` : "Chưa dạy"}</td>
                  <td className="row-actions">
                    <button disabled={!canWrite || busy} onClick={() => void send(skill.id, { ...skill, taught: !skill.taught })}>
                      {skill.taught ? "Đánh dấu chưa dạy" : "Đánh dấu đã dạy"}
                    </button>
                    <button disabled={!canWrite || busy} onClick={() => { setEditing(skill.id); setDraft({ ...skill }); }}>
                      Sửa
                    </button>
                    <button disabled={!canWrite || busy} onClick={() => void send(skill.id, { ...skill, archived: !skill.archived })}>
                      {skill.archived ? "Dùng lại" : "Ngừng dùng"}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

// ---- T28 ----
type MaterialDraft = { skillId: string; title: string; content: string; minutes: number; difficulty: Material["difficulty"] };

function MaterialsTab({ classroom, curriculum, canWrite, denied }: Shared) {
  const active = curriculum.skills.filter((s) => !s.archived);
  const empty: MaterialDraft = { skillId: active[0]?.id ?? "", title: "", content: "", minutes: 10, difficulty: "standard" };
  const [items, setItems] = useState<Material[] | null>(null);
  const [draft, setDraft] = useState<MaterialDraft>(empty);
  const [editing, setEditing] = useState<Material | null>(null);
  const [filter, setFilter] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [conflict, setConflict] = useState(false);
  const path = `${learningPath(classroom.id)}/materials`;
  const load = useCallback(async () => {
    try {
      const list = await api<Material[]>(path);
      setItems(list);
      setEditing((current) => (current ? (list.find((x) => x.id === current.id) ?? current) : current));
    } catch (e) {
      setError(errorText(e, "Không thể tải học liệu."));
      denied(e);
    }
  }, [path, denied]);
  useEffect(() => {
    void load();
  }, [load]);
  async function act(run: () => Promise<Material>, reset: boolean) {
    setBusy(true);
    setError("");
    try {
      const saved = await run();
      setItems((list) => [...(list ?? []).filter((x) => x.id !== saved.id), saved].sort((a, b) => a.title.localeCompare(b.title, "vi")));
      setConflict(false);
      if (reset) {
        setEditing(null);
        setDraft(empty);
      }
    } catch (e) {
      if (e instanceof ApiError && e.status === 409) setConflict(true);
      else setError(errorText(e, "Không thể lưu học liệu. Nội dung đang nhập được giữ nguyên."));
      denied(e);
    } finally {
      setBusy(false);
    }
  }
  const skillName = (id: string) => {
    const s = curriculum.skills.find((x) => x.id === id);
    return s ? `${s.code} · ${s.name}` : "Kỹ năng không còn trong chương trình";
  };
  const shown = (items ?? []).filter((x) => !filter || x.skillId === filter);
  return (
    <div className="stack">
      <form
        className="card form-grid"
        aria-label={editing ? "Sửa học liệu" : "Thêm học liệu"}
        onSubmit={(e) => {
          e.preventDefault();
          void act(
            () => api<Material>(editing ? `${path}/${editing.id}` : path, editing ? "PUT" : "POST", { ...draft, version: editing?.version ?? 0 }),
            true,
          );
        }}
      >
        <h2>{editing ? "Sửa học liệu" : "Thêm học liệu"}</h2>
        {editing?.status === "approved" && (
          <p className="notice full-row">Sửa nội dung học liệu đã duyệt sẽ đưa học liệu về dự thảo để duyệt lại.</p>
        )}
        <label>
          Kỹ năng
          <select required value={draft.skillId} disabled={!canWrite || busy} onChange={(e) => setDraft({ ...draft, skillId: e.target.value })}>
            {active.map((s) => (
              <option key={s.id} value={s.id}>
                {s.code} · {s.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          Tên học liệu
          <input required maxLength={200} value={draft.title} disabled={!canWrite || busy} onChange={(e) => setDraft({ ...draft, title: e.target.value })} />
        </label>
        <label className="full-row">
          Nội dung hoặc đường dẫn
          <textarea required rows={3} maxLength={2000} value={draft.content} disabled={!canWrite || busy} onChange={(e) => setDraft({ ...draft, content: e.target.value })} />
        </label>
        <label>
          Thời lượng dự kiến (phút)
          <input type="number" min={1} max={240} required value={draft.minutes} disabled={!canWrite || busy} onChange={(e) => setDraft({ ...draft, minutes: Number(e.target.value) })} />
        </label>
        <label>
          Độ khó
          <select value={draft.difficulty} disabled={!canWrite || busy} onChange={(e) => setDraft({ ...draft, difficulty: e.target.value as Material["difficulty"] })}>
            {Object.entries(difficulties).map(([id, label]) => (
              <option key={id} value={id}>
                {label}
              </option>
            ))}
          </select>
        </label>
        <div className="full-row form-actions">
          <button className="primary" disabled={!canWrite || busy || !active.length}>
            {editing ? "Lưu học liệu" : "Thêm học liệu"}
          </button>
          {editing && (
            <button type="button" onClick={() => { setEditing(null); setDraft(empty); }}>
              Huỷ sửa
            </button>
          )}
        </div>
        {!active.length && <p className="hint full-row">Thêm kỹ năng trong chương trình lớp trước.</p>}
        {conflict && (
          <div className="full-row">
            <ConflictNotice label="Tải lại học liệu" onReload={() => void load().then(() => setConflict(false))} />
          </div>
        )}
        {error && <p className="error full-row" role="alert">{error}</p>}
      </form>
      <section className="card" aria-label="Kho học liệu">
        <div className="card-heading">
          <h2>Kho học liệu</h2>
          <label>
            Lọc theo kỹ năng
            <select value={filter} onChange={(e) => setFilter(e.target.value)}>
              <option value="">Tất cả kỹ năng</option>
              {curriculum.skills.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.code} · {s.name}
                </option>
              ))}
            </select>
          </label>
        </div>
        {items === null && <p role="status">Đang tải…</p>}
        {items && shown.length === 0 && <p className="muted">Chưa có học liệu.</p>}
        <ul className="item-list">
          {shown.map((m) => (
            <li key={m.id} className="item">
              <div>
                <span className={`badge ${m.status}`}>{m.status === "approved" ? "Đã duyệt" : "Dự thảo"}</span>
                <h3>{m.title}</h3>
                <p className="muted">
                  {skillName(m.skillId)} · {m.minutes} phút · {difficulties[m.difficulty]}
                </p>
                <p className="pre">{m.content}</p>
              </div>
              <div className="row-actions">
                {m.status === "draft" && (
                  <button className="primary" disabled={!canWrite || busy} onClick={() => void act(() => api<Material>(`${path}/${m.id}/approve`, "POST", { version: m.version }), false)}>
                    Duyệt học liệu
                  </button>
                )}
                <button
                  disabled={!canWrite || busy}
                  onClick={() => {
                    setEditing(m);
                    setDraft({ skillId: m.skillId, title: m.title, content: m.content, minutes: m.minutes, difficulty: m.difficulty });
                  }}
                >
                  Sửa
                </button>
              </div>
            </li>
          ))}
        </ul>
      </section>
    </div>
  );
}

// ---- T30 ----
function CompetencyTab({ classroom, denied }: { classroom: TeacherClass; denied: (e: unknown) => void }) {
  const [profile, setProfile] = useState<CompetencyProfile | null>(null);
  const [studentId, setStudentId] = useState(() => new URLSearchParams(window.location.search).get("student") ?? "");
  const [error, setError] = useState("");
  const load = useCallback(async () => {
    setError("");
    try {
      setProfile(await api<CompetencyProfile>(`${learningPath(classroom.id)}/competency`));
    } catch (e) {
      setError(errorText(e, "Không thể tải hồ sơ năng lực."));
      denied(e);
    }
  }, [classroom.id, denied]);
  useEffect(() => {
    void load();
  }, [load]);
  const students = profile?.students ?? [];
  const selected = students.find((s) => s.studentId === studentId) ?? students[0];
  useEffect(() => {
    if (!selected) return;
    const url = new URL(window.location.href);
    url.searchParams.set("student", selected.studentId);
    window.history.replaceState(null, "", url);
  }, [selected]);
  if (error)
    return (
      <p className="error" role="alert">
        {error} <button onClick={() => void load()}>Tải lại</button>
      </p>
    );
  if (!profile) return <p role="status">Đang tải…</p>;
  const rule = profile.rule!;
  return (
    <div className="stack">
      <section className="card">
        <div className="card-heading">
          <h2>Hồ sơ năng lực</h2>
          <button onClick={() => void load()}>Tải lại hồ sơ</button>
        </div>
        <p className="hint">
          Quy tắc phiên bản {rule.version}: đạt khi {rule.requiredAttempts} lần đánh giá gần nhất, mỗi lần ít nhất {rule.minTasks} nhiệm vụ, đều đúng từ {rule.threshold}% trở lên.
          Chỉ dùng kết quả theo kỹ năng đã xác nhận; không cộng dồn nhiều lần.
        </p>
        {students.length === 0 ? (
          <p className="muted">Lớp chưa có học sinh đang theo học.</p>
        ) : (
          <label>
            Học sinh
            <select value={selected?.studentId} onChange={(e) => setStudentId(e.target.value)}>
              {students.map((s) => (
                <option key={s.studentId} value={s.studentId}>
                  {s.name}
                </option>
              ))}
            </select>
          </label>
        )}
        {students.length > 0 && (
          <div className="summary-row" aria-label="Tổng hợp mức đạt">
            {students.map((s) => (
              <div key={s.studentId} className="summary-card">
                <strong>{s.name}</strong>
                {(["mastered", "not_mastered", "insufficient"] as const).map((state) => (
                  <span key={state} className={`state ${state}`}>
                    {competencyStates[state]}: {s.skills.filter((k) => k.state === state).length}
                  </span>
                ))}
              </div>
            ))}
          </div>
        )}
      </section>
      {selected && (
        <section className="card" aria-label={`Kỹ năng của ${selected.name}`}>
          <h2>{selected.name}</h2>
          {selected.skills.length === 0 && <p className="muted">Chương trình lớp chưa có kỹ năng đang dùng.</p>}
          <ul className="item-list">
            {selected.skills.map((skill) => (
              <li key={skill.skillId} className="item competency-item">
                <div>
                  <span className={`state ${skill.state}`}>{competencyStates[skill.state]}</span>
                  <h3>
                    {skill.code} · {skill.name}
                  </h3>
                  {skill.strand && <p className="muted">{skill.strand}</p>}
                  {skill.state === "insufficient" && (
                    <p className="hint">
                      Cần thêm {skill.missingAttempts} lần đánh giá có ít nhất {rule.minTasks} nhiệm vụ đúng kỹ năng này trước khi kết luận.
                    </p>
                  )}
                  {skill.state === "not_taught" && <p className="hint">Kỹ năng chưa được đánh dấu đã dạy nên không kết luận mức đạt.</p>}
                </div>
                {skill.evidence.length > 0 && (
                  <details>
                    <summary>Bằng chứng ({skill.evidence.length})</summary>
                    <table className="data-table">
                      <thead>
                        <tr>
                          <th scope="col">Ngày</th>
                          <th scope="col">Bài đánh giá</th>
                          <th scope="col">Kết quả</th>
                          <th scope="col">Căn cứ</th>
                        </tr>
                      </thead>
                      <tbody>
                        {skill.evidence.map((e) => (
                          <tr key={e.assessmentId}>
                            <td>{viDate(e.date)}</td>
                            <td>{e.title}</td>
                            <td>
                              {e.status === "assessed" ? `${e.correct}/${e.taskCount} (${Math.round(((e.correct ?? 0) * 100) / e.taskCount)}%)` : "Không làm bài"}
                            </td>
                            <td>{e.counted ? (e.meetsThreshold ? "Tính · đạt ngưỡng" : "Tính · dưới ngưỡng") : e.excluded}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </details>
                )}
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}
