import { test, expect } from "@playwright/test";
import {
  FlowError,
  emptyProgress,
  runFlow,
  sessionRange,
  type FlowApi,
  type FlowInput,
  type FlowProgress,
} from "../src/class-flow";

// A fake server with the behaviour the real one has that matters here: schedule and rate writes are versioned (a
// stale version is a 409), generating sessions is repeatable, and any call can be told to fail the way a network does.
type Slot = { dayOfWeek: number; startTime: string; endTime: string };
function fake() {
  const classes: { id: string; name: string; subject: string }[] = [];
  const schedules = new Map<
    string,
    { version: number; effectiveFrom: string | null; slots: Slot[] }
  >();
  const rates = new Map<
    string,
    {
      version: number;
      rates: { effectiveFrom: string; unitPriceVnd: string }[];
    }
  >();
  const generated: {
    classId: string;
    from: string;
    to: string;
    scheduleVersion: number;
  }[] = [];
  const calls = {
    listClasses: 0,
    createClass: 0,
    getSchedule: 0,
    saveSchedule: 0,
    getRates: 0,
    saveRate: 0,
    generate: 0,
  };
  type Fail = Error | "lose" | null;
  const plan = {
    listClasses: (): Error | null => null,
    createClass: (_n: number): Fail => null,
    getSchedule: (): Error | null => null,
    saveSchedule: (_n: number): Fail => null,
    saveRate: (_n: number): Fail => null,
    generate: (_n: number): Error | null => null,
  };
  const lost = () => new FlowError("Mất kết nối.", { definite: false });
  let seq = 0;
  const api: FlowApi = {
    async listClasses() {
      calls.listClasses++;
      const failure = plan.listClasses();
      if (failure) throw failure;
      return classes.map((c) => ({ ...c }));
    },
    async createClass(input) {
      calls.createClass++;
      const failure = plan.createClass(calls.createClass);
      if (failure instanceof Error) throw failure;
      const made = {
        id: `c${++seq}`,
        name: input.name,
        subject: input.subject,
      };
      classes.push(made);
      if (failure === "lose") throw lost();
      return { id: made.id };
    },
    async getSchedule(classId) {
      calls.getSchedule++;
      const failure = plan.getSchedule();
      if (failure) throw failure;
      return (
        schedules.get(classId) ?? { version: 0, effectiveFrom: null, slots: [] }
      );
    },
    async saveSchedule(classId, input) {
      calls.saveSchedule++;
      const failure = plan.saveSchedule(calls.saveSchedule);
      if (failure instanceof Error) throw failure;
      const current = schedules.get(classId)?.version ?? 0;
      if (input.version !== current)
        throw new FlowError("Lịch tuần đã thay đổi.", {
          definite: true,
          status: 409,
        });
      schedules.set(classId, {
        version: current + 1,
        effectiveFrom: input.effectiveFrom,
        slots: input.slots,
      });
      if (failure === "lose") throw lost();
      return { version: current + 1 };
    },
    async getRates(classId) {
      calls.getRates++;
      return rates.get(classId) ?? { version: 0, rates: [] };
    },
    async saveRate(classId, input) {
      calls.saveRate++;
      const failure = plan.saveRate(calls.saveRate);
      if (failure instanceof Error) throw failure;
      const plan0 = rates.get(classId) ?? { version: 0, rates: [] };
      if (input.version !== plan0.version)
        throw new FlowError("Đơn giá đã thay đổi.", {
          definite: true,
          status: 409,
        });
      rates.set(classId, {
        version: plan0.version + 1,
        rates: [
          ...plan0.rates,
          {
            effectiveFrom: input.effectiveFrom,
            unitPriceVnd: input.unitPriceVnd,
          },
        ],
      });
      if (failure === "lose") throw lost();
    },
    async generate(classId, input) {
      calls.generate++;
      const failure = plan.generate(calls.generate);
      if (failure) throw failure;
      const current = schedules.get(classId)?.version ?? 0;
      if (input.scheduleVersion !== current)
        throw new FlowError("Lịch tuần đã thay đổi.", {
          definite: true,
          status: 409,
        });
      generated.push({ classId, ...input });
    },
  };
  return { api, classes, schedules, rates, generated, calls, plan };
}

const T2_T4_T6: FlowInput["slots"] = [1, 3, 5].map((d) => ({
  dayOfWeek: d,
  startTime: "18:00",
  endTime: "19:30",
}));
const input = (patch: Partial<FlowInput> = {}): FlowInput => ({
  name: "Toán 9A",
  subject: "Toán",
  description: "",
  slots: T2_T4_T6,
  effectiveFrom: "2026-11-19",
  fee: "150000",
  generate: true,
  weeks: 4,
  ...patch,
});
const TODAY = "2026-11-19";

async function run(
  f: ReturnType<typeof fake>,
  flow: FlowInput,
  progress: FlowProgress = emptyProgress(),
) {
  const state = { progress };
  const result = await runFlow({
    input: flow,
    progress: state.progress,
    api: f.api,
    today: TODAY,
    update: (next) => (state.progress = next),
  });
  return { result, progress: state.progress };
}

test.describe("creating a class in one flow: the engine (fake server)", () => {
  test("class, schedule, fee and sessions are saved in that order with the typed values", async () => {
    const f = fake();
    const { result, progress } = await run(f, input());
    expect(result.stopped).toBeNull();
    expect(f.classes).toEqual([{ id: "c1", name: "Toán 9A", subject: "Toán" }]);
    expect(f.schedules.get("c1")).toEqual({
      version: 1,
      effectiveFrom: "2026-11-19",
      slots: T2_T4_T6.map((s) => ({
        ...s,
        startTime: "18:00:00",
        endTime: "19:30:00",
      })),
    });
    expect(f.rates.get("c1")?.rates).toEqual([
      { effectiveFrom: "2026-11-19", unitPriceVnd: "150000" },
    ]);
    // Four weeks from today: 28 days, ending 2026-12-16.
    expect(f.generated).toEqual([
      {
        classId: "c1",
        from: "2026-11-19",
        to: "2026-12-16",
        scheduleVersion: 1,
      },
    ]);
    expect(progress).toMatchObject({
      classId: "c1",
      scheduleVersion: 1,
      feeSaved: true,
      sessionsDone: true,
    });
  });

  test("the session range is the same one the preview shows: from the later of today and the start date", () => {
    expect(sessionRange("2026-10-01", "2026-11-19", 4)).toEqual({
      from: "2026-11-19",
      to: "2026-12-16",
    });
    expect(sessionRange("2026-12-01", "2026-11-19", 2)).toEqual({
      from: "2026-12-01",
      to: "2026-12-14",
    });
  });

  test("without slots the schedule and the sessions are skipped, and an empty fee skips the rate", async () => {
    const f = fake();
    const { result } = await run(f, input({ slots: [], fee: "" }));
    expect(result.stopped).toBeNull();
    expect(f.classes).toHaveLength(1);
    expect(f.calls).toMatchObject({
      saveSchedule: 0,
      saveRate: 0,
      generate: 0,
    });
  });

  test("a fee without a schedule still saves the rate, from the chosen start date", async () => {
    const f = fake();
    await run(
      f,
      input({ slots: [], fee: "200000", effectiveFrom: "2026-12-01" }),
    );
    expect(f.rates.get("c1")?.rates).toEqual([
      { effectiveFrom: "2026-12-01", unitPriceVnd: "200000" },
    ]);
    expect(f.calls.saveSchedule).toBe(0);
  });

  test("with 'generate' off the schedule is saved but no sessions are made", async () => {
    const f = fake();
    await run(f, input({ generate: false }));
    expect(f.calls.saveSchedule).toBe(1);
    expect(f.calls.generate).toBe(0);
  });

  test("a bad start date or week count stops before anything is created", async () => {
    const f = fake();
    for (const patch of [
      { effectiveFrom: "" },
      { effectiveFrom: "2026-13-40" },
      { weeks: 0 },
      { weeks: 200 },
    ]) {
      const { result } = await run(f, input(patch));
      expect(result.stopped?.reason).toBe("invalid");
    }
    expect(f.calls.listClasses + f.calls.createClass).toBe(0);
  });

  test("a validation error on the class goes back to the form and creates nothing else", async () => {
    const f = fake();
    f.plan.createClass = () =>
      new FlowError("Tên lớp bắt buộc.", {
        definite: true,
        status: 400,
        field: true,
        errors: { name: ["Tên lớp bắt buộc."] },
      });
    const { result, progress } = await run(f, input());
    expect(result.stopped).toMatchObject({
      step: "class",
      reason: "invalid",
      errors: { name: ["Tên lớp bắt buộc."] },
    });
    expect(progress.classId).toBeNull();
    expect(progress.classUnknown).toBe(false);
    expect(f.calls.saveSchedule).toBe(0);
  });

  test("a schedule failure stops the run; the retry saves the schedule without creating a second class", async () => {
    const f = fake();
    f.plan.saveSchedule = (n) =>
      n === 1
        ? new FlowError("Máy chủ lỗi.", { definite: false, status: 500 })
        : null;
    const first = await run(f, input());
    expect(first.result.stopped).toMatchObject({
      step: "schedule",
      reason: "failure",
    });
    expect(first.progress.classId).toBe("c1");
    expect(first.progress.scheduleUnknown).toBe(true);
    expect(f.calls.saveRate + f.calls.generate).toBe(0); // nothing after the failed step ran

    f.plan.saveSchedule = () => null;
    const second = await run(f, input(), first.progress);
    expect(second.result.stopped).toBeNull();
    expect(f.classes).toHaveLength(1);
    expect(f.calls.createClass).toBe(1);
    expect(f.schedules.get("c1")?.version).toBe(1);
    expect(f.generated).toHaveLength(1);
  });

  test("a lost answer after the class was created: the retry adopts it instead of creating a duplicate", async () => {
    const f = fake();
    f.plan.createClass = (n) => (n === 1 ? "lose" : null);
    const first = await run(f, input());
    expect(first.result.stopped).toMatchObject({
      step: "class",
      reason: "failure",
    });
    expect(first.progress.classUnknown).toBe(true);
    expect(f.classes).toHaveLength(1); // the server did create it

    f.plan.createClass = () => null;
    const second = await run(f, input(), first.progress);
    expect(second.result.stopped).toBeNull();
    expect(f.classes).toHaveLength(1);
    expect(second.progress.classId).toBe("c1");
    expect(f.calls.createClass).toBe(1);
    expect(f.schedules.has("c1")).toBe(true);
  });

  test("a lost answer when nothing was created: the retry creates the class", async () => {
    const f = fake();
    f.plan.createClass = (n) =>
      n === 1 ? new FlowError("Mất kết nối.", { definite: false }) : null;
    const first = await run(f, input());
    expect(f.classes).toHaveLength(0);
    f.plan.createClass = () => null;
    const second = await run(f, input(), first.progress);
    expect(second.result.stopped).toBeNull();
    expect(f.classes).toHaveLength(1);
  });

  test("classes that existed before, even with the same name, are never adopted", async () => {
    const f = fake();
    f.classes.push({ id: "old", name: "Toán 9A", subject: "Toán" });
    f.plan.createClass = (n) => (n === 1 ? "lose" : null);
    const first = await run(f, input());
    f.plan.createClass = () => null;
    const second = await run(f, input(), first.progress);
    expect(second.progress.classId).toBe("c1");
    expect(f.schedules.has("old")).toBe(false);
  });

  test("two new classes that match are ambiguous: the run stops and creates nothing", async () => {
    const f = fake();
    f.plan.createClass = (n) => (n === 1 ? "lose" : null);
    const first = await run(f, input());
    f.classes.push({ id: "other", name: "Toán 9A", subject: "Toán" }); // made meanwhile, e.g. in another tab
    const second = await run(f, input(), first.progress);
    expect(second.result.stopped?.reason).toBe("ambiguous");
    expect(f.calls.createClass).toBe(1);
  });

  test("a lost answer on the schedule: the retry sees it is saved and does not write again, so there is no 409", async () => {
    const f = fake();
    f.plan.saveSchedule = (n) => (n === 1 ? "lose" : null);
    const first = await run(f, input());
    expect(first.result.stopped).toMatchObject({ step: "schedule" });
    expect(f.schedules.get("c1")?.version).toBe(1);

    f.plan.saveSchedule = () => null;
    const second = await run(f, input(), first.progress);
    expect(second.result.stopped).toBeNull();
    expect(f.calls.saveSchedule).toBe(1);
    expect(f.generated[0].scheduleVersion).toBe(1);
  });

  test("a schedule that is different on the server is overwritten with the version read, not a stale one", async () => {
    const f = fake();
    f.plan.saveSchedule = (n) =>
      n === 1
        ? new FlowError("Máy chủ lỗi.", { definite: false, status: 500 })
        : null;
    const first = await run(f, input());
    // Someone saved another schedule for this class in the meantime.
    f.schedules.set("c1", {
      version: 1,
      effectiveFrom: "2026-11-01",
      slots: [{ dayOfWeek: 2, startTime: "08:00:00", endTime: "09:00:00" }],
    });
    f.plan.saveSchedule = () => null;
    const second = await run(f, input(), first.progress);
    expect(second.result.stopped).toBeNull();
    expect(f.schedules.get("c1")?.version).toBe(2);
    expect(f.generated[0].scheduleVersion).toBe(2);
  });

  test("a lost answer on the fee: the retry finds the rate and does not post it twice", async () => {
    const f = fake();
    f.plan.saveRate = (n) => (n === 1 ? "lose" : null);
    const first = await run(f, input());
    expect(first.result.stopped).toMatchObject({ step: "fee" });
    f.plan.saveRate = () => null;
    const second = await run(f, input(), first.progress);
    expect(second.result.stopped).toBeNull();
    expect(f.calls.saveRate).toBe(1);
    expect(f.rates.get("c1")?.rates).toHaveLength(1);
    expect(f.generated).toHaveLength(1);
  });

  test("a failure while making sessions is repeated alone; nothing before it is redone", async () => {
    const f = fake();
    f.plan.generate = (n) =>
      n === 1
        ? new FlowError("Máy chủ lỗi.", { definite: false, status: 500 })
        : null;
    const first = await run(f, input());
    expect(first.result.stopped).toMatchObject({
      step: "sessions",
      reason: "failure",
    });
    f.plan.generate = () => null;
    const second = await run(f, input(), first.progress);
    expect(second.result.stopped).toBeNull();
    expect(f.calls).toMatchObject({
      createClass: 1,
      saveSchedule: 1,
      saveRate: 1,
      generate: 2,
    });
  });

  test("an expired session pauses the run at the failing step and a later run carries on", async () => {
    const f = fake();
    f.plan.saveRate = (n) =>
      n === 1
        ? new FlowError("Phiên đã hết hạn.", { definite: true, status: 401 })
        : null;
    const first = await run(f, input());
    expect(first.result.stopped).toMatchObject({ step: "fee", reason: "auth" });
    expect(first.progress.feeUnknown).toBe(false);
    f.plan.saveRate = () => null;
    const second = await run(f, input(), first.progress);
    expect(second.result.stopped).toBeNull();
    expect(f.calls.createClass).toBe(1);
    expect(f.rates.get("c1")?.rates).toHaveLength(1);
  });

  test("when the class list cannot be read before the first write, nothing is created", async () => {
    const f = fake();
    f.plan.listClasses = () =>
      new FlowError("Mất kết nối.", { definite: false });
    const { result } = await run(f, input());
    expect(result.stopped).toMatchObject({ step: "class", reason: "failure" });
    expect(f.calls.createClass).toBe(0);
  });
});
