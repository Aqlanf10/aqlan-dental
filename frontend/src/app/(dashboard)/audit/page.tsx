"use client";
import { useState, useCallback } from "react";
import {
  FileText, Search, Download, ChevronRight, ChevronLeft,
  Filter, RotateCcw,
} from "lucide-react";
import { useAuditLogs, useExportAuditLogs } from "@/hooks/useSettings";
import { cn } from "@/lib/utils";
import { toast } from "@/stores/toastStore";

// ─── Constants ──────────────────────────────────────────────────────────────────

const ACTION_LABELS: Record<string, string> = {
  Create: "إنشاء",
  Update: "تحديث",
  Delete: "حذف",
  View: "عرض",
  Export: "تصدير",
  Login: "دخول",
  Logout: "خروج",
  Approve: "اعتماد",
};

const ACTION_COLORS: Record<string, string> = {
  Create: "bg-green-100 text-green-700 border-green-200",
  Update: "bg-blue-100 text-blue-700 border-blue-200",
  Delete: "bg-red-100 text-red-700 border-red-200",
  View: "bg-gray-100 text-gray-600 border-gray-200",
  Export: "bg-purple-100 text-purple-700 border-purple-200",
  Login: "bg-teal-100 text-teal-700 border-teal-200",
  Logout: "bg-orange-100 text-orange-700 border-orange-200",
  Approve: "bg-amber-100 text-amber-700 border-amber-200",
};

const RESOURCE_LABELS: Record<string, string> = {
  patients: "المرضى",
  appointments: "المواعيد",
  ortho_cases: "حالات التقويم",
  ortho: "التقويم",
  ceph: "السيفالومتري",
  surgery_cases: "حالات الجراحة",
  surgery: "الجراحة",
  general_treatments: "العلاجات العامة",
  general: "طب الأسنان العام",
  contracts: "العقود",
  payments: "الدفعات",
  finance: "المالية",
  expenses: "المصروفات",
  users: "المستخدمون",
  settings: "الإعدادات",
  branches: "الفروع",
  role_permissions: "صلاحيات الأدوار",
  inventory: "المخزون",
  prescriptions: "الوصفات الطبية",
  lab: "المختبر",
  referrals: "الإحالات",
  reports: "التقارير",
  auth: "المصادقة",
};

const ACTION_OPTIONS = [
  { value: "", label: "جميع الإجراءات" },
  { value: "Create", label: "إنشاء" },
  { value: "Update", label: "تحديث" },
  { value: "Delete", label: "حذف" },
  { value: "View", label: "عرض" },
  { value: "Export", label: "تصدير" },
  { value: "Login", label: "دخول" },
  { value: "Logout", label: "خروج" },
  { value: "Approve", label: "اعتماد" },
];

const inputCls =
  "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal";

// ─── Helper: Beautify resource name ────────────────────────────────────────────
function beautifyResource(resource: string): string {
  return RESOURCE_LABELS[resource] ?? RESOURCE_LABELS[resource.toLowerCase()] ?? resource;
}

// ─── Helper: Format date for display ───────────────────────────────────────────
function formatAuditDate(dateStr: string): string {
  try {
    const d = new Date(dateStr);
    return d.toLocaleDateString("ar-YE", {
      year: "numeric",
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });
  } catch {
    return dateStr;
  }
}

// ─── Component ──────────────────────────────────────────────────────────────────

export default function AuditLogPage() {
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [action, setAction] = useState("");
  const [resource, setResource] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [search, setSearch] = useState("");
  const [appliedFilters, setAppliedFilters] = useState<{
    page: number;
    pageSize: number;
    action?: string;
    resource?: string;
    from?: string;
    to?: string;
    search?: string;
  }>({ page: 1, pageSize: 20 });

  const { data, isLoading } = useAuditLogs(appliedFilters);
  const exportMutation = useExportAuditLogs();

  const applyFilters = useCallback(() => {
    setAppliedFilters({
      page,
      pageSize,
      action: action || undefined,
      resource: resource || undefined,
      from: from || undefined,
      to: to || undefined,
      search: search || undefined,
    });
  }, [page, pageSize, action, resource, from, to, search]);

  const resetFilters = useCallback(() => {
    setPage(1);
    setAction("");
    setResource("");
    setFrom("");
    setTo("");
    setSearch("");
    setAppliedFilters({ page: 1, pageSize: 20 });
  }, []);

  const handlePageChange = useCallback(
    (newPage: number) => {
      setPage(newPage);
      setAppliedFilters((prev) => ({ ...prev, page: newPage }));
    },
    []
  );

  const handleExport = useCallback(async () => {
    try {
      const result = await exportMutation.mutateAsync({
        action: appliedFilters.action,
        resource: appliedFilters.resource,
        userId: undefined,
        from: appliedFilters.from,
        to: appliedFilters.to,
        search: appliedFilters.search,
      });
      // Create downloadable blob
      const blob = new Blob([result as unknown as BlobPart], { type: "text/csv;charset=utf-8;" });
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = `audit-logs-${new Date().toISOString().slice(0, 10)}.csv`;
      link.click();
      URL.revokeObjectURL(url);
      toast.success("تم تصدير السجل بنجاح");
    } catch {
      toast.error("حدث خطأ أثناء التصدير");
    }
  }, [exportMutation, appliedFilters]);

  const logs = data?.data ?? [];
  const totalPages = data?.totalPages ?? 1;
  const totalCount = data?.totalCount ?? 0;

  return (
    <div className="space-y-5 max-w-7xl">
      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div>
          <h1 className="text-2xl font-extrabold text-gray-900 flex items-center gap-2">
            <FileText className="w-6 h-6 text-clinic-teal" />
            سجل التدقيق
          </h1>
          <p className="text-sm text-gray-500 mt-0.5">
            عرض جميع العمليات والتغييرات في النظام
          </p>
        </div>
        <button
          onClick={handleExport}
          disabled={exportMutation.isPending || totalCount === 0}
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-50 transition"
        >
          <Download className="w-4 h-4" />
          {exportMutation.isPending ? "جارٍ التصدير..." : "تصدير CSV"}
        </button>
      </div>

      {/* Filters */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-4">
        <div className="flex items-center gap-2 mb-3">
          <Filter className="w-4 h-4 text-gray-400" />
          <span className="text-sm font-semibold text-gray-700">تصفية</span>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-3">
          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">
              الإجراء
            </label>
            <select
              value={action}
              onChange={(e) => setAction(e.target.value)}
              className={inputCls}
            >
              {ACTION_OPTIONS.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">
              المورد
            </label>
            <input
              value={resource}
              onChange={(e) => setResource(e.target.value)}
              className={inputCls}
              placeholder="patients, users..."
              dir="ltr"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">
              من تاريخ
            </label>
            <input
              type="date"
              value={from}
              onChange={(e) => setFrom(e.target.value)}
              className={inputCls}
              dir="ltr"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">
              إلى تاريخ
            </label>
            <input
              type="date"
              value={to}
              onChange={(e) => setTo(e.target.value)}
              className={inputCls}
              dir="ltr"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">
              بحث
            </label>
            <div className="relative">
              <Search className="absolute right-2.5 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
              <input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                className={cn(inputCls, "pr-8")}
                placeholder="بحث في السجل..."
              />
            </div>
          </div>
        </div>
        <div className="flex items-center gap-2 mt-3">
          <button
            onClick={applyFilters}
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 transition"
          >
            <Search className="w-4 h-4" />
            تطبيق
          </button>
          <button
            onClick={resetFilters}
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg border border-gray-300 text-gray-600 hover:bg-gray-50 transition"
          >
            <RotateCcw className="w-4 h-4" />
            إعادة تعيين
          </button>
          {totalCount > 0 && (
            <span className="text-xs text-gray-400 mr-auto">
              {totalCount} سجل
            </span>
          )}
        </div>
      </div>

      {/* Table */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
        {isLoading ? (
          <div className="p-5 space-y-3 animate-pulse">
            {Array.from({ length: 8 }).map((_, i) => (
              <div key={i} className="h-12 bg-gray-100 rounded-lg" />
            ))}
          </div>
        ) : logs.length === 0 ? (
          <div className="text-center py-16 text-gray-400">
            <FileText className="w-12 h-12 mx-auto mb-3 opacity-30" />
            <p className="text-sm">لا توجد سجلات تطابق التصفية</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-200">
                <tr>
                  {[
                    "التاريخ",
                    "المستخدم",
                    "الإجراء",
                    "المورد",
                    "معرف المورد",
                    "عنوان IP",
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
                {logs.map((log) => (
                  <tr key={log.id} className="hover:bg-gray-50 transition">
                    <td className="px-4 py-3 text-xs text-gray-600 whitespace-nowrap">
                      {formatAuditDate(log.timestamp)}
                    </td>
                    <td className="px-4 py-3 font-medium text-gray-900">
                      {log.userName ?? log.userId}
                    </td>
                    <td className="px-4 py-3">
                      <span
                        className={cn(
                          "inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold border",
                          ACTION_COLORS[log.action] ??
                            "bg-gray-100 text-gray-600 border-gray-200"
                        )}
                      >
                        {ACTION_LABELS[log.action] ?? log.action}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-gray-700">
                      {beautifyResource(log.resource)}
                    </td>
                    <td className="px-4 py-3 font-mono text-xs text-gray-500" dir="ltr">
                      {log.resourceId ?? "—"}
                    </td>
                    <td className="px-4 py-3 font-mono text-xs text-gray-400" dir="ltr">
                      {log.ipAddress ?? "—"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between px-4 py-3 border-t border-gray-100 bg-gray-50">
            <p className="text-xs text-gray-500">
              صفحة {page} من {totalPages}
            </p>
            <div className="flex items-center gap-2">
              <button
                onClick={() => handlePageChange(page - 1)}
                disabled={page <= 1}
                className="flex items-center gap-1 px-3 py-1.5 text-xs font-medium rounded-lg border border-gray-300 text-gray-600 hover:bg-white disabled:opacity-40 disabled:cursor-not-allowed transition"
              >
                <ChevronRight className="w-3.5 h-3.5" />
                السابقة
              </button>
              <button
                onClick={() => handlePageChange(page + 1)}
                disabled={page >= totalPages}
                className="flex items-center gap-1 px-3 py-1.5 text-xs font-medium rounded-lg border border-gray-300 text-gray-600 hover:bg-white disabled:opacity-40 disabled:cursor-not-allowed transition"
              >
                التالية
                <ChevronLeft className="w-3.5 h-3.5" />
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
