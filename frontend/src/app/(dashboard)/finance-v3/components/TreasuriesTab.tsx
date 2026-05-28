"use client";

import { useState, useEffect, useCallback } from "react";
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
import { formatYER, extractErrorMessage, safeFormatDateTime, safeArray } from "./FinanceHelpers";

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 7: Treasuries
   Zero-State Resiliency: Safe array extraction, null-safe rendering
   ═══════════════════════════════════════════════════════════════════════════════ */
export function TreasuriesTab() {
  const [treasuries, setTreasuries] = useState<Treasury[]>([]);
  const [transfers, setTransfers] = useState<VaultTransfer[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showCreateTreasury, setShowCreateTreasury] = useState(false);
  const [showCreateTransfer, setShowCreateTransfer] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [confirmApprove, setConfirmApprove] = useState<{ id: string; action: "approve" | "reject" } | null>(null);

  // Create treasury form
  const [tName, setTName] = useState("");
  const [tType, setTType] = useState("Vault");
  const [tBalance, setTBalance] = useState("0");

  // Create transfer form
  const [srcId, setSrcId] = useState("");
  const [dstId, setDstId] = useState("");
  const [trAmount, setTrAmount] = useState("");
  const [trDepositSource, setTrDepositSource] = useState("");
  const [trNotes, setTrNotes] = useState("");

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const [tRes, trRes] = await Promise.all([
        api.get<{ data: Treasury[] }>("/api/finance-v3/treasuries"),
        api.get<{ data: VaultTransfer[] }>("/api/finance-v3/vault-transfers"),
      ]);
      setTreasuries(safeArray(tRes.data?.data));
      setTransfers(safeArray(trRes.data?.data ?? (Array.isArray(trRes.data) ? trRes.data as unknown as VaultTransfer[] : undefined)));
    } catch (err) {
      setError(extractErrorMessage(err, "فشل في تحميل بيانات الخزائن"));
      toast.error("فشل في تحميل بيانات الخزائن");
    } finally { setLoading(false); }
  }, []);

  useEffect(() => { fetchData(); }, [fetchData]);

  const handleCreateTreasury = async () => {
    if (!tName.trim()) { toast.error("يرجى إدخال اسم الخزينة"); return; }
    try {
      setSubmitting(true);
      const payload: CreateTreasuryRequest = { name: tName, type: tType, openingBalance: Number(tBalance) || 0 };
      await api.post("/api/finance-v3/treasuries", payload);
      toast.success("تم إنشاء الخزينة بنجاح");
      setShowCreateTreasury(false);
      setTName(""); setTType("Vault"); setTBalance("0");
      fetchData();
    } catch (err: unknown) {
      const msg = extractErrorMessage(err, "فشل في إنشاء الخزينة");
      const axiosErr = err as { response?: { status?: number } } | null;
      // Common errors: unique constraint, admin-only, no branch
      if (msg.includes("unique") || msg.includes("مكرر") || msg.includes("IX_Treasuries")) {
        toast.error("يوجد خزينة بنفس الاسم والنوع في هذا الفرع. يرجى تغيير الاسم أو النوع.");
      } else if (msg.includes("Admin") || msg.includes("مسؤول") || msg.includes("403") || axiosErr?.response?.status === 403) {
        toast.error("فقط المسؤول يمكنه إنشاء خزائن جديدة.");
      } else if (msg.includes("فرع") || msg.includes("branch")) {
        toast.error("لم يتم تحديد فرع للمستخدم. يرجى تسجيل الدخول بفرع صالح.");
      } else {
        toast.error(msg);
      }
    } finally { setSubmitting(false); }
  };

  const handleCreateTransfer = async () => {
    if (!srcId || !dstId || !trAmount || Number(trAmount) <= 0) {
      toast.error("يرجى ملء جميع الحقول المطلوبة");
      return;
    }
    if (srcId === dstId) { toast.error("لا يمكن التحويل من وإلى نفس الخزينة"); return; }
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
        ? `/api/finance-v3/vault-transfers/${confirmApprove.id}/approve`
        : `/api/finance-v3/vault-transfers/${confirmApprove.id}/reject`;
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

        {loading ? <LoadingSkeleton /> : treasuries.length === 0 ? <EmptyState icon={Landmark} message="لا توجد خزائن" /> : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {treasuries.map((t) => (
              <div key={t.id} className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
                <div className="flex items-center justify-between mb-2">
                  <h4 className="text-sm font-bold" style={{ color: tokens.textPrimary }}>{t.name ?? ""}</h4>
                  <span className="text-[11px] px-2 py-0.5 rounded-full" style={{ backgroundColor: tokens.brandLight, color: tokens.brand }}>{TREASURY_TYPES.find((x) => x.value === (t.type ?? ""))?.label ?? t.type}</span>
                </div>
                <p className="text-lg font-bold mb-1" style={{ color: tokens.successBorder }}>{formatYER(t.balance ?? 0)}</p>
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
              { key: "sourceTreasuryName", label: "من", render: (r) => r.sourceTreasuryName ?? "" },
              { key: "destinationTreasuryName", label: "إلى", render: (r) => r.destinationTreasuryName ?? "" },
              { key: "amount", label: "المبلغ", render: (r) => formatYER(r.amount ?? 0) },
              { key: "status", label: "الحالة", render: (r) => <StatusBadge status={r.status} /> },
              { key: "requestedBy", label: "بواسطة", render: (r) => r.requestedBy ?? "" },
              { key: "requestedAt", label: "التاريخ", render: (r) => safeFormatDateTime(r.requestedAt ?? "") },
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
          <div><label style={labelStyle}>الخزينة المصدر <span style={{ color: tokens.dangerBorder }}>*</span></label><select value={srcId} onChange={(e) => setSrcId(e.target.value)} style={inputStyle}><option value="">— اختر —</option>{treasuries.map((t) => (<option key={t.id} value={t.id}>{t.name} ({formatYER(t.balance)})</option>))}</select></div>
          <div><label style={labelStyle}>الخزينة الوجهة <span style={{ color: tokens.dangerBorder }}>*</span></label><select value={dstId} onChange={(e) => setDstId(e.target.value)} style={inputStyle}><option value="">— اختر —</option>{treasuries.map((t) => (<option key={t.id} value={t.id}>{t.name} ({formatYER(t.balance)})</option>))}</select></div>
          <div><label style={labelStyle}>المبلغ <span style={{ color: tokens.dangerBorder }}>*</span></label><input type="number" min="0.01" max={srcId ? treasuries.find((t) => t.id === srcId)?.balance : undefined} step="0.01" value={trAmount} onChange={(e) => setTrAmount(e.target.value)} dir="ltr" style={inputStyle} />{srcId && Number(trAmount) > (treasuries.find((t) => t.id === srcId)?.balance ?? 0) && (<p className="text-xs mt-1" style={{ color: tokens.dangerBorder }}>⚠ المبلغ يتجاوز رصيد الخزينة المصدر</p>)}</div>
          {/* Deposit source for external transfers */}
          {(() => { const srcT = treasuries.find((t) => t.id === srcId); return srcT?.type === "External" || (srcId && !treasuries.find((t) => t.id === srcId)); })() ? (
            <div><label style={labelStyle}>مصدر الإيداع</label><select value={trDepositSource} onChange={(e) => setTrDepositSource(e.target.value)} style={inputStyle}><option value="">— اختر —</option>{DEPOSIT_SOURCES.map((d) => (<option key={d.value} value={d.value}>{d.label}</option>))}</select></div>
          ) : null}
          <div><label style={labelStyle}>ملاحظات</label><input value={trNotes} onChange={(e) => setTrNotes(e.target.value)} placeholder="ملاحظات اختيارية..." style={inputStyle} /></div>
          <div className="flex gap-3 pt-2 border-t" style={{ borderColor: tokens.border }}>
            <button onClick={() => setShowCreateTransfer(false)} style={btnGhost}>إلغاء</button>
            <button onClick={handleCreateTransfer} disabled={submitting || (srcId ? Number(trAmount) > (treasuries.find((t) => t.id === srcId)?.balance ?? 0) : false)} style={{ ...btnPrimary, opacity: submitting || (srcId ? Number(trAmount) > (treasuries.find((t) => t.id === srcId)?.balance ?? 0) : false) ? 0.6 : 1 }}>{submitting ? "جارٍ الحفظ..." : "إنشاء تحويل"}</button>
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
