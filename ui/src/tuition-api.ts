export type RatePlan = {
  version: number;
  rates: {
    id: string;
    effectiveFrom: string;
    unitPriceVnd: string;
    version: number;
  }[];
};
export type TuitionLine = {
  sessionId: string;
  date: string;
  startTime: string;
  kind: string;
  sessionStatus: string;
  attendanceStatus: string;
  unitPriceVnd: string | null;
  amountVnd: string | null;
  rateId: string | null;
  sourceVersion: number;
};
export type Adjustment = {
  id: string;
  studentId: string;
  month: string;
  direction: "increase" | "decrease";
  amountVnd: string;
  reason: string;
  recordedBy: string;
  recordedAt: string;
  version: number;
};
export type StudentTuition = {
  studentId: string;
  name: string;
  totalVnd: string;
  lines: TuitionLine[];
  adjustments: Adjustment[];
};
export type TuitionReport = {
  month: string;
  version: number;
  rateVersion: number;
  status: "ready" | "pending" | "missing-rate";
  totalVnd: string;
  students: StudentTuition[];
};
const formatter = new Intl.NumberFormat("vi-VN", {
  style: "currency",
  currency: "VND",
  maximumFractionDigits: 0,
});
export const money = (value: string | null) =>
  value === null ? "Chưa có đơn giá" : formatter.format(BigInt(value));
export function feeBasis(line: TuitionLine) {
  if (line.sessionStatus === "cancelled") return "Buổi hủy · Không tính phí";
  if (line.sessionStatus === "planned") return "Dự kiến · Không tính phí";
  if (line.attendanceStatus === "unmarked")
    return "Chưa điểm danh · Không tính phí";
  if (line.attendanceStatus === "absent") return "Vắng · Không tính phí";
  return line.kind === "makeup" ? "Có mặt · Học bù" : "Có mặt";
}
