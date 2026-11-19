import { api, ApiError, type TeacherClass } from "./api";

// First-run setup progress is derived from the teacher's real data, never from a stored flag, so finishing
// a task on another device updates it. Only two conveniences live in localStorage, per account: the wizard
// step to come back to, and whether the "Hoàn tất thiết lập" card was hidden.

export type SetupProgress = {
  classes: TeacherClass[];
  recipient: boolean;
  students: boolean;
  /** Every class has at least one unit price. */
  fee: boolean;
  /** The first class still without a unit price, where "Đặt học phí" should lead. */
  feeClassId: string | null;
};

export const setupTotal = 3;
export const setupDone = (p: SetupProgress) =>
  Number(p.recipient) + Number(p.students) + Number(p.fee);

export async function loadSetupProgress(): Promise<SetupProgress> {
  const [classes, students, recipient] = await Promise.all([
    api<TeacherClass[]>("/classes"),
    api<unknown[]>("/students"),
    api<unknown>("/billing/recipient").then(
      () => true,
      (e) => {
        if (e instanceof ApiError && e.status === 404) return false;
        throw e;
      },
    ),
  ]);
  const plans = await Promise.all(
    classes.map((c) =>
      api<{ rates: unknown[] }>(`/billing/classes/${c.id}/rates`),
    ),
  );
  const unpriced = classes.find((_, i) => plans[i].rates.length === 0);
  return {
    classes,
    recipient,
    students: students.length > 0,
    fee: classes.length > 0 && !unpriced,
    feeClassId: unpriced?.id ?? null,
  };
}

type Store = Pick<Storage, "getItem" | "setItem" | "removeItem">;
const store = (): Store | null => {
  try {
    return window.localStorage;
  } catch {
    return null;
  }
};
const stepKey = (email: string) => `ekt.setup.step.${email.toLowerCase()}`;
const hiddenKey = (email: string) => `ekt.setup.card.${email.toLowerCase()}`;

function read(key: string) {
  try {
    return store()?.getItem(key) ?? null;
  } catch {
    return null;
  }
}
function write(key: string, value: string | null) {
  try {
    if (value === null) store()?.removeItem(key);
    else store()?.setItem(key, value);
  } catch {
    /* storage can be blocked; the wizard then simply starts from derived data */
  }
}

/** The wizard step left unfinished (1-3), or null when the wizard was never started or was finished. */
export function savedStep(email: string): 1 | 2 | 3 | null {
  const value = read(stepKey(email));
  return value === "1" || value === "2" || value === "3"
    ? (Number(value) as 1 | 2 | 3)
    : null;
}
export const rememberStep = (email: string, step: 1 | 2 | 3 | null) =>
  write(stepKey(email), step === null ? null : String(step));
export const cardHidden = (email: string) => read(hiddenKey(email)) === "1";
export const hideCard = (email: string, hidden: boolean) =>
  write(hiddenKey(email), hidden ? "1" : null);

/** Where the wizard should open: never before the first step that data still needs. */
export function firstStep(
  saved: 1 | 2 | 3 | null,
  p: Pick<SetupProgress, "classes">,
): 1 | 2 | 3 {
  const floor = p.classes.length > 0 ? 2 : 1;
  return Math.max(saved ?? 1, floor) as 1 | 2 | 3;
}
