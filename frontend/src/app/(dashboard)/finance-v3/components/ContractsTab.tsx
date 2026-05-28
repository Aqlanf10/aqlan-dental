"use client";

import { useState, useEffect, useCallback } from "react";
import {
  HandCoins,
  RefreshCw,
  AlertTriangle,
  CalendarClock,
} from "lucide-react";
import { api } from "@/lib/api";
import { toast } from "@/stores/toastStore";
import type { ContractListItem } from "./types";
import { SectionHeader, LoadingSkeleton, EmptyState, DataTable, StatusBadge, tokens, inputStyle, btnPrimary } from "./FinanceSharedUI";
import { formatYER, safeFormatDate, extractErrorMessage, safeArray } from "./FinanceHelpers";
import CreateInstallmentModal from "./CreateInstallmentModal";

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 5: Contracts — مع زر إنشاء خطة تقسيط
   Zero-State Resiliency: Safe array extraction, null-safe rendering
   ═══════════════════════════════════════════════════════════════════════════════ */
export function ContractsTab() {
  const [data, setData] = useState<ContractListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("");

  // ── نافذة إنشاء خطة تقسيط ──
  const [installmentModal, setInstallmentModal] = useState<{
    contractId: string;
    totalAmount: number;
  } | null>(null);

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const params: Record<string, string> = {};
      if (statusFilter) params.status = statusFilter;
      const { data: responseData } = await api.get<{ data: ContractListItem[]; total: number }>("/api/finance-v3/contracts", { params });
      // API wraps response in { data: [...], total: number }, handle both shapes
      const contracts = safeArray(responseData?.data ?? (Array.isArray(responseData) ? responseData as unknown as ContractListItem[] : undefined));
      setData(contracts);
    } catch (err) {
      setError(extractErrorMessage(err, "فشل في تحميل العقود"));
      toast.error("فشل في تحميل العقود");
    } finally { setLoading(false); }
  }, [statusFilter]);

  useEffect(() => { fetchData(); }, [fetchData]);

  const filtered = data.filter((c) =>
    (c.patientName ?? "").includes(search) || (c.contractNumber ?? "").includes(search) || (c.patientNumber ?? "").includes(search)
  );

  return (
    <div className="p-6 space-y-4">
      <SectionHeader title="العقود" action={
        <div className="flex items-center gap-2">
          <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="بحث..." style={{ ...inputStyle, width: 200, fontSize: 13 }} />
          <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)} style={{ ...inputStyle, width: 140, fontSize: 13 }}>
            <option value="">جميع الحالات</option>
            <option value="Active">نشط</option>
            <option value="Completed">مكتمل</option>
            <option value="Overdue">متأخرة</option>
          </select>
          <button onClick={fetchData} className="w-8 h-8 rounded-md flex items-center justify-center" style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }} title="تحديث"><RefreshCw className="w-4 h-4" /></button>
        </div>
      } />

      {loading ? <LoadingSkeleton /> : error ? (
        <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.dangerBg, borderColor: tokens.dangerBorder }}>
          <p className="text-sm" style={{ color: tokens.dangerText }}>{error}</p>
          <button onClick={fetchData} className="text-xs font-medium mt-2 underline" style={{ color: tokens.brand }}>إعادة المحاولة</button>
        </div>
      ) : filtered.length === 0 ? <EmptyState icon={HandCoins} message="لا توجد عقود" /> : (
        <DataTable<ContractListItem>
          keyField="id"
          data={filtered}
          columns={[
            { key: "contractNumber", label: "رقم العقد" },
            { key: "patientName", label: "المريض" },
            { key: "totalAmount", label: "الإجمالي", render: (r) => formatYER(r.totalAmount ?? 0) },
            { key: "paidAmount", label: "المدفوع", render: (r) => formatYER(r.paidAmount ?? 0) },
            { key: "outstandingAmount", label: "المستحق", render: (r) => <span style={{ color: (r.outstandingAmount ?? 0) > 0 ? tokens.dangerBorder : tokens.successBorder, fontWeight: 700 }}>{formatYER(r.outstandingAmount ?? 0)}</span> },
            { key: "status", label: "الحالة", render: (r) => <StatusBadge status={r.status} /> },
            { key: "isOverdue", label: "متأخرة", render: (r) => r.isOverdue ? <AlertTriangle className="w-4 h-4" style={{ color: tokens.dangerBorder }} /> : "—" },
            { key: "startDate", label: "تاريخ البداية", render: (r) => safeFormatDate(r.startDate ?? "") },
            {
              key: "installment",
              label: "تقسيط",
              render: (r) => r.status === "Active" ? (
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    setInstallmentModal({
                      contractId: r.id,
                      totalAmount: r.totalAmount ?? 0,
                    });
                  }}
                  className="inline-flex items-center gap-1 px-2 py-1 rounded-md text-xs font-medium transition-colors"
                  style={{
                    color: tokens.brand,
                    backgroundColor: tokens.brandLight,
                    border: "none",
                    cursor: "pointer",
                  }}
                  onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = tokens.brand; e.currentTarget.style.color = tokens.textOnBrand; }}
                  onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = tokens.brandLight; e.currentTarget.style.color = tokens.brand; }}
                >
                  <CalendarClock className="w-3 h-3" />
                  تقسيط
                </button>
              ) : "—",
            },
          ]}
        />
      )}

      {/* ── نافذة إنشاء خطة التقسيط ── */}
      {installmentModal && (
        <CreateInstallmentModal
          contractId={installmentModal.contractId}
          totalAmount={installmentModal.totalAmount}
          open={!!installmentModal}
          onClose={() => setInstallmentModal(null)}
          onCreated={fetchData}
        />
      )}
    </div>
  );
}
