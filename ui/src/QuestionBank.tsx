import {
  lazy,
  Suspense,
  useCallback,
  useEffect,
  useMemo,
  useState,
  type FormEvent,
} from "react";
import { api, ApiError, upload, type TeacherClass } from "./api";
import {
  answerText,
  levelLabel,
  levels,
  pointsText,
  questionTypes,
  stemParts,
  type FillMode,
  type Question,
  type QuestionBank,
  type QuestionLevel,
  type QuestionType,
} from "./bank-api";
import { errorText, learningPath } from "./personalization-api";
import {
  blankNumbers,
  sanitizeStem,
  stemText,
  withImageLinks,
} from "./rich-stem";
import "./bank.css";
import "./rich-stem.css";

// The editor (Tiptap) is large: it is a chunk of its own, loaded only when a typed fill-in is being written.
const RichStemEditor = lazy(() => import("./RichStemEditor"));

type Props = {
  classroom: TeacherClass;
  canWrite: boolean;
  denied: (e: unknown) => void;
};

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
  multi: string[];
  points: string;
  fillMode: FillMode;
  ignoreDiacritics: boolean;
  typedAnswers: string[];
  stemHtml: string;
  audioFile: string;
  sampleAnswer: string;
  archived: boolean;
  version: number;
};

const letters = ["A", "B", "C", "D", "E", "F"];
const blank = (skill: string): Draft => ({
  code: "",
  type: "single_choice",
  level: "nhan_biet",
  primarySkillId: skill,
  secondarySkillIds: [],
  lessonNo: "",
  stem: "",
  stemImage: "",
  options: letters.map((key) => ({ key, text: "", image: "" })),
  chips: "",
  answer: "",
  multi: [],
  points: "1",
  fillMode: "chips",
  ignoreDiacritics: false,
  typedAnswers: [],
  stemHtml: "",
  audioFile: "",
  sampleAnswer: "",
  archived: false,
  version: 0,
});

function toDraft(q: Question): Draft {
  const typed = q.type === "fill_blanks" && q.fillMode === "type";
  return {
    code: q.code,
    type: q.type,
    level: q.level,
    primarySkillId: q.primarySkillId,
    secondarySkillIds: q.secondarySkillIds,
    lessonNo: q.lessonNo?.toString() ?? "",
    stem: q.stem,
    stemImage: q.stemImage ?? "",
    options: letters.map((key) => {
      const o = q.options.find((x) => x.key === key);
      return { key, text: o?.text ?? "", image: o?.image ?? "" };
    }),
    chips:
      q.type === "fill_blanks" && !typed
        ? q.options.map((x) => x.text).join("; ")
        : "",
    answer:
      q.type === "single_choice"
        ? (q.answer[0]?.[0] ?? "")
        : q.type === "fill_blanks" && !typed
          ? q.answer.map((x) => x.join("/")).join("; ")
          : "",
    multi: q.type === "multi_choice" ? (q.answer[0] ?? []) : [],
    points: String(q.points ?? 1),
    fillMode: q.fillMode ?? "chips",
    ignoreDiacritics: q.ignoreDiacritics ?? false,
    typedAnswers: typed ? q.answer.map((x) => x.join(" | ")) : [],
    stemHtml: q.stemHtml ?? "",
    audioFile: q.audioFile ?? "",
    sampleAnswer: q.sampleAnswer ?? "",
    archived: q.archived,
    version: q.version,
  };
}

function toBody(d: Draft) {
  const fill = d.type === "fill_blanks";
  const typed = fill && d.fillMode === "type";
  const choices = d.options
    .filter((o) => o.text.trim() || o.image.trim())
    .map((o) => ({
      key: o.key,
      text: o.text.trim(),
      image: o.image.trim() || null,
    }));
  const options =
    d.type === "essay" || typed
      ? []
      : fill
        ? d.chips
            .split(";")
            .map((x) => x.trim())
            .filter(Boolean)
            .map((text, i) => ({ key: String(i + 1), text, image: null }))
        : choices;
  const answer =
    d.type === "essay"
      ? []
      : typed
        ? d.typedAnswers
            .slice(0, blankNumbers(stemText(d.stemHtml)).length)
            .map((x) =>
              x
                .split("|")
                .map((v) => v.trim())
                .filter(Boolean),
            )
        : fill
          ? d.answer.trim()
            ? d.answer.split(";").map((x) =>
                x
                  .split("/")
                  .map((v) => v.trim())
                  .filter(Boolean),
              )
            : []
          : d.type === "multi_choice"
            ? [[...d.multi].sort()]
            : [[d.answer.trim().toUpperCase()]];
  const points = Number(d.points.replace(",", "."));
  return {
    code: d.code,
    type: d.type,
    level: d.level,
    primarySkillId: d.primarySkillId,
    secondarySkillIds: d.secondarySkillIds,
    lessonNo: d.lessonNo ? Number(d.lessonNo) : null,
    stem: typed ? stemText(d.stemHtml) : d.stem,
    stemImage: d.stemImage.trim() || null,
    options,
    answer,
    points: Number.isFinite(points) && d.points.trim() ? points : null,
    fillMode: fill ? d.fillMode : null,
    ignoreDiacritics: typed && d.ignoreDiacritics,
    sampleAnswer: d.type === "essay" ? d.sampleAnswer.trim() || null : null,
    stemHtml: typed ? d.stemHtml || null : null,
    audioFile: typed ? d.audioFile.trim() || null : null,
    archived: d.archived,
    version: d.version,
  };
}

// "Ngân hàng câu hỏi" (#79): filter, read and edit the class's questions; images and audio are uploaded by file name.
export function QuestionBankTab({ classroom, canWrite, denied }: Props) {
  const [bank, setBank] = useState<QuestionBank | null>(null);
  const [error, setError] = useState("");
  const [level, setLevel] = useState("");
  const [skill, setSkill] = useState("");
  const [search, setSearch] = useState("");
  const [showArchived, setShowArchived] = useState(false);
  const [editing, setEditing] = useState<{
    id: string | null;
    draft: Draft;
  } | null>(null);
  const load = useCallback(async () => {
    try {
      setBank(
        await api<QuestionBank>(`${learningPath(classroom.id)}/questions`),
      );
      setError("");
    } catch (e) {
      setError(errorText(e, "Không thể tải ngân hàng câu hỏi."));
      denied(e);
    }
  }, [classroom.id, denied]);
  useEffect(() => {
    void load();
  }, [load]);
  const codes = useMemo(
    () => new Map((bank?.skills ?? []).map((s) => [s.id, s.code])),
    [bank],
  );
  const links = useMemo(
    () =>
      new Map(
        (bank?.images ?? []).map((x) => [x.fileName.toLowerCase(), x.url]),
      ),
    [bank],
  );
  const shown = (bank?.questions ?? []).filter(
    (q) =>
      (showArchived || !q.archived) &&
      (!level || q.level === level) &&
      (!skill ||
        q.primarySkillId === skill ||
        q.secondarySkillIds.includes(skill)) &&
      (!search ||
        `${q.code} ${q.stem}`.toLowerCase().includes(search.toLowerCase())),
  );
  const counts = levels.map(
    (l) =>
      (bank?.questions ?? []).filter(
        (q) =>
          !q.archived &&
          q.level === l.code &&
          (!skill || q.primarySkillId === skill),
      ).length,
  );

  if (error)
    return (
      <p className="error" role="alert">
        {error} <button onClick={() => void load()}>Tải lại</button>
      </p>
    );
  if (!bank) return <p role="status">Đang tải…</p>;
  return (
    <div className="stack">
      <section className="card" aria-label="Ngân hàng câu hỏi">
        <div className="card-heading">
          <h2>
            Ngân hàng câu hỏi (
            {bank.questions.filter((q) => !q.archived).length})
          </h2>
          <button
            className="primary"
            disabled={!canWrite || bank.skills.length === 0}
            onClick={() =>
              setEditing({
                id: null,
                draft: blank(skill || bank.skills[0]?.id || ""),
              })
            }
          >
            Thêm câu hỏi
          </button>
        </div>
        <p className="hint">
          Mỗi câu có một kỹ năng chính (được tính kết quả) và tối đa 2 kỹ năng
          phụ (chỉ để lọc). Muốn thêm nhiều câu, dùng file Excel ở tab Buổi
          chương trình.
        </p>
        <div className="bank-levels" aria-label="Số câu theo mức">
          {levels.map((l, i) => (
            <span key={l.code} className={`level-chip level-${l.code}`}>
              {l.label}: {counts[i]}
            </span>
          ))}
        </div>
        <div className="bank-filters">
          <label>
            Mức độ
            <select value={level} onChange={(e) => setLevel(e.target.value)}>
              <option value="">Tất cả</option>
              {levels.map((l) => (
                <option key={l.code} value={l.code}>
                  {l.label}
                </option>
              ))}
            </select>
          </label>
          <label>
            Kỹ năng
            <select value={skill} onChange={(e) => setSkill(e.target.value)}>
              <option value="">Tất cả</option>
              {bank.skills.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.code} – {s.name}
                </option>
              ))}
            </select>
          </label>
          <label className="grow">
            Tìm
            <input
              type="search"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Mã câu hoặc nội dung"
            />
          </label>
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={showArchived}
              onChange={(e) => setShowArchived(e.target.checked)}
            />{" "}
            Hiện câu ngừng dùng
          </label>
        </div>
        {editing && (
          <QuestionForm
            classroom={classroom}
            bank={bank}
            editing={editing}
            denied={denied}
            onFilesChanged={() => void load()}
            onCancel={() => setEditing(null)}
            onSaved={() => {
              setEditing(null);
              void load();
            }}
          />
        )}
        {shown.length === 0 ? (
          <p className="muted">Không có câu hỏi phù hợp.</p>
        ) : (
          <ul className="question-list">
            {shown.map((q) => (
              <li key={q.id} className={q.archived ? "archived" : undefined}>
                <div className="question-meta">
                  <strong>{q.code}</strong>
                  <span className={`level-chip level-${q.level}`}>
                    {levelLabel(q.level)}
                  </span>
                  <span>{questionTypes[q.type]}</span>
                  <span>{pointsText(q.points)} điểm</span>
                  <span>
                    Kỹ năng {codes.get(q.primarySkillId)}
                    {q.secondarySkillIds.length > 0 &&
                      ` (+ ${q.secondarySkillIds.map((x) => codes.get(x)).join(", ")})`}
                  </span>
                  {q.lessonNo && <span>Buổi {q.lessonNo}</span>}
                  {q.archived && <span className="badge">Ngừng dùng</span>}
                </div>
                <QuestionPreview question={q} links={links} />
                <div className="row-actions">
                  <button
                    disabled={!canWrite}
                    onClick={() => setEditing({ id: q.id, draft: toDraft(q) })}
                  >
                    Sửa
                  </button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>
      <ImagesCard
        classroom={classroom}
        bank={bank}
        canWrite={canWrite}
        denied={denied}
        onUploaded={() => void load()}
      />
    </div>
  );
}

type PreviewQuestion = Pick<
  Question,
  "type" | "stem" | "stemImageUrl" | "options" | "answer"
> &
  Partial<
    Pick<
      Question,
      "stemHtml" | "audioFile" | "sampleAnswer" | "points" | "fillMode"
    >
  > & { audioUrl?: string | null };

/** A question as the teacher sees it, key included. `links` turns the file names of a stored stem into links. */
export function QuestionPreview({
  question,
  links,
}: {
  question: PreviewQuestion;
  links?: ReadonlyMap<string, string>;
}) {
  const typed = question.type === "fill_blanks" && question.fillMode === "type";
  const html =
    typed && question.stemHtml
      ? sanitizeStem(
          links ? withImageLinks(question.stemHtml, links) : question.stemHtml,
        )
      : "";
  const audio =
    question.audioUrl ??
    (question.audioFile ? links?.get(question.audioFile.toLowerCase()) : null);
  const keys = new Set(question.answer[0] ?? []);
  return (
    <div className="question-preview">
      {audio && (
        <audio
          controls
          preload="none"
          src={audio}
          aria-label="Âm thanh của câu"
        />
      )}
      {html ? (
        <div
          className="rich-stem question-stem"
          dangerouslySetInnerHTML={{ __html: html }}
        />
      ) : (
        <p className="question-stem">
          {question.type === "fill_blanks"
            ? (typed
                ? question.stem.split(/\{\{\d+\}\}/)
                : stemParts(question.stem)
              ).map((part, i, all) => (
                <span key={i}>
                  {part}
                  {i < all.length - 1 && (
                    <span className="blank-box">{i + 1}</span>
                  )}
                </span>
              ))
            : question.stem}
        </p>
      )}
      {question.stemImageUrl && (
        <img
          className="question-image"
          src={question.stemImageUrl}
          alt="Ảnh đề"
        />
      )}
      {question.type === "single_choice" || question.type === "multi_choice" ? (
        <ol className="option-list">
          {question.options.map((o) => (
            <li
              key={o.key}
              className={keys.has(o.key) ? "is-answer" : undefined}
            >
              <span className="option-key">{o.key}.</span>{" "}
              {o.imageUrl ? (
                <img src={o.imageUrl} alt={o.text || o.image || o.key} />
              ) : (
                o.text
              )}
            </li>
          ))}
        </ol>
      ) : question.type === "fill_blanks" && !typed ? (
        <p className="chip-row">
          {question.options.map((o) => (
            <span key={o.key} className="chip">
              {o.text}
            </span>
          ))}
        </p>
      ) : null}
      <p className="answer-line">
        {question.type === "essay" ? "Đáp án mẫu" : "Đáp án"}:{" "}
        <strong>{answerText(question)}</strong>
      </p>
    </div>
  );
}

function QuestionForm({
  classroom,
  bank,
  editing,
  denied,
  onFilesChanged,
  onCancel,
  onSaved,
}: {
  classroom: TeacherClass;
  bank: QuestionBank;
  editing: { id: string | null; draft: Draft };
  denied: (e: unknown) => void;
  onFilesChanged: () => void;
  onCancel: () => void;
  onSaved: () => void;
}) {
  const [draft, setDraft] = useState<Draft>(editing.draft);
  const [busy, setBusy] = useState(false);
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [error, setError] = useState("");
  const set = (patch: Partial<Draft>) => setDraft((d) => ({ ...d, ...patch }));
  const typed = draft.type === "fill_blanks" && draft.fillMode === "type";
  const blanks = typed
    ? blankNumbers(stemText(draft.stemHtml)).length
    : stemParts(draft.stem).length - 1;
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
      else
        setError(
          e instanceof ApiError && e.status === 409
            ? "Câu hỏi đã được sửa ở phiên khác. Đóng rồi mở lại để sửa tiếp."
            : errorText(e, "Không thể lưu câu hỏi."),
        );
      denied(e);
    } finally {
      setBusy(false);
    }
  }
  const fieldError = (key: string) =>
    errors[key]?.map((m) => (
      <span key={m} className="field-error">
        {m}
      </span>
    ));
  const files = bank.images.filter((x) => x.contentType.startsWith("image/"));
  const audio = bank.images.filter((x) => x.contentType.startsWith("audio/"));
  const images = files.map((x) => x.fileName);
  async function uploadImage(file: File) {
    const saved = await upload<{ fileName: string; url: string }>(
      `${learningPath(classroom.id)}/question-images`,
      file,
      file.name,
    );
    onFilesChanged();
    return { fileName: saved.fileName, url: saved.url };
  }
  async function uploadAudio(file: File | undefined) {
    if (!file) return;
    setError("");
    try {
      const saved = await upload<{ fileName: string }>(
        `${learningPath(classroom.id)}/question-images`,
        file,
        file.name,
      );
      onFilesChanged();
      set({ audioFile: saved.fileName });
    } catch (e) {
      setError(errorText(e, "Không thể tải âm thanh."));
      denied(e);
    }
  }
  const visibleOptions =
    draft.type === "multi_choice" ? draft.options : draft.options.slice(0, 4);
  const toggleKey = (key: string, on: boolean) =>
    set({
      multi: on ? [...draft.multi, key] : draft.multi.filter((x) => x !== key),
    });
  return (
    <form
      className="question-form"
      onSubmit={(e) => void save(e)}
      aria-label={editing.id ? "Sửa câu hỏi" : "Thêm câu hỏi"}
    >
      <h3>{editing.id ? `Sửa câu ${editing.draft.code}` : "Câu hỏi mới"}</h3>
      <div className="form-row">
        <label>
          Mã câu
          <input
            required
            maxLength={40}
            value={draft.code}
            onChange={(e) => set({ code: e.target.value })}
          />
          {fieldError("code")}
        </label>
        <label>
          Dạng
          <select
            value={draft.type}
            onChange={(e) =>
              set({
                type: e.target.value as QuestionType,
                answer: "",
                multi: [],
              })
            }
          >
            {Object.entries(questionTypes).map(([k, v]) => (
              <option key={k} value={k}>
                {v}
              </option>
            ))}
          </select>
          {fieldError("type")}
        </label>
        <label>
          Mức độ
          <select
            value={draft.level}
            onChange={(e) => set({ level: e.target.value as QuestionLevel })}
          >
            {levels.map((l) => (
              <option key={l.code} value={l.code}>
                {l.label}
              </option>
            ))}
          </select>
        </label>
        <label>
          Điểm
          <input
            inputMode="decimal"
            value={draft.points}
            onChange={(e) => set({ points: e.target.value })}
            aria-describedby="points-hint"
          />
          <span id="points-hint" className="hint">
            0,25 đến 10
          </span>
          {fieldError("points")}
        </label>
        <label>
          Buổi
          <input
            type="number"
            min={1}
            max={500}
            value={draft.lessonNo}
            onChange={(e) => set({ lessonNo: e.target.value })}
          />
          {fieldError("lessonNo")}
        </label>
      </div>
      <div className="form-row">
        <label>
          Kỹ năng chính
          <select
            required
            value={draft.primarySkillId}
            onChange={(e) => set({ primarySkillId: e.target.value })}
          >
            {bank.skills.map((s) => (
              <option key={s.id} value={s.id}>
                {s.code} – {s.name}
              </option>
            ))}
          </select>
          {fieldError("primarySkillId")}
        </label>
        <fieldset className="secondary-skills">
          <legend>Kỹ năng phụ (tối đa 2)</legend>
          {bank.skills
            .filter((s) => s.id !== draft.primarySkillId)
            .map((s) => (
              <label key={s.id} className="checkbox-label">
                <input
                  type="checkbox"
                  checked={draft.secondarySkillIds.includes(s.id)}
                  disabled={
                    !draft.secondarySkillIds.includes(s.id) &&
                    draft.secondarySkillIds.length >= 2
                  }
                  onChange={(e) =>
                    set({
                      secondarySkillIds: e.target.checked
                        ? [...draft.secondarySkillIds, s.id]
                        : draft.secondarySkillIds.filter((x) => x !== s.id),
                    })
                  }
                />
                {s.code}
              </label>
            ))}
          {fieldError("secondarySkillIds")}
        </fieldset>
      </div>
      {draft.type === "fill_blanks" && (
        <fieldset>
          <legend>Cách điền</legend>
          <label className="checkbox-label">
            <input
              type="radio"
              name="fill-mode"
              checked={draft.fillMode === "chips"}
              onChange={() => set({ fillMode: "chips" })}
            />{" "}
            Học sinh chọn nút
          </label>
          <label className="checkbox-label">
            <input
              type="radio"
              name="fill-mode"
              checked={draft.fillMode === "type"}
              onChange={() => set({ fillMode: "type" })}
            />{" "}
            Học sinh gõ chữ vào chỗ trống
          </label>
          {fieldError("fillMode")}
        </fieldset>
      )}
      {typed ? (
        <div className="stack">
          <span className="label-text" id="stem-label">
            Đề bài{" "}
            <span className="hint">
              — bấm "Chỗ trống" để chèn; hiện có {blanks} chỗ (tối đa 10, đánh
              số liên tiếp 1, 2, 3…)
            </span>
          </span>
          <Suspense fallback={<p role="status">Đang tải trình soạn thảo…</p>}>
            <RichStemEditor
              value={draft.stemHtml}
              onChange={(html) => set({ stemHtml: html })}
              images={files.map((x) => ({ fileName: x.fileName, url: x.url }))}
              uploadImage={uploadImage}
              label="Đề bài"
            />
          </Suspense>
          {fieldError("stem")}
          {fieldError("stemHtml")}
          <fieldset>
            <legend>Đáp án từng chỗ trống</legend>
            {Array.from({ length: blanks }, (_, i) => (
              <label key={i}>
                Chỗ trống {i + 1}{" "}
                <span className="hint">
                  — nhiều đáp án đúng thì ngăn bằng |
                </span>
                <input
                  required
                  value={draft.typedAnswers[i] ?? ""}
                  onChange={(e) => {
                    const next = [...draft.typedAnswers];
                    next[i] = e.target.value;
                    set({ typedAnswers: next });
                  }}
                  placeholder="Hà Nội | Ha Noi"
                />
              </label>
            ))}
            {blanks === 0 && (
              <p className="muted">Chèn ít nhất một chỗ trống vào đề bài.</p>
            )}
            {fieldError("answer")}
          </fieldset>
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={draft.ignoreDiacritics}
              onChange={(e) => set({ ignoreDiacritics: e.target.checked })}
            />{" "}
            Bỏ qua dấu tiếng Việt khi chấm (chữ hoa/thường và khoảng trắng luôn
            được bỏ qua)
          </label>
          <fieldset className="audio-card">
            <legend>Âm thanh (mp3 hoặc m4a, tối đa 5 MB)</legend>
            <label>
              Chọn tệp đã tải lên
              <select
                value={draft.audioFile}
                onChange={(e) => set({ audioFile: e.target.value })}
              >
                <option value="">Không có</option>
                {audio.map((x) => (
                  <option key={x.id} value={x.fileName}>
                    {x.fileName}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Hoặc tải tệp mới
              <input
                type="file"
                accept="audio/mpeg,audio/mp4,.mp3,.m4a"
                onChange={(e) => void uploadAudio(e.target.files?.[0])}
              />
            </label>
            {draft.audioFile &&
              audio.find((x) => x.fileName === draft.audioFile) && (
                <audio
                  controls
                  preload="none"
                  src={audio.find((x) => x.fileName === draft.audioFile)!.url}
                  aria-label="Nghe thử"
                />
              )}
            {fieldError("audioFile")}
          </fieldset>
        </div>
      ) : (
        <label>
          Đề bài{" "}
          {draft.type === "fill_blanks" && (
            <span className="hint">
              — mỗi ô trống viết là [ ] (hiện có {blanks} ô)
            </span>
          )}
          <textarea
            required
            rows={3}
            maxLength={2000}
            value={draft.stem}
            onChange={(e) => set({ stem: e.target.value })}
          />
          {fieldError("stem")}
        </label>
      )}
      {!typed && (
        <>
          <label>
            Ảnh đề (tên file)
            <input
              list="question-images"
              value={draft.stemImage}
              onChange={(e) => set({ stemImage: e.target.value })}
              placeholder="Ví dụ: dongho3.png"
            />
            {fieldError("stemImage")}
          </label>
          <datalist id="question-images">
            {images.map((x) => (
              <option key={x} value={x} />
            ))}
          </datalist>
        </>
      )}
      {draft.type === "single_choice" || draft.type === "multi_choice" ? (
        <fieldset>
          <legend>
            {draft.type === "multi_choice"
              ? "Phương án (3–6, tích các phương án đúng)"
              : "Phương án (2–4)"}
          </legend>
          {visibleOptions.map((o, i) => (
            <div key={o.key} className="form-row option-row">
              <label>
                {o.key}
                <input
                  value={o.text}
                  maxLength={300}
                  onChange={(e) =>
                    set({
                      options: draft.options.map((x, j) =>
                        j === i ? { ...x, text: e.target.value } : x,
                      ),
                    })
                  }
                />
              </label>
              <label>
                Ảnh {o.key}
                <input
                  list="question-images"
                  value={o.image}
                  onChange={(e) =>
                    set({
                      options: draft.options.map((x, j) =>
                        j === i ? { ...x, image: e.target.value } : x,
                      ),
                    })
                  }
                />
              </label>
              {draft.type === "multi_choice" && (
                <label className="checkbox-label">
                  <input
                    type="checkbox"
                    checked={draft.multi.includes(o.key)}
                    onChange={(e) => toggleKey(o.key, e.target.checked)}
                    aria-label={`Phương án ${o.key} đúng`}
                  />{" "}
                  Đúng
                </label>
              )}
            </div>
          ))}
          {fieldError("options")}
          {draft.type === "single_choice" ? (
            <label>
              Đáp án đúng
              <select
                required
                value={draft.answer}
                onChange={(e) => set({ answer: e.target.value })}
              >
                <option value="">Chọn…</option>
                {letters.slice(0, 4).map((x) => (
                  <option key={x} value={x}>
                    {x}
                  </option>
                ))}
              </select>
              {fieldError("answer")}
            </label>
          ) : (
            fieldError("answer")
          )}
        </fieldset>
      ) : draft.type === "essay" ? (
        <label>
          Đáp án mẫu{" "}
          <span className="hint">
            — không bắt buộc; hiện cho giáo viên khi chấm
          </span>
          <textarea
            rows={3}
            maxLength={2000}
            value={draft.sampleAnswer}
            onChange={(e) => set({ sampleAnswer: e.target.value })}
          />
          {fieldError("sampleAnswer")}
        </label>
      ) : !typed ? (
        <>
          <label>
            Các nút để điền <span className="hint">— cách nhau bằng dấu ;</span>
            <input
              required
              value={draft.chips}
              onChange={(e) => set({ chips: e.target.value })}
              placeholder="12; 13; 14; 15"
            />
            {fieldError("options")}
          </label>
          <label>
            Đáp án từng ô{" "}
            <span className="hint">
              — theo thứ tự, cách nhau bằng ; (nhiều đáp án đúng cho một ô thì
              ngăn bằng /)
            </span>
            <input
              required
              value={draft.answer}
              onChange={(e) => set({ answer: e.target.value })}
              placeholder="6; 4"
            />
            {fieldError("answer")}
          </label>
        </>
      ) : null}
      {editing.id && (
        <label className="checkbox-label">
          <input
            type="checkbox"
            checked={draft.archived}
            onChange={(e) => set({ archived: e.target.checked })}
          />{" "}
          Ngừng dùng câu này
        </label>
      )}
      {error && (
        <p className="error" role="alert">
          {error}
        </p>
      )}
      <div className="form-actions">
        <button className="primary" disabled={busy}>
          {editing.id ? "Lưu câu hỏi" : "Thêm vào ngân hàng"}
        </button>
        <button type="button" onClick={onCancel} disabled={busy}>
          Huỷ
        </button>
      </div>
    </form>
  );
}

function ImagesCard({
  classroom,
  bank,
  canWrite,
  denied,
  onUploaded,
}: {
  classroom: TeacherClass;
  bank: QuestionBank;
  canWrite: boolean;
  denied: (e: unknown) => void;
  onUploaded: () => void;
}) {
  const [busy, setBusy] = useState("");
  const [error, setError] = useState("");
  async function send(files: File[]) {
    setError("");
    try {
      for (const [i, file] of files.entries()) {
        setBusy(`Đang tải ${i + 1}/${files.length}…`);
        await upload(
          `${learningPath(classroom.id)}/question-images`,
          file,
          file.name,
        );
      }
      onUploaded();
    } catch (e) {
      setError(errorText(e, "Không thể tải tệp."));
      denied(e);
    } finally {
      setBusy("");
    }
  }
  const pictures = bank.images.filter((x) =>
    x.contentType.startsWith("image/"),
  );
  const sounds = bank.images.filter((x) => x.contentType.startsWith("audio/"));
  return (
    <section className="card" aria-label="Ảnh câu hỏi">
      <h2>Ảnh và âm thanh của câu hỏi ({bank.images.length})</h2>
      <p className="hint">
        Câu hỏi gọi tệp bằng tên file. Tải tệp trùng tên sẽ thay tệp cũ. Âm
        thanh: mp3 hoặc m4a, tối đa 5 MB.
      </p>
      <label>
        Tải ảnh hoặc âm thanh lên
        <input
          type="file"
          accept="image/png,image/jpeg,image/webp,image/gif,audio/mpeg,audio/mp4,.mp3,.m4a"
          multiple
          disabled={!canWrite || !!busy}
          onChange={(e) => void send([...(e.target.files ?? [])])}
        />
      </label>
      {busy && <p role="status">{busy}</p>}
      {error && (
        <p className="error" role="alert">
          {error}
        </p>
      )}
      <ul className="image-grid">
        {pictures.map((x) => (
          <li key={x.id}>
            <img src={x.url} alt={x.fileName} loading="lazy" />
            <span>{x.fileName}</span>
          </li>
        ))}
      </ul>
      {sounds.length > 0 && (
        <ul className="sound-list" aria-label="Tệp âm thanh">
          {sounds.map((x) => (
            <li key={x.id}>
              <span>{x.fileName}</span>{" "}
              <audio
                controls
                preload="none"
                src={x.url}
                aria-label={`Nghe ${x.fileName}`}
              />
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
