"use client";

import { useState, useEffect, useCallback } from "react";
import {
  Wallet,
  RefreshCw,
  AlertTriangle,
  CheckCircle2,
  DollarSign,
} from "lucide-react";
import { api } from "@/lib/api";
import { toast } from "@/stores/toastStore";
import type { PatientBalance, PatientBalanceDetail } from "./types";
import { SectionHeader, LoadingSkeleton, EmptyState, DataTable, Modal, tokens, inputStyle, btnPrimary, btnGhost } from "./FinanceSharedUI";
import { formatYER } from "./FinanceHelpers";

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 2: Patient Accounts
   ═══════════════════════════════════════════════════════════════════════════════ */
export function PatientAccountsTab() {
  const [data, setData] = useState<PatientBalance[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [selected, setSelected] = useState<PatientBalanceDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [showPayment, setShowPayment] = useState(false);

  const fetchData = useCallback(async () => {
    try { setLoading(true); const { data: responseData } = await api.get<{ data: PatientBalance[]; total: number }>("/api/finance-v3/patient-accounts"); setData(responseData.data); } catch { toast.error("فشل في تحميل حسابات المرضى"); } finally { setLoading(false); }
  }, []);

  useEffect(() => { fetchData(); }, [fetchData]);

  const openDetail = async (patient: PatientBalance) => {
    try {
      setDetailLoading(true);
      const { data } = await api.get<PatientBalanceDetail>(`/api/finance-v3/patient-balance/${patient.PatientId}`);
      setSelected(data);
    } catch { toast.error("فشل في تحميل تفاصيل الحساب"); } finally { setDetailLoading(false); }
  };

  const filtered = data.filter((p) =>
    p.PatientName.includes(search) || p.PatientNumber.includes(search)
  );

  return (
    <div className="p-6 space-y-4">
      <SectionHeader title="حسابات المرضى" action={
        <div className="flex items-center gap-2">
          <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="بحث بالاسم أو الرقم..." style={{ ...inputStyle, width: 240, fontSize: 13 }} />
          <button onClick={fetchData} className="w-8 h-8 rounded-md flex items-center justify-center" style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }} title="تحديث"><RefreshCw className="w-4 h-4" /></button>
        </div>
      } />

      {loading ? <LoadingSkeleton /> : filtered.length === 0 ? <EmptyState icon={Wallet} message="لا يوجد حسابات مرضى" /> : (
        <DataTable<PatientBalance>
          keyField="PatientId"
          data={filtered}
          onRowClick={openDetail}
          columns={[
            { key: "PatientNumber", label: "رقم المريض" },
            { key: "PatientName", label: "الاسم" },
            { key: "TotalInvoiced", label: "إجمالي الفواتير", render: (r) => formatYER(r.TotalInvoiced) },
            { key: "TotalPaid", label: "إجمالي المدفوع", render: (r) => formatYER(r.TotalPaid) },
            { key: "Balance", label: "الرصيد", render: (r) => <span style={{ color: r.Balance > 0 ? tokens.dangerBorder : tokens.successBorder, fontWeight: 700 }}>{formatYER(r.Balance)}</span> },
            { key: "HasOutstanding", label: "معلّق", render: (r) => r.HasOutstanding ? <AlertTriangle className="w-4 h-4" style={{ color: tokens.warningBorder }} /> : <CheckCircle2 className="w-4 h-4" style={{ color: tokens.successBorder }} /> },
          ]}
        />
      )}

      {/* Detail modal */}
      <Modal open={!!selected} onClose={() => { setSelected(null); setShowPayment(false); }} title={selected?.PatientName ?? "تفاصيل الحساب"} wide>
        {detailLoading ? <LoadingSkeleton rows={4} /> : selected ? (
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-3">
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>رقم المريض</p><p className="text-sm font-bold" style={{ color: tokens.textPrimary }}>{selected.PatientNumber}</p></div>
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>إجمالي الفواتير</p><p className="text-sm font-bold" style={{ color: tokens.textPrimary }}>{formatYER(selected.TotalInvoiced)}</p></div>
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>إجمالي المدفوع</p><p className="text-sm font-bold" style={{ color: tokens.successBorder }}>{formatYER(selected.NetPaid)}</p></div>
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>المرتجعات</p><p className="text-sm font-bold" style={{ color: tokens.warningText }}>{formatYER(selected.TotalRefunds)}</p></div>
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>الخصومات</p><p className="text-sm font-bold" style={{ color: tokens.brand }}>{formatYER(selected.TotalDiscounts)}</p></div>
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>الرصيد</p><p className="text-sm font-bold" style={{ color: selected.Balance > 0 ? tokens.dangerBorder : tokens.successBorder }}>{formatYER(selected.Balance)}</p></div>
              {selected.ContractOutstanding > 0 && <div className="col-span-2"><p className="text-[11px]" style={{ color: tokens.textTertiary }}>مستحقات العقود</p><p className="text-sm font-bold" style={{ color: tokens.warningText }}>{formatYER(selected.ContractOutstanding)}</p></div>}
            </div>
            <div className="flex justify-end gap-2 pt-2 border-t" style={{ borderColor: tokens.border }}>
              <button onClick={() => setShowPayment(true)} style={btnPrimary}>
                <DollarSign className="w-4 h-4" /> تسجيل دفعة
              </button>
            </div>
          </div>
        ) : null}
      </Modal>

      {/* Payment navigation prompt */}
      <Modal open={showPayment} onClose={() => setShowPayment(false)} title="تسجيل دفعة">
        <p className="text-sm mb-4" style={{ color: tokens.textSecondary }}>
          لتسجيل دفعة لهذا المريض، يرجى الانتقال إلى تبويب التحصيل أو شاشة التشغيل اليومي.
        </p>
        <div className="flex gap-2">
          <button onClick={() => setShowPayment(false)} style={btnGhost}>إلغاء</button>
          <a href="/daily-operations" style={btnPrimary}>الانتقال للتشغيل اليومي</a>
        </div>
      </Modal>
    </div>
  );
}
