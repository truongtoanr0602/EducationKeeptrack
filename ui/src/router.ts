import { useEffect, useState } from "react";

// Screens are addressed by the URL fragment (#/classes/<id>/<section>[/<tab>]) so the app keeps
// working behind the same-origin API without server-side routes. Per-screen state (month, selected
// assessment, report scope…) stays in the query string, as before.
export type Section =
  "students" | "sessions" | "tuition" | "learning" | "reports";
// "learning" (Học tập) holds three areas, each with its own tabs (ADR 0005, 22/11/2026): the assessments, the program
// and the personalization. The old section names are still accepted by classHref and by old links.
export type LearningArea = "assessments" | "program" | "personalization";
// "program" holds the curriculum, lessons, question bank, exams and the competency profile: none of it is AI, so it is
// always shown. "personalization" holds materials and review plans and is hidden without AI access (ADR 0005).
export type ProgramTab =
  "curriculum" | "lessons" | "questions" | "exams" | "competency";
export type PersonalTab = "materials" | "plans";
export type ClassTab = ProgramTab | PersonalTab;
export type Route =
  // Cross-class top-level screens (ADR 0005): today's sessions, tuition owed, monthly reports.
  | { name: "today" }
  | { name: "billing" }
  | { name: "reports" }
  | { name: "student"; studentId: string }
  | { name: "classes" }
  | {
      name: "class";
      classId: string;
      section: Section;
      /** Only for section "learning": which area. */
      area?: LearningArea;
      tab?: ClassTab;
    }
  // Teacher: service status, payment reconciliation, AI quota, login email.
  | { name: "account" }
  // First-run setup (UX8-5): 1 = class, 2 = receiving account, 3 = students.
  | { name: "setup"; step: 1 | 2 | 3 }
  // Admin: service access only (never classes, students or tuition).
  | { name: "admin-teachers"; teacherId?: string }
  | { name: "admin-settings" };

export const sections: { id: Section; label: string; icon: string }[] = [
  { id: "students", label: "Học sinh", icon: "◎" },
  { id: "sessions", label: "Lịch", icon: "▦" },
  { id: "tuition", label: "Học phí", icon: "₫" },
  { id: "learning", label: "Học tập", icon: "☷" },
  { id: "reports", label: "Báo cáo", icon: "▤" },
];
export const learningAreas: { id: LearningArea; label: string }[] = [
  { id: "assessments", label: "Đánh giá" },
  { id: "program", label: "Chương trình & kiểm tra" },
  { id: "personalization", label: "Cá nhân hoá" },
];
export const programTabs: { id: ProgramTab; label: string }[] = [
  { id: "exams", label: "Đề kiểm tra" },
  { id: "questions", label: "Ngân hàng câu hỏi" },
  { id: "lessons", label: "Buổi chương trình" },
  { id: "curriculum", label: "Chương trình lớp" },
  { id: "competency", label: "Hồ sơ năng lực" },
];
export const personalTabs: { id: PersonalTab; label: string }[] = [
  { id: "materials", label: "Học liệu" },
  { id: "plans", label: "Kế hoạch ôn tập" },
];

const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function parse(hash: string): Route {
  const raw = hash.replace(/^#/, "");
  // Links from before screen navigation used "#<classId>".
  if (guid.test(raw))
    return { name: "class", classId: raw, section: "students" };
  const [first, classId, section, tab, inner] = raw
    .replace(/^\//, "")
    .split("/");
  if (first === "account") return { name: "account" };
  if (first === "setup")
    return {
      name: "setup",
      step: classId === "3" ? 3 : classId === "2" ? 2 : 1,
    };
  if (first === "students" && classId && guid.test(classId))
    return { name: "student", studentId: classId };
  if (first === "billing") return { name: "billing" };
  if (first === "reports") return { name: "reports" };
  if (first === "admin" && classId === "settings")
    return { name: "admin-settings" };
  if (first === "admin")
    return section && guid.test(section)
      ? { name: "admin-teachers", teacherId: section }
      : { name: "admin-teachers" };
  if (first === "classes" && !classId) return { name: "classes" };
  if (first !== "classes" || !classId || !guid.test(classId))
    return { name: "today" };
  const learning = (area: LearningArea, tabId?: string): Route => {
    // Links from before the split (#/classes/<id>/personalization/curriculum…) open the same tab in the program area.
    const moved = programTabs.find((x) => x.id === tabId)?.id;
    if (area === "personalization" && moved)
      return {
        name: "class",
        classId,
        section: "learning",
        area: "program",
        tab: moved,
      };
    return {
      name: "class",
      classId,
      section: "learning",
      area,
      tab:
        area === "personalization"
          ? (personalTabs.find((x) => x.id === tabId)?.id ?? "materials")
          : area === "program"
            ? moved
            : undefined,
    };
  };
  // Old section names (#/classes/<id>/assessments, /program/<tab>, /personalization/<tab>) move into "learning".
  const legacy = learningAreas.find((x) => x.id === section)?.id;
  if (legacy) return learning(legacy, tab);
  if (section === "learning")
    return learning(
      learningAreas.find((x) => x.id === tab)?.id ?? "assessments",
      inner,
    );
  const known = sections.find((x) => x.id === section)?.id ?? "students";
  return { name: "class", classId, section: known };
}

export function href(route: Route): string {
  if (route.name === "student") return `#/students/${route.studentId}`;
  if (route.name === "today") return "#/today";
  if (route.name === "billing") return "#/billing";
  if (route.name === "reports") return "#/reports";
  if (route.name === "classes") return "#/classes";
  if (route.name === "account") return "#/account";
  if (route.name === "setup") return `#/setup/${route.step}`;
  if (route.name === "admin-settings") return "#/admin/settings";
  if (route.name === "admin-teachers")
    return route.teacherId
      ? `#/admin/teachers/${route.teacherId}`
      : "#/admin/teachers";
  if (route.section === "learning")
    return `#/classes/${route.classId}/learning/${route.area ?? "assessments"}${route.tab ? `/${route.tab}` : ""}`;
  return `#/classes/${route.classId}/${route.section}`;
}

export function classHref(
  classId: string,
  section: Section | LearningArea,
  tab?: ClassTab,
) {
  const area = learningAreas.find((x) => x.id === section)?.id;
  if (area)
    return href({ name: "class", classId, section: "learning", area, tab });
  return href({ name: "class", classId, section: section as Section });
}

export function navigate(route: Route) {
  window.location.hash = href(route);
}

export function useRoute(): Route {
  const [route, setRoute] = useState(() => parse(window.location.hash));
  useEffect(() => {
    const change = () => setRoute(parse(window.location.hash));
    window.addEventListener("hashchange", change);
    // Normalize legacy or empty fragments without adding a history entry.
    const current = parse(window.location.hash);
    if (window.location.hash !== href(current)) {
      const url = new URL(window.location.href);
      url.hash = href(current);
      window.history.replaceState(null, "", url);
    }
    return () => window.removeEventListener("hashchange", change);
  }, []);
  return route;
}
