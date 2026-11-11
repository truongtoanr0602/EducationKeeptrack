import { useCallback, useEffect, useMemo, useState } from "react";
import { api, ApiError, type TeacherClass } from "./api";
import { today } from "./roster-api";
import {
  addDays,
  competencyStates,
  errorText,
  executionStates,
  learningPath,
  mondayOf,
  viDate,
  weekdayOf,
  type ClassSessionSource,
  type CompetencyProfile,
  type Curriculum,
  type ExecutionEntry,
  type ExecutionStatus,
  type Material,
  type PlanActivity,
  type PlanContent,
  type PlanProgress,
  type PlanSummary,
  type ReviewPlan,
} from "./personalization-api";

type Props = { classroom: TeacherClass; curriculum: Curriculum; canWrite: boolean; denied: (e: unknown) => void };
const MAX_GOALS = 2;

export function ReviewPlans({ classroom, curriculum, canWrite, denied }: Props) {
  const base = learningPath(classroom.id);
  const [week, setWeek] = useState(() => {
    const q = new URLSearchParams(window.location.search).get("planWeek");
    return q && /^\d{4}-\d{2}-\d{2}$/.test(q) ? mondayOf(q) : mondayOf(today());
  });
  const [plans, setPlans] = useState<PlanSummary[] | null>(null);
  const [plan, setPlan] = useState<ReviewPlan | null>(null);
  const [profile, setProfile] = useState<CompetencyProfile | null>(null);
  const [sessions, setSessions] = useState<ClassSessionSource[]>([]);
  const [materials, setMaterials] = useState<Material[]>([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    const url = new URL(window.location.href);
    url.searchParams.set("planWeek", week);
    window.history.replaceState(null, "", url);
  }, [week]);
  // A quiet reload (after a conflict) keeps the editor mounted so the teacher's draft survives.
  const load = useCallback(async (quiet = false) => {
    if (!quiet) setLoading(true);
    setError("");
    try {
      const [list, competency, weekSessions, library] = await Promise.all([
        api<PlanSummary[]>(`${base}/review-plans`),
        api<CompetencyProfile>(`${base}/competency`),
        api<ClassSessionSource[]>(`${base}/review-sessions?weekStart=${week}`),
        api<Material[]>(`${base}/materials`),
      ]);
      const summary = list.find((x) => x.weekStart === week);
      setPlans(list);
      setProfile(competency);
      setSessions(weekSessions);
      setMaterials(library);
      setPlan(summary ? await api<ReviewPlan>(`${base}/review-plans/${summary.id}`) : null);
    } catch (e) {
      setError(errorText(e, "Không thể tải kế hoạch ôn tập."));
      denied(e);
    } finally {
      setLoading(false);
    }
  }, [base, week, denied]);
  useEffect(() => {
    void load();
  }, [load]);
  return (
    <div className="stack">
      <section className="card week-picker" aria-label="Chọn tuần">
        <div className="card-heading">
          <h2>Kế hoạch ôn tập tuần</h2>
          <div className="row-actions">
            <button onClick={() => setWeek(addDays(week, -7))}>‹ Tuần trước</button>
            <label className="inline">
              <span className="sr-only">Ngày trong tuần</span>
              <input type="date" value={week} onChange={(e) => e.target.value && setWeek(mondayOf(e.target.value))} />
            </label>
            <button onClick={() => setWeek(addDays(week, 7))}>Tuần sau ›</button>
          </div>
        </div>
        <p className="hint">
          Tuần {viDate(week)} – {viDate(addDays(week, 6))}. Mỗi học sinh tối đa {MAX_GOALS} kỹ năng ưu tiên; thời lượng hoạt động của từng em không vượt quỹ phút của buổi.
        </p>
        {plans && plans.length > 0 && (
          <div className="chips" aria-label="Các tuần đã có kế hoạch">
            {plans.slice(0, 8).map((p) => (
              <button key={p.id} aria-pressed={p.weekStart === week} onClick={() => setWeek(p.weekStart)}>
                {viDate(p.weekStart)} · {p.status === "approved" ? "Đã duyệt" : "Dự thảo"}
              </button>
            ))}
          </div>
        )}
      </section>
      {error && (
        <p className="error" role="alert">
          {error} <button onClick={() => void load()}>Tải lại</button>
        </p>
      )}
      {loading && <p role="status">Đang tải…</p>}
      {!loading && profile && (
        <PlanEditor
          key={week}
          base={base}
          week={week}
          plan={plan}
          profile={profile}
          curriculum={curriculum}
          sessions={sessions}
          materials={materials}
          canWrite={canWrite}
          denied={denied}
          onSaved={(value) => {
            setPlan(value);
            setPlans((list) => [...(list ?? []).filter((x) => x.id !== value.id), value].sort((a, b) => b.weekStart.localeCompare(a.weekStart)));
          }}
          onReload={() => load(true)}
        />
      )}
    </div>
  );
}

function suggestedContent(week: string, profile: CompetencyProfile, sessions: ClassSessionSource[]): PlanContent {
  // Rule-based starting point (not AI): skills already "not_mastered" by the mastery rule, citing their evidence.
  const planSessions = sessions.filter((s) => s.status !== "cancelled").slice(0, 7).map((s) => ({ date: s.date, minutes: 30, sessionId: s.id }));
  return {
    sessions: planSessions.length ? planSessions : [{ date: week, minutes: 30, sessionId: null }],
    students: profile.students.map((s) => ({
      studentId: s.studentId,
      studentName: s.name,
      goals: s.skills
        .filter((k) => k.state === "not_mastered")
        .slice(0, MAX_GOALS)
        .map((k) => ({
          skillId: k.skillId,
          reason: `Chưa đạt theo quy tắc: ${k.evidence
            .filter((e) => e.counted)
            .map((e) => `${e.correct}/${e.taskCount} (${viDate(e.date)})`)
            .join(", ")}`,
          recheck: "Bài đánh giá lại khác câu, cùng kỹ năng và độ khó tương đương",
        })),
    })).filter((s) => s.goals.length > 0),
    activities: [],
    note: null,
  };
}

function PlanEditor({
  base,
  week,
  plan,
  profile,
  curriculum,
  sessions,
  materials,
  canWrite,
  denied,
  onSaved,
  onReload,
}: {
  base: string;
  week: string;
  plan: ReviewPlan | null;
  profile: CompetencyProfile;
  curriculum: Curriculum;
  sessions: ClassSessionSource[];
  materials: Material[];
  canWrite: boolean;
  denied: (e: unknown) => void;
  onSaved: (plan: ReviewPlan) => void;
  onReload: () => Promise<void>;
}) {
  const [draft, setDraft] = useState<PlanContent | null>(() => (plan ? plan.content : null));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [conflict, setConflict] = useState(false);
  const [message, setMessage] = useState("");
  const dirty = !!draft && (!plan || JSON.stringify(draft) !== JSON.stringify(plan.content));
  const editable = canWrite && !busy && (!plan || plan.execution.length === 0);
  const names = useMemo(() => new Map(profile.students.map((s) => [s.studentId, s.name])), [profile]);
  const skills = curriculum.skills;
  const plannable = skills.filter((s) => s.taught && !s.archived);
  const states = useMemo(() => {
    const map = new Map<string, string>();
    for (const s of profile.students) for (const k of s.skills) map.set(`${s.studentId}/${k.skillId}`, competencyStates[k.state]);
    return map;
  }, [profile]);

  async function save() {
    if (!draft) return;
    setBusy(true);
    setError("");
    setMessage("");
    try {
      const value = plan
        ? await api<ReviewPlan>(`${base}/review-plans/${plan.id}`, "PUT", { content: draft, version: plan.version })
        : await api<ReviewPlan>(`${base}/review-plans`, "POST", { weekStart: week, content: draft });
      setDraft(value.content);
      onSaved(value);
      setConflict(false);
      setMessage(plan?.status === "approved" ? "Đã lưu phiên bản mới; kế hoạch chuyển về dự thảo để duyệt lại." : "Đã lưu dự thảo kế hoạch.");
    } catch (e) {
      if (e instanceof ApiError && e.status === 409) setConflict(true);
      else setError(errorText(e, "Không thể lưu kế hoạch. Nội dung đang nhập được giữ nguyên."));
      denied(e);
    } finally {
      setBusy(false);
    }
  }
  async function approve() {
    if (!plan) return;
    setBusy(true);
    setError("");
    try {
      const value = await api<ReviewPlan>(`${base}/review-plans/${plan.id}/approve`, "POST", { version: plan.version });
      onSaved(value);
      setMessage("Đã duyệt kế hoạch. Phiên bản này được dùng để tổ chức buổi ôn.");
    } catch (e) {
      if (e instanceof ApiError && e.status === 409) setConflict(true);
      else setError(errorText(e, "Không thể duyệt kế hoạch."));
      denied(e);
    } finally {
      setBusy(false);
    }
  }

  if (!draft)
    return (
      <section className="card empty">
        <h2>Chưa có kế hoạch cho tuần này</h2>
        <p>
          Bắt đầu từ gợi ý theo quy tắc: các kỹ năng đang "Chưa đạt" kèm bằng chứng, và các buổi học của lớp trong tuần. Bạn sửa toàn bộ trước khi lưu.
        </p>
        <button className="primary" disabled={!canWrite} onClick={() => setDraft(suggestedContent(week, profile, sessions))}>
          Lập kế hoạch tuần này
        </button>
      </section>
    );

  const update = (change: (d: PlanContent) => PlanContent) => setDraft((d) => (d ? change(d) : d));
  const goalsOf = (studentId: string) => draft.students.find((s) => s.studentId === studentId)?.goals.map((g) => g.skillId) ?? [];
  const load = draft.sessions.map((_, index) =>
    draft.students.map((s) => ({
      studentId: s.studentId,
      minutes: draft.activities.filter((a) => a.session === index && a.studentIds.includes(s.studentId)).reduce((sum, a) => sum + (a.minutes || 0), 0),
    })),
  );
  const included = new Set(draft.students.map((s) => s.studentId));

  return (
    <>
      <section className="card plan-status" aria-label="Trạng thái kế hoạch">
        <div className="card-heading">
          <div>
            <span className={`badge ${plan?.status ?? "draft"}`}>{plan ? (plan.status === "approved" ? "Đã duyệt" : "Dự thảo") : "Chưa lưu"}</span>
            {plan && <span className="muted"> · phiên bản {plan.version}{plan.approvedVersion && plan.status !== "approved" ? ` · bản duyệt gần nhất: ${plan.approvedVersion}` : ""}</span>}
          </div>
          <div className="row-actions">
            <button className="primary" disabled={!editable || !dirty} onClick={() => void save()}>
              {plan ? "Lưu thay đổi" : "Lưu dự thảo"}
            </button>
            {plan && plan.status === "draft" && (
              <button disabled={!canWrite || busy || dirty} onClick={() => void approve()}>
                Duyệt kế hoạch
              </button>
            )}
            {plan && dirty && (
              <button disabled={busy} onClick={() => setDraft(plan.content)}>
                Bỏ thay đổi
              </button>
            )}
          </div>
        </div>
        {dirty && plan && <p className="notice">Có thay đổi chưa lưu. Lưu trước khi duyệt.</p>}
        {plan && plan.execution.length > 0 && <p className="notice">Kế hoạch đã ghi nhận thực hiện nên không sửa nội dung; lập kế hoạch cho tuần tiếp theo.</p>}
        {message && <p className="notice success" role="status">{message}</p>}
        {conflict && (
          <p className="notice" role="alert">
            Kế hoạch đã thay đổi ở phiên khác. Nội dung đang nhập được giữ nguyên.{" "}
            <button onClick={() => void onReload()}>Tải lại kế hoạch</button>
          </p>
        )}
        {error && <p className="error" role="alert">{error}</p>}
      </section>

      <section className="card" aria-label="Buổi ôn trong tuần">
        <h3>Buổi ôn và quỹ phút</h3>
        <ol className="plan-sessions">
          {draft.sessions.map((session, index) => (
            <li key={index} className="plan-row">
              <label>
                Buổi học của lớp
                <select
                  value={session.sessionId ?? ""}
                  disabled={!editable}
                  onChange={(e) => {
                    const linked = sessions.find((s) => s.id === e.target.value);
                    update((d) => ({ ...d, sessions: d.sessions.map((s, i) => (i === index ? { ...s, sessionId: linked?.id ?? null, date: linked?.date ?? s.date } : s)) }));
                  }}
                >
                  <option value="">Tự nhập ngày</option>
                  {sessions.filter((s) => s.status !== "cancelled").map((s) => (
                    <option key={s.id} value={s.id}>
                      {weekdayOf(s.date)} {viDate(s.date)} · {s.startTime.slice(0, 5)}–{s.endTime.slice(0, 5)}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Ngày
                <input
                  type="date"
                  min={week}
                  max={addDays(week, 6)}
                  value={session.date}
                  disabled={!editable || !!session.sessionId}
                  onChange={(e) => update((d) => ({ ...d, sessions: d.sessions.map((s, i) => (i === index ? { ...s, date: e.target.value } : s)) }))}
                />
              </label>
              <label>
                Quỹ phút ôn tập
                <input
                  type="number"
                  min={5}
                  max={240}
                  value={session.minutes}
                  disabled={!editable}
                  onChange={(e) => update((d) => ({ ...d, sessions: d.sessions.map((s, i) => (i === index ? { ...s, minutes: Number(e.target.value) } : s)) }))}
                />
              </label>
              <button
                disabled={!editable || draft.sessions.length <= 1}
                onClick={() =>
                  update((d) => ({
                    ...d,
                    sessions: d.sessions.filter((_, i) => i !== index),
                    activities: d.activities.filter((a) => a.session !== index).map((a) => ({ ...a, session: a.session > index ? a.session - 1 : a.session })),
                  }))
                }
              >
                Bỏ buổi
              </button>
            </li>
          ))}
        </ol>
        <button disabled={!editable || draft.sessions.length >= 7} onClick={() => update((d) => ({ ...d, sessions: [...d.sessions, { date: week, minutes: 30, sessionId: null }] }))}>
          ＋ Thêm buổi ôn
        </button>
      </section>

      <section className="card" aria-label="Mục tiêu từng học sinh">
        <h3>Mục tiêu từng học sinh</h3>
        <p className="hint">Mỗi em có mục tiêu riêng. Nêu lý do kèm bằng chứng và cách kiểm tra lại cho từng kỹ năng.</p>
        <div className="student-goals">
          {profile.students.map((student) => {
            const entry = draft.students.find((s) => s.studentId === student.studentId);
            return (
              <fieldset key={student.studentId} className="goal-card">
                <legend>
                  <label className="checkbox-label">
                    <input
                      type="checkbox"
                      checked={!!entry}
                      disabled={!editable}
                      onChange={(e) =>
                        update((d) =>
                          e.target.checked
                            ? { ...d, students: [...d.students, { studentId: student.studentId, studentName: student.name, goals: [] }] }
                            : {
                                ...d,
                                students: d.students.filter((s) => s.studentId !== student.studentId),
                                activities: d.activities.map((a) => ({ ...a, studentIds: a.studentIds.filter((id) => id !== student.studentId) })),
                              },
                        )
                      }
                    />
                    {student.name}
                  </label>
                </legend>
                {entry?.goals.map((goal, gi) => (
                  <div key={gi} className="goal">
                    <label>
                      Kỹ năng ưu tiên {gi + 1}
                      <select
                        value={goal.skillId}
                        disabled={!editable}
                        onChange={(e) =>
                          update((d) => ({
                            ...d,
                            students: d.students.map((s) =>
                              s.studentId === student.studentId ? { ...s, goals: s.goals.map((g, i) => (i === gi ? { ...g, skillId: e.target.value } : g)) } : s,
                            ),
                          }))
                        }
                      >
                        {!plannable.some((k) => k.id === goal.skillId) && <option value={goal.skillId}>Kỹ năng không còn dùng được</option>}
                        {plannable.map((k) => (
                          <option key={k.id} value={k.id}>
                            {k.code} · {k.name} — {states.get(`${student.studentId}/${k.id}`) ?? ""}
                          </option>
                        ))}
                      </select>
                    </label>
                    <label>
                      Lý do và bằng chứng
                      <textarea
                        rows={2}
                        maxLength={500}
                        value={goal.reason}
                        disabled={!editable}
                        onChange={(e) =>
                          update((d) => ({
                            ...d,
                            students: d.students.map((s) =>
                              s.studentId === student.studentId ? { ...s, goals: s.goals.map((g, i) => (i === gi ? { ...g, reason: e.target.value } : g)) } : s,
                            ),
                          }))
                        }
                      />
                    </label>
                    <label>
                      Cách kiểm tra lại
                      <input
                        maxLength={300}
                        value={goal.recheck}
                        disabled={!editable}
                        onChange={(e) =>
                          update((d) => ({
                            ...d,
                            students: d.students.map((s) =>
                              s.studentId === student.studentId ? { ...s, goals: s.goals.map((g, i) => (i === gi ? { ...g, recheck: e.target.value } : g)) } : s,
                            ),
                          }))
                        }
                      />
                    </label>
                    <button
                      disabled={!editable}
                      onClick={() =>
                        update((d) => ({
                          ...d,
                          students: d.students.map((s) => (s.studentId === student.studentId ? { ...s, goals: s.goals.filter((_, i) => i !== gi) } : s)),
                        }))
                      }
                    >
                      Bỏ kỹ năng
                    </button>
                  </div>
                ))}
                {entry && (
                  <button
                    disabled={!editable || entry.goals.length >= MAX_GOALS || !plannable.length}
                    onClick={() =>
                      update((d) => ({
                        ...d,
                        students: d.students.map((s) =>
                          s.studentId === student.studentId
                            ? { ...s, goals: [...s.goals, { skillId: (plannable.find((k) => !s.goals.some((g) => g.skillId === k.id)) ?? plannable[0]).id, reason: "", recheck: "" }] }
                            : s,
                        ),
                      }))
                    }
                  >
                    ＋ Thêm kỹ năng ({entry.goals.length}/{MAX_GOALS})
                  </button>
                )}
              </fieldset>
            );
          })}
        </div>
        {!plannable.length && <p className="notice">Chưa có kỹ năng đã dạy để lập ôn tập. Đánh dấu tiến độ giảng dạy trong Chương trình lớp.</p>}
      </section>

      <section className="card" aria-label="Hoạt động theo buổi">
        <h3>Hoạt động theo buổi</h3>
        <p className="hint">Hoạt động chung có nhiều em tham gia; hoạt động riêng của từng em diễn ra song song nên chỉ tính vào thời lượng của em đó.</p>
        {draft.sessions.map((session, si) => (
          <div key={si} className="session-block">
            <h4>
              Buổi {si + 1} · {weekdayOf(session.date)} {viDate(session.date)} · quỹ {session.minutes} phút
            </h4>
            <ul className="load-list" aria-label={`Thời lượng từng học sinh buổi ${si + 1}`}>
              {load[si].map((l) => (
                <li key={l.studentId} className={l.minutes > session.minutes ? "over" : undefined}>
                  {names.get(l.studentId)}: {l.minutes}/{session.minutes} phút{l.minutes > session.minutes ? " — vượt quỹ" : ""}
                </li>
              ))}
            </ul>
            {draft.activities.map((activity, ai) =>
              activity.session !== si ? null : (
                <ActivityEditor
                  key={ai}
                  index={ai}
                  activity={activity}
                  students={draft.students.map((s) => ({ id: s.studentId, name: names.get(s.studentId) ?? s.studentName ?? "" }))}
                  goalsOf={goalsOf}
                  skills={skills}
                  materials={materials}
                  editable={editable}
                  onChange={(value) => update((d) => ({ ...d, activities: d.activities.map((a, i) => (i === ai ? value : a)) }))}
                  onRemove={() => update((d) => ({ ...d, activities: d.activities.filter((_, i) => i !== ai) }))}
                />
              ),
            )}
            <button
              disabled={!editable || included.size === 0}
              onClick={() =>
                update((d) => ({
                  ...d,
                  activities: [...d.activities, { session: si, studentIds: [...included], skillId: null, materialId: null, minutes: 5, description: "" }],
                }))
              }
            >
              ＋ Thêm hoạt động
            </button>
          </div>
        ))}
        <label>
          Ghi chú của giáo viên
          <textarea rows={2} maxLength={1000} value={draft.note ?? ""} disabled={!editable} onChange={(e) => update((d) => ({ ...d, note: e.target.value || null }))} />
        </label>
      </section>

      {plan && plan.evidence.length > 0 && (
        <section className="card" aria-label="Căn cứ khi lưu">
          <h3>Căn cứ tại lần lưu gần nhất</h3>
          <ul className="item-list compact">
            {plan.evidence.map((e) => (
              <li key={`${e.studentId}/${e.skillId}`}>
                {names.get(e.studentId) ?? "Học sinh"} · {skills.find((k) => k.id === e.skillId)?.code} — {competencyStates[e.state]} (quy tắc v{e.ruleVersion})
                {e.evidence.length > 0 && `: ${e.evidence.map((x) => `${x.correct}/${x.taskCount} ngày ${viDate(x.date)}`).join(", ")}`}
              </li>
            ))}
          </ul>
        </section>
      )}

      {plan && plan.status === "approved" && plan.approvedContent && (
        <Execution base={base} plan={plan} names={names} canWrite={canWrite} denied={denied} onSaved={onSaved} />
      )}
      {plan && plan.approvedVersion && <Progress base={base} planId={plan.id} version={plan.version} denied={denied} />}
      {plan && (
        <details className="card">
          <summary>Lịch sử phiên bản ({plan.revisions.length})</summary>
          <ol className="item-list compact">
            {plan.revisions.map((r) => (
              <li key={r.version}>
                v{r.version} · {{ created: "Tạo", saved: "Lưu", approved: "Duyệt", execution: "Ghi nhận thực hiện" }[r.action] ?? r.action} · {new Date(r.recordedAt).toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" })}
              </li>
            ))}
          </ol>
        </details>
      )}
    </>
  );
}

function ActivityEditor({
  index,
  activity,
  students,
  goalsOf,
  skills,
  materials,
  editable,
  onChange,
  onRemove,
}: {
  index: number;
  activity: PlanActivity;
  students: { id: string; name: string }[];
  goalsOf: (studentId: string) => string[];
  skills: Curriculum["skills"];
  materials: Material[];
  editable: boolean;
  onChange: (value: PlanActivity) => void;
  onRemove: () => void;
}) {
  // A skill may be attached only if every participant has it as a goal.
  const common = activity.studentIds.length
    ? activity.studentIds.map(goalsOf).reduce((a, b) => a.filter((x) => b.includes(x)))
    : [];
  const usable = materials.filter((m) => m.status === "approved" && m.skillId === activity.skillId);
  return (
    <fieldset className="activity">
      <legend>
        Hoạt động {index + 1} · {activity.studentIds.length > 1 ? "Chung" : "Riêng"}
      </legend>
      <div className="participants" role="group" aria-label={`Học sinh tham gia hoạt động ${index + 1}`}>
        {students.map((s) => (
          <label key={s.id} className="checkbox-label">
            <input
              type="checkbox"
              checked={activity.studentIds.includes(s.id)}
              disabled={!editable}
              onChange={(e) => {
                const ids = e.target.checked ? [...activity.studentIds, s.id] : activity.studentIds.filter((x) => x !== s.id);
                const keep = activity.skillId && ids.every((id) => goalsOf(id).includes(activity.skillId!));
                onChange({ ...activity, studentIds: ids, skillId: keep ? activity.skillId : null, materialId: keep ? activity.materialId : null });
              }}
            />
            {s.name}
          </label>
        ))}
      </div>
      <div className="activity-fields">
        <label>
          Kỹ năng
          <select
            value={activity.skillId ?? ""}
            disabled={!editable}
            onChange={(e) => onChange({ ...activity, skillId: e.target.value || null, materialId: null })}
          >
            <option value="">Không gắn kỹ năng (hoạt động chung)</option>
            {skills.filter((k) => common.includes(k.id) || k.id === activity.skillId).map((k) => (
              <option key={k.id} value={k.id}>
                {k.code} · {k.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          Học liệu đã duyệt
          <select
            value={activity.materialId ?? ""}
            disabled={!editable || !activity.skillId}
            onChange={(e) => {
              const material = materials.find((m) => m.id === e.target.value);
              onChange({ ...activity, materialId: material?.id ?? null, minutes: material && !activity.minutes ? material.minutes : activity.minutes });
            }}
          >
            <option value="">Không dùng học liệu</option>
            {activity.materialId && !usable.some((m) => m.id === activity.materialId) && <option value={activity.materialId}>Học liệu chưa duyệt</option>}
            {usable.map((m) => (
              <option key={m.id} value={m.id}>
                {m.title} · {m.minutes} phút
              </option>
            ))}
          </select>
        </label>
        <label>
          Thời lượng (phút)
          <input type="number" min={1} max={240} value={activity.minutes} disabled={!editable} onChange={(e) => onChange({ ...activity, minutes: Number(e.target.value) })} />
        </label>
      </div>
      <label>
        Mô tả hoạt động
        <textarea rows={2} maxLength={500} value={activity.description} disabled={!editable} onChange={(e) => onChange({ ...activity, description: e.target.value })} />
      </label>
      <button disabled={!editable} onClick={onRemove}>
        Bỏ hoạt động
      </button>
    </fieldset>
  );
}

function Execution({
  base,
  plan,
  names,
  canWrite,
  denied,
  onSaved,
}: {
  base: string;
  plan: ReviewPlan;
  names: Map<string, string>;
  canWrite: boolean;
  denied: (e: unknown) => void;
  onSaved: (plan: ReviewPlan) => void;
}) {
  const content = plan.approvedContent!;
  const key = (a: number, s: string) => `${a}/${s}`;
  const initial = () => {
    const map = new Map(plan.execution.map((e) => [key(e.activity, e.studentId), e]));
    return content.activities.flatMap((a, ai) => a.studentIds.map((s) => map.get(key(ai, s)) ?? null)).filter((x): x is ExecutionEntry => !!x);
  };
  const [entries, setEntries] = useState<ExecutionEntry[]>(initial);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [saved, setSaved] = useState("");
  const find = (a: number, s: string) => entries.find((e) => e.activity === a && e.studentId === s);
  const set = (a: number, s: string, change: Partial<ExecutionEntry> | null) =>
    setEntries((list) => {
      const rest = list.filter((e) => !(e.activity === a && e.studentId === s));
      if (change === null) return rest;
      const current = list.find((e) => e.activity === a && e.studentId === s) ?? { activity: a, studentId: s, status: "done" as ExecutionStatus, actualMinutes: null, note: null };
      return [...rest, { ...current, ...change }];
    });
  async function save() {
    setBusy(true);
    setError("");
    setSaved("");
    try {
      const value = await api<ReviewPlan>(`${base}/review-plans/${plan.id}/execution`, "PUT", { entries, version: plan.version });
      onSaved(value);
      setSaved("Đã lưu ghi nhận thực hiện.");
    } catch (e) {
      setError(
        e instanceof ApiError && e.status === 409
          ? "Kế hoạch đã thay đổi ở phiên khác. Tải lại kế hoạch; ghi nhận đang nhập được giữ trên màn hình."
          : errorText(e, "Không thể lưu ghi nhận. Nội dung đang nhập được giữ nguyên."),
      );
      denied(e);
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="card" aria-label="Ghi nhận buổi ôn">
      <h3>Ghi nhận buổi ôn</h3>
      <p className="hint">Ghi rõ vắng hoặc chưa làm; đây là tình trạng thực tế, không phải kết luận học tập.</p>
      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              <th scope="col">Hoạt động</th>
              <th scope="col">Học sinh</th>
              <th scope="col">Thực hiện</th>
              <th scope="col">Phút thực tế</th>
              <th scope="col">Ghi chú</th>
            </tr>
          </thead>
          <tbody>
            {content.activities.flatMap((a, ai) =>
              a.studentIds.map((s) => {
                const entry = find(ai, s);
                return (
                  <tr key={key(ai, s)}>
                    <td>
                      Buổi {a.session + 1} · {a.description}
                    </td>
                    <td>{names.get(s) ?? "Học sinh"}</td>
                    <td>
                      <select
                        aria-label={`Thực hiện của ${names.get(s) ?? "học sinh"}, hoạt động ${ai + 1}`}
                        value={entry?.status ?? ""}
                        disabled={!canWrite || busy}
                        onChange={(e) => set(ai, s, e.target.value ? { status: e.target.value as ExecutionStatus } : null)}
                      >
                        <option value="">Chưa ghi</option>
                        {Object.entries(executionStates).map(([id, label]) => (
                          <option key={id} value={id}>
                            {label}
                          </option>
                        ))}
                      </select>
                    </td>
                    <td>
                      <input
                        type="number"
                        min={0}
                        max={240}
                        aria-label="Phút thực tế"
                        value={entry?.actualMinutes ?? ""}
                        disabled={!canWrite || busy || !entry}
                        onChange={(e) => set(ai, s, { actualMinutes: e.target.value === "" ? null : Number(e.target.value) })}
                      />
                    </td>
                    <td>
                      <input
                        maxLength={300}
                        aria-label="Ghi chú thực hiện"
                        value={entry?.note ?? ""}
                        disabled={!canWrite || busy || !entry}
                        onChange={(e) => set(ai, s, { note: e.target.value || null })}
                      />
                    </td>
                  </tr>
                );
              }),
            )}
          </tbody>
        </table>
      </div>
      <div className="form-actions">
        <button className="primary" disabled={!canWrite || busy} onClick={() => void save()}>
          Lưu ghi nhận
        </button>
        {saved && <span role="status" className="success-text">{saved}</span>}
      </div>
      {error && <p className="error" role="alert">{error}</p>}
    </section>
  );
}

function Progress({ base, planId, version, denied }: { base: string; planId: string; version: number; denied: (e: unknown) => void }) {
  const [progress, setProgress] = useState<PlanProgress | null>(null);
  const [error, setError] = useState("");
  useEffect(() => {
    let active = true;
    api<PlanProgress>(`${base}/review-plans/${planId}/progress`)
      .then((value) => active && setProgress(value))
      .catch((e) => {
        if (!active) return;
        setError(errorText(e, "Không thể tải so sánh trước/sau."));
        denied(e);
      });
    return () => {
      active = false;
    };
  }, [base, planId, version, denied]);
  const show = (list: PlanProgress["goals"][number]["before"]) =>
    list.length ? list.map((e) => (e.status === "assessed" ? `${e.correct}/${e.taskCount} (${viDate(e.date)})` : `Không làm (${viDate(e.date)})`)).join(", ") : "Chưa có";
  return (
    <section className="card" aria-label="Trước và sau tuần ôn">
      <h3>Trước và sau tuần ôn</h3>
      <p className="hint">
        So sánh kết quả theo kỹ năng đã xác nhận trước và từ đầu tuần ôn. Cải thiện không mặc định là do kế hoạch; mức đạt vẫn theo quy tắc của lớp.
      </p>
      {error && <p className="error" role="alert">{error}</p>}
      {!progress && !error && <p role="status">Đang tải…</p>}
      {progress && (
        <div className="table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th scope="col">Học sinh</th>
                <th scope="col">Kỹ năng</th>
                <th scope="col">Trước tuần ôn</th>
                <th scope="col">Từ tuần ôn</th>
                <th scope="col">Hiện tại</th>
              </tr>
            </thead>
            <tbody>
              {progress.goals.map((g) => (
                <tr key={`${g.studentId}/${g.skillId}`}>
                  <td>{g.studentName}</td>
                  <td>{g.skillName}</td>
                  <td>{show(g.before)}</td>
                  <td>{show(g.after)}</td>
                  <td>
                    <span className={`state ${g.currentState}`}>{competencyStates[g.currentState]}</span>
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
