"use client";

import { useState, useMemo, useCallback } from "react";
import {
  BarChart2, Download, Filter, RefreshCw,
  AlertTriangle, CreditCard, X, History,
  TrendingUp, Wallet, Building2,
  ChevronDown, ChevronUp, Search, RotateCcw,
  DollarSign, Receipt, PiggyBank, Calculator,
} from "lucide-react";
import {
  useCommissionReport, useRecordCommissionPayment,
  useCommissionPayments,
} from "@/hooks/useCommissions";
import { useDoctors } from "@/hooks/useDoctors";
import { useBranches } from "@/hooks/useBranches";
import type { CommissionStatus, CommissionReportRow } from "@/types/commission";
import { Skeleton, CardSkeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";

// ── Helpers ───────────────────────────────────────────────────────────────────

function fmt(n: number) {
  return n.toLocaleString("ar-YE", { minimumFractionDigits: 0, maximumFractionDigits: 2 });
}

function today() { return new Date().toISOString().slice(0, 10); }
function firstOfMonth() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

const STATUS_CONFIG: Record<CommissionStatus, { label: string; color: string; dot: string }> = {
  Pending:    { label: "قيد الانتظار", color: "bg-gray-100 text-gray-700",    dot: "bg-gray-400" },
  Calculated: { label: "محسوبة",        color: "bg-blue-100 text-blue-700",    dot: "bg-blue-500" },
  Approved:   { label: "معتمدة",        color: "bg-green-100 text-green-700",  dot: "bg-green-500" },
  Paid:       { label: "مدفوعة",        color: "bg-emerald-100 text-emerald-700", dot: "bg-emerald-500" },
};

const PAYMENT_METHOD_LABELS: Record<string, string> = {
  cash: "نقدي",
  card: "بطاقة",
  bank_transfer: "تحويل بنكي",
  check: "شيك",
};

const SERVICE_CATEGORIES: Record<string, string> = {
  Consultation: "استشارة",
  Preventive: "وقائي",
  Restorative: "ترميمي",
  Endodontics: "علاج عصب",
  Prosthodontics: "تعويضي",
  Orthodontics: "تقويم",
  Surgery: "جراحة",
  Cosmetic: "تجميلي",
  Radiology: "أشعة",
  Other: "أخرى",
};

const PAYMENT_STATUS_OPTIONS = [
  { value: "Unpaid", label: "غير مدفوعة" },
  { value: "PartiallyPaid", label: "مدفوعة جزئياً" },
  { value: "FullyPaid", label: "مدفوعة بالكامل" },
];

// ── Summary card ──────────────────────────────────────────────────────────────

interface SummaryCardProps {
  label: string;
  value: string;
  sub?: string;
  icon: React.ElementType;
  iconBg: string;
  iconColor: string;
  borderAccent: string;
}

function SummaryCard({ label, value, sub, icon: Icon, iconBg, iconColor, borderAccent }: SummaryCardProps) {
  return (
    <div className={cn(
      "relative rounded-xl p-4 border bg-white shadow-card overflow-hidden group",
      borderAccent,
    )}>
      {/* Decorative accent bar at top */}
      <div className={cn("absolute top-0 right-0 left-0 h-1", iconBg)} />
      <div className="flex items-start justify-between gap-2">
        <div className="flex-1 min-w-0">
          <p className="text-xs font-semibold text-gray-500 mb-1 truncate">{label}</p>
          <p className="text-xl font-extrabold text-gray-900 leading-tight">{value}</p>
          {sub && <p className="text-[11px] text-gray-400 mt-1 truncate">{sub}</p>}
        </div>
        <div className={cn("w-9 h-9 rounded-lg flex items-center justify-center flex-shrink-0", iconBg)}>
          <Icon className={cn("w-4.5 h-4.5", iconColor)} />
        </div>
      </div>
    </div>
  );
}

// ── Loading skeletons ──────────────────────────────────────────────────────────

function SummarySkeleton() {
  return (
    <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3">
      {Array.from({ length: 6 }).map((_, i) => (
        <CardSkeleton key={i} />
      ))}
    </div>
  );
}

function TableSkeleton() {
  return (
    <div className="bg-white rounded-2xl border border-gray-100 overflow-hidden">
      <div className="p-4 space-y-3">
        <div className="flex gap-4">
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-8 flex-1" />
          ))}
        </div>
        {Array.from({ length: 5 }).map((_, i) => (
          <div key={i} className="flex gap-4">
            {Array.from({ length: 6 }).map((_, j) => (
              <Skeleton key={j} className="h-10 flex-1" />
            ))}
          </div>
        ))}
      </div>
    </div>
  );
}

// ── Expandable row detail ─────────────────────────────────────────────────────

function RowDetail({ row }: { row: CommissionReportRow }) {
  const centerShare = row.netCommissionableAmount - row.doctorCommission;
  return (
    <div className="bg-gray-50/70 px-6 py-4" dir="rtl">
      <div className="grid grid-cols-2 md:grid-cols-4 lg:grid-cols-5 gap-3">
        <DetailItem label="إجمالي العلاج" value={`${fmt(row.grossAmount)} ر.ي`} />
        <DetailItem label="الخصم" value={row.discount > 0 ? `(${fmt(row.discount)}) ر.ي` : "—"} valueColor="text-red-600" />
        <DetailItem label="تكلفة المواد" value={row.materialCost > 0 ? `(${fmt(row.materialCost)}) ر.ي` : "—"} valueColor="text-red-600" />
        <DetailItem label="أجور المعمل" value={row.labCost > 0 ? `(${fmt(row.labCost)}) ر.ي` : "—"} valueColor="text-red-600" />
        <DetailItem label="تكاليف أخرى" value={row.otherCosts > 0 ? `(${fmt(row.otherCosts)}) ر.ي` : "—"} valueColor="text-red-600" />
        <DetailItem label="الصافي الخاضع للعمولة" value={`${fmt(row.netCommissionableAmount)} ر.ي`} valueColor="text-blue-700" bold />
        <DetailItem label="نسبة الطبيب" value={`${row.doctorPercentage}%`} />
        <DetailItem label="مستحق الطبيب" value={`${fmt(row.doctorCommission)} ر.ي`} valueColor="text-purple-700" bold />
        <DetailItem label="نصيب المركز" value={`${fmt(centerShare)} ر.ي`} valueColor="text-clinic-navy" bold />
        <DetailItem label="حالة العمولة" value={STATUS_CONFIG[row.status as CommissionStatus]?.label ?? row.status} />
      </div>
      {row.netCommissionableAmount < 0 && (
        <div className="mt-3 flex items-center gap-1.5 text-xs text-red-600 bg-red-50 rounded-lg px-3 py-2">
          <AlertTriangle className="w-3.5 h-3.5 flex-shrink-0" />
          تحذير: الصافي سالب — التكاليف تتجاوز قيمة الخدمة بعد الخصم
        </div>
      )}
    </div>
  );
}

function DetailItem({ label, value, valueColor, bold }: {
  label: string; value: string; valueColor?: string; bold?: boolean;
}) {
  return (
    <div className="bg-white rounded-lg px-3 py-2 border border-gray-100">
      <p className="text-[11px] text-gray-400 font-medium mb-0.5">{label}</p>
      <p className={cn("text-sm", bold && "font-bold", valueColor ?? "text-gray-800")}>{value}</p>
    </div>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────

export default function CommissionsPage() {
  // Filters
  const [from, setFrom]                     = useState(firstOfMonth());
  const [to, setTo]                         = useState(today());
  const [doctorId, setDoctorId]             = useState("");
  const [branchId, setBranchId]             = useState("");
  const [commissionStatus, setCommStatus]   = useState("");
  const [paymentStatus, setPaymentStatus]   = useState("");
  const [serviceCategory, setServiceCategory] = useState("");
  const [applied, setApplied]               = useState<{
    from: string; to: string;
    doctorId?: string; branchId?: string;
    commissionStatus?: string; paymentStatus?: string;
    serviceCategory?: string;
  } | null>(null);

  // Expandable rows
  const [expandedRow, setExpandedRow]       = useState<number | null>(null);

  // Payment dialog
  const [showPayDialog, setShowPayDialog]   = useState(false);
  const [showHistory, setShowHistory]       = useState(false);
  const [payDoctorId, setPayDoctorId]       = useState("");
  const [payAmount, setPayAmount]           = useState<number>(0);
  const [payMethod, setPayMethod]           = useState("cash");
  const [payRef, setPayRef]                 = useState("");
  const [payNotes, setPayNotes]             = useState("");
  const [payErr, setPayErr]                 = useState("");

  const { data: doctors }                     = useDoctors();
  const { data: branches }                    = useBranches();
  const { data: report, isLoading, error, refetch } = useCommissionReport(applied);
  const recordPayment                         = useRecordCommissionPayment();
  const { data: payments, refetch: refetchPayments } = useCommissionPayments();

  const summary = report?.summary;
  const rows    = useMemo(() => report?.rows ?? [], [report]);

  // Computed values for payment dialog
  const selectedDoctorStats = useMemo(() => {
    if (!payDoctorId || !rows.length) return null;
    const doctorRows = rows.filter(r => r.doctorName && r.doctorName === doctors?.find(d => d.id === payDoctorId)?.name);
    const totalEarned = doctorRows.reduce((s, r) => s + r.doctorCommission, 0);
    const totalPaid = doctorRows.reduce((s, r) => s + r.paidCommission, 0);
    const totalRemaining = doctorRows.reduce((s, r) => s + r.remainingCommission, 0);
    return { totalEarned, totalPaid, totalRemaining };
  }, [payDoctorId, rows, doctors]);

  const isOverpay = selectedDoctorStats
    ? payAmount > selectedDoctorStats.totalRemaining
    : false;

  const activeFilterCount = [
    doctorId, branchId, commissionStatus, paymentStatus, serviceCategory,
  ].filter(Boolean).length;

  function applyFilters() {
    setApplied({
      from,
      to,
      doctorId:           doctorId || undefined,
      branchId:           branchId || undefined,
      commissionStatus:   commissionStatus || undefined,
      paymentStatus:      paymentStatus || undefined,
      serviceCategory:    serviceCategory || undefined,
    });
  }

  function resetFilters() {
    setFrom(firstOfMonth());
    setTo(today());
    setDoctorId("");
    setBranchId("");
    setCommStatus("");
    setPaymentStatus("");
    setServiceCategory("");
    setApplied(null);
  }

  async function handleRecordPayment() {
    if (!payDoctorId) {
      setPayErr("يرجى اختيار الطبيب");
      return;
    }
    if (!payAmount || payAmount <= 0) {
      setPayErr("يرجى إدخال مبلغ أكبر من صفر");
      return;
    }
    if (isOverpay) {
      setPayErr("المبلغ يتجاوز المستحق المتبقي للطبيب. يرجى تصحيح المبلغ.");
      return;
    }
    setPayErr("");
    try {
      await recordPayment.mutateAsync({
        doctorId: payDoctorId,
        amount: payAmount,
        paymentDate: today(),
        paymentMethod: payMethod || undefined,
        referenceNumber: payRef || undefined,
        notes: payNotes || undefined,
      });
      setShowPayDialog(false);
      setPayDoctorId(""); setPayAmount(0); setPayRef(""); setPayNotes(""); setPayMethod("cash");
      refetchPayments();
      if (applied) refetch();
    } catch (e: unknown) {
      const msg = (e as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setPayErr(msg ?? "فشل تسجيل الدفعة. يرجى المحاولة مرة أخرى.");
    }
  }

  const toggleRow = useCallback((i: number) => {
    setExpandedRow(prev => prev === i ? null : i);
  }, []);

  // ── Export CSV ────────────────────────────────────────────────────────────
  function exportCsv() {
    if (!rows.length) return;
    const headers = [
      "التاريخ","المريض","الطبيب","رقم الفاتورة","الخدمة",
      "الإجمالي","الخصم","تكلفة المواد","أجور المعمل","تكاليف أخرى",
      "الصافي الخاضع للعمولة","نسبة الطبيب","مستحق الطبيب","المدفوع","المتبقي","الحالة",
    ];
    const csv = [
      headers.join(","),
      ...rows.map(r => [
        r.date.slice(0,10), r.patientName, r.doctorName ?? "", r.invoiceNumber, r.serviceName,
        r.grossAmount, r.discount, r.materialCost,
        r.labCost, r.otherCosts, r.netCommissionableAmount, r.doctorPercentage,
        r.doctorCommission, r.paidCommission, r.remainingCommission, r.status,
      ].join(","))
    ].join("\n");
    const blob = new Blob(["\uFEFF" + csv], { type: "text/csv;charset=utf-8" });
    const url  = URL.createObjectURL(blob);
    const a    = document.createElement("a");
    a.href = url; a.download = `commissions-${from}-${to}.csv`; a.click();
    URL.revokeObjectURL(url);
  }

  // ── Computed summary values ──────────────────────────────────────────────
  const totalDirectCosts = summary
    ? summary.totalMaterialCost + summary.totalLabCost + summary.totalOtherCosts
    : 0;
  const totalCenterShare = summary
    ? summary.totalNet - summary.totalDoctorCommission
    : 0;

  return (
    <div className="p-6 space-y-6 max-w-7xl mx-auto page-content" dir="rtl">
      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div>
          <h1 className="text-2xl font-extrabold text-gray-900">تقرير عمولات الأطباء</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            محاسبة مستحقات الأطباء بعد خصم التكاليف المباشرة — مركز د. عقلان الكامل
          </p>
        </div>
        <div className="flex items-center gap-2 flex-wrap">
          <button
            onClick={() => { setShowHistory((v) => !v); if (!showHistory) refetchPayments(); }}
            className={cn(
              "flex items-center gap-2 px-4 py-2 rounded-xl text-sm font-medium transition",
              showHistory
                ? "bg-clinic-navy text-white"
                : "bg-white border border-gray-200 text-gray-700 hover:bg-gray-50"
            )}
          >
            <History className="w-4 h-4" /> سجل الدفعات
          </button>
          <button
            onClick={() => setShowPayDialog(true)}
            className="flex items-center gap-2 px-4 py-2 bg-clinic-orange text-white rounded-xl text-sm font-semibold hover:bg-clinic-orange/90 shadow-sm transition"
          >
            <CreditCard className="w-4 h-4" /> دفع عمولات
          </button>
          <button
            onClick={exportCsv}
            disabled={!rows.length}
            className="flex items-center gap-2 px-4 py-2 bg-white border border-gray-200 rounded-xl text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-40 transition"
          >
            <Download className="w-4 h-4" /> تصدير CSV
          </button>
        </div>
      </div>

      {/* Filters */}
      <div className="bg-white rounded-2xl border border-gray-100 shadow-card p-5">
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-2">
            <Filter className="w-4 h-4 text-gray-400" />
            <span className="text-sm font-bold text-gray-700">الفلاتر</span>
            {activeFilterCount > 0 && (
              <span className="bg-clinic-orange/10 text-clinic-orange text-[11px] font-bold px-2 py-0.5 rounded-full">
                {activeFilterCount} نشط
              </span>
            )}
          </div>
          <button
            onClick={resetFilters}
            className="flex items-center gap-1.5 text-xs text-gray-500 hover:text-gray-700 transition"
          >
            <RotateCcw className="w-3 h-3" /> إعادة تعيين
          </button>
        </div>
        <div className="grid grid-cols-2 md:grid-cols-4 lg:grid-cols-7 gap-3">
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">من تاريخ</label>
            <input type="date" value={from} onChange={e => setFrom(e.target.value)}
              className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm outline-none focus:border-clinic-blue focus:ring-1 focus:ring-clinic-blue/20 transition" />
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">إلى تاريخ</label>
            <input type="date" value={to} onChange={e => setTo(e.target.value)}
              className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm outline-none focus:border-clinic-blue focus:ring-1 focus:ring-clinic-blue/20 transition" />
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">الطبيب</label>
            <select value={doctorId} onChange={e => setDoctorId(e.target.value)}
              className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm outline-none focus:border-clinic-blue bg-white transition">
              <option value="">كل الأطباء</option>
              {doctors?.filter(d => d.isActive !== false).map(d => (
                <option key={d.id} value={d.id}>{d.name}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">الفرع</label>
            <select value={branchId} onChange={e => setBranchId(e.target.value)}
              className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm outline-none focus:border-clinic-blue bg-white transition">
              <option value="">كل الفروع</option>
              {branches?.filter(b => b.isActive).map(b => (
                <option key={b.id} value={b.id}>{b.name}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">حالة العمولة</label>
            <select value={commissionStatus} onChange={e => setCommStatus(e.target.value)}
              className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm outline-none focus:border-clinic-blue bg-white transition">
              <option value="">كل الحالات</option>
              <option value="Pending">قيد الانتظار</option>
              <option value="Calculated">محسوبة</option>
              <option value="Approved">معتمدة</option>
              <option value="Paid">مدفوعة</option>
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">حالة الدفع</label>
            <select value={paymentStatus} onChange={e => setPaymentStatus(e.target.value)}
              className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm outline-none focus:border-clinic-blue bg-white transition">
              <option value="">كل حالات الدفع</option>
              {PAYMENT_STATUS_OPTIONS.map(o => (
                <option key={o.value} value={o.value}>{o.label}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">تصنيف الخدمة</label>
            <select value={serviceCategory} onChange={e => setServiceCategory(e.target.value)}
              className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm outline-none focus:border-clinic-blue bg-white transition">
              <option value="">كل التصنيفات</option>
              {Object.entries(SERVICE_CATEGORIES).map(([k, v]) => (
                <option key={k} value={k}>{v}</option>
              ))}
            </select>
          </div>
        </div>
        <div className="mt-4 flex justify-end">
          <button
            onClick={applyFilters}
            className="flex items-center gap-2 px-6 py-2.5 bg-clinic-blue text-white rounded-xl text-sm font-semibold hover:bg-clinic-blue/90 shadow-sm transition"
          >
            <Search className="w-4 h-4" /> عرض التقرير
          </button>
        </div>
      </div>

      {/* Summary cards */}
      {isLoading && <SummarySkeleton />}
      {!isLoading && summary && (
        <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3">
          <SummaryCard
            label="إجمالي العمولات النظرية"
            value={`${fmt(summary.totalDoctorCommission)} ر.ي`}
            sub={`من ${fmt(summary.totalNet)} صافٍ خاضع`}
            icon={TrendingUp}
            iconBg="bg-purple-50"
            iconColor="text-purple-600"
            borderAccent="border-purple-100"
          />
          <SummaryCard
            label="المستحق القابل للصرف"
            value={`${fmt(summary.totalRemaining)} ر.ي`}
            sub={summary.totalRemaining > 0 ? "مبالغ غير مدفوعة بعد" : "لا توجد مستحقات"}
            icon={Wallet}
            iconBg="bg-amber-50"
            iconColor="text-amber-600"
            borderAccent="border-amber-100"
          />
          <SummaryCard
            label="المدفوع للأطباء"
            value={`${fmt(summary.totalPaid)} ر.ي`}
            sub={summary.totalDoctorCommission > 0
              ? `${Math.round((summary.totalPaid / summary.totalDoctorCommission) * 100)}% من الإجمالي`
              : undefined
            }
            icon={DollarSign}
            iconBg="bg-green-50"
            iconColor="text-green-600"
            borderAccent="border-green-100"
          />
          <SummaryCard
            label="المتبقي"
            value={`${fmt(summary.totalRemaining)} ر.ي`}
            sub={`من إجمالي ${fmt(summary.totalDoctorCommission)} ر.ي`}
            icon={PiggyBank}
            iconBg="bg-rose-50"
            iconColor="text-rose-600"
            borderAccent="border-rose-100"
          />
          <SummaryCard
            label="صافي حصة المركز"
            value={`${fmt(totalCenterShare)} ر.ي`}
            sub={`بعد خصم عمولات الأطباء`}
            icon={Building2}
            iconBg="bg-clinic-blue-50"
            iconColor="text-clinic-blue"
            borderAccent="border-clinic-blue-100"
          />
          <SummaryCard
            label="إجمالي التكاليف المباشرة"
            value={`${fmt(totalDirectCosts)} ر.ي`}
            sub={`مواد ${fmt(summary.totalMaterialCost)} | معمل ${fmt(summary.totalLabCost)} | أخرى ${fmt(summary.totalOtherCosts)}`}
            icon={Calculator}
            iconBg="bg-red-50"
            iconColor="text-red-500"
            borderAccent="border-red-100"
          />
        </div>
      )}

      {/* Report table */}
      <div className="bg-white rounded-2xl border border-gray-100 shadow-card overflow-hidden">
        {/* Loading state */}
        {isLoading && (
          <div className="p-5">
            <div className="flex items-center gap-2 mb-4">
              <RefreshCw className="w-4 h-4 animate-spin text-clinic-blue" />
              <span className="text-sm text-gray-500">جاري تحميل بيانات التقرير…</span>
            </div>
            <TableSkeleton />
          </div>
        )}

        {/* Error state */}
        {!isLoading && error && (
          <div className="flex flex-col items-center justify-center py-16 text-center">
            <div className="w-14 h-14 rounded-full bg-red-50 flex items-center justify-center mb-4">
              <AlertTriangle className="w-7 h-7 text-red-500" />
            </div>
            <p className="text-sm font-semibold text-gray-800 mb-1">تعذّر تحميل التقرير</p>
            <p className="text-xs text-gray-400 mb-4">حدث خطأ أثناء جلب بيانات العمولات. يرجى المحاولة مرة أخرى.</p>
            <button
              onClick={() => applied && refetch()}
              className="flex items-center gap-2 px-4 py-2 bg-clinic-blue text-white rounded-lg text-sm font-medium hover:bg-clinic-blue/90 transition"
            >
              <RefreshCw className="w-4 h-4" /> إعادة المحاولة
            </button>
          </div>
        )}

        {/* Initial state — no filters applied */}
        {!isLoading && !error && !applied && (
          <div className="flex flex-col items-center justify-center py-20 text-center">
            <div className="w-16 h-16 rounded-full bg-clinic-blue-50 flex items-center justify-center mb-4">
              <BarChart2 className="w-8 h-8 text-clinic-blue opacity-60" />
            </div>
            <p className="text-sm font-semibold text-gray-700 mb-1">تقرير العمولات</p>
            <p className="text-xs text-gray-400">اختر الفلاتر المطلوبة ثم اضغط &quot;عرض التقرير&quot;</p>
          </div>
        )}

        {/* No data state */}
        {!isLoading && applied && rows.length === 0 && (
          <div className="flex flex-col items-center justify-center py-20 text-center">
            <div className="w-14 h-14 rounded-full bg-gray-50 flex items-center justify-center mb-4">
              <Search className="w-7 h-7 text-gray-300" />
            </div>
            <p className="text-sm font-semibold text-gray-700 mb-1">لا توجد بيانات</p>
            <p className="text-xs text-gray-400 mb-4">لم يتم العثور على عمولات للفترة والفلاتر المحددة</p>
            <button
              onClick={resetFilters}
              className="flex items-center gap-1.5 px-4 py-2 text-xs text-gray-600 bg-gray-50 rounded-lg hover:bg-gray-100 transition"
            >
              <RotateCcw className="w-3 h-3" /> إعادة تعيين الفلاتر
            </button>
          </div>
        )}

        {/* Data table */}
        {!isLoading && rows.length > 0 && (
          <>
            <div className="flex items-center justify-between px-5 py-3 border-b border-gray-100">
              <span className="text-xs text-gray-500">{rows.length} سجل</span>
              <span className="text-xs text-gray-400">اضغط على الصف لعرض التفاصيل</span>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="bg-gray-50/80 text-gray-600 text-xs">
                    <th className="px-3 py-3 text-right font-semibold whitespace-nowrap w-6"></th>
                    {[
                      "التاريخ","المريض","الطبيب","رقم الفاتورة",
                      "الخدمة","الإجمالي","الخصم","تكلفة المواد","أجور المعمل",
                      "تكاليف أخرى","الصافي الخاضع للعمولة","نسبة الطبيب",
                      "مستحق الطبيب","المدفوع","المتبقي","الحالة",
                    ].map(h => (
                      <th key={h} className="px-3 py-3 text-right font-semibold whitespace-nowrap">{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {rows.map((row, i) => {
                    const st = STATUS_CONFIG[row.status as CommissionStatus] ?? STATUS_CONFIG.Pending;
                    const isExpanded = expandedRow === i;
                    return (
                      <tr key={i} className="contents">
                        {/* Main row — clickable */}
                        <tr
                          className={cn(
                            "hover:bg-blue-50/30 transition cursor-pointer",
                            isExpanded && "bg-blue-50/20"
                          )}
                          onClick={() => toggleRow(i)}
                        >
                          <td className="px-2 py-2.5 text-gray-400">
                            {isExpanded
                              ? <ChevronUp className="w-3.5 h-3.5" />
                              : <ChevronDown className="w-3.5 h-3.5" />
                            }
                          </td>
                          <td className="px-3 py-2.5 text-gray-500 whitespace-nowrap text-xs">{row.date.slice(0,10)}</td>
                          <td className="px-3 py-2.5 font-medium text-gray-900 whitespace-nowrap">{row.patientName}</td>
                          <td className="px-3 py-2.5 text-gray-700 whitespace-nowrap">{row.doctorName ?? "—"}</td>
                          <td className="px-3 py-2.5 text-gray-500 font-mono text-xs">{row.invoiceNumber}</td>
                          <td className="px-3 py-2.5 text-gray-700 max-w-[130px] truncate">{row.serviceName}</td>
                          <td className="px-3 py-2.5 font-medium text-gray-800 whitespace-nowrap">{fmt(row.grossAmount)}</td>
                          <td className="px-3 py-2.5 text-red-600 whitespace-nowrap">{row.discount > 0 ? `(${fmt(row.discount)})` : "—"}</td>
                          <td className="px-3 py-2.5 text-red-600 whitespace-nowrap">{row.materialCost > 0 ? `(${fmt(row.materialCost)})` : "—"}</td>
                          <td className="px-3 py-2.5 text-red-600 whitespace-nowrap">{row.labCost > 0 ? `(${fmt(row.labCost)})` : "—"}</td>
                          <td className="px-3 py-2.5 text-red-600 whitespace-nowrap">{row.otherCosts > 0 ? `(${fmt(row.otherCosts)})` : "—"}</td>
                          <td className="px-3 py-2.5 font-bold text-blue-700 whitespace-nowrap">{fmt(row.netCommissionableAmount)}</td>
                          <td className="px-3 py-2.5 text-gray-600 whitespace-nowrap">{row.doctorPercentage}%</td>
                          <td className="px-3 py-2.5 font-bold text-purple-700 whitespace-nowrap">{fmt(row.doctorCommission)}</td>
                          <td className="px-3 py-2.5 text-green-700 whitespace-nowrap">{fmt(row.paidCommission)}</td>
                          <td className="px-3 py-2.5 text-amber-700 whitespace-nowrap">{fmt(row.remainingCommission)}</td>
                          <td className="px-3 py-2.5">
                            <span className={cn("text-[10px] font-semibold px-2 py-0.5 rounded-full inline-flex items-center gap-1", st.color)}>
                              <span className={cn("w-1.5 h-1.5 rounded-full", st.dot)} />
                              {st.label}
                            </span>
                          </td>
                        </tr>
                        {/* Expanded detail */}
                        {isExpanded && (
                          <tr>
                            <td colSpan={17}>
                              <RowDetail row={row} />
                            </td>
                          </tr>
                        )}
                      </tr>
                    );
                  })}
                </tbody>
                {/* Totals row */}
                {summary && (
                  <tfoot>
                    <tr className="bg-gray-50/80 border-t-2 border-gray-200 text-xs font-bold text-gray-700">
                      <td className="px-2 py-3"></td>
                      <td className="px-3 py-3" colSpan={4}>الإجمالي</td>
                      <td className="px-3 py-3 whitespace-nowrap">{fmt(summary.totalGross)}</td>
                      <td className="px-3 py-3 text-red-600 whitespace-nowrap">{summary.totalDiscount > 0 ? `(${fmt(summary.totalDiscount)})` : "—"}</td>
                      <td className="px-3 py-3 text-red-600 whitespace-nowrap">{summary.totalMaterialCost > 0 ? `(${fmt(summary.totalMaterialCost)})` : "—"}</td>
                      <td className="px-3 py-3 text-red-600 whitespace-nowrap">{summary.totalLabCost > 0 ? `(${fmt(summary.totalLabCost)})` : "—"}</td>
                      <td className="px-3 py-3 text-red-600 whitespace-nowrap">{summary.totalOtherCosts > 0 ? `(${fmt(summary.totalOtherCosts)})` : "—"}</td>
                      <td className="px-3 py-3 text-blue-700 whitespace-nowrap">{fmt(summary.totalNet)}</td>
                      <td className="px-3 py-3">—</td>
                      <td className="px-3 py-3 text-purple-700 whitespace-nowrap">{fmt(summary.totalDoctorCommission)}</td>
                      <td className="px-3 py-3 text-green-700 whitespace-nowrap">{fmt(summary.totalPaid)}</td>
                      <td className="px-3 py-3 text-amber-700 whitespace-nowrap">{fmt(summary.totalRemaining)}</td>
                      <td className="px-3 py-3"></td>
                    </tr>
                  </tfoot>
                )}
              </table>
            </div>
          </>
        )}
      </div>

      {/* Payment history */}
      {showHistory && (
        <div className="bg-white rounded-2xl border border-gray-100 shadow-card overflow-hidden">
          <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
            <div className="flex items-center gap-2">
              <History className="w-4 h-4 text-gray-500" />
              <h2 className="font-bold text-gray-900">سجل دفعات العمولات</h2>
              <span className="text-xs text-gray-400 bg-gray-50 px-2 py-0.5 rounded-full">{payments?.length ?? 0} دفعة</span>
            </div>
          </div>
          {!payments?.length ? (
            <div className="flex flex-col items-center justify-center py-12 text-center">
              <div className="w-12 h-12 rounded-full bg-gray-50 flex items-center justify-center mb-3">
                <Receipt className="w-6 h-6 text-gray-300" />
              </div>
              <p className="text-sm text-gray-500">لا توجد دفعات مسجلة</p>
              <p className="text-xs text-gray-400 mt-0.5">ستظهر هنا دفعات العمولات بعد تسجيلها</p>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="bg-gray-50/80 text-gray-600 text-xs">
                    {["التاريخ","الطبيب","المبلغ","طريقة الدفع","رقم المرجع","ملاحظات","تاريخ التسجيل"].map(h => (
                      <th key={h} className="px-4 py-3 text-right font-semibold whitespace-nowrap">{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {payments.map((p) => (
                    <tr key={p.id} className="hover:bg-gray-50/50 transition">
                      <td className="px-4 py-2.5 text-gray-500 whitespace-nowrap text-xs">{p.paymentDate?.toString().slice(0,10) ?? "—"}</td>
                      <td className="px-4 py-2.5 font-medium text-gray-900 whitespace-nowrap">{p.doctorName ?? "—"}</td>
                      <td className="px-4 py-2.5 font-bold text-green-700 whitespace-nowrap">{fmt(p.amount)} ر.ي</td>
                      <td className="px-4 py-2.5 text-gray-600">
                        {PAYMENT_METHOD_LABELS[p.paymentMethod ?? ""] ?? p.paymentMethod ?? "—"}
                      </td>
                      <td className="px-4 py-2.5 text-gray-500 font-mono text-xs">{p.referenceNumber ?? "—"}</td>
                      <td className="px-4 py-2.5 text-gray-400 text-xs max-w-[160px] truncate">{p.notes ?? "—"}</td>
                      <td className="px-4 py-2.5 text-gray-400 text-xs whitespace-nowrap">{p.createdAt?.slice(0,10) ?? "—"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {/* Pay commissions dialog */}
      {showPayDialog && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg mx-4 overflow-hidden" dir="rtl">
            {/* Dialog header */}
            <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100 bg-gray-50/50">
              <div className="flex items-center gap-2">
                <div className="w-8 h-8 rounded-lg bg-clinic-orange/10 flex items-center justify-center">
                  <CreditCard className="w-4 h-4 text-clinic-orange" />
                </div>
                <h3 className="text-base font-bold text-gray-900">تسجيل دفعة عمولة</h3>
              </div>
              <button
                onClick={() => { setShowPayDialog(false); setPayErr(""); }}
                className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:bg-gray-100 transition"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            {/* Doctor info card (when doctor selected) */}
            {payDoctorId && selectedDoctorStats && (
              <div className="mx-6 mt-4 rounded-xl bg-clinic-blue-50 border border-clinic-blue-100 p-4">
                <p className="text-xs font-bold text-clinic-blue mb-2">
                  {doctors?.find(d => d.id === payDoctorId)?.name ?? "الطبيب"}
                </p>
                <div className="grid grid-cols-3 gap-2">
                  <div className="text-center">
                    <p className="text-[11px] text-gray-500">إجمالي المستحق</p>
                    <p className="text-sm font-bold text-purple-700">{fmt(selectedDoctorStats.totalEarned)} ر.ي</p>
                  </div>
                  <div className="text-center">
                    <p className="text-[11px] text-gray-500">تم الدفع</p>
                    <p className="text-sm font-bold text-green-700">{fmt(selectedDoctorStats.totalPaid)} ر.ي</p>
                  </div>
                  <div className="text-center">
                    <p className="text-[11px] text-gray-500">المتبقي</p>
                    <p className="text-sm font-bold text-amber-700">{fmt(selectedDoctorStats.totalRemaining)} ر.ي</p>
                  </div>
                </div>
              </div>
            )}

            {/* Form fields */}
            <div className="px-6 py-4 space-y-4">
              <div>
                <label className="block text-xs font-semibold text-gray-600 mb-1">
                  الطبيب <span className="text-red-500">*</span>
                </label>
                <select
                  value={payDoctorId}
                  onChange={(e) => { setPayDoctorId(e.target.value); setPayErr(""); }}
                  className="w-full rounded-lg border border-gray-300 bg-white px-3 py-2.5 text-sm outline-none focus:border-clinic-blue focus:ring-1 focus:ring-clinic-blue/20 transition"
                >
                  <option value="">اختر الطبيب</option>
                  {doctors?.filter(d => d.isActive !== false).map(d => (
                    <option key={d.id} value={d.id}>{d.name}</option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block text-xs font-semibold text-gray-600 mb-1">
                  المبلغ (ر.ي) <span className="text-red-500">*</span>
                </label>
                <input
                  type="number"
                  min={0.01}
                  step={0.01}
                  value={payAmount || ""}
                  onChange={(e) => { setPayAmount(parseFloat(e.target.value) || 0); setPayErr(""); }}
                  className={cn(
                    "w-full rounded-lg border px-3 py-2.5 text-sm outline-none transition",
                    isOverpay
                      ? "border-red-300 focus:border-red-500 focus:ring-1 focus:ring-red-200 bg-red-50/30"
                      : "border-gray-300 focus:border-clinic-blue focus:ring-1 focus:ring-clinic-blue/20"
                  )}
                  dir="ltr"
                  placeholder="0.00"
                />
                {isOverpay && (
                  <div className="flex items-center gap-1.5 mt-1.5 text-xs text-red-600">
                    <AlertTriangle className="w-3.5 h-3.5 flex-shrink-0" />
                    المبلغ يتجاوز المستحق المتبقي ({fmt(selectedDoctorStats?.totalRemaining ?? 0)} ر.ي)
                  </div>
                )}
              </div>

              <div>
                <label className="block text-xs font-semibold text-gray-600 mb-1">طريقة الدفع</label>
                <select
                  value={payMethod}
                  onChange={(e) => setPayMethod(e.target.value)}
                  className="w-full rounded-lg border border-gray-300 bg-white px-3 py-2.5 text-sm outline-none focus:border-clinic-blue focus:ring-1 focus:ring-clinic-blue/20 transition"
                >
                  <option value="cash">نقدي</option>
                  <option value="card">بطاقة</option>
                  <option value="bank_transfer">تحويل بنكي</option>
                  <option value="check">شيك</option>
                </select>
              </div>

              <div>
                <label className="block text-xs font-semibold text-gray-600 mb-1">رقم المرجع / الإيصال</label>
                <input
                  type="text"
                  value={payRef}
                  onChange={(e) => setPayRef(e.target.value)}
                  className="w-full rounded-lg border border-gray-300 px-3 py-2.5 text-sm outline-none focus:border-clinic-blue focus:ring-1 focus:ring-clinic-blue/20 transition"
                  placeholder="REF-001"
                  dir="ltr"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-gray-600 mb-1">ملاحظات</label>
                <textarea
                  value={payNotes}
                  onChange={(e) => setPayNotes(e.target.value)}
                  rows={2}
                  className="w-full rounded-lg border border-gray-300 px-3 py-2.5 text-sm outline-none focus:border-clinic-blue focus:ring-1 focus:ring-clinic-blue/20 transition resize-none"
                  placeholder="ملاحظات اختيارية…"
                />
              </div>

              {payErr && (
                <div className="flex items-start gap-2 text-xs text-red-600 bg-red-50 rounded-lg px-3 py-2.5">
                  <AlertTriangle className="w-3.5 h-3.5 flex-shrink-0 mt-0.5" />
                  <span>{payErr}</span>
                </div>
              )}
            </div>

            {/* Dialog actions */}
            <div className="flex items-center gap-3 px-6 py-4 border-t border-gray-100 bg-gray-50/30">
              <button
                onClick={handleRecordPayment}
                disabled={recordPayment.isPending || !payDoctorId || payAmount <= 0 || isOverpay}
                className="flex-1 flex items-center justify-center gap-2 px-4 py-2.5 text-sm font-semibold rounded-lg bg-clinic-orange text-white hover:bg-clinic-orange/90 disabled:opacity-50 disabled:cursor-not-allowed transition shadow-sm"
              >
                <CreditCard className="w-4 h-4" />
                {recordPayment.isPending ? "جاري التسجيل…" : "تسجيل الدفعة"}
              </button>
              <button
                onClick={() => { setShowPayDialog(false); setPayErr(""); }}
                className="px-4 py-2.5 text-sm font-medium rounded-lg border border-gray-200 hover:bg-gray-50 transition"
              >
                إلغاء
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
