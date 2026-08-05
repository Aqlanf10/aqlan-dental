"use client";

import React, { useState } from "react";
import { Scale, RefreshCw, AlertTriangle, XCircle } from "lucide-react";
import {
  tokens, SectionHeader, LoadingSkeleton, EmptyState, Modal,
  inputStyle, labelStyle, btnGhost, btnDanger,
} from "./FinanceSharedUI";
import { formatMoney } from "./FinanceHelpers";
import {
  useCommissionAdjustments,
  useBackfillCommissionAdjustments,
  useCancelCommissionAdjustment,
} from "@/hooks/useCommissions";
import type { CommissionAdjustment } from "@/types/commission";
import { toast } from "@/stores/toastStore";

/**
 * CORE-FIN-LAB-ADJ. Correction lines on commissions that were ALREADY PAID.
 *
 * The owner needs to see these separately from the commission table above, because they are not
 * part of any single invoice's arithmetic — they are what the clinic owes (or is owed) on top of
 * it, and they are the only visible trace that a paid commission turned out to be wrong. The
 * original commission line is deliberately left untouched, so without this panel the difference
 * would be invisible.
 */
export function CommissionAdjustmentsPanel({ doctorId }: { doctorId?: string }) {
  const { data: adjustments, isLoading, isError, refetch } =
    useCommissionAdjustments(doctorId ? { doctorId } : undefined);
  const backfill = useBackfillCommissionAdjustments();
  const cancel = useCancelCommissionAdjustment();

  const [cancelTarget, setCancelTarget] = useState<CommissionAdjustment | null>(null);
  const [cancelReason, setCancelReason] = useState("");

  const rows = adjustments ?? [];
  const live = rows.filter((a) => a.status !== "Cancelled");
  const netByCurrency = Array.from(live.reduce((totals, adjustment) => {
    totals.set(adjustment.currency, (totals.get(adjustment.currency) ?? 0) + adjustment.adjustmentAmount);
    return totals;
  }, new Map<string, number>()).entries());
  const pendingCount = rows.filter((a) => a.status === "Pending").length;

  const runBackfill = async () => {
    try {
      const result = await backfill.mutateAsync(doctorId);
      const parts = [
        `فُحص ${result.lineItemsExamined} بندًا`,
        `أُعيد حساب ${result.recalculated}`,
        `أُنشئ ${result.adjustmentsRaised} بند تسوية`,
      ];
      toast.success(parts.join(" — "));

      // Never present a partial sweep as a complete one: anything deliberately left
      // untouched is surfaced, not swallowed.
      if (result.skipped.length > 0) {
        toast.error(
          `تُركت ${result.skipped.length} حالة دون تعديل لتعذّر تحديدها بدقة: ${result.skipped[0]}`
        );
      }
    } catch {
      toast.error("تعذّر تشغيل مزامنة العمولات");
    }
  };

  const confirmCancel = async () => {
    if (!cancelTarget) return;
    if (!cancelReason.trim()) {
      toast.error("سبب الإلغاء مطلوب");
      return;
    }
    try {
      await cancel.mutateAsync({ adjustmentId: cancelTarget.id, reason: cancelReason.trim() });
      toast.success("تم إلغاء بند التسوية");
      setCancelTarget(null);
      setCancelReason("");
    } catch {
      toast.error("تعذّر إلغاء بند التسوية");
    }
  };

  return (
    <div
      className="rounded-lg border"
      style={{ backgroundColor: tokens.card, borderColor: tokens.border, boxShadow: tokens.shadow2 }}
    >
      <div className="p-4 border-b flex items-center justify-between gap-3 flex-wrap" style={{ borderColor: tokens.border }}>
        <SectionHeader title="بنود تسوية العمولات (تغيّر تكلفة المعمل بعد الصرف)" />

        <div className="flex items-center gap-3">
          {netByCurrency.map(([currency, net]) => (
            <span key={currency} className="text-xs font-bold" style={{ color: net < 0 ? tokens.dangerBorder : tokens.successBorder }}>
              صافي التسويات ({currency}): {formatMoney(net, currency)}
            </span>
          ))}
          <button
            type="button"
            onClick={runBackfill}
            disabled={backfill.isPending}
            style={btnGhost}
            className="disabled:opacity-40 disabled:cursor-not-allowed hover:bg-slate-50"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${backfill.isPending ? "animate-spin" : ""}`} />
            <span>مزامنة تكاليف المعامل</span>
          </button>
        </div>
      </div>

      {isLoading ? (
        <div className="p-6">
          <LoadingSkeleton rows={3} />
        </div>
      ) : isError ? (
        // An empty table here would read as "no corrections outstanding", which is the
        // opposite of what a failed load means for money owed.
        <div className="p-6 text-center space-y-3">
          <AlertTriangle className="w-6 h-6 mx-auto" style={{ color: tokens.dangerBorder }} />
          <p className="text-sm font-medium" style={{ color: tokens.dangerBorder }}>
            تعذّر تحميل بنود التسوية — لا يمكن تأكيد عدم وجود فروقات
          </p>
          <button type="button" onClick={() => refetch()} style={btnGhost} className="hover:bg-slate-50 mx-auto">
            <RefreshCw className="w-3.5 h-3.5" />
            <span>إعادة المحاولة</span>
          </button>
        </div>
      ) : rows.length === 0 ? (
        <EmptyState icon={Scale} message="لا توجد فروقات في تكاليف المعامل تستدعي تسوية" />
      ) : (
        <>
          {pendingCount > 0 && (
            <div
              className="mx-4 mt-4 rounded-md border px-3 py-2 text-xs"
              style={{ borderColor: tokens.warningBorder, color: tokens.warningBorder }}
            >
              {pendingCount} بند تسوية بانتظار الصرف — ستُدرج تلقائيًا في التسوية القادمة للطبيب.
            </div>
          )}

          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr style={{ backgroundColor: tokens.cardHover }}>
                  {["الطبيب", "العملة", "المريض", "الخدمة", "طلب المعمل", "تكلفة سابقة", "تكلفة حالية",
                    "عمولة مصروفة", "العمولة الصحيحة", "الفرق", "الحالة", ""].map((h) => (
                    <th
                      key={h}
                      className="text-right px-4 py-3 font-semibold text-xs whitespace-nowrap"
                      style={{ color: tokens.textSecondary }}
                    >
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {rows.map((a) => {
                  const cancelled = a.status === "Cancelled";
                  return (
                    <tr
                      key={a.id}
                      style={{ borderBottom: `1px solid ${tokens.border}`, opacity: cancelled ? 0.5 : 1 }}
                      className="transition-colors hover:bg-slate-50"
                      title={a.reason}
                    >
                      <td className="px-4 py-3 font-medium whitespace-nowrap" style={{ color: tokens.textPrimary }}>
                        {a.doctorName ?? "—"}
                      </td>
                      <td className="px-4 py-3 font-semibold" style={{ color: tokens.brand }}>{a.currency}</td>
                      <td className="px-4 py-3" style={{ color: tokens.textSecondary }}>{a.patientName || "—"}</td>
                      <td className="px-4 py-3" style={{ color: tokens.textSecondary }}>{a.serviceName || "—"}</td>
                      <td className="px-4 py-3 whitespace-nowrap" style={{ color: tokens.textSecondary }}>
                        {a.labOrderNumber ?? "—"}
                      </td>
                      <td className="px-4 py-3" style={{ color: tokens.textSecondary }}>{formatMoney(a.previousLabCost, a.currency)}</td>
                      <td className="px-4 py-3" style={{ color: tokens.textSecondary }}>{formatMoney(a.currentLabCost, a.currency)}</td>
                      <td className="px-4 py-3" style={{ color: tokens.textSecondary }}>{formatMoney(a.paidCommissionAmount, a.currency)}</td>
                      <td className="px-4 py-3" style={{ color: tokens.textSecondary }}>{formatMoney(a.recalculatedCommissionAmount, a.currency)}</td>
                      <td
                        className="px-4 py-3 font-bold whitespace-nowrap"
                        style={{ color: a.adjustmentAmount < 0 ? tokens.dangerBorder : tokens.successBorder }}
                      >
                        {a.adjustmentAmount > 0 ? "+" : ""}{formatMoney(a.adjustmentAmount, a.currency)}
                      </td>
                      <td className="px-4 py-3 text-xs whitespace-nowrap" style={{ color: tokens.textSecondary }}>
                        {a.status === "Pending" ? "بانتظار الصرف"
                          : a.status === "Settled" ? `صُرف${a.settledOn ? ` — ${a.settledOn}` : ""}`
                          : "ملغي"}
                      </td>
                      <td className="px-4 py-3">
                        {a.status === "Pending" && (
                          <button
                            type="button"
                            onClick={() => { setCancelTarget(a); setCancelReason(""); }}
                            className="text-xs hover:underline"
                            style={{ color: tokens.dangerBorder }}
                          >
                            إلغاء
                          </button>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </>
      )}

      <Modal
        open={cancelTarget !== null}
        onClose={() => setCancelTarget(null)}
        title="إلغاء بند تسوية عمولة"
      >
        <div className="space-y-4">
          <p className="text-sm" style={{ color: tokens.textSecondary }}>
            سيُستبعد هذا البند من رصيد الطبيب، ويبقى مسجّلًا في سجل التدقيق. السبب إلزامي.
          </p>
          {cancelTarget && (
            <div className="rounded-md border p-3 text-xs" style={{ borderColor: tokens.border, color: tokens.textSecondary }}>
              {cancelTarget.reason}
            </div>
          )}
          <div>
            <label style={labelStyle}>سبب الإلغاء</label>
            <input
              value={cancelReason}
              onChange={(e) => setCancelReason(e.target.value)}
              style={inputStyle}
              placeholder="مثال: أُدخلت التكلفة خطأً وصُحّحت من المصدر"
            />
          </div>
          <div className="flex items-center justify-end gap-3 pt-2">
            <button type="button" onClick={() => setCancelTarget(null)} style={btnGhost} className="hover:bg-slate-50">
              تراجع
            </button>
            <button
              type="button"
              onClick={confirmCancel}
              disabled={cancel.isPending}
              style={btnDanger}
              className="disabled:opacity-40 disabled:cursor-not-allowed hover:opacity-90"
            >
              <XCircle className="w-3.5 h-3.5" />
              <span>تأكيد الإلغاء</span>
            </button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
