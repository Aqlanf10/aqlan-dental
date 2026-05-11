/**
 * Shared constants for public-facing pages (homepage, doctors, services, booking).
 * Keeps service lists, doctor fallbacks, and design tokens consistent.
 */
import {
  Smile,
  Plus,
  Sparkles,
  Zap,
  Award,
  Scissors,
  ClipboardList,
  Stethoscope,
  type LucideIcon,
} from "lucide-react";

// ─── Public-safe doctor shape (returned by GET /api/public/doctors) ────────────
export interface PublicDoctor {
  id: string;
  name: string;
  specialty: string;
  color: string;
  avatarInitials: string;
}

// ─── Service card shape (static – no backend table yet) ────────────────────────
export interface ServiceItem {
  icon: LucideIcon;
  title: string;
  desc: string;
}

/**
 * Unified service list used on homepage, /home/services, and booking dropdown.
 * The `title` value is the same string sent as `serviceType` in booking requests.
 */
export const SERVICES: ServiceItem[] = [
  {
    icon: Smile,
    title: "تقويم الأسنان",
    desc: "تقويم معدني وشفاف وخطط علاجية مخصصة للبالغين والأطفال.",
  },
  {
    icon: Plus,
    title: "زراعة الأسنان",
    desc: "تعويض الأسنان المفقودة بزراعات تساعد على استعادة الوظيفة والمظهر.",
  },
  {
    icon: Sparkles,
    title: "تجميل الأسنان",
    desc: "قشور تجميلية، تبييض، وتحسين شكل الابتسامة بخطة مناسبة لكل حالة.",
  },
  {
    icon: Stethoscope,
    title: "طب الأسنان العام",
    desc: "فحوصات دورية، حشوات، وعلاجات أساسية للحفاظ على صحة الفم والأسنان.",
  },
  {
    icon: Zap,
    title: "علاج الجذور",
    desc: "علاج القنوات الجذرية بدقة للحفاظ على الأسنان وتقليل الألم.",
  },
  {
    icon: Award,
    title: "تركيبات الأسنان",
    desc: "تيجان وجسور زيركون وبورسلان بتصميم وظيفي وجمالي.",
  },
  {
    icon: Scissors,
    title: "جراحة الوجه والفكين",
    desc: "خلع ضرس العقل وبعض إجراءات جراحة الفم واللثة حسب الحالة.",
  },
  {
    icon: ClipboardList,
    title: "تنظيف الأسنان",
    desc: "تنظيف مهني عميق وإزالة الجير والتصبغات للحفاظ على صحة اللثة.",
  },
];

/** Flat list of service titles (for booking dropdown) */
export const SERVICE_TITLES = SERVICES.map((s) => s.title);

// ─── Website settings fallback defaults ────────────────────────────────────────
export const FALLBACK_SETTINGS: Record<string, string> = {
  clinicName: "مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان",
  heroTitle: "ابتسامة تجمع بين دقة العلم ولمسة الفن",
  heroSubtitle:
    "مركز الدكتور عقلان الكامل يقدم رعاية متكاملة في تقويم وزراعة وتجميل الأسنان، مع تشخيص دقيق وخطط علاج واضحة ومتابعة مستمرة لكل حالة.",
  marketingSlogan: "قيادة طبية… وابتسامة بثقة",
  aboutText:
    "يقدم مركز الدكتور عقلان الكامل خدمات تخصصية شاملة في تقويم وزراعة وتجميل الأسنان، معتمدين على تشخيص دقيق، وخطط علاج واضحة، ومتابعة مستمرة للحالات للمساعدة في الوصول إلى نتائج علاجية دقيقة ومناسبة لكل حالة.",
  phone: "04-253028",
  whatsapp: "967770245745",
  address: "تعز، اليمن — شارع التحرير الأعلى",
  workingHours: "السبت – الخميس: 8 ص – 8 م",
  facebook: "",
  instagram: "",
  servicesSectionTitle: "حلول طبية متكاملة لابتسامة صحية وواثقة",
  bookingButtonText: "احجز موعدك الآن",
  whatsappButtonText: "تواصل عبر الواتساب",
};

// ─── Design tokens used across public pages ────────────────────────────────────
export const DESIGN = {
  primary: "#0284c7",
  primaryLight: "#87CEEB",
  accent: "#FF8C00",
  dark: "#0F172A",
  muted: "#94a3b8",
  surface: "#F8FAFC",
} as const;
