"use client";
import { useEffect, useState, useMemo } from "react";
import Link from "next/link";
import { FileText, Search, Plus } from "lucide-react";
import type { Invoice } from "@/types/finance";
import api from "@/lib/api";
import { cn, formatYemeniRiyal, formatArabicDate } from "@/lib/utils";

const STATUS_LABELS: Record<string, string> = {
  Draft: "مسودة",
  Issued: "مصدرة",
  Cancelled: "ملغاة",
  Paid: "مدفوعة",
};

const STATUS_COLORS: Record<string, string> = {
  Draft: "bg-blue-50 text-blue-700",
  Issued: "bg-green-50 text-green-700",
  Cancelled: "bg-gray-100 text-gray-500",
  Paid: "bg-emerald-50 text-emerald-700",
};

const STATUS_TABS = ["", "Draft", "Issued", "Cancelled", "Paid"] as const;

export default function InvoicesPage() {
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 20;

  useEffect(() => {
    setLoading(true);
    const params = new URLSearchParams({
      page: page.toString(),
      pageSize: pageSize.toString(),
    });
    if (statusFilter) params.set("status", statusFilter);
    api
      .get<{ invoices: Invoice[]; total: number; page: number; pageSize: number }>(`/api/invoices?${params}`)
      .then((r) => {
        setInvoices(r.data.invoices ?? []);
        setTotalCount(r.data.total ?? 0);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [statusFilter, page]);

  const filtered = useMemo(() => {
    if (!search.trim()) return invoices;
    const term = search.trim().toLowerCase();
    return invoices.filter(
      (inv) =>
        inv.patientName?.toLowerCase().includes(term) ||
        inv.invoiceNumber.toLowerCase().includes(term)
    );
  }, [invoices, search]);

  return (
    <div className="space-y-5 max-w-5xl">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-extrabold text-gray-900">الفواتير</h1>
          <p className="text-sm text-gray-500 mt-0.5">إدارة فواتير المرضى والمسودات</p>
        </div>
        <Link
          href="/finance/invoices/new"
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
        >
          <Plus className="w-4 h-4" />
          فاتورة مسودة جديدة
        </Link>
      </div>

      {/* Search + Status filter */}
      <div className="flex items-center gap-2 flex-wrap">
        <div className="relative min-w-56 flex-1">
          <Search className="w-4 h-4 absolute top-1/2 -translate-y-1/2 end-3 text-gray-400" />
          <input
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="بحث باسم المريض أو رقم الفاتورة..."
            className="w-full h-9 pe-9 ps-3 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue"
          />
        </div>
        {STATUS_TABS.map((s) => (
          <button
            key={s}
            onClick={() => { setStatusFilter(s); setPage(1); }}
            className={cn(
              "px-3 py-1.5 text-sm rounded-lg border transition font-medium whitespace-nowrap",
              statusFilter === s
                ? "bg-clinic-blue text-white border-clinic-blue"
                : "border-gray-200 text-gray-600 hover:bg-gray-50"
            )}
          >
            {s === "" ? "الكل" : STATUS_LABELS[s]}
          </button>
        ))}
      </div>

      {loading ? (
        <div className="space-y-2 animate-pulse">
          {Array.from({ length: 5 }).map((_, i) => (
            <div key={i} className="h-16 bg-gray-100 rounded-xl" />
          ))}
        </div>
      ) : filtered.length === 0 ? (
        <div className="text-center py-20 text-gray-400">
          <FileText className="w-12 h-12 mx-auto mb-3 opacity-30" />
          <p className="text-sm">
            {search ? "لا توجد نتائج مطابقة" : "لا توجد فواتير"}
          </p>
        </div>
      ) : (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-200">
                <tr>
                  {[
                    "رقم الفاتورة",
                    "المريض",
                    "الحالة",
                    "الإجمالي",
                    "عدد البنود",
                    "التاريخ",
                    "إجراءات",
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
                {filtered.map((inv) => (
                  <tr
                    key={inv.id}
                    className="hover:bg-gray-50 transition cursor-pointer"
                    onClick={() =>
                      (window.location.href = `/finance/invoices/${inv.id}`)
                    }
                  >
                    <td className="px-4 py-3 font-mono text-xs text-gray-700">
                      {inv.invoiceNumber}
                    </td>
                    <td className="px-4 py-3">
                      <span className="font-medium text-gray-900">
                        {inv.patientName ?? "—"}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <span
                        className={cn(
                          "text-xs px-2 py-0.5 rounded-full font-medium",
                          STATUS_COLORS[inv.status] ??
                            "bg-gray-100 text-gray-600"
                        )}
                      >
                        {inv.statusArabic ?? STATUS_LABELS[inv.status] ?? inv.status}
                      </span>
                    </td>
                    <td className="px-4 py-3 font-mono font-semibold text-gray-900">
                      {formatYemeniRiyal(inv.totalAmount)}
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {inv.lineItemCount ?? "—"}
                    </td>
                    <td className="px-4 py-3 text-gray-600 text-xs">
                      {formatArabicDate(inv.createdAt)}
                    </td>
                    <td className="px-4 py-3">
                      <Link
                        href={`/finance/invoices/${inv.id}`}
                        className="text-xs text-clinic-blue hover:underline"
                        onClick={(e) => e.stopPropagation()}
                      >
                        عرض
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {filtered.length > 0 && (
            <div className="px-4 py-2 border-t border-gray-100 bg-gray-50 text-xs text-gray-500 flex items-center justify-between">
              <span>{totalCount} فاتورة</span>
              {totalCount > pageSize && (
                <div className="flex items-center gap-2">
                  <button
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                    disabled={page === 1}
                    className="px-2 py-1 rounded border border-gray-200 hover:bg-gray-100 disabled:opacity-40"
                  >
                    السابق
                  </button>
                  <span>
                    {page} / {Math.ceil(totalCount / pageSize)}
                  </span>
                  <button
                    onClick={() => setPage((p) => p + 1)}
                    disabled={page >= Math.ceil(totalCount / pageSize)}
                    className="px-2 py-1 rounded border border-gray-200 hover:bg-gray-100 disabled:opacity-40"
                  >
                    التالي
                  </button>
                </div>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
