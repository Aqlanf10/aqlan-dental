"use client";

import { useEffect, useState, useCallback } from "react";
import api from "@/lib/api";
import {
  Wallet, CreditCard, TrendingUp, AlertTriangle,
  RefreshCw, Loader2, FileText, CheckCircle, Clock,
  DollarSign, ArrowUpRight, ArrowDownRight, Users,
  Receipt, IndianRupee,
} from "lucide-react";
import { NAVY, BLUE, ORANGE, fmtRial } from "../_lib/constants";
import { toast } from "@/stores/toastStore";
import { useQueryClient } from "@tanstack/react-query";

/* ─── Types ────────────────────────────────────────────────────────────────── */
interface FinanceSummary {
  todayCollected: number;
  monthCollected: number;
  totalOutstanding: number;
  activeContracts: number;
  unpaidInvoicesCount: number;
  draftInvoicesCount: number;
  overdueAmount: number;
  pendingCommissionsAmount: number;
  recentPayments?: PaymentItem[];
  recentInvoices?: InvoiceItem[];
}

interface PaymentItem {
  id: string;
  amount: number;
  paymentDate: string;
  patientName?: string;
  paymentMethod?: string;
}

interface InvoiceItem {
  id: string;
  invoiceNumber: string;
  totalAmount: number;
  status: string;
  patientName?: string;
}

interface ReadyForCheckoutPatient {
  appointmentId: string;
  patientId: string;
  patientName: string;
  serviceName?: string;
  doctorName: string;
  amountDue?: number;
  checkoutStatus?: string;
}

/* ─── Status colors ────────────────────────────────────────────────────────── */
const INVOICE_STATUS: Record<string, { label: string; color: string; bg: string }> = {
  Draft:     { label: "مسودة",     color: "#6b7280", bg: "#f3f4f6" },
  Pending:   { label: "معلّقة",     color: "#d97706", bg: "#fffbeb" },
  Paid:      { label: "مدفوعة",     color: "#16a34a", bg: "#f0fdf4" },
  Partially: { label: "مدفوعة جزئياً", color: "#2563eb", bg: "#eff6ff" },
  Overdue:   { label: "متأخرة",     color: "#dc2626", bg: "#fef2f2" },
  Cancelled: { label: "ملغاة",     color: "#6b7280", bg: "#f9fafb" },
};

const METHOD_LABELS: Record<string, string> = {
  Cash: "نقدي",
  Card: "بطاقة",
  BankTransfer: "تحويل بنكي",
  MobileWallet: "محفظة إلكترونية",
};

/* ─── Component ────────────────────────────────────────────────────────────── */
export default function FinanceView() {
  const queryClient = useQueryClient();
  const [summary, setSummary] = useState<FinanceSummary | null>(null);
  const [readyPatients, setReadyPatients] = useState<ReadyForCheckoutPatient[]>([]);
  const [loading, setLoading] = useState(true);

  const fetchData = useCallback(async () => {
    try {
      const [financeRes, journeyRes] = await Promise.allSettled([
        api.get<FinanceSummary>("/api/finance/summary"),
        api.get<ReadyForCheckoutPatient[]>("/api/patient-journey/today"),
      ]);

      if (financeRes.status === "fulfilled") {
        setSummary(financeRes.value.data);
      }
      if (journeyRes.status === "fulfilled") {
        const allPatients = journeyRes.value.data ?? [];
        setReadyPatients(
          allPatients.filter(
            (p) =>
              p.checkoutStatus === "ReadyForCheckout" ||
              p.checkoutStatus === "Pending"
          )
        );
      }
    } catch {
      /* silent */
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchData();
    const interval = setInterval(fetchData, 30_000);
    return () => clearInterval(interval);
  }, [fetchData]);

  const handleCreateDraftInvoice = async (visitId: string) => {
    try {
      await api.post(`/api/patient-journey/${visitId}/create-draft-invoice`);
      toast.success("تم إنشاء فاتورة مسودة");
      queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
      queryClient.invalidateQueries({ queryKey: ["finance"] });
      fetchData();
    } catch {
      toast.error("فشل إنشاء الفاتورة");
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-full">
        <Loader2 className="w-8 h-8 animate-spin" style={{ color: BLUE }} />
      </div>
    );
  }

  return (
    <div className="flex flex-col h-full overflow-auto p-4 gap-4" style={{ background: "#f8fafc" }}>
      {/* ── Summary Cards ── */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <SummaryCard
          icon={DollarSign}
          label="تحصيل اليوم"
          value={fmtRial(summary?.todayCollected)}
          color="#16a34a"
          bg="#f0fdf4"
          trend="up"
        />
        <SummaryCard
          icon={TrendingUp}
          label="تحصيل الشهر"
          value={fmtRial(summary?.monthCollected)}
          color="#2563eb"
          bg="#eff6ff"
          trend="up"
        />
        <SummaryCard
          icon={AlertTriangle}
          label="مبالغ متأخرة"
          value={fmtRial(summary?.overdueAmount)}
          color="#dc2626"
          bg="#fef2f2"
          trend="down"
        />
        <SummaryCard
          icon={Users}
          label="عقود نشطة"
          value={String(summary?.activeContracts ?? 0)}
          color="#7c3aed"
          bg="#f5f3ff"
        />
      </div>

      {/* ── Ready for Checkout Banner ── */}
      {readyPatients.length > 0 && (
        <div
          className="rounded-xl border p-4"
          style={{
            background: "linear-gradient(135deg, #fff7ed, #fef3c7)",
            borderColor: "#f5922e40",
            boxShadow: "0 2px 8px rgba(245,146,46,0.1)",
          }}
        >
          <div className="flex items-center gap-2 mb-3">
            <div
              className="w-8 h-8 rounded-lg flex items-center justify-center"
              style={{ background: ORANGE + "20" }}
            >
              <CreditCard className="w-4 h-4" style={{ color: ORANGE }} />
            </div>
            <h3 className="text-sm font-extrabold" style={{ color: NAVY }}>
              جاهزون للدفع من الطبيب ({readyPatients.length})
            </h3>
          </div>
          <div className="space-y-2">
            {readyPatients.map((p) => (
              <div
                key={p.appointmentId}
                className="flex items-center justify-between bg-white rounded-lg border px-3 py-2.5"
                style={{ borderColor: "#e5e7eb" }}
              >
                <div className="flex items-center gap-3">
                  <div
                    className="w-9 h-9 rounded-full flex items-center justify-center text-xs font-bold"
                    style={{ background: NAVY + "15", color: NAVY }}
                  >
                    {p.patientName.split(" ").filter(Boolean).length >= 2
                      ? p.patientName.split(" ")[0][0] + p.patientName.split(" ")[1][0]
                      : p.patientName[0]}
                  </div>
                  <div>
                    <p className="text-xs font-bold" style={{ color: NAVY }}>
                      {p.patientName}
                    </p>
                    <p className="text-[10px]" style={{ color: "#94a3b8" }}>
                      {p.serviceName ?? "—"} · {p.doctorName}
                    </p>
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  {p.amountDue != null && p.amountDue > 0 && (
                    <span className="text-xs font-bold" style={{ color: ORANGE }}>
                      {fmtRial(p.amountDue)}
                    </span>
                  )}
                  <button
                    onClick={() => {
                      // Navigate to finance-v3 for full payment processing
                      window.open("/finance-v3", "_blank");
                    }}
                    className="px-3 py-1.5 rounded-lg text-[11px] font-bold text-white transition hover:opacity-90"
                    style={{ background: ORANGE }}
                  >
                    تحصيل
                  </button>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* ── Two-column: Recent Payments + Recent Invoices ── */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        {/* Recent Payments */}
        <div
          className="rounded-xl border p-4"
          style={{ background: "#fff", borderColor: "#e5e7eb" }}
        >
          <div className="flex items-center justify-between mb-3">
            <div className="flex items-center gap-2">
              <Receipt className="w-4 h-4" style={{ color: BLUE }} />
              <h3 className="text-xs font-bold" style={{ color: NAVY }}>
                آخر المدفوعات
              </h3>
            </div>
            <button onClick={fetchData} className="p-1 rounded-lg hover:bg-gray-100">
              <RefreshCw className="w-3.5 h-3.5" style={{ color: "#94a3b8" }} />
            </button>
          </div>
          {summary?.recentPayments && summary.recentPayments.length > 0 ? (
            <div className="space-y-2">
              {summary.recentPayments.map((p) => (
                <div
                  key={p.id}
                  className="flex items-center justify-between py-2 border-b last:border-0"
                  style={{ borderColor: "#f1f5f9" }}
                >
                  <div>
                    <p className="text-xs font-bold" style={{ color: NAVY }}>
                      {p.patientName ?? "—"}
                    </p>
                    <p className="text-[10px]" style={{ color: "#94a3b8" }}>
                      {p.paymentDate ? new Date(p.paymentDate).toLocaleDateString("ar-YE") : "—"}
                      {p.paymentMethod ? ` · ${METHOD_LABELS[p.paymentMethod] ?? p.paymentMethod}` : ""}
                    </p>
                  </div>
                  <span className="text-xs font-extrabold" style={{ color: "#16a34a" }}>
                    {fmtRial(p.amount)}
                  </span>
                </div>
              ))}
            </div>
          ) : (
            <div className="text-center py-6 text-xs" style={{ color: "#94a3b8" }}>
              لا توجد مدفوعات اليوم
            </div>
          )}
        </div>

        {/* Recent Invoices */}
        <div
          className="rounded-xl border p-4"
          style={{ background: "#fff", borderColor: "#e5e7eb" }}
        >
          <div className="flex items-center justify-between mb-3">
            <div className="flex items-center gap-2">
              <FileText className="w-4 h-4" style={{ color: NAVY }} />
              <h3 className="text-xs font-bold" style={{ color: NAVY }}>
                آخر الفواتير
              </h3>
            </div>
            <span className="text-[10px] font-bold px-2 py-0.5 rounded-full" style={{ background: "#fef3c7", color: "#d97706" }}>
              {summary?.draftInvoicesCount ?? 0} مسودة
            </span>
          </div>
          {summary?.recentInvoices && summary.recentInvoices.length > 0 ? (
            <div className="space-y-2">
              {summary.recentInvoices.map((inv) => {
                const st = INVOICE_STATUS[inv.status] ?? INVOICE_STATUS.Draft;
                return (
                  <div
                    key={inv.id}
                    className="flex items-center justify-between py-2 border-b last:border-0"
                    style={{ borderColor: "#f1f5f9" }}
                  >
                    <div>
                      <p className="text-xs font-bold" style={{ color: NAVY }}>
                        {inv.invoiceNumber}
                      </p>
                      <p className="text-[10px]" style={{ color: "#94a3b8" }}>
                        {inv.patientName ?? "—"}
                      </p>
                    </div>
                    <div className="flex items-center gap-2">
                      <span className="text-xs font-bold" style={{ color: NAVY }}>
                        {fmtRial(inv.totalAmount)}
                      </span>
                      <span
                        className="text-[10px] font-bold px-2 py-0.5 rounded-full"
                        style={{ background: st.bg, color: st.color }}
                      >
                        {st.label}
                      </span>
                    </div>
                  </div>
                );
              })}
            </div>
          ) : (
            <div className="text-center py-6 text-xs" style={{ color: "#94a3b8" }}>
              لا توجد فواتير حديثة
            </div>
          )}
        </div>
      </div>

      {/* ── Quick Stats Row ── */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <QuickStat
          icon={IndianRupee}
          label="إجمالي مستحق"
          value={fmtRial(summary?.totalOutstanding)}
          color="#b91c1c"
        />
        <QuickStat
          icon={FileText}
          label="فواتير معلّقة"
          value={String(summary?.unpaidInvoicesCount ?? 0)}
          color="#d97706"
        />
        <QuickStat
          icon={FileText}
          label="فواتير مسودة"
          value={String(summary?.draftInvoicesCount ?? 0)}
          color="#6b7280"
        />
        <QuickStat
          icon={TrendingUp}
          label="عمولات معلّقة"
          value={fmtRial(summary?.pendingCommissionsAmount)}
          color="#7c3aed"
        />
      </div>
    </div>
  );
}

/* ─── Sub-components ────────────────────────────────────────────────────────── */

function SummaryCard({
  icon: Icon,
  label,
  value,
  color,
  bg,
  trend,
}: {
  icon: React.ElementType;
  label: string;
  value: string;
  color: string;
  bg: string;
  trend?: "up" | "down";
}) {
  return (
    <div className="rounded-xl border p-4" style={{ background: "#fff", borderColor: "#e5e7eb" }}>
      <div className="flex items-center gap-2 mb-2">
        <div className="w-8 h-8 rounded-lg flex items-center justify-center" style={{ background: bg }}>
          <Icon className="w-4 h-4" style={{ color }} />
        </div>
        {trend === "up" && <ArrowUpRight className="w-3 h-3" style={{ color: "#16a34a" }} />}
        {trend === "down" && <ArrowDownRight className="w-3 h-3" style={{ color: "#dc2626" }} />}
      </div>
      <p className="text-[10px] font-medium mb-0.5" style={{ color: "#94a3b8" }}>
        {label}
      </p>
      <p className="text-sm font-extrabold" style={{ color: NAVY }}>
        {value}
      </p>
    </div>
  );
}

function QuickStat({
  icon: Icon,
  label,
  value,
  color,
}: {
  icon: React.ElementType;
  label: string;
  value: string;
  color: string;
}) {
  return (
    <div
      className="rounded-lg border px-3 py-2.5 flex items-center gap-2"
      style={{ background: "#fff", borderColor: "#e5e7eb" }}
    >
      <Icon className="w-4 h-4 flex-shrink-0" style={{ color }} />
      <div>
        <p className="text-[10px]" style={{ color: "#94a3b8" }}>
          {label}
        </p>
        <p className="text-xs font-bold" style={{ color: NAVY }}>
          {value}
        </p>
      </div>
    </div>
  );
}
