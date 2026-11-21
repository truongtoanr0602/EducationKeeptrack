import {
  BookOpen,
  CalendarCheck,
  CalendarDays,
  ClipboardCheck,
  FileText,
  GraduationCap,
  Settings,
  Sparkles,
  Users,
  Wallet,
  type LucideIcon,
} from "lucide-react";

// Navigation icons by tab or section id (they used to be text glyphs).
const icons: Record<string, LucideIcon> = {
  today: CalendarDays,
  classes: GraduationCap,
  billing: Wallet,
  reports: FileText,
  account: Settings,
  students: Users,
  sessions: CalendarCheck,
  tuition: Wallet,
  assessments: ClipboardCheck,
  program: BookOpen,
  personalization: Sparkles,
};

export function NavIcon({ name }: { name: string }) {
  const Icon = icons[name] ?? FileText;
  return (
    <Icon className="nav-icon" size={18} strokeWidth={2} aria-hidden="true" />
  );
}
