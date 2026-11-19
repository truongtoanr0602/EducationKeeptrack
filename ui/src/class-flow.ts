import { addDays, hhmm, type Slot } from "./schedule-calendar";

// The engine behind "create a class in one flow": class, then weekly schedule, then fee, then the planned sessions,
// each with the API that already exists. It stops at the first failure and remembers what is done, so a retry never
// creates a second class and never repeats a write whose answer was lost: it reads first, then writes only what is
// missing. Pure and driven by an injected API, so every rule is tested without a page or a server.

/** A failed request, told apart by whether the server certainly did not do the work. */
export class FlowError extends Error {
  definite: boolean;
  status?: number;
  /** A validation message about the data the teacher typed (HTTP 400 with field errors). */
  field: boolean;
  errors?: Record<string, string[]>;
  constructor(
    message: string,
    options: {
      definite: boolean;
      status?: number;
      field?: boolean;
      errors?: Record<string, string[]>;
    },
  ) {
    super(message);
    this.definite = options.definite;
    this.status = options.status;
    this.field = options.field ?? false;
    this.errors = options.errors;
  }
}

export type FlowInput = {
  name: string;
  subject: string;
  description: string;
  /** Times as "HH:mm". */
  slots: Slot[];
  effectiveFrom: string;
  /** Digits only, "" for no fee. */
  fee: string;
  generate: boolean;
  weeks: number;
};

export type FlowProgress = {
  classId: string | null;
  /** The answer to "create the class" was lost, so it may exist. */
  classUnknown: boolean;
  /** Ids of the classes that existed before the first write; they are never adopted. */
  baseline: string[] | null;
  scheduleVersion: number | null;
  scheduleUnknown: boolean;
  feeSaved: boolean;
  feeUnknown: boolean;
  sessionsDone: boolean;
};
export const emptyProgress = (): FlowProgress => ({
  classId: null,
  classUnknown: false,
  baseline: null,
  scheduleVersion: null,
  scheduleUnknown: false,
  feeSaved: false,
  feeUnknown: false,
  sessionsDone: false,
});

type ServerSlot = { dayOfWeek: number; startTime: string; endTime: string };
export type FlowApi = {
  listClasses(): Promise<{ id: string; name: string; subject: string }[]>;
  createClass(input: {
    name: string;
    subject: string;
    description: string;
  }): Promise<{ id: string }>;
  getSchedule(classId: string): Promise<{
    version: number;
    effectiveFrom: string | null;
    slots: ServerSlot[];
  }>;
  saveSchedule(
    classId: string,
    input: { version: number; effectiveFrom: string; slots: ServerSlot[] },
  ): Promise<{ version: number }>;
  getRates(classId: string): Promise<{
    version: number;
    rates: { effectiveFrom: string; unitPriceVnd: string }[];
  }>;
  saveRate(
    classId: string,
    input: { effectiveFrom: string; unitPriceVnd: string; version: number },
  ): Promise<void>;
  generate(
    classId: string,
    input: { from: string; to: string; scheduleVersion: number },
  ): Promise<void>;
};

export type FlowStep = "class" | "schedule" | "fee" | "sessions";
export type FlowStop = {
  step: FlowStep;
  /** invalid: the teacher's data was refused, go back to the form; auth: the session ended. */
  reason: "invalid" | "auth" | "failure" | "ambiguous";
  message: string;
  errors?: Record<string, string[]>;
};

export const MAX_WEEKS = 52;

const validDate = (date: string) => {
  const time = Date.parse(`${date}T00:00:00Z`);
  return (
    /^\d{4}-\d{2}-\d{2}$/.test(date) &&
    !Number.isNaN(time) &&
    new Date(time).toISOString().slice(0, 10) === date
  );
};

/** The days the planned sessions cover: the same range the month preview shows. */
export function sessionRange(
  effectiveFrom: string,
  today: string,
  weeks: number,
) {
  const from =
    validDate(effectiveFrom) && effectiveFrom > today ? effectiveFrom : today;
  return { from, to: addDays(from, weeks * 7 - 1) };
}

const asError = (e: unknown) =>
  e instanceof FlowError
    ? e
    : new FlowError(
        "Mất kết nối; chưa chắc bước này đã được lưu. Bấm “Thử lại”, hệ thống sẽ kiểm tra trước để không lưu trùng.",
        { definite: false },
      );
const isAuth = (e: FlowError) => e.status === 401 || e.status === 403;
const slotKey = (s: ServerSlot) =>
  `${s.dayOfWeek}-${hhmm(s.startTime)}-${hhmm(s.endTime)}`;
const sameSlots = (a: ServerSlot[], b: ServerSlot[]) =>
  a.length === b.length &&
  [...a].map(slotKey).sort().join("|") === [...b].map(slotKey).sort().join("|");

export async function runFlow({
  input,
  progress,
  api,
  today,
  update,
}: {
  input: FlowInput;
  progress: FlowProgress;
  api: FlowApi;
  today: string;
  /** Called with the new progress after every step, so a failure leaves the right state behind. */
  update: (progress: FlowProgress) => void;
}): Promise<{ stopped: FlowStop | null }> {
  let p = { ...progress };
  const save = (patch: Partial<FlowProgress>) => {
    p = { ...p, ...patch };
    update(p);
  };
  const stop = (
    step: FlowStep,
    reason: FlowStop["reason"],
    message: string,
    errors?: Record<string, string[]>,
  ) => ({ stopped: { step, reason, message, errors } });
  /** The same decision after any failed request, so each step only says what it does with an unknown outcome. */
  const failed = (step: FlowStep, e: unknown, unknown: () => void) => {
    const error = asError(e);
    if (error.definite && error.field)
      return stop(step, "invalid", error.message, error.errors);
    if (isAuth(error)) return stop(step, "auth", error.message);
    if (!error.definite) unknown();
    return stop(step, "failure", error.message);
  };

  const hasSlots = input.slots.length > 0;
  const hasFee = input.fee.trim() !== "";
  // Checked before anything is written, so a bad date cannot leave a class with a half-saved setup.
  if ((hasSlots || hasFee) && !validDate(input.effectiveFrom))
    return stop("schedule", "invalid", "Nhập ngày áp dụng hợp lệ.");
  if (
    hasSlots &&
    input.generate &&
    !(
      Number.isInteger(input.weeks) &&
      input.weeks >= 1 &&
      input.weeks <= MAX_WEEKS
    )
  )
    return stop("sessions", "invalid", "Chọn số tuần tạo buổi học hợp lệ.");

  if (p.classId === null) {
    const name = input.name.trim();
    const subject = input.subject.trim();
    if (p.baseline === null) {
      try {
        save({ baseline: (await api.listClasses()).map((c) => c.id) });
      } catch (e) {
        return stop("class", "failure", asError(e).message);
      }
    }
    let id: string | null = null;
    if (p.classUnknown) {
      // The class may exist already: look for the one this flow made before creating another.
      let classes;
      try {
        classes = await api.listClasses();
      } catch (e) {
        return stop("class", "failure", asError(e).message);
      }
      const matches = classes.filter(
        (c) =>
          !p.baseline!.includes(c.id) &&
          c.name === name &&
          c.subject === subject,
      );
      if (matches.length > 1)
        return stop(
          "class",
          "ambiguous",
          `Có nhiều lớp “${name}” vừa được tạo. Hãy kiểm tra danh sách lớp rồi thử lại.`,
        );
      if (matches.length === 1) id = matches[0].id;
    }
    if (id === null) {
      try {
        id = (
          await api.createClass({
            name,
            subject,
            description: input.description.trim(),
          })
        ).id;
      } catch (e) {
        return failed("class", e, () => save({ classUnknown: true }));
      }
    }
    save({ classId: id, classUnknown: false });
  }
  const classId = p.classId!;

  if (hasSlots && p.scheduleVersion === null) {
    let version = 0;
    if (p.scheduleUnknown) {
      try {
        const current = await api.getSchedule(classId);
        const mine = input.slots.map((s) => ({
          dayOfWeek: s.dayOfWeek,
          startTime: s.startTime,
          endTime: s.endTime,
        }));
        if (
          current.version > 0 &&
          current.effectiveFrom === input.effectiveFrom &&
          sameSlots(current.slots, mine)
        )
          save({ scheduleVersion: current.version, scheduleUnknown: false });
        else version = current.version;
      } catch (e) {
        return stop("schedule", "failure", asError(e).message);
      }
    }
    if (p.scheduleVersion === null) {
      try {
        const saved = await api.saveSchedule(classId, {
          version,
          effectiveFrom: input.effectiveFrom,
          slots: input.slots.map((s) => ({
            dayOfWeek: s.dayOfWeek,
            startTime: `${s.startTime}:00`,
            endTime: `${s.endTime}:00`,
          })),
        });
        save({ scheduleVersion: saved.version, scheduleUnknown: false });
      } catch (e) {
        return failed("schedule", e, () => save({ scheduleUnknown: true }));
      }
    }
  }

  if (hasFee && !p.feeSaved) {
    let version = 0;
    if (p.feeUnknown) {
      try {
        const plan = await api.getRates(classId);
        if (
          plan.rates.some(
            (r) =>
              r.effectiveFrom === input.effectiveFrom &&
              Number(r.unitPriceVnd) === Number(input.fee),
          )
        )
          save({ feeSaved: true, feeUnknown: false });
        else version = plan.version;
      } catch (e) {
        return stop("fee", "failure", asError(e).message);
      }
    }
    if (!p.feeSaved) {
      try {
        await api.saveRate(classId, {
          effectiveFrom: input.effectiveFrom,
          unitPriceVnd: input.fee.trim(),
          version,
        });
        save({ feeSaved: true, feeUnknown: false });
      } catch (e) {
        return failed("fee", e, () => save({ feeUnknown: true }));
      }
    }
  }

  // Making the sessions is repeatable on the server (a session that already exists is skipped), so a retry just runs it again.
  if (hasSlots && input.generate && !p.sessionsDone) {
    try {
      await api.generate(classId, {
        ...sessionRange(input.effectiveFrom, today, input.weeks),
        scheduleVersion: p.scheduleVersion!,
      });
      save({ sessionsDone: true });
    } catch (e) {
      return failed("sessions", e, () => undefined);
    }
  }
  return { stopped: null };
}
