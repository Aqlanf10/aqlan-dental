// Sprint 11A — shared constants extracted from the former monolithic
// settings/page.tsx. Imported by the main settings shell (page.tsx) and by
// any per-tab component that needs them. Behavior unchanged.

import {
  Settings,
  Users,
  Shield,
  Wallet,
  Mail,
  Sparkles,
  type LucideIcon,
} from "lucide-react";

export type Tab = "clinic" | "users" | "roles" | "email" | "ai" | "finance";

export const TABS: { key: Tab; label: string; icon: LucideIcon }[] = [
  { key: "clinic",  label: "بيانات المركز",      icon: Settings },
  { key: "users",   label: "المستخدمون",        icon: Users },
  { key: "roles",   label: "الأدوار",           icon: Shield },
  { key: "finance", label: "المالية",            icon: Wallet },
  { key: "email",   label: "البريد",             icon: Mail },
  { key: "ai",      label: "الذكاء الاصطناعي",   icon: Sparkles },
];

export const ROLE_LABELS: Record<string, string> = {
  Admin: "مدير النظام",
  Orthodontist: "أخصائي تقويم",
  GeneralDentist: "طبيب أسنان",
  OralSurgeon: "جراح وجه وفكين",
  Reception: "استقبال",
  Accountant: "محاسب",
  Assistant: "مساعد",
  BranchManager: "مدير فرع",
};

export const ALL_ROLES = [
  "Admin",
  "Orthodontist",
  "GeneralDentist",
  "OralSurgeon",
  "Reception",
  "Accountant",
  "Assistant",
  "BranchManager",
] as const;

export const inputCls =
  "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue";
