"use client";

import React, { useState, useEffect } from "react";
import { Loader2, Wallet } from "lucide-react";
import { api } from "@/lib/api";
import type { ProcessRefundRequest, Treasury } from "./types";
import { Modal, tokens, inputStyle, labelStyle, btnGhost, btnPrimary } from "./FinanceSharedUI";
import { formatYER } from "./FinanceHelpers";

/* ═══════════════════════════════════════════════════════════════════════════════
   Process Refund Modal
   بعد إنشاء "الإشعار الدائن"، يستخدم هذه النافذة لتسليم المريض الكاش
   من الدرج وخصمها من الخزينة
   ═══════════════════════════════════════════════════════════════════════════════ */

interface Props {
  creditNoteId: string;
  refundAmount: number;
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (creditNoteId: string, data: ProcessRefundRequest) => Promise<void>;
}

export default function ProcessRefundModal({
  creditNoteId,
  refundAmount,
  isOpen,
  onClose,
  onSubmit,
}: Props) {
  const [treasuryId, setTreasuryId] = useState<string>("");
  const [notes, setNotes] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [treasuries, setTreasuries] = useState<Treasury[]>([]);
  const [treasuriesLoading, setTreasuriesLoading] = useState(false);

  // جلب الخزائن النشطة عند فتح النافذة
  useEffect(() => {
    if (isOpen) {
      setTreasuriesLoading(true);
      api
        .get<{ data: Treasury[]; total: number }>("/api/finance-v3/treasuries")
        .then((res) => {
          const list = res.data?.data ?? (Array.isArray(res.data) ? (res.data as unknown as Treasury[]) : []);
          setTreasuries(list);
          // اختيار أول خزينة تلقائياً
          if (list.length > 0 && !treasuryId) {
            setTreasuryId(list[0].id);
          }
        })
        .catch(() => {
          setTreasuries([]);
        })
        .finally(() => setTreasuriesLoading(false));
    }
  }, [isOpen]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!treasuryId) return;

    setIsSubmitting(true);
    try {
      await onSubmit(creditNoteId, { treasuryId, notes });
      setNotes("");
      onClose();
    } catch (error) {
      console.error("Error processing refund:", error);
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleClose = () => {
    if (!isSubmitting) {
      setNotes("");
      onClose();
    }
  };

  return (
    <Modal open={isOpen} onClose={handleClose} title="تسليم مرتجع نقدي">
      <form onSubmit={handleSubmit} className="space-y-4">
        {/* Amount display */}
        <div
          className="rounded-md p-4 text-center"
          style={{
            backgroundColor: tokens.warningBg,
            border: `1px solid ${tokens.warningBorder}`,
          }}
        >
          <p className="text-xs mb-1" style={{ color: tokens.textSecondary }}>
            المبلغ المستحق تسليمه للمريض
          </p>
          <p className="text-2xl font-bold" style={{ color: tokens.warningBorder }}>
            {formatYER(refundAmount)}
          </p>
        </div>

        {/* Treasury selector */}
        <div>
          <label style={labelStyle}>
            السحب من الخزينة <span style={{ color: tokens.dangerBorder }}>*</span>
          </label>
          {treasuriesLoading ? (
            <div className="flex items-center gap-2 text-xs" style={{ color: tokens.textTertiary }}>
              <Loader2 className="w-3.5 h-3.5 animate-spin" /> جاري تحميل الخزائن...
            </div>
          ) : treasuries.length === 0 ? (
            <div
              className="rounded-md p-3 text-xs"
              style={{ backgroundColor: tokens.dangerBg, color: tokens.dangerText, border: `1px solid ${tokens.dangerBorder}` }}
            >
              لا توجد خزائن نشطة. يرجى إنشاء خزينة أولاً.
            </div>
          ) : (
            <select
              value={treasuryId}
              onChange={(e) => setTreasuryId(e.target.value)}
              style={inputStyle}
              required
            >
              {treasuries.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.name} ({formatYER(t.balance)})
                </option>
              ))}
            </select>
          )}
        </div>

        {/* Notes */}
        <div>
          <label style={labelStyle}>ملاحظات الصرف (اختياري)</label>
          <input
            type="text"
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            placeholder="مثال: تم تسليم المبلغ نقداً للمريض..."
            style={inputStyle}
          />
        </div>

        {/* Selected treasury balance hint */}
        {treasuryId && (() => {
          const selected = treasuries.find((t) => t.id === treasuryId);
          if (!selected) return null;
          return (
            <div
              className="rounded-md p-2 flex items-center gap-2"
              style={{ backgroundColor: tokens.brandLight, border: `1px solid ${tokens.infoBorder}` }}
            >
              <Wallet className="w-3.5 h-3.5 flex-shrink-0" style={{ color: tokens.brand }} />
              <span className="text-xs" style={{ color: tokens.infoText }}>
                رصيد {selected.name}: <strong>{formatYER(selected.balance)}</strong>
                {selected.balance < refundAmount && (
                  <span style={{ color: tokens.dangerText, marginRight: 8 }}>
                    ⚠ الرصيد غير كافٍ
                  </span>
                )}
              </span>
            </div>
          );
        })()}

        {/* Actions */}
        <div className="flex gap-3 pt-2 border-t" style={{ borderColor: tokens.border }}>
          <button type="button" onClick={handleClose} disabled={isSubmitting} style={btnGhost}>
            إلغاء
          </button>
          <button
            type="submit"
            disabled={isSubmitting || !treasuryId || treasuries.length === 0}
            style={{
              ...btnPrimary,
              opacity: isSubmitting || !treasuryId || treasuries.length === 0 ? 0.6 : 1,
              cursor: isSubmitting || !treasuryId || treasuries.length === 0 ? "not-allowed" : "pointer",
            }}
          >
            {isSubmitting && <Loader2 className="w-4 h-4 animate-spin" />}
            {isSubmitting ? "جاري السحب..." : "تأكيد السحب من الخزينة"}
          </button>
        </div>
      </form>
    </Modal>
  );
}
