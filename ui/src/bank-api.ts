// Shapes of the lesson, import, question bank and exam routes (src/Modules/Learning/BankEndpoints.cs).
export type LessonSession = {
  id: string;
  date: string;
  startTime: string;
  status: "planned" | "held" | "cancelled";
};
export type Lesson = {
  id: string;
  no: number;
  topic: string;
  title: string;
  knowledge: string;
  outcomes: string;
  skillsText: string;
  competencies: string;
  skillIds: string[];
  skillCodes: string[];
  session: LessonSession | null;
  manual: boolean;
  taught: boolean;
};
export type LessonPlan = {
  configured: boolean;
  version: number;
  lessons: Lesson[];
  archived: { id: string; no: number; title: string }[];
  taughtSkillIds: string[];
  sessions: LessonSession[];
};

export type ImportIssue = {
  sheet: string;
  row: number;
  column: string;
  message: string;
};
export type ImportReport = {
  committed: boolean;
  curriculumCreated: boolean;
  skills: { added: number; updated: number };
  lessons: { added: number; updated: number };
  questions: { added: number; updated: number };
  issues: ImportIssue[];
  missingImages: string[];
};

export type QuestionLevel =
  "nhan_biet" | "thong_hieu" | "van_dung" | "van_dung_cao";
export const levels: { code: QuestionLevel; label: string; short: string }[] = [
  { code: "nhan_biet", label: "Nhận biết", short: "NB" },
  { code: "thong_hieu", label: "Thông hiểu", short: "TH" },
  { code: "van_dung", label: "Vận dụng", short: "VD" },
  { code: "van_dung_cao", label: "Vận dụng cao", short: "VDC" },
];
export const levelLabel = (code: string) =>
  levels.find((x) => x.code === code)?.label ?? code;
export type QuestionType =
  "single_choice" | "multi_choice" | "fill_blanks" | "essay";
export const questionTypes: Record<QuestionType, string> = {
  single_choice: "Trắc nghiệm",
  multi_choice: "Nhiều đáp án",
  fill_blanks: "Điền ô",
  essay: "Tự luận",
};
/** A fill-in is filled by tapping a button (the original way, blanks written [ ]) or by typing into the text (blanks {{1}}, {{2}}…). */
export type FillMode = "chips" | "type";

export type QuestionOption = {
  key: string;
  text: string;
  image?: string | null;
  imageUrl?: string | null;
};
export type Question = {
  id: string;
  code: string;
  type: QuestionType;
  level: QuestionLevel;
  primarySkillId: string;
  secondarySkillIds: string[];
  lessonNo: number | null;
  stem: string;
  stemImage: string | null;
  stemImageUrl: string | null;
  options: QuestionOption[];
  answer: string[][];
  archived: boolean;
  version: number;
  updatedAt: string;
  blanks: number;
  points: number;
  fillMode: FillMode;
  ignoreDiacritics: boolean;
  sampleAnswer: string | null;
  /** Clean HTML naming images by file (data-file); the screen adds their links. Only a typed fill-in has one. */
  stemHtml: string | null;
  audioFile: string | null;
};
export type BankSkill = {
  id: string;
  code: string;
  name: string;
  strand: string;
};
export type QuestionImage = {
  id: string;
  fileName: string;
  contentType: string;
  byteLength: number;
  url: string;
  updatedAt: string;
};
export type QuestionBank = {
  questions: Question[];
  images: QuestionImage[];
  skills: BankSkill[];
};

export type Blueprint = {
  id: string;
  name: string;
  counts: number[];
  lessonFrom: number | null;
  lessonTo: number | null;
  skillIds: string[];
};
export type ExamStatus = "draft" | "open" | "closed" | "confirmed";
export const examStates: Record<ExamStatus, string> = {
  draft: "Bản nháp",
  open: "Đang mở",
  closed: "Đã đóng",
  confirmed: "Đã xác nhận kết quả",
};
export type AttemptStatus =
  "none" | "in_progress" | "submitted" | "auto_submitted";
export const attemptStates: Record<AttemptStatus, string> = {
  none: "Chưa vào",
  in_progress: "Đang làm",
  submitted: "Đã nộp",
  auto_submitted: "Tự nộp",
};
export type ExamSummary = {
  id: string;
  title: string;
  status: ExamStatus;
  createdAt: string;
  openedAt: string | null;
  closedAt: string | null;
  confirmedAt: string | null;
  assessmentId: string | null;
  durationMinutes: number | null;
  questionCount: number;
  seats: number;
  submitted: number;
};
export type ExamItem = {
  order: number;
  questionId: string;
  code: string;
  type: QuestionType;
  level: QuestionLevel;
  stem: string;
  stemImageUrl: string | null;
  blanks: number;
  options: QuestionOption[];
  answer: string[][];
  primarySkillId: string;
  skillCode: string | null;
};
export type ExamSeat = {
  studentId: string;
  studentName: string;
  code: string;
  attemptStatus: AttemptStatus;
  answered: number;
  correct: number;
  total: number;
  startedAt: string | null;
  submittedAt: string | null;
  deadline: string | null;
  locked: boolean;
  failedTries: number;
  skills:
    | { skillId: string; code: string | null; tasks: number; correct: number }[]
    | null;
};
export type Exam = {
  id: string;
  title: string;
  status: ExamStatus;
  durationMinutes: number | null;
  showResults: boolean;
  publicToken: string;
  assessmentId: string | null;
  counts: number[];
  lessonFrom: number | null;
  lessonTo: number | null;
  skillIds: string[];
  version: number;
  items: ExamItem[];
  skills: { id: string; code: string | null; tasks: number }[];
  seats: ExamSeat[];
};
export type ExamPreview = { questions: Question[]; skills: BankSkill[] };

/** Splits a fill-in stem at its blanks: "8 + 5 = [ ]" → ["8 + 5 = ", ""]; blanks lie between the parts. */
export function stemParts(stem: string): string[] {
  return stem.split(/\[\s*\]/);
}

/** The answer key in words, for the teacher: "B" or "6 | 4" (alternatives joined by "/"). */
export function answerText(question: {
  type: QuestionType;
  answer: string[][];
  options: QuestionOption[];
  sampleAnswer?: string | null;
}): string {
  if (question.type === "single_choice") {
    const key = question.answer[0]?.[0] ?? "";
    const option = question.options.find((o) => o.key === key);
    return option?.text ? `${key}. ${option.text}` : key;
  }
  if (question.type === "multi_choice")
    return (question.answer[0] ?? []).join(", ");
  if (question.type === "essay")
    return question.sampleAnswer || "Giáo viên chấm tay";
  return question.answer.map((x) => x.join(" / ")).join(" | ");
}

/** Points as the teacher writes them: 1 or 0,5. */
export const pointsText = (points: number) => String(points).replace(".", ",");

export function examLink(token: string): string {
  return `${window.location.origin}${window.location.pathname}#/lam-bai/${token}`;
}

/** Share of right answers per skill as a band the teacher reads at a glance. */
export function skillBand(
  correct: number,
  tasks: number,
): "strong" | "weak" | "mid" {
  if (tasks === 0) return "mid";
  const rate = correct / tasks;
  return rate >= 0.8 ? "strong" : rate < 0.5 ? "weak" : "mid";
}
