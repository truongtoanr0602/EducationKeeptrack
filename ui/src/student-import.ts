import { digits, foldName } from "./student-paste";

// The engine that saves a pasted list: for each row create the profile, then enroll it in the class, one row at a
// time, using the API that already exists. It never creates the same profile twice: when an answer is lost it looks
// before it repeats, and it stops at the first problem that could leave an unknown state behind.

/** A failed request, told apart by whether the server certainly did not do the work. */
export class ImportError extends Error {
  definite: boolean;
  status?: number;
  /** A validation message about this one row (HTTP 400 with a field error). */
  field: boolean;
  constructor(
    message: string,
    options: { definite: boolean; status?: number; field?: boolean },
  ) {
    super(message);
    this.definite = options.definite;
    this.status = options.status;
    this.field = options.field ?? false;
  }
}

export type RowState =
  | { phase: "todo" }
  /** The profile exists; only the enrollment is left. */
  | { phase: "created"; studentId: string }
  /** The answer to "create" was lost, so the profile may or may not exist. */
  | { phase: "unknown-create" }
  /** The answer to "enroll" was lost, so the student may or may not be enrolled. */
  | { phase: "unknown-enroll"; studentId: string }
  | { phase: "done"; studentId: string }
  /** The server refused this row's data; edit the line and save again. */
  | { phase: "rejected"; message: string };

export type ImportRow = {
  /** Stable for the whole import, never the line number (the text may be edited between runs). */
  id: number;
  name: string;
  phone: string;
  state: RowState;
};

export type ImportApi = {
  create(row: { name: string; phone: string }): Promise<{ id: string }>;
  enroll(studentId: string, startDate: string): Promise<void>;
  listStudents(): Promise<{ id: string; name: string; phone: string | null }[]>;
  /** Ids of the students already enrolled in this class. */
  enrolledIds(): Promise<string[]>;
};

export type Stop = {
  reason: "auth" | "failure" | "ambiguous";
  message: string;
  rowId: number | null;
};

const validDate = (date: string) => {
  const time = Date.parse(`${date}T00:00:00Z`);
  return (
    /^\d{4}-\d{2}-\d{2}$/.test(date) &&
    !Number.isNaN(time) &&
    new Date(time).toISOString().slice(0, 10) === date
  );
};
const asError = (e: unknown) =>
  e instanceof ImportError
    ? e
    : new ImportError(
        "Mất kết nối; chưa chắc học sinh này đã được lưu. Bấm “Thử lại”, hệ thống sẽ kiểm tra trước để không tạo trùng.",
        { definite: false },
      );
const isAuth = (e: ImportError) => e.status === 401 || e.status === 403;

export async function runImport({
  rows,
  startDate,
  api,
  baseline,
  update,
}: {
  rows: ImportRow[];
  startDate: string;
  api: ImportApi;
  /** Ids of every profile that existed before the import began; they are never adopted. */
  baseline: Set<string>;
  update: (id: number, patch: Partial<ImportRow>) => void;
}): Promise<{ stopped: Stop | null }> {
  // A bad date would create a profile on every row and then fail to enroll each one.
  if (!validDate(startDate))
    return {
      stopped: {
        reason: "failure",
        message: "Nhập ngày bắt đầu hợp lệ.",
        rowId: null,
      },
    };
  const set = (row: ImportRow, state: RowState) => {
    row.state = state;
    update(row.id, { state });
  };
  const stop = (
    reason: Stop["reason"],
    row: ImportRow,
    message: string,
  ): { stopped: Stop } => ({ stopped: { reason, message, rowId: row.id } });

  for (const row of rows) {
    const phase = row.state.phase;
    if (phase === "done" || phase === "rejected") continue;
    let studentId: string | null =
      "studentId" in row.state ? row.state.studentId : null;

    if (studentId === null) {
      if (phase === "unknown-create") {
        // The profile may exist already: look for it before creating another.
        let profiles;
        try {
          profiles = await api.listStudents();
        } catch (e) {
          return stop("failure", row, asError(e).message);
        }
        const claimed = new Set(
          rows.flatMap((r) =>
            "studentId" in r.state ? [r.state.studentId] : [],
          ),
        );
        const matches = profiles.filter(
          (p) =>
            !baseline.has(p.id) &&
            !claimed.has(p.id) &&
            foldName(p.name) === foldName(row.name) &&
            digits(p.phone ?? "") === digits(row.phone),
        );
        if (matches.length > 1)
          return stop(
            "ambiguous",
            row,
            `Có nhiều hồ sơ “${row.name}” vừa được tạo. Hãy kiểm tra danh sách học sinh rồi thử lại.`,
          );
        if (matches.length === 1) studentId = matches[0].id;
      }
      if (studentId === null) {
        try {
          studentId = (await api.create({ name: row.name, phone: row.phone }))
            .id;
        } catch (e) {
          const error = asError(e);
          if (error.definite && error.field) {
            set(row, { phase: "rejected", message: error.message });
            continue;
          }
          if (isAuth(error)) return stop("auth", row, error.message);
          if (error.definite) return stop("failure", row, error.message);
          set(row, { phase: "unknown-create" });
          return stop("failure", row, error.message);
        }
      }
      set(row, { phase: "created", studentId });
    }

    try {
      // The answer to a previous enrollment was lost: the student may already be in the class.
      if (
        phase === "unknown-enroll" &&
        (await api.enrolledIds()).includes(studentId)
      ) {
        set(row, { phase: "done", studentId });
        continue;
      }
      await api.enroll(studentId, startDate);
      set(row, { phase: "done", studentId });
    } catch (e) {
      const error = asError(e);
      if (isAuth(error)) return stop("auth", row, error.message);
      if (!error.definite) set(row, { phase: "unknown-enroll", studentId });
      return stop("failure", row, error.message);
    }
  }
  return { stopped: null };
}
