"use client";

import { useState } from "react";
import { AlertTriangle, CheckCircle2, Lock, Unlock, RefreshCw, ChevronDown, ChevronUp } from "lucide-react";
import type { LineItemCommission, UpdateLineItemCommissionRequest } from "@/types/commission";
import { useUpdateCommissionCosts, useApproveCommission, useUnlockCommission } from "@/hooks/useCommissions";
import { cn } from "@/lib/utils";

const STATUS_CONFIG = {
  Pending:    { label: "قيد الانتظار",  color: "bg-gray-100 text-gray-600" },
  Calculated: { label: "محسوبة",         color: "bg-blue-100 text-blue-700" },
  Approved:   { label: "معتمدة",         color: "bg-green-100 text-green-700" },
  Paid:       { label: "مدفوعة",         color: "bg-emerald-100 text-emerald-700" },
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

  const updateMutation  = useUpdateCommissionCosts();
  const approveMutation = useApproveCommission();
  const unlockMutation  = useUnlockCommission();

  const status = STATUS_CONFIG[c.commissionStatus] ?? STATUS_CONFIG.Pending;
  const isLocked = c.isApproved;

  async function handleSave() {
    setErr("");
    try {
      await updateMutation.mutateAsync({ lineItemId: c.lineItemId, req: form });
      setEditing(false);
      onUpdated?.();
    } catch (e: unknown) {
      const msg = (e as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setErr(msg ?? "فشل الحفظ");
    }
  }

  async function handleApprove() {
    setErr("");
    try {
      await approveMutation.mutateAsync({ lineItemId: c.lineItemId });
      onUpdated?.();
    } catch (e: unknown) {
      const msg = (e as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setErr(msg ?? "فشل الاعتماد");
    }
  }

  async function handleUnlock() {
    try {
      await unlockMutation.mutateAsync(c.lineItemId);
      onUpdated?.();
    } catch {}
  }

  return (
    <div className="border border-gray-100 rounded-xl overflow-hidden" dir="rtl">
      {/* Header row */}
      <button
        className="w-full flex items-center justify-between px-4 py-3 bg-gray-50 hover:bg-gray-100 transition text-sm"
        onClick={() => setOpen(o => !o)}
      >
        <div className="flex items-center gap-2">
          <span className="font-medium text-gray-700">تفاصيل العمولة</span>
          <span className={cn("text-[11px] px-2 py-0.5 rounded-full font-semibold", status.color)}>
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
                { label: "إجمالي العلاج",          value: c.totalPrice,              highlight: false },
                { label: "الخصم",                   value: -c.lineDiscountAmount,      highlight: false, neg: true },
                { label: "تكلفة المواد",            value: -c.materialCost,           highlight: false, neg: true },
                { label: "أجور المعمل",             value: -c.labCost,                highlight: false, neg: true },
                { label: "تكاليف أخرى",             value: -c.otherDirectCost,        highlight: false, neg: true },
              ].map(({ label, value, neg }) => (
                <div key={label} className="flex justify-between py-1 border-b border-gray-50">
                  <span className="text-gray-600">{label}</span>
                  <span className={cn("font-medium", neg && value < 0 ? "text-red-600" : "text-gray-800")}>
                    {neg && value < 0 ? `(${fmt(Math.abs(value))})` : fmt(value)} ر.ي
                  </span>
                </div>
              ))}

              {/* Net */}
              <div className={cn(
                "flex justify-between py-1.5 px-2 rounded-lg font-bold",
                c.netCommissionableAmount < 0
                  ? "bg-red-50 text-red-700"
                  : "bg-blue-50 text-blue-800"
              )}>
                <span>الصافي الخاضع للنسبة</span>
                <span>{fmt(c.netCommissionableAmount)} ر.ي</span>
              </div>

              {c.netCommissionableAmount < 0 && (
                <p className="text-xs text-red-600 flex items-center gap-1">
                  <AlertTriangle className="w-3.5 h-3.5" />
                  تحذير: الصافي سالب — التكاليف تتجاوز قيمة الخدمة
                </p>
              )}

              <div className="grid grid-cols-3 gap-2 pt-1">
                <div className="bg-gray-50 rounded-lg p-2 text-center">
                  <p className="text-[11px] text-gray-500">نسبة الطبيب</p>
                  <p className="font-bold text-gray-800">{c.doctorCommissionPercentage}%</p>
                </div>
                <div className="bg-clinic-blue-50 rounded-lg p-2 text-center">
                  <p className="text-[11px] text-gray-500">مستحق الطبيب</p>
                  <p className="font-bold text-clinic-blue">{fmt(c.doctorCommissionAmount)} ر.ي</p>
                </div>
                <div className="bg-orange-50 rounded-lg p-2 text-center">
                  <p className="text-[11px] text-gray-500">نصيب المركز</p>
                  <p className="font-bold text-orange-700">{fmt(c.centerShareAmount)} ر.ي</p>
                </div>
              </div>

              {c.commissionNotes && (
                <p className="text-xs text-gray-500 italic pt-1">{c.commissionNotes}</p>
              )}
            </div>
          )}

          {/* Edit form */}
          {editing && (
            <div className="space-y-3">
              {[
                { key: "materialCost",               label: "تكلفة المواد (ر.ي)" },
                { key: "labCost",                    label: "أجور المعمل (ر.ي)" },
                { key: "otherDirectCost",            label: "تكاليف أخرى (ر.ي)" },
                { key: "doctorCommissionPercentage", label: "نسبة الطبيب (%)" },
              ].map(({ key, label }) => (
                <div key={key}>
                  <label className="block text-xs font-medium text-gray-600 mb-1">{label}</label>
                  <input
                    type="number"
                    min={0}
                    step="0.01"
                    value={(form as Record<string, number | string>)[key] as number}
                    onChange={e => setForm(f => ({ ...f, [key]: parseFloat(e.target.value) || 0 }))}
                    className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm outline-none focus:border-clinic-blue"
                  />
                </div>
              ))}
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">ملاحظات</label>
                <input
                  type="text"
                  value={form.commissionNotes}
                  onChange={e => setForm(f => ({ ...f, commissionNotes: e.target.value }))}
                  className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm outline-none focus:border-clinic-blue"
                />
              </div>
            </div>
          )}

          {err && (
            <p className="text-xs text-red-600 bg-red-50 rounded-lg px-3 py-2">{err}</p>
          )}

          {/* Actions */}
          <div className="flex gap-2 flex-wrap">
            {canEdit && !isLocked && (
              editing ? (
                <>
                  <button
                    onClick={handleSave}
                    disabled={updateMutation.isPending}
                    className="px-3 py-1.5 text-xs font-medium bg-clinic-blue text-white rounded-lg hover:bg-clinic-blue/90 disabled:opacity-50"
                  >
                    {updateMutation.isPending ? "جاري الحفظ…" : "حفظ"}
                  </button>
                  <button
                    onClick={() => { setEditing(false); setErr(""); }}
                    className="px-3 py-1.5 text-xs font-medium border border-gray-200 text-gray-600 rounded-lg hover:bg-gray-50"
                  >
                    إلغاء
                  </button>
                </>
              ) : (
                <button
                  onClick={() => setEditing(true)}
                  className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium border border-gray-200 text-gray-700 rounded-lg hover:bg-gray-50"
                >
                  <RefreshCw className="w-3.5 h-3.5" /> تعديل التكاليف
                </button>
              )
            )}

            {canApprove && !isLocked && c.commissionStatus === "Calculated" && (
              <button
                onClick={handleApprove}
                disabled={approveMutation.isPending || c.labCostMissing}
                className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium bg-green-600 text-white rounded-lg hover:bg-green-700 disabled:opacity-50"
              >
                <CheckCircle2 className="w-3.5 h-3.5" />
                {approveMutation.isPending ? "جاري الاعتماد…" : "اعتماد العمولة"}
              </button>
            )}

            {canApprove && isLocked && c.commissionStatus === "Approved" && (
              <button
                onClick={handleUnlock}
                disabled={unlockMutation.isPending}
                className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium border border-amber-300 text-amber-700 bg-amber-50 rounded-lg hover:bg-amber-100"
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
