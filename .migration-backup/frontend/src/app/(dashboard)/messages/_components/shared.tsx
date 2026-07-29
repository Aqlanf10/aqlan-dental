import {
  Eye,
  EyeOff,
  Shield,
  Stethoscope,
  Building2,
  ShieldCheck,
} from "lucide-react";
import type { ConversationFilter, ConversationType } from "@/types/messaging";
import api from "@/lib/api";

// ─── Attachment constants ─────────────────────────────────────────────────────

export const MAX_FILE_SIZE = 10 * 1024 * 1024; // 10MB
export const ALLOWED_EXTENSIONS = [".jpg", ".jpeg", ".png", ".pdf", ".webm", ".ogg", ".mp4", ".m4a", ".mp3", ".wav"];
export const ALLOWED_MIME_TYPES = ["image/jpeg", "image/png", "application/pdf", "audio/webm", "audio/ogg", "audio/mp4", "audio/mpeg", "audio/wav"];

/** Convert a full upload URL to a relative /uploads/ path for the backend */
export function toRelativeUploadUrl(url: string): string {
  try {
    const u = new URL(url);
    return u.pathname; // e.g. /uploads/guid.ext
  } catch {
    // Already a relative path
    return url;
  }
}

/** Convert a relative /uploads/ path to a URL for display (img src, links).
 *  NAV-CEPH-FIX (Part 2): keep relative — the Next.js rewrite proxies /uploads/* same-origin
 *  so the aqlan_access_token cookie travels and the backend /uploads auth middleware (SEC-03)
 *  accepts the request. */
export function toFullUploadUrl(url: string): string {
  if (!url) return "";
  if (url.startsWith("http://") || url.startsWith("https://")) return url;
  return url;
}

// ─── مستخدمو النظام (للمحادثة الجديدة) ───────────────────────────────────────

export interface SystemUser {
  id: string;
  username: string;
  role: string;
  doctorName?: string;
  doctorColor?: string;
  doctorInitials?: string;
}

export async function fetchUsers(): Promise<SystemUser[]> {
  const { data } = await api.get("/api/users/contacts");
  return Array.isArray(data) ? data : [];
}

// ─── تنسيق الوقت ──────────────────────────────────────────────────────────────

export function formatTime(dateStr: string) {
  const d = new Date(dateStr);
  const now = new Date();
  const diff = now.getTime() - d.getTime();
  const mins = Math.floor(diff / 60000);
  const hours = Math.floor(mins / 60);
  const days = Math.floor(hours / 24);

  if (mins < 1) return "الآن";
  if (mins < 60) return `منذ ${mins} د`;
  if (hours < 24)
    return d.toLocaleTimeString("ar-SA", { hour: "2-digit", minute: "2-digit" });
  if (days < 7) return `منذ ${days} ي`;
  return d.toLocaleDateString("ar-SA", { month: "short", day: "numeric" });
}

export function formatFullTime(dateStr: string) {
  const d = new Date(dateStr);
  return d.toLocaleString("ar-SA", {
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

// ─── مكونات الفلتر ────────────────────────────────────────────────────────────

export const FILTER_OPTIONS: { value: ConversationFilter; label: string }[] = [
  { value: "all", label: "الكُل" },
  { value: "unread", label: "غير مقروءة" },
  { value: "PatientFacing", label: "مرئية للمريض" },
  { value: "StaffToPatient", label: "داخلية" },
  { value: "StaffToStaff", label: "موظفين" },
];

export const ROLE_LABELS: Record<string, string> = {
  Admin: "مدير",
  Orthodontist: "تقويم",
  GeneralDentist: "أسنان عام",
  OralSurgeon: "جراح",
  Reception: "استقبال",
  Accountant: "محاسب",
  Assistant: "مساعد",
  BranchManager: "مدير فرع",
  Doctor: "طبيب",
  Nurse: "ممرض",
};

// ─── مساعد تفاصيل الخطأ ──────────────────────────────────────────────────────

export function getErrorDetail(error: unknown): {
  title: string;
  description: string;
} {
  const err = error as { response?: { status?: number } } | null;
  const status = err?.response?.status;
  if (status === 403) {
    return {
      title: "غير مصرّح بالوصول",
      description: "ليس لديك صلاحية للوصول إلى هذا المحتوى",
    };
  }
  if (status === 404) {
    return {
      title: "المحتوى غير موجود",
      description: "المحادثة المطلوبة غير موجودة أو تم حذفها",
    };
  }
  return {
    title: "خطأ",
    description: "حدث خطأ أثناء تحميل المحادثات",
  };
}

// ─── شارات نوع المحادثة والمستلم ──────────────────────────────────────────────

export function ConversationTypeBadge({ type }: { type: ConversationType }) {
  switch (type) {
    case "PatientFacing":
      return (
        <span className="flex-shrink-0 inline-flex items-center gap-1 text-[9px] font-bold px-1.5 py-0.5 rounded-full bg-emerald-50 text-emerald-700 border border-emerald-200 leading-none">
          <Eye className="w-2.5 h-2.5" />
          مرئية للمريض
        </span>
      );
    case "StaffToPatient":
      return (
        <span className="flex-shrink-0 inline-flex items-center gap-1 text-[9px] font-bold px-1.5 py-0.5 rounded-full bg-amber-50 text-amber-700 border border-amber-200 leading-none">
          <EyeOff className="w-2.5 h-2.5" />
          داخلية
        </span>
      );
    case "StaffToStaff":
      return (
        <span className="flex-shrink-0 inline-flex items-center gap-1 text-[9px] font-bold px-1.5 py-0.5 rounded-full bg-blue-50 text-blue-700 border border-blue-200 leading-none">
          <Shield className="w-2.5 h-2.5" />
          طاقم
        </span>
      );
    default:
      return null;
  }
}

export function RecipientTypeBadge({ recipientType }: { recipientType?: string | null }) {
  if (!recipientType) return null;
  switch (recipientType) {
    case "TreatingDoctor":
      return (
        <span className="flex-shrink-0 inline-flex items-center gap-1 text-[9px] font-bold px-1.5 py-0.5 rounded-full bg-emerald-50 text-emerald-600 border border-emerald-200 leading-none">
          <Stethoscope className="w-2.5 h-2.5" />
          موجهة إلى الطبيب
        </span>
      );
    case "Reception":
      return (
        <span className="flex-shrink-0 inline-flex items-center gap-1 text-[9px] font-bold px-1.5 py-0.5 rounded-full bg-amber-50 text-amber-600 border border-amber-200 leading-none">
          <Building2 className="w-2.5 h-2.5" />
          موجهة إلى الاستقبال
        </span>
      );
    case "Admin":
      return (
        <span className="flex-shrink-0 inline-flex items-center gap-1 text-[9px] font-bold px-1.5 py-0.5 rounded-full bg-purple-50 text-purple-600 border border-purple-200 leading-none">
          <ShieldCheck className="w-2.5 h-2.5" />
          موجهة إلى الإدارة
        </span>
      );
    default:
      return null;
  }
}
