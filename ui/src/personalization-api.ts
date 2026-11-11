import { ApiError } from "./api";

export const learningPath = (classId: string) => `/learning/classes/${classId}`;

export type MasteryRule = {
  threshold: number;
  minTasks: number;
  requiredAttempts: number;
  version: number;
};
export type Skill = {
  id: string;
  code: string;
  name: string;
  strand: string;
  taught: boolean;
  taughtOn: string | null;
  archived: boolean;
};
export type Curriculum = {
  configured: boolean;
  version: number;
  subject: string;
  grade: string;
  term: string;
  textbook: string;
  rule: MasteryRule;
  skills: Skill[];
};
export type Material = {
  id: string;
  skillId: string;
  title: string;
  content: string;
  minutes: number;
  difficulty: "basic" | "standard" | "advanced";
  status: "draft" | "approved";
  version: number;
  updatedAt: string;
};
export const difficulties: Record<Material["difficulty"], string> = {
  basic: "Cơ bản",
  standard: "Vừa",
  advanced: "Nâng cao",
};

export type SkillResultStatus = "unassessed" | "assessed" | "not_done" | "unassigned";
export const skillResultStates: Record<SkillResultStatus, string> = {
  unassessed: "Chưa đánh giá",
  assessed: "Đã đánh giá",
  not_done: "Không làm bài",
  unassigned: "Không giao",
};
export type SkillRevision = {
  version: number;
  status: SkillResultStatus;
  taskCount: number;
  correct: number | null;
  note: string | null;
  action: "confirmed" | "edited";
  recordedAt: string;
};
export type SkillResultRow = {
  studentId: string;
  studentName: string;
  skillId: string;
  status: SkillResultStatus;
  correct: number | null;
  note: string | null;
  version: number;
  finalResult: SkillRevision | null;
  history: SkillRevision[];
};
export type AssessmentSkills = {
  assessmentId: string;
  title: string;
  date: string;
  version: number;
  skills: { skillId: string; code: string; name: string; taskCount: number; locked: boolean }[];
  results: SkillResultRow[];
};

export type CompetencyState = "mastered" | "not_mastered" | "insufficient" | "not_taught";
export const competencyStates: Record<CompetencyState, string> = {
  mastered: "Đạt",
  not_mastered: "Chưa đạt",
  insufficient: "Chưa đủ dữ liệu",
  not_taught: "Chưa dạy",
};
export type Evidence = {
  assessmentId: string;
  title: string;
  date: string;
  status: string;
  taskCount: number;
  correct: number | null;
  counted: boolean;
  meetsThreshold: boolean;
  excluded: string | null;
};
export type SkillCompetency = {
  skillId: string;
  code: string;
  name: string;
  strand: string;
  state: CompetencyState;
  missingAttempts: number;
  evidence: Evidence[];
};
export type CompetencyProfile = {
  configured: boolean;
  rule: MasteryRule | null;
  curriculumVersion: number;
  students: { studentId: string; name: string; skills: SkillCompetency[] }[];
};

export type PlanSession = { date: string; minutes: number; sessionId: string | null };
export type PlanGoal = { skillId: string; reason: string; recheck: string };
export type PlanStudent = { studentId: string; studentName?: string | null; goals: PlanGoal[] };
export type PlanActivity = {
  session: number;
  studentIds: string[];
  skillId: string | null;
  materialId: string | null;
  minutes: number;
  description: string;
};
export type PlanContent = {
  sessions: PlanSession[];
  students: PlanStudent[];
  activities: PlanActivity[];
  note: string | null;
};
export type ExecutionStatus = "done" | "partial" | "not_done" | "absent";
export const executionStates: Record<ExecutionStatus, string> = {
  done: "Hoàn thành",
  partial: "Một phần",
  not_done: "Chưa làm",
  absent: "Vắng",
};
export type ExecutionEntry = {
  activity: number;
  studentId: string;
  status: ExecutionStatus;
  actualMinutes: number | null;
  note: string | null;
};
export type PlanSummary = {
  id: string;
  weekStart: string;
  status: "draft" | "approved";
  version: number;
  approvedVersion: number | null;
  updatedAt: string;
};
export type ReviewPlan = PlanSummary & {
  content: PlanContent;
  approvedContent: PlanContent | null;
  evidence: { studentId: string; skillId: string; state: CompetencyState; ruleVersion: number; evidence: Evidence[] }[];
  execution: ExecutionEntry[];
  revisions: { version: number; action: string; status: string; recordedAt: string }[];
};
export type ClassSessionSource = {
  id: string;
  date: string;
  startTime: string;
  endTime: string;
  status: string;
  kind: string;
};
export type PlanProgress = {
  planId: string;
  weekStart: string;
  goals: {
    studentId: string;
    studentName: string;
    skillId: string;
    skillName: string;
    before: Evidence[];
    after: Evidence[];
    currentState: CompetencyState;
  }[];
};

export function errorText(e: unknown, fallback: string) {
  if (!(e instanceof ApiError)) return fallback;
  return Object.values(e.errors ?? {}).flat().join(" ") || e.message;
}

export function mondayOf(date: string) {
  const [y, m, d] = date.split("-").map(Number);
  const value = new Date(Date.UTC(y, m - 1, d));
  const day = (value.getUTCDay() + 6) % 7;
  value.setUTCDate(value.getUTCDate() - day);
  return value.toISOString().slice(0, 10);
}
export function addDays(date: string, days: number) {
  const [y, m, d] = date.split("-").map(Number);
  const value = new Date(Date.UTC(y, m - 1, d + days));
  return value.toISOString().slice(0, 10);
}
export function viDate(date: string) {
  const [y, m, d] = date.split("-");
  return `${d}/${m}/${y}`;
}
export const weekdays = ["Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy", "Chủ nhật"];
export function weekdayOf(date: string) {
  const [y, m, d] = date.split("-").map(Number);
  return weekdays[(new Date(Date.UTC(y, m - 1, d)).getUTCDay() + 6) % 7];
}
