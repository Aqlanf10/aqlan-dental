"use client";

import { useState } from "react";
import { AlertTriangle, CheckCircle2, Lock, Unlock, RefreshCw, ChevronDown, ChevronUp } from "lucide-react";
import type { LineItemCommission } from "@/types/commission";
import { useUpdateCommissionCosts, useApproveCommission, useUnlockCommission } from "@/hooks/useCommissions";
import { cn } from "@/lib/utils";

const STATUS_CONFIG: Record<string, { label: string; color: string; dot: string }> = {
  Pending:    { label: "قيد الانتظار",  color: "bg-gray-100 text-gray-700",    dot: "bg-gray-400" },
  Calculated: { label: "محسوبة",         color: "bg-blue-100 text-blue-700",    dot: "bg-blue-500" },
  Approved:   { label: "معتمدة",         color: "bg-green-100 text-green-700",  dot: "bg-green-500" },
  Paid:       { label: "مدفوعة",         color: "bg-emerald-100 text-emerald-700", dot: "bg-emerald-500" },
};

const RECOGNITION_MODE_LABELS: Record<string, string> = {
  OnInvoiceApproval: "عند اعتماد الفاتورة",
  OnPaymentCollection: "عند تحصيل الدفع",
};

function fmt(n: number) {
  return n.toLocaleString("ar-YE", { minimumFractionDigits: 0, maximumFractionDigits: 2 });
}

interface Props {
  commission: LineItemCommission;
  canEdit: boolean;
  canApprove: boolean;
  onUpdated?: () => void;
}

export function CommissionBreakdown({ commission: c, canEdit, canApprove, onUpdated }: Props) {
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState(false);
  const [form, setForm] = useState({
    materialCost:               c.materialCost,
    labCost:                    c.labCost,
    otherDirectCost:            c.otherDirectCost,
    doctorCommissionPercentage: c.doctorCommissionPercentage,
    commissionNotes:            c.commissionNotes ?? "",
  });
  const [err, setErr] = useState("");
  const [validationErrors, setValidationErrors] = useState<Record<string, string>>({});

  const updateMutation  = useUpdateCommissionCosts();
  const approveMutation = useApproveCommission();
  const unlockMutation  = useUnlockCommission();

  const status = STATUS_CONFIG[c.commissionStatus] ?? STATUS_CONFIG.Pending;
  const isLocked = c.isApproved;

  function validateForm(): boolean {
    const errors: Record<string, string> = {};
    if (form.materialCost < 0) errors.materialCost = "تكلفة المواد لا يمكن أن تكون سالبة";
    if (form.labCost < 0) errors.labCost = "أجور المعمل لا يمكن أن تكون سالبة";
    if (form.otherDirectCost < 0) errors.otherDirectCost = "التكاليف الأخرى لا يمكن أن تكون سالبة";
    if (form.doctorCommissionPercentage < 0) errors.doctorCommissionPercentage = "نسبة الطبيب لا يمكن أن تكون سالبة";
    if (form.doctorCommissionPercentage > 100) errors.doctorCommissionPercentage = "نسبة الطبيب لا يمكن أن تتجاوز 100%";
    setValidationErrors(errors);
    return Object.keys(errors).length === 0;
  }

  async function handleSave() {
    if (!validateForm()) return;
    setErr("");
    try {
      await updateMutation.mutateAsync({ lineItemId: c.lineItemId, req: form });
      setEditing(false);
      onUpdated?.();
    } catch (e: unknown) {
      const msg = (e as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setErr(msg ?? "فشل الحفظ. يرجى المحاولة مرة أخرى.");
    }
  }

  async function handleApprove() {
    setErr("");
    try {
      await approveMutation.mutateAsync({ lineItemId: c.lineItemId });
      onUpdated?.();
    } catch (e: unknown) {
      const msg = (e as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setErr(msg ?? "فشل الاعتماد. يرجى المحاولة مرة أخرى.");
    }
  }

  async function handleUnlock() {
    setErr("");
    try {
      await unlockMutation.mutateAsync(c.lineItemId);
      onUpdated?.();
    } catch (e: unknown) {
      const msg = (e as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setErr(msg ?? "فشل فتح العمولة للتعديل.");
    }
  }

  return (
    <div className="border border-gray-100 rounded-xl overflow-hidden shadow-card" dir="rtl">
      {/* Header row */}
      <button
        className={cn(
          "w-full flex items-center justify-between px-4 py-3 transition text-sm",
          open ? "bg-gray-50" : "bg-gray-50/60 hover:bg-gray-100"
        )}
        onClick={() => setOpen(o => !o)}
      >
        <div className="flex items-center gap-2">
          <span className="font-semibold text-gray-700">تفاصيل العمولة</span>
          <span className={cn("text-[11px] px-2 py-0.5 rounded-full font-semibold inline-flex items-center gap-1", status.color)}>
            <span className={cn("w-1.5 h-1.5 rounded-full", status.dot)} />
            {status.label}
          </span>
          {c.labCostMissing && (
            <span className="flex items-center gap-1 text-[11px] text-amber-600 bg-amber-50 px-2 py-0.5 rounded-full">
              <AlertTriangle className="w-3 h-3" /> تكلفة المعمل مفقودة
            </span>
          )}
        </div>
        <div className="flex items-center gap-3">
          <span className="text-gray-500 text-xs">
            مستحق الطبيب: <span className="font-bold text-gray-800">{fmt(c.doctorCommissionAmount)} ر.ي</span>
          </span>
          {open ? <ChevronUp className="w-4 h-4 text-gray-400" /> : <ChevronDown className="w-4 h-4 text-gray-400" />}
        </div>
      </button>

      {open && (
        <div className="p-4 space-y-4 bg-white">
          {/* Breakdown table */}
          {!editing && (
            <div className="space-y-1.5 text-sm">
              {[
                { label: "إجمالي العلاج",          value: c.totalPrice,              neg: false },
                { label: "الخصم",                   value: -c.lineDiscountAmount,      neg: true },
                { label: "تكلفة المواد",            value: -c.materialCost,           neg: true },
                { label: "أجور المعمل",             value: -c.labCost,                neg: true },
                { label: "تكاليف أخرى",             value: -c.otherDirectCost,        neg: true },
              ].map(({ label, value, neg }) => (
                <div key={label} className="flex justify-between py-1.5 border-b border-gray-50">
                  <span className="text-gray-600">{label}</span>
                  <span className={cn("font-medium tabular-nums", neg && value < 0 ? "text-red-600" : "text-gray-800")}>
                    {neg && value < 0 ? `(${fmt(Math.abs(value))})` : fmt(value)} ر.ي
                  </span>
                </div>
              ))}

              {/* Net */}
              <div className={cn(
                "flex justify-between py-2 px-3 rounded-lg font-bold",
                c.netCommissionableAmount < 0
                  ? "bg-red-50 text-red-700"
                  : "bg-blue-50 text-blue-800"
              )}>
                <span>الصافي الخاضع للعمولة</span>
                <span className="tabular-nums">{fmt(c.netCommissionableAmount)} ر.ي</span>
              </div>

              {c.netCommissionableAmount < 0 && (
                <div className="flex items-center gap-1.5 text-xs text-red-600 bg-red-50 rounded-lg px-3 py-2">
                  <AlertTriangle className="w-3.5 h-3.5 flex-shrink-0" />
                  تحذير: الصافي سالب — التكاليف تتجاوز قيمة الخدمة بعد الخصم
                </div>
              )}

              {/* Three-column summary */}
              <div className="grid grid-cols-3 gap-2 pt-1">
                <div className="bg-gray-50 rounded-lg p-2.5 text-center border border-gray-100">
                  <p className="text-[11px] text-gray-500 mb-0.5">نسبة الطبيب</p>
                  <p className="font-bold text-gray-800 tabular-nums">{c.doctorCommissionPercentage}%</p>
                </div>
                <div className="bg-clinic-blue-50 rounded-lg p-2.5 text-center border border-clinic-blue-100">
                  <p className="text-[11px] text-gray-500 mb-0.5">مستحق الطبيب</p>
                  <p className="font-bold text-clinic-blue tabular-nums">{fmt(c.doctorCommissionAmount)} ر.ي</p>
                </div>
                <div className="bg-orange-50 rounded-lg p-2.5 text-center border border-orange-100">
                  <p className="text-[11px] text-gray-500 mb-0.5">نصيب المركز</p>
                  <p className="font-bold text-orange-700 tabular-nums">{fmt(c.centerShareAmount)} ر.ي</p>
                </div>
              </div>

              {/* Recognition mode if available */}
              {(c as LineItemCommission & { commissionRecognitionMode?: string }).commissionRecognitionMode && (
                <div className="text-xs text-gray-400 flex items-center gap-1 pt-1">
                  <span>وضع الاحتساب:</span>
                  <span className="font-medium text-gray-500">
                    {RECOGNITION_MODE_LABELS[(c as LineItemCommission & { commissionRecognitionMode?: string }).commissionRecognitionMode ?? ""] ?? (c as LineItemCommission & { commissionRecognitionMode?: string }).commissionRecognitionMode}
                  </span>
                </div>
              )}

              {c.commissionNotes && (
                <div className="text-xs text-gray-500 bg-gray-50 rounded-lg px-3 py-2 border border-gray-100">
                  <span className="font-medium text-gray-600">ملاحظات:</span> {c.commissionNotes}
                </div>
              )}
            </div>
          )}

          {/* Edit form */}
          {editing && (
            <div className="space-y-3">
              {[
                { key: "materialCost",               label: "تكلفة المواد (ر.ي)", min: 0, max: undefined },
                { key: "labCost",                    label: "أجور المعمل (ر.ي)", min: 0, max: undefined },
                { key: "otherDirectCost",            label: "تكاليف أخرى (ر.ي)", min: 0, max: undefined },
                { key: "doctorCommissionPercentage", label: "نسبة الطبيب (%)", min: 0, max: 100 },
              ].map(({ key, label, min, max }) => (
                <div key={key}>
                  <label className="block text-xs font-semibold text-gray-600 mb-1">{label}</label>
                  <input
                    type="number"
                    min={min}
                    max={max}
                    step="0.01"
                    value={(form as Record<string, number | string>)[key] as number}
                    onChange={e => {
                      setForm(f => ({ ...f, [key]: parseFloat(e.target.value) || 0 }));
                      setValidationErrors(ve => ({ ...ve, [key]: "" }));
                    }}
                    className={cn(
                      "w-full rounded-lg border px-3 py-2 text-sm outline-none transition tabular-nums",
                      validationErrors[key]
                        ? "border-red-300 focus:border-red-500 bg-red-50/30"
                        : "border-gray-200 focus:border-clinic-blue"
                    )}
                  />
                  {validationErrors[key] && (
                    <p className="text-[11px] text-red-600 mt-0.5 flex items-center gap-1">
                      <AlertTriangle className="w-3 h-3" /> {validationErrors[key]}
                    </p>
                  )}
                </div>
              ))}
              <div>
                <label className="block text-xs font-semibold text-gray-600 mb-1">ملاحظات</label>
                <textarea
                  value={form.commissionNotes}
                  onChange={e => setForm(f => ({ ...f, commissionNotes: e.target.value }))}
                  rows={2}
                  className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm outline-none focus:border-clinic-blue resize-none transition"
                  placeholder="ملاحظات اختيارية…"
                />
              </div>
            </div>
          )}

          {err && (
            <div className="flex items-start gap-2 text-xs text-red-600 bg-red-50 rounded-lg px-3 py-2.5">
              <AlertTriangle className="w-3.5 h-3.5 flex-shrink-0 mt-0.5" />
              <span>{err}</span>
            </div>
          )}

          {/* Actions */}
          <div className="flex gap-2 flex-wrap">
            {canEdit && !isLocked && (
              editing ? (
                <>
                  <button
                    onClick={handleSave}
                    disabled={updateMutation.isPending}
                    className="px-3 py-1.5 text-xs font-semibold bg-clinic-blue text-white rounded-lg hover:bg-clinic-blue/90 disabled:opacity-50 transition"
                  >
                    {updateMutation.isPending ? "جاري الحفظ…" : "حفظ التعديلات"}
                  </button>
                  <button
                    onClick={() => { setEditing(false); setErr(""); setValidationErrors({}); }}
                    className="px-3 py-1.5 text-xs font-medium border border-gray-200 text-gray-600 rounded-lg hover:bg-gray-50 transition"
                  >
                    إلغاء
                  </button>
                </>
              ) : (
                <button
                  onClick={() => setEditing(true)}
                  className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium border border-gray-200 text-gray-700 rounded-lg hover:bg-gray-50 transition"
                >
                  <RefreshCw className="w-3.5 h-3.5" /> تعديل التكاليف
                </button>
              )
            )}

            {canApprove && !isLocked && c.commissionStatus === "Calculated" && (
              <button
                onClick={handleApprove}
                disabled={approveMutation.isPending || c.labCostMissing}
                className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold bg-green-600 text-white rounded-lg hover:bg-green-700 disabled:opacity-50 transition"
              >
                <CheckCircle2 className="w-3.5 h-3.5" />
                {approveMutation.isPending ? "جاري الاعتماد…" : "اعتماد العمولة"}
              </button>
            )}
            {c.labCostMissing && c.commissionStatus === "Calculated" && canApprove && (
              <span className="flex items-center gap-1 text-[11px] text-amber-600">
                <AlertTriangle className="w-3 h-3" /> لا يمكن الاعتماد بدون تكلفة المعمل
              </span>
            )}

            {canApprove && isLocked && c.commissionStatus === "Approved" && (
              <button
                onClick={handleUnlock}
                disabled={unlockMutation.isPending}
                className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium border border-amber-300 text-amber-700 bg-amber-50 rounded-lg hover:bg-amber-100 transition"
              >
                <Unlock className="w-3.5 h-3.5" /> فتح للتعديل
              </button>
            )}

            {isLocked && (
              <span className="flex items-center gap-1 text-xs text-gray-400">
                <Lock className="w-3 h-3" /> معتمدة
              </span>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
