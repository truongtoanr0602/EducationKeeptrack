import { api } from "./api";
import { formatDate } from "./format-date";

export type WeeklySlot = {
  dayOfWeek: number;
  startTime: string;
  endTime: string;
};
export type WeeklySchedule = {
  version: number;
  effectiveFrom: string | null;
  slots: WeeklySlot[];
};
export type SessionStatus = "planned" | "held" | "cancelled";
export const sessionStatusNames: Record<SessionStatus, string> = {
  planned: "Dự kiến",
  held: "Đã diễn ra",
  cancelled: "Đã hủy",
};
export const weekDays = [
  "Chủ nhật",
  "Thứ Hai",
  "Thứ Ba",
  "Thứ Tư",
  "Thứ Năm",
  "Thứ Sáu",
  "Thứ Bảy",
];
export type ClassSession = {
  id: string;
  date: string;
  startTime: string;
  endTime: string;
  startsAtUtc: string;
  endsAtUtc: string;
  timeZone: string;
  status: SessionStatus;
  kind: "regular" | "makeup";
  scheduleVersion: number | null;
  version: number;
  replacesSessionId: string | null;
  reason: string | null;
};
export type AttendanceStatus = "unmarked" | "present" | "absent";
export type Attendance = {
  studentId: string;
  name: string;
  status: AttendanceStatus;
  reason: string | null;
  excused: boolean;
  version: number;
  sessionVersion: number;
};
export type AttendanceMark = {
  studentId: string;
  status: AttendanceStatus;
  reason: string | null;
  excused: boolean;
  version: number;
};
// Saves every changed mark of one session at once; a stale row rejects the whole batch.
export const saveAttendance = (
  classId: string,
  sessionId: string,
  marks: AttendanceMark[],
) =>
  api<Attendance[]>(
    `/classes/${classId}/sessions/${sessionId}/attendance`,
    "PUT",
    { marks },
  );
export type MissingAttendance = {
  sessionId: string;
  date: string;
  startTime: string;
  studentId: string;
  name: string;
};
export type DateRange = { from: string; to: string };
export const sessionLabel = (session: ClassSession) =>
  `${formatDate(session.date)} · ${session.startTime.slice(0, 5)}–${session.endTime.slice(0, 5)} · ${sessionStatusNames[session.status]}`;
