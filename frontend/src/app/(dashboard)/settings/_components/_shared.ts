// Sprint 11A — shared constants extracted from the former monolithic
// settings/page.tsx. Imported by the main settings shell (page.tsx) and by
// any per-tab component that needs them.
//
// YOLO-S3 (Settings Hub): the TABS array was reorganized from 6 flat tabs
// (clinic/users/roles/finance/email/ai) into 8 logical category groups.
// Each tab may render an existing per-tab component AND a grid of quick-link
// cards pointing at the related /settings/* sub-routes. The component-vs-link
// split is owned by page.tsx; this file only owns the metadata (labels,
// icons, and the link cards themselves). No existing tab component was
// deleted — they are now grouped differently on the landing page.

import {
  Settings,
  ShieldCheck,
  Stethoscope,
  Wallet,
  FlaskConical,
  Mail,
  FileText,
  Cpu,
  Globe,
  DoorOpen,
  FileSearch,
  Database,
  Package,
  CreditCard,
  Languages,
  Building2,
  Wrench,
  DollarSign,
  MessageCircle,
  Activity,
  type LucideIcon,
} from "lucide-react";

/**
 * The 8 logical category groups shown on the settings landing page.
 * Order matters — it matches the owner-approved spec (YOLO-S3).
 */
export type Tab =
  | "clinic"
  | "permissions"
  | "services"
  | "finance"
  | "lab"
  | "communication"
  | "templates"
  | "ortho-ceph";

/** Tailwind color tokens used by QuickLink tiles. */
export type QuickLinkColor =
  | "blue"
  | "purple"
  | "emerald"
  | "amber"
  | "sky"
  | "violet"
  | "rose"
  | "indigo"
  | "teal"
  | "slate";

export interface QuickLink {
  href: string;
  label: string;
  description: string;
  icon: LucideIcon;
  color: QuickLinkColor;
  /**
   * When true, the card is rendered as a non-clickable "قريبًا" placeholder
   * for a feature that does not yet have its own route. The href is kept
   * (often "#") so the data shape stays uniform, but the renderer wraps it
   * in a div instead of a Link.
   */
  comingSoon?: boolean;
}

export interface TabDef {
  key: Tab;
  label: string;
  icon: LucideIcon;
  /** Quick-link cards shown inside the tab (below the main component, if any). */
  links?: QuickLink[];
}

export const TABS: TabDef[] = [
  {
    key: "clinic",
    label: "بيانات المركز",
    icon: Settings,
    links: [
      {
        href: "/settings/website",
        label: "إعدادات الموقع",
        description: "تحكم بمحتوى الصفحة الرئيسية والعنوان والتواصل",
        icon: Globe,
        color: "blue",
      },
      {
        href: "/settings/rooms",
        label: "غرف العيادة",
        description: "إدارة الغرف وتوزيعها",
        icon: DoorOpen,
        color: "amber",
      },
    ],
  },
  {
    key: "permissions",
    label: "الصلاحيات",
    icon: ShieldCheck,
    links: [
      {
        href: "/settings/audit",
        label: "سجل التدقيق",
        description: "عرض كل العمليات المنفذة في النظام",
        icon: FileSearch,
        color: "purple",
      },
      {
        href: "/settings/backup",
        label: "النسخ الاحتياطي",
        description: "تنزيل نسخة احتياطية من قاعدة البيانات",
        icon: Database,
        color: "slate",
      },
    ],
  },
  {
    key: "services",
    label: "الخدمات",
    icon: Stethoscope,
    links: [
      {
        href: "/settings/services",
        label: "خدمات العيادة",
        description: "إدارة كتالوج الخدمات والأسعار",
        icon: Stethoscope,
        color: "emerald",
      },
      {
        href: "/settings/packages",
        label: "باقات العلاج",
        description: "كتالوج الباقات (تبييض، تقويم) لاستخدامها في المواعيد",
        icon: Package,
        color: "violet",
      },
    ],
  },
  {
    key: "finance",
    label: "المالية",
    icon: Wallet,
    links: [
      {
        href: "/settings/payment-methods",
        label: "طرق الدفع",
        description: "إدارة طرق الدفع المتاحة والرقم المرجعي",
        icon: CreditCard,
        color: "purple",
      },
    ],
  },
  {
    key: "lab",
    label: "المختبر",
    icon: FlaskConical,
    links: [
      {
        href: "/settings/labs",
        label: "المختبرات",
        description: "إدارة بيانات المختبرات وجهات الاتصال",
        icon: Building2,
        color: "blue",
      },
      {
        href: "/settings/lab-work-types",
        label: "أنواع أعمال المختبر",
        description: "كتالوج أنواع التراكيب المتاحة",
        icon: Wrench,
        color: "emerald",
      },
      {
        href: "/settings/lab-pricing",
        label: "تسعير أعمال المختبر",
        description: "أسعار أنواع التراكيب لكل مختبر",
        icon: DollarSign,
        color: "amber",
      },
    ],
  },
  {
    key: "communication",
    label: "التواصل",
    icon: Mail,
    links: [
      {
        href: "/settings/language",
        label: "لغة الوحدات",
        description: "التحكم بلغة كل وحدة (عربي / إنجليزي)",
        icon: Languages,
        color: "sky",
      },
    ],
  },
  {
    key: "templates",
    label: "القوالب",
    icon: FileText,
    links: [
      {
        href: "#",
        label: "قوالب الوصفات الطبية",
        description: "قوالب جاهزة للوصفات المتكررة لتسريع إصدارها",
        icon: FileText,
        color: "indigo",
        comingSoon: true,
      },
      {
        href: "#",
        label: "قوالب الرسائل",
        description: "قوالب WhatsApp و SMS لإعادة الاستدعاء والتذكير",
        icon: MessageCircle,
        color: "rose",
        comingSoon: true,
      },
    ],
  },
  {
    key: "ortho-ceph",
    label: "التقويم والسيفالو",
    icon: Cpu,
    links: [
      {
        href: "#",
        label: "المعايير السيفالومترية",
        description: "إدارة المعايير المرجعية المستخدمة في التحليل (قيد التطوير)",
        icon: Activity,
        color: "teal",
        comingSoon: true,
      },
    ],
  },
];

/** Map from QuickLinkColor → Tailwind classes for the icon tile + hover state. */
export const QUICK_LINK_COLOR_CLASSES: Record<
  QuickLinkColor,
  { tile: string; hoverTile: string }
> = {
  blue: { tile: "bg-blue-50 text-blue-600", hoverTile: "group-hover:bg-blue-100" },
  purple: { tile: "bg-purple-50 text-purple-600", hoverTile: "group-hover:bg-purple-100" },
  emerald: { tile: "bg-emerald-50 text-emerald-600", hoverTile: "group-hover:bg-emerald-100" },
  amber: { tile: "bg-amber-50 text-amber-600", hoverTile: "group-hover:bg-amber-100" },
  sky: { tile: "bg-sky-50 text-sky-600", hoverTile: "group-hover:bg-sky-100" },
  violet: { tile: "bg-violet-50 text-violet-600", hoverTile: "group-hover:bg-violet-100" },
  rose: { tile: "bg-rose-50 text-rose-600", hoverTile: "group-hover:bg-rose-100" },
  indigo: { tile: "bg-indigo-50 text-indigo-600", hoverTile: "group-hover:bg-indigo-100" },
  teal: { tile: "bg-teal-50 text-teal-600", hoverTile: "group-hover:bg-teal-100" },
  slate: { tile: "bg-slate-50 text-slate-600", hoverTile: "group-hover:bg-slate-100" },
};

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
