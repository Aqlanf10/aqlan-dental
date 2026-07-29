/**
 * DirectPaymentModal — collect a payment for any patient (search by name /
 * phone, no appointment required).
 *
 * Extracted from `_components/Modals.tsx` (CLEANUP-1). No behavior changes —
 * pure file extraction. Arabic RTL preserved.
 */


import { useState, useEffect } from "react";
import { CreditCard, Loader2, AlertCircle, FileText, Stethoscope, User } from "lucide-react";
import { PAYMENT_METHODS, inputCls, NAVY, BLUE } from "../../_lib/constants";
import type { PatientListItem } from "@/types/patient";
import { PatientCombobox } from "@/components/shared/PatientCombobox";
import { usePaymentMethodSettings } from "../../_lib/hooks";
import { ModalShell } from "./ModalShell";

interface SearchedPatient {
  id: string;
  name: string;
  phone?: string;
  patientNumber?: string;
}

export function DirectPaymentModal({
  open, onClose, isPending, onConfirm,
}: {
  open: boolean; onClose: () => void;
  isPending: boolean;
  onConfirm: (data: {
    patientId: string; patientName: string;
    amount: number; paymentMethod: string;
    currency?: string; accountCurrency?: string; exchangeRateToAccountCurrency?: number;
    serviceDescription: string; notes: string;
    referenceNumber?: string;
  }) => void;
}) {
  const [selectedPatient, setSelectedPatient] = useState<SearchedPatient | null>(null);

  // Payment form state
  const [amount, setAmount] = useState("");
  const [method, setMethod] = useState("cash");
  const [currency, setCurrency] = useState("YER");
  const [accountCurrency, setAccountCurrency] = useState("YER");
  const [exchangeRate, setExchangeRate] = useState("");
  const [desc, setDesc] = useState("");
  const [notes, setNotes] = useState("");
  const [voucherType, setVoucherType] = useState<"consultation" | "procedure">("consultation");
  const [referenceNumber, setReferenceNumber] = useState("");
  const [referenceError, setReferenceError] = useState("");

  // Dynamic payment methods from API
  const { data: paymentMethodSettings = [] } = usePaymentMethodSettings();
  const activePaymentMethods = paymentMethodSettings.filter(m => m.isActive);
  const selectedMethodSetting = activePaymentMethods.find(m => m.code.toLowerCase() === method.toLowerCase());
  const requiresRef = selectedMethodSetting?.requiresReferenceNumber ?? false;

  // Reset on close
  useEffect(() => {
    if (!open) {
      setSelectedPatient(null);
      setAmount(""); setMethod("cash"); setCurrency("YER"); setAccountCurrency("YER"); setExchangeRate(""); setDesc(""); setNotes(""); setVoucherType("consultation");
      setReferenceNumber(""); setReferenceError("");
    }
  }, [open]);

  const handleSubmit = () => {
    if (!selectedPatient) return;
    const num = parseFloat(amount);
    if (!num || num <= 0) return;
    // Validate reference number if required
    if (requiresRef && !referenceNumber.trim()) {
      setReferenceError("الرقم المرجعي مطلوب لطريقة الدفع هذه");
      return;
    }
    setReferenceError("");
    const voucherPrefix = voucherType === "consultation" ? "[سند معاينة] " : "[إجراءات شغل/خدمة مقدمة] ";
    onConfirm({
      patientId: selectedPatient.id,
      patientName: selectedPatient.name,
      amount: num,
      paymentMethod: method,
      currency,
      accountCurrency,
      exchangeRateToAccountCurrency: exchangeRate ? Number(exchangeRate) : undefined,
      serviceDescription: desc ? voucherPrefix + desc : voucherPrefix.trim(),
      notes: notes,
      referenceNumber: requiresRef ? referenceNumber.trim() : undefined,
    });
  };

  return (
    <ModalShell open={open} onClose={onClose} title="دفع لمريض (بدون موعد)" icon={CreditCard} iconColor="#22c55e" wide>
      {/* Step 1: Search Patient */}
      {!selectedPatient && (
        <div className="space-y-3">
          <div className="p-2.5 rounded-xl flex items-center gap-2" style={{ background: "#f0f5fb", border: "1px solid #3d7ab520" }}>
            <AlertCircle className="w-4 h-4 flex-shrink-0" style={{ color: BLUE }} />
            <span className="text-[11px] font-medium" style={{ color: NAVY }}>
              ابحث عن مريض لتسجيل دفعة مباشرة بدون الحاجة لموعد محجوز
            </span>
          </div>
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>بحث عن مريض *</label>
            <PatientCombobox
              onSelect={(p: PatientListItem) => setSelectedPatient({
                id: p.id,
                name: p.fullName,
                phone: p.phone,
                patientNumber: p.patientNumber,
              })}
              placeholder="اسم المريض أو رقم الهاتف..."
            />
          </div>
        </div>
      )}

      {/* Step 2: Payment Form */}
      {selectedPatient && (
        <div className="space-y-3">
          {/* Selected patient chip */}
          <div className="p-3 rounded-xl flex items-center gap-3" style={{ background: "#f0f5fb" }}>
            <div className="w-9 h-9 rounded-full flex items-center justify-center flex-shrink-0"
              style={{ background: "#22c55e15" }}>
              <User className="w-4 h-4" style={{ color: "#22c55e" }} />
            </div>
            <div className="flex-1 min-w-0">
              <div className="text-sm font-bold truncate" style={{ color: NAVY }}>{selectedPatient.name}</div>
              <div className="text-[10px]" style={{ color: "#94a3b8" }}>
                {selectedPatient.phone && <span>{selectedPatient.phone}</span>}
                {selectedPatient.patientNumber && <span className="mr-2">#{selectedPatient.patientNumber}</span>}
              </div>
            </div>
            <button onClick={() => setSelectedPatient(null)}
              className="px-2 py-1 rounded-lg text-[10px] font-bold"
              style={{ background: "#ef444415", color: "#ef4444", border: "1px solid #ef444430" }}>
              تغيير
            </button>
          </div>

          {/* Voucher Type Selector */}
          <div>
            <label className="text-xs font-semibold block mb-2" style={{ color: NAVY }}>نوع السند *</label>
            <div className="grid grid-cols-2 gap-2">
              <button
                type="button"
                onClick={() => setVoucherType("consultation")}
                className="flex flex-col items-center gap-1.5 p-3 rounded-xl border-2 transition-all text-center"
                style={{
                  background: voucherType === "consultation" ? "#f0f5fb" : "#fff",
                  borderColor: voucherType === "consultation" ? "#3d7ab5" : "#e5e7eb",
                  boxShadow: voucherType === "consultation" ? "0 2px 8px rgba(61,122,181,0.15)" : "none",
                }}>
                <Stethoscope className="w-5 h-5" style={{ color: voucherType === "consultation" ? "#3d7ab5" : "#94a3b8" }} />
                <span className="text-xs font-bold" style={{ color: voucherType === "consultation" ? NAVY : "#94a3b8" }}>سند معاينة</span>
              </button>
              <button
                type="button"
                onClick={() => setVoucherType("procedure")}
                className="flex flex-col items-center gap-1.5 p-3 rounded-xl border-2 transition-all text-center"
                style={{
                  background: voucherType === "procedure" ? "#faf5ff" : "#fff",
                  borderColor: voucherType === "procedure" ? "#9333ea" : "#e5e7eb",
                  boxShadow: voucherType === "procedure" ? "0 2px 8px rgba(147,51,234,0.15)" : "none",
                }}>
                <FileText className="w-5 h-5" style={{ color: voucherType === "procedure" ? "#9333ea" : "#94a3b8" }} />
                <span className="text-xs font-bold" style={{ color: voucherType === "procedure" ? NAVY : "#94a3b8" }}>إجراءات شغل</span>
              </button>
            </div>
          </div>

          {/* Amount */}
                  <div>
                    <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>المبلغ *</label>
            <input type="number" value={amount} onChange={e => setAmount(e.target.value)}
              placeholder="0" className={inputCls()} min={0} step={0.01} dir="ltr" />
                  </div>
                  <div className="grid grid-cols-3 gap-2">
                    <div>
                      <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>عملة الدفع</label>
                      <select value={currency} onChange={e => setCurrency(e.target.value)} className={inputCls()}>
                        <option value="YER">يمني</option>
                        <option value="SAR">سعودي</option>
                        <option value="USD">دولار</option>
                      </select>
                    </div>
                    <div>
                      <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>عملة الحساب</label>
                      <select value={accountCurrency} onChange={e => setAccountCurrency(e.target.value)} className={inputCls()}>
                        <option value="YER">يمني</option>
                        <option value="SAR">سعودي</option>
                        <option value="USD">دولار</option>
                      </select>
                    </div>
                    <div>
                      <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>الصرف</label>
                      <input type="number" value={exchangeRate} onChange={e => setExchangeRate(e.target.value)} placeholder="تلقائي" min="0" step="0.000001" dir="ltr" className={inputCls()} />
                    </div>
                  </div>

          {/* Payment Method */}
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>طريقة الدفع</label>
            <select value={method} onChange={e => { setMethod(e.target.value); setReferenceError(""); }} className={inputCls()}>
              {activePaymentMethods.length > 0 ? (
                activePaymentMethods.map(m => <option key={m.id} value={m.code}>{m.name}</option>)
              ) : (
                PAYMENT_METHODS.map(m => <option key={m.value} value={m.value}>{m.label}</option>)
              )}
            </select>
          </div>

          {/* Reference Number (shown when required) */}
          {requiresRef && (
            <div>
              <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>الرقم المرجعي <span className="text-red-500">*</span></label>
              <input value={referenceNumber} onChange={e => { setReferenceNumber(e.target.value); setReferenceError(""); }}
                placeholder="أدخل الرقم المرجعي" className={inputCls(!!referenceError)} dir="ltr" />
              {referenceError && <p className="text-[10px] text-red-500 mt-1">{referenceError}</p>}
            </div>
          )}

          {/* Service Description */}
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>وصف الخدمة</label>
            <input value={desc} onChange={e => setDesc(e.target.value)}
              placeholder={voucherType === "consultation" ? "مثال: كشف + فحص أشعة" : "مثال: حشوة تجميلية + تنظيف"}
              className={inputCls()} />
          </div>

          {/* Notes */}
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>ملاحظات</label>
            <input value={notes} onChange={e => setNotes(e.target.value)} placeholder="اختياري" className={inputCls()} />
          </div>
        </div>
      )}

      {/* Actions */}
      <div className="flex gap-2 mt-5">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        {selectedPatient && (
          <button onClick={handleSubmit} disabled={!amount || isPending}
            className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
            style={{ background: "#22c55a", opacity: !amount || isPending ? 0.5 : 1 }}>
            {isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <CreditCard className="w-4 h-4" />}
            تسجيل الدفع
          </button>
        )}
      </div>
    </ModalShell>
  );
}
