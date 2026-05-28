"use client";

import { useState, useEffect, useCallback } from "react";
import {
  Lock,
  RefreshCw,
  CircleDot,
  Vault,
  Loader2,
  Calculator,
} from "lucide-react";
import { api } from "@/lib/api";
import { toast } from "@/stores/toastStore";
import type { CashierSession, CloseSessionRequest } from "./types";
import { SectionHeader, LoadingSkeleton, EmptyState, DataTable, Modal, ConfirmDialog, StatusBadge, tokens, inputStyle, labelStyle, btnDanger, btnGhost } from "./FinanceSharedUI";
import { formatYER, extractErrorMessage, safeFormatDateTime, safeArray } from "./FinanceHelpers";

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 6: Cashier
   Zero-State Resiliency: Safe array extraction, null-safe rendering
   ═══════════════════════════════════════════════════════════════════════════════ */
export function CashierTab({ isAdmin }: { isAdmin: boolean }) {
  const [sessions, setSessions] = useState<CashierSession[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [closeSession, setCloseSession] = useState<CashierSession | null>(null);
  const [actualCash, setActualCash] = useState("");
  const [actualCard, setActualCard] = useState("");
  const [actualBank, setActualBank] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [confirmReconcile, setConfirmReconcile] = useState<string | null>(null);

  const fetchSessions = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const { data: responseData } = await api.get<{ data: CashierSession[]; total: number }>("/api/cashier-sessions");
      setSessions(safeArray(responseData?.data));
    } catch (err) {
      setError(extractErrorMessage(err, "فشل في تحميل ورديات الصندوق"));
      toast.error("فشل في تحميل ورديات الصندوق");
    } finally { setLoading(false); }
  }, []);

  useEffect(() => { fetchSessions(); }, [fetchSessions]);

  const openCloseSession = async (s: CashierSession) => {
    setCloseSession(s);
    // Fetch session detail to get accurate per-bucket expected values
    // Never initialize actual amounts from undefined — fetch first
    try {
      const { data: detail } = await api.get<{
        expectedClosingCash: number;
        expectedClosingCard: number;
        expectedClosingBank: number;
      }>(`/api/cashier-sessions/${s.id}`);
      setActualCash(String(detail.expectedClosingCash ?? 0));
      setActualCard(String(detail.expectedClosingCard ?? 0));
      setActualBank(String(detail.expectedClosingBank ?? 0));
    } catch {
      // Fallback: use list values if available
      setActualCash(String(s.expectedClosingCash ?? 0));
      setActualCard(String(s.expectedClosingCard ?? 0));
      setActualBank(String(s.expectedClosingBank ?? 0));
    }
  };

  const handleClose = async () => {
    if (!closeSession) return;
    try {
      setSubmitting(true);
      const payload: CloseSessionRequest = {
        actualClosingCash: Number(actualCash) || 0,
        actualClosingCard: Number(actualCard) || 0,
        actualClosingBank: Number(actualBank) || 0,
      };
      await api.post(`/api/finance-v3/cashier-sessions/close`, payload);
      toast.success("تم إقفال الوردية بنجاح");
      setCloseSession(null);
      fetchSessions();
    } catch (err) { toast.error(extractErrorMessage(err, "فشل في إقفال الوردية")); } finally { setSubmitting(false); }
  };

  const handleReconcile = async () => {
    if (!confirmReconcile) return;
    try {
      setSubmitting(true);
      await api.patch(`/api/finance-v3/cashier-sessions/${confirmReconcile}/reconcile`);
      toast.success("تم تسوية الوردية بنجاح");
      setConfirmReconcile(null);
      fetchSessions();
    } catch (err) { toast.error(extractErrorMessage(err, "فشل في التسوية")); } finally { setSubmitting(false); }
  };

  const openSession = sessions.find((s) => s.status === "Open");

  return (
    <div className="p-6 space-y-4">
      <SectionHeader title="الصندوق" action={
        <div className="flex items-center gap-2">
          {openSession && (
            <button onClick={() => openCloseSession(openSession)} style={btnDanger}>
              <Lock className="w-4 h-4" /> إقفال الوردية
            </button>
          )}
          <button onClick={fetchSessions} className="w-8 h-8 rounded-md flex items-center justify-center" style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }} title="تحديث"><RefreshCw className="w-4 h-4" /></button>
        </div>
      } />

      {/* Current session info */}
      {openSession && (
        <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.successBg, borderColor: tokens.successBorder }}>
          <div className="flex items-center gap-2 mb-2">
            <CircleDot className="w-4 h-4" style={{ color: tokens.successBorder }} />
            <h4 className="text-sm font-bold" style={{ color: tokens.successBorder }}>وردية مفتوحة</h4>
          </div>
          <div className="grid grid-cols-3 gap-4 text-sm">
            <div><span style={{ color: tokens.textTertiary }}>الكاشر:</span> <span className="font-bold">{openSession.cashierName ?? "—"}</span></div>
            <div><span style={{ color: tokens.textTertiary }}>الافتتاح:</span> <span className="font-bold">{safeFormatDateTime(openSession.openedAt)}</span></div>
            <div><span style={{ color: tokens.textTertiary }}>رصيد الافتتاح:</span> <span className="font-bold">{formatYER(openSession.openingBalance ?? 0)}</span></div>
          </div>
        </div>
      )}

      {loading ? <LoadingSkeleton /> : error ? (
        <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.dangerBg, borderColor: tokens.dangerBorder }}>
          <p className="text-sm" style={{ color: tokens.dangerText }}>{error}</p>
          <button onClick={fetchSessions} className="text-xs font-medium mt-2 underline" style={{ color: tokens.brand }}>إعادة المحاولة</button>
        </div>
      ) : sessions.length === 0 ? <EmptyState icon={Vault} message="لا توجد ورديات صندوق" /> : (
        <DataTable<CashierSession>
          keyField="id"
          data={sessions}
          columns={[
            { key: "cashierName", label: "الكاشر" },
            { key: "openedAt", label: "وقت الافتتاح", render: (r) => safeFormatDateTime(r.openedAt) },
            { key: "closingTime", label: "وقت الإقفال", render: (r) => safeFormatDateTime(r.closingTime) },
            { key: "openingBalance", label: "رصيد الافتتاح", render: (r) => formatYER(r.openingBalance) },
            { key: "shortageOrSurplus", label: "عجز/فائض", render: (r) => {
              if (r.shortageOrSurplus == null) return "—";
              if (r.shortageOrSurplus < 0) return <span style={{ color: tokens.dangerBorder, fontWeight: 700 }}>عجز {formatYER(Math.abs(r.shortageOrSurplus))}</span>;
              if (r.shortageOrSurplus > 0) return <span style={{ color: tokens.successBorder, fontWeight: 700 }}>فائض {formatYER(r.shortageOrSurplus)}</span>;
              return <span style={{ color: tokens.successBorder }}>✓</span>;
            }},
            { key: "status", label: "الحالة", render: (r) => <StatusBadge status={r.status} /> },
            { key: "actions", label: "إجراءات", render: (r) => (
              <div className="flex items-center gap-1">
                {r.status === "Open" && <button onClick={(e) => { e.stopPropagation(); openCloseSession(r); }} style={{ color: tokens.dangerBorder }} className="w-7 h-7 rounded-md flex items-center justify-center" title="إقفال"><Lock className="w-3.5 h-3.5" /></button>}
                {r.status === "Closed" && isAdmin && <button onClick={(e) => { e.stopPropagation(); setConfirmReconcile(r.id); }} style={{ color: tokens.brand }} className="w-7 h-7 rounded-md flex items-center justify-center" title="تسوية"><Calculator className="w-3.5 h-3.5" /></button>}
              </div>
            )},
          ]}
        />
      )}

      {/* Close session modal with actual counts */}
      <Modal open={!!closeSession} onClose={() => setCloseSession(null)} title="إقفال الوردية">
        {closeSession && (
          <div className="space-y-4">
            <div className="rounded-md p-3" style={{ backgroundColor: tokens.infoBg, border: `1px solid ${tokens.infoBorder}` }}>
              <p className="text-xs" style={{ color: tokens.infoText }}>
                أدخل العد الفعلي لكل وسيلة دفع. سيتم حساب العجز/الفائض تلقائياً بناءً على القيم المتوقعة.
              </p>
            </div>

            <div className="space-y-3">
              <div className="grid grid-cols-2 gap-3">
                <div><span className="text-xs" style={{ color: tokens.textTertiary }}>النقدي المتوقع</span><p className="text-sm font-bold">{formatYER(closeSession.expectedClosingCash ?? 0)}</p></div>
                <div>
                  <label style={labelStyle}>النقدي الفعلي <span style={{ color: tokens.dangerBorder }}>*</span></label>
                  <input type="number" min="0" step="0.01" value={actualCash} onChange={(e) => setActualCash(e.target.value)} dir="ltr" style={inputStyle} />
                </div>
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div><span className="text-xs" style={{ color: tokens.textTertiary }}>البطاقة المتوقعة</span><p className="text-sm font-bold">{formatYER(closeSession.expectedClosingCard ?? 0)}</p></div>
                <div>
                  <label style={labelStyle}>البطاقة الفعلية <span style={{ color: tokens.dangerBorder }}>*</span></label>
                  <input type="number" min="0" step="0.01" value={actualCard} onChange={(e) => setActualCard(e.target.value)} dir="ltr" style={inputStyle} />
                </div>
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div><span className="text-xs" style={{ color: tokens.textTertiary }}>البنكي المتوقع</span><p className="text-sm font-bold">{formatYER(closeSession.expectedClosingBank ?? 0)}</p></div>
                <div>
                  <label style={labelStyle}>البنكي الفعلي <span style={{ color: tokens.dangerBorder }}>*</span></label>
                  <input type="number" min="0" step="0.01" value={actualBank} onChange={(e) => setActualBank(e.target.value)} dir="ltr" style={inputStyle} />
                </div>
              </div>
            </div>

            {/* Shortage/Surplus preview */}
            {(() => {
              const cashDiff = (Number(actualCash) || 0) - (closeSession.expectedClosingCash ?? 0);
              const cardDiff = (Number(actualCard) || 0) - (closeSession.expectedClosingCard ?? 0);
              const bankDiff = (Number(actualBank) || 0) - (closeSession.expectedClosingBank ?? 0);
              const hasDiff = cashDiff !== 0 || cardDiff !== 0 || bankDiff !== 0;
              if (!hasDiff) return null;
              return (
                <div className="rounded-md p-3" style={{ backgroundColor: tokens.warningBg, border: `1px solid ${tokens.warningBorder}` }}>
                  <h4 className="text-xs font-bold mb-2" style={{ color: tokens.warningText }}>الفروقات</h4>
                  <div className="space-y-1 text-xs">
                    {cashDiff !== 0 && <div className="flex justify-between"><span style={{ color: tokens.warningText }}>نقدي:</span><span style={{ color: cashDiff < 0 ? tokens.dangerBorder : tokens.successBorder, fontWeight: 700 }}>{cashDiff < 0 ? "عجز" : "فائض"} {formatYER(Math.abs(cashDiff))}</span></div>}
                    {cardDiff !== 0 && <div className="flex justify-between"><span style={{ color: tokens.warningText }}>بطاقة:</span><span style={{ color: cardDiff < 0 ? tokens.dangerBorder : tokens.successBorder, fontWeight: 700 }}>{cardDiff < 0 ? "عجز" : "فائض"} {formatYER(Math.abs(cardDiff))}</span></div>}
                    {bankDiff !== 0 && <div className="flex justify-between"><span style={{ color: tokens.warningText }}>بنكي:</span><span style={{ color: bankDiff < 0 ? tokens.dangerBorder : tokens.successBorder, fontWeight: 700 }}>{bankDiff < 0 ? "عجز" : "فائض"} {formatYER(Math.abs(bankDiff))}</span></div>}
                  </div>
                </div>
              );
            })()}

            <div className="flex gap-3 pt-2 border-t" style={{ borderColor: tokens.border }}>
              <button onClick={() => setCloseSession(null)} style={btnGhost}>إلغاء</button>
              <button onClick={handleClose} disabled={submitting} style={{ ...btnDanger, opacity: submitting ? 0.6 : 1 }}>
                {submitting && <Loader2 className="w-4 h-4 animate-spin" />}
                {submitting ? "جارٍ الإقفال..." : "إقفال الوردية"}
              </button>
            </div>
          </div>
        )}
      </Modal>

      <ConfirmDialog open={!!confirmReconcile} onClose={() => setConfirmReconcile(null)} onConfirm={handleReconcile} title="تسوية الوردية" message="هل أنت متأكد من تسوية هذه الوردية؟ هذا الإجراء متاح فقط للمسؤول." confirmLabel="تسوية" />
    </div>
  );
}
