"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { ArrowRight, Printer, Plus, FileText, CheckCircle, Clock, AlertCircle } from "lucide-react";
import type { Contract, Payment } from "@/types/finance";
import api from "@/lib/api";
import { cn, formatYemeniRiyal, formatArabicDate } from "@/lib/utils";
import { ReceiptView } from "@/components/finance/ReceiptView";

const STATUS_LABELS: Record<string, string> = {
  active: "نشط", completed: "مكتمل", cancelled: "ملغى",
};
const METHOD_LABELS: Record<string, string> = {
  cash: "نقداً", bank_transfer: "تحويل بنكي", card: "بطاقة",
};
const SPECIALTY_LABELS: Record<string, string> = {
  orthodontics: "تقويم", general: "أسنان عام", surgery: "جراحة", other: "أخرى",
};

// ─── Installment Schedule ────────────────────────────────────────────────────
function InstallmentSchedule({ contract }: { contract: Contract }) {
  const totalPaid    = contract.paidAmount;
  const installAmt   = contract.installmentAmount ?? 0;
  const downPayment  = contract.downPayment ?? 0;
  const startDate    = contract.startDate ? new Date(contract.startDate) : null;
  if (!startDate || installAmt === 0) return null;

  // Generate installment rows
  const installments = Array.from({ length: contract.installmentsCount }, (_, i) => {
    const due = new Date(startDate);
    due.setMonth(due.getMonth() + i + 1); // first installment 1 month after start
    const dueStr = due.toISOString().slice(0, 10);
    const cumulative = downPayment + (i + 1) * installAmt;
    const isPaid = totalPaid >= cumulative;
    const today = new Date().toISOString().slice(0, 10);
    const isOverdue = !isPaid && dueStr < today;
    return { num: i + 1, dueStr, amount: installAmt, isPaid, isOverdue };
  });

  const paidCount    = installments.filter((i) => i.isPaid).length;
  const overdueCount = installments.filter((i) => i.isOverdue).length;

  return (
    <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card">
      <div className="px-5 py-4 border-b border-[#f1f5f9] flex items-center justify-between">
        <h2 className="font-bold text-[#0d2137]">جدول الأقساط</h2>
        <div className="flex items-center gap-3 text-xs">
          <span className="flex items-center gap-1 text-[#22c55e]">
            <CheckCircle className="w-3.5 h-3.5" /> {paidCount} مسدّد
          </span>
          {overdueCount > 0 && (
            <span className="flex items-center gap-1 text-[#ef4444]">
              <AlertCircle className="w-3.5 h-3.5" /> {overdueCount} متأخر
            </span>
          )}
          <span className="flex items-center gap-1 text-[#94a3b8]">
            <Clock className="w-3.5 h-3.5" /> {contract.installmentsCount - paidCount} متبقي
          </span>
        </div>
      </div>
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead className="bg-[#f7fafd] border-b border-[#e8f0f9]">
            <tr>
              {["#", "تاريخ الاستحقاق", "المبلغ", "الحالة"].map((h) => (
                <th key={h} className="text-start px-4 py-2.5 text-xs font-bold text-[#64748b]">{h}</th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-[#f1f5f9]">
            {installments.map((inst) => (
              <tr key={inst.num}
                className={cn("transition", inst.isPaid ? "bg-[#22c55e08]" : inst.isOverdue ? "bg-[#ef444408]" : "hover:bg-[#f7fafd]")}
              >
                <td className="px-4 py-2.5 font-mono text-xs text-[#94a3b8]">{inst.num}</td>
                <td className="px-4 py-2.5 text-[#64748b]">{formatArabicDate(inst.dueStr)}</td>
                <td className="px-4 py-2.5 font-mono font-semibold text-[#0d2137]">{formatYemeniRiyal(inst.amount)}</td>
                <td className="px-4 py-2.5">
                  {inst.isPaid ? (
                    <span className="flex items-center gap-1 text-xs text-[#22c55e] font-medium">
                      <CheckCircle className="w-3.5 h-3.5" /> مسدّد
                    </span>
                  ) : inst.isOverdue ? (
                    <span className="flex items-center gap-1 text-xs text-[#ef4444] font-medium">
                      <AlertCircle className="w-3.5 h-3.5" /> متأخر
                    </span>
                  ) : (
                    <span className="flex items-center gap-1 text-xs text-[#94a3b8]">
                      <Clock className="w-3.5 h-3.5" /> قادم
                    </span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export default function ContractDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [contract, setContract] = useState<Contract | null>(null);
  const [loading, setLoading] = useState(true);
  const [printPayment, setPrintPayment] = useState<Payment | null>(null);

  useEffect(() => {
    api.get<Contract>(`/api/contracts/${id}`)
      .then((r) => setContract(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [id]);

  if (loading) {
    return (
      <div className="max-w-3xl space-y-4">
        {Array.from({ length: 3 }).map((_, i) => (
          <div key={i} className="h-24 bg-[#eef3f9] rounded-xl animate-pulse" />
        ))}
      </div>
    );
  }

  if (!contract) {
    return (
      <div className="text-center py-20 text-[#94a3b8]">
        <FileText className="w-12 h-12 mx-auto mb-3 opacity-30" />
        <p className="text-sm">العقد غير موجود</p>
      </div>
    );
  }

  const pctPaid = contract.totalAmount > 0
    ? Math.min(100, (contract.paidAmount / (contract.totalAmount - (contract.discountAmount ?? 0))) * 100)
    : 0;

  return (
    <div className="space-y-5 max-w-3xl">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-[#64748b]">
        <Link href="/finance" className="hover:text-accent-blue transition">المالية</Link>
        <span>/</span>
        <Link href="/finance/contracts" className="hover:text-accent-blue transition">العقود</Link>
        <span>/</span>
        <span className="text-[#0d2137] font-medium">{contract.patientName}</span>
      </div>

      <div className="flex items-center gap-3">
        <Link href="/finance/contracts" className="p-1.5 rounded-lg border border-[#e8f0f9] hover:bg-[#f7fafd] transition text-[#64748b]">
          <ArrowRight className="w-4 h-4" />
        </Link>
        <h1 className="text-2xl font-extrabold text-[#0d2137]">تفاصيل العقد</h1>
      </div>

      {/* Contract summary card */}
      <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card p-5">
        <div className="flex items-start justify-between mb-4">
          <div>
            <Link href={`/patients/${contract.patientId}`} className="text-lg font-bold text-[#0d2137] hover:text-accent-blue transition">
              {contract.patientName}
            </Link>
            <p className="text-xs text-[#94a3b8] font-mono mt-0.5">{contract.patientNumber}</p>
          </div>
          <span className={cn(
            "text-xs px-[10px] py-[2px] rounded-full font-medium",
            contract.status === "active" ? "bg-[#22c55e18] text-[#22c55e]" :
            contract.status === "completed" ? "bg-[#3d7ab518] text-accent-blue" :
            "bg-[#94a3b818] text-[#94a3b8]"
          )}>
            {STATUS_LABELS[contract.status] ?? contract.status}
          </span>
        </div>

        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-5">
          <div className="bg-[#f7fafd] rounded-lg p-3 text-center">
            <p className="text-xs text-[#64748b]">إجمالي العقد</p>
            <p className="font-bold font-mono text-[#0d2137] mt-1">{formatYemeniRiyal(contract.totalAmount)}</p>
          </div>
          {(contract.discountAmount ?? 0) > 0 && (
            <div className="bg-[#f5922e18] rounded-lg p-3 text-center">
              <p className="text-xs text-[#64748b]">الخصم</p>
              <p className="font-bold font-mono text-[#f5922e] mt-1">{formatYemeniRiyal(contract.discountAmount ?? 0)}</p>
            </div>
          )}
          <div className="bg-[#22c55e18] rounded-lg p-3 text-center">
            <p className="text-xs text-[#64748b]">المدفوع</p>
            <p className="font-bold font-mono text-[#22c55e] mt-1">{formatYemeniRiyal(contract.paidAmount)}</p>
          </div>
          <div className="bg-[#ef444418] rounded-lg p-3 text-center">
            <p className="text-xs text-[#64748b]">المتبقي</p>
            <p className="font-bold font-mono text-[#ef4444] mt-1">{formatYemeniRiyal(contract.remainingAmount)}</p>
          </div>
        </div>

        {/* Progress bar */}
        <div>
          <div className="flex justify-between text-xs text-[#64748b] mb-1">
            <span>نسبة السداد</span>
            <span>{pctPaid.toFixed(0)}%</span>
          </div>
          <div className="h-2.5 bg-[#eef3f9] rounded-full overflow-hidden">
            <div
              className={cn("h-full rounded-full transition-all", pctPaid >= 100 ? "bg-[#22c55e]" : "bg-accent-blue")}
              style={{ width: `${pctPaid}%` }}
            />
          </div>
        </div>

        <div className="flex items-center gap-4 mt-4 text-xs text-[#64748b] pt-4 border-t border-[#f1f5f9] flex-wrap">
          {contract.specialty && <span>التخصص: <span className="font-medium text-[#0d2137]">{SPECIALTY_LABELS[contract.specialty] ?? contract.specialty}</span></span>}
          {contract.startDate && <span>تاريخ البدء: <span className="font-medium text-[#0d2137]">{formatArabicDate(contract.startDate)}</span></span>}
          <span>الأقساط: <span className="font-medium text-[#0d2137]">{contract.installmentsCount} قسط × {formatYemeniRiyal(contract.installmentAmount ?? 0)}</span></span>
        </div>
        {contract.notes && (
          <p className="mt-3 text-sm text-[#64748b] bg-[#f7fafd] rounded-lg p-3">{contract.notes}</p>
        )}
      </div>

      {/* Installment Schedule */}
      {contract.installmentsCount > 1 && contract.startDate && (
        <InstallmentSchedule contract={contract} />
      )}

      {/* Print receipt if selected */}
      {printPayment && (
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <p className="text-sm font-semibold text-[#0d2137]">معاينة السند</p>
            <div className="flex gap-2">
              <button onClick={() => window.print()}
                className="flex items-center gap-1.5 px-3 py-1.5 text-sm rounded-lg border border-[#e8f0f9] hover:bg-[#f7fafd] transition"
              >
                <Printer className="w-3.5 h-3.5" />
                طباعة
              </button>
              <button onClick={() => setPrintPayment(null)}
                className="px-3 py-1.5 text-sm rounded-lg border border-[#e8f0f9] hover:bg-[#f7fafd] transition"
              >
                إغلاق
              </button>
            </div>
          </div>
          <ReceiptView payment={printPayment} />
        </div>
      )}

      {/* Payments list */}
      <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card">
        <div className="flex items-center justify-between px-5 py-4 border-b border-[#f1f5f9]">
          <h2 className="font-bold text-[#0d2137]">سجل الدفعات</h2>
          {contract.status === "active" && (
            <Link
              href={`/finance/payments?contractId=${id}&patientId=${contract.patientId}&patientName=${encodeURIComponent(contract.patientName ?? "")}`}
              className="flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-lg bg-accent-blue text-white hover:bg-blue-hover transition"
            >
              <Plus className="w-3.5 h-3.5" />
              دفعة جديدة
            </Link>
          )}
        </div>

        {!contract.payments?.length ? (
          <div className="text-center py-10 text-[#94a3b8] text-sm">لا توجد دفعات مسجلة</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-[#f7fafd] border-b border-[#e8f0f9]">
                <tr>
                  {["رقم السند", "المبلغ", "التاريخ", "الطريقة", "الوصف", ""].map((h) => (
                    <th key={h} className="text-start px-4 py-3 text-xs font-bold text-[#64748b]">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-[#f1f5f9]">
                {contract.payments.map((p) => (
                  <tr key={p.id} className="hover:bg-[#f7fafd] transition">
                    <td className="px-4 py-3 font-mono text-xs text-[#94a3b8]">{p.receiptNumber ?? "—"}</td>
                    <td className="px-4 py-3 font-mono font-semibold text-[#22c55e]">{formatYemeniRiyal(p.amount)}</td>
                    <td className="px-4 py-3 text-[#64748b]">{formatArabicDate(p.paymentDate)}</td>
                    <td className="px-4 py-3 text-[#64748b]">{METHOD_LABELS[p.paymentMethod ?? ""] ?? p.paymentMethod ?? "—"}</td>
                    <td className="px-4 py-3 text-[#64748b]">{p.serviceDescription ?? "—"}</td>
                    <td className="px-4 py-3">
                      <button
                        onClick={() => setPrintPayment(p)}
                        className="text-xs text-[#94a3b8] hover:text-accent-blue transition"
                        title="طباعة السند"
                      >
                        <Printer className="w-3.5 h-3.5" />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
