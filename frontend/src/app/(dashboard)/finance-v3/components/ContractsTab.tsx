"use client";

import { useState, useEffect, useCallback } from "react";
import {
  HandCoins,
  RefreshCw,
  AlertTriangle,
} from "lucide-react";
import { api } from "@/lib/api";
import { toast } from "@/stores/toastStore";
import type { ContractListItem } from "./types";
import { SectionHeader, LoadingSkeleton, EmptyState, DataTable, StatusBadge, tokens, inputStyle } from "./FinanceSharedUI";
import { formatYER } from "./FinanceHelpers";

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 5: Contracts
   ═══════════════════════════════════════════════════════════════════════════════ */
export function ContractsTab() {
  const [data, setData] = useState<ContractListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("");

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      const params: Record<string, string> = {};
      if (statusFilter) params.status = statusFilter;
      const { data } = await api.get<ContractListItem[]>("/api/contracts", { params });
      setData(data);
    } catch { toast.error("فشل في تحميل العقود"); } finally { setLoading(false); }
  }, [statusFilter]);

  useEffect(() => { fetchData(); }, [fetchData]);

  const filtered = data.filter((c) =>
    c.PatientName.includes(search) || c.ContractNumber.includes(search) || c.PatientNumber.includes(search)
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

      {loading ? <LoadingSkeleton /> : filtered.length === 0 ? <EmptyState icon={HandCoins} message="لا توجد عقود" /> : (
        <DataTable<ContractListItem>
          keyField="Id"
          data={filtered}
          columns={[
            { key: "ContractNumber", label: "رقم العقد" },
            { key: "PatientName", label: "المريض" },
            { key: "TotalAmount", label: "الإجمالي", render: (r) => formatYER(r.TotalAmount) },
            { key: "PaidAmount", label: "المدفوع", render: (r) => formatYER(r.PaidAmount) },
            { key: "OutstandingAmount", label: "المستحق", render: (r) => <span style={{ color: r.OutstandingAmount > 0 ? tokens.dangerBorder : tokens.successBorder, fontWeight: 700 }}>{formatYER(r.OutstandingAmount)}</span> },
            { key: "Status", label: "الحالة", render: (r) => <StatusBadge status={r.Status} /> },
            { key: "IsOverdue", label: "متأخرة", render: (r) => r.IsOverdue ? <AlertTriangle className="w-4 h-4" style={{ color: tokens.dangerBorder }} /> : "—" },
            { key: "StartDate", label: "تاريخ البداية", render: (r) => new Date(r.StartDate).toLocaleDateString("ar-SA") },
          ]}
        />
      )}
    </div>
  );
}
