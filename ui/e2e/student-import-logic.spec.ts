import { test, expect } from "@playwright/test";
import {
  ImportError,
  runImport,
  type ImportApi,
  type ImportRow,
} from "../src/student-import";

// A fake server: it keeps the profiles and enrollments the real one would, and can be told to fail in the ways a
// real network does (answer lost after the work was done, 5xx, validation errors, expired session).
type Profile = { id: string; name: string; phone: string | null };
function fake(options: { existing?: Profile[]; enrolledIds?: string[] } = {}) {
  const profiles: Profile[] = [...(options.existing ?? [])];
  // Profiles that exist before an import starts; the engine never adopts these.
  const baseline = new Set(profiles.map((p) => p.id));
  const enrolled = new Set<string>(options.enrolledIds ?? []);
  const calls = { create: 0, enroll: 0, list: 0, enrolledIds: 0 };
  const plan = {
    // Called before the fake does the work; return a failure to throw, or "lose" to do the work then throw a network error.
    create: (
      _row: { name: string; phone: string },
      _n: number,
    ): Error | "lose" | null => null,
    enroll: (_id: string, _n: number): Error | "lose" | null => null,
    list: (): Error | null => null,
  };
  let seq = 0;
  const api: ImportApi = {
    async create(row) {
      calls.create++;
      const failure = plan.create(row, calls.create);
      if (failure instanceof Error) throw failure;
      const profile = {
        id: `s${++seq}`,
        name: row.name,
        phone: row.phone || null,
      };
      profiles.push(profile);
      if (failure === "lose")
        throw new ImportError("Mất kết nối.", { definite: false });
      return { id: profile.id };
    },
    async enroll(studentId) {
      calls.enroll++;
      const failure = plan.enroll(studentId, calls.enroll);
      if (failure instanceof Error) throw failure;
      if (enrolled.has(studentId))
        throw new ImportError("Học sinh đã có đăng ký.", {
          definite: true,
          status: 409,
        });
      enrolled.add(studentId);
      if (failure === "lose")
        throw new ImportError("Mất kết nối.", { definite: false });
    },
    async listStudents() {
      calls.list++;
      const failure = plan.list();
      if (failure) throw failure;
      return profiles.map((p) => ({ ...p }));
    },
    async enrolledIds() {
      calls.enrolledIds++;
      return [...enrolled];
    },
  };
  return { api, profiles, enrolled, calls, plan, baseline };
}

const rowsOf = (...names: string[]): ImportRow[] =>
  names.map((name, i) => ({
    id: i + 1,
    name,
    phone: "",
    state: { phase: "todo" },
  }));
const phases = (rows: ImportRow[]) => rows.map((r) => r.state.phase);

async function run(
  rows: ImportRow[],
  f: ReturnType<typeof fake>,
  startDate = "2026-11-19",
) {
  const update = (id: number, patch: Partial<ImportRow>) => {
    const row = rows.find((r) => r.id === id)!;
    Object.assign(row, patch);
  };
  return runImport({
    rows,
    startDate,
    api: f.api,
    baseline: f.baseline,
    update,
  });
}

test.describe("importing pasted students: the retry engine (fake server)", () => {
  test("every row is created and enrolled, in order", async () => {
    const f = fake();
    const rows = rowsOf("An", "Bình", "Chi");
    const result = await run(rows, f);
    expect(result.stopped).toBeNull();
    expect(phases(rows)).toEqual(["done", "done", "done"]);
    expect(f.profiles.map((p) => p.name)).toEqual(["An", "Bình", "Chi"]);
    expect(f.enrolled.size).toBe(3);
  });

  test("a bad start date stops before anything is created", async () => {
    const f = fake();
    const rows = rowsOf("An", "Bình");
    for (const date of ["", "2026-13-45", "19/11/2026"]) {
      const result = await run(rows, f, date);
      expect(result.stopped?.reason).toBe("failure");
    }
    expect(f.calls.create).toBe(0);
    expect(phases(rows)).toEqual(["todo", "todo"]);
  });

  test("a validation error on one line rejects that line and carries on with the rest", async () => {
    const f = fake();
    f.plan.create = (row) =>
      row.name === "Bình"
        ? new ImportError("Số điện thoại không hợp lệ.", {
            definite: true,
            status: 400,
            field: true,
          })
        : null;
    const rows = rowsOf("An", "Bình", "Chi");
    const result = await run(rows, f);
    expect(result.stopped).toBeNull();
    expect(phases(rows)).toEqual(["done", "rejected", "done"]);
    expect(rows[1].state).toEqual({
      phase: "rejected",
      message: "Số điện thoại không hợp lệ.",
    });
    expect(f.profiles.map((p) => p.name)).toEqual(["An", "Chi"]);
  });

  test("a failed enrollment stops the run, and the retry enrolls only: no second profile", async () => {
    const f = fake();
    f.plan.enroll = (_id, n) =>
      n === 2
        ? new ImportError("Máy chủ lỗi.", { definite: false, status: 500 })
        : null;
    const rows = rowsOf("An", "Bình", "Chi");
    const first = await run(rows, f);
    expect(first.stopped?.reason).toBe("failure");
    expect(first.stopped?.rowId).toBe(2);
    expect(phases(rows)).toEqual(["done", "unknown-enroll", "todo"]);
    expect(f.profiles).toHaveLength(2); // Chi was never touched

    f.plan.enroll = () => null;
    const second = await run(rows, f);
    expect(second.stopped).toBeNull();
    expect(phases(rows)).toEqual(["done", "done", "done"]);
    expect(f.profiles.map((p) => p.name)).toEqual(["An", "Bình", "Chi"]); // each profile exactly once
    expect(f.calls.create).toBe(3);
  });

  test("a definite enrollment rejection keeps the profile and stops, so the retry does not create again", async () => {
    const f = fake();
    f.plan.enroll = (_id, n) =>
      n === 1
        ? new ImportError("Ngày bắt đầu không hợp lệ.", {
            definite: true,
            status: 400,
          })
        : null;
    const rows = rowsOf("An");
    const first = await run(rows, f);
    expect(first.stopped?.reason).toBe("failure");
    expect(rows[0].state).toMatchObject({ phase: "created", studentId: "s1" });
    f.plan.enroll = () => null;
    await run(rows, f);
    expect(phases(rows)).toEqual(["done"]);
    expect(f.calls.create).toBe(1);
  });

  test("a lost answer after the profile was created: the retry adopts it instead of creating a duplicate", async () => {
    const f = fake();
    f.plan.create = (_row, n) => (n === 2 ? "lose" : null);
    const rows = rowsOf("An", "Bình", "Chi");
    const first = await run(rows, f);
    expect(first.stopped?.reason).toBe("failure");
    expect(phases(rows)).toEqual(["done", "unknown-create", "todo"]);
    expect(f.profiles.map((p) => p.name)).toEqual(["An", "Bình"]); // the server did create Bình

    f.plan.create = () => null;
    const second = await run(rows, f);
    expect(second.stopped).toBeNull();
    expect(phases(rows)).toEqual(["done", "done", "done"]);
    expect(f.profiles.map((p) => p.name)).toEqual(["An", "Bình", "Chi"]); // Bình exactly once
    expect(f.enrolled.size).toBe(3);
  });

  test("a lost answer when nothing was created: the retry creates it", async () => {
    const f = fake();
    f.plan.create = (_row, n) =>
      n === 1 ? new ImportError("Mất kết nối.", { definite: false }) : null;
    const rows = rowsOf("An");
    await run(rows, f);
    expect(rows[0].state.phase).toBe("unknown-create");
    expect(f.profiles).toHaveLength(0);
    f.plan.create = () => null;
    await run(rows, f);
    expect(phases(rows)).toEqual(["done"]);
    expect(f.profiles).toHaveLength(1);
  });

  test("adoption ignores profiles that existed before the run and profiles another row already owns", async () => {
    const f = fake({ existing: [{ id: "old", name: "An", phone: null }] });
    f.plan.create = (_row, n) => (n === 1 ? "lose" : null);
    const rows = rowsOf("An");
    await run(rows, f);
    expect(rows[0].state.phase).toBe("unknown-create");
    f.plan.create = () => null;
    await run(rows, f);
    // The run's own "An" (created before the answer was lost) is adopted, not the old one.
    expect(rows[0].state).toMatchObject({ phase: "done", studentId: "s1" });
    expect(f.profiles.map((p) => p.id)).toEqual(["old", "s1"]);
    expect(f.enrolled.has("old")).toBe(false);
  });

  test("two unclaimed matches are ambiguous: the run stops and creates nothing", async () => {
    const f = fake();
    f.plan.create = () => "lose";
    const rows = rowsOf("An");
    await run(rows, f);
    await run(rows, f); // second attempt: finds the first one, adopts it
    expect(rows[0].state.phase).toBe("done");

    const g = fake();
    g.plan.create = (_row, n) => (n <= 2 ? "lose" : null);
    const rows2 = rowsOf("An");
    await run(rows2, g); // create #1 lost
    rows2[0].state = { phase: "unknown-create" };
    g.profiles.push({ id: "dup", name: "An", phone: null }); // someone else made a second one meanwhile
    const result = await run(rows2, g);
    expect(result.stopped?.reason).toBe("ambiguous");
    expect(rows2[0].state.phase).toBe("unknown-create");
    expect(g.calls.create).toBe(1);
  });

  test("a lost answer on enrollment: the retry sees the student is already enrolled and does not post again", async () => {
    const f = fake();
    f.plan.enroll = (_id, n) => (n === 1 ? "lose" : null);
    const rows = rowsOf("An");
    await run(rows, f);
    expect(rows[0].state).toMatchObject({
      phase: "unknown-enroll",
      studentId: "s1",
    });
    expect(f.enrolled.has("s1")).toBe(true);
    const enrollsBefore = f.calls.enroll;
    f.plan.enroll = () => null;
    const second = await run(rows, f);
    expect(second.stopped).toBeNull();
    expect(phases(rows)).toEqual(["done"]);
    expect(f.calls.enroll).toBe(enrollsBefore); // checked the class instead of posting
    expect(f.calls.enrolledIds).toBe(1);
  });

  test("an expired session pauses the run without marking the rest as failed", async () => {
    const f = fake();
    f.plan.create = (_row, n) =>
      n === 2
        ? new ImportError("Phiên đã hết hạn.", { definite: true, status: 401 })
        : null;
    const rows = rowsOf("An", "Bình", "Chi");
    const result = await run(rows, f);
    expect(result.stopped?.reason).toBe("auth");
    expect(phases(rows)).toEqual(["done", "todo", "todo"]);
    f.plan.create = () => null;
    await run(rows, f);
    expect(phases(rows)).toEqual(["done", "done", "done"]);
  });

  test("when the profile list cannot be read to check an unknown create, nothing is created", async () => {
    const f = fake();
    f.plan.create = (_row, n) => (n === 1 ? "lose" : null);
    const rows = rowsOf("An");
    await run(rows, f);
    f.plan.list = () => new ImportError("Mất kết nối.", { definite: false });
    const result = await run(rows, f);
    expect(result.stopped?.reason).toBe("failure");
    expect(rows[0].state.phase).toBe("unknown-create");
    expect(f.calls.create).toBe(1);
  });

  test("rows that are already done or rejected are left alone", async () => {
    const f = fake();
    const rows = rowsOf("An", "Bình", "Chi");
    rows[0].state = { phase: "done", studentId: "x" };
    rows[1].state = {
      phase: "rejected",
      message: "Số điện thoại không hợp lệ.",
    };
    await run(rows, f);
    expect(f.profiles.map((p) => p.name)).toEqual(["Chi"]);
    expect(rows[1].state.phase).toBe("rejected");
  });
});
