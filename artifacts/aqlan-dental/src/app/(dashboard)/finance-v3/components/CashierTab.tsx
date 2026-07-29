
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
import type { CashierForeignCurrencyActivity, CashierSession, CloseSessionRequest } from "./types";
import { SectionHeader, LoadingSkeleton, EmptyState, DataTable, Modal, ConfirmDialog, StatusBadge, tokens, inputStyle, labelStyle, btnPrimary, btnDanger, btnGhost } from "./FinanceSharedUI";
import { formatCurrencyAmounts, formatMoney, formatYER, extractErrorMessage, safeFormatDateTime } from "./FinanceHelpers";

const FOREIGN_DRAWER_CURRENCIES = ["SAR", "USD"] as const;

function getSessionOpeningBalances(session: CashierSession) {
  return [
    { currency: "YER", amount: session.openingBalance ?? 0 },
    ...(session.currencyOpeningBalances ?? []).map((balance) => ({
      currency: balance.currency,
      amount: balance.openingCash,
    })),
  ];
}

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 6: Cashier — الورديات اليومية (التشغيل اليومي)
   ═══════════════════════════════════════════════════════════════════════════════ */
export function CashierTab({ isAdmin }: { isAdmin: boolean }) {
  const { user } = useAuthStore();
  const [sessions, setSessions] = useState<CashierSession[]>([]);
  const [loading, setLoading] = useState(true);
  const [closeSession, setCloseSession] = useState<CashierSession | null>(null);
  const [actualCash, setActualCash] = useState("");
  const [actualCard, setActualCard] = useState("");
  const [actualBank, setActualBank] = useState("");
  const [foreignCurrencyActivity, setForeignCurrencyActivity] = useState<CashierForeignCurrencyActivity[]>([]);
  const [foreignActuals, setForeignActuals] = useState<Record<string, { cash: string; bank: string }>>({});
  const [submitting, setSubmitting] = useState(false);
  const [confirmReconcile, setConfirmReconcile] = useState<string | null>(null);

  // Open session modal state
  const [showOpenModal, setShowOpenModal] = useState(false);
  const [openingBalance, setOpeningBalance] = useState("");
  const [foreignOpeningCash, setForeignOpeningCash] = useState<Record<string, string>>({ SAR: "", USD: "" });
  const [openNotes, setOpenNotes] = useState("");

  const fetchSessions = useCallback(async () => {
    try {
      setLoading(true);
      const { data: responseData } = await api.get<{ data: CashierSession[]; total: number }>("/api/cashier-sessions");
      setSessions(responseData?.data ?? []);
    } catch {
      toast.error("فشل في تحميل ورديات الصندوق");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { fetchSessions(); }, [fetchSessions]);

  const openSession = sessions.find((s) => {
    if (s.status !== "Open") return false;
    if (s.cashierId && user?.id) return s.cashierId.toLowerCase() === user.id.toLowerCase();
    if (s.cashierName && user?.username) return s.cashierName.toLowerCase() === user.username.toLowerCase();
    return false;
  });

  // ── Open new session ──
  const handleOpenSession = async () => {
    try {
      setSubmitting(true);
      await api.post("/api/cashier-sessions/open", {
        openingBalance: Number(openingBalance) || 0,
        currencyOpeningBalances: FOREIGN_DRAWER_CURRENCIES
          .filter((currency) => Number(foreignOpeningCash[currency]) > 0)
          .map((currency) => ({
            currency,
            openingCash: Number(foreignOpeningCash[currency]),
          })),
        notes: openNotes?.trim() || null,
      });
      toast.success("تم فتح صندوق الكاشير والوردية اليومية بنجاح");
      setShowOpenModal(false);
      setOpeningBalance("");
      setForeignOpeningCash({ SAR: "", USD: "" });
      setOpenNotes("");
      fetchSessions();
    } catch (err) {
      toast.error(extractErrorMessage(err, "فشل في فتح الوردية"));
    } finally {
      setSubmitting(false);
    }
  };

  // ── Close session ──
  const openCloseSessionModal = async (s: CashierSession) => {
    setCloseSession(s);
    const setForeignClosingValues = (activities: CashierForeignCurrencyActivity[]) => {
      setForeignCurrencyActivity(activities);
      setForeignActuals(Object.fromEntries(activities.map((activity) => [
        activity.currency,
        { cash: String(activity.netCash), bank: String(activity.netBank) },
      ])));
    };
    // Fetch session detail to get accurate per-bucket expected values
    try {
      const { data: detail } = await api.get<{
        expectedClosingCash: number;
        expectedClosingCard: number;
        expectedClosingBank: number;
        foreignCurrencyActivity?: CashierForeignCurrencyActivity[];
      }>(`/api/cashier-sessions/${s.id}`);
      setActualCash(String(detail.expectedClosingCash ?? 0));
      setActualCard(String(detail.expectedClosingCard ?? 0));
      setActualBank(String(detail.expectedClosingBank ?? 0));
      setForeignClosingValues(detail.foreignCurrencyActivity ?? []);
    } catch {
      // Fallback: use list values if available
      setActualCash(String(s.expectedClosingCash ?? 0));
      setActualCard(String(s.expectedClosingCard ?? 0));
      setActualBank(String(s.expectedClosingBank ?? 0));
      setForeignClosingValues(s.foreignCurrencyActivity ?? []);
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
        currencyClosings: foreignCurrencyActivity.map((activity) => ({
          currency: activity.currency,
          actualCash: Number(foreignActuals[activity.currency]?.cash) || 0,
          actualBank: Number(foreignActuals[activity.currency]?.bank) || 0,
        })),
      };
      await api.post(`/api/cashier-sessions/close`, payload);
      toast.success("تم إقفال الوردية بنجاح");
      setCloseSession(null);
      setForeignCurrencyActivity([]);
      setForeignActuals({});
      fetchSessions();
    } catch (err) {
      toast.error(extractErrorMessage(err, "فشل في إقفال الوردية"));
    } finally {
      setSubmitting(false);
    }
  };

  // ── Reconcile session ──
  const handleReconcile = async () => {
    if (!confirmReconcile) return;
    try {
      setSubmitting(true);
      await api.patch(`/api/cashier-sessions/${confirmReconcile}/reconcile`);
      toast.success("تم تسوية الوردية بنجاح");
      setConfirmReconcile(null);
      fetchSessions();
    } catch (err) {
      toast.error(extractErrorMessage(err, "فشل في التسوية"));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="p-6 space-y-4">
      <SectionHeader title="الصندوق" action={
        <div className="flex items-center gap-2">
          {!openSession && (
            <button onClick={() => setShowOpenModal(true)} style={btnPrimary}>
              <Unlock className="w-4 h-4" /> فتح وردية
            </button>
          )}
          {openSession && (
            <button onClick={() => openCloseSessionModal(openSession)} style={btnDanger}>
              <Lock className="w-4 h-4" /> إقفال الوردية
            </button>
          )}
          <button onClick={fetchSessions} className="w-8 h-8 rounded-md flex items-center justify-center" style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }} title="تحديث"><RefreshCw className="w-4 h-4" /></button>
        </div>
      } />

      {/* Clarify Cash box (drawer/shift) vs Treasuries (persistent accounts) */}
      <div className="rounded-lg border p-3 text-xs leading-relaxed" style={{ backgroundColor: tokens.infoBg, borderColor: tokens.border, color: tokens.textSecondary }}>
        <span className="font-bold">الصندوق</span> هو درج الكاش اليومي متعدد العملات الذي تُفتح عليه <span className="font-bold">الوردية</span> (شيفت العمل)، ويستقبل YER وSAR وUSD مع حفظ رصيد وحركة وإقفال كل عملة بصورة مستقلة.
        أمّا <span className="font-bold">الخزائن</span> (تبويب «الخزائن») فهي الأرصدة الدائمة المرتبطة بهذه الحركات، ولكل عملة خزينة مستقلة.
      </div>

      {/* Current session info */}
      {openSession && (
        <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.successBg, borderColor: tokens.successBorder }}>
          <div className="flex items-center gap-2 mb-2">
            <CircleDot className="w-4 h-4" style={{ color: tokens.successBorder }} />
            <h4 className="text-sm font-bold" style={{ color: tokens.successBorder }}>وردية مفتوحة</h4>
          </div>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-sm">
            <div><span style={{ color: tokens.textTertiary }}>الكاشر:</span> <span className="font-bold">{openSession.cashierName ?? "—"}</span></div>
            <div><span style={{ color: tokens.textTertiary }}>الافتتاح:</span> <span className="font-bold">{safeFormatDateTime(openSession.openedAt)}</span></div>
              <div><span style={{ color: tokens.textTertiary }}>أرصدة الافتتاح:</span> <span className="font-bold">{formatCurrencyAmounts(getSessionOpeningBalances(openSession))}</span></div>
            </div>
        </div>
      )}

      {/* No open session warning */}
      {!openSession && !loading && (
        <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.warningBg, borderColor: tokens.warningBorder }}>
          <div className="flex items-center gap-2">
            <Vault className="w-4 h-4" style={{ color: tokens.warningText }} />
            <h4 className="text-sm font-bold" style={{ color: tokens.warningText }}>لا توجد وردية مفتوحة</h4>
          </div>
          <p className="text-xs mt-1" style={{ color: tokens.warningText }}>
            يجب فتح وردية (صندوق الكاشير) أولاً قبل تسجيل أي مدفوعات أو عمليات مالية. اضغط &quot;فتح وردية&quot; أعلاه.
          </p>
        </div>
      )}

      {loading ? <LoadingSkeleton /> : sessions.length === 0 ? <EmptyState icon={Vault} message="لا توجد ورديات صندوق" /> : (
        <DataTable<CashierSession>
          keyField="id"
          data={sessions}
          columns={[
            { key: "cashierName", label: "الكاشر" },
            { key: "openedAt", label: "وقت الافتتاح", render: (r) => safeFormatDateTime(r.openedAt) },
            { key: "closingTime", label: "وقت الإقفال", render: (r) => safeFormatDateTime(r.closingTime) },
            { key: "openingBalance", label: "أرصدة الافتتاح", render: (r) => formatCurrencyAmounts(getSessionOpeningBalances(r)) },
            { key: "shortageOrSurplus", label: "عجز/فائض", render: (r) => {
              if (r.shortageOrSurplus == null) return "—";
              if (r.shortageOrSurplus < 0) return <span style={{ color: tokens.dangerBorder, fontWeight: 700 }}>عجز {formatYER(Math.abs(r.shortageOrSurplus))}</span>;
              if (r.shortageOrSurplus > 0) return <span style={{ color: tokens.successBorder, fontWeight: 700 }}>فائض {formatYER(r.shortageOrSurplus)}</span>;
              return <span style={{ color: tokens.successBorder }}>✓</span>;
            }},
            { key: "status", label: "الحالة", render: (r) => <StatusBadge status={r.status} /> },
            { key: "actions", label: "إجراءات", render: (r) => (
              <div className="flex items-center gap-1">
                {r.status === "Open" && <button onClick={(e) => { e.stopPropagation(); openCloseSessionModal(r); }} style={{ color: tokens.dangerBorder }} className="w-7 h-7 rounded-md flex items-center justify-center" title="إقفال"><Lock className="w-3.5 h-3.5" /></button>}
                {r.status === "Closed" && isAdmin && <button onClick={(e) => { e.stopPropagation(); setConfirmReconcile(r.id); }} style={{ color: tokens.brand }} className="w-7 h-7 rounded-md flex items-center justify-center" title="تسوية"><Calculator className="w-3.5 h-3.5" /></button>}
              </div>
            )},
          ]}
        />
      )}

      {/* ═══ Open Session Modal ═══ */}
      <Modal open={showOpenModal} onClose={() => setShowOpenModal(false)} title="فتح وردية جديدة">
        <div className="space-y-4">
          <div className="rounded-md p-3" style={{ backgroundColor: tokens.infoBg, border: `1px solid ${tokens.infoBorder}` }}>
            <p className="text-xs" style={{ color: tokens.infoText }}>
              فتح وردية الكاشير ضروري قبل تسجيل أي مدفوعات. سيتم ربط جميع المعاملات المالية بهذه الوردية تلقائياً.
            </p>
          </div>

          <div>
            <label style={labelStyle}>رصيد افتتاح YER</label>
            <input
              type="number"
              min="0"
              step="0.01"
              value={openingBalance}
              onChange={(e) => setOpeningBalance(e.target.value)}
              dir="ltr"
              placeholder="0"
              style={inputStyle}
            />
            <p className="text-[11px] mt-1" style={{ color: tokens.textTertiary }}>
              أدخل أرصدة النقد الموجودة في الدرج عند بدء الوردية لكل عملة بصورة مستقلة.
            </p>
          </div>

          <div className="grid grid-cols-2 gap-3">
            {FOREIGN_DRAWER_CURRENCIES.map((currency) => (
              <div key={currency}>
                <label style={labelStyle}>رصيد افتتاح {currency}</label>
                <input
                  type="number"
                  min="0"
                  step="0.01"
                  value={foreignOpeningCash[currency] ?? ""}
                  onChange={(event) => setForeignOpeningCash((current) => ({
                    ...current,
                    [currency]: event.target.value,
                  }))}
                  dir="ltr"
                  placeholder="0"
                  style={inputStyle}
                />
              </div>
            ))}
          </div>

          <div>
            <label style={labelStyle}>ملاحظات (اختياري)</label>
            <input
              type="text"
              value={openNotes}
              onChange={(e) => setOpenNotes(e.target.value)}
              placeholder="ملاحظات إضافية..."
              style={inputStyle}
            />
          </div>

          <div className="flex gap-3 pt-2 border-t" style={{ borderColor: tokens.border }}>
            <button onClick={() => setShowOpenModal(false)} style={btnGhost}>إلغاء</button>
            <button onClick={handleOpenSession} disabled={submitting} style={{ ...btnPrimary, opacity: submitting ? 0.6 : 1 }}>
              {submitting && <Loader2 className="w-4 h-4 animate-spin" />}
              {submitting ? "جارٍ الفتح..." : "فتح الوردية"}
            </button>
          </div>
        </div>
      </Modal>

      {/* ═══ Close Session Modal ═══ */}
      <Modal open={!!closeSession} onClose={() => { setCloseSession(null); setForeignCurrencyActivity([]); setForeignActuals({}); }} title="إقفال الوردية">
        {closeSession && (
          <div className="space-y-4">
            <div className="rounded-md p-3" style={{ backgroundColor: tokens.infoBg, border: `1px solid ${tokens.infoBorder}` }}>
              <p className="text-xs" style={{ color: tokens.infoText }}>
                أدخل العد الفعلي لكل وسيلة دفع. سيتم حساب العجز/الفائض تلقائياً بناءً على القيم المتوقعة.
              </p>
            </div>

            {foreignCurrencyActivity.length > 0 && (
              <div className="rounded-md p-3 space-y-2" style={{ backgroundColor: tokens.warningBg, border: `1px solid ${tokens.warningBorder}` }}>
                <p className="text-xs font-bold" style={{ color: tokens.warningText }}>حركات الصندوق حسب العملة</p>
                <p className="text-[11px]" style={{ color: tokens.warningText }}>يُحسب المتوقع والفعلي والعجز أو الفائض لكل عملة مستقلة، ولا تُجمع العملات المختلفة.</p>
                {foreignCurrencyActivity.map((activity) => (
                  <div key={activity.currency} className="border-t pt-2" style={{ borderColor: tokens.warningBorder }}>
                    <div className="flex items-center justify-between text-xs mb-2">
                      <span className="font-semibold">{activity.currency}</span>
                      <span dir="ltr">Cash {formatMoney(activity.netCash, activity.currency)} | Bank {formatMoney(activity.netBank, activity.currency)}</span>
                    </div>
                    <div className="grid grid-cols-2 gap-2">
                      <div>
                        <label style={labelStyle}>النقد الفعلي</label>
                        <input
                          type="number"
                          min="0"
                          step="0.01"
                          value={foreignActuals[activity.currency]?.cash ?? ""}
                          onChange={(event) => setForeignActuals((current) => ({
                            ...current,
                            [activity.currency]: { ...current[activity.currency], cash: event.target.value, bank: current[activity.currency]?.bank ?? "" },
                          }))}
                          dir="ltr"
                          style={inputStyle}
                        />
                      </div>
                      <div>
                        <label style={labelStyle}>البنك الفعلي</label>
                        <input
                          type="number"
                          min="0"
                          step="0.01"
                          value={foreignActuals[activity.currency]?.bank ?? ""}
                          onChange={(event) => setForeignActuals((current) => ({
                            ...current,
                            [activity.currency]: { ...current[activity.currency], cash: current[activity.currency]?.cash ?? "", bank: event.target.value },
                          }))}
                          dir="ltr"
                          style={inputStyle}
                        />
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}

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
