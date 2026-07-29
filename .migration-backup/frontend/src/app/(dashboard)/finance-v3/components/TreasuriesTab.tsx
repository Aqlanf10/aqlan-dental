"use client";

import { useState, useEffect, useCallback, useMemo } from "react";
import {
  Plus,
  RefreshCw,
  Landmark,
  ArrowRightLeft,
  Calculator,
  ThumbsUp,
  ThumbsDown,
} from "lucide-react";
import { api } from "@/lib/api";
import { toast } from "@/stores/toastStore";
import type { Treasury, CreateTreasuryRequest, VaultTransfer, CreateTransferRequest } from "./types";
import { TREASURY_TYPES, DEPOSIT_SOURCES } from "./types";
import { SectionHeader, LoadingSkeleton, EmptyState, DataTable, Modal, ConfirmDialog, StatusBadge, tokens, inputStyle, labelStyle, btnPrimary, btnGhost } from "./FinanceSharedUI";
import { formatYER, extractErrorMessage, safeFormatDateTime } from "./FinanceHelpers";

const formatMoney = (amount: number, currency?: string | null) =>
  `${(amount ?? 0).toLocaleString("ar-YE", { minimumFractionDigits: 0, maximumFractionDigits: 2 })} ${currency ?? "YER"}`;

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 7: Treasuries
   ═══════════════════════════════════════════════════════════════════════════════ */
export function TreasuriesTab() {
  const [treasuries, setTreasuries] = useState<Treasury[]>([]);
  const [transfers, setTransfers] = useState<VaultTransfer[]>([]);
  const [loading, setLoading] = useState(true);
  const [showCreateTreasury, setShowCreateTreasury] = useState(false);
  const [showCreateTransfer, setShowCreateTransfer] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [confirmApprove, setConfirmApprove] = useState<{ id: string; action: "approve" | "reject" } | null>(null);

  // Create treasury form
  const [tName, setTName] = useState("");
  const [tType, setTType] = useState("Vault");
  const [tCurrency, setTCurrency] = useState<"YER" | "SAR" | "USD">("YER");
  const [tBalance, setTBalance] = useState("0");

  // Create transfer form
  const [srcId, setSrcId] = useState("");
  const [dstId, setDstId] = useState("");
  const [trAmount, setTrAmount] = useState("");
  const [trDepositSource, setTrDepositSource] = useState("");
  const [trNotes, setTrNotes] = useState("");
  const sourceTreasury = srcId ? treasuries.find((t) => t.id === srcId) : undefined;
  const destinationTreasury = dstId ? treasuries.find((t) => t.id === dstId) : undefined;
  const isCrossCurrencyTransfer = !!sourceTreasury && !!destinationTreasury && sourceTreasury.currency !== destinationTreasury.currency;

  // Per-currency totals (cash drawer vs bank) computed from the already-loaded treasuries.
  // Currencies are NOT summed together — each is a separate total (no implicit conversion).
  const currencyTotals = useMemo(() => {
    const order = ["YER", "SAR", "USD"] as const;
    const map = new Map<string, { cash: number; bank: number; total: number }>();
    for (const t of treasuries) {
      const cur = t.currency ?? "YER";
      const entry = map.get(cur) ?? { cash: 0, bank: 0, total: 0 };
      if (t.type === "Bank") entry.bank += t.balance ?? 0;
      else entry.cash += t.balance ?? 0;
      entry.total += t.balance ?? 0;
      map.set(cur, entry);
    }
    return order.filter((c) => map.has(c)).map((c) => ({ currency: c, ...map.get(c)! }));
  }, [treasuries]);

  const currencyLabel = (c: string) =>
    c === "SAR" ? "ريال سعودي" : c === "USD" ? "دولار أمريكي" : "ريال يمني";

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      const [tRes, trRes] = await Promise.all([
        api.get<{ data: Treasury[] }>("/api/finance-v3/treasuries"),
        api.get<{ data: VaultTransfer[] }>("/api/finance-v3/vault-transfers"),
      ]);
      setTreasuries(tRes.data.data ?? []);
      setTransfers(trRes.data.data ?? (Array.isArray(trRes.data) ? trRes.data as unknown as VaultTransfer[] : []));
    } catch { toast.error("فشل في تحميل بيانات الخزائن"); } finally { setLoading(false); }
  }, []);

  useEffect(() => { fetchData(); }, [fetchData]);

  const handleCreateTreasury = async () => {
    if (!tName.trim()) { toast.error("يرجى إدخال اسم الخزينة"); return; }
    try {
      setSubmitting(true);
      const payload: CreateTreasuryRequest = { name: tName, type: tType, currency: tCurrency, openingBalance: Number(tBalance) || 0 };
      await api.post("/api/finance-v3/treasuries", payload);
      toast.success("تم إنشاء الخزينة بنجاح");
      setShowCreateTreasury(false);
      setTName(""); setTType("Vault"); setTCurrency("YER"); setTBalance("0");
      fetchData();
    } catch (err) { toast.error(extractErrorMessage(err, "فشل في إنشاء الخزينة")); } finally { setSubmitting(false); }
  };

  const handleCreateTransfer = async () => {
    if (!srcId || !dstId || !trAmount || Number(trAmount) <= 0) {
      toast.error("يرجى ملء جميع الحقول المطلوبة");
      return;
    }
    if (srcId === dstId) { toast.error("لا يمكن التحويل من وإلى نفس الخزينة"); return; }
    if (isCrossCurrencyTransfer) {
      toast.error("لا يمكن التحويل بين خزائن بعملات مختلفة. سجّل عملية مصارفة مستقلة بسعر صرف موثق.");
      return;
    }
    try {
      setSubmitting(true);
      const payload: CreateTransferRequest = {
        sourceTreasuryId: srcId,
        destinationTreasuryId: dstId,
        amount: Number(trAmount),
        depositSource: trDepositSource || undefined,
        notes: trNotes || undefined,
      };
      await api.post("/api/finance-v3/vault-transfers", payload);
      toast.success("تم إنشاء التحويل بنجاح");
      setShowCreateTransfer(false);
      setSrcId(""); setDstId(""); setTrAmount(""); setTrDepositSource(""); setTrNotes("");
      fetchData();
    } catch (err) { toast.error(extractErrorMessage(err, "فشل في إنشاء التحويل")); } finally { setSubmitting(false); }
  };

  const handleTransferAction = async () => {
    if (!confirmApprove) return;
    try {
      setSubmitting(true);
      const url = confirmApprove.action === "approve"
        ? `/api/vault-transfers/${confirmApprove.id}/approve`
        : `/api/vault-transfers/${confirmApprove.id}/reject`;
      await api.post(url);
      toast.success(confirmApprove.action === "approve" ? "تم اعتماد التحويل" : "تم رفض التحويل");
      setConfirmApprove(null);
      fetchData();
    } catch (err) { toast.error(extractErrorMessage(err, "فشل في تنفيذ الإجراء")); } finally { setSubmitting(false); }
  };

  const handleRecalculate = async (id: string) => {
    try {
      await api.post(`/api/finance-v3/treasuries/${id}/recalculate`);
      toast.success("تم إعادة حساب رصيد الخزينة");
      fetchData();
    } catch (err) { toast.error(extractErrorMessage(err, "فشل في إعادة الحساب")); }
  };

  return (
    <div className="p-6 space-y-6">
      {/* Treasuries */}
      <div>
        <SectionHeader title="الخزائن" action={
          <div className="flex items-center gap-2">
            <button onClick={() => setShowCreateTreasury(true)} style={btnPrimary}><Plus className="w-4 h-4" /> خزينة جديدة</button>
            <button onClick={fetchData} className="w-8 h-8 rounded-md flex items-center justify-center" style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }} title="تحديث"><RefreshCw className="w-4 h-4" /></button>
          </div>
        } />

        {/* Clarify Treasuries (persistent per-currency accounts) vs the daily Cash box/shift */}
        <div className="rounded-lg border p-3 mb-3 text-xs leading-relaxed" style={{ backgroundColor: tokens.infoBg, borderColor: tokens.border, color: tokens.textSecondary }}>
          كل <span className="font-bold">خزينة</span> هي رصيد دائم لنوع محدّد (<span className="font-bold">درج كاش</span> أو <span className="font-bold">حساب بنكي</span>) وعملة محدّدة — ولكل عملة (YER / SAR / USD) خزينة مستقلة.
          أمّا <span className="font-bold">وردية الكاشير اليومية</span> فتُفتح على درج الكاش بالريال اليمني من تبويب «الصندوق». لا يجوز التحويل بين خزينتين بعملتين مختلفتين إلا بعملية مصارفة بسعر صرف موثّق.
        </div>

        {/* Per-currency totals (each currency kept separate — no implicit conversion) */}
        {!loading && currencyTotals.length > 0 && (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3 mb-4">
            {currencyTotals.map((ct) => (
              <div key={ct.currency} className="rounded-lg border p-3" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
                <span className="text-xs font-bold" style={{ color: tokens.textSecondary }}>{currencyLabel(ct.currency)} ({ct.currency})</span>
                <p className="text-lg font-black my-1" style={{ color: tokens.successBorder }}>{formatMoney(ct.total, ct.currency)}</p>
                <div className="flex items-center gap-3 text-[11px]" style={{ color: tokens.textTertiary }}>
                  <span>نقد/درج: <span className="font-bold" style={{ color: tokens.textPrimary }}>{formatMoney(ct.cash, ct.currency)}</span></span>
                  <span>بنك: <span className="font-bold" style={{ color: tokens.textPrimary }}>{formatMoney(ct.bank, ct.currency)}</span></span>
                </div>
              </div>
            ))}
          </div>
        )}

        {loading ? <LoadingSkeleton /> : treasuries.length === 0 ? <EmptyState icon={Landmark} message="لا توجد خزائن" /> : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {treasuries.map((t) => (
              <div key={t.id} className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
                <div className="flex items-center justify-between mb-2">
                  <h4 className="text-sm font-bold" style={{ color: tokens.textPrimary }}>{t.name}</h4>
                  <span className="text-[11px] px-2 py-0.5 rounded-full" style={{ backgroundColor: tokens.brandLight, color: tokens.brand }}>{TREASURY_TYPES.find((x) => x.value === t.type)?.label ?? t.type}</span>
                </div>
                <p className="text-lg font-bold mb-1" style={{ color: tokens.successBorder }}>{formatMoney(t.balance, t.currency)}</p>
                <button onClick={() => handleRecalculate(t.id)} className="text-xs flex items-center gap-1" style={{ color: tokens.brand }}>
                  <Calculator className="w-3 h-3" /> إعادة حساب
                </button>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Transfers */}
      <div>
        <SectionHeader title="التحويلات" action={
          <button onClick={() => setShowCreateTransfer(true)} style={btnPrimary}><ArrowRightLeft className="w-4 h-4" /> تحويل جديد</button>
        } />

        {loading ? <LoadingSkeleton /> : transfers.length === 0 ? <EmptyState icon={ArrowRightLeft} message="لا توجد تحويلات" /> : (
          <DataTable<VaultTransfer>
            keyField="id"
            data={transfers}
            columns={[
              { key: "sourceTreasuryName", label: "من" },
              { key: "destinationTreasuryName", label: "إلى" },
              { key: "amount", label: "المبلغ", render: (r) => formatYER(r.amount) },
              { key: "status", label: "الحالة", render: (r) => <StatusBadge status={r.status} /> },
              { key: "performedBy", label: "بواسطة" },
              { key: "transferDate", label: "التاريخ", render: (r) => safeFormatDateTime(r.transferDate) },
              { key: "actions", label: "إجراءات", render: (r) => r.status === "Pending" ? (
                <div className="flex items-center gap-1">
                  <button onClick={(e) => { e.stopPropagation(); setConfirmApprove({ id: r.id, action: "approve" }); }} className="w-7 h-7 rounded-md flex items-center justify-center" style={{ color: tokens.successBorder }} title="اعتماد"><ThumbsUp className="w-3.5 h-3.5" /></button>
                  <button onClick={(e) => { e.stopPropagation(); setConfirmApprove({ id: r.id, action: "reject" }); }} className="w-7 h-7 rounded-md flex items-center justify-center" style={{ color: tokens.dangerBorder }} title="رفض"><ThumbsDown className="w-3.5 h-3.5" /></button>
                </div>
              ) : null },
            ]}
          />
        )}
      </div>

      {/* Create Treasury Modal */}
      <Modal open={showCreateTreasury} onClose={() => setShowCreateTreasury(false)} title="خزينة جديدة">
        <div className="space-y-4">
          <div><label style={labelStyle}>اسم الخزينة <span style={{ color: tokens.dangerBorder }}>*</span></label><input value={tName} onChange={(e) => setTName(e.target.value)} placeholder="مثال: الصندوق الرئيسي" style={inputStyle} /></div>
          <div><label style={labelStyle}>النوع</label><select value={tType} onChange={(e) => setTType(e.target.value)} style={inputStyle}>{TREASURY_TYPES.map((t) => (<option key={t.value} value={t.value}>{t.label}</option>))}</select></div>
          <div><label style={labelStyle}>العملة</label><select value={tCurrency} onChange={(e) => setTCurrency(e.target.value as "YER" | "SAR" | "USD")} style={inputStyle}><option value="YER">ريال يمني YER</option><option value="SAR">ريال سعودي SAR</option><option value="USD">دولار USD</option></select></div>
          <div><label style={labelStyle}>الرصيد الافتتاحي</label><input type="number" min="0" step="0.01" value={tBalance} onChange={(e) => setTBalance(e.target.value)} dir="ltr" style={inputStyle} /></div>
          <div className="flex gap-3 pt-2 border-t" style={{ borderColor: tokens.border }}>
            <button onClick={() => setShowCreateTreasury(false)} style={btnGhost}>إلغاء</button>
            <button onClick={handleCreateTreasury} disabled={submitting} style={{ ...btnPrimary, opacity: submitting ? 0.6 : 1 }}>{submitting ? "جارٍ الحفظ..." : "إنشاء"}</button>
          </div>
        </div>
      </Modal>

      {/* Create Transfer Modal */}
      <Modal open={showCreateTransfer} onClose={() => setShowCreateTransfer(false)} title="تحويل جديد">
        <div className="space-y-4">
          <div><label style={labelStyle}>الخزينة المصدر <span style={{ color: tokens.dangerBorder }}>*</span></label><select value={srcId} onChange={(e) => setSrcId(e.target.value)} style={inputStyle}><option value="">— اختر —</option>{treasuries.map((t) => (<option key={t.id} value={t.id}>{t.name} ({formatMoney(t.balance, t.currency)})</option>))}</select></div>
          <div><label style={labelStyle}>الخزينة الوجهة <span style={{ color: tokens.dangerBorder }}>*</span></label><select value={dstId} onChange={(e) => setDstId(e.target.value)} style={inputStyle}><option value="">— اختر —</option>{treasuries.map((t) => (<option key={t.id} value={t.id} disabled={!!sourceTreasury && t.currency !== sourceTreasury.currency}>{t.name} ({formatMoney(t.balance, t.currency)})</option>))}</select></div>
          {isCrossCurrencyTransfer && (
            <p className="text-xs rounded-md border px-3 py-2" style={{ color: tokens.dangerBorder, borderColor: tokens.dangerBorder }}>
              لا يمكن التحويل المباشر بين خزائن بعملات مختلفة. استخدم عملية مصارفة مستقلة بسعر صرف موثق.
            </p>
          )}
          <div><label style={labelStyle}>المبلغ <span style={{ color: tokens.dangerBorder }}>*</span></label><input type="number" min="0.01" max={srcId ? treasuries.find((t) => t.id === srcId)?.balance : undefined} step="0.01" value={trAmount} onChange={(e) => setTrAmount(e.target.value)} dir="ltr" style={inputStyle} />{srcId && Number(trAmount) > (treasuries.find((t) => t.id === srcId)?.balance ?? 0) && (<p className="text-xs mt-1" style={{ color: tokens.dangerBorder }}>⚠ المبلغ يتجاوز رصيد الخزينة المصدر</p>)}</div>
          {/* Deposit source for external transfers */}
          {(() => { const srcT = treasuries.find((t) => t.id === srcId); return srcT?.type === "External" || (srcId && !treasuries.find((t) => t.id === srcId)); })() ? (
            <div><label style={labelStyle}>مصدر الإيداع</label><select value={trDepositSource} onChange={(e) => setTrDepositSource(e.target.value)} style={inputStyle}><option value="">— اختر —</option>{DEPOSIT_SOURCES.map((d) => (<option key={d.value} value={d.value}>{d.label}</option>))}</select></div>
          ) : null}
          <div><label style={labelStyle}>ملاحظات</label><input value={trNotes} onChange={(e) => setTrNotes(e.target.value)} placeholder="ملاحظات اختيارية..." style={inputStyle} /></div>
          <div className="flex gap-3 pt-2 border-t" style={{ borderColor: tokens.border }}>
            <button onClick={() => setShowCreateTransfer(false)} style={btnGhost}>إلغاء</button>
            <button onClick={handleCreateTransfer} disabled={submitting || isCrossCurrencyTransfer || (srcId ? Number(trAmount) > (treasuries.find((t) => t.id === srcId)?.balance ?? 0) : false)} style={{ ...btnPrimary, opacity: submitting || isCrossCurrencyTransfer || (srcId ? Number(trAmount) > (treasuries.find((t) => t.id === srcId)?.balance ?? 0) : false) ? 0.6 : 1 }}>{submitting ? "جارٍ الحفظ..." : "إنشاء تحويل"}</button>
          </div>
        </div>
      </Modal>

      <ConfirmDialog
        open={!!confirmApprove}
        onClose={() => setConfirmApprove(null)}
        onConfirm={handleTransferAction}
        title={confirmApprove?.action === "approve" ? "اعتماد التحويل" : "رفض التحويل"}
        message={confirmApprove?.action === "approve" ? "هل أنت متأكد من اعتماد هذا التحويل؟ سيتم تحديث أرصدة الخزائن." : "هل أنت متأكد من رفض هذا التحويل؟"}
        confirmLabel={confirmApprove?.action === "approve" ? "اعتماد" : "رفض"}
        danger={confirmApprove?.action === "reject"}
      />
    </div>
  );
}
