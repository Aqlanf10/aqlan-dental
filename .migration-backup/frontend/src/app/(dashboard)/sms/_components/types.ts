import {
  Send,
  CheckCircle2,
  Clock,
  XCircle,
  LayoutDashboard,
  FileText,
  MessageSquare,
  Settings,
} from "lucide-react";

// ─── Types ────────────────────────────────────────────────────────────────────

export interface SmsMessageDto {
  id: string;
  patientId: string;
  patientName: string;
  phoneNumber: string;
  templateType: string;
  messageContent: string;
  status: string;
  externalId?: string;
  errorMessage?: string;
  retryCount: number;
  sentAt?: string;
  deliveredAt?: string;
  relatedEntityId?: string;
  relatedEntityType?: string;
  gateway: string;
  characterCount: number;
  segmentCount: number;
  createdAt: string;
}

export interface SmsTemplateDto {
  id: string;
  templateKey: string;
  nameAr: string;
  contentTemplate: string;
  isTemplateActive: boolean;
  category: string;
  maxLength: number;
}

export interface SmsDashboardDto {
  isEnabled: boolean;
  isGatewayConnected: boolean;
  gatewayUrl?: string;
  senderName?: string;
  dailyLimit: number;
  sentToday: number;
  sentThisMonth: number;
  failedToday: number;
  pendingCount: number;
  deliveredToday: number;
  deliveryRate: number;
  recentMessages: SmsMessageDto[];
}

export interface SmsGatewaySettingsDto {
  enabled: boolean;
  apiUrl?: string;
  hasApiKey: boolean;
  /** Gateway mode: "local_android" (base URL + /sms/send) or "cloud_api" (full endpoint URL) */
  gatewayMode: string;
  senderName?: string;
  dailyLimit: number;
  sendAppointmentReminders: boolean;
  sendPaymentReminders: boolean;
  reminderHours?: string;
  isGatewayConnected: boolean;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export type Tab = "dashboard" | "messages" | "templates" | "settings";

// ─── Shared Constants ─────────────────────────────────────────────────────────

export const TABS: {
  key: Tab;
  label: string;
  icon: React.ComponentType<{ className?: string }>;
}[] = [
  { key: "dashboard", label: "لوحة التحكم", icon: LayoutDashboard },
  { key: "messages", label: "الرسائل", icon: FileText },
  { key: "templates", label: "القوالب", icon: MessageSquare },
  { key: "settings", label: "الإعدادات", icon: Settings },
];

export const STATUS_MAP: Record<
  string,
  {
    label: string;
    color: string;
    icon: React.ComponentType<{ className?: string }>;
  }
> = {
  sent: { label: "مرسلة", color: "bg-green-100 text-green-700", icon: Send },
  delivered: {
    label: "تم التسليم",
    color: "bg-emerald-100 text-emerald-700",
    icon: CheckCircle2,
  },
  pending: {
    label: "في الانتظار",
    color: "bg-amber-100 text-amber-700",
    icon: Clock,
  },
  failed: {
    label: "فاشلة",
    color: "bg-red-100 text-red-700",
    icon: XCircle,
  },
};

export const STATUS_FILTER_OPTIONS = [
  { value: "", label: "الكل" },
  { value: "sent", label: "مرسلة" },
  { value: "delivered", label: "تم التسليم" },
  { value: "pending", label: "في الانتظار" },
  { value: "failed", label: "فاشلة" },
];

export const CATEGORY_COLORS: Record<string, string> = {
  appointment: "bg-blue-50 text-blue-700",
  payment: "bg-emerald-50 text-emerald-700",
  reminder: "bg-amber-50 text-amber-700",
  general: "bg-gray-100 text-gray-700",
  marketing: "bg-purple-50 text-purple-700",
};

export const CATEGORY_LABELS: Record<string, string> = {
  appointment: "موعد",
  payment: "دفع",
  reminder: "تذكير",
  general: "عام",
  marketing: "تسويق",
};

export const inputCls =
  "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue transition";

// ─── Helpers ──────────────────────────────────────────────────────────────────

export function formatRelativeTime(dateStr: string): string {
  const date = new Date(dateStr);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMin = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMs / 3600000);
  const diffDays = Math.floor(diffMs / 86400000);

  if (diffMin < 1) return "الآن";
  if (diffMin < 60) return `منذ ${diffMin} دقيقة`;
  if (diffHours < 24) return `منذ ${diffHours} ساعة`;
  if (diffDays < 7) return `منذ ${diffDays} يوم`;
  return date.toLocaleDateString("ar-YE", {
    year: "numeric",
    month: "short",
    day: "numeric",
  });
}

export function formatFullDate(dateStr: string): string {
  return new Date(dateStr).toLocaleDateString("ar-YE", {
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}
