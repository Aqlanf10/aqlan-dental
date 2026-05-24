"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import {
  AlertCircle,
  CheckCircle2,
  CreditCard,
  FileText,
  Plus,
  Printer,
  RefreshCw,
  Wallet,
  X,
} from "lucide-react";
import type { FinanceSummary, Payment } from "@/types/finance";
import api from "@/lib/api";
import { cn, formatArabicDate, formatYemeniRiyal } from "@/lib/utils";
import { toast } from "@/stores/toastStore";
import { WorkflowNav, WORKFLOW_LINKS } from "@/components/shared/WorkflowNav";

interface ReadyCheckoutItem {
  appointmentId: string;
  patientId: string;
  patientName: string;
  appointmentTime: string;
  doctorName: string;
  serviceName?: string;
  visitId?: string;
  amountDueReference?: number;
  checkoutStatus?: string;
}

interface DraftInvoiceResult {
  id?: string;
  invoiceId?: string;
  invoiceNumber: string;
  totalAmount?: number;
}

const inputCls =
  "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue";

const paymentMethods = [
  { value: "cash", label: "نقدي" },
  { value: "bank_transfer", label: "تحويل" },
  { value: "card", label: "بطاقة" },
];

export default function FinancePage() {
  const [summary, setSummary] = useState<FinanceSummary | null>(null);
  const [readyItems, setReadyItems] = useState<ReadyCheckoutItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const [selectedItem, setSelectedItem] = useState<ReadyCheckoutItem | null>(null);
  const [amount, setAmount] = useState(0);
  const [paymentMethod, setPaymentMethod] = useState("cash");
  const [notes, setNotes] = useState("");
  const [saving, setSaving] = useState(false);
  const [lastPayment, setLastPayment] = useState<Payment | null>(null);

  const loadFinance = () => {
    setLoading(true);
    setError("");
    Promise.all([
      api.get<FinanceSummary>("/api/finance/summary"),
      api.get<ReadyCheckoutItem[]>("/api/patient-journey/today"),
    ])
      .then(([summaryRes, journeyRes]) => {
        setSummary(summaryRes.data);
        setReadyItems(
          journeyRes.data.filter((item) => item.checkoutStatus === "ReadyForCheckout")
        );
      })
      .catch((err) => {
        const msg =
          err?.response?.data?.message ??
          "تعذر تحميل المالية. تحقق من الاتصال ثم أعد المحاولة.";
        setError(msg);
      })
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    loadFinance();
  }, []);

  const stats = useMemo(
    () => [
      {
        label: "محصل اليوم",
        value: formatYemeniRiyal(summary?.todayCollected ?? 0),
        icon: Wallet,
        className: "bg-clinic-blue-50 text-clinic-blue border-clinic-blue-100",
      },
      {
        label: "جاهز للحساب",
        value: readyItems.length.toString(),
        icon: CreditCard,
        className: "bg-emerald-50 text-emerald-700 border-emerald-100",
      },
      {
        label: "المستحقات",
        value: formatYemeniRiyal(summary?.totalOutstanding ?? 0),
        icon: AlertCircle,
        className: "bg-red-50 text-red-700 border-red-100",
      },
      {
        label: "العقود النشطة",
        value: (summary?.activeContracts ?? 0).toString(),
        icon: FileText,
        className: "bg-slate-50 text-slate-700 border-slate-200",
      },
    ],
    [readyItems.length, summary]
  );

  const openCollection = (item: ReadyCheckoutItem) => {
    setSelectedItem(item);
    setAmount(item.amountDueReference ?? 0);
    setPaymentMethod("cash");
    setNotes("");
    setLastPayment(null);
    setError("");
  };

  const collectPayment = async (completeWithoutPayment = false) => {
    if (!selectedItem) return;
    if (!completeWithoutPayment && amount <= 0) {
      setError("أدخل مبلغ التحصيل أو اختر إنهاء بدون دفع.");
      return;
    }

    setSaving(true);
    setError("");
    try {
      let payment: Payment | null = null;

      if (!completeWithoutPayment) {
        let invoiceId: string | undefined;
        if (selectedItem.visitId) {
          const draft = await api.post<DraftInvoiceResult>(
            `/api/patient-journey/${selectedItem.visitId}/create-draft-invoice`,
            {}
          );
          invoiceId = draft.data.id ?? draft.data.invoiceId;
          if (invoiceId) {
            await api.patch(`/api/invoices/${invoiceId}/issue`, {});
          }
        }

        const paymentRes = await api.post<Payment>("/api/payments", {
          patientId: selectedItem.patientId,
          invoiceId,
          amount,
          paymentMethod,
          serviceDescription:
            selectedItem.serviceName ??
            `تحصيل زيارة ${selectedItem.appointmentTime}`,
          notes: notes || undefined,
        });
        payment = paymentRes.data;
      }

      await api.post(`/api/patient-journey/${selectedItem.appointmentId}/checkout`, {
        paymentAmount: completeWithoutPayment ? null : amount,
        paymentMethod: completeWithoutPayment ? null : paymentMethod,
        notes: notes || null,
      });

      setLastPayment(payment);
      toast.success(
        completeWithoutPayment ? "تم إنهاء الزيارة بدون دفع" : "تم التحصيل وإنهاء الزيارة"
      );
      loadFinance();
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        "فشل التحصيل. راجع البيانات ثم حاول مرة أخرى.";
      setError(msg);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-5 max-w-6xl">
      <div className="flex items-center justify-between gap-3 flex-wrap">
        <div>
          <h1 className="text-2xl font-extrabold text-gray-900">المالية</h1>
          <p className="text-sm text-gray-500 mt-1">
            تحصيل سريع من المرضى الجاهزين للحساب بعد تسليم الطبيب.
          </p>
        </div>
        <div className="flex items-center gap-2 flex-wrap">
          <button
            onClick={loadFinance}
            className="flex items-center gap-2 px-3 py-2 text-sm font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50"
          >
            <RefreshCw className="w-4 h-4" />
            تحديث
          </button>
          <Link
            href="/finance/payments"
            className="flex items-center gap-2 px-3 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90"
          >
            <Plus className="w-4 h-4" />
            دفعة يدوية
          </Link>
          <Link
            href="/finance/invoices"
            className="flex items-center gap-2 px-3 py-2 text-sm font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50"
          >
            <FileText className="w-4 h-4" />
            الفواتير
          </Link>
        </div>
      </div>

      <WorkflowNav
        links={[
          WORKFLOW_LINKS.backToDailyOps(),
          WORKFLOW_LINKS.patientJourney(),
          WORKFLOW_LINKS.invoices(),
          WORKFLOW_LINKS.payments(),
        ]}
        currentPage="/finance"
      />

      {error && (
        <div className="flex items-center gap-3 bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          <AlertCircle className="w-4 h-4 shrink-0" />
          <span className="flex-1">{error}</span>
        </div>
      )}

      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        {stats.map(({ label, value, icon: Icon, className }) => (
          <div key={label} className={cn("rounded-lg border p-4 bg-white", className)}>
            <Icon className="w-5 h-5 mb-2 opacity-80" />
            <p className="text-2xl font-extrabold leading-tight font-mono">{value}</p>
            <p className="text-xs font-medium mt-1 opacity-75">{label}</p>
          </div>
        ))}
      </div>

      <section className="bg-white rounded-lg border border-gray-200 shadow-sm overflow-hidden">
        <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
          <div>
            <h2 className="font-bold text-gray-900">جاهزون للحساب</h2>
            <p className="text-xs text-gray-500 mt-1">هؤلاء تم تسليمهم من الطبيب للاستقبال.</p>
          </div>
          <Link href="/patient-journey" className="text-sm text-clinic-blue hover:underline">
            رحلة المرضى
          </Link>
        </div>

        {loading ? (
          <div className="p-5 space-y-2 animate-pulse">
            {Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className="h-14 rounded-lg bg-gray-100" />
            ))}
          </div>
        ) : readyItems.length === 0 ? (
          <div className="py-12 text-center text-sm text-gray-400">
            لا يوجد مرضى جاهزون للحساب الآن
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-100">
                <tr>
                  {["المريض", "الوقت", "الطبيب", "الخدمة", "المبلغ", "الإجراء"].map((h) => (
                    <th key={h} className="text-start px-4 py-3 text-xs font-semibold text-gray-500">
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {readyItems.map((item) => (
                  <tr key={item.appointmentId} className="hover:bg-gray-50">
                    <td className="px-4 py-3 font-semibold text-gray-900">{item.patientName}</td>
                    <td className="px-4 py-3 font-mono text-xs" dir="ltr">{item.appointmentTime}</td>
                    <td className="px-4 py-3 text-gray-700">{item.doctorName}</td>
                    <td className="px-4 py-3 text-gray-700">{item.serviceName ?? "زيارة"}</td>
                    <td className="px-4 py-3 font-mono font-semibold text-gray-900">
                      {formatYemeniRiyal(item.amountDueReference ?? 0)}
                    </td>
                    <td className="px-4 py-3">
                      <button
                        onClick={() => openCollection(item)}
                        className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-emerald-600 text-white text-xs font-semibold hover:bg-emerald-700"
                      >
                        <CreditCard className="w-3.5 h-3.5" />
                        تحصيل
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className="bg-white rounded-lg border border-gray-200 shadow-sm overflow-hidden">
        <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
          <h2 className="font-bold text-gray-900">آخر الدفعات</h2>
          <Link href="/finance/contracts" className="text-sm text-clinic-blue hover:underline">
            العقود
          </Link>
        </div>
        {!summary?.recentPayments.length ? (
          <div className="py-10 text-center text-sm text-gray-400">لا توجد دفعات مسجلة</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <tbody className="divide-y divide-gray-100">
                {summary.recentPayments.slice(0, 6).map((p) => (
                  <tr key={p.id} className="hover:bg-gray-50">
                    <td className="px-4 py-3 font-medium text-gray-900">{p.patientName}</td>
                    <td className="px-4 py-3 font-mono font-semibold text-green-700">
                      {formatYemeniRiyal(p.amount)}
                    </td>
                    <td className="px-4 py-3 text-gray-600">{formatArabicDate(p.paymentDate)}</td>
                    <td className="px-4 py-3 text-gray-500">{p.receiptNumber ?? "-"}</td>
                    <td className="px-4 py-3">
                      <Link href={`/finance/payments/${p.id}`} className="inline-flex items-center gap-1 text-xs text-clinic-blue hover:underline">
                        <Printer className="w-3.5 h-3.5" />
                        سند
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {selectedItem && (
        <div className="fixed inset-0 bg-black/40 z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-lg p-6 space-y-4">
            <div className="flex items-center justify-between">
              <div>
                <h2 className="text-lg font-bold text-gray-900">تحصيل حساب الزيارة</h2>
                <p className="text-sm text-gray-500 mt-1">{selectedItem.patientName}</p>
              </div>
              <button onClick={() => setSelectedItem(null)} className="p-1 rounded-lg hover:bg-gray-100">
                <X className="w-5 h-5" />
              </button>
            </div>

            {lastPayment ? (
              <div className="space-y-4">
                <div className="rounded-lg bg-green-50 border border-green-200 text-green-800 px-4 py-3 text-sm flex items-center gap-2">
                  <CheckCircle2 className="w-4 h-4" />
                  تم التحصيل. رقم السند: {lastPayment.receiptNumber}
                </div>
                <div className="flex justify-end gap-2">
                  <Link href={`/finance/payments/${lastPayment.id}`} className="px-4 py-2 text-sm font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50">
                    فتح السند
                  </Link>
                  <button onClick={() => setSelectedItem(null)} className="px-4 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white">
                    إغلاق
                  </button>
                </div>
              </div>
            ) : (
              <>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-xs font-medium text-gray-600 mb-1">المبلغ</label>
                    <input type="number" min={0} value={amount} onChange={(e) => setAmount(parseInt(e.target.value) || 0)} className={inputCls} dir="ltr" />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-600 mb-1">طريقة الدفع</label>
                    <select value={paymentMethod} onChange={(e) => setPaymentMethod(e.target.value)} className={inputCls}>
                      {paymentMethods.map((method) => (
                        <option key={method.value} value={method.value}>{method.label}</option>
                      ))}
                    </select>
                  </div>
                  <div className="md:col-span-2">
                    <label className="block text-xs font-medium text-gray-600 mb-1">ملاحظات</label>
                    <textarea value={notes} onChange={(e) => setNotes(e.target.value)} className={cn(inputCls, "h-20 resize-none")} />
                  </div>
                </div>

                <div className="rounded-lg bg-amber-50 border border-amber-200 text-amber-800 px-4 py-3 text-xs">
                  عند التحصيل سيتم إنشاء فاتورة الزيارة، إصدارها، تسجيل الدفعة، ثم إنهاء الزيارة تلقائياً.
                </div>

                <div className="flex justify-end gap-2">
                  <button onClick={() => collectPayment(true)} disabled={saving} className="px-4 py-2 text-sm font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 disabled:opacity-60">
                    إنهاء بدون دفع
                  </button>
                  <button onClick={() => collectPayment(false)} disabled={saving} className="px-4 py-2 text-sm font-medium rounded-lg bg-emerald-600 text-white hover:bg-emerald-700 disabled:opacity-60">
                    {saving ? "جارٍ التحصيل..." : "تحصيل وإنهاء"}
                  </button>
                </div>
              </>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
