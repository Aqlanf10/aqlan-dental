"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { ArrowRight, Shield, Search, RefreshCw } from "lucide-react";
import api from "@/lib/api";
import { cn } from "@/lib/utils";

interface AuditEntry {
  id: string;
  action: string;
  resource: string;
  resourceId?: string;
  ipAddress?: string;
  createdAt: string;
  username: string;
  userId?: string;
}

interface AuditResponse {
  data: AuditEntry[];
  total: number;
  page: number;
  pageSize: number;
  resources: string[];
}

const ACTION_COLORS: Record<string, string> = {
  Create: "bg-green-50 text-green-700 border-green-200",
  Update: "bg-blue-50 text-blue-700 border-blue-200",
  Delete: "bg-red-50 text-red-700 border-red-200",
  Login:  "bg-purple-50 text-purple-700 border-purple-200",
  Logout: "bg-gray-50 text-gray-600 border-gray-200",
  View:   "bg-gray-50 text-gray-500 border-gray-200",
};

const ACTION_AR: Record<string, string> = {
  Create: "إنشاء",
  Update: "تحديث",
  Delete: "حذف",
  Login:  "دخول",
  Logout: "خروج",
  View:   "عرض",
};

const RESOURCE_AR: Record<string, string> = {
  Patient:      "مريض",
  Appointment:  "موعد",
  Payment:      "دفعة",
  Contract:     "عقد",
  OrthoCase:    "حالة تقويم",
  CephAnalysis: "تحليل سيفالو",
  User:         "مستخدم",
  Settings:     "إعداد",
  Prescription: "وصفة",
  LabOrder:     "طلب مختبر",
};

function formatDateTime(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleString("ar-YE", {
    year: "numeric", month: "2-digit", day: "2-digit",
    hour: "2-digit", minute: "2-digit",
  });
}

export default function AuditLogPage() {
  const [data,      setData]      = useState<AuditEntry[]>([]);
  const [total,     setTotal]     = useState(0);
  const [resources, setResources] = useState<string[]>([]);
  const [loading,   setLoading]   = useState(true);
  const [page,      setPage]      = useState(1);
  const pageSize = 50;

  // Filters
  const [resource, setResource] = useState("");
  const [action,   setAction]   = useState("");
  const [from,     setFrom]     = useState("");
  const [to,       setTo]       = useState("");

  const load = () => {
    setLoading(true);
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (resource) params.set("resource", resource);
    if (action)   params.set("action",   action);
    if (from)     params.set("from",     from);
    if (to)       params.set("to",       to);

    api.get<AuditResponse>(`/api/audit-logs?${params}`)
      .then(r => {
        setData(r.data.data);
        setTotal(r.data.total);
        setResources(r.data.resources);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  };

  useEffect(() => { load(); }, [page, resource, action, from, to]); // eslint-disable-line react-hooks/exhaustive-deps

  const totalPages = Math.ceil(total / pageSize);

  return (
    <div className="space-y-5 max-w-6xl">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Link href="/settings" className="hover:text-clinic-teal transition">الإعدادات</Link>
        <span>/</span>
        <span className="text-gray-900 font-medium">سجل التدقيق</span>
      </div>

      <div className="flex items-center gap-3">
        <Link href="/settings" className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 transition text-gray-500">
          <ArrowRight className="w-4 h-4" />
        </Link>
        <div>
          <h1 className="text-2xl font-extrabold text-gray-900 flex items-center gap-2">
            <Shield className="w-6 h-6 text-clinic-teal" />
            سجل التدقيق
          </h1>
          <p className="text-sm text-gray-500 mt-0.5">تتبع كل العمليات التي تمت في النظام</p>
        </div>
      </div>

      {/* Filters */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-4 flex items-center gap-3 flex-wrap">
        <div className="relative flex-1 min-w-48">
          <Search className="w-4 h-4 absolute top-1/2 -translate-y-1/2 end-3 text-gray-400" />
          <select
            value={resource}
            onChange={e => { setResource(e.target.value); setPage(1); }}
            className="w-full pe-9 ps-3 h-9 text-sm rounded-lg border border-gray-200 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal"
          >
            <option value="">كل الوحدات</option>
            {resources.map(r => (
              <option key={r} value={r}>{RESOURCE_AR[r] ?? r}</option>
            ))}
          </select>
        </div>

        <select
          value={action}
          onChange={e => { setAction(e.target.value); setPage(1); }}
          className="h-9 px-3 text-sm rounded-lg border border-gray-200 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal"
        >
          <option value="">كل الإجراءات</option>
          {Object.entries(ACTION_AR).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
        </select>

        <input
          type="date" value={from} onChange={e => { setFrom(e.target.value); setPage(1); }}
          className="h-9 px-3 text-sm rounded-lg border border-gray-200 focus:outline-none focus:ring-2 focus:ring-clinic-teal"
        />
        <span className="text-gray-400 text-sm">—</span>
        <input
          type="date" value={to} onChange={e => { setTo(e.target.value); setPage(1); }}
          className="h-9 px-3 text-sm rounded-lg border border-gray-200 focus:outline-none focus:ring-2 focus:ring-clinic-teal"
        />

        <button
          onClick={() => { setResource(""); setAction(""); setFrom(""); setTo(""); setPage(1); }}
          className="h-9 px-3 text-sm rounded-lg border border-gray-200 hover:bg-gray-50 transition flex items-center gap-1.5 text-gray-600"
        >
          <RefreshCw className="w-3.5 h-3.5" />
          إعادة تعيين
        </button>

        <span className="text-xs text-gray-400 ms-auto">{total.toLocaleString("ar")} سجل</span>
      </div>

      {/* Table */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
        {loading ? (
          <div className="p-5 space-y-2 animate-pulse">
            {Array.from({ length: 10 }).map((_, i) => <div key={i} className="h-10 bg-gray-100 rounded-lg" />)}
          </div>
        ) : data.length === 0 ? (
          <div className="text-center py-16 text-gray-400">
            <Shield className="w-10 h-10 mx-auto mb-3 opacity-30" />
            <p className="text-sm">لا توجد سجلات تدقيق</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-100">
                <tr>
                  {["التاريخ والوقت", "المستخدم", "الإجراء", "الوحدة", "المعرّف", "عنوان IP"].map(h => (
                    <th key={h} className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {data.map(entry => (
                  <tr key={entry.id} className="hover:bg-gray-50 transition">
                    <td className="px-4 py-3 text-xs text-gray-500 whitespace-nowrap font-mono">
                      {formatDateTime(entry.createdAt)}
                    </td>
                    <td className="px-4 py-3 font-medium text-gray-900 whitespace-nowrap">
                      {entry.username}
                    </td>
                    <td className="px-4 py-3">
                      <span className={cn(
                        "inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium border",
                        ACTION_COLORS[entry.action] ?? "bg-gray-50 text-gray-600 border-gray-200"
                      )}>
                        {ACTION_AR[entry.action] ?? entry.action}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-gray-700">
                      {RESOURCE_AR[entry.resource] ?? entry.resource}
                    </td>
                    <td className="px-4 py-3 text-xs text-gray-400 font-mono">
                      {entry.resourceId ? entry.resourceId.slice(0, 8) + "…" : "—"}
                    </td>
                    <td className="px-4 py-3 text-xs text-gray-400 font-mono">
                      {entry.ipAddress ?? "—"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between px-4 py-3 border-t border-gray-100">
            <button
              onClick={() => setPage(p => Math.max(1, p - 1))}
              disabled={page === 1}
              className="px-3 py-1.5 text-sm rounded-lg border border-gray-200 hover:bg-gray-50 disabled:opacity-40 transition"
            >
              السابق
            </button>
            <span className="text-sm text-gray-500">
              صفحة {page} من {totalPages}
            </span>
            <button
              onClick={() => setPage(p => Math.min(totalPages, p + 1))}
              disabled={page === totalPages}
              className="px-3 py-1.5 text-sm rounded-lg border border-gray-200 hover:bg-gray-50 disabled:opacity-40 transition"
            >
              التالي
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
