"use client";

import React, { useState, useEffect, useMemo } from "react";
import { Loader2, Plus, Trash2, Shield, Receipt } from "lucide-react";
import { api } from "@/lib/api";
import { toast } from "@/stores/toastStore";
import type { InsuranceCompanyDto } from "./types";
import {
  Modal,
  tokens,
  inputStyle,
  labelStyle,
  btnPrimary,
  btnGhost,
} from "./FinanceSharedUI";
import { formatYER, extractErrorMessage } from "./FinanceHelpers";

/* ═══════════════════════════════════════════════════════════════════════════════
   CreateInvoiceModal — إنشاء فاتورة مع دعم التأمين والضرائب (Phase 4)
   ═══════════════════════════════════════════════════════════════════════════════
   Features:
   - إضافة بنود (Line Items) ديناميكياً
   - حساب الضريبة كنسبة مئوية
   - اختيار شركة التأمين مع تعديل نسبة التغطية
   - معاينة مالية لحظية (subTotal / tax / grossTotal / covered / co-pay)
   - إرسال البيانات إلى POST /api/invoices
   ═══════════════════════════════════════════════════════════════════════════════ */

interface LineItem {
  key: string;
  description: string;
  quantity: number;
  unitPrice: number;
}

interface Props {
  patientId: string;
  patientName: string;
  open: boolean;
  onClose: () => void;
  onCreated?: () => void;
}

let itemKeyCounter = 0;

export default function CreateInvoiceModal({
  patientId,
  patientName,
  open,
  onClose,
  onCreated,
}: Props) {
  // ── Line Items ──
  const [items, setItems] = useState<LineItem[]>([
    { key: `item-${++itemKeyCounter}`, description: "", quantity: 1, unitPrice: 0 },
  ]);

  // ── Discount & Tax ──
  const [discountAmount, setDiscountAmount] = useState<number>(0);
  const [taxPercentage, setTaxPercentage] = useState<number>(0);

  // ── Insurance ──
  const [insuranceCompanies, setInsuranceCompanies] = useState<
    InsuranceCompanyDto[]
  >([]);
  const [selectedInsuranceId, setSelectedInsuranceId] = useState<string>("");
  const [customCoverage, setCustomCoverage] = useState<number | "">("");

  // ── Notes & Submitting ──
  const [notes, setNotes] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  // ── Fetch insurance companies on open ──
  useEffect(() => {
    if (!open) return;
    api
      .get<{ data: InsuranceCompanyDto[] }>(
        "/api/finance-v3/insurance-companies"
      )
      .then(({ data: res }) => {
        const list = Array.isArray(res)
          ? res
          : (res as { data?: InsuranceCompanyDto[] })?.data ?? [];
        setInsuranceCompanies(
          list.filter((c) => c.isActive)
        );
      })
      .catch(() => {
        // قد لا يكون الـ endpoint موجوداً بعد — لا مشكلة
      });
  }, [open]);

  // ── Real-time financial preview ──
  const financePreview = useMemo(() => {
    const subTotal = items.reduce(
      (acc, item) => acc + item.quantity * item.unitPrice,
      0
    );
    const taxAmount = subTotal * (taxPercentage / 100);
    const grossTotal = subTotal + taxAmount - discountAmount;

    let coveredAmount = 0;
    let patientCoPay = grossTotal;
    let coveragePercent = 0;

    if (selectedInsuranceId) {
      const company = insuranceCompanies.find(
        (c) => c.id === selectedInsuranceId
      );
      if (company) {
        coveragePercent =
          customCoverage !== "" ? Number(customCoverage) : company.defaultCoveragePercentage;
        coveredAmount = grossTotal * (coveragePercent / 100);
        patientCoPay = grossTotal - coveredAmount;
      }
    }

    return {
      subTotal,
      taxAmount,
      grossTotal: Math.max(0, grossTotal),
      coveredAmount: Math.max(0, coveredAmount),
      patientCoPay: Math.max(0, patientCoPay),
      coveragePercent,
    };
  }, [items, taxPercentage, discountAmount, selectedInsuranceId, customCoverage, insuranceCompanies]);

  // ── Line item helpers ──
  const addItem = () => {
    setItems((prev) => [
      ...prev,
      {
        key: `item-${++itemKeyCounter}`,
        description: "",
        quantity: 1,
        unitPrice: 0,
      },
    ]);
  };

  const removeItem = (key: string) => {
    setItems((prev) => prev.filter((i) => i.key !== key));
  };

  const updateItem = (
    key: string,
    field: keyof LineItem,
    value: string | number
  ) => {
    setItems((prev) =>
      prev.map((i) => (i.key === key ? { ...i, [field]: value } : i))
    );
  };

  // ── Validation ──
  const hasValidItems = items.some(
    (i) => i.description.trim() !== "" && i.unitPrice > 0
  );
  const isValid = hasValidItems && financePreview.grossTotal > 0;

  // ── Submit ──
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!isValid) {
      toast.error("يرجى إضافة بند واحد على الأقل بسعر صحيح");
      return;
    }

    setIsSubmitting(true);
    try {
      const payload = {
        patientId,
        lineItems: items
          .filter((i) => i.description.trim() !== "" && i.unitPrice > 0)
          .map((i) => ({
            description: i.description,
            quantity: i.quantity,
            unitPrice: i.unitPrice,
          })),
        discountAmount: discountAmount > 0 ? discountAmount : undefined,
        taxPercentage,
        insuranceCompanyId: selectedInsuranceId || undefined,
        customCoveragePercentage:
          customCoverage !== "" ? Number(customCoverage) : undefined,
        notes: notes || undefined,
      };

      await api.post("/api/invoices", payload);
      toast.success("تم إصدار الفاتورة بنجاح");
      onCreated?.();
      onClose();
      // Reset
      setItems([{ key: `item-${++itemKeyCounter}`, description: "", quantity: 1, unitPrice: 0 }]);
      setDiscountAmount(0);
      setTaxPercentage(0);
      setSelectedInsuranceId("");
      setCustomCoverage("");
      setNotes("");
    } catch (err) {
      toast.error(extractErrorMessage(err, "فشل في إصدار الفاتورة"));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Modal open={open} onClose={onClose} title="إصدار فاتورة جديدة" wide>
      <form onSubmit={handleSubmit} className="space-y-5">
        {/* ── Patient info ── */}
        <div
          className="rounded-lg p-3 flex items-center gap-3"
          style={{ backgroundColor: tokens.brandLight }}
        >
          <Receipt className="w-5 h-5" style={{ color: tokens.brand }} />
          <div>
            <p className="text-xs" style={{ color: tokens.textTertiary }}>
              المريض
            </p>
            <p
              className="text-sm font-bold"
              style={{ color: tokens.brand }}
            >
              {patientName}
            </p>
          </div>
        </div>

        {/* ── Line Items ── */}
        <div>
          <div className="flex items-center justify-between mb-2">
            <label style={{ ...labelStyle, marginBottom: 0 }}>بنود الفاتورة</label>
            <button
              type="button"
              onClick={addItem}
              className="inline-flex items-center gap-1 text-xs font-medium px-2 py-1 rounded-md"
              style={{
                color: tokens.brand,
                backgroundColor: tokens.brandLight,
                border: "none",
                cursor: "pointer",
              }}
            >
              <Plus className="w-3 h-3" /> إضافة بند
            </button>
          </div>

          <div className="space-y-2">
            {items.map((item, idx) => (
              <div key={item.key} className="flex items-center gap-2">
                <span
                  className="text-xs font-mono w-6 text-center flex-shrink-0"
                  style={{ color: tokens.textTertiary }}
                >
                  {idx + 1}
                </span>
                <input
                  type="text"
                  value={item.description}
                  onChange={(e) =>
                    updateItem(item.key, "description", e.target.value)
                  }
                  placeholder="وصف الخدمة"
                  style={{ ...inputStyle, flex: 2, fontSize: 13 }}
                />
                <input
                  type="number"
                  min={1}
                  value={item.quantity}
                  onChange={(e) =>
                    updateItem(item.key, "quantity", Number(e.target.value))
                  }
                  style={{ ...inputStyle, width: 60, fontSize: 13 }}
                  title="الكمية"
                />
                <input
                  type="number"
                  min={0}
                  value={item.unitPrice}
                  onChange={(e) =>
                    updateItem(item.key, "unitPrice", Number(e.target.value))
                  }
                  placeholder="سعر الوحدة"
                  style={{ ...inputStyle, flex: 1, fontSize: 13 }}
                />
                <span
                  className="text-xs font-bold flex-shrink-0 w-24 text-left"
                  style={{ color: tokens.textPrimary }}
                >
                  {formatYER(item.quantity * item.unitPrice)}
                </span>
                {items.length > 1 && (
                  <button
                    type="button"
                    onClick={() => removeItem(item.key)}
                    className="flex-shrink-0 w-7 h-7 rounded-md flex items-center justify-center"
                    style={{
                      color: tokens.dangerBorder,
                      backgroundColor: "transparent",
                      border: `1px solid ${tokens.dangerBorder}`,
                      cursor: "pointer",
                    }}
                    title="حذف البند"
                  >
                    <Trash2 className="w-3 h-3" />
                  </button>
                )}
              </div>
            ))}
          </div>
        </div>

        {/* ── Discount & Tax ── */}
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label style={labelStyle}>مبلغ الخصم</label>
            <input
              type="number"
              min={0}
              value={discountAmount}
              onChange={(e) => setDiscountAmount(Number(e.target.value))}
              style={inputStyle}
              placeholder="0"
            />
          </div>
          <div>
            <label style={labelStyle}>نسبة الضريبة (VAT %)</label>
            <input
              type="number"
              min={0}
              max={100}
              value={taxPercentage}
              onChange={(e) => setTaxPercentage(Number(e.target.value))}
              style={inputStyle}
              placeholder="0"
            />
          </div>
        </div>

        {/* ── Insurance Section ── */}
        <div
          className="rounded-xl border p-4 space-y-3"
          style={{
            borderColor: selectedInsuranceId ? tokens.brand : tokens.border,
            backgroundColor: selectedInsuranceId ? tokens.brandLight : tokens.card,
          }}
        >
          <div className="flex items-center gap-2 mb-1">
            <Shield className="w-4 h-4" style={{ color: tokens.brand }} />
            <span
              className="text-xs font-semibold"
              style={{ color: tokens.textSecondary }}
            >
              تغطية التأمين (اختياري)
            </span>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label style={labelStyle}>شركة التأمين</label>
              <select
                value={selectedInsuranceId}
                onChange={(e) => {
                  setSelectedInsuranceId(e.target.value);
                  setCustomCoverage("");
                }}
                style={inputStyle}
              >
                <option value="">-- الدفع نقداً (بدون تأمين) --</option>
                {insuranceCompanies.map((company) => (
                  <option key={company.id} value={company.id}>
                    {company.name} ({company.defaultCoveragePercentage}%)
                  </option>
                ))}
              </select>
            </div>

            {selectedInsuranceId && (
              <div>
                <label style={labelStyle}>
                  تعديل نسبة التغطية لهذه الحالة
                </label>
                <input
                  type="number"
                  min={0}
                  max={100}
                  value={customCoverage}
                  onChange={(e) =>
                    setCustomCoverage(
                      e.target.value !== "" ? Number(e.target.value) : ""
                    )
                  }
                  placeholder={`الافتراضي: ${
                    insuranceCompanies.find(
                      (c) => c.id === selectedInsuranceId
                    )?.defaultCoveragePercentage ?? 0
                  }%`}
                  style={inputStyle}
                />
              </div>
            )}
          </div>
        </div>

        {/* ── Financial Preview (Real-time) ── */}
        <div
          className="rounded-lg border p-4 space-y-2"
          style={{
            backgroundColor: tokens.card,
            borderColor: tokens.border,
          }}
        >
          <h4
            className="text-xs font-semibold mb-3"
            style={{ color: tokens.textSecondary }}
          >
            معاينة الفاتورة
          </h4>

          {/* Subtotal */}
          <div
            className="flex justify-between text-sm"
            style={{ color: tokens.textSecondary }}
          >
            <span>الإجمالي الفرعي:</span>
            <span>{formatYER(financePreview.subTotal)}</span>
          </div>

          {/* Tax */}
          {taxPercentage > 0 && (
            <div
              className="flex justify-between text-sm"
              style={{ color: tokens.textSecondary }}
            >
              <span>الضريبة ({taxPercentage}%):</span>
              <span>+ {formatYER(financePreview.taxAmount)}</span>
            </div>
          )}

          {/* Discount */}
          {discountAmount > 0 && (
            <div
              className="flex justify-between text-sm"
              style={{ color: tokens.textSecondary }}
            >
              <span>الخصم:</span>
              <span>- {formatYER(discountAmount)}</span>
            </div>
          )}

          {/* Gross Total */}
          <div
            className="flex justify-between font-bold text-base pt-2 border-t"
            style={{
              color: tokens.textPrimary,
              borderColor: tokens.border,
            }}
          >
            <span>الإجمالي الكلي:</span>
            <span>{formatYER(financePreview.grossTotal)}</span>
          </div>

          {/* Insurance Split */}
          {selectedInsuranceId && financePreview.coveredAmount > 0 && (
            <div
              className="mt-3 p-4 rounded-lg border flex justify-between items-center"
              style={{
                backgroundColor: tokens.brandLight,
                borderColor: tokens.infoBorder,
              }}
            >
              <div>
                <p
                  className="text-xs font-medium"
                  style={{ color: tokens.brand }}
                >
                  يتحمله التأمين ({financePreview.coveragePercent}%):
                </p>
                <p
                  className="text-lg font-bold"
                  style={{ color: tokens.brand }}
                >
                  {formatYER(financePreview.coveredAmount)}
                </p>
              </div>
              <div className="text-left">
                <p
                  className="text-xs font-medium"
                  style={{ color: tokens.warningBorder }}
                >
                  يتحمله المريض (Co-pay):
                </p>
                <p
                  className="text-2xl font-bold"
                  style={{ color: tokens.warningBorder }}
                >
                  {formatYER(financePreview.patientCoPay)}
                </p>
              </div>
            </div>
          )}
        </div>

        {/* ── Notes ── */}
        <div>
          <label style={labelStyle}>ملاحظات (اختياري)</label>
          <input
            type="text"
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            placeholder="ملاحظات إضافية..."
            style={inputStyle}
          />
        </div>

        {/* ── Actions ── */}
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
                جاري الإصدار...
              </span>
            ) : (
              "إصدار الفاتورة"
            )}
          </button>
        </div>
      </form>
    </Modal>
  );
}
