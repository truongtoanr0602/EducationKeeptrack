import { useCallback, useEffect, useMemo, useState, type FormEvent } from "react";
import { api, ApiError, upload, type TeacherClass } from "./api";
import { answerText, levelLabel, levels, questionTypes, stemParts, type Question, type QuestionBank, type QuestionLevel, type QuestionType } from "./bank-api";
import { errorText, learningPath } from "./personalization-api";
import "./bank.css";

type Props = { classroom: TeacherClass; canWrite: boolean; denied: (e: unknown) => void };

type Draft = {
  code: string;
  type: QuestionType;
  level: QuestionLevel;
  primarySkillId: string;
  secondarySkillIds: string[];
  lessonNo: string;
  stem: string;
  stemImage: string;
  options: { key: string; text: string; image: string }[];
  chips: string;
  answer: string;
  archived: boolean;
  version: number;
};

const letters = ["A", "B", "C", "D"];
const blank = (skill: string): Draft => ({
  code: "", type: "single_choice", level: "nhan_biet", primarySkillId: skill, secondarySkillIds: [], lessonNo: "", stem: "", stemImage: "",
  options: letters.map((key) => ({ key, text: "", image: "" })), chips: "", answer: "", archived: false, version: 0,
});

function toDraft(q: Question): Draft {
  return {
    code: q.code, type: q.type, level: q.level, primarySkillId: q.primarySkillId, secondarySkillIds: q.secondarySkillIds,
    lessonNo: q.lessonNo?.toString() ?? "", stem: q.stem, stemImage: q.stemImage ?? "",
    options: letters.map((key) => { const o = q.options.find((x) => x.key === key); return { key, text: o?.text ?? "", image: o?.image ?? "" }; }),
    chips: q.type === "fill_blanks" ? q.options.map((x) => x.text).join("; ") : "",
    answer: q.type === "single_choice" ? q.answer[0]?.[0] ?? "" : q.answer.map((x) => x.join("/")).join("; "),
    archived: q.archived, version: q.version,
  };
}

function toBody(d: Draft) {
  const fill = d.type === "fill_blanks";
  const options = fill
    ? d.chips.split(";").map((x) => x.trim()).filter(Boolean).map((text, i) => ({ key: String(i + 1), text, image: null }))
    : d.options.filter((o) => o.text.trim() || o.image.trim()).map((o) => ({ key: o.key, text: o.text.trim(), image: o.image.trim() || null }));
  const answer = fill
    ? d.answer.trim() ? d.answer.split(";").map((x) => x.split("/").map((v) => v.trim()).filter(Boolean)) : []
    : [[d.answer.trim().toUpperCase()]];
  return {
    code: d.code, type: d.type, level: d.level, primarySkillId: d.primarySkillId, secondarySkillIds: d.secondarySkillIds,
    lessonNo: d.lessonNo ? Number(d.lessonNo) : null, stem: d.stem, stemImage: d.stemImage.trim() || null, options, answer,
    archived: d.archived, version: d.version,
  };
}

// "Ngân hàng câu hỏi" (#79): filter, read and edit the class's questions; images are uploaded by file name.
export function QuestionBankTab({ classroom, canWrite, denied }: Props) {
  const [bank, setBank] = useState<QuestionBank | null>(null);
  const [error, setError] = useState("");
  const [level, setLevel] = useState("");
  const [skill, setSkill] = useState("");
  const [search, setSearch] = useState("");
  const [showArchived, setShowArchived] = useState(false);
  const [editing, setEditing] = useState<{ id: string | null; draft: Draft } | null>(null);
  const load = useCallback(async () => {
    try {
      setBank(await api<QuestionBank>(`${learningPath(classroom.id)}/questions`));
      setError("");
    } catch (e) {
      setError(errorText(e, "Không thể tải ngân hàng câu hỏi."));
      denied(e);
    }
  }, [classroom.id, denied]);
  useEffect(() => {
    void load();
  }, [load]);
  const codes = useMemo(() => new Map((bank?.skills ?? []).map((s) => [s.id, s.code])), [bank]);
  const shown = (bank?.questions ?? []).filter((q) =>
    (showArchived || !q.archived) && (!level || q.level === level) &&
    (!skill || q.primarySkillId === skill || q.secondarySkillIds.includes(skill)) &&
    (!search || `${q.code} ${q.stem}`.toLowerCase().includes(search.toLowerCase())));
  const counts = levels.map((l) => (bank?.questions ?? []).filter((q) => !q.archived && q.level === l.code && (!skill || q.primarySkillId === skill)).length);

  if (error) return <p className="error" role="alert">{error} <button onClick={() => void load()}>Tải lại</button></p>;
  if (!bank) return <p role="status">Đang tải…</p>;
  return (
    <div className="stack">
      <section className="card" aria-label="Ngân hàng câu hỏi">
        <div className="card-heading">
          <h2>Ngân hàng câu hỏi ({bank.questions.filter((q) => !q.archived).length})</h2>
          <button className="primary" disabled={!canWrite || bank.skills.length === 0}
            onClick={() => setEditing({ id: null, draft: blank(skill || bank.skills[0]?.id || "") })}>
            Thêm câu hỏi
          </button>
        </div>
        <p className="hint">Mỗi câu có một kỹ năng chính (được tính kết quả) và tối đa 2 kỹ năng phụ (chỉ để lọc). Muốn thêm nhiều câu, dùng file Excel ở tab Buổi chương trình.</p>
        <div className="bank-levels" aria-label="Số câu theo mức">
          {levels.map((l, i) => <span key={l.code} className={`level-chip level-${l.code}`}>{l.label}: {counts[i]}</span>)}
        </div>
        <div className="bank-filters">
          <label>Mức độ
            <select value={level} onChange={(e) => setLevel(e.target.value)}>
              <option value="">Tất cả</option>
              {levels.map((l) => <option key={l.code} value={l.code}>{l.label}</option>)}
            </select>
          </label>
          <label>Kỹ năng
            <select value={skill} onChange={(e) => setSkill(e.target.value)}>
              <option value="">Tất cả</option>
              {bank.skills.map((s) => <option key={s.id} value={s.id}>{s.code} – {s.name}</option>)}
            </select>
          </label>
          <label className="grow">Tìm
            <input type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Mã câu hoặc nội dung" />
          </label>
          <label className="checkbox-label">
            <input type="checkbox" checked={showArchived} onChange={(e) => setShowArchived(e.target.checked)} /> Hiện câu ngừng dùng
          </label>
        </div>
        {editing && (
          <QuestionForm classroom={classroom} bank={bank} editing={editing} denied={denied}
            onCancel={() => setEditing(null)} onSaved={() => { setEditing(null); void load(); }} />
        )}
        {shown.length === 0 ? <p className="muted">Không có câu hỏi phù hợp.</p> : (
          <ul className="question-list">
            {shown.map((q) => (
              <li key={q.id} className={q.archived ? "archived" : undefined}>
                <div className="question-meta">
                  <strong>{q.code}</strong>
                  <span className={`level-chip level-${q.level}`}>{levelLabel(q.level)}</span>
                  <span>{questionTypes[q.type]}</span>
                  <span>Kỹ năng {codes.get(q.primarySkillId)}{q.secondarySkillIds.length > 0 && ` (+ ${q.secondarySkillIds.map((x) => codes.get(x)).join(", ")})`}</span>
                  {q.lessonNo && <span>Buổi {q.lessonNo}</span>}
                  {q.archived && <span className="badge">Ngừng dùng</span>}
                </div>
                <QuestionPreview question={q} />
                <div className="row-actions">
                  <button disabled={!canWrite} onClick={() => setEditing({ id: q.id, draft: toDraft(q) })}>Sửa</button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>
      <ImagesCard classroom={classroom} bank={bank} canWrite={canWrite} denied={denied} onUploaded={() => void load()} />
    </div>
  );
}

export function QuestionPreview({ question }: { question: Pick<Question, "type" | "stem" | "stemImageUrl" | "options" | "answer"> }) {
  return (
    <div className="question-preview">
      <p className="question-stem">
        {question.type === "fill_blanks"
          ? stemParts(question.stem).map((part, i, all) => <span key={i}>{part}{i < all.length - 1 && <span className="blank-box">{i + 1}</span>}</span>)
          : question.stem}
      </p>
      {question.stemImageUrl && <img className="question-image" src={question.stemImageUrl} alt="Ảnh đề" />}
      {question.type === "single_choice" ? (
        <ol className="option-list">
          {question.options.map((o) => (
            <li key={o.key} className={question.answer[0]?.[0] === o.key ? "is-answer" : undefined}>
              <span className="option-key">{o.key}.</span> {o.imageUrl ? <img src={o.imageUrl} alt={o.text || o.image || o.key} /> : o.text}
            </li>
          ))}
        </ol>
      ) : (
        <p className="chip-row">{question.options.map((o) => <span key={o.key} className="chip">{o.text}</span>)}</p>
      )}
      <p className="answer-line">Đáp án: <strong>{answerText(question)}</strong></p>
    </div>
  );
}

function QuestionForm({ classroom, bank, editing, denied, onCancel, onSaved }: {
  classroom: TeacherClass; bank: QuestionBank; editing: { id: string | null; draft: Draft }; denied: (e: unknown) => void; onCancel: () => void; onSaved: () => void;
}) {
  const [draft, setDraft] = useState<Draft>(editing.draft);
  const [busy, setBusy] = useState(false);
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [error, setError] = useState("");
  const set = (patch: Partial<Draft>) => setDraft((d) => ({ ...d, ...patch }));
  const blanks = stemParts(draft.stem).length - 1;
  async function save(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setErrors({});
    setError("");
    try {
      const path = `${learningPath(classroom.id)}/questions${editing.id ? `/${editing.id}` : ""}`;
      await api(path, editing.id ? "PUT" : "POST", toBody(draft));
      onSaved();
    } catch (e) {
      if (e instanceof ApiError && e.errors) setErrors(e.errors);
      else setError(e instanceof ApiError && e.status === 409 ? "Câu hỏi đã được sửa ở phiên khác. Đóng rồi mở lại để sửa tiếp." : errorText(e, "Không thể lưu câu hỏi."));
      denied(e);
    } finally {
      setBusy(false);
    }
  }
  const fieldError = (key: string) => errors[key]?.map((m) => <span key={m} className="field-error">{m}</span>);
  const images = bank.images.map((x) => x.fileName);
  return (
    <form className="question-form" onSubmit={(e) => void save(e)} aria-label={editing.id ? "Sửa câu hỏi" : "Thêm câu hỏi"}>
      <h3>{editing.id ? `Sửa câu ${editing.draft.code}` : "Câu hỏi mới"}</h3>
      <div className="form-row">
        <label>Mã câu<input required maxLength={40} value={draft.code} onChange={(e) => set({ code: e.target.value })} />{fieldError("code")}</label>
        <label>Loại
          <select value={draft.type} onChange={(e) => set({ type: e.target.value as QuestionType, answer: "" })}>
            {Object.entries(questionTypes).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </select>
        </label>
        <label>Mức độ
          <select value={draft.level} onChange={(e) => set({ level: e.target.value as QuestionLevel })}>
            {levels.map((l) => <option key={l.code} value={l.code}>{l.label}</option>)}
          </select>
        </label>
        <label>Buổi<input type="number" min={1} max={500} value={draft.lessonNo} onChange={(e) => set({ lessonNo: e.target.value })} />{fieldError("lessonNo")}</label>
      </div>
      <div className="form-row">
        <label>Kỹ năng chính
          <select required value={draft.primarySkillId} onChange={(e) => set({ primarySkillId: e.target.value })}>
            {bank.skills.map((s) => <option key={s.id} value={s.id}>{s.code} – {s.name}</option>)}
          </select>{fieldError("primarySkillId")}
        </label>
        <fieldset className="secondary-skills">
          <legend>Kỹ năng phụ (tối đa 2)</legend>
          {bank.skills.filter((s) => s.id !== draft.primarySkillId).map((s) => (
            <label key={s.id} className="checkbox-label">
              <input type="checkbox" checked={draft.secondarySkillIds.includes(s.id)}
                disabled={!draft.secondarySkillIds.includes(s.id) && draft.secondarySkillIds.length >= 2}
                onChange={(e) => set({ secondarySkillIds: e.target.checked ? [...draft.secondarySkillIds, s.id] : draft.secondarySkillIds.filter((x) => x !== s.id) })} />
              {s.code}
            </label>
          ))}
          {fieldError("secondarySkillIds")}
        </fieldset>
      </div>
      <label>Đề bài {draft.type === "fill_blanks" && <span className="hint">— mỗi ô trống viết là [ ] (hiện có {blanks} ô)</span>}
        <textarea required rows={3} maxLength={2000} value={draft.stem} onChange={(e) => set({ stem: e.target.value })} />{fieldError("stem")}
      </label>
      <label>Ảnh đề (tên file)
        <input list="question-images" value={draft.stemImage} onChange={(e) => set({ stemImage: e.target.value })} placeholder="Ví dụ: dongho3.png" />{fieldError("stemImage")}
      </label>
      <datalist id="question-images">{images.map((x) => <option key={x} value={x} />)}</datalist>
      {draft.type === "single_choice" ? (
        <fieldset>
          <legend>Phương án (2–4)</legend>
          {draft.options.map((o, i) => (
            <div key={o.key} className="form-row option-row">
              <label>{o.key}<input value={o.text} maxLength={300} onChange={(e) => set({ options: draft.options.map((x, j) => (j === i ? { ...x, text: e.target.value } : x)) })} /></label>
              <label>Ảnh {o.key}<input list="question-images" value={o.image} onChange={(e) => set({ options: draft.options.map((x, j) => (j === i ? { ...x, image: e.target.value } : x)) })} /></label>
            </div>
          ))}
          {fieldError("options")}
          <label>Đáp án đúng
            <select required value={draft.answer} onChange={(e) => set({ answer: e.target.value })}>
              <option value="">Chọn…</option>
              {letters.map((x) => <option key={x} value={x}>{x}</option>)}
            </select>{fieldError("answer")}
          </label>
        </fieldset>
      ) : (
        <>
          <label>Các nút để điền <span className="hint">— cách nhau bằng dấu ;</span>
            <input required value={draft.chips} onChange={(e) => set({ chips: e.target.value })} placeholder="12; 13; 14; 15" />{fieldError("options")}
          </label>
          <label>Đáp án từng ô <span className="hint">— theo thứ tự, cách nhau bằng ; (nhiều đáp án đúng cho một ô thì ngăn bằng /)</span>
            <input required value={draft.answer} onChange={(e) => set({ answer: e.target.value })} placeholder="6; 4" />{fieldError("answer")}
          </label>
        </>
      )}
      {editing.id && (
        <label className="checkbox-label">
          <input type="checkbox" checked={draft.archived} onChange={(e) => set({ archived: e.target.checked })} /> Ngừng dùng câu này
        </label>
      )}
      {error && <p className="error" role="alert">{error}</p>}
      <div className="form-actions">
        <button className="primary" disabled={busy}>{editing.id ? "Lưu câu hỏi" : "Thêm vào ngân hàng"}</button>
        <button type="button" onClick={onCancel} disabled={busy}>Huỷ</button>
      </div>
    </form>
  );
}

function ImagesCard({ classroom, bank, canWrite, denied, onUploaded }: { classroom: TeacherClass; bank: QuestionBank; canWrite: boolean; denied: (e: unknown) => void; onUploaded: () => void }) {
  const [busy, setBusy] = useState("");
  const [error, setError] = useState("");
  async function send(files: File[]) {
    setError("");
    try {
      for (const [i, file] of files.entries()) {
        setBusy(`Đang tải ${i + 1}/${files.length}…`);
        await upload(`${learningPath(classroom.id)}/question-images`, file, file.name);
      }
      onUploaded();
    } catch (e) {
      setError(errorText(e, "Không thể tải ảnh."));
      denied(e);
    } finally {
      setBusy("");
    }
  }
  return (
    <section className="card" aria-label="Ảnh câu hỏi">
      <h2>Ảnh câu hỏi ({bank.images.length})</h2>
      <p className="hint">Câu hỏi gọi ảnh bằng tên file. Tải ảnh trùng tên sẽ thay ảnh cũ.</p>
      <label>
        Tải ảnh lên
        <input type="file" accept="image/png,image/jpeg,image/webp,image/gif" multiple disabled={!canWrite || !!busy}
          onChange={(e) => void send([...(e.target.files ?? [])])} />
      </label>
      {busy && <p role="status">{busy}</p>}
      {error && <p className="error" role="alert">{error}</p>}
      <ul className="image-grid">
        {bank.images.map((x) => (
          <li key={x.id}><img src={x.url} alt={x.fileName} loading="lazy" /><span>{x.fileName}</span></li>
        ))}
      </ul>
    </section>
  );
}
