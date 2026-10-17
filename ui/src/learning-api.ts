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
export type Submission = {
  studentId: string;
  studentName: string;
  method: string;
  status: AssessmentState;
  completion: number | null;
  correctness: number | null;
  version: number;
  finalResult: ResultRevision | null;
  history: ResultRevision[];
};
export type AssessmentSummary = {
  id: string;
  title: string;
  date: string;
  defaultMethod: string;
};
export type Assessment = AssessmentSummary & { submissions: Submission[] };
export function resultText(
  result: Pick<ResultRevision, "status" | "completion" | "correctness">,
) {
  return result.status === "unassigned" || result.status === "unassessed"
    ? `${assessmentStates[result.status]} · Không tính vào thống kê`
    : `Hoàn thành ${result.completion}% · Làm đúng ${result.correctness}%`;
}
