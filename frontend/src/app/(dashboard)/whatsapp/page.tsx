"use client";
import { useState } from "react";
import { MessageSquare, Send, RefreshCw, Clock, CheckCircle, XCircle, AlertCircle, Settings, ChevronDown, ChevronUp, Phone } from "lucide-react";
import { useWhatsAppDashboard, useSendPendingReminders, useRetryWhatsAppMessage, useWhatsAppTemplates, useUpdateWhatsAppTemplate } from "@/hooks/useWhatsApp";
import { cn, formatArabicDate } from "@/lib/utils";
import type { WhatsAppTemplate } from "@/types/whatsapp";

const STATUS_MAP: Record<string, { label: string; color: string; icon: React.ComponentType<{ className?: string }> }> = {
  pending: { label: "قيد الانتظار", color: "bg-yellow-100 text-yellow-700", icon: Clock },
  sent: { label: "تم الإرسال", color: "bg-blue-100 text-blue-700", icon: Send },
  delivered: { label: "تم التسليم", color: "bg-green-100 text-green-700", icon: CheckCircle },
  read: { label: "تمت القراءة", color: "bg-teal-100 text-teal-700", icon: CheckCircle },
  failed: { label: "فشل", color: "bg-red-100 text-red-700", icon: XCircle },
};

export default function WhatsAppPage() {
  const { data: dashboard, isLoading } = useWhatsAppDashboard();
  const sendPending = useSendPendingReminders();
  const retryMutation = useRetryWhatsAppMessage();
  const [showTemplates, setShowTemplates] = useState(false);

  return (
    <div className="space-y-5 max-w-5xl">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-extrabold text-gray-900 flex items-center gap-2">
            <MessageSquare className="w-6 h-6 text-green-600" />
            تكامل واتساب
          </h1>
          <p className="text-sm text-gray-500 mt-0.5">إدارة الرسائل والتذكيرات عبر واتساب</p>
        </div>
        <div className="flex gap-2">
          <button
            onClick={() => setShowTemplates(!showTemplates)}
            className="flex items-center gap-1.5 px-3 py-2 text-sm font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
          >
            <Settings className="w-4 h-4" />
            القوالب
            {showTemplates ? <ChevronUp className="w-3 h-3" /> : <ChevronDown className="w-3 h-3" />}
          </button>
          <button
            onClick={() => sendPending.mutate()}
            disabled={sendPending.isPending}
            className="flex items-center gap-1.5 px-4 py-2 text-sm font-medium rounded-lg bg-green-600 text-white hover:bg-green-700 disabled:opacity-60 transition"
          >
            <Send className="w-4 h-4" />
            {sendPending.isPending ? "جارٍ الإرسال..." : "إرسال التذكيرات"}
          </button>
        </div>
      </div>

      {sendPending.isSuccess && (
        <div className="bg-green-50 border border-green-200 text-green-700 rounded-lg p-3 text-sm">
          {sendPending.data.message}
        </div>
      )}

      {/* Stats Cards */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
        {[
          { label: "أُرسلت اليوم", value: dashboard?.sentToday ?? 0, color: "text-blue-700 bg-blue-50" },
          { label: "تم تسليمها", value: dashboard?.deliveredToday ?? 0, color: "text-green-700 bg-green-50" },
          { label: "فشلت", value: dashboard?.failedToday ?? 0, color: "text-red-700 bg-red-50" },
          { label: "قيد الانتظار", value: dashboard?.pendingCount ?? 0, color: "text-yellow-700 bg-yellow-50" },
        ].map((stat) => (
          <div key={stat.label} className={cn("rounded-xl p-4 text-center", stat.color)}>
            <p className="text-2xl font-bold">{stat.value}</p>
            <p className="text-xs mt-1 opacity-80">{stat.label}</p>
          </div>
        ))}
      </div>

      {/* Templates Section */}
      {showTemplates && <TemplatesSection />}

      {/* Recent Messages */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
          <h2 className="font-bold text-gray-900">آخر الرسائل</h2>
          <span className="text-xs text-gray-400">{dashboard?.recentMessages?.length ?? 0} رسالة</span>
        </div>

        {isLoading ? (
          <div className="p-5 space-y-2 animate-pulse">
            {Array.from({ length: 5 }).map((_, i) => <div key={i} className="h-16 bg-gray-100 rounded-lg" />)}
          </div>
        ) : !dashboard?.recentMessages || dashboard.recentMessages.length === 0 ? (
          <div className="text-center py-16 text-gray-400">
            <MessageSquare className="w-12 h-12 mx-auto mb-3 opacity-30" />
            <p className="text-sm">لا توجد رسائل بعد</p>
          </div>
        ) : (
          <div className="divide-y divide-gray-100">
            {dashboard.recentMessages.map((msg) => {
              const statusInfo = STATUS_MAP[msg.status] || STATUS_MAP.pending;
              const StatusIcon = statusInfo.icon;
              return (
                <div key={msg.id} className="px-5 py-3 hover:bg-gray-50 transition">
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 mb-1">
                        <Phone className="w-3 h-3 text-gray-400 flex-shrink-0" />
                        <span className="text-sm font-medium text-gray-900 truncate">{msg.patientName}</span>
                        <span className="text-xs text-gray-400 font-mono" dir="ltr">{msg.phoneNumber}</span>
                      </div>
                      <p className="text-xs text-gray-500 line-clamp-2">{msg.messageContent}</p>
                      {msg.errorMessage && (
                        <p className="text-xs text-red-500 mt-1 flex items-center gap-1">
                          <AlertCircle className="w-3 h-3" /> {msg.errorMessage}
                        </p>
                      )}
                    </div>
                    <div className="flex flex-col items-end gap-1 flex-shrink-0">
                      <span className={cn("text-[10px] px-2 py-0.5 rounded-full font-medium flex items-center gap-1", statusInfo.color)}>
                        <StatusIcon className="w-2.5 h-2.5" /> {statusInfo.label}
                      </span>
                      <span className="text-[10px] text-gray-400">{formatArabicDate(msg.createdAt)}</span>
                      {msg.status === "failed" && msg.retryCount < 3 && (
                        <button
                          onClick={() => retryMutation.mutate(msg.id)}
                          className="text-[10px] text-blue-600 hover:underline flex items-center gap-0.5"
                        >
                          <RefreshCw className="w-2.5 h-2.5" /> إعادة
                        </button>
                      )}
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}

// ── Templates Section ──────────────────────────────────────────────────────
function TemplatesSection() {
  const { data: templates, isLoading } = useWhatsAppTemplates();
  const updateMutation = useUpdateWhatsAppTemplate();
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editContent, setEditContent] = useState("");

  if (isLoading) return <div className="bg-white rounded-xl p-5 animate-pulse h-40" />;

  const handleEdit = (t: WhatsAppTemplate) => {
    setEditingId(t.id);
    setEditContent(t.contentTemplate);
  };

  const handleSave = (t: WhatsAppTemplate) => {
    updateMutation.mutate({ id: t.id, contentTemplate: editContent, isActive: t.isActive }, {
      onSuccess: () => setEditingId(null),
    });
  };

  const handleToggleActive = (t: WhatsAppTemplate) => {
    updateMutation.mutate({ id: t.id, contentTemplate: t.contentTemplate, isActive: !t.isActive });
  };

  return (
    <div className="bg-white rounded-xl border border-gray-200 shadow-sm">
      <div className="px-5 py-4 border-b border-gray-100">
        <h2 className="font-bold text-gray-900">قوالب الرسائل</h2>
        <p className="text-xs text-gray-400 mt-0.5">استخدم {"{{placeholder}}"} للمتغيرات</p>
      </div>
      <div className="divide-y divide-gray-100">
        {templates?.map((t) => (
          <div key={t.id} className="px-5 py-4">
            <div className="flex items-center justify-between mb-2">
              <div>
                <p className="text-sm font-bold text-gray-900">{t.nameAr}</p>
                <p className="text-[10px] text-gray-400 font-mono">{t.templateKey} · {t.category}</p>
              </div>
              <div className="flex items-center gap-2">
                <button
                  onClick={() => handleToggleActive(t)}
                  className={cn(
                    "text-xs px-2 py-0.5 rounded-full transition",
                    t.isActive ? "bg-green-100 text-green-700" : "bg-gray-100 text-gray-500"
                  )}
                >
                  {t.isActive ? "مفعّل" : "معطّل"}
                </button>
                {editingId !== t.id && (
                  <button onClick={() => handleEdit(t)}
                    className="text-xs text-blue-600 hover:underline">تعديل</button>
                )}
              </div>
            </div>
            {editingId === t.id ? (
              <div className="space-y-2">
                <textarea
                  value={editContent}
                  onChange={(e) => setEditContent(e.target.value)}
                  rows={5}
                  className="w-full px-3 py-2 text-sm rounded-lg border border-gray-300 focus:ring-2 focus:ring-green-500 focus:outline-none resize-none font-mono"
                  dir="rtl"
                />
                <div className="flex gap-2 justify-end">
                  <button onClick={() => setEditingId(null)}
                    className="px-3 py-1 text-xs rounded-lg border border-gray-300 text-gray-600 hover:bg-gray-50">إلغاء</button>
                  <button onClick={() => handleSave(t)} disabled={updateMutation.isPending}
                    className="px-3 py-1 text-xs rounded-lg bg-green-600 text-white hover:bg-green-700 disabled:opacity-60">حفظ</button>
                </div>
              </div>
            ) : (
              <pre className="text-xs text-gray-600 bg-gray-50 rounded-lg p-3 whitespace-pre-wrap font-sans" dir="rtl">
                {t.contentTemplate}
              </pre>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
