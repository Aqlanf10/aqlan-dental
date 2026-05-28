"use client";

import React, { useState, useMemo } from "react";
import { Loader2 } from "lucide-react";
import { api } from "@/lib/api";
import { toast } from "@/stores/toastStore";
import type { CreateInstallmentPlanRequest } from "./types";
import { Modal, tokens, inputStyle, labelStyle, btnPrimary, btnGhost } from "./FinanceSharedUI";
import { formatYER, extractErrorMessage } from "./FinanceHelpers";

/* ═══════════════════════════════════════════════════════════════════════════════
   CreateInstallmentModal — نافذة إنشاء خطة تقسيط لعقد تقويم
   ═══════════════════════════════════════════════════════════════════════════════
   Features:
   - حساب القسط الشهري اللحظي (Real-time Preview) قبل الحفظ
   - التحقق من أن الدفعة المقدمة لا تتجاوز الإجمالي
   - دمج كامل مع FinanceSharedUI (Modal, tokens, styles)
   - API call to POST /api/finance-v3/contracts/{id}/installments
   ═══════════════════════════════════════════════════════════════════════════════ */

interface Props {
  contractId: string;
  totalAmount: number;
  open: boolean;
  onClose: () => void;
  onCreated?: () => void;
}

export default function CreateInstallmentModal({
  contractId,
  totalAmount,
  open,
  onClose,
  onCreated,
}: Props) {
  const [downPayment, setDownPayment] = useState<number>(0);
  const [numberOfMonths, setNumberOfMonths] = useState<number>(1);
  const [startDate, setStartDate] = useState<string>(
    new Date().toISOString().split("T")[0]
  );
  const [isSubmitting, setIsSubmitting] = useState(false);

  // ── حساب القسط الشهري اللحظي ──
  const previewMonthlyAmount = useMemo(() => {
    if (downPayment >= totalAmount || numberOfMonths <= 0) return 0;
    const remaining = totalAmount - downPayment;
    return remaining / numberOfMonths;
  }, [totalAmount, downPayment, numberOfMonths]);

  // ── التحقق من صحة المدخلات ──
  const isValid =
    downPayment >= 0 &&
    downPayment < totalAmount &&
    numberOfMonths >= 1 &&
    numberOfMonths <= 60 &&
    !!startDate;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!isValid) {
      toast.error("يرجى تصحيح البيانات قبل الحفظ");
      return;
    }

    setIsSubmitting(true);
    try {
      const payload: CreateInstallmentPlanRequest = {
        contractId,
        downPayment,
        numberOfMonths,
        startDate,
      };
      await api.post(
        `/api/finance-v3/contracts/${contractId}/installments`,
        payload
      );
      toast.success("تم إنشاء خطة التقسيط بنجاح");
      onCreated?.();
      onClose();
      // إعادة تعيين الحقول
      setDownPayment(0);
      setNumberOfMonths(1);
      setStartDate(new Date().toISOString().split("T")[0]);
    } catch (err) {
      toast.error(extractErrorMessage(err, "فشل في إنشاء خطة التقسيط"));
    } finally {
      setIsSubmitting(false);
    }
  };

  // ── المبلغ المتبقي ──
  const remainingAmount = Math.max(0, totalAmount - downPayment);

  return (
    <Modal open={open} onClose={onClose} title="جدولة أقساط العقد">
      <form onSubmit={handleSubmit} className="space-y-4">
        {/* ── شريط إجمالي العقد ── */}
        <div
          className="rounded-lg p-3 flex justify-between items-center"
          style={{
            backgroundColor: tokens.brandLight,
            color: tokens.infoText,
          }}
        >
          <span className="font-semibold text-sm">إجمالي العقد:</span>
          <span className="font-bold text-lg">{formatYER(totalAmount)}</span>
        </div>

        {/* ── الدفعة المقدمة ── */}
        <div>
          <label style={labelStyle}>الدفعة المقدمة (Down Payment)</label>
          <input
            type="number"
            min={0}
            max={totalAmount - 1}
            step={1}
            value={downPayment}
            onChange={(e) => setDownPayment(Number(e.target.value))}
            style={inputStyle}
            placeholder="0"
          />
          {downPayment >= totalAmount && downPayment > 0 && (
            <p className="text-xs mt-1" style={{ color: tokens.dangerBorder }}>
              لا يمكن أن تكون الدفعة المقدمة أكبر من أو تساوي إجمالي العقد
            </p>
          )}
        </div>

        {/* ── عدد الأشهر + تاريخ أول قسط ── */}
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label style={labelStyle}>عدد الأشهر</label>
            <input
              type="number"
              min={1}
              max={60}
              value={numberOfMonths}
              onChange={(e) => setNumberOfMonths(Number(e.target.value))}
              style={inputStyle}
            />
          </div>
          <div>
            <label style={labelStyle}>تاريخ أول قسط</label>
            <input
              type="date"
              value={startDate}
              onChange={(e) => setStartDate(e.target.value)}
              style={inputStyle}
            />
          </div>
        </div>

        {/* ── معاينة القسط الشهري ── */}
        <div
          className="rounded-lg p-4"
          style={{
            backgroundColor: tokens.warningBg,
            borderRight: `4px solid ${tokens.warningBorder}`,
          }}
        >
          <div className="flex justify-between items-center">
            <div>
              <p
                className="text-xs"
                style={{ color: tokens.textSecondary }}
              >
                المبلغ المتبقي:
              </p>
              <p
                className="text-sm font-bold"
                style={{ color: tokens.textPrimary }}
              >
                {formatYER(remainingAmount)}
              </p>
            </div>
            <div className="text-left">
              <p
                className="text-xs"
                style={{ color: tokens.textSecondary }}
              >
                القسط الشهري المتوقع:
              </p>
              <p
                className="text-xl font-bold"
                style={{ color: tokens.warningBorder }}
              >
                {formatYER(previewMonthlyAmount)}
              </p>
            </div>
          </div>
        </div>

        {/* ── أزرار التحكم ── */}
        <div className="flex items-center gap-3 pt-2">
          <button type="button" onClick={onClose} style={btnGhost}>
            إلغاء
          </button>
          <button
            type="submit"
            disabled={isSubmitting || !isValid}
            style={{
              ...btnPrimary,
              opacity: isSubmitting || !isValid ? 0.5 : 1,
              cursor: isSubmitting || !isValid ? "not-allowed" : "pointer",
            }}
          >
            {isSubmitting ? (
              <span className="flex items-center gap-2">
                <Loader2 className="w-4 h-4 animate-spin" />
                جاري الحفظ...
              </span>
            ) : (
              "إنشاء الخطة"
            )}
          </button>
        </div>
      </form>
    </Modal>
  );
}
