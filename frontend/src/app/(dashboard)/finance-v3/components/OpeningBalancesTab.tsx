"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { History, Plus, RefreshCw, Truck } from "lucide-react";
import { PatientCombobox } from "@/components/shared/PatientCombobox";
import { api } from "@/lib/api";
import { toast } from "@/stores/toastStore";
import { extractErrorMessage, formatMoney, safeFormatDate } from "./FinanceHelpers";
import {
  EmptyState,
  LoadingSkeleton,
  Modal,
  SectionHeader,
  StatusBadge,
  btnGhost,
  btnPrimary,
  inputStyle,
  labelStyle,
  tokens,
} from "./FinanceSharedUI";

interface OpeningBalanceItem {
  id: string;
  counterpartyId: string;
  counterpartyName: string;
  kind: "PatientReceivable" | "SupplierPayable";
  reference: string;
  amount: number;
  paidAmount: number;
  outstandingAmount: number;
  currency: string;
  exchangeRateToYer: number;
  entryDate: string;
  status: string;
  isReversed: boolean;
  notes: string | null;
}

interface OpeningBalanceTotal {
  kind: string;
  currency: string;
  amount: number;
  paidAmount: number;
  outstandingAmount: number;
}

interface OpeningBalancesResponse {
  data: OpeningBalanceItem[];
  totalsByCurrency: OpeningBalanceTotal[];
}

const today = new Date().toISOString().slice(0, 10);

export function OpeningBalancesTab() {
  const router = useRouter();
  const [items, setItems] = useState<OpeningBalanceItem[]>([]);
  const [totals, setTotals] = useState<OpeningBalanceTotal[]>([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [showCreate, setShowCreate] = useState(false);
  const [patientId, setPatientId] = useState("");
  const [patientDisplay, setPatientDisplay] = useState("");
  const [amount, setAmount] = useState("");
  const [currency, setCurrency] = useState("YER");
  const [exchangeRate, setExchangeRate] = useState("");
  const [balanceDate, setBalanceDate] = useState(today);
  const [reference, setReference] = useState("");
  const [notes, setNotes] = useState("");

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const { data } = await api.get<OpeningBalancesResponse>("/api/finance-v3/opening-balances");
      setItems(data.data ?? []);
      setTotals(data.totalsByCurrency ?? []);
    } catch (error) {
      toast.error(extractErrorMessage(error, "تعذر تحميل الأرصدة السابقة"));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { void load(); }, [load]);

  const reset = () => {
    setPatientId("");
    setPatientDisplay("");
    setAmount("");
    setCurrency("YER");
    setExchangeRate("");
    setBalanceDate(today);
    setReference("");
    setNotes("");
  };

  const submit = async () => {
    if (!patientId || Number(amount) <= 0 || !balanceDate) {
      toast.error("اختر المريض وأدخل المبلغ وتاريخ الرصيد");
      return;
    }
    if (currency !== "YER" && Number(exchangeRate) <= 0) {
      toast.error("أدخل سعر الصرف الفعلي إلى الريال اليمني");
      return;
    }

    setSubmitting(true);
    try {
      await api.post("/api/finance-v3/opening-balances/patient-receivables", {
        patientId,
        amount: Number(amount),
        currency,
        exchangeRateToYer: currency === "YER" ? 1 : Number(exchangeRate),
        balanceDate,
        reference: reference.trim() || null,
        notes: notes.trim() || null,
      });
      toast.success("تم تسجيل مديونية المريض السابقة وترحيل قيدها");
      setShowCreate(false);
      reset();
      await load();
    } catch (error) {
      toast.error(extractErrorMessage(error, "تعذر تسجيل الرصيد السابق"));
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) return <LoadingSkeleton />;

  return (
    <div className="p-6 space-y-4">
      <SectionHeader
        title="الأرصدة والمديونيات السابقة"
        action={(
          <div className="flex items-center gap-2">
            <button type="button" onClick={() => void load()} style={btnGhost} title="تحديث">
              <RefreshCw className="w-4 h-4" />
            </button>
            <button type="button" onClick={() => router.push("/finance-v3?tab=suppliers")} style={btnGhost}>
              <Truck className="w-4 h-4" /> رصيد مورد/معمل
            </button>
            <button type="button" onClick={() => setShowCreate(true)} style={btnPrimary}>
              <Plus className="w-4 h-4" /> مديونية مريض سابقة
            </button>
          </div>
        )}
      />

      {totals.length > 0 && (
        <div className="flex flex-wrap gap-3">
          {totals.map((total) => (
            <div key={`${total.kind}-${total.currency}`} className="border rounded-md px-4 py-3 min-w-52" style={{ borderColor: tokens.border, backgroundColor: tokens.card }}>
              <p className="text-xs" style={{ color: tokens.textTertiary }}>
                {total.kind === "PatientReceivable" ? "مستحق من المرضى" : "مستحق للموردين والمعامل"} - {total.currency}
              </p>
              <p className="font-bold mt-1" style={{ color: tokens.textPrimary }}>{formatMoney(total.outstandingAmount, total.currency)}</p>
              <p className="text-[11px] mt-1" style={{ color: tokens.textTertiary }}>الأصل {formatMoney(total.amount, total.currency)} | المسدد {formatMoney(total.paidAmount, total.currency)}</p>
            </div>
          ))}
        </div>
      )}

      {items.length === 0 ? (
        <EmptyState icon={History} message="لا توجد أرصدة سابقة مسجلة" />
      ) : (
        <div className="overflow-x-auto border rounded-md" style={{ borderColor: tokens.border, backgroundColor: tokens.card }}>
          <table className="w-full text-sm">
            <thead style={{ backgroundColor: tokens.cardHover, color: tokens.textSecondary }}>
              <tr>
                <th className="text-right px-4 py-3 font-semibold">الجهة</th>
                <th className="text-right px-4 py-3 font-semibold">النوع</th>
                <th className="text-right px-4 py-3 font-semibold">المرجع</th>
                <th className="text-right px-4 py-3 font-semibold">التاريخ</th>
                <th className="text-right px-4 py-3 font-semibold">الرصيد الأصلي</th>
                <th className="text-right px-4 py-3 font-semibold">المسدد</th>
                <th className="text-right px-4 py-3 font-semibold">المتبقي</th>
                <th className="text-right px-4 py-3 font-semibold">الحالة</th>
              </tr>
            </thead>
            <tbody>
              {items.map((item) => (
                <tr key={`${item.kind}-${item.id}`} className="border-t" style={{ borderColor: tokens.border }}>
                  <td className="px-4 py-3 font-semibold" style={{ color: tokens.textPrimary }}>{item.counterpartyName}</td>
                  <td className="px-4 py-3" style={{ color: tokens.textSecondary }}>{item.kind === "PatientReceivable" ? "مستحق من مريض" : "مستحق لمورد/معمل"}</td>
                  <td className="px-4 py-3 font-mono text-xs">{item.reference}</td>
                  <td className="px-4 py-3">{safeFormatDate(item.entryDate)}</td>
                  <td className="px-4 py-3 font-mono">{formatMoney(item.amount, item.currency)}</td>
                  <td className="px-4 py-3 font-mono">{formatMoney(item.paidAmount, item.currency)}</td>
                  <td className="px-4 py-3 font-mono font-bold" style={{ color: tokens.brand }}>{formatMoney(item.outstandingAmount, item.currency)}</td>
                  <td className="px-4 py-3"><StatusBadge status={item.isReversed ? "Cancelled" : item.status} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <Modal open={showCreate} onClose={() => { setShowCreate(false); reset(); }} title="إدخال مديونية مريض سابقة" wide>
        <div className="space-y-4">
          <div>
            <label style={labelStyle}>المريض</label>
            <PatientCombobox
              defaultDisplayValue={patientDisplay}
              onSelect={(patient) => {
                setPatientId(patient.id);
                setPatientDisplay(`${patient.fullName} (${patient.patientNumber})`);
              }}
            />
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <div><label style={labelStyle}>المبلغ</label><input type="number" min="0" step="0.01" value={amount} onChange={(event) => setAmount(event.target.value)} dir="ltr" style={inputStyle} /></div>
            <div><label style={labelStyle}>عملة حساب المريض</label><select value={currency} onChange={(event) => { setCurrency(event.target.value); setExchangeRate(""); }} style={inputStyle}><option value="YER">YER</option><option value="SAR">SAR</option><option value="USD">USD</option></select></div>
            <div><label style={labelStyle}>تاريخ الرصيد السابق</label><input type="date" value={balanceDate} onChange={(event) => setBalanceDate(event.target.value)} style={inputStyle} /></div>
          </div>
          {currency !== "YER" && (
            <div>
              <label style={labelStyle}>سعر الصرف الفعلي في تاريخ الإدخال: 1 {currency} = كم YER؟</label>
              <input type="number" min="0" step="0.000001" value={exchangeRate} onChange={(event) => setExchangeRate(event.target.value)} dir="ltr" style={inputStyle} />
            </div>
          )}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div><label style={labelStyle}>مرجع الدفتر أو الملف القديم</label><input value={reference} onChange={(event) => setReference(event.target.value)} style={inputStyle} /></div>
            <div><label style={labelStyle}>ملاحظات</label><input value={notes} onChange={(event) => setNotes(event.target.value)} style={inputStyle} /></div>
          </div>
          <div className="rounded-md border px-3 py-2 text-xs" style={{ backgroundColor: tokens.infoBg, borderColor: tokens.infoBorder, color: tokens.infoText }}>
            سيُنشئ النظام فاتورة رصيد سابق وقيداً متوازناً على ذمة المريض، ولن يعتبر المبلغ إيراداً جديداً. بعد ذلك يظهر المبلغ في حساب المريض ويمكن تحصيله من تبويب التحصيل.
          </div>
          <div className="flex justify-end gap-2 pt-3 border-t" style={{ borderColor: tokens.border }}>
            <button type="button" onClick={() => { setShowCreate(false); reset(); }} style={btnGhost}>إلغاء</button>
            <button type="button" onClick={() => void submit()} disabled={submitting} style={{ ...btnPrimary, opacity: submitting ? 0.6 : 1 }}>
              {submitting ? "جارٍ الترحيل..." : "ترحيل الرصيد السابق"}
            </button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
