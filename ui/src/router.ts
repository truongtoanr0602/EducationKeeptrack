import { useEffect, useState } from "react";

// Screens are addressed by the URL fragment (#/classes/<id>/<section>[/<tab>]) so the app keeps
// working behind the same-origin API without server-side routes. Per-screen state (month, selected
// assessment, report scope…) stays in the query string, as before.
export type Section =
  | "students"
  | "sessions"
  | "tuition"
  | "assessments"
  | "personalization"
  | "reports";
export type PersonalTab = "curriculum" | "materials" | "competency" | "plans";
export type Route =
  | { name: "classes" }
  | { name: "class"; classId: string; section: Section; tab?: PersonalTab }
  // Teacher: service status, payment reconciliation, AI quota, login email.
  | { name: "account" }
  // Admin: service access only (never classes, students or tuition).
  | { name: "admin-teachers"; teacherId?: string }
  | { name: "admin-settings" };

export const sections: { id: Section; label: string; icon: string }[] = [
  { id: "students", label: "Học sinh", icon: "◎" },
  { id: "sessions", label: "Lịch & điểm danh", icon: "▦" },
  { id: "tuition", label: "Học phí & thu tiền", icon: "₫" },
  { id: "assessments", label: "Đánh giá", icon: "✎" },
  { id: "personalization", label: "Cá nhân hoá", icon: "✦" },
  { id: "reports", label: "Báo cáo tháng", icon: "▤" },
];
export const personalTabs: { id: PersonalTab; label: string }[] = [
  { id: "curriculum", label: "Chương trình lớp" },
  { id: "materials", label: "Học liệu" },
  { id: "competency", label: "Hồ sơ năng lực" },
  { id: "plans", label: "Kế hoạch ôn tập" },
];

const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function parse(hash: string): Route {
  const raw = hash.replace(/^#/, "");
  // Links from before screen navigation used "#<classId>".
  if (guid.test(raw))
    return { name: "class", classId: raw, section: "students" };
  const [first, classId, section, tab] = raw.replace(/^\//, "").split("/");
  if (first === "account") return { name: "account" };
  if (first === "admin" && classId === "settings")
    return { name: "admin-settings" };
  if (first === "admin")
    return section && guid.test(section)
      ? { name: "admin-teachers", teacherId: section }
      : { name: "admin-teachers" };
  if (first !== "classes" || !classId || !guid.test(classId))
    return { name: "classes" };
  const known = sections.find((x) => x.id === section)?.id ?? "students";
  return {
    name: "class",
    classId,
    section: known,
    tab:
      known === "personalization"
        ? (personalTabs.find((x) => x.id === tab)?.id ?? "curriculum")
        : undefined,
  };
}

export function href(route: Route): string {
  if (route.name === "classes") return "#/classes";
  if (route.name === "account") return "#/account";
  if (route.name === "admin-settings") return "#/admin/settings";
  if (route.name === "admin-teachers")
    return route.teacherId
      ? `#/admin/teachers/${route.teacherId}`
      : "#/admin/teachers";
  return `#/classes/${route.classId}/${route.section}${route.tab ? `/${route.tab}` : ""}`;
}

export function classHref(
  classId: string,
  section: Section,
  tab?: PersonalTab,
) {
  return href({ name: "class", classId, section, tab });
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
