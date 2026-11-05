export type Skill = { id: string; title: string; taught: boolean };
export type Profile = {
  textbook: string;
  semester: string;
  skills: Skill[];
  version: number;
};
export type Observation = {
  id: string;
  date: string;
  level: "red" | "amber" | "green";
  note: string | null;
  recordedAt: string;
};
export type Evidence = {
  id: string;
  date: string;
  title: string;
  total: number;
  correct: number;
  confirmed: boolean;
};
export type ProgressRow = {
  studentId: string;
  studentName: string;
  skillId: string;
  skillTitle: string;
  taught: boolean;
  latestObservation: Observation | null;
  observations: Observation[];
  evidence: Evidence[];
  mastery: "insufficient" | "achieved" | "needs_practice" | "not_taught";
  pendingReview: boolean;
};
export type Progress = {
  date: string;
  students: { id: string; name: string }[];
  rows: ProgressRow[];
};
export type Plan = {
  id: string;
  title: string;
  date: string;
  minutes: number;
  content: string;
  status: "draft" | "approved";
  version: number;
  historyJson: string;
  snapshotJson: string;
  prioritiesJson: string;
  activitiesJson: string;
};
