"use client";

import { useState, useEffect, useCallback } from "react";
import {
  Lock,
  RefreshCw,
  CircleDot,
  Vault,
  Loader2,
  Calculator,
  Unlock,
} from "lucide-react";
import { api } from "@/lib/api";
import { toast } from "@/stores/toastStore";
import { useAuthStore } from "@/stores/authStore";
import { useBranches } from "@/hooks/useBranches";
import type { CashierSession, CloseSessionRequest } from "./types";
import { SectionHeader, LoadingSkeleton, EmptyState, DataTable, Modal, ConfirmDialog, StatusBadge, tokens, inputStyle, labelStyle, btnPrimary, btnDanger, btnGhost } from "./FinanceSharedUI";
import { formatYER, extractErrorMessage, safeFormatDateTime, safeArray } from "./FinanceHelpers";

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 6: Cashier
   Zero-State Resiliency: Safe array extraction, null-safe rendering
   ═══════════════════════════════════════════════════════════════════════════════ */
export function CashierTab({ isAdmin }: { isAdmin: boolean }) {
  const { user } = useAuthStore();
  const { data: branches } = useBranches("active");
  const [sessions, setSessions] = useState<CashierSession[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [closeSession, setCloseSession] = useState<CashierSession | null>(null);
  const [actualCash, setActualCash] = useState("");
  const [actualCard, setActualCard] = useState("");
  const [actualBank, setActualBank] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [confirmReconcile, setConfirmReconcile] = useState<string | null>(null);
  // Open session state
  const [showOpenSession, setShowOpenSession] = useState(false);
  const [openBalance, setOpenBalance] = useState("0");
  const [openBranchId, setOpenBranchId] = useState("");
  const [openNotes, setOpenNotes] = useState("");
  const [openSubmitting, setOpenSubmitting] = useState(false);

  // Determine if user needs to select a branch (no branch in token)
  const userBranchId = user?.branchId;
  const needsBranchSelection = !userBranchId || userBranchId === "";
  // Active branches for dropdown
  const activeBranches = (branches ?? []).filter((b) => b.isActive);

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

  /** Open a new cashier session */
  const handleOpenSession = useCallback(async () => {
    if (needsBranchSelection && !openBranchId) {
      toast.error("يرجى اختيار الفرع قبل فتح الوردية");
      return;
    }
    try {
      setOpenSubmitting(true);
      const payload: { openingBalance: number; branchId?: string; notes?: string } = {
        openingBalance: Number(openBalance) || 0,
      };
      if (needsBranchSelection && openBranchId) {
        payload.branchId = openBranchId;
      }
      if (openNotes.trim()) payload.notes = openNotes.trim();
      await api.post("/api/cashier-sessions/open", payload);
      toast.success("تم فتح الوردية بنجاح");
      setShowOpenSession(false);
      setOpenBalance("0");
      setOpenBranchId("");
      setOpenNotes("");
      fetchSessions();
    } catch (err) {
      toast.error(extractErrorMessage(err, "فشل في فتح الوردية"));
    } finally {
      setOpenSubmitting(false);
    }
  }, [needsBranchSelection, openBranchId, openBalance, openNotes, fetchSessions]);

  return (
    <div className="p-6 space-y-4">
      <SectionHeader title="الصندوق" action={
        <div className="flex items-center gap-2">
          {!openSession && (
            <button onClick={() => setShowOpenSession(true)} style={btnPrimary}>
              <Unlock className="w-4 h-4" /> فتح وردية
            </button>
          )}
          {openSession && (
            <button onClick={() => openCloseSession(openSession)} style={btnDanger}>
              <Lock className="w-4 h-4" /> إقفال الوردية
            </button>
          )}
          <button onClick={fetchSessions} className="w-8 h-8 rounded-md flex items-center justify-center" style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }} title="تحديث"><RefreshCw className="w-4 h-4" /></button>
        </div>
      } />

      {/* No open session warning */}
      {!openSession && !loading && (
        <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.warningBg, borderColor: tokens.warningBorder }}>
          <div className="flex items-center gap-2 mb-2">
            <CircleDot className="w-4 h-4" style={{ color: tokens.warningBorder }} />
            <h4 className="text-sm font-bold" style={{ color: tokens.warningBorder }}>لا توجد وردية مفتوحة</h4>
          </div>
          <p className="text-xs" style={{ color: tokens.warningText }}>
            يجب فتح وردية كاشير قبل تسجيل أي مدفوعات. اضغط على &ldquo;فتح وردية&rdquo; أعلاه لبدء الدورة النقدية.
          </p>
        </div>
      )}

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
            { key: "cashierName", label: "الكاشر", render: (r) => r.cashierName ?? "" },
            { key: "openedAt", label: "وقت الافتتاح", render: (r) => safeFormatDateTime(r.openedAt ?? "") },
            { key: "closingTime", label: "وقت الإقفال", render: (r) => safeFormatDateTime(r.closingTime ?? "") },
            { key: "openingBalance", label: "رصيد الافتتاح", render: (r) => formatYER(r.openingBalance ?? 0) },
            { key: "shortageOrSurplus", label: "عجز/فائض", render: (r) => {
              const val = r.shortageOrSurplus ?? null;
              if (val == null) return "—";
              if (val < 0) return <span style={{ color: tokens.dangerBorder, fontWeight: 700 }}>عجز {formatYER(Math.abs(val))}</span>;
              if (val > 0) return <span style={{ color: tokens.successBorder, fontWeight: 700 }}>فائض {formatYER(val)}</span>;
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

      {/* Open session modal */}
      <Modal open={showOpenSession} onClose={() => setShowOpenSession(false)} title="فتح وردية جديدة">
        <div className="space-y-4">
          <div className="rounded-md p-3" style={{ backgroundColor: tokens.infoBg, border: `1px solid ${tokens.infoBorder}` }}>
            <p className="text-xs" style={{ color: tokens.infoText }}>
              فتح وردية جديد يتيح لك تسجيل المدفوعات والتحصيلات. تأكد من اختيار الفرع الصحيح إذا لم يكن معيناً في حسابك.
            </p>
          </div>

          {/* Branch selector — only shown when user has no branch in token */}
          {needsBranchSelection && (
            <div>
              <label style={labelStyle}>الفرع <span style={{ color: tokens.dangerBorder }}>*</span></label>
              <select
                value={openBranchId}
                onChange={(e) => setOpenBranchId(e.target.value)}
                style={inputStyle}
              >
                <option value="">— اختر الفرع —</option>
                {activeBranches.map((b) => (
                  <option key={b.id} value={b.id}>{b.name}{b.isMain ? " (الرئيسي)" : ""}</option>
                ))}
              </select>
              {activeBranches.length === 0 && (
                <p className="text-xs mt-1" style={{ color: tokens.dangerBorder }}>
                  لا توجد فروع نشطة. يرجى إنشاء فرع أولاً من الإعدادات.
                </p>
              )}
            </div>
          )}

          {/* If user has a branch in token, show it as read-only */}
          {!needsBranchSelection && userBranchId && (
            <div>
              <label style={labelStyle}>الفرع</label>
              <div className="rounded-md px-3 py-2 text-sm" style={{ backgroundColor: tokens.cardHover, color: tokens.textPrimary, border: `1px solid ${tokens.border}` }}>
                {activeBranches.find((b) => b.id === userBranchId)?.name ?? `فرع (${userBranchId.slice(0, 8)}...)`}
              </div>
            </div>
          )}

          <div>
            <label style={labelStyle}>رصيد العهدة الافتتاحية</label>
            <input
              type="number"
              min="0"
              step="0.01"
              value={openBalance}
              onChange={(e) => setOpenBalance(e.target.value)}
              dir="ltr"
              style={inputStyle}
              placeholder="0"
            />
            <p className="text-[11px] mt-1" style={{ color: tokens.textTertiary }}>
              المبلغ النقدي الموجود في الدرج عند بداية الوردية
            </p>
          </div>

          <div>
            <label style={labelStyle}>ملاحظات</label>
            <input
              value={openNotes}
              onChange={(e) => setOpenNotes(e.target.value)}
              placeholder="ملاحظات اختيارية..."
              style={inputStyle}
            />
          </div>

          <div className="flex gap-3 pt-2 border-t" style={{ borderColor: tokens.border }}>
            <button onClick={() => setShowOpenSession(false)} style={btnGhost}>إلغاء</button>
            <button
              onClick={handleOpenSession}
              disabled={openSubmitting || (needsBranchSelection && !openBranchId)}
              style={{ ...btnPrimary, opacity: openSubmitting || (needsBranchSelection && !openBranchId) ? 0.6 : 1 }}
            >
              {openSubmitting && <Loader2 className="w-4 h-4 animate-spin" />}
              {openSubmitting ? "جارٍ الفتح..." : "فتح الوردية"}
            </button>
          </div>
        </div>
      </Modal>

      <ConfirmDialog open={!!confirmReconcile} onClose={() => setConfirmReconcile(null)} onConfirm={handleReconcile} title="تسوية الوردية" message="هل أنت متأكد من تسوية هذه الوردية؟ هذا الإجراء متاح فقط للمسؤول." confirmLabel="تسوية" />
    </div>
  );
}
