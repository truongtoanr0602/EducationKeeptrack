export type StudentDraft = {
  name: string;
  parentName: string;
  phone: string;
  email: string;
  notes: string;
};
export type Student = { id: string; version: number } & {
  [K in keyof StudentDraft]: K extends "name" ? string : string | null;
};
export type EnrollmentStatus = "active" | "leave" | "stopped";
export const statusNames: Record<EnrollmentStatus, string> = {
  active: "Đang học",
  leave: "Nghỉ có phép",
  stopped: "Dừng học",
};
export type Enrollment = {
  id: string;
  studentId: string;
  student: Student;
  startDate: string;
  endDate: string | null;
  status: EnrollmentStatus;
  version: number;
  history: {
    status: EnrollmentStatus;
    effectiveDate: string;
    recordedAt: string;
    version: number;
  }[];
};
export const today = () =>
  new Intl.DateTimeFormat("en-CA", {
    timeZone: "Asia/Ho_Chi_Minh",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(new Date());
