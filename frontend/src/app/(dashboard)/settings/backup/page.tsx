"use client";

import { useState, useEffect, useCallback } from "react";
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
  Trash2,
  FileSearch,
  Users,
  CalendarDays,
  Banknote,
  Stethoscope,
} from "lucide-react";
import { cn } from "@/lib/utils";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";

interface BackupStatusData {
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
  recordCounts: {
    patients: number;
    appointments: number;
    visits: number;
    payments: number;
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

interface ValidateResult {
  id: string;
  startedAt: string;
  completedAt: string;
  sizeMB: number;
  tables: string[];
  message: string;
}

const STATUS_CONFIG: Record<string, { label: string; color: string; icon: React.ReactNode }> = {
  Completed: { label: "مكتمل", color: "bg-emerald-100 text-emerald-700", icon: <CheckCircle className="w-4 h-4" /> },
  Failed: { label: "فشل", color: "bg-red-100 text-red-700", icon: <XCircle className="w-4 h-4" /> },
  InProgress: { label: "جارٍ التنفيذ", color: "bg-sky-100 text-sky-700", icon: <Loader2 className="w-4 h-4 animate-spin" /> },
  Pending: { label: "قيد الانتظار", color: "bg-amber-100 text-amber-700", icon: <Clock className="w-4 h-4" /> },
};

export default function BackupPage() {
  const [status, setStatus] = useState<BackupStatusData | null>(null);
  const [history, setHistory] = useState<BackupHistoryItem[]>([]);
  const [totalHistory, setTotalHistory] = useState(0);
  const [totalPages, setTotalPages] = useState(1);
  const [page, setPage] = useState(1);
  const [typeFilter, setTypeFilter] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [isLoading, setIsLoading] = useState(true);
  const [isBackingUp, setIsBackingUp] = useState(false);
  const [validating, setValidating] = useState<string | null>(null);
  const [validateResult, setValidateResult] = useState<ValidateResult | null>(null);
  const [deleting, setDeleting] = useState<string | null>(null);

  const fetchStatus = useCallback(async () => {
    try {
      const { data } = await api.get("/api/backup/status");
      setStatus(data);
    } catch {
      toast.error("حدث خطأ أثناء تحميل حالة النسخ الاحتياطي");
    }
  }, []);

  const fetchHistory = useCallback(async () => {
    try {
      const params = new URLSearchParams();
      params.set("page", String(page));
      params.set("pageSize", "10");
      if (typeFilter) params.set("type", typeFilter);
      if (statusFilter) params.set("status", statusFilter);
      const { data } = await api.get(`/api/backup/history?${params.toString()}`);
      setHistory(data.data || []);
      setTotalHistory(data.total || 0);
      setTotalPages(data.totalPages || 1);
    } catch {
      toast.error("حدث خطأ أثناء تحميل السجل");
    } finally {
      setIsLoading(false);
    }
  }, [page, typeFilter, statusFilter]);

  useEffect(() => {
    fetchStatus();
    fetchHistory();
  }, [fetchStatus, fetchHistory]);

  const handleBackupDatabase = async () => {
    setIsBackingUp(true);
    try {
      const { data } = await api.post("/api/backup/database");
      toast.success(data.message || "تم إنشاء النسخة الاحتياطية بنجاح");
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

  const handleDownload = async (id: string) => {
    try {
      const response = await api.get(`/api/backup/download/${id}`, { responseType: "blob" });
      const url = window.URL.createObjectURL(new Blob([response.data]));
      const link = document.createElement("a");
      link.href = url;
      link.setAttribute("download", `backup_${id}.json`);
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.URL.revokeObjectURL(url);
      toast.success("تم بدء تحميل النسخة الاحتياطية");
    } catch {
      toast.error("فشل تحميل النسخة الاحتياطية");
    }
  };

  const handleValidate = async (id: string) => {
    setValidating(id);
    try {
      const { data } = await api.post(`/api/backup/validate/${id}`);
      setValidateResult(data);
    } catch (err: unknown) {
      const msg = err && typeof err === "object" && "response" in err
        ? ((err as { response?: { data?: { message?: string } } }).response?.data?.message ?? "حدث خطأ")
        : "حدث خطأ";
      toast.error(msg);
    } finally {
      setValidating(null);
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm("هل أنت متأكد من حذف هذه النسخة الاحتياطية؟")) return;
    setDeleting(id);
    try {
      await api.delete(`/api/backup/${id}`);
      toast.success("تم حذف النسخة الاحتياطية");
      fetchStatus();
      fetchHistory();
    } catch {
      toast.error("فشل حذف النسخة الاحتياطية");
    } finally {
      setDeleting(null);
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
          {isBackingUp ? "جارٍ الإنشاء..." : "إنشاء نسخة احتياطية"}
        </button>
      </div>

      {/* Status Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
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
                <span className="text-gray-400">النوع:</span> {status.lastBackup.type === "Database" ? "قاعدة البيانات" : status.lastBackup.type === "Files" ? "الملفات" : "كامل"}
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

        {/* Record Counts */}
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-6">
          <div className="flex items-center gap-3 mb-4">
            <div className="w-10 h-10 rounded-xl bg-sky-100 flex items-center justify-center">
              <Users className="w-5 h-5 text-sky-600" />
            </div>
            <h3 className="font-bold text-[#0d2137]">إحصائيات السجلات</h3>
          </div>
          <div className="space-y-2">
            <p className="text-sm text-gray-600 flex items-center gap-2">
              <Users className="w-3.5 h-3.5 text-gray-400" />
              <span className="text-gray-400">المرضى:</span>
              <span className="font-semibold text-[#0d2137]">{status?.recordCounts?.patients ?? 0}</span>
            </p>
            <p className="text-sm text-gray-600 flex items-center gap-2">
              <CalendarDays className="w-3.5 h-3.5 text-gray-400" />
              <span className="text-gray-400">المواعيد:</span>
              <span className="font-semibold text-[#0d2137]">{status?.recordCounts?.appointments ?? 0}</span>
            </p>
            <p className="text-sm text-gray-600 flex items-center gap-2">
              <Stethoscope className="w-3.5 h-3.5 text-gray-400" />
              <span className="text-gray-400">الزيارات:</span>
              <span className="font-semibold text-[#0d2137]">{status?.recordCounts?.visits ?? 0}</span>
            </p>
            <p className="text-sm text-gray-600 flex items-center gap-2">
              <Banknote className="w-3.5 h-3.5 text-gray-400" />
              <span className="text-gray-400">المدفوعات:</span>
              <span className="font-semibold text-[#0d2137]">{status?.recordCounts?.payments ?? 0}</span>
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

      {/* Railway Backup Info + Auto Backup Info */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div className="bg-amber-50 border border-amber-200 rounded-2xl p-5">
          <div className="flex items-start gap-3">
            <AlertTriangle className="w-5 h-5 text-amber-600 flex-shrink-0 mt-0.5" />
            <div>
              <h3 className="font-bold text-amber-900 mb-1">النسخ الاحتياطي التلقائي من Railway</h3>
              <p className="text-sm text-amber-700 leading-relaxed">
                يُنصح بتفعيل خاصية النسخ الاحتياطي التلقائي من لوحة تحكم Railway. اذهب إلى إعدادات PostgreSQL وفعّل &quot;Continuous Backups&quot;.
              </p>
            </div>
          </div>
        </div>
        <div className="bg-emerald-50 border border-emerald-200 rounded-2xl p-5">
          <div className="flex items-start gap-3">
            <CheckCircle className="w-5 h-5 text-emerald-600 flex-shrink-0 mt-0.5" />
            <div>
              <h3 className="font-bold text-emerald-900 mb-1">النسخ التلقائي اليومي</h3>
              <p className="text-sm text-emerald-700 leading-relaxed">
                يقوم النظام تلقائياً بإنشاء نسخة احتياطية يومياً في الساعة 5:00 صباحاً بتوقيت اليمن. يتم الاحتفاظ بآخر 7 نسخ فقط.
              </p>
            </div>
          </div>
        </div>
      </div>

      {/* Backup History */}
      <div>
        <div className="flex items-center justify-between mb-4 flex-wrap gap-3">
          <h2 className="text-lg font-bold text-[#0d2137]">سجل النسخ الاحتياطي</h2>
          <div className="flex items-center gap-3">
            <select value={typeFilter} onChange={(e) => { setTypeFilter(e.target.value); setPage(1); }}
              className="border border-gray-200 rounded-xl px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 transition">
              <option value="">جميع الأنواع</option>
              <option value="Database">قاعدة البيانات</option>
              <option value="Files">الملفات</option>
              <option value="Full">كامل</option>
            </select>
            <select value={statusFilter} onChange={(e) => { setStatusFilter(e.target.value); setPage(1); }}
              className="border border-gray-200 rounded-xl px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 transition">
              <option value="">جميع الحالات</option>
              <option value="Completed">مكتمل</option>
              <option value="Failed">فشل</option>
              <option value="InProgress">جارٍ التنفيذ</option>
            </select>
            <button onClick={() => { fetchStatus(); fetchHistory(); }}
              className="flex items-center gap-1 text-sm text-[#1a3a5c] hover:text-[#f5922e] transition">
              <RefreshCw className="w-4 h-4" /> تحديث
            </button>
          </div>
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
                    <th className="text-center px-4 py-3 font-semibold text-gray-600">إجراءات</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {history.map((item) => {
                    const cfg = STATUS_CONFIG[item.status] ?? { label: item.status, color: "bg-gray-100 text-gray-700", icon: null };
                    const isCompleted = item.status === "Completed";
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
                        <td className="px-4 py-3">
                          <div className="flex items-center justify-center gap-1">
                            {isCompleted && (
                              <>
                                <button onClick={() => handleDownload(item.id)}
                                  className="w-7 h-7 rounded-lg flex items-center justify-center text-[#1a3a5c] hover:bg-[#1a3a5c]/10 transition" title="تحميل">
                                  <Download className="w-4 h-4" />
                                </button>
                                <button onClick={() => handleValidate(item.id)} disabled={validating === item.id}
                                  className="w-7 h-7 rounded-lg flex items-center justify-center text-emerald-600 hover:bg-emerald-50 transition" title="التحقق">
                                  {validating === item.id ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <FileSearch className="w-4 h-4" />}
                                </button>
                              </>
                            )}
                            <button onClick={() => handleDelete(item.id)} disabled={deleting === item.id}
                              className="w-7 h-7 rounded-lg flex items-center justify-center text-red-500 hover:bg-red-50 transition" title="حذف">
                              {deleting === item.id ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Trash2 className="w-4 h-4" />}
                            </button>
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
            {totalPages > 1 && (
              <div className="flex items-center justify-between px-4 py-3 border-t border-gray-100 bg-gray-50/40">
                <p className="text-xs text-gray-500">عرض {history.length} من {totalHistory}</p>
                <div className="flex items-center gap-2">
                  <button onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page <= 1}
                    className="px-3 py-1 rounded-lg text-sm text-gray-500 hover:bg-gray-200 disabled:opacity-40">السابق</button>
                  <span className="text-sm text-gray-600">{page} / {totalPages}</span>
                  <button onClick={() => setPage((p) => Math.min(totalPages, p + 1))} disabled={page >= totalPages}
                    className="px-3 py-1 rounded-lg text-sm text-gray-500 hover:bg-gray-200 disabled:opacity-40">التالي</button>
                </div>
              </div>
            )}
          </div>
        )}
      </div>

      {/* Validate Result Modal */}
      {validateResult && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={() => setValidateResult(null)} />
          <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-lg p-6">
            <h3 className="text-lg font-bold text-gray-900 mb-4">نتيجة التحقق من النسخة الاحتياطية</h3>
            <div className="space-y-3">
              <div className="flex items-center gap-2 text-emerald-600">
                <CheckCircle className="w-5 h-5" />
                <span className="font-semibold">النسخة الاحتياطية صالحة</span>
              </div>
              <div className="bg-gray-50 rounded-xl p-4">
                <p className="text-sm text-gray-600 mb-2">
                  <span className="text-gray-400">الحجم:</span> {validateResult.sizeMB} ميجابايت
                </p>
                <p className="text-sm text-gray-600 mb-2">
                  <span className="text-gray-400">تاريخ الإنشاء:</span> {formatDate(validateResult.startedAt)}
                </p>
                {validateResult.tables.length > 0 && (
                  <div>
                    <p className="text-sm text-gray-600 mb-1">الجداول:</p>
                    <ul className="text-xs text-gray-500 space-y-1">
                      {validateResult.tables.map((t, i) => (
                        <li key={i} className="font-mono bg-white rounded px-2 py-1 border border-gray-100">{t}</li>
                      ))}
                    </ul>
                  </div>
                )}
              </div>
              <p className="text-xs text-amber-600 bg-amber-50 rounded-lg px-3 py-2">
                {validateResult.message}
              </p>
            </div>
            <div className="mt-4">
              <button onClick={() => setValidateResult(null)}
                className="w-full px-5 py-2.5 rounded-xl text-sm font-medium text-gray-600 hover:bg-gray-100 transition">إغلاق</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
