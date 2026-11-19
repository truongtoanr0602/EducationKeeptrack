export type AssessmentState =
  "unassessed" | "unassigned" | "assessed" | "not_done" | "no_correct";
export const assessmentStates: Record<AssessmentState, string> = {
  unassessed: "Chưa đánh giá",
  unassigned: "Không giao bài",
  assessed: "Đã đánh giá",
  not_done: "Không làm bài",
  no_correct: "Không có câu đúng",
};
export type ResultRevision = {
  id: string;
  version: number;
  status: AssessmentState;
  completion: number | null;
  correctness: number | null;
  method: string;
  action: "confirmed" | "edited" | "method_changed";
  recordedAt: string;
  includedInStatistics: boolean;
};
export type PhotoSummary = {
  id: string;
  sequenceIndex: number;
  contentType: string;
  byteLength: number;
  downloadUrl?: string;
  createdAt: string;
};
export type Submission = {
  studentId: string;
  studentName: string;
  method: string;
  status: AssessmentState;
  completion: number | null;
  correctness: number | null;
  version: number;
  aiCompletion?: number | null;
  aiCorrectness?: number | null;
  aiConfidence?: number | null;
  aiReasoning?: string | null;
  aiWarning?: string | null;
  aiStatus?: string | null;
  aiProposedAt?: string | null;
  photos?: PhotoSummary[];
  finalResult: ResultRevision | null;
  history: ResultRevision[];
};
export type AssessmentSummary = {
  id: string;
  title: string;
  date: string;
  defaultMethod: string;
  rubric?: string;
  rubricVersion?: number;
};
export type Assessment = AssessmentSummary & { submissions: Submission[] };
export function resultText(
  result: Pick<ResultRevision, "status" | "completion" | "correctness">,
) {
  return result.status === "unassigned" || result.status === "unassessed"
    ? `${assessmentStates[result.status]} · Không tính vào thống kê`
    : `Hoàn thành ${result.completion}% · Làm đúng ${result.correctness}%`;
}

export type ProgressPoint = {
  assessmentId: string;
  title: string;
  date: string;
  method: string;
  status: AssessmentState;
  completion: number | null;
  correctness: number | null;
  plotted: boolean;
  confirmed: boolean;
  reason: string;
};
export type StudentProgress = {
  studentId: string;
  studentName: string;
  from: string;
  to: string;
  enoughForTrend: boolean;
  completeness: {
    total: number;
    plotted: number;
    notAssigned: number;
    unconfirmed: number;
    unassessed: number;
  };
  points: ProgressPoint[];
};
