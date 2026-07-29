
import React, { useState } from "react";
import { Loader2, AlertTriangle } from "lucide-react";
import type { CreateCreditNoteRequest } from "./types";
import { Modal, tokens, inputStyle, labelStyle, btnGhost, btnDanger } from "./FinanceSharedUI";
import { formatYER } from "./FinanceHelpers";

/* ═══════════════════════════════════════════════════════════════════════════════
   Create Credit Note Modal
   يظهر عندما يضغط الكاشير على زر "إرجاع مبلغ" بجوار الفاتورة
   ═══════════════════════════════════════════════════════════════════════════════ */

interface Props {
  invoiceId: string;
  invoiceNumber: string;
  invoiceTotal: number;
  paidAmount: number;
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (data: CreateCreditNoteRequest) => Promise<void>;
}

export default function CreateCreditNoteModal({
  invoiceId,
  invoiceNumber,
  invoiceTotal,
  paidAmount,
  isOpen,
  onClose,
  onSubmit,
}: Props) {
  const [amount, setAmount] = useState<string>("");
  const [reason, setReason] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const numericAmount = Number(amount);
  const isAmountValid = amount !== "" && numericAmount > 0 && numericAmount <= paidAmount;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!isAmountValid) {
      return;
    }

    setIsSubmitting(true);
    try {
      await onSubmit({ invoiceId, amount: numericAmount, reason });
      // Reset form on success
      setAmount("");
      setReason("");
      onClose();
    } catch (error) {
      console.error("Error creating credit note:", error);
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleClose = () => {
    if (!isSubmitting) {
      setAmount("");
      setReason("");
      onClose();
    }
  };

  return (
    <Modal open={isOpen} onClose={handleClose} title="إصدار إشعار دائن (مرتجع)">
      <form onSubmit={handleSubmit} className="space-y-4">
        {/* Invoice info */}
        <div
          className="rounded-md p-3 space-y-2"
          style={{ backgroundColor: tokens.infoBg, border: `1px solid ${tokens.infoBorder}` }}
        >
          <div className="flex justify-between items-center">
            <span className="text-xs" style={{ color: tokens.infoText }}>
              الفاتورة
            </span>
            <span className="text-sm font-bold" style={{ color: tokens.textPrimary }}>
              {invoiceNumber}
            </span>
          </div>
          <div className="flex justify-between items-center">
            <span className="text-xs" style={{ color: tokens.infoText }}>
              إجمالي الفاتورة
            </span>
            <span className="text-sm font-bold">{formatYER(invoiceTotal)}</span>
          </div>
          <div className="flex justify-between items-center">
            <span className="text-xs" style={{ color: tokens.infoText }}>
              المدفوع فعلاً
            </span>
            <span className="text-sm font-bold" style={{ color: tokens.successBorder }}>
              {formatYER(paidAmount)}
            </span>
          </div>
        </div>

        {/* Warning if amount exceeds paid */}
        {amount !== "" && numericAmount > paidAmount && (
          <div
            className="rounded-md p-3 flex items-center gap-2"
            style={{ backgroundColor: tokens.dangerBg, border: `1px solid ${tokens.dangerBorder}` }}
          >
            <AlertTriangle className="w-4 h-4 flex-shrink-0" style={{ color: tokens.dangerBorder }} />
            <span className="text-xs" style={{ color: tokens.dangerText }}>
              المبلغ يتجاوز المدفوع ({formatYER(paidAmount)}). لا يمكن إرجاع أكثر مما دفعه المريض.
            </span>
          </div>
        )}

        {/* Amount */}
        <div>
          <label style={labelStyle}>
            المبلغ المراد إرجاعه <span style={{ color: tokens.dangerBorder }}>*</span>
          </label>
          <input
            type="number"
            min="1"
            max={paidAmount}
            step="0.01"
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
            placeholder="0"
            dir="ltr"
            style={inputStyle}
            required
          />
        </div>

        {/* Reason */}
        <div>
          <label style={labelStyle}>
            سبب الاسترجاع <span style={{ color: tokens.dangerBorder }}>*</span>
          </label>
          <textarea
            rows={3}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="مثال: المريض قرر عدم إكمال خطة العلاج..."
            style={{ ...inputStyle, resize: "vertical" }}
            required
          />
        </div>

        {/* Actions */}
        <div className="flex gap-3 pt-2 border-t" style={{ borderColor: tokens.border }}>
          <button type="button" onClick={handleClose} disabled={isSubmitting} style={btnGhost}>
            إلغاء
          </button>
          <button
            type="submit"
            disabled={isSubmitting || !isAmountValid || !reason.trim()}
            style={{
              ...btnDanger,
              opacity: isSubmitting || !isAmountValid || !reason.trim() ? 0.6 : 1,
              cursor: isSubmitting || !isAmountValid || !reason.trim() ? "not-allowed" : "pointer",
            }}
          >
            {isSubmitting && <Loader2 className="w-4 h-4 animate-spin" />}
            {isSubmitting ? "جاري الإصدار..." : "اعتماد المرتجع"}
          </button>
        </div>
      </form>
    </Modal>
  );
}
