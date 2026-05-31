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
    try { setLoading(true); const { data: responseData } = await api.get<{ data: PatientBalance[]; total: number }>("/api/finance-v3/patient-accounts"); setData(responseData?.data ?? []); } catch { toast.error("فشل في تحميل حسابات المرضى"); } finally { setLoading(false); }
  }, []);

  useEffect(() => { fetchData(); }, [fetchData]);

  const openDetail = async (patient: PatientBalance) => {
    try {
      setDetailLoading(true);
      const { data } = await api.get<PatientBalanceDetail>(`/api/finance-v3/patient-balance/${patient.patientId}`);
      setSelected(data);
    } catch { toast.error("فشل في تحميل تفاصيل الحساب"); } finally { setDetailLoading(false); }
  };

  const filtered = data.filter((p) =>
    p.patientName.includes(search) || p.patientNumber.includes(search)
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
          keyField="patientId"
          data={filtered}
          onRowClick={openDetail}
          columns={[
            { key: "patientNumber", label: "رقم المريض" },
            { key: "patientName", label: "الاسم" },
            { key: "totalInvoiced", label: "إجمالي الفواتير", render: (r) => formatYER(r.totalInvoiced) },
            { key: "totalPaid", label: "إجمالي المدفوع", render: (r) => formatYER(r.totalPaid) },
            { key: "balance", label: "الرصيد", render: (r) => <span style={{ color: r.balance > 0 ? tokens.dangerBorder : tokens.successBorder, fontWeight: 700 }}>{formatYER(r.balance)}</span> },
            { key: "hasOutstanding", label: "معلّق", render: (r) => r.hasOutstanding ? <AlertTriangle className="w-4 h-4" style={{ color: tokens.warningBorder }} /> : <CheckCircle2 className="w-4 h-4" style={{ color: tokens.successBorder }} /> },
          ]}
        />
      )}

      {/* Detail modal */}
      <Modal open={!!selected} onClose={() => { setSelected(null); setShowPayment(false); }} title={selected?.patientName ?? "تفاصيل الحساب"} wide>
        {detailLoading ? <LoadingSkeleton rows={4} /> : selected ? (
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-3">
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>رقم المريض</p><p className="text-sm font-bold" style={{ color: tokens.textPrimary }}>{selected.patientNumber}</p></div>
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>إجمالي الفواتير</p><p className="text-sm font-bold" style={{ color: tokens.textPrimary }}>{formatYER(selected.totalInvoiced)}</p></div>
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>إجمالي المدفوع</p><p className="text-sm font-bold" style={{ color: tokens.successBorder }}>{formatYER(selected.netPaid)}</p></div>
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>المرتجعات</p><p className="text-sm font-bold" style={{ color: tokens.warningText }}>{formatYER(selected.totalRefunds)}</p></div>
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>الخصومات</p><p className="text-sm font-bold" style={{ color: tokens.brand }}>{formatYER(selected.totalDiscounts)}</p></div>
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>الرصيد</p><p className="text-sm font-bold" style={{ color: selected.balance > 0 ? tokens.dangerBorder : tokens.successBorder }}>{formatYER(selected.balance)}</p></div>
              {selected.contractOutstanding > 0 && <div className="col-span-2"><p className="text-[11px]" style={{ color: tokens.textTertiary }}>مستحقات العقود</p><p className="text-sm font-bold" style={{ color: tokens.warningText }}>{formatYER(selected.contractOutstanding)}</p></div>}
              {/* Fix 5: Reconciliation indicator — warn if JournalLine balance differs from entity balance */}
              {selected.journalReceivable != null && selected.entityBalance != null && Math.abs(selected.journalReceivable - selected.entityBalance) > 0.01 && (
                <div className="col-span-2 p-2.5 rounded-lg flex items-center gap-2" style={{ background: "#fef3c740", border: `1px solid ${tokens.warningBorder}` }}>
                  <AlertTriangle className="w-4 h-4 flex-shrink-0" style={{ color: tokens.warningBorder }} />
                  <span className="text-[11px] font-medium" style={{ color: tokens.warningText }}>
                    تباين بين رصيد القيود ({formatYER(selected.journalReceivable)}) والرصيد المسجل ({formatYER(selected.entityBalance)}). قد تكون هناك سجلات قديمة بدون قيود محاسبية.
                  </span>
                </div>
              )}
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
