"use client";

import React, { useState } from "react";
import { CheckCircle, Clock, AlertTriangle, Loader2 } from "lucide-react";
import { api } from "@/lib/api";
import { toast } from "@/stores/toastStore";
import type { InstallmentDto, PayInstallmentRequest } from "./types";
import { INSTALLMENT_STATUS_MAP } from "./types";
import { tokens, inputStyle, labelStyle, btnPrimary, btnGhost, Modal, ConfirmDialog } from "./FinanceSharedUI";
import { formatYER, safeFormatDate, extractErrorMessage } from "./FinanceHelpers";

/* ═══════════════════════════════════════════════════════════════════════════════
   InstallmentCard — بطاقة قسط واحد مع زر سداد
   ═══════════════════════════════════════════════════════════════════════════════ */

interface Props {
  installment: InstallmentDto;
  onPaid?: () => void;
}

export default function InstallmentCard({ installment, onPaid }: Props) {
  const [showPayModal, setShowPayModal] = useState(false);
  const [paymentMethod, setPaymentMethod] = useState("cash");
  const [notes, setNotes] = useState("");
  const [isPaying, setIsPaying] = useState(false);

  const statusCfg = INSTALLMENT_STATUS_MAP[installment.status] ?? {
    bg: tokens.cardHover,
    text: tokens.textSecondary,
    label: installment.status,
  };

  const statusIcon =
    installment.status === "Paid" ? (
      <CheckCircle className="w-4 h-4" style={{ color: tokens.successBorder }} />
    ) : installment.status === "Overdue" ? (
      <AlertTriangle className="w-4 h-4" style={{ color: tokens.dangerBorder }} />
    ) : (
      <Clock className="w-4 h-4" style={{ color: tokens.warningBorder }} />
    );

  const handlePay = async () => {
    setIsPaying(true);
    try {
      const payload: PayInstallmentRequest = { paymentMethod, notes: notes || undefined };
      await api.post(`/api/finance-v3/installments/${installment.id}/pay`, payload);
      toast.success("تم سداد القسط بنجاح");
      setShowPayModal(false);
      onPaid?.();
    } catch (err) {
      toast.error(extractErrorMessage(err, "فشل في سداد القسط"));
    } finally {
      setIsPaying(false);
    }
  };

  return (
    <>
      <div
        className="rounded-lg border p-4 flex items-center gap-4"
        style={{
          backgroundColor: tokens.card,
          borderColor: tokens.border,
        }}
      >
        {/* حالة القسط */}
        <div className="flex-shrink-0">{statusIcon}</div>

        {/* تفاصيل القسط */}
        <div className="flex-1 min-w-0">
          <div className="flex items-center justify-between mb-1">
            <span className="text-sm font-bold" style={{ color: tokens.textPrimary }}>
              {formatYER(installment.amount)}
            </span>
            <span
              className="inline-flex text-[11px] font-semibold px-2 py-0.5 rounded-full"
              style={{ backgroundColor: statusCfg.bg, color: statusCfg.text }}
            >
              {statusCfg.label}
            </span>
          </div>
          <div className="flex items-center gap-4 text-xs" style={{ color: tokens.textSecondary }}>
            <span>استحقاق: {safeFormatDate(installment.dueDate)}</span>
            {installment.paidDate && (
              <span style={{ color: tokens.successBorder }}>
                سُدد: {safeFormatDate(installment.paidDate)}
              </span>
            )}
          </div>
        </div>

        {/* زر السداد */}
        {installment.status === "Pending" || installment.status === "Overdue" ? (
          <button
            onClick={() => setShowPayModal(true)}
            className="flex-shrink-0 px-3 py-1.5 rounded-md text-xs font-semibold text-white transition-colors"
            style={{
              backgroundColor: tokens.successBorder,
              opacity: 1,
            }}
            onMouseEnter={(e) => { e.currentTarget.style.opacity = "0.85"; }}
            onMouseLeave={(e) => { e.currentTarget.style.opacity = "1"; }}
          >
            سداد
          </button>
        ) : null}
      </div>

      {/* نافذة سداد القسط */}
      <Modal open={showPayModal} onClose={() => setShowPayModal(false)} title="سداد القسط">
        <div className="space-y-4">
          <div
            className="rounded-lg p-3 text-center"
            style={{ backgroundColor: tokens.brandLight }}
          >
            <p className="text-xs" style={{ color: tokens.textSecondary }}>
              مبلغ القسط
            </p>
            <p className="text-xl font-bold" style={{ color: tokens.brand }}>
              {formatYER(installment.amount)}
            </p>
          </div>

          <div>
            <label style={labelStyle}>طريقة الدفع</label>
            <select
              value={paymentMethod}
              onChange={(e) => setPaymentMethod(e.target.value)}
              style={inputStyle}
            >
              <option value="cash">نقدي</option>
              <option value="card">بطاقة</option>
              <option value="bank_transfer">تحويل بنكي</option>
            </select>
          </div>

          <div>
            <label style={labelStyle}>ملاحظات (اختياري)</label>
            <input
              type="text"
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="رقم إيصال، ملاحظة..."
              style={inputStyle}
            />
          </div>

          <div className="flex items-center gap-3 pt-2">
            <button type="button" onClick={() => setShowPayModal(false)} style={btnGhost}>
              إلغاء
            </button>
            <button
              onClick={handlePay}
              disabled={isPaying}
              style={{
                ...btnPrimary,
                backgroundColor: tokens.successBorder,
                opacity: isPaying ? 0.5 : 1,
                cursor: isPaying ? "not-allowed" : "pointer",
              }}
            >
              {isPaying ? (
                <span className="flex items-center gap-2">
                  <Loader2 className="w-4 h-4 animate-spin" />
                  جاري السداد...
                </span>
              ) : (
                "تأكيد السداد"
              )}
            </button>
          </div>
        </div>
      </Modal>
    </>
  );
}
