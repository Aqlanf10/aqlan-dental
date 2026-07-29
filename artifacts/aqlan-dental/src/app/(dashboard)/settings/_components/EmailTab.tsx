// Sprint 11A — extracted from the former monolithic settings/page.tsx.
// Behavior unchanged: same UI, same API calls, same state management.

import { useState } from "react";
import {
  Mail, MailWarning, Clock, Send, XCircle, Globe,
} from "lucide-react";
import { cn } from "@/lib/utils";
import {
  useEmailStats,
  useEmailHistory,
} from "@/hooks/useUsers";

// ─── Email Tab ────────────────────────────────────────────────────────────────
export function EmailTab() {
  const { data: stats, isLoading } = useEmailStats();
  const [categoryFilter, setCategoryFilter] = useState<string>("all");
  const { data: history } = useEmailHistory(
    undefined,
    undefined,
    categoryFilter === "all" ? undefined : categoryFilter,
  );

  const CATEGORY_LABELS: Record<string, string> = {
    password_reset: "استعادة كلمة المرور",
    appointment_reminder: "تذكير موعد",
    general: "عام",
  };

  if (isLoading) {
    return (
      <div className="animate-pulse space-y-4">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-24 bg-gray-100 rounded-lg" />
        ))}
      </div>
    );
  }

  const limit = stats?.limit;
  const today = stats?.today;
  const week = stats?.week;

  return (
    <div className="space-y-6">
      {/* Daily Limit Alert */}
      {limit?.isAtLimit && (
        <div className="flex items-center gap-3 px-4 py-3 rounded-lg bg-red-50 border border-red-200 text-red-800">
          <MailWarning className="w-5 h-5 flex-shrink-0" />
          <div>
            <p className="font-medium">تم بلوغ الحد اليومي لإرسال البريد!</p>
            <p className="text-sm text-red-700">
              تم إرسال {limit.used} من {limit.dailyLimit} رسالة اليوم. لن يتم إرسال المزيد حتى الغد.
              يرجى ترقية خطة Resend أو التحقق من النطاق.
            </p>
          </div>
        </div>
      )}
      {limit?.isNearLimit && !limit.isAtLimit && (
        <div className="flex items-center gap-3 px-4 py-3 rounded-lg bg-amber-50 border border-amber-200 text-amber-800">
          <MailWarning className="w-5 h-5 flex-shrink-0" />
          <div>
            <p className="font-medium">قرب الحد اليومي للبريد</p>
            <p className="text-sm text-amber-700">
              متبقي {limit.remaining} رسالة فقط من أصل {limit.dailyLimit} اليوم. فكر في ترقية خطة Resend.
            </p>
          </div>
        </div>
      )}

      {/* Stats Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div className="bg-white rounded-xl border border-gray-200 p-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-green-50 flex items-center justify-center">
              <Send className="w-5 h-5 text-green-600" />
            </div>
            <div>
              <p className="text-xs text-gray-500">مرسلة اليوم</p>
              <p className="text-xl font-bold text-gray-900">{today?.sent ?? 0}</p>
            </div>
          </div>
        </div>
        <div className="bg-white rounded-xl border border-gray-200 p-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-red-50 flex items-center justify-center">
              <XCircle className="w-5 h-5 text-red-600" />
            </div>
            <div>
              <p className="text-xs text-gray-500">فاشلة اليوم</p>
              <p className="text-xl font-bold text-gray-900">{today?.failed ?? 0}</p>
            </div>
          </div>
        </div>
        <div className="bg-white rounded-xl border border-gray-200 p-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-blue-50 flex items-center justify-center">
              <Clock className="w-5 h-5 text-blue-600" />
            </div>
            <div>
              <p className="text-xs text-gray-500">هذا الأسبوع</p>
              <p className="text-xl font-bold text-gray-900">{week?.sent ?? 0}</p>
            </div>
          </div>
        </div>
        <div className="bg-white rounded-xl border border-gray-200 p-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-purple-50 flex items-center justify-center">
              <Mail className="w-5 h-5 text-purple-600" />
            </div>
            <div>
              <p className="text-xs text-gray-500">الحد اليومي</p>
              <p className="text-xl font-bold text-gray-900">
                {limit?.used ?? 0}<span className="text-sm text-gray-400 font-normal">/{limit?.dailyLimit ?? 100}</span>
              </p>
            </div>
          </div>
          {/* Progress bar */}
          <div className="mt-2 w-full bg-gray-100 rounded-full h-1.5">
            <div
              className={cn(
                "h-1.5 rounded-full transition-all",
                (limit?.percentage ?? 0) >= 90 ? "bg-red-500" : (limit?.percentage ?? 0) >= 70 ? "bg-amber-500" : "bg-green-500"
              )}
              style={{ width: `${Math.min(limit?.percentage ?? 0, 100)}%` }}
            />
          </div>
        </div>
      </div>

      {/* Resend Domain Setup Guide */}
      <div className="bg-blue-50 border border-blue-200 rounded-xl p-5">
        <h3 className="font-bold text-blue-900 flex items-center gap-2 mb-3">
          <Globe className="w-5 h-5" />
          إعداد نطاق Resend (لإرسال بريد للمرضى)
        </h3>
        <p className="text-sm text-blue-800 mb-3">
          حالياً Resend يرسل رسائل فقط لحسابك المسجل (aqlanf10@gmail.com) لأن النطاق غير مفعل.
          لإرسال بريد للمرضى، تحتاج إضافة نطاقك الخاص في Resend:
        </p>
        <ol className="text-sm text-blue-800 space-y-2 list-decimal list-inside">
          <li>ادخل على <a href="https://resend.com/domains" target="_blank" rel="noopener noreferrer" className="underline font-medium">resend.com/domains</a></li>
          <li>أضف نطاقك (مثلاً aqlandental.com)</li>
          <li>أضف سجلات DNS المطلوبة (SPF, DKIM, DMARC) في لوحة تحكم النطاق</li>
          <li>بعد التحقق، غيّر <code className="bg-blue-100 px-1 rounded">SMTP_FROM_EMAIL</code> إلى <code className="bg-blue-100 px-1 rounded">noreply@aqlandental.com</code></li>
          <li>بعد التحقق، يمكنك إرسال بريد لأي عنوان</li>
        </ol>
        <p className="text-xs text-blue-600 mt-3">
          بدون هذه الخطوة، الإرسال مقتصر على بريدك فقط (حساب Resend المجاني).
        </p>
      </div>

      {/* Recent Emails */}
      <div className="space-y-3">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-bold text-gray-800 flex items-center gap-2">
            <Mail className="w-4 h-4 text-clinic-blue" />
            آخر الرسائل
          </h3>
          <select
            value={categoryFilter}
            onChange={(e) => setCategoryFilter(e.target.value)}
            className="px-3 py-1.5 text-xs rounded-lg border border-gray-300 bg-white"
          >
            <option value="all">جميع الأنواع</option>
            <option value="password_reset">استعادة كلمة المرور</option>
            <option value="appointment_reminder">تذكير موعد</option>
            <option value="general">عام</option>
          </select>
        </div>

        <div className="overflow-x-auto rounded-lg border border-gray-200">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                {["الموضوع", "البريد", "النوع", "المزود", "الحالة", "التاريخ"].map((h) => (
                  <th key={h} className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap">{h}</th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {(history?.emails ?? stats?.recentEmails ?? []).length === 0 ? (
                <tr>
                  <td colSpan={6} className="px-4 py-8 text-center text-gray-400">
                    لا توجد رسائل مسجلة بعد
                  </td>
                </tr>
              ) : (
                (history?.emails ?? stats?.recentEmails ?? []).map((email) => (
                  <tr key={email.id} className="hover:bg-gray-50 transition">
                    <td className="px-4 py-3 font-medium text-gray-900 max-w-[200px] truncate">{email.subject}</td>
                    <td className="px-4 py-3 text-gray-500 text-xs font-mono" dir="ltr">{email.toEmail}</td>
                    <td className="px-4 py-3">
                      <span className="text-xs bg-blue-50 text-blue-700 px-2 py-0.5 rounded-full font-medium">
                        {CATEGORY_LABELS[email.category] ?? email.category}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-gray-500 text-xs">{email.provider ?? "—"}</td>
                    <td className="px-4 py-3">
                      {email.isSent ? (
                        <span className="text-xs bg-green-50 text-green-700 px-2 py-0.5 rounded-full font-medium">تم الإرسال</span>
                      ) : (
                        <span className="text-xs bg-red-50 text-red-600 px-2 py-0.5 rounded-full font-medium" title={email.errorMessage ?? ""}>
                          فشل
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-gray-500 text-xs">
                      {new Date(email.createdAt).toLocaleDateString("ar-YE", {
                        year: "numeric", month: "short", day: "numeric",
                        hour: "2-digit", minute: "2-digit",
                      })}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
