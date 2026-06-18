"use client";
import { useEffect, useState, useCallback } from "react";
import {
  MessageSquare, Send, LayoutDashboard, FileText, Settings,
  RefreshCw, Clock, CheckCircle2, XCircle, AlertTriangle,
  Search, Filter, ChevronLeft, ChevronRight, Eye, EyeOff,
  Loader2, Pencil, X, Save, Zap, Phone, ArrowUpDown,
  Activity,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { extractErrorMessage } from "@/lib/errors";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";

// ─── Types ────────────────────────────────────────────────────────────────────

interface SmsMessageDto {
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

interface SmsTemplateDto {
  id: string;
  templateKey: string;
  nameAr: string;
  contentTemplate: string;
  isTemplateActive: boolean;
  category: string;
  maxLength: number;
}

interface SmsDashboardDto {
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

interface SmsGatewaySettingsDto {
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

interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

type Tab = "dashboard" | "messages" | "templates" | "settings";

const TABS: { key: Tab; label: string; icon: React.ComponentType<{ className?: string }> }[] = [
  { key: "dashboard", label: "لوحة التحكم", icon: LayoutDashboard },
  { key: "messages", label: "الرسائل", icon: FileText },
  { key: "templates", label: "القوالب", icon: MessageSquare },
  { key: "settings", label: "الإعدادات", icon: Settings },
];

const STATUS_MAP: Record<string, { label: string; color: string; icon: React.ComponentType<{ className?: string }> }> = {
  sent: { label: "مرسلة", color: "bg-green-100 text-green-700", icon: Send },
  delivered: { label: "تم التسليم", color: "bg-emerald-100 text-emerald-700", icon: CheckCircle2 },
  pending: { label: "في الانتظار", color: "bg-amber-100 text-amber-700", icon: Clock },
  failed: { label: "فاشلة", color: "bg-red-100 text-red-700", icon: XCircle },
};

const STATUS_FILTER_OPTIONS = [
  { value: "", label: "الكل" },
  { value: "sent", label: "مرسلة" },
  { value: "delivered", label: "تم التسليم" },
  { value: "pending", label: "في الانتظار" },
  { value: "failed", label: "فاشلة" },
];

const CATEGORY_COLORS: Record<string, string> = {
  appointment: "bg-blue-50 text-blue-700",
  payment: "bg-emerald-50 text-emerald-700",
  reminder: "bg-amber-50 text-amber-700",
  general: "bg-gray-100 text-gray-700",
  marketing: "bg-purple-50 text-purple-700",
};

const CATEGORY_LABELS: Record<string, string> = {
  appointment: "موعد",
  payment: "دفع",
  reminder: "تذكير",
  general: "عام",
  marketing: "تسويق",
};

const inputCls =
  "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue transition";

// FE-11: getApiErrorMessage removed — use extractErrorMessage from @/lib/errors.

function formatRelativeTime(dateStr: string): string {
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

function formatFullDate(dateStr: string): string {
  return new Date(dateStr).toLocaleDateString("ar-YE", {
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

// ─── Main Page ────────────────────────────────────────────────────────────────

export default function SmsPage() {
  const [activeTab, setActiveTab] = useState<Tab>("dashboard");
  const [showQuickSend, setShowQuickSend] = useState(false);

  return (
    <div className="space-y-5 max-w-6xl">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-extrabold text-gray-900 flex items-center gap-2">
            <MessageSquare className="w-6 h-6 text-clinic-blue" />
            إدارة الرسائل القصيرة
          </h1>
          <p className="text-sm text-gray-500 mt-0.5">إرسال وتتبع الرسائل القصيرة للمرضى</p>
        </div>
        <button
          onClick={() => setShowQuickSend(true)}
          className="flex items-center gap-1.5 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition shadow-sm"
        >
          <Zap className="w-4 h-4" />
          إرسال سريع
        </button>
      </div>

      {/* Tabs */}
      <div className="inline-flex rounded-lg border border-gray-200 bg-gray-50 p-1">
        {TABS.map((tab) => {
          const Icon = tab.icon;
          return (
            <button
              key={tab.key}
              onClick={() => setActiveTab(tab.key)}
              className={cn(
                "flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-md transition",
                activeTab === tab.key
                  ? "bg-white text-clinic-blue shadow-sm"
                  : "text-gray-600 hover:text-gray-900"
              )}
            >
              <Icon className="w-4 h-4" />
              {tab.label}
            </button>
          );
        })}
      </div>

      {/* Tab Content */}
      {activeTab === "dashboard" && <DashboardTab />}
      {activeTab === "messages" && <MessagesTab />}
      {activeTab === "templates" && <TemplatesTab />}
      {activeTab === "settings" && <SettingsTab />}

      {/* Quick Send Modal */}
      {showQuickSend && (
        <QuickSendModal onClose={() => setShowQuickSend(false)} />
      )}
    </div>
  );
}

// ─── Dashboard Tab ────────────────────────────────────────────────────────────

function DashboardTab() {
  const [dashboard, setDashboard] = useState<SmsDashboardDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  const fetchDashboard = useCallback(async () => {
    setLoading(true);
    setError(false);
    try {
      const { data } = await api.get<SmsDashboardDto>("/api/sms/dashboard");
      setDashboard(data);
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchDashboard();
  }, [fetchDashboard]);

  if (error) {
    return (
      <div className="flex flex-col items-center justify-center py-20 gap-4 text-center">
        <XCircle className="w-12 h-12 text-red-400" />
        <p className="text-gray-600 text-sm">تعذّر تحميل لوحة التحكم</p>
        <button
          onClick={fetchDashboard}
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
        >
          <RefreshCw className="w-4 h-4" />
          إعادة المحاولة
        </button>
      </div>
    );
  }

  if (loading) {
    return (
      <div className="space-y-4 animate-pulse">
        <div className="grid grid-cols-2 md:grid-cols-5 gap-3">
          {Array.from({ length: 5 }).map((_, i) => (
            <div key={i} className="h-24 bg-gray-100 rounded-xl" />
          ))}
        </div>
        <div className="h-64 bg-gray-100 rounded-xl" />
      </div>
    );
  }

  const stats = [
    {
      label: "رسائل اليوم",
      value: dashboard?.sentToday ?? 0,
      color: "text-blue-700 bg-blue-50",
      icon: Send,
    },
    {
      label: "المرسلة هذا الشهر",
      value: dashboard?.sentThisMonth ?? 0,
      color: "text-green-700 bg-green-50",
      icon: CheckCircle2,
    },
    {
      label: "الفاشلة",
      value: dashboard?.failedToday ?? 0,
      color: "text-red-700 bg-red-50",
      icon: XCircle,
    },
    {
      label: "في الانتظار",
      value: dashboard?.pendingCount ?? 0,
      color: "text-amber-700 bg-amber-50",
      icon: Clock,
    },
    {
      label: "معدل التوصيل",
      value: `${(dashboard?.deliveryRate ?? 0).toFixed(1)}%`,
      color: "text-emerald-700 bg-emerald-50",
      icon: Activity,
    },
  ];

  return (
    <div className="space-y-5">
      {/* Stats Cards */}
      <div className="grid grid-cols-2 md:grid-cols-5 gap-3">
        {stats.map((stat) => {
          const Icon = stat.icon;
          return (
            <div key={stat.label} className={cn("rounded-xl p-4", stat.color)}>
              <div className="flex items-center justify-between mb-2">
                <Icon className="w-5 h-5 opacity-70" />
              </div>
              <p className="text-2xl font-bold">{stat.value}</p>
              <p className="text-xs mt-1 opacity-80">{stat.label}</p>
            </div>
          );
        })}
      </div>

      {/* Gateway Status */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm px-5 py-4 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div
            className={cn(
              "w-3 h-3 rounded-full",
              dashboard?.isGatewayConnected ? "bg-green-500" : "bg-red-500"
            )}
          />
          <span className="text-sm font-medium text-gray-700">
            حالة بوابة الرسائل:
          </span>
          <span
            className={cn(
              "text-sm font-bold",
              dashboard?.isGatewayConnected ? "text-green-700" : "text-red-700"
            )}
          >
            {dashboard?.isGatewayConnected ? "متصل" : "غير متصل"}
          </span>
        </div>
        {dashboard?.senderName && (
          <span className="text-xs text-gray-500">
            المرسل: <span className="font-mono font-medium" dir="ltr">{dashboard.senderName}</span>
          </span>
        )}
        {dashboard && (
          <span className="text-xs text-gray-500">
            الحد اليومي: {dashboard.sentToday}/{dashboard.dailyLimit}
          </span>
        )}
      </div>

      {/* Recent Messages Table */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
          <h2 className="font-bold text-gray-900">آخر الرسائل</h2>
          <span className="text-xs text-gray-400">
            {dashboard?.recentMessages?.length ?? 0} رسالة
          </span>
        </div>
        {!dashboard?.recentMessages || dashboard.recentMessages.length === 0 ? (
          <div className="text-center py-16 text-gray-400">
            <MessageSquare className="w-12 h-12 mx-auto mb-3 opacity-30" />
            <p className="text-sm">لا توجد رسائل بعد</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-200">
                <tr>
                  {["المريض", "الرقم", "الرسالة", "الحالة", "الوقت"].map(
                    (h) => (
                      <th
                        key={h}
                        className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap"
                      >
                        {h}
                      </th>
                    )
                  )}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {dashboard.recentMessages.map((msg) => {
                  const statusInfo = STATUS_MAP[msg.status] || STATUS_MAP.pending;
                  const StatusIcon = statusInfo.icon;
                  return (
                    <tr key={msg.id} className="hover:bg-gray-50 transition">
                      <td className="px-4 py-3 font-medium text-gray-900 whitespace-nowrap">
                        {msg.patientName || "—"}
                      </td>
                      <td className="px-4 py-3 text-gray-500 font-mono text-xs" dir="ltr">
                        {msg.phoneNumber}
                      </td>
                      <td className="px-4 py-3 text-gray-600 max-w-[200px]">
                        <p className="truncate">{msg.messageContent}</p>
                      </td>
                      <td className="px-4 py-3">
                        <span
                          className={cn(
                            "text-[10px] px-2 py-0.5 rounded-full font-medium inline-flex items-center gap-1",
                            statusInfo.color
                          )}
                        >
                          <StatusIcon className="w-2.5 h-2.5" />
                          {statusInfo.label}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-gray-500 text-xs whitespace-nowrap">
                        {formatRelativeTime(msg.createdAt)}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}

// ─── Messages Tab ─────────────────────────────────────────────────────────────

function MessagesTab() {
  const [messages, setMessages] = useState<SmsMessageDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [totalPages, setTotalPages] = useState(1);
  const [statusFilter, setStatusFilter] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [retryingId, setRetryingId] = useState<string | null>(null);

  const fetchMessages = useCallback(async () => {
    setLoading(true);
    setError(false);
    try {
      const params = new URLSearchParams({
        page: page.toString(),
        pageSize: pageSize.toString(),
      });
      if (statusFilter) params.set("status", statusFilter);
      const { data } = await api.get<PagedResult<SmsMessageDto>>(
        `/api/sms/messages?${params.toString()}`
      );
      setMessages(data.items);
      setTotalCount(data.totalCount);
      setTotalPages(data.totalPages);
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, statusFilter]);

  useEffect(() => {
    fetchMessages();
  }, [fetchMessages]);

  const handleRetry = async (id: string) => {
    setRetryingId(id);
    try {
      await api.post(`/api/sms/messages/${id}/retry`);
      toast.success("تم إعادة إرسال الرسالة بنجاح");
      fetchMessages();
    } catch (err) {
      toast.error(extractErrorMessage(err, "فشل إعادة إرسال الرسالة"));
    } finally {
      setRetryingId(null);
    }
  };

  if (error) {
    return (
      <div className="flex flex-col items-center justify-center py-20 gap-4 text-center">
        <XCircle className="w-12 h-12 text-red-400" />
        <p className="text-gray-600 text-sm">تعذّر تحميل الرسائل</p>
        <button
          onClick={fetchMessages}
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
        >
          <RefreshCw className="w-4 h-4" />
          إعادة المحاولة
        </button>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {/* Filters */}
      <div className="flex flex-wrap items-center gap-3">
        <div className="flex items-center gap-2">
          <Filter className="w-4 h-4 text-gray-400" />
          <select
            value={statusFilter}
            onChange={(e) => {
              setStatusFilter(e.target.value);
              setPage(1);
            }}
            className="px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue"
          >
            {STATUS_FILTER_OPTIONS.map((opt) => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>
        </div>
        <span className="text-xs text-gray-500 mr-auto">
          {totalCount} رسالة
        </span>
        <button
          onClick={fetchMessages}
          className="p-2 rounded-lg text-gray-400 hover:text-clinic-blue hover:bg-blue-50 transition"
          title="تحديث"
        >
          <RefreshCw className="w-4 h-4" />
        </button>
      </div>

      {/* Messages Table */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
        {loading ? (
          <div className="p-5 space-y-2 animate-pulse">
            {Array.from({ length: 8 }).map((_, i) => (
              <div key={i} className="h-14 bg-gray-100 rounded-lg" />
            ))}
          </div>
        ) : messages.length === 0 ? (
          <div className="text-center py-16 text-gray-400">
            <FileText className="w-12 h-12 mx-auto mb-3 opacity-30" />
            <p className="text-sm">لا توجد رسائل</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-200">
                <tr>
                  {[
                    "المريض",
                    "رقم الهاتف",
                    "المحتوى",
                    "القالب",
                    "الحالة",
                    "عدد الأجزاء",
                    "التاريخ",
                    "",
                  ].map((h) => (
                    <th
                      key={h}
                      className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap"
                    >
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {messages.map((msg) => {
                  const statusInfo =
                    STATUS_MAP[msg.status] || STATUS_MAP.pending;
                  const StatusIcon = statusInfo.icon;
                  return (
                    <tr key={msg.id} className="hover:bg-gray-50 transition">
                      <td className="px-4 py-3 font-medium text-gray-900 whitespace-nowrap">
                        {msg.patientName || "—"}
                      </td>
                      <td
                        className="px-4 py-3 text-gray-500 font-mono text-xs"
                        dir="ltr"
                      >
                        {msg.phoneNumber}
                      </td>
                      <td className="px-4 py-3 text-gray-600 max-w-[220px]">
                        <p className="truncate">{msg.messageContent}</p>
                        {msg.errorMessage && (
                          <p className="text-xs text-red-500 mt-0.5 flex items-center gap-1">
                            <AlertTriangle className="w-3 h-3 flex-shrink-0" />
                            {msg.errorMessage}
                          </p>
                        )}
                      </td>
                      <td className="px-4 py-3">
                        <span className="text-xs bg-gray-100 text-gray-600 px-2 py-0.5 rounded-full font-medium">
                          {msg.templateType || "—"}
                        </span>
                      </td>
                      <td className="px-4 py-3">
                        <span
                          className={cn(
                            "text-[10px] px-2 py-0.5 rounded-full font-medium inline-flex items-center gap-1",
                            statusInfo.color
                          )}
                        >
                          <StatusIcon className="w-2.5 h-2.5" />
                          {statusInfo.label}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-gray-500 text-xs text-center">
                        {msg.segmentCount}
                      </td>
                      <td className="px-4 py-3 text-gray-500 text-xs whitespace-nowrap">
                        {formatFullDate(msg.createdAt)}
                      </td>
                      <td className="px-4 py-3">
                        {msg.status === "failed" && msg.retryCount < 3 && (
                          <button
                            onClick={() => handleRetry(msg.id)}
                            disabled={retryingId === msg.id}
                            className="p-1.5 rounded-lg text-blue-600 hover:bg-blue-50 transition disabled:opacity-60"
                            title="إعادة الإرسال"
                          >
                            {retryingId === msg.id ? (
                              <Loader2 className="w-4 h-4 animate-spin" />
                            ) : (
                              <RefreshCw className="w-4 h-4" />
                            )}
                          </button>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between px-5 py-3 border-t border-gray-100 bg-gray-50">
            <p className="text-xs text-gray-500">
              صفحة {page} من {totalPages}
            </p>
            <div className="flex items-center gap-1">
              <button
                onClick={() => setPage(Math.max(1, page - 1))}
                disabled={page <= 1}
                className="p-1.5 rounded-lg text-gray-500 hover:bg-white hover:text-clinic-blue disabled:opacity-40 disabled:cursor-not-allowed transition"
              >
                <ChevronRight className="w-4 h-4" />
              </button>
              <button
                onClick={() => setPage(Math.min(totalPages, page + 1))}
                disabled={page >= totalPages}
                className="p-1.5 rounded-lg text-gray-500 hover:bg-white hover:text-clinic-blue disabled:opacity-40 disabled:cursor-not-allowed transition"
              >
                <ChevronLeft className="w-4 h-4" />
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

// ─── Templates Tab ────────────────────────────────────────────────────────────

function TemplatesTab() {
  const [templates, setTemplates] = useState<SmsTemplateDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editContent, setEditContent] = useState("");
  const [saving, setSaving] = useState(false);

  const fetchTemplates = useCallback(async () => {
    setLoading(true);
    setError(false);
    try {
      const { data } = await api.get<SmsTemplateDto[]>("/api/sms/templates");
      setTemplates(data);
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchTemplates();
  }, [fetchTemplates]);

  const handleEdit = (t: SmsTemplateDto) => {
    setEditingId(t.id);
    setEditContent(t.contentTemplate);
  };

  const handleCancelEdit = () => {
    setEditingId(null);
    setEditContent("");
  };

  const handleSave = async (t: SmsTemplateDto) => {
    setSaving(true);
    try {
      const { data } = await api.put<SmsTemplateDto>(
        `/api/sms/templates/${t.id}`,
        { contentTemplate: editContent, isTemplateActive: t.isTemplateActive }
      );
      setTemplates((prev) =>
        prev.map((tmpl) => (tmpl.id === data.id ? data : tmpl))
      );
      setEditingId(null);
      setEditContent("");
      toast.success("تم تحديث القالب بنجاح");
    } catch (err) {
      toast.error(extractErrorMessage(err, "فشل تحديث القالب"));
    } finally {
      setSaving(false);
    }
  };

  /** Highlight {{placeholder}} patterns in template content */
  const renderTemplateContent = (content: string) => {
    const parts = content.split(/({{[^}]+}})/g);
    return parts.map((part, i) =>
      /{{[^}]+}}/.test(part) ? (
        <span
          key={i}
          className="bg-clinic-blue/10 text-clinic-blue font-semibold px-1 rounded"
        >
          {part}
        </span>
      ) : (
        <span key={i}>{part}</span>
      )
    );
  };

  if (error) {
    return (
      <div className="flex flex-col items-center justify-center py-20 gap-4 text-center">
        <XCircle className="w-12 h-12 text-red-400" />
        <p className="text-gray-600 text-sm">تعذّر تحميل القوالب</p>
        <button
          onClick={fetchTemplates}
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
        >
          <RefreshCw className="w-4 h-4" />
          إعادة المحاولة
        </button>
      </div>
    );
  }

  if (loading) {
    return (
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 animate-pulse">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-40 bg-gray-100 rounded-xl" />
        ))}
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-gray-500">{templates.length} قالب</p>
        <button
          onClick={fetchTemplates}
          className="p-2 rounded-lg text-gray-400 hover:text-clinic-blue hover:bg-blue-50 transition"
          title="تحديث"
        >
          <RefreshCw className="w-4 h-4" />
        </button>
      </div>

      {templates.length === 0 ? (
        <div className="text-center py-16 text-gray-400 bg-white rounded-xl border border-gray-200">
          <MessageSquare className="w-12 h-12 mx-auto mb-3 opacity-30" />
          <p className="text-sm">لا توجد قوالب</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {templates.map((t) => {
            const isEditing = editingId === t.id;
            const currentContent = isEditing ? editContent : t.contentTemplate;
            const charCount = currentContent.length;
            const maxLen = t.maxLength || 160;
            const isOverLimit = charCount > maxLen;

            return (
              <div
                key={t.id}
                className={cn(
                  "bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-3 transition",
                  isEditing && "ring-2 ring-clinic-blue/30 border-clinic-blue/30"
                )}
              >
                {/* Header */}
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0">
                    <p className="text-sm font-bold text-gray-900 truncate">
                      {t.nameAr}
                    </p>
                    <p className="text-[10px] text-gray-400 font-mono mt-0.5">
                      {t.templateKey}
                    </p>
                  </div>
                  <div className="flex items-center gap-2 flex-shrink-0">
                    <span
                      className={cn(
                        "text-[10px] px-2 py-0.5 rounded-full font-medium",
                        CATEGORY_COLORS[t.category] || CATEGORY_COLORS.general
                      )}
                    >
                      {CATEGORY_LABELS[t.category] || t.category}
                    </span>
                    {!isEditing && (
                      <button
                        onClick={() => handleEdit(t)}
                        className="p-1.5 rounded-lg text-gray-400 hover:text-clinic-blue hover:bg-blue-50 transition"
                        title="تعديل"
                      >
                        <Pencil className="w-3.5 h-3.5" />
                      </button>
                    )}
                  </div>
                </div>

                {/* Content */}
                {isEditing ? (
                  <div className="space-y-2">
                    <textarea
                      value={editContent}
                      onChange={(e) => setEditContent(e.target.value)}
                      rows={4}
                      className={cn(
                        "w-full px-3 py-2 text-sm rounded-lg border focus:outline-none focus:ring-2 focus:ring-clinic-blue resize-none font-sans",
                        isOverLimit
                          ? "border-red-300 bg-red-50/30"
                          : "border-gray-300 bg-white"
                      )}
                      dir="rtl"
                    />
                    <div className="flex items-center justify-between">
                      <span
                        className={cn(
                          "text-xs",
                          isOverLimit ? "text-red-600 font-bold" : "text-gray-400"
                        )}
                      >
                        {charCount}/{maxLen}
                      </span>
                      <div className="flex items-center gap-2">
                        <button
                          onClick={handleCancelEdit}
                          className="px-3 py-1 text-xs rounded-lg border border-gray-300 text-gray-600 hover:bg-gray-50 transition"
                        >
                          إلغاء
                        </button>
                        <button
                          onClick={() => handleSave(t)}
                          disabled={saving}
                          className="px-3 py-1 text-xs rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-60 transition flex items-center gap-1"
                        >
                          {saving ? (
                            <Loader2 className="w-3 h-3 animate-spin" />
                          ) : (
                            <Save className="w-3 h-3" />
                          )}
                          حفظ
                        </button>
                      </div>
                    </div>
                  </div>
                ) : (
                  <>
                    <div className="text-xs text-gray-600 bg-gray-50 rounded-lg p-3 whitespace-pre-wrap font-sans leading-relaxed">
                      {renderTemplateContent(t.contentTemplate)}
                    </div>
                    <div className="flex items-center justify-between">
                      <span
                        className={cn(
                          "text-[10px]",
                          charCount > maxLen
                            ? "text-red-500 font-semibold"
                            : "text-gray-400"
                        )}
                      >
                        {charCount}/{maxLen}
                      </span>
                      <span
                        className={cn(
                          "text-[10px] px-2 py-0.5 rounded-full font-medium",
                          t.isTemplateActive
                            ? "bg-green-100 text-green-700"
                            : "bg-gray-100 text-gray-500"
                        )}
                      >
                        {t.isTemplateActive ? "مفعّل" : "معطّل"}
                      </span>
                    </div>
                  </>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

// ─── Settings Tab ─────────────────────────────────────────────────────────────

function SettingsTab() {
  const [settings, setSettings] = useState<SmsGatewaySettingsDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [showApiKey, setShowApiKey] = useState(false);
  const [apiKey, setApiKey] = useState("");
  const [testLoading, setTestLoading] = useState(false);
  const [testResult, setTestResult] = useState<{
    connected: boolean;
    message: string;
  } | null>(null);

  const fetchSettings = useCallback(async () => {
    setLoading(true);
    setError(false);
    try {
      const { data } = await api.get<SmsGatewaySettingsDto>("/api/sms/settings");
      setSettings(data);
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchSettings();
  }, [fetchSettings]);

  const handleSave = async () => {
    if (!settings) return;
    setSaving(true);
    setSaved(false);
    try {
      const payload: Record<string, unknown> = {
        enabled: settings.enabled,
        apiUrl: settings.apiUrl,
        gatewayMode: settings.gatewayMode || "local_android",
        senderName: settings.senderName,
        dailyLimit: settings.dailyLimit,
        sendAppointmentReminders: settings.sendAppointmentReminders,
        sendPaymentReminders: settings.sendPaymentReminders,
        reminderHours: settings.reminderHours,
      };
      if (apiKey) {
        payload.apiKey = apiKey;
      }
      const { data } = await api.put<SmsGatewaySettingsDto>(
        "/api/sms/settings",
        payload
      );
      setSettings(data);
      setApiKey("");
      setShowApiKey(false);
      setSaved(true);
      toast.success("تم حفظ الإعدادات بنجاح");
      setTimeout(() => setSaved(false), 3000);
    } catch (err) {
      toast.error(extractErrorMessage(err, "فشل حفظ الإعدادات"));
    } finally {
      setSaving(false);
    }
  };

  const handleTestConnection = async () => {
    setTestLoading(true);
    setTestResult(null);
    try {
      const { data } = await api.post<{ connected: boolean; message: string }>(
        "/api/sms/test-connection"
      );
      setTestResult(data);
      if (data.connected) {
        toast.success(data.message || "تم الاتصال بنجاح");
      } else {
        toast.error(data.message || "فشل الاتصال");
      }
    } catch (err) {
      const msg = extractErrorMessage(err, "فشل اختبار الاتصال");
      setTestResult({ connected: false, message: msg });
      toast.error(msg);
    } finally {
      setTestLoading(false);
    }
  };

  if (error) {
    return (
      <div className="flex flex-col items-center justify-center py-20 gap-4 text-center">
        <XCircle className="w-12 h-12 text-red-400" />
        <p className="text-gray-600 text-sm">تعذّر تحميل الإعدادات</p>
        <button
          onClick={fetchSettings}
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
        >
          <RefreshCw className="w-4 h-4" />
          إعادة المحاولة
        </button>
      </div>
    );
  }

  if (loading || !settings) {
    return (
      <div className="animate-pulse space-y-3 max-w-2xl">
        {Array.from({ length: 8 }).map((_, i) => (
          <div key={i} className="h-12 bg-gray-100 rounded-lg" />
        ))}
      </div>
    );
  }

  return (
    <div className="max-w-2xl space-y-5">
      {/* SMS Enabled Toggle */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-5">
        <h3 className="font-bold text-gray-900 flex items-center gap-2">
          <Settings className="w-4 h-4 text-clinic-blue" />
          الإعدادات العامة
        </h3>

        {/* Toggle: SMS Enabled */}
        <div className="flex items-center justify-between">
          <div>
            <p className="text-sm font-medium text-gray-700">تفعيل الرسائل القصيرة</p>
            <p className="text-xs text-gray-500 mt-0.5">تفعيل أو تعطيل نظام الرسائل القصيرة بالكامل</p>
          </div>
          <button
            onClick={() =>
              setSettings({ ...settings, enabled: !settings.enabled })
            }
            className={cn(
              "relative inline-flex h-6 w-11 items-center rounded-full transition-colors",
              settings.enabled ? "bg-clinic-blue" : "bg-gray-300"
            )}
            role="switch"
            aria-checked={settings.enabled}
          >
            <span
              className={cn(
                "inline-block h-4 w-4 transform rounded-full bg-white shadow transition-transform",
                settings.enabled ? "-translate-x-6" : "-translate-x-1"
              )}
            />
          </button>
        </div>

        {/* Gateway Mode */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">
            نوع البوابة
          </label>
          <div className="flex gap-3">
            <button
              type="button"
              onClick={() => setSettings({ ...settings, gatewayMode: "local_android" })}
              className={cn(
                "flex-1 px-3 py-2.5 text-xs font-medium rounded-lg border-2 transition text-center",
                settings.gatewayMode === "local_android" || !settings.gatewayMode
                  ? "border-clinic-blue bg-clinic-blue/5 text-clinic-blue"
                  : "border-gray-200 bg-white text-gray-600 hover:border-gray-300"
              )}
            >
              <Phone className="w-4 h-4 mx-auto mb-1" />
              بوابة أندرويد محلية
            </button>
            <button
              type="button"
              onClick={() => setSettings({ ...settings, gatewayMode: "cloud_api" })}
              className={cn(
                "flex-1 px-3 py-2.5 text-xs font-medium rounded-lg border-2 transition text-center",
                settings.gatewayMode === "cloud_api"
                  ? "border-clinic-blue bg-clinic-blue/5 text-clinic-blue"
                  : "border-gray-200 bg-white text-gray-600 hover:border-gray-300"
              )}
            >
              <Zap className="w-4 h-4 mx-auto mb-1" />
              بوابة سحابية (API)
            </button>
          </div>
          <p className="text-xs text-gray-400 mt-1">
            {settings.gatewayMode === "cloud_api"
              ? "أدخل رابط إرسال الرسائل الكامل (Full Endpoint URL)"
              : "أدخل الرابط الأساسي للبوابة (Base URL) — سيتم إضافة /sms/send تلقائياً"}
          </p>
        </div>

        {/* Gateway URL */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">
            عنوان بوابة الرسائل (URL)
          </label>
          <input
            value={settings.apiUrl ?? ""}
            onChange={(e) =>
              setSettings({ ...settings, apiUrl: e.target.value })
            }
            className={inputCls}
            placeholder={settings.gatewayMode === "cloud_api" ? "https://us-central1-xxx.cloudfunctions.net/api_sms_send" : "http://192.168.1.100:8080"}
            dir="ltr"
          />
          {settings.gatewayMode !== "cloud_api" && (
            <p className="text-xs text-gray-400 mt-1">
              سيتم إضافة <code className="bg-gray-100 px-1 rounded font-mono" dir="ltr">/sms/send</code> و <code className="bg-gray-100 px-1 rounded font-mono" dir="ltr">/status</code> تلقائياً
            </p>
          )}
        </div>

        {/* API Key */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">
            مفتاح API
          </label>
          <div className="relative">
            <input
              value={apiKey}
              onChange={(e) => setApiKey(e.target.value)}
              type={showApiKey ? "text" : "password"}
              className={cn(inputCls, "pl-10")}
              placeholder={settings.hasApiKey ? "•••••••• (اتركه فارغًا للإبقاء على الحالي)" : "أدخل مفتاح API"}
              dir="ltr"
            />
            <button
              type="button"
              onClick={() => setShowApiKey(!showApiKey)}
              className="absolute left-2 top-1/2 -translate-y-1/2 p-1 text-gray-400 hover:text-gray-600 transition"
            >
              {showApiKey ? (
                <EyeOff className="w-4 h-4" />
              ) : (
                <Eye className="w-4 h-4" />
              )}
            </button>
          </div>
        </div>

        {/* Sender Name */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">
            اسم المرسل
          </label>
          <input
            value={settings.senderName ?? ""}
            onChange={(e) =>
              setSettings({ ...settings, senderName: e.target.value })
            }
            className={inputCls}
            placeholder="AqlanDental"
            dir="ltr"
          />
        </div>

        {/* Daily Limit */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">
            الحد اليومي للرسائل
          </label>
          <input
            type="number"
            value={settings.dailyLimit}
            onChange={(e) =>
              setSettings({
                ...settings,
                dailyLimit: parseInt(e.target.value) || 0,
              })
            }
            className={inputCls}
            min={0}
            dir="ltr"
          />
        </div>
      </div>

      {/* Reminders Section */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-5">
        <h3 className="font-bold text-gray-900 flex items-center gap-2">
          <Clock className="w-4 h-4 text-clinic-blue" />
          التذكيرات
        </h3>

        {/* Toggle: Appointment Reminders */}
        <div className="flex items-center justify-between">
          <div>
            <p className="text-sm font-medium text-gray-700">
              تذكيرات المواعيد
            </p>
            <p className="text-xs text-gray-500 mt-0.5">
              إرسال تذكير تلقائي قبل الموعد
            </p>
          </div>
          <button
            onClick={() =>
              setSettings({
                ...settings,
                sendAppointmentReminders: !settings.sendAppointmentReminders,
              })
            }
            className={cn(
              "relative inline-flex h-6 w-11 items-center rounded-full transition-colors",
              settings.sendAppointmentReminders
                ? "bg-clinic-blue"
                : "bg-gray-300"
            )}
            role="switch"
            aria-checked={settings.sendAppointmentReminders}
          >
            <span
              className={cn(
                "inline-block h-4 w-4 transform rounded-full bg-white shadow transition-transform",
                settings.sendAppointmentReminders
                  ? "-translate-x-6"
                  : "-translate-x-1"
              )}
            />
          </button>
        </div>

        {/* Toggle: Payment Reminders */}
        <div className="flex items-center justify-between">
          <div>
            <p className="text-sm font-medium text-gray-700">
              تذكيرات الدفع
            </p>
            <p className="text-xs text-gray-500 mt-0.5">
              إرسال تذكير بالمبالغ المستحقة
            </p>
          </div>
          <button
            onClick={() =>
              setSettings({
                ...settings,
                sendPaymentReminders: !settings.sendPaymentReminders,
              })
            }
            className={cn(
              "relative inline-flex h-6 w-11 items-center rounded-full transition-colors",
              settings.sendPaymentReminders
                ? "bg-clinic-blue"
                : "bg-gray-300"
            )}
            role="switch"
            aria-checked={settings.sendPaymentReminders}
          >
            <span
              className={cn(
                "inline-block h-4 w-4 transform rounded-full bg-white shadow transition-transform",
                settings.sendPaymentReminders
                  ? "-translate-x-6"
                  : "-translate-x-1"
              )}
            />
          </button>
        </div>

        {/* Reminder Hours */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">
            ساعات التذكير
          </label>
          <input
            value={settings.reminderHours ?? ""}
            onChange={(e) =>
              setSettings({ ...settings, reminderHours: e.target.value })
            }
            className={inputCls}
            placeholder="24,2"
            dir="ltr"
          />
          <p className="text-xs text-gray-400 mt-1">
            أدخل الساعات مفصولة بفواصل (مثال: 24,2 يعني تذكير قبل 24 ساعة وساعتين)
          </p>
        </div>
      </div>

      {/* Test & Save */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-4">
        {/* Test Connection */}
        <div className="space-y-3">
          <h3 className="font-bold text-gray-900 flex items-center gap-2">
            <ArrowUpDown className="w-4 h-4 text-clinic-blue" />
            اختبار الاتصال
          </h3>
          <button
            onClick={handleTestConnection}
            disabled={testLoading}
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 disabled:opacity-60 transition"
          >
            {testLoading ? (
              <Loader2 className="w-4 h-4 animate-spin" />
            ) : (
              <Zap className="w-4 h-4" />
            )}
            {testLoading ? "جارٍ الاختبار..." : "اختبار الاتصال"}
          </button>
          {testResult && (
            <div
              className={cn(
                "text-xs px-3 py-2 rounded-lg flex items-center gap-2",
                testResult.connected
                  ? "bg-green-50 text-green-700 border border-green-200"
                  : "bg-red-50 text-red-700 border border-red-200"
              )}
            >
              {testResult.connected ? (
                <CheckCircle2 className="w-4 h-4 flex-shrink-0" />
              ) : (
                <XCircle className="w-4 h-4 flex-shrink-0" />
              )}
              {testResult.message}
            </div>
          )}
        </div>

        {/* Save */}
        <div className="flex items-center gap-3 pt-2 border-t border-gray-100">
          <button
            onClick={handleSave}
            disabled={saving}
            className="flex items-center gap-2 px-5 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-60 transition"
          >
            <Save className="w-4 h-4" />
            {saving ? "جارٍ الحفظ..." : "حفظ الإعدادات"}
          </button>
          {saved && (
            <span className="text-sm text-green-600 font-medium flex items-center gap-1">
              <CheckCircle2 className="w-4 h-4" />
              تم الحفظ
            </span>
          )}
        </div>
      </div>
    </div>
  );
}

// ─── Quick Send Modal ─────────────────────────────────────────────────────────

interface QuickSendModalProps {
  onClose: () => void;
}

function QuickSendModal({ onClose }: QuickSendModalProps) {
  const [patientSearch, setPatientSearch] = useState("");
  const [patients, setPatients] = useState<
    { id: string; name: string; phone: string }[]
  >([]);
  const [selectedPatient, setSelectedPatient] = useState<{
    id: string;
    name: string;
    phone: string;
  } | null>(null);
  const [searching, setSearching] = useState(false);
  const [templates, setTemplates] = useState<SmsTemplateDto[]>([]);
  const [selectedTemplate, setSelectedTemplate] = useState<string>("");
  const [message, setMessage] = useState("");
  const [sending, setSending] = useState(false);

  // Fetch templates for dropdown
  useEffect(() => {
    api
      .get<SmsTemplateDto[]>("/api/sms/templates")
      .then(({ data }) => setTemplates(data.filter((t) => t.isTemplateActive)))
      .catch(() => {});
  }, []);

  // Search patients
  useEffect(() => {
    if (!patientSearch.trim() || patientSearch.trim().length < 2) {
      setPatients([]);
      return;
    }
    const timer = setTimeout(async () => {
      setSearching(true);
      try {
        const { data } = await api.get<{
          data: { id: string; fullName: string; phone?: string }[];
          items?: { id: string; fullName: string; phone?: string }[];
        }>(`/api/patients?search=${encodeURIComponent(patientSearch.trim())}&pageSize=10`);
        // API returns { data: [...] } from PaginatedResponse, but doctor access returns { items: [...] }
        const list = data.data ?? data.items ?? [];
        setPatients(list.map(p => ({ id: p.id, name: p.fullName, phone: p.phone ?? "" })));
      } catch {
        setPatients([]);
      } finally {
        setSearching(false);
      }
    }, 350);
    return () => clearTimeout(timer);
  }, [patientSearch]);

  // When a template is selected, fill the message
  const handleTemplateSelect = (templateKey: string) => {
    setSelectedTemplate(templateKey);
    const tmpl = templates.find((t) => t.templateKey === templateKey);
    if (tmpl) {
      setMessage(tmpl.contentTemplate);
    }
  };

  const handleSend = async () => {
    if (!selectedPatient || !message.trim()) return;
    setSending(true);
    try {
      await api.post("/api/sms/send", {
        patientId: selectedPatient.id,
        messageContent: message.trim(),
        templateType: selectedTemplate || undefined,
      });
      toast.success(`تم إرسال الرسالة إلى ${selectedPatient.name}`);
      onClose();
    } catch (err) {
      toast.error(extractErrorMessage(err, "فشل إرسال الرسالة"));
    } finally {
      setSending(false);
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4"
      role="dialog"
      aria-modal="true"
    >
      <div
        className="absolute inset-0 bg-black/50 backdrop-blur-sm"
        onClick={onClose}
      />
      <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-lg p-6 space-y-5 max-h-[90vh] overflow-y-auto">
        {/* Header */}
        <div className="flex items-center justify-between">
          <h3 className="text-base font-bold text-gray-900 flex items-center gap-2">
            <Send className="w-5 h-5 text-clinic-blue" />
            إرسال رسالة سريعة
          </h3>
          <button
            onClick={onClose}
            className="p-1 rounded-lg text-gray-400 hover:text-gray-700 hover:bg-gray-100 transition"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Patient Search */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">
            المريض <span className="text-red-500">*</span>
          </label>
          {selectedPatient ? (
            <div className="flex items-center justify-between px-3 py-2 bg-clinic-blue/5 border border-clinic-blue/20 rounded-lg">
              <div>
                <p className="text-sm font-medium text-gray-900">
                  {selectedPatient.name}
                </p>
                <p
                  className="text-xs text-gray-500 font-mono"
                  dir="ltr"
                >
                  {selectedPatient.phone}
                </p>
              </div>
              <button
                onClick={() => {
                  setSelectedPatient(null);
                  setPatientSearch("");
                }}
                className="p-1 rounded-lg text-gray-400 hover:text-red-500 hover:bg-red-50 transition"
              >
                <X className="w-4 h-4" />
              </button>
            </div>
          ) : (
            <div className="relative">
              <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
              <input
                type="text"
                value={patientSearch}
                onChange={(e) => setPatientSearch(e.target.value)}
                placeholder="ابحث عن مريض..."
                className={cn(inputCls, "pr-9")}
              />
              {searching && (
                <Loader2 className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 animate-spin text-gray-400" />
              )}
              {/* Search Results */}
              {patients.length > 0 && !selectedPatient && (
                <div className="absolute z-10 mt-1 w-full bg-white border border-gray-200 rounded-lg shadow-lg max-h-48 overflow-y-auto">
                  {patients.map((p) => (
                    <button
                      key={p.id}
                      onClick={() => {
                        setSelectedPatient(p);
                        setPatientSearch("");
                        setPatients([]);
                      }}
                      className="w-full text-right px-3 py-2 hover:bg-gray-50 transition flex items-center justify-between"
                    >
                      <span className="text-sm font-medium text-gray-900">
                        {p.name}
                      </span>
                      <span
                        className="text-xs text-gray-400 font-mono"
                        dir="ltr"
                      >
                        {p.phone}
                      </span>
                    </button>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>

        {/* Template Selection */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">
            القالب
          </label>
          <select
            value={selectedTemplate}
            onChange={(e) => handleTemplateSelect(e.target.value)}
            className={inputCls}
          >
            <option value="">بدون قالب</option>
            {templates.map((t) => (
              <option key={t.id} value={t.templateKey}>
                {t.nameAr}
              </option>
            ))}
          </select>
        </div>

        {/* Message Content */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">
            محتوى الرسالة <span className="text-red-500">*</span>
          </label>
          <textarea
            value={message}
            onChange={(e) => setMessage(e.target.value)}
            rows={4}
            className={cn(
              "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue resize-none",
              message.length > 160 && "border-amber-300"
            )}
            dir="rtl"
            placeholder="اكتب رسالتك هنا..."
          />
          <div className="flex items-center justify-between mt-1">
            <span
              className={cn(
                "text-xs",
                message.length > 160
                  ? "text-amber-600 font-medium"
                  : "text-gray-400"
              )}
            >
              {message.length}/160
              {message.length > 160 &&
                ` · ${Math.ceil(message.length / 153)} أجزاء`}
            </span>
            {message.length > 160 && (
              <span className="text-xs text-amber-600">
                سيتم تقسيم الرسالة
              </span>
            )}
          </div>
        </div>

        {/* Actions */}
        <div className="flex items-center justify-end gap-3 pt-2">
          <button
            onClick={onClose}
            className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 transition"
          >
            إلغاء
          </button>
          <button
            onClick={handleSend}
            disabled={!selectedPatient || !message.trim() || sending}
            className="px-5 py-2 text-sm font-medium text-white bg-clinic-blue rounded-lg hover:opacity-90 disabled:opacity-60 transition flex items-center gap-2"
          >
            {sending ? (
              <Loader2 className="w-4 h-4 animate-spin" />
            ) : (
              <Send className="w-4 h-4" />
            )}
            {sending ? "جارٍ الإرسال..." : "إرسال"}
          </button>
        </div>
      </div>
    </div>
  );
}
