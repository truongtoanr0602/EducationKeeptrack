import { useEffect, useRef, useState, type FormEvent } from "react";
import {
  api,
  ApiError,
  limits,
  type ClassDraft,
  type TeacherClass,
} from "./api";
import {
  FlowError,
  emptyProgress,
  runFlow,
  type FlowApi,
  type FlowProgress,
  type FlowStep,
  type FlowStop,
} from "./class-flow";
import { ScheduleEditor } from "./ScheduleEditor";
import { today } from "./roster-api";
import type { Slot } from "./schedule-calendar";
import { money, type RatePlan } from "./tuition-api";
import "./create-class.css";

/** Everything the dialog needs to carry on after the session ended in the middle of creating a class. */
export type FlowSnapshot = {
  step: 1 | 2;
  slots: Slot[];
  effectiveFrom: string;
  fee: string;
  generate: boolean;
  weeks: number;
  progress: FlowProgress;
  created: TeacherClass | null;
};
export type CreateClassDraft = ClassDraft & { flow?: FlowSnapshot };

const WEEK_CHOICES = [2, 4, 8, 12];
const STEP_NAMES: Record<FlowStep, string> = {
  class: "Tạo lớp",
  schedule: "Lưu lịch tuần",
  fee: "Đặt học phí",
  sessions: "Tạo buổi học dự kiến",
};

// What the server said, in the two kinds the engine needs to tell apart: it certainly did not do the work (a 4xx
// answer) or it may have (no answer, a 5xx, a timeout).
function toFlowError(e: unknown): FlowError {
  if (e instanceof ApiError) {
    const message =
      Object.values(e.errors ?? {})
        .flat()
        .join(" ") || e.message;
    return new FlowError(message, {
      definite: e.status >= 400 && e.status < 500 && e.status !== 408,
      status: e.status,
      field: e.status === 400 && !!e.errors && Object.keys(e.errors).length > 0,
      errors: e.errors,
    });
  }
  return new FlowError("Mất kết nối.", { definite: false });
}
async function call<T>(request: () => Promise<T>): Promise<T> {
  try {
    return await request();
  } catch (e) {
    throw toFlowError(e);
  }
}

export function CreateClass({
  initial,
  onClose,
  onCreated,
  onSaved,
  onExpired,
}: {
  initial: CreateClassDraft;
  onClose: () => void;
  /** The class exists now (called once, as soon as it is created), even if the rest of the setup is still to do. */
  onCreated: (item: TeacherClass) => void;
  /** The whole setup is done. */
  onSaved: (item: TeacherClass) => void;
  onExpired: (draft: CreateClassDraft) => void;
}) {
  const flow = initial.flow;
  const dialog = useRef<HTMLDialogElement>(null);
  const nameInput = useRef<HTMLInputElement>(null);
  const scheduleHeading = useRef<HTMLHeadingElement>(null);
  const [draft, setDraft] = useState<ClassDraft>({
    name: initial.name,
    subject: initial.subject,
    description: initial.description,
  });
  const [step, setStep] = useState<1 | 2>(flow?.step ?? 1);
  const [slots, setSlots] = useState<Slot[]>(flow?.slots ?? []);
  const [effectiveFrom, setEffectiveFrom] = useState(
    flow?.effectiveFrom ?? today(),
  );
  const [fee, setFee] = useState(flow?.fee ?? "");
  const [generate, setGenerate] = useState(flow?.generate ?? true);
  const [weeks, setWeeks] = useState(flow?.weeks ?? 4);
  const [progress, setProgress] = useState<FlowProgress>(
    flow?.progress ?? emptyProgress(),
  );
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [message, setMessage] = useState("");
  const [stop, setStop] = useState<FlowStop | null>(null);
  const [pending, setPending] = useState(false);
  const [busy, setBusy] = useState(false);
  // The class object the server returned, and the class list last read: either may hold the class that was made.
  const created = useRef<TeacherClass | null>(flow?.created ?? null);
  const listed = useRef<TeacherClass[]>([]);
  const notified = useRef(!!flow?.progress.classId);
  const latest = useRef(progress);

  useEffect(() => {
    dialog.current?.showModal();
  }, []);
  useEffect(() => {
    if (step === 2) scheduleHeading.current?.focus();
    else if (!progress.classId) nameInput.current?.focus();
  }, [step]);

  const hasSlots = slots.length > 0;
  const classMade = progress.classId !== null;
  const started = classMade || stop !== null || busy;

  const flowApi: FlowApi = {
    listClasses: () =>
      call(
        async () => (listed.current = await api<TeacherClass[]>("/classes")),
      ),
    createClass: (input) =>
      call(async () => {
        const made = await api<TeacherClass>("/classes", "POST", input);
        created.current = made;
        return made;
      }),
    getSchedule: (id) =>
      call(() =>
        api<{
          version: number;
          effectiveFrom: string | null;
          slots: { dayOfWeek: number; startTime: string; endTime: string }[];
        }>(`/classes/${id}/schedule`),
      ),
    saveSchedule: (id, input) =>
      call(() =>
        api<{ version: number }>(`/classes/${id}/schedule`, "PUT", input),
      ),
    getRates: (id) => call(() => api<RatePlan>(`/billing/classes/${id}/rates`)),
    saveRate: (id, input) =>
      call(async () => {
        await api(`/billing/classes/${id}/rates`, "POST", input);
      }),
    generate: (id, input) =>
      call(async () => {
        await api(`/classes/${id}/sessions/generate`, "POST", input);
      }),
  };

  function snapshot(): CreateClassDraft {
    return {
      ...draft,
      flow: {
        step,
        slots,
        effectiveFrom,
        fee,
        generate,
        weeks,
        progress: latest.current,
        created: created.current,
      },
    };
  }
  function recorded(next: FlowProgress) {
    latest.current = next;
    setProgress(next);
    if (next.classId && !notified.current) {
      const item =
        created.current?.id === next.classId
          ? created.current
          : listed.current.find((c) => c.id === next.classId);
      if (item) {
        created.current = item;
        notified.current = true;
        onCreated(item);
      }
    }
  }
  function infoErrors() {
    const found: Record<string, string[]> = {};
    if (!draft.name.trim()) found.name = ["Vui lòng nhập tên lớp."];
    if (!draft.subject.trim()) found.subject = ["Vui lòng nhập môn học."];
    return found;
  }
  function next() {
    const found = infoErrors();
    setErrors(found);
    if (Object.keys(found).length === 0) setStep(2);
  }

  async function submit(event?: FormEvent) {
    event?.preventDefault();
    if (busy) return;
    const found = infoErrors();
    setErrors(found);
    if (Object.keys(found).length > 0) {
      setStep(1);
      return;
    }
    if (pending) {
      setStep(2);
      setMessage(
        "Bạn đang soạn một khung giờ chưa thêm vào lịch. Bấm “Thêm vào lịch” (hoặc “Cập nhật khung giờ”) trước khi tạo lớp.",
      );
      return;
    }
    setMessage("");
    setStop(null);
    setBusy(true);
    const result = await runFlow({
      input: {
        name: draft.name,
        subject: draft.subject,
        description: draft.description,
        slots,
        effectiveFrom,
        fee,
        generate,
        weeks,
      },
      progress: latest.current,
      api: flowApi,
      today: today(),
      update: recorded,
    });
    setBusy(false);
    const problem = result.stopped;
    if (!problem) {
      onSaved(
        created.current ?? {
          id: latest.current.classId!,
          name: draft.name.trim(),
          subject: draft.subject.trim(),
          description: draft.description.trim() || null,
        },
      );
      return;
    }
    if (problem.reason === "auth") {
      onExpired(snapshot());
      return;
    }
    if (problem.reason === "invalid" && problem.step === "class") {
      setErrors(problem.errors ?? { name: [problem.message] });
      setStep(1);
      return;
    }
    if (problem.step !== "class") setStep(2);
    setStop(problem);
  }

  const perMonth = Math.round((slots.length * 52) / 12);
  const lockClass = classMade;
  const stepState = (s: FlowStep) => {
    const done =
      s === "class"
        ? classMade
        : s === "schedule"
          ? progress.scheduleVersion !== null
          : s === "fee"
            ? progress.feeSaved
            : progress.sessionsDone;
    if (done) return "done";
    if (stop?.step === s) return "failed";
    return "waiting";
  };
  const applicable: FlowStep[] = [
    "class",
    ...(hasSlots ? (["schedule"] as FlowStep[]) : []),
    ...(fee ? (["fee"] as FlowStep[]) : []),
    ...(hasSlots && generate ? (["sessions"] as FlowStep[]) : []),
  ];
  const working = busy
    ? applicable.find((s) => stepState(s) !== "done")
    : undefined;
  const stateText = { done: "Xong", failed: "Lỗi", waiting: "Chờ" } as const;

  return (
    <dialog
      ref={dialog}
      className="create-flow"
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

        <section hidden={step !== 1} aria-label="Thông tin lớp">
          <p className="step-label">Bước 1/2 · Thông tin lớp</p>
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
                  disabled={busy || lockClass}
                  aria-invalid={!!errors[field]}
                  aria-describedby={`${field}-help`}
                  onChange={(e) =>
                    setDraft({ ...draft, [field]: e.target.value })
                  }
                />
              ) : (
                <input
                  ref={field === "name" ? nameInput : undefined}
                  autoFocus={field === "name"}
                  id={field}
                  required
                  maxLength={limits[field]}
                  value={draft[field]}
                  disabled={busy || lockClass}
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
          <p className="hint">
            “Lưu lớp” tạo lớp ngay; bạn đặt lịch học và học phí sau cũng được.
          </p>
        </section>

        <section hidden={step !== 2} aria-label="Lịch học và học phí">
          <p className="step-label">Bước 2/2 · Lịch học và học phí</p>
          <h3 tabIndex={-1} ref={scheduleHeading}>
            Lịch học
          </h3>
          <p>
            Chọn các ngày và giờ học trong tuần. Bỏ trống thì lớp được tạo mà
            chưa có lịch.
          </p>
          <ScheduleEditor
            slots={slots}
            onSlotsChange={setSlots}
            effectiveFrom={effectiveFrom}
            onEffectiveFromChange={setEffectiveFrom}
            disabled={busy || progress.scheduleVersion !== null}
            weeks={generate ? weeks : undefined}
            emptyHint="Chưa có khung giờ nào. Lớp sẽ được tạo mà chưa có lịch; bạn đặt lịch sau cũng được."
            onPendingChange={setPending}
          >
            <div className="field fee-field">
              <label htmlFor="class-fee">
                Học phí mỗi buổi (đồng, tùy chọn)
              </label>
              <input
                id="class-fee"
                inputMode="numeric"
                autoComplete="off"
                maxLength={12}
                value={fee}
                disabled={busy || progress.feeSaved}
                onChange={(e) => setFee(e.target.value.replace(/\D/g, ""))}
              />
              <small className="hint">
                {fee && hasSlots
                  ? `≈ ${perMonth} buổi/tháng × ${money(fee)} = ${money(String(BigInt(fee) * BigInt(perMonth)))}/tháng`
                  : "Để trống thì đặt học phí sau; chưa có học phí thì chưa lập được hóa đơn."}
              </small>
            </div>
            <div className="generate-row">
              <label>
                <input
                  type="checkbox"
                  checked={generate && hasSlots}
                  disabled={busy || !hasSlots || progress.sessionsDone}
                  onChange={(e) => setGenerate(e.target.checked)}
                />
                Tạo buổi học dự kiến cho
              </label>
              <select
                aria-label="Số tuần tạo buổi học"
                value={weeks}
                disabled={
                  busy || !hasSlots || !generate || progress.sessionsDone
                }
                onChange={(e) => setWeeks(Number(e.target.value))}
              >
                {WEEK_CHOICES.map((w) => (
                  <option key={w} value={w}>
                    {w} tuần tới
                  </option>
                ))}
              </select>
            </div>
          </ScheduleEditor>
        </section>

        {started && (
          <ul className="create-steps" aria-label="Tiến trình tạo lớp">
            {applicable.map((s) => {
              const state = stepState(s);
              return (
                <li key={s} className={state}>
                  <span className="state">
                    {working === s ? "Đang làm…" : stateText[state]}
                  </span>
                  <span>{STEP_NAMES[s]}</span>
                </li>
              );
            })}
          </ul>
        )}
        {message && (
          <p className="error" role="alert">
            {message}
          </p>
        )}
        {stop && (
          <p className="error" role="alert">
            {stop.message}
            {classMade &&
              " Lớp đã được tạo; bấm “Thử lại” để làm tiếp phần còn lại."}
          </p>
        )}

        <div className="dialog-actions">
          {step === 2 && (
            <button type="button" disabled={busy} onClick={() => setStep(1)}>
              Quay lại
            </button>
          )}
          <button type="button" disabled={busy} onClick={onClose}>
            {classMade ? "Đóng, làm tiếp sau" : "Hủy"}
          </button>
          {step === 1 && !classMade && (
            <>
              <button type="submit" disabled={busy}>
                {busy ? (
                  <>
                    <span className="spinner" /> Đang lưu…
                  </>
                ) : (
                  "Lưu lớp"
                )}
              </button>
              <button
                type="button"
                className="primary"
                disabled={busy}
                onClick={next}
              >
                Tiếp theo: lịch học
              </button>
            </>
          )}
          {(step === 2 || classMade) && (
            <button className="primary" disabled={busy}>
              {busy ? (
                <>
                  <span className="spinner" /> Đang lưu…
                </>
              ) : classMade || stop ? (
                "Thử lại"
              ) : (
                "Tạo lớp"
              )}
            </button>
          )}
        </div>
      </form>
    </dialog>
  );
}
