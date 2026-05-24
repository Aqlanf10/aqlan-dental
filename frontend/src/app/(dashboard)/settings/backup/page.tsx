"use client";

import { useState, useEffect } from "react";
import {
  Shield,
  Database,
  HardDrive,
  CheckCircle,
  XCircle,
  Clock,
  Loader2,
  RefreshCw,
  Download,
  AlertTriangle,
} from "lucide-react";
import { cn } from "@/lib/utils";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";

// ─── Types ──────────────────────────────────────────────────────────────

interface BackupStatus {
  lastBackup: {
    startedAt: string;
    completedAt: string;
    type: string;
    sizeMB: number | null;
  } | null;
  totalBackups: number;
  failedBackups: number;
  totalSizeMB: number;
  filesCount: {
    photos: number;
    radiographs: number;
    total: number;
  };
}

interface BackupHistoryItem {
  id: string;
  type: string;
  status: string;
  startedAt: string;
  completedAt: string | null;
  sizeMB: number | null;
  filePath: string | null;
  errorMessage: string | null;
  isAutomatic: boolean;
}

const STATUS_CONFIG: Record<string, { label: string; color: string; icon: React.ReactNode }> = {
  Completed: { label: "مكتمل", color: "bg-emerald-100 text-emerald-700", icon: <CheckCircle className="w-4 h-4" /> },
  Failed: { label: "فشل", color: "bg-red-100 text-red-700", icon: <XCircle className="w-4 h-4" /> },
  InProgress: { label: "جارٍ التنفيذ", color: "bg-sky-100 text-sky-700", icon: <Loader2 className="w-4 h-4 animate-spin" /> },
  Pending: { label: "قيد الانتظار", color: "bg-amber-100 text-amber-700", icon: <Clock className="w-4 h-4" /> },
};

export default function BackupPage() {
  const [status, setStatus] = useState<BackupStatus | null>(null);
  const [history, setHistory] = useState<BackupHistoryItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isBackingUp, setIsBackingUp] = useState(false);

  const fetchStatus = async () => {
    try {
      const { data } = await api.get("/api/backup/status");
      setStatus(data);
    } catch {
      toast.error("حدث خطأ أثناء تحميل حالة النسخ الاحتياطي");
    }
  };

  const fetchHistory = async () => {
    try {
      const { data } = await api.get("/api/backup/history?pageSize=10");
      setHistory(data.data || []);
    } catch {
      toast.error("حدث خطأ أثناء تحميل السجل");
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchStatus();
    fetchHistory();
  }, []);

  const handleBackupDatabase = async () => {
    setIsBackingUp(true);
    try {
      const { data } = await api.post("/api/backup/database");
      toast.success(data.message || "تم فحص قاعدة البيانات بنجاح");
      fetchStatus();
      fetchHistory();
    } catch (err: unknown) {
      const msg = err && typeof err === "object" && "response" in err
        ? ((err as { response?: { data?: { message?: string } } }).response?.data?.message ?? "حدث خطأ")
        : "حدث خطأ";
      toast.error(msg);
    } finally {
      setIsBackingUp(false);
    }
  };

  const formatDate = (dateStr: string) => {
    try { return new Date(dateStr).toLocaleDateString("ar-SA", { year: "numeric", month: "long", day: "numeric", hour: "2-digit", minute: "2-digit" }); } catch { return dateStr; }
  };

  return (
    <div className="space-y-6" dir="rtl">
      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-4">
        <div>
          <h1 className="text-2xl font-bold text-[#0d2137]">النسخ الاحتياطي</h1>
          <p className="text-sm text-gray-500 mt-1">إدارة النسخ الاحتياطي لقاعدة البيانات والملفات</p>
        </div>
        <button onClick={handleBackupDatabase} disabled={isBackingUp}
          className="flex items-center gap-2 bg-[#f5922e] text-white px-4 py-2.5 rounded-xl text-sm font-semibold hover:opacity-90 transition shadow-sm disabled:opacity-50">
          {isBackingUp ? <Loader2 className="w-4 h-4 animate-spin" /> : <Database className="w-4 h-4" />}
          {isBackingUp ? "جارٍ الفحص..." : "فحص قاعدة البيانات"}
        </button>
      </div>

      {/* Status Cards */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        {/* Last Backup */}
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-6">
          <div className="flex items-center gap-3 mb-4">
            <div className="w-10 h-10 rounded-xl bg-[#1a3a5c]/10 flex items-center justify-center">
              <Shield className="w-5 h-5 text-[#1a3a5c]" />
            </div>
            <h3 className="font-bold text-[#0d2137]">آخر نسخة احتياطية</h3>
          </div>
          {status?.lastBackup ? (
            <div className="space-y-2">
              <p className="text-sm text-gray-600">
                <span className="text-gray-400">التاريخ:</span>{" "}
                {formatDate(status.lastBackup.completedAt || status.lastBackup.startedAt)}
              </p>
              <p className="text-sm text-gray-600">
                <span className="text-gray-400">النوع:</span> {status.lastBackup.type === "Database" ? "قاعدة البيانات" : status.lastBackup.type}
              </p>
              {status.lastBackup.sizeMB != null && (
                <p className="text-sm text-gray-600">
                  <span className="text-gray-400">الحجم:</span> {status.lastBackup.sizeMB} ميجابايت
                </p>
              )}
            </div>
          ) : (
            <p className="text-sm text-gray-400">لا يوجد نسخ احتياطي سابق</p>
          )}
        </div>

        {/* Statistics */}
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-6">
          <div className="flex items-center gap-3 mb-4">
            <div className="w-10 h-10 rounded-xl bg-emerald-100 flex items-center justify-center">
              <Database className="w-5 h-5 text-emerald-600" />
            </div>
            <h3 className="font-bold text-[#0d2137]">إحصائيات</h3>
          </div>
          <div className="space-y-2">
            <p className="text-sm text-gray-600">
              <span className="text-gray-400">إجمالي النسخ:</span>{" "}
              <span className="font-semibold text-[#0d2137]">{status?.totalBackups ?? 0}</span>
            </p>
            <p className="text-sm text-gray-600">
              <span className="text-gray-400">فاشلة:</span>{" "}
              <span className={cn("font-semibold", (status?.failedBackups ?? 0) > 0 ? "text-red-600" : "text-emerald-600")}>
                {status?.failedBackups ?? 0}
              </span>
            </p>
            <p className="text-sm text-gray-600">
              <span className="text-gray-400">إجمالي الحجم:</span>{" "}
              <span className="font-semibold text-[#0d2137]" dir="ltr">{status?.totalSizeMB ?? 0} ميجابايت</span>
            </p>
          </div>
        </div>

        {/* Files Count */}
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-6">
          <div className="flex items-center gap-3 mb-4">
            <div className="w-10 h-10 rounded-xl bg-purple-100 flex items-center justify-center">
              <HardDrive className="w-5 h-5 text-purple-600" />
            </div>
            <h3 className="font-bold text-[#0d2137]">الملفات</h3>
          </div>
          <div className="space-y-2">
            <p className="text-sm text-gray-600">
              <span className="text-gray-400">الصور السريرية:</span>{" "}
              <span className="font-semibold text-[#0d2137]">{status?.filesCount?.photos ?? 0}</span>
            </p>
            <p className="text-sm text-gray-600">
              <span className="text-gray-400">الأشعة:</span>{" "}
              <span className="font-semibold text-[#0d2137]">{status?.filesCount?.radiographs ?? 0}</span>
            </p>
            <p className="text-sm text-gray-600">
              <span className="text-gray-400">الإجمالي:</span>{" "}
              <span className="font-bold text-[#0d2137]">{status?.filesCount?.total ?? 0}</span>
            </p>
          </div>
        </div>
      </div>

      {/* Railway Backup Info */}
      <div className="bg-amber-50 border border-amber-200 rounded-2xl p-6">
        <div className="flex items-start gap-3">
          <AlertTriangle className="w-5 h-5 text-amber-600 flex-shrink-0 mt-0.5" />
          <div>
            <h3 className="font-bold text-amber-900 mb-1">تفعيل النسخ الاحتياطي التلقائي</h3>
            <p className="text-sm text-amber-700 leading-relaxed">
              يُنصح بتفعيل خاصية النسخ الاحتياطي التلقائي لقاعدة البيانات من لوحة تحكم Railway.
              يمكنك الوصول إليها من إعدادات خدمة PostgreSQL في Railway وتفعيل &quot;Continuous Backups&quot;.
              هذا يضمن استعادة البيانات في حالة أي مشكلة.
            </p>
          </div>
        </div>
      </div>

      {/* Backup History */}
      <div>
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-bold text-[#0d2137]">سجل النسخ الاحتياطي</h2>
          <button onClick={() => { fetchStatus(); fetchHistory(); }}
            className="flex items-center gap-1 text-sm text-[#1a3a5c] hover:text-[#f5922e] transition">
            <RefreshCw className="w-4 h-4" /> تحديث
          </button>
        </div>

        {isLoading ? (
          <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-6 text-center">
            <Loader2 className="w-6 h-6 text-gray-400 animate-spin mx-auto" />
          </div>
        ) : history.length === 0 ? (
          <div className="text-center py-16 text-gray-400 bg-white rounded-2xl border border-gray-100 shadow-sm">
            <Database className="w-12 h-12 mx-auto mb-3 opacity-40" />
            <p className="font-medium">لا يوجد سجل نسخ احتياطي</p>
          </div>
        ) : (
          <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-gray-100 bg-gray-50/60">
                    <th className="text-right px-4 py-3 font-semibold text-gray-600">النوع</th>
                    <th className="text-right px-4 py-3 font-semibold text-gray-600">الحالة</th>
                    <th className="text-right px-4 py-3 font-semibold text-gray-600">بدأ في</th>
                    <th className="text-right px-4 py-3 font-semibold text-gray-600">انتهى في</th>
                    <th className="text-right px-4 py-3 font-semibold text-gray-600">الحجم</th>
                    <th className="text-right px-4 py-3 font-semibold text-gray-600">تفاصيل</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {history.map((item) => {
                    const cfg = STATUS_CONFIG[item.status] ?? { label: item.status, color: "bg-gray-100 text-gray-700", icon: null };
                    return (
                      <tr key={item.id} className="hover:bg-gray-50/50 transition">
                        <td className="px-4 py-3 font-medium text-[#0d2137]">
                          {item.type === "Database" ? "قاعدة البيانات" : item.type === "Files" ? "الملفات" : "كامل"}
                          {item.isAutomatic && <span className="text-[10px] text-gray-400 mr-1">(تلقائي)</span>}
                        </td>
                        <td className="px-4 py-3">
                          <span className={cn("inline-flex items-center gap-1 text-[11px] font-semibold px-2 py-0.5 rounded-full", cfg.color)}>
                            {cfg.icon} {cfg.label}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-gray-500">{formatDate(item.startedAt)}</td>
                        <td className="px-4 py-3 text-gray-500">{item.completedAt ? formatDate(item.completedAt) : "—"}</td>
                        <td className="px-4 py-3 text-gray-600" dir="ltr">
                          {item.sizeMB != null ? `${item.sizeMB} ميجابايت` : "—"}
                        </td>
                        <td className="px-4 py-3 text-gray-400 text-xs max-w-[200px] truncate">
                          {item.errorMessage ?? item.filePath ?? "—"}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
