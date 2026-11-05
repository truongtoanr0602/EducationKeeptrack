import { useCallback, useEffect, useRef, useState } from "react";
import { api, ApiError, type Me, type TeacherClass } from "./api";
import { today } from "./roster-api";
import type {
  Profile,
  Progress,
  ProgressRow,
  Plan,
} from "./personalized-learning-api";

const levels = {
  red: "Đỏ — Cần hỗ trợ",
  amber: "Vàng — Cần củng cố",
  green: "Xanh — Ổn trong buổi",
};
const mastery = {
  insufficient: "Chưa đủ bằng chứng",
  achieved: "Đạt theo hai lần đánh giá",
  needs_practice: "Cần luyện thêm theo bài kiểm tra",
  not_taught: "Chưa dạy",
};
const emptyProfile = (): Profile => ({
  textbook: "",
  semester: "Toán 2 - HK1",
  version: 0,
  skills: [{ id: crypto.randomUUID(), title: "", taught: false }],
});

export function PersonalizedLearning({
  classroom,
  me,
}: {
  classroom: TeacherClass;
  me: Me;
}) {
  const base = `/learning/classes/${classroom.id}`;
  const [profile, setProfile] = useState<Profile>(emptyProfile);
  const [progress, setProgress] = useState<Progress | null>(null);
  const [plans, setPlans] = useState<Plan[]>([]);
  const [date, setDate] = useState(today);
  const [busy, setBusy] = useState(false);
  const [ready, setReady] = useState(false);
  const [blocked, setBlocked] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [filter, setFilter] = useState(false);
  const generation = useRef(0);
  const disabled = busy || blocked || !ready || !me.access.canWrite;
  function fail(e: unknown) {
    setError(
      e instanceof Error
        ? e.message
        : "Không thể lưu. Nội dung đang nhập được giữ nguyên.",
    );
    if (e instanceof ApiError && [401, 403].includes(e.status)) {
      setBlocked(true);
      setProgress(null);
      setPlans([]);
    }
  }
  const loadProgress = useCallback(async () => {
    const g = ++generation.current;
    try {
      const p = await api<Progress>(`${base}/progress?date=${date}`);
      if (g === generation.current) setProgress(p);
    } catch (e) {
      if (g === generation.current) {
        setProgress(null);
        fail(e);
      }
    }
  }, [base, date]);
  useEffect(() => {
    let alive = true;
    setReady(false);
    setProgress(null);
    setPlans([]);
    setBlocked(false);
    void Promise.all([
      api<Profile>(`${base}/profile`),
      api<Plan[]>(`${base}/plans`),
    ])
      .then(([p, list]) => {
        if (!alive) return;
        setProfile(
          p.skills.length ? p : { ...p, skills: emptyProfile().skills },
        );
        setPlans(list);
        setReady(true);
      })
      .catch((e) => {
        if (alive) fail(e);
      });
    return () => {
      alive = false;
      generation.current++;
    };
  }, [base]);
  useEffect(() => {
    if (ready) void loadProgress();
    return () => {
      generation.current++;
    };
  }, [ready, loadProgress]);
  async function action(work: () => Promise<void>, success: string) {
    setBusy(true);
    setError("");
    setMessage("");
    try {
      await work();
      setMessage(success);
      return true;
    } catch (e) {
      fail(e);
      return false;
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="personalized-panel" aria-label="Cá nhân hóa học tập">
      <h2>Cá nhân hóa học tập</h2>
      <p>
        Theo dõi từng kỹ năng qua các buổi. Ba màu là quan sát nhanh, không thay
        thế bài kiểm tra.
      </p>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      {message && <p role="status">{message}</p>}
      {blocked && (
        <p>
          Phiên hoặc quyền truy cập đã thay đổi. Đăng nhập lại và tải lại trang.
        </p>
      )}
      {!me.access.canWrite && <p>Chỉ xem dữ liệu đã lưu.</p>}
      <details open={!profile.version}>
        <summary>Cấu hình bộ sách và kỹ năng</summary>
        <form
          onSubmit={(e) => {
            e.preventDefault();
            void action(async () => {
              const saved = await api<Profile>(
                `${base}/profile`,
                "PUT",
                profile,
              );
              setProfile(saved);
              await loadProgress();
            }, "Đã lưu cấu hình.");
          }}
        >
          <label>
            Bộ sách của lớp
            <input
              required
              maxLength={120}
              value={profile.textbook}
              disabled={disabled}
              onChange={(e) =>
                setProfile({ ...profile, textbook: e.target.value })
              }
            />
          </label>
          <label>
            Học kỳ
            <input
              required
              maxLength={120}
              value={profile.semester}
              disabled={disabled}
              onChange={(e) =>
                setProfile({ ...profile, semester: e.target.value })
              }
            />
          </label>
          {profile.skills.map((skill, i) => (
            <div className="skill-config" key={skill.id}>
              <label>
                Tên kỹ năng {i + 1}
                <input
                  required
                  maxLength={160}
                  value={skill.title}
                  disabled={disabled}
                  onChange={(e) =>
                    setProfile({
                      ...profile,
                      skills: profile.skills.map((s) =>
                        s.id === skill.id ? { ...s, title: e.target.value } : s,
                      ),
                    })
                  }
                />
              </label>
              <label>
                <input
                  type="checkbox"
                  aria-label={`Đã dạy kỹ năng ${i + 1}`}
                  checked={skill.taught}
                  disabled={disabled}
                  onChange={(e) =>
                    setProfile({
                      ...profile,
                      skills: profile.skills.map((s) =>
                        s.id === skill.id
                          ? { ...s, taught: e.target.checked }
                          : s,
                      ),
                    })
                  }
                />{" "}
                Đã dạy
              </label>
            </div>
          ))}
          <button
            type="button"
            disabled={disabled || profile.skills.length >= 100}
            onClick={() =>
              setProfile({
                ...profile,
                skills: [
                  ...profile.skills,
                  { id: crypto.randomUUID(), title: "", taught: false },
                ],
              })
            }
          >
            Thêm kỹ năng
          </button>
          <button disabled={disabled}>Lưu cấu hình kỹ năng</button>
        </form>
      </details>
      <label>
        Ngày theo dõi
        <input
          type="date"
          required
          disabled={busy}
          value={date}
          onChange={(e) => {
            setProgress(null);
            setDate(e.target.value);
          }}
        />
      </label>
      <button
        disabled={busy}
        onClick={() =>
          void action(async () => {
            const p = await api<Profile>(`${base}/profile`);
            setProfile(
              p.skills.length ? p : { ...p, skills: emptyProfile().skills },
            );
            setPlans(await api<Plan[]>(`${base}/plans`));
            setReady(true);
            await loadProgress();
          }, "Đã tải lại dữ liệu.")
        }
      >
        Tải lại dữ liệu cá nhân hóa
      </button>
      <label>
        <input
          type="checkbox"
          checked={filter}
          onChange={(e) => setFilter(e.target.checked)}
        />{" "}
        Chỉ việc cần kiểm tra lại
      </label>
      <p>
        Đạt thí điểm: hai lần hợp lệ gần nhất, mỗi lần ít nhất 5 nhiệm vụ và ít
        nhất 80% đúng.
      </p>
      {progress && !progress.students.length && (
        <p>
          Chưa có học sinh theo học ở ngày đã chọn. Thêm học sinh trong danh
          sách lớp.
        </p>
      )}
      <div className="skill-grid">
        {progress?.rows
          .filter((r) => !filter || r.pendingReview)
          .map((row) => (
            <SkillCard
              key={`${base}:${row.studentId}:${row.skillId}:${date}`}
              row={row}
              disabled={disabled || !row.taught || date > today()}
              save={(kind, input) =>
                action(async () => {
                  await api(`${base}/${kind}`, "POST", {
                    ...input,
                    studentId: row.studentId,
                    skillId: row.skillId,
                    date,
                  });
                  await loadProgress();
                }, "Đã lưu ghi nhận.")
              }
            />
          ))}
      </div>
      {ready && !blocked && (
        <PlanEditor
          base={base}
          date={date}
          plans={plans}
          progress={progress}
          disabled={disabled || !profile.version}
          run={action}
          refresh={async () => setPlans(await api<Plan[]>(`${base}/plans`))}
        />
      )}
    </section>
  );
}

function SkillCard({
  row,
  disabled,
  save,
}: {
  row: ProgressRow;
  disabled: boolean;
  save: (kind: string, input: object) => Promise<boolean>;
}) {
  const [note, setNote] = useState("");
  const [title, setTitle] = useState("");
  const [total, setTotal] = useState(5);
  const [correct, setCorrect] = useState(0);
  const [confirmed, setConfirmed] = useState(false);
  const [evidenceSaved, setEvidenceSaved] = useState(false);
  const observationKey = useRef(crypto.randomUUID());
  const observationPayload = useRef("");
  const evidenceKey = useRef(crypto.randomUUID());
  return (
    <article
      className="skill-card"
      aria-label={`${row.studentName} — ${row.skillTitle}`}
    >
      <h3>
        {row.studentName} — {row.skillTitle}
      </h3>
      <p
        className={`skill-level ${row.latestObservation?.level ?? "unobserved"}`}
      >
        {row.latestObservation
          ? levels[row.latestObservation.level]
          : "Chưa đánh giá"}
      </p>
      {row.latestObservation && (
        <p>
          Quan sát ngày {row.latestObservation.date}
          {row.latestObservation.note && ` · ${row.latestObservation.note}`}
        </p>
      )}
      <p>{mastery[row.mastery]}</p>
      {row.pendingReview && (
        <p className="notice">Cần kiểm tra lại ở buổi tham dự tiếp theo.</p>
      )}
      <label>
        Ghi chú quan sát
        <input
          maxLength={500}
          value={note}
          disabled={disabled}
          onChange={(e) => {
            observationKey.current = crypto.randomUUID();
            setNote(e.target.value);
          }}
        />
      </label>
      <div className="level-actions">
        {(Object.keys(levels) as (keyof typeof levels)[]).map((level) => (
          <button
            className={`skill-choice ${level}`}
            disabled={disabled}
            key={level}
            onClick={() => {
              const payload = JSON.stringify({ level, note });
              if (observationPayload.current !== payload) {
                observationPayload.current = payload;
                observationKey.current = crypto.randomUUID();
              }
              void save("observations", {
                level,
                note,
                requestId: observationKey.current,
              });
            }}
          >
            {levels[level]}
          </button>
        ))}
      </div>
      <details>
        <summary>Nhập kết quả bài kiểm tra chung hoặc riêng</summary>
        <form
          onSubmit={(e) => {
            e.preventDefault();
            void save("evidence", {
              title,
              total,
              correct,
              confirmed,
              requestId: evidenceKey.current,
            }).then((ok) => {
              if (ok) setEvidenceSaved(true);
            });
          }}
        >
          <label>
            Tên bài kiểm tra
            <input
              required
              maxLength={160}
              value={title}
              disabled={disabled || evidenceSaved}
              onChange={(e) => setTitle(e.target.value)}
            />
          </label>
          <label>
            Tổng nhiệm vụ
            <input
              type="number"
              required
              min={1}
              max={1000}
              value={total}
              disabled={disabled || evidenceSaved}
              onChange={(e) => setTotal(Number(e.target.value))}
            />
          </label>
          <label>
            Số đúng
            <input
              type="number"
              required
              min={0}
              max={total}
              value={correct}
              disabled={disabled || evidenceSaved}
              onChange={(e) => setCorrect(Number(e.target.value))}
            />
          </label>
          <label>
            <input
              type="checkbox"
              checked={confirmed}
              disabled={disabled || evidenceSaved}
              onChange={(e) => setConfirmed(e.target.checked)}
            />{" "}
            Xác nhận đây là kết quả độc lập để đánh giá kỹ năng
          </label>
          <button disabled={disabled || evidenceSaved}>
            Lưu kết quả kỹ năng
          </button>
          <p>
            Thử lại cùng nội dung không tạo bài mới. Chỉ bắt đầu bài mới khi học
            sinh thực sự làm lần khác.
          </p>
          <button
            type="button"
            disabled={disabled}
            onClick={() => {
              evidenceKey.current = crypto.randomUUID();
              setEvidenceSaved(false);
              setTitle("");
              setCorrect(0);
              setConfirmed(false);
            }}
          >
            Bắt đầu bài kiểm tra mới
          </button>
        </form>
      </details>
      <details>
        <summary>
          Lịch sử và bằng chứng ({row.observations.length + row.evidence.length}
          )
        </summary>
        {row.observations.map((o) => (
          <p key={o.id}>
            {o.date}: {levels[o.level]} {o.note}
          </p>
        ))}
        {row.evidence.map((e) => (
          <p key={e.id}>
            {e.date}: {e.title} — {e.correct}/{e.total},{" "}
            {e.confirmed ? "đã xác nhận" : "bản nháp"}
          </p>
        ))}
      </details>
    </article>
  );
}

function PlanEditor({
  base,
  date,
  plans,
  progress,
  disabled,
  run,
  refresh,
}: {
  base: string;
  date: string;
  plans: Plan[];
  progress: Progress | null;
  disabled: boolean;
  run: (work: () => Promise<void>, success: string) => Promise<boolean>;
  refresh: () => Promise<void>;
}) {
  const [editing, setEditing] = useState<Plan | null>(null);
  const [title, setTitle] = useState("");
  const [content, setContent] = useState("");
  const [minutes, setMinutes] = useState(30);
  const [priorities, setPriorities] = useState<string[]>([]);
  const [activities, setActivities] = useState([
    { title: "Khởi động chung", minutes: 5 },
    { title: "Luyện riêng", minutes: 20 },
    { title: "Kiểm tra lại", minutes: 5 },
  ]);
  return (
    <div className="plan-editor">
      <h3>Kế hoạch ôn tập</h3>
      <p>
        AI tạo kế hoạch chưa được bật: đang chờ nhà cung cấp và chính sách hạn
        mức. Tạo, sửa và duyệt kế hoạch thủ công tại đây.
      </p>
      <button
        type="button"
        disabled={disabled || !!editing}
        onClick={() => {
          const selected = new Map<string, ProgressRow[]>();
          const rank = (r: ProgressRow) =>
            r.latestObservation?.level === "red"
              ? 0
              : r.latestObservation?.level === "amber"
                ? 1
                : r.mastery === "needs_practice"
                  ? 2
                  : 3;
          for (const row of [...(progress?.rows ?? [])]
            .filter((r) => r.taught && rank(r) < 3)
            .sort((a, b) => rank(a) - rank(b))) {
            const list = selected.get(row.studentId) ?? [];
            if (list.length < 2) {
              list.push(row);
              selected.set(row.studentId, list);
            }
          }
          const rows = [...selected.values()].flat();
          setPriorities(rows.map((r) => `${r.studentId}:${r.skillId}`));
          setTitle(`Ôn tập ngày ${date}`);
          setContent(
            rows.length
              ? rows
                  .map(
                    (r) =>
                      `${r.studentName}: ôn ${r.skillTitle}. Căn cứ: ${r.latestObservation ? `${levels[r.latestObservation.level]} ngày ${r.latestObservation.date}` : mastery[r.mastery]}. Giáo viên bổ sung học liệu và cách kiểm tra lại.`,
                  )
                  .join("\n")
              : "Chưa có kỹ năng cần ưu tiên từ dữ liệu hiện tại. Giáo viên chọn mục tiêu hoặc khảo sát bổ sung.",
          );
        }}
      >
        Tạo khung từ dữ liệu theo dõi (không dùng AI)
      </button>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          void run(async () => {
            await api(
              `${base}/plans${editing ? `/${editing.id}` : ""}`,
              editing ? "PUT" : "POST",
              {
                title,
                date: editing?.date ?? date,
                content,
                minutes,
                priorities: priorities.map((k) => {
                  const [studentId, skillId] = k.split(":");
                  return { studentId, skillId };
                }),
                activities,
                version: editing?.version ?? 0,
              },
            );
            await refresh();
            setEditing(null);
            setTitle("");
            setContent("");
            setPriorities([]);
          }, "Đã lưu dự thảo kế hoạch.");
        }}
      >
        <label>
          Tên kế hoạch
          <input
            required
            maxLength={160}
            value={title}
            disabled={disabled}
            onChange={(e) => setTitle(e.target.value)}
          />
        </label>
        <label>
          Quỹ phút ôn tập
          <input
            type="number"
            required
            min={1}
            max={480}
            value={minutes}
            disabled={disabled}
            onChange={(e) => setMinutes(Number(e.target.value))}
          />
        </label>
        <p>
          Ngày kế hoạch: {editing?.date ?? date}. Chọn tối đa hai kỹ năng mỗi
          em.
        </p>
        <fieldset disabled={disabled}>
          <legend>Kỹ năng ưu tiên</legend>
          {progress?.rows
            .filter((r) => r.taught)
            .map((r) => {
              const key = `${r.studentId}:${r.skillId}`;
              return (
                <label key={key}>
                  <input
                    type="checkbox"
                    checked={priorities.includes(key)}
                    onChange={(e) =>
                      setPriorities(
                        e.target.checked
                          ? [...priorities, key]
                          : priorities.filter((x) => x !== key),
                      )
                    }
                  />{" "}
                  {r.studentName}: {r.skillTitle}
                </label>
              );
            })}
        </fieldset>
        <fieldset disabled={disabled}>
          <legend>Hoạt động và thời lượng</legend>
          {activities.map((a, i) => (
            <div className="skill-config" key={i}>
              <label>
                Hoạt động {i + 1}
                <input
                  required
                  maxLength={160}
                  value={a.title}
                  onChange={(e) =>
                    setActivities(
                      activities.map((v, j) =>
                        j === i ? { ...v, title: e.target.value } : v,
                      ),
                    )
                  }
                />
              </label>
              <label>
                Phút hoạt động {i + 1}
                <input
                  type="number"
                  required
                  min={1}
                  max={480}
                  value={a.minutes}
                  onChange={(e) =>
                    setActivities(
                      activities.map((v, j) =>
                        j === i ? { ...v, minutes: Number(e.target.value) } : v,
                      ),
                    )
                  }
                />
              </label>
            </div>
          ))}
        </fieldset>
        <label>
          Nội dung kế hoạch
          <textarea
            required
            maxLength={10000}
            value={content}
            disabled={disabled}
            onChange={(e) => setContent(e.target.value)}
          />
        </label>
        <button disabled={disabled}>Lưu dự thảo kế hoạch</button>
        {editing && (
          <button
            type="button"
            onClick={() => {
              setEditing(null);
              setTitle("");
              setContent("");
              setPriorities([]);
            }}
          >
            Hủy sửa
          </button>
        )}
      </form>
      {plans.map((p) => (
        <article
          className="skill-card"
          key={p.id}
          aria-label={`Kế hoạch ${p.title}`}
        >
          <h4>
            {p.title} · {p.date} · {p.minutes} phút
          </h4>
          <p>{p.status === "approved" ? "Đã duyệt" : "Dự thảo"}</p>
          <p className="plan-content">{p.content}</p>
          <button
            disabled={disabled}
            onClick={() => {
              setEditing(p);
              setTitle(p.title);
              setContent(p.content);
              setMinutes(p.minutes);
              setPriorities(
                (
                  JSON.parse(p.prioritiesJson) as {
                    StudentId: string;
                    SkillId: string;
                  }[]
                ).map((x) => `${x.StudentId}:${x.SkillId}`),
              );
              setActivities(
                JSON.parse(p.activitiesJson).map(
                  (x: { Title: string; Minutes: number }) => ({
                    title: x.Title,
                    minutes: x.Minutes,
                  }),
                ),
              );
            }}
          >
            Sửa thành bản nháp
          </button>
          {p.status === "draft" && (
            <button
              disabled={disabled}
              onClick={() =>
                void run(async () => {
                  await api(`${base}/plans/${p.id}/approve`, "POST", {
                    version: p.version,
                  });
                  await refresh();
                }, "Đã duyệt kế hoạch.")
              }
            >
              Duyệt kế hoạch này
            </button>
          )}
          <details>
            <summary>Căn cứ và lịch sử phiên bản</summary>
            <pre>{JSON.stringify(JSON.parse(p.snapshotJson), null, 2)}</pre>
            <pre>{JSON.stringify(JSON.parse(p.historyJson), null, 2)}</pre>
          </details>
        </article>
      ))}
    </div>
  );
}
