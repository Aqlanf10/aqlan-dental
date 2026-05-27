"use client";

import { useState, useEffect, useCallback } from "react";
import {
  Wallet,
  FileText,
  Receipt,
  HandCoins,
  Landmark,
  Vault,
  TrendingDown,
  Truck,
  BarChart3,
  ClipboardCheck,
  AlertTriangle,
  ShieldX,
  Plus,
  Lock,
  Bell,
  CircleDot,
  CheckCircle2,
  Clock,
  XCircle,
  RefreshCw,
  Loader2,
  X,
  Trash2,
  Printer,
  ArrowRightLeft,
  Calculator,
  Eye,
  DollarSign,
  ThumbsUp,
  ThumbsDown,
} from "lucide-react";
import { useAuthStore } from "@/stores/authStore";
import { api } from "@/lib/api";
import { toast } from "@/stores/toastStore";
import type {
  DashboardData,
  PatientBalance,
  PatientBalanceDetail,
  InvoiceListItem,
  InvoiceDetail,
  PaymentListItem,
  RegisterPaymentRequest,
  ContractListItem,
  CashierSession,
  CloseSessionRequest,
  Treasury,
  CreateTreasuryRequest,
  VaultTransfer,
  CreateTransferRequest,
  ExpenseListItem,
  CreateExpenseRequest,
  SupplierListItem,
  SupplierBill,
  CreateSupplierBillRequest,
  PaySupplierBillRequest,
  ProfitLossData,
  DailyCashSummary,
  AccountBalancesData,
} from "./components/types";
import {
  EXPENSE_CATEGORIES,
  DEPOSIT_SOURCES,
  PAYMENT_METHODS,
  TREASURY_TYPES,
} from "./components/types";

/* ═══════════════════════════════════════════════════════════════════════════════
   Microsoft Fluent 2 Design Tokens
   ═══════════════════════════════════════════════════════════════════════════════ */
const tokens = {
  /* Surface */
  bg:              "#faf9f8",
  card:            "#ffffff",
  cardHover:       "#f3f2f1",
  /* Brand */
  brand:           "#0078d4",
  brandLight:      "#deecf9",
  /* Text */
  textPrimary:     "#323130",
  textSecondary:   "#605e5c",
  textTertiary:    "#a19f9d",
  textOnBrand:     "#ffffff",
  /* Borders */
  border:          "#edebe9",
  /* Semantic */
  warningBg:       "#fff4ce",
  warningBorder:   "#ffb900",
  warningText:     "#8a6914",
  infoBg:          "#deecf9",
  infoBorder:      "#0078d4",
  infoText:        "#0b5fa5",
  successBg:       "#dff6dd",
  successBorder:   "#107c10",
  dangerBg:        "#fde7e9",
  dangerBorder:    "#d13438",
  dangerText:      "#a4262c",
  /* Shadows */
  shadow2:         "0 1.6px 3.6px rgba(0,0,0,.132), 0 .3px .9px rgba(0,0,0,.108)",
  shadow4:         "0 3.2px 7.2px rgba(0,0,0,.132), 0 .6px 1.8px rgba(0,0,0,.108)",
} as const;

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab definition
   ═══════════════════════════════════════════════════════════════════════════════ */
interface TabDef {
  key: string;
  label: string;
  icon: React.ElementType;
}

const TABS: TabDef[] = [
  { key: "overview",       label: "نظرة عامة",        icon: BarChart3 },
  { key: "patient-acct",   label: "حسابات المرضى",    icon: Wallet },
  { key: "invoices",       label: "الفواتير",          icon: FileText },
  { key: "collections",    label: "التحصيل",           icon: Receipt },
  { key: "contracts",      label: "العقود",            icon: HandCoins },
  { key: "cashier",        label: "الصندوق",           icon: Vault },
  { key: "treasuries",     label: "الخزائن",           icon: Landmark },
  { key: "expenses",       label: "المصروفات",         icon: TrendingDown },
  { key: "suppliers",      label: "الموردون",          icon: Truck },
  { key: "audit",          label: "سجل المراجعة",      icon: ClipboardCheck },
];

/* ═══════════════════════════════════════════════════════════════════════════════
   Helpers
   ═══════════════════════════════════════════════════════════════════════════════ */
function todayArabic(): string {
  return new Date().toLocaleDateString("ar-SA", {
    weekday: "long",
    year: "numeric",
    month: "long",
    day: "numeric",
  });
}

function formatYER(amount: number): string {
  return amount.toLocaleString("ar-YE", { minimumFractionDigits: 0, maximumFractionDigits: 0 }) + " ر.ي";
}

function formatNumber(amount: number): string {
  return amount.toLocaleString("ar-YE", { minimumFractionDigits: 0, maximumFractionDigits: 0 });
}

function extractErrorMessage(err: unknown, fallback = "حدث خطأ"): string {
  if (err && typeof err === "object" && "response" in err) {
    const resp = (err as { response?: { data?: { message?: string }; status?: number } }).response;
    if (resp?.data?.message) return resp.data.message;
    if (resp?.status === 401) return "ليس لديك صلاحية. يرجى تسجيل الدخول مجدداً.";
    if (resp?.status === 403) return "غير مصرح بهذا الإجراء.";
  }
  return fallback;
}

/* Phase status items removed — no longer used in this version */

/* ═══════════════════════════════════════════════════════════════════════════════
   Shared UI Primitives
   ═══════════════════════════════════════════════════════════════════════════════ */

/* ── KPI Card ── */
function KpiCard({ label, value, sublabel, color, icon }: { label: string; value: string; sublabel?: string; color: string; icon: React.ReactNode }) {
  return (
    <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
      <div className="flex items-center gap-2 mb-2">
        <div className="w-7 h-7 rounded-md flex items-center justify-center" style={{ backgroundColor: `${color}15` }}>{icon}</div>
        <span className="text-xs font-medium" style={{ color: tokens.textTertiary }}>{label}</span>
      </div>
      <p className="text-base font-bold" style={{ color }}>{value}</p>
      {sublabel && <p className="text-[11px] mt-0.5" style={{ color: tokens.textTertiary }}>{sublabel}</p>}
    </div>
  );
}

/* ── Section header ── */
function SectionHeader({ title, action }: { title: string; action?: React.ReactNode }) {
  return (
    <div className="flex items-center justify-between mb-4">
      <h3 className="text-sm font-semibold" style={{ color: tokens.textPrimary }}>{title}</h3>
      {action}
    </div>
  );
}

/* ── Modal wrapper ── */
function Modal({ open, onClose, title, children, wide }: { open: boolean; onClose: () => void; title: string; children: React.ReactNode; wide?: boolean }) {
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div
        className="relative rounded-lg shadow-2xl w-full overflow-y-auto max-h-[85vh]"
        style={{ backgroundColor: tokens.card, maxWidth: wide ? 700 : 480, boxShadow: tokens.shadow4 }}
      >
        <div className="flex items-center justify-between px-5 py-4 border-b" style={{ borderColor: tokens.border }}>
          <h3 className="text-base font-bold" style={{ color: tokens.textPrimary }}>{title}</h3>
          <button onClick={onClose} className="w-7 h-7 rounded-md flex items-center justify-center transition-colors" style={{ color: tokens.textSecondary }} onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = tokens.cardHover; }} onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}>
            <X className="w-4 h-4" />
          </button>
        </div>
        <div className="p-5">{children}</div>
      </div>
    </div>
  );
}

/* ── Confirm dialog ── */
function ConfirmDialog({ open, onClose, onConfirm, title, message, confirmLabel, danger }: { open: boolean; onClose: () => void; onConfirm: () => void; title: string; message: string; confirmLabel?: string; danger?: boolean }) {
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative rounded-lg shadow-2xl w-full max-w-sm p-6" style={{ backgroundColor: tokens.card, boxShadow: tokens.shadow4 }}>
        <h3 className="text-base font-bold mb-2" style={{ color: tokens.textPrimary }}>{title}</h3>
        <p className="text-sm leading-relaxed mb-5" style={{ color: tokens.textSecondary }}>{message}</p>
        <div className="flex items-center gap-3">
          <button onClick={onClose} className="flex-1 px-4 py-2 rounded-md text-sm font-medium" style={{ color: tokens.textSecondary, border: `1px solid ${tokens.border}` }}>إلغاء</button>
          <button onClick={onConfirm} className="flex-1 px-4 py-2 rounded-md text-sm font-semibold text-white" style={{ backgroundColor: danger ? tokens.dangerBorder : tokens.brand }}>{confirmLabel ?? "تأكيد"}</button>
        </div>
      </div>
    </div>
  );
}

/* ── Status badge ── */
function StatusBadge({ status }: { status: string }) {
  const map: Record<string, { bg: string; text: string; label: string }> = {
    Draft:           { bg: tokens.infoBg,     text: tokens.infoText,    label: "مسودة" },
    Issued:          { bg: tokens.brandLight,  text: tokens.brand,      label: "صادرة" },
    Paid:            { bg: tokens.successBg,   text: tokens.successBorder, label: "مدفوعة" },
    PartiallyPaid:   { bg: tokens.warningBg,   text: tokens.warningText, label: "مدفوعة جزئياً" },
    Cancelled:       { bg: tokens.cardHover,   text: tokens.textTertiary, label: "ملغاة" },
    Pending:         { bg: tokens.warningBg,   text: tokens.warningText, label: "قيد المراجعة" },
    Approved:        { bg: tokens.successBg,   text: tokens.successBorder, label: "معتمد" },
    Rejected:        { bg: tokens.dangerBg,    text: tokens.dangerText, label: "مرفوض" },
    Active:          { bg: tokens.successBg,   text: tokens.successBorder, label: "نشط" },
    Completed:       { bg: tokens.brandLight,  text: tokens.brand,      label: "مكتمل" },
    Open:            { bg: tokens.successBg,   text: tokens.successBorder, label: "مفتوحة" },
    Closed:          { bg: tokens.cardHover,   text: tokens.textTertiary, label: "مقفولة" },
    Overdue:         { bg: tokens.dangerBg,    text: tokens.dangerText, label: "متأخرة" },
  };
  const cfg = map[status] ?? { bg: tokens.cardHover, text: tokens.textSecondary, label: status };
  return (
    <span className="inline-flex text-[11px] font-semibold px-2 py-0.5 rounded-full" style={{ backgroundColor: cfg.bg, color: cfg.text }}>
      {cfg.label}
    </span>
  );
}

/* ── Data table ── */
function DataTable<T>({ columns, data, onRowClick, keyField }: { columns: { key: string; label: string; render?: (row: T) => React.ReactNode }[]; data: T[]; onRowClick?: (row: T) => void; keyField: keyof T }) {
  return (
    <div className="overflow-x-auto rounded-lg border" style={{ borderColor: tokens.border }}>
      <table className="w-full text-sm">
        <thead>
          <tr style={{ backgroundColor: tokens.cardHover }}>
            {columns.map((col) => (
              <th key={col.key} className="text-right px-4 py-2.5 font-semibold text-xs" style={{ color: tokens.textSecondary }}>{col.label}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {data.map((row) => (
            <tr
              key={String(row[keyField])}
              className="transition-colors cursor-default"
              style={{ borderBottom: `1px solid ${tokens.border}` }}
              onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = tokens.cardHover; }}
              onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}
              onClick={onRowClick ? () => onRowClick(row) : undefined}
            >
              {columns.map((col) => (
                <td key={col.key} className="px-4 py-2.5" style={{ color: tokens.textPrimary }}>
                  {col.render ? col.render(row) : String((row as Record<string, unknown>)[col.key] ?? "—")}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/* ── Loading skeleton ── */
function LoadingSkeleton({ rows = 5 }: { rows?: number }) {
  return (
    <div className="space-y-2">
      {Array.from({ length: rows }).map((_, i) => (
        <div key={i} className="rounded-md h-10 animate-pulse" style={{ backgroundColor: tokens.cardHover }} />
      ))}
    </div>
  );
}

/* ── Empty state ── */
function EmptyState({ icon: Icon, message }: { icon: React.ElementType; message: string }) {
  return (
    <div className="flex flex-col items-center justify-center py-16 px-6">
      <div className="w-14 h-14 rounded-full flex items-center justify-center mb-4" style={{ backgroundColor: tokens.brandLight }}>
        <Icon className="w-7 h-7" style={{ color: tokens.brand }} />
      </div>
      <p className="text-sm" style={{ color: tokens.textSecondary }}>{message}</p>
    </div>
  );
}

/* ── Form field styles ── */
const inputStyle: React.CSSProperties = {
  width: "100%",
  border: `1px solid ${tokens.border}`,
  borderRadius: 6,
  padding: "8px 12px",
  fontSize: 14,
  outline: "none",
  color: tokens.textPrimary,
  backgroundColor: tokens.card,
};

const labelStyle: React.CSSProperties = {
  display: "block",
  fontSize: 13,
  fontWeight: 600,
  color: tokens.textSecondary,
  marginBottom: 4,
};

const btnPrimary: React.CSSProperties = {
  display: "inline-flex",
  alignItems: "center",
  justifyContent: "center",
  gap: 6,
  padding: "8px 20px",
  borderRadius: 6,
  fontSize: 13,
  fontWeight: 600,
  color: tokens.textOnBrand,
  backgroundColor: tokens.brand,
  border: "none",
  cursor: "pointer",
};

const btnDanger: React.CSSProperties = {
  display: "inline-flex",
  alignItems: "center",
  justifyContent: "center",
  gap: 6,
  padding: "8px 20px",
  borderRadius: 6,
  fontSize: 13,
  fontWeight: 600,
  color: tokens.textOnBrand,
  backgroundColor: tokens.dangerBorder,
  border: "none",
  cursor: "pointer",
};

const btnGhost: React.CSSProperties = {
  display: "inline-flex",
  alignItems: "center",
  justifyContent: "center",
  gap: 6,
  padding: "8px 16px",
  borderRadius: 6,
  fontSize: 13,
  fontWeight: 500,
  color: tokens.textSecondary,
  backgroundColor: "transparent",
  border: `1px solid ${tokens.border}`,
  cursor: "pointer",
};

/* ═══════════════════════════════════════════════════════════════════════════════
   Access Denied
   ═══════════════════════════════════════════════════════════════════════════════ */
function AccessDenied() {
  return (
    <div className="min-h-screen flex items-center justify-center" style={{ backgroundColor: tokens.bg, direction: "rtl" }}>
      <div className="rounded-lg border p-8 max-w-md text-center" style={{ backgroundColor: tokens.card, borderColor: tokens.dangerBorder, boxShadow: tokens.shadow4 }}>
        <div className="w-14 h-14 rounded-full flex items-center justify-center mx-auto mb-4" style={{ backgroundColor: tokens.dangerBg }}>
          <ShieldX className="w-7 h-7" style={{ color: tokens.dangerBorder }} />
        </div>
        <h2 className="text-lg font-bold mb-2" style={{ color: tokens.textPrimary }}>غير مصرح بالوصول</h2>
        <p className="text-sm leading-relaxed" style={{ color: tokens.textSecondary }}>
          هذه الشاشة متاحة فقط للمسؤول والمحاسب. إذا كنت تحتاج الوصول إلى تسجيل التحصيل، يرجى استخدام شاشة التشغيل اليومي.
        </p>
      </div>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 1: Overview
   ═══════════════════════════════════════════════════════════════════════════════ */
function OverviewTab() {
  const [data, setData] = useState<DashboardData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [plData, setPlData] = useState<ProfitLossData | null>(null);
  const [plLoading, setPlLoading] = useState(false);

  const fetchDashboard = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const { data } = await api.get<DashboardData>("/api/finance-v3/dashboard");
      setData(data);
    } catch (err: unknown) {
      if (err && typeof err === "object" && "response" in err) {
        const status = (err as { response?: { status?: number } }).response?.status;
        if (status === 401 || status === 403) {
          setError("ليس لديك صلاحية الوصول. يرجى تسجيل الدخول مجدداً أو التواصل مع المسؤول.");
        } else {
          setError("فشل في تحميل البيانات. يرجى المحاولة لاحقاً.");
        }
      } else {
        setError("فشل في الاتصال بالخادم. تحقق من اتصال الإنترنت وحاول مجدداً.");
      }
    } finally {
      setLoading(false);
    }
  }, []);

  const fetchPL = useCallback(async () => {
    try {
      setPlLoading(true);
      const now = new Date();
      const from = new Date(now.getFullYear(), now.getMonth(), 1).toISOString().slice(0, 10);
      const to = now.toISOString().slice(0, 10);
      const { data } = await api.get<ProfitLossData>("/api/finance-v3/profit-loss", { params: { from, to } });
      setPlData(data);
    } catch {
      // P&L is supplementary — don't block the page
    } finally {
      setPlLoading(false);
    }
  }, []);

  useEffect(() => { fetchDashboard(); fetchPL(); }, [fetchDashboard, fetchPL]);

  return (
    <div className="p-6 space-y-6">
      {/* Header with refresh */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-base font-semibold mb-1" style={{ color: tokens.textPrimary }}>المركز المالي</h2>
          <p className="text-sm leading-relaxed" style={{ color: tokens.textSecondary }}>
            مرحباً بك في المركز المالي. يتم تسجيل تحصيل المرضى من شاشة التشغيل اليومي، بينما هذه الشاشة مخصصة للمراجعة والتسوية والتقارير.
          </p>
        </div>
        <button
          onClick={() => { fetchDashboard(); fetchPL(); }}
          className="w-8 h-8 rounded-md flex items-center justify-center transition-colors"
          style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }}
          onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = tokens.brandLight; }}
          onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}
          title="تحديث البيانات"
        >
          <RefreshCw className="w-4 h-4" />
        </button>
      </div>

      {/* Live KPI Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {loading ? (
          Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="rounded-lg border p-4 animate-pulse" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
              <div className="h-3 w-20 rounded mb-2" style={{ backgroundColor: tokens.cardHover }} />
              <div className="h-6 w-32 rounded" style={{ backgroundColor: tokens.cardHover }} />
            </div>
          ))
        ) : error ? (
          <div className="col-span-full rounded-lg border p-4" style={{ backgroundColor: tokens.dangerBg, borderColor: tokens.dangerBorder }}>
            <p className="text-sm" style={{ color: tokens.dangerText }}>{error}</p>
            <button onClick={fetchDashboard} className="text-xs font-medium mt-2 underline" style={{ color: tokens.brand }}>إعادة المحاولة</button>
          </div>
        ) : data ? (
          <>
            <KpiCard label="إيراد اليوم (مستحق)" value={formatYER(data.TodayAccruedRevenue)} sublabel={`التدفقات الداخلة: ${formatYER(data.TodayInflow)}`} color={tokens.successBorder} icon={<Receipt className="w-4 h-4" />} />
            <KpiCard label="التدفقات الخارجة اليوم" value={formatYER(data.TodayOutflow)} sublabel={`شهري: ${formatYER(data.MonthOutflow)}`} color={tokens.dangerBorder} icon={<TrendingDown className="w-4 h-4" />} />
            <KpiCard label="رصيد الخزائن" value={formatYER(data.TotalTreasuryBalance)} sublabel={`${data.JournalEntryCount} قيد محاسبي`} color={tokens.brand} icon={<Vault className="w-4 h-4" />} />
            <KpiCard label="المستحقات المعلقة" value={formatYER(data.TotalOutstanding)} sublabel={`عقود: ${formatYER(data.ContractOutstanding)}`} color={tokens.warningBorder} icon={<HandCoins className="w-4 h-4" />} />
          </>
        ) : null}
      </div>

      {/* Dual-write health + Pending actions */}
      {data && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
            <div className="flex items-center gap-2 mb-3">
              <CheckCircle2 className="w-4 h-4" style={{ color: tokens.successBorder }} />
              <h4 className="text-sm font-semibold" style={{ color: tokens.textPrimary }}>حالة الكتابة المزدوجة</h4>
            </div>
            <div className="space-y-2">
              <div className="flex justify-between text-xs"><span style={{ color: tokens.textSecondary }}>إجمالي القيود</span><span className="font-bold" style={{ color: tokens.textPrimary }}>{data.JournalEntryCount}</span></div>
              <div className="flex justify-between text-xs"><span style={{ color: tokens.textSecondary }}>قيود مرحّلة</span><span className="font-bold" style={{ color: tokens.successBorder }}>{data.PostedEntryCount}</span></div>
              <div className="flex justify-between text-xs"><span style={{ color: tokens.textSecondary }}>قيود عكسية</span><span className="font-bold" style={{ color: tokens.warningText }}>{data.ReversalEntryCount}</span></div>
              <div className="flex justify-between text-xs"><span style={{ color: tokens.textSecondary }}>نسبة التغطية</span><span className="font-bold" style={{ color: tokens.brand }}>{data.DualWriteCoverage}</span></div>
            </div>
          </div>
          <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
            <div className="flex items-center gap-2 mb-3">
              <Clock className="w-4 h-4" style={{ color: tokens.warningBorder }} />
              <h4 className="text-sm font-semibold" style={{ color: tokens.textPrimary }}>إجراءات معلقة</h4>
            </div>
            <div className="space-y-2">
              <div className="flex justify-between text-xs"><span style={{ color: tokens.textSecondary }}>مصروفات بانتظار الاعتماد</span><span className="font-bold" style={{ color: data.PendingExpenses > 0 ? tokens.warningText : tokens.textPrimary }}>{data.PendingExpenses}</span></div>
              <div className="flex justify-between text-xs"><span style={{ color: tokens.textSecondary }}>تحويلات معلقة</span><span className="font-bold" style={{ color: data.PendingTransfers > 0 ? tokens.warningText : tokens.textPrimary }}>{data.PendingTransfers}</span></div>
              <div className="flex justify-between text-xs"><span style={{ color: tokens.textSecondary }}>فواتير غير مدفوعة</span><span className="font-bold" style={{ color: tokens.textPrimary }}>{formatYER(data.InvoiceOutstanding)}</span></div>
            </div>
          </div>
        </div>
      )}

      {/* P&L Summary */}
      <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
        <div className="flex items-center gap-2 mb-3">
          <BarChart3 className="w-4 h-4" style={{ color: tokens.brand }} />
          <h4 className="text-sm font-semibold" style={{ color: tokens.textPrimary }}>ملخص الأرباح والخسائر (الشهر الحالي)</h4>
        </div>
        {plLoading ? (
          <div className="animate-pulse space-y-2">
            <div className="h-4 w-40 rounded" style={{ backgroundColor: tokens.cardHover }} />
            <div className="h-4 w-60 rounded" style={{ backgroundColor: tokens.cardHover }} />
          </div>
        ) : plData ? (
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>الإيرادات المستحقة</p><p className="text-sm font-bold" style={{ color: tokens.successBorder }}>{formatYER(plData.AccruedRevenue)}</p></div>
            <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>المصروفات المستحقة</p><p className="text-sm font-bold" style={{ color: tokens.dangerBorder }}>{formatYER(plData.AccruedExpenses)}</p></div>
            <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>صافي الربح المستحق</p><p className="text-sm font-bold" style={{ color: plData.AccruedNetProfit >= 0 ? tokens.successBorder : tokens.dangerBorder }}>{formatYER(plData.AccruedNetProfit)}</p></div>
            <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>صافي الربح النقدي</p><p className="text-sm font-bold" style={{ color: plData.CashNetProfit >= 0 ? tokens.successBorder : tokens.dangerBorder }}>{formatYER(plData.CashNetProfit)}</p></div>
            <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>التحصيلات النقدية</p><p className="text-sm font-bold" style={{ color: tokens.brand }}>{formatYER(plData.CashCollections)}</p></div>
            <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>المرتجعات النقدية</p><p className="text-sm font-bold" style={{ color: tokens.warningText }}>{formatYER(plData.CashRefunds)}</p></div>
            <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>إجمالي التكاليف</p><p className="text-sm font-bold" style={{ color: tokens.dangerBorder }}>{formatYER(plData.TotalCosts)}</p></div>
            <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>هامش الربح</p><p className="text-sm font-bold" style={{ color: tokens.brand }}>{plData.ProfitMargin.toFixed(1)}%</p></div>
          </div>
        ) : (
          <p className="text-xs" style={{ color: tokens.textTertiary }}>لم يتم تحميل بيانات الأرباح والخسائر</p>
        )}
      </div>

      {/* Monthly summary */}
      {data && (
        <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
          <h4 className="text-sm font-semibold mb-3" style={{ color: tokens.textPrimary }}>ملخص الشهر</h4>
          <div className="grid grid-cols-3 gap-4 text-center">
            <div><p className="text-xs" style={{ color: tokens.textTertiary }}>الإيرادات المستحقة</p><p className="text-lg font-bold" style={{ color: tokens.successBorder }}>{formatYER(data.MonthAccruedRevenue)}</p></div>
            <div><p className="text-xs" style={{ color: tokens.textTertiary }}>التدفقات الخارجة</p><p className="text-lg font-bold" style={{ color: tokens.dangerBorder }}>{formatYER(data.MonthOutflow)}</p></div>
            <div><p className="text-xs" style={{ color: tokens.textTertiary }}>صافي التدفق</p><p className="text-lg font-bold" style={{ color: data.MonthNet >= 0 ? tokens.successBorder : tokens.dangerBorder }}>{formatYER(data.MonthNet)}</p></div>
          </div>
        </div>
      )}
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 2: Patient Accounts
   ═══════════════════════════════════════════════════════════════════════════════ */
function PatientAccountsTab() {
  const [data, setData] = useState<PatientBalance[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [selected, setSelected] = useState<PatientBalanceDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [showPayment, setShowPayment] = useState(false);

  const fetchData = useCallback(async () => {
    try { setLoading(true); const { data: responseData } = await api.get<{ data: PatientBalance[]; total: number }>("/api/finance-v3/patient-accounts"); setData(responseData.data); } catch { toast.error("فشل في تحميل حسابات المرضى"); } finally { setLoading(false); }
  }, []);

  useEffect(() => { fetchData(); }, [fetchData]);

  const openDetail = async (patient: PatientBalance) => {
    try {
      setDetailLoading(true);
      const { data } = await api.get<PatientBalanceDetail>(`/api/finance-v3/patient-balance/${patient.PatientId}`);
      setSelected(data);
    } catch { toast.error("فشل في تحميل تفاصيل الحساب"); } finally { setDetailLoading(false); }
  };

  const filtered = data.filter((p) =>
    p.PatientName.includes(search) || p.PatientNumber.includes(search)
  );

  return (
    <div className="p-6 space-y-4">
      <SectionHeader title="حسابات المرضى" action={
        <div className="flex items-center gap-2">
          <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="بحث بالاسم أو الرقم..." style={{ ...inputStyle, width: 240, fontSize: 13 }} />
          <button onClick={fetchData} className="w-8 h-8 rounded-md flex items-center justify-center" style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }} title="تحديث"><RefreshCw className="w-4 h-4" /></button>
        </div>
      } />

      {loading ? <LoadingSkeleton /> : filtered.length === 0 ? <EmptyState icon={Wallet} message="لا يوجد حسابات مرضى" /> : (
        <DataTable<PatientBalance>
          keyField="PatientId"
          data={filtered}
          onRowClick={openDetail}
          columns={[
            { key: "PatientNumber", label: "رقم المريض" },
            { key: "PatientName", label: "الاسم" },
            { key: "TotalInvoiced", label: "إجمالي الفواتير", render: (r) => formatYER(r.TotalInvoiced) },
            { key: "TotalPaid", label: "إجمالي المدفوع", render: (r) => formatYER(r.TotalPaid) },
            { key: "Balance", label: "الرصيد", render: (r) => <span style={{ color: r.Balance > 0 ? tokens.dangerBorder : tokens.successBorder, fontWeight: 700 }}>{formatYER(r.Balance)}</span> },
            { key: "HasOutstanding", label: "معلّق", render: (r) => r.HasOutstanding ? <AlertTriangle className="w-4 h-4" style={{ color: tokens.warningBorder }} /> : <CheckCircle2 className="w-4 h-4" style={{ color: tokens.successBorder }} /> },
          ]}
        />
      )}

      {/* Detail modal */}
      <Modal open={!!selected} onClose={() => { setSelected(null); setShowPayment(false); }} title={selected?.PatientName ?? "تفاصيل الحساب"} wide>
        {detailLoading ? <LoadingSkeleton rows={4} /> : selected ? (
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-3">
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>رقم المريض</p><p className="text-sm font-bold" style={{ color: tokens.textPrimary }}>{selected.PatientNumber}</p></div>
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>إجمالي الفواتير</p><p className="text-sm font-bold" style={{ color: tokens.textPrimary }}>{formatYER(selected.TotalInvoiced)}</p></div>
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>إجمالي المدفوع</p><p className="text-sm font-bold" style={{ color: tokens.successBorder }}>{formatYER(selected.NetPaid)}</p></div>
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>المرتجعات</p><p className="text-sm font-bold" style={{ color: tokens.warningText }}>{formatYER(selected.TotalRefunds)}</p></div>
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>الخصومات</p><p className="text-sm font-bold" style={{ color: tokens.brand }}>{formatYER(selected.TotalDiscounts)}</p></div>
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>الرصيد</p><p className="text-sm font-bold" style={{ color: selected.Balance > 0 ? tokens.dangerBorder : tokens.successBorder }}>{formatYER(selected.Balance)}</p></div>
              {selected.ContractOutstanding > 0 && <div className="col-span-2"><p className="text-[11px]" style={{ color: tokens.textTertiary }}>مستحقات العقود</p><p className="text-sm font-bold" style={{ color: tokens.warningText }}>{formatYER(selected.ContractOutstanding)}</p></div>}
            </div>
            <div className="flex justify-end gap-2 pt-2 border-t" style={{ borderColor: tokens.border }}>
              <button onClick={() => setShowPayment(true)} style={btnPrimary}>
                <DollarSign className="w-4 h-4" /> تسجيل دفعة
              </button>
            </div>
          </div>
        ) : null}
      </Modal>

      {/* Payment navigation prompt */}
      <Modal open={showPayment} onClose={() => setShowPayment(false)} title="تسجيل دفعة">
        <p className="text-sm mb-4" style={{ color: tokens.textSecondary }}>
          لتسجيل دفعة لهذا المريض، يرجى الانتقال إلى تبويب التحصيل أو شاشة التشغيل اليومي.
        </p>
        <div className="flex gap-2">
          <button onClick={() => setShowPayment(false)} style={btnGhost}>إلغاء</button>
          <a href="/daily-operations" style={btnPrimary}>الانتقال للتشغيل اليومي</a>
        </div>
      </Modal>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 3: Invoices
   ═══════════════════════════════════════════════════════════════════════════════ */
function InvoicesTab() {
  const [data, setData] = useState<InvoiceListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [detail, setDetail] = useState<InvoiceDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [confirmCancel, setConfirmCancel] = useState<string | null>(null);
  const [cancelling, setCancelling] = useState(false);

  const fetchData = useCallback(async () => {
    try { setLoading(true); const { data: responseData } = await api.get<{ data: InvoiceListItem[]; total: number }>("/api/finance-v3/invoices"); setData(responseData.data); } catch { toast.error("فشل في تحميل الفواتير"); } finally { setLoading(false); }
  }, []);

  useEffect(() => { fetchData(); }, [fetchData]);

  const openDetail = async (inv: InvoiceListItem) => {
    try {
      setDetailLoading(true);
      const { data } = await api.get<InvoiceDetail>(`/api/invoices/${inv.Id}`);
      setDetail(data);
    } catch { toast.error("فشل في تحميل تفاصيل الفاتورة"); } finally { setDetailLoading(false); }
  };

  const handleCancel = async () => {
    if (!confirmCancel) return;
    try {
      setCancelling(true);
      await api.patch(`/api/invoices/${confirmCancel}/cancel`);
      toast.success("تم إلغاء الفاتورة بنجاح");
      setConfirmCancel(null);
      setDetail(null);
      fetchData();
    } catch (err) { toast.error(extractErrorMessage(err, "فشل في إلغاء الفاتورة")); } finally { setCancelling(false); }
  };

  const filtered = data.filter((i) =>
    i.InvoiceNumber.includes(search) || i.PatientName.includes(search) || i.PatientNumber.includes(search)
  );

  return (
    <div className="p-6 space-y-4">
      <SectionHeader title="الفواتير" action={
        <div className="flex items-center gap-2">
          <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="بحث بالرقم أو الاسم..." style={{ ...inputStyle, width: 240, fontSize: 13 }} />
          <button onClick={fetchData} className="w-8 h-8 rounded-md flex items-center justify-center" style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }} title="تحديث"><RefreshCw className="w-4 h-4" /></button>
        </div>
      } />

      {loading ? <LoadingSkeleton /> : filtered.length === 0 ? <EmptyState icon={FileText} message="لا توجد فواتير" /> : (
        <DataTable<InvoiceListItem>
          keyField="Id"
          data={filtered}
          onRowClick={openDetail}
          columns={[
            { key: "InvoiceNumber", label: "رقم الفاتورة" },
            { key: "PatientName", label: "المريض" },
            { key: "TotalAmount", label: "الإجمالي", render: (r) => formatYER(r.TotalAmount) },
            { key: "PaidAmount", label: "المدفوع", render: (r) => formatYER(r.PaidAmount) },
            { key: "Balance", label: "المتبقي", render: (r) => <span style={{ color: r.Balance > 0 ? tokens.dangerBorder : tokens.successBorder, fontWeight: 700 }}>{formatYER(r.Balance)}</span> },
            { key: "Status", label: "الحالة", render: (r) => <StatusBadge status={r.Status} /> },
            { key: "IssueDate", label: "التاريخ", render: (r) => new Date(r.IssueDate).toLocaleDateString("ar-SA") },
          ]}
        />
      )}

      {/* Invoice detail modal */}
      <Modal open={!!detail} onClose={() => setDetail(null)} title={`فاتورة ${detail?.InvoiceNumber ?? ""}`} wide>
        {detailLoading ? <LoadingSkeleton rows={4} /> : detail ? (
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-3">
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>المريض</p><p className="text-sm font-bold" style={{ color: tokens.textPrimary }}>{detail.PatientName} ({detail.PatientNumber})</p></div>
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>الحالة</p><StatusBadge status={detail.Status} /></div>
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>الإجمالي</p><p className="text-sm font-bold">{formatYER(detail.TotalAmount)}</p></div>
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>المدفوع</p><p className="text-sm font-bold" style={{ color: tokens.successBorder }}>{formatYER(detail.PaidAmount)}</p></div>
            </div>

            {/* Line items */}
            {detail.LineItems && detail.LineItems.length > 0 && (
              <div>
                <h4 className="text-xs font-semibold mb-2" style={{ color: tokens.textSecondary }}>البنود</h4>
                <div className="overflow-x-auto rounded-md border" style={{ borderColor: tokens.border }}>
                  <table className="w-full text-xs">
                    <thead><tr style={{ backgroundColor: tokens.cardHover }}>
                      <th className="text-right px-3 py-2">العلاج</th>
                      <th className="text-right px-3 py-2">السن</th>
                      <th className="text-right px-3 py-2">الكمية</th>
                      <th className="text-right px-3 py-2">سعر الوحدة</th>
                      <th className="text-right px-3 py-2">الخصم</th>
                      <th className="text-right px-3 py-2">الإجمالي</th>
                    </tr></thead>
                    <tbody>
                      {detail.LineItems.map((li) => (
                        <tr key={li.Id} style={{ borderBottom: `1px solid ${tokens.border}` }}>
                          <td className="px-3 py-2">{li.TreatmentName}</td>
                          <td className="px-3 py-2">{li.ToothNumber ?? "—"}</td>
                          <td className="px-3 py-2">{li.Quantity}</td>
                          <td className="px-3 py-2">{formatYER(li.UnitPrice)}</td>
                          <td className="px-3 py-2">{formatYER(li.DiscountAmount)}</td>
                          <td className="px-3 py-2 font-bold">{formatYER(li.TotalPrice)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            {/* Cancel action */}
            {detail.Status === "Draft" && (
              <div className="flex justify-end pt-2 border-t" style={{ borderColor: tokens.border }}>
                <button onClick={() => setConfirmCancel(detail.Id)} style={btnDanger}>
                  <XCircle className="w-4 h-4" /> إلغاء الفاتورة
                </button>
              </div>
            )}
          </div>
        ) : null}
      </Modal>

      <ConfirmDialog open={!!confirmCancel} onClose={() => setConfirmCancel(null)} onConfirm={handleCancel} title="إلغاء الفاتورة" message="هل أنت متأكد من إلغاء هذه الفاتورة؟ لا يمكن التراجع عن هذا الإجراء." confirmLabel="إلغاء الفاتورة" danger />
      {cancelling && <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/20"><Loader2 className="w-6 h-6 animate-spin" style={{ color: tokens.brand }} /></div>}
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 4: Collections
   ═══════════════════════════════════════════════════════════════════════════════ */
function CollectionsTab() {
  const [payments, setPayments] = useState<PaymentListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [showRegister, setShowRegister] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  // Register payment form state
  const [patientSearch, setPatientSearch] = useState("");
  const [patientOptions, setPatientOptions] = useState<{ id: string; fullName: string; patientNumber: string }[]>([]);
  const [selectedPatient, setSelectedPatient] = useState("");
  const [invoiceOptions, setInvoiceOptions] = useState<{ id: string; invoiceNumber: string; balance: number }[]>([]);
  const [contractOptions, setContractOptions] = useState<{ id: string; contractNumber: string; outstandingAmount: number }[]>([]);
  const [selectedInvoice, setSelectedInvoice] = useState("");
  const [selectedContract, setSelectedContract] = useState("");
  const [payAmount, setPayAmount] = useState("");
  const [payMethod, setPayMethod] = useState("cash");
  const [payNotes, setPayNotes] = useState("");

  const fetchPayments = useCallback(async () => {
    try { setLoading(true); const { data: responseData } = await api.get<{ data: PaymentListItem[]; total: number }>("/api/finance-v3/payments"); setPayments(responseData.data); } catch { toast.error("فشل في تحميل التحصيلات"); } finally { setLoading(false); }
  }, []);

  useEffect(() => { fetchPayments(); }, [fetchPayments]);

  // Patient search
  const searchPatients = useCallback(async (q: string) => {
    if (q.length < 2) { setPatientOptions([]); return; }
    try {
      const { data } = await api.get<{ data: { id: string; fullName: string; patientNumber: string }[] }>("/api/patients", { params: { search: q, pageSize: 10 } });
      setPatientOptions(data.data ?? data as unknown as { id: string; fullName: string; patientNumber: string }[] ?? []);
    } catch { /* ignore */ }
  }, []);

  const onPatientSelect = async (patientId: string) => {
    setSelectedPatient(patientId);
    setSelectedInvoice("");
    setSelectedContract("");
    try {
      const [invRes, conRes] = await Promise.all([
        api.get<{ data: InvoiceListItem[] }>(`/api/patients/${patientId}/invoices`),
        api.get<ContractListItem[]>(`/api/patients/${patientId}/contracts`),
      ]);
      const invData = invRes.data?.data ?? invRes.data as unknown as InvoiceListItem[];
      setInvoiceOptions((Array.isArray(invData) ? invData : []).filter((i) => i.Balance > 0).map((i) => ({ id: i.Id, invoiceNumber: i.InvoiceNumber, balance: i.Balance })));
      const conData = Array.isArray(conRes.data) ? conRes.data : (conRes.data as { data?: ContractListItem[] }).data ?? [];
      setContractOptions(conData.filter((c) => c.OutstandingAmount > 0).map((c) => ({ id: c.Id, contractNumber: c.ContractNumber, outstandingAmount: c.OutstandingAmount })));
    } catch { /* ignore */ }
  };

  // Overpayment guard
  const maxAmount = (() => {
    if (selectedInvoice) { const inv = invoiceOptions.find((i) => i.id === selectedInvoice); return inv?.balance ?? 0; }
    if (selectedContract) { const con = contractOptions.find((c) => c.id === selectedContract); return con?.outstandingAmount ?? 0; }
    return 0;
  })();

  const handleRegister = async () => {
    if (!selectedPatient || !payAmount || Number(payAmount) <= 0) {
      toast.error("يرجى اختيار المريض وإدخال المبلغ");
      return;
    }
    if (maxAmount > 0 && Number(payAmount) > maxAmount) {
      toast.error(`المبلغ يتجاوز المستحق (${formatYER(maxAmount)})`);
      return;
    }
    try {
      setSubmitting(true);
      const payload: RegisterPaymentRequest = {
        PatientId: selectedPatient,
        Amount: Number(payAmount),
        PaymentMethod: payMethod,
        Notes: payNotes || undefined,
      };
      if (selectedInvoice) payload.InvoiceId = selectedInvoice;
      if (selectedContract) payload.ContractId = selectedContract;
      await api.post("/api/payments", payload);
      toast.success("تم تسجيل الدفعة بنجاح");
      resetForm();
      setShowRegister(false);
      fetchPayments();
    } catch (err) { toast.error(extractErrorMessage(err, "فشل في تسجيل الدفعة")); } finally { setSubmitting(false); }
  };

  const handleDelete = async () => {
    if (!confirmDelete) return;
    try {
      setSubmitting(true);
      await api.delete(`/api/payments/${confirmDelete}`);
      toast.success("تم عكس الدفعة بنجاح");
      setConfirmDelete(null);
      fetchPayments();
    } catch (err) { toast.error(extractErrorMessage(err, "فشل في عكس الدفعة")); } finally { setSubmitting(false); }
  };

  const resetForm = () => {
    setPatientSearch(""); setSelectedPatient(""); setPatientOptions([]);
    setInvoiceOptions([]); setContractOptions([]);
    setSelectedInvoice(""); setSelectedContract("");
    setPayAmount(""); setPayMethod("cash"); setPayNotes("");
  };

  return (
    <div className="p-6 space-y-4">
      <SectionHeader title="التحصيل" action={
        <div className="flex items-center gap-2">
          <button onClick={() => { resetForm(); setShowRegister(true); }} style={btnPrimary}><Plus className="w-4 h-4" /> تسجيل دفعة</button>
          <button onClick={fetchPayments} className="w-8 h-8 rounded-md flex items-center justify-center" style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }} title="تحديث"><RefreshCw className="w-4 h-4" /></button>
        </div>
      } />

      {loading ? <LoadingSkeleton /> : payments.length === 0 ? <EmptyState icon={Receipt} message="لا توجد تحصيلات" /> : (
        <DataTable<PaymentListItem>
          keyField="Id"
          data={payments}
          columns={[
            { key: "PaymentNumber", label: "رقم الإيصال" },
            { key: "PatientName", label: "المريض" },
            { key: "Amount", label: "المبلغ", render: (r) => formatYER(r.Amount) },
            { key: "PaymentMethod", label: "طريقة الدفع", render: (r) => PAYMENT_METHODS.find((m) => m.value === r.PaymentMethod)?.label ?? r.PaymentMethod },
            { key: "PaymentDate", label: "التاريخ", render: (r) => new Date(r.PaymentDate).toLocaleDateString("ar-SA") },
            { key: "IsReversal", label: "عكسي", render: (r) => r.IsReversal ? <span style={{ color: tokens.dangerBorder, fontWeight: 700 }}>نعم</span> : "—" },
            { key: "actions", label: "إجراءات", render: (r) => !r.IsReversal && !r.ReversedById ? (
              <div className="flex items-center gap-1">
                <button onClick={(e) => { e.stopPropagation(); setConfirmDelete(r.Id); }} className="w-7 h-7 rounded-md flex items-center justify-center" style={{ color: tokens.dangerBorder }} title="عكس الدفعة"><Trash2 className="w-3.5 h-3.5" /></button>
                <button onClick={(e) => { e.stopPropagation(); window.print(); }} className="w-7 h-7 rounded-md flex items-center justify-center" style={{ color: tokens.brand }} title="طباعة إيصال"><Printer className="w-3.5 h-3.5" /></button>
              </div>
            ) : null },
          ]}
        />
      )}

      {/* Register Payment Modal */}
      <Modal open={showRegister} onClose={() => setShowRegister(false)} title="تسجيل دفعة جديدة" wide>
        <div className="space-y-4">
          {/* Patient search */}
          <div>
            <label style={labelStyle}>المريض <span style={{ color: tokens.dangerBorder }}>*</span></label>
            <input
              value={patientSearch}
              onChange={(e) => { setPatientSearch(e.target.value); searchPatients(e.target.value); }}
              placeholder="ابحث بالاسم أو الرقم..."
              style={inputStyle}
            />
            {patientOptions.length > 0 && !selectedPatient && (
              <div className="mt-1 rounded-md border max-h-40 overflow-y-auto" style={{ borderColor: tokens.border }}>
                {patientOptions.map((p) => (
                  <button key={p.id} onClick={() => { setPatientSearch(`${p.fullName} (${p.patientNumber})`); onPatientSelect(p.id); }} className="w-full text-right px-3 py-2 text-sm transition-colors" style={{ color: tokens.textPrimary }} onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = tokens.cardHover; }} onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}>
                    {p.fullName} ({p.patientNumber})
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* Select invoice or contract */}
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label style={labelStyle}>فاتورة</label>
              <select value={selectedInvoice} onChange={(e) => { setSelectedInvoice(e.target.value); setSelectedContract(""); }} style={inputStyle}>
                <option value="">— اختر فاتورة —</option>
                {invoiceOptions.map((i) => (<option key={i.id} value={i.id}>{i.invoiceNumber} ({formatYER(i.balance)})</option>))}
              </select>
            </div>
            <div>
              <label style={labelStyle}>عقد</label>
              <select value={selectedContract} onChange={(e) => { setSelectedContract(e.target.value); setSelectedInvoice(""); }} style={inputStyle}>
                <option value="">— اختر عقد —</option>
                {contractOptions.map((c) => (<option key={c.id} value={c.id}>{c.contractNumber} ({formatYER(c.outstandingAmount)})</option>))}
              </select>
            </div>
          </div>

          {/* Amount */}
          <div>
            <label style={labelStyle}>المبلغ <span style={{ color: tokens.dangerBorder }}>*</span></label>
            <input type="number" min="0" step="0.01" value={payAmount} onChange={(e) => setPayAmount(e.target.value)} placeholder="0" dir="ltr" style={inputStyle} />
            {maxAmount > 0 && Number(payAmount) > maxAmount && (
              <p className="text-xs mt-1" style={{ color: tokens.dangerBorder }}>⚠ المبلغ يتجاوز المستحق ({formatYER(maxAmount)})</p>
            )}
          </div>

          {/* Payment method */}
          <div>
            <label style={labelStyle}>طريقة الدفع</label>
            <select value={payMethod} onChange={(e) => setPayMethod(e.target.value)} style={inputStyle}>
              {PAYMENT_METHODS.map((m) => (<option key={m.value} value={m.value}>{m.label}</option>))}
            </select>
          </div>

          {/* Notes */}
          <div>
            <label style={labelStyle}>ملاحظات</label>
            <input value={payNotes} onChange={(e) => setPayNotes(e.target.value)} placeholder="ملاحظات اختيارية..." style={inputStyle} />
          </div>

          <div className="flex gap-3 pt-2 border-t" style={{ borderColor: tokens.border }}>
            <button onClick={() => setShowRegister(false)} style={btnGhost}>إلغاء</button>
            <button onClick={handleRegister} disabled={submitting} style={{ ...btnPrimary, opacity: submitting ? 0.6 : 1 }}>
              {submitting && <Loader2 className="w-4 h-4 animate-spin" />}
              {submitting ? "جارٍ الحفظ..." : "تسجيل الدفعة"}
            </button>
          </div>
        </div>
      </Modal>

      <ConfirmDialog open={!!confirmDelete} onClose={() => setConfirmDelete(null)} onConfirm={handleDelete} title="عكس الدفعة" message="هل أنت متأكد من عكس هذه الدفعة؟ سيتم إنشاء قيد عكسي." confirmLabel="عكس الدفعة" danger />
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 5: Contracts
   ═══════════════════════════════════════════════════════════════════════════════ */
function ContractsTab() {
  const [data, setData] = useState<ContractListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("");

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      const params: Record<string, string> = {};
      if (statusFilter) params.status = statusFilter;
      const { data } = await api.get<ContractListItem[]>("/api/contracts", { params });
      setData(data);
    } catch { toast.error("فشل في تحميل العقود"); } finally { setLoading(false); }
  }, [statusFilter]);

  useEffect(() => { fetchData(); }, [fetchData]);

  const filtered = data.filter((c) =>
    c.PatientName.includes(search) || c.ContractNumber.includes(search) || c.PatientNumber.includes(search)
  );

  return (
    <div className="p-6 space-y-4">
      <SectionHeader title="العقود" action={
        <div className="flex items-center gap-2">
          <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="بحث..." style={{ ...inputStyle, width: 200, fontSize: 13 }} />
          <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)} style={{ ...inputStyle, width: 140, fontSize: 13 }}>
            <option value="">جميع الحالات</option>
            <option value="Active">نشط</option>
            <option value="Completed">مكتمل</option>
            <option value="Overdue">متأخرة</option>
          </select>
          <button onClick={fetchData} className="w-8 h-8 rounded-md flex items-center justify-center" style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }} title="تحديث"><RefreshCw className="w-4 h-4" /></button>
        </div>
      } />

      {loading ? <LoadingSkeleton /> : filtered.length === 0 ? <EmptyState icon={HandCoins} message="لا توجد عقود" /> : (
        <DataTable<ContractListItem>
          keyField="Id"
          data={filtered}
          columns={[
            { key: "ContractNumber", label: "رقم العقد" },
            { key: "PatientName", label: "المريض" },
            { key: "TotalAmount", label: "الإجمالي", render: (r) => formatYER(r.TotalAmount) },
            { key: "PaidAmount", label: "المدفوع", render: (r) => formatYER(r.PaidAmount) },
            { key: "OutstandingAmount", label: "المستحق", render: (r) => <span style={{ color: r.OutstandingAmount > 0 ? tokens.dangerBorder : tokens.successBorder, fontWeight: 700 }}>{formatYER(r.OutstandingAmount)}</span> },
            { key: "Status", label: "الحالة", render: (r) => <StatusBadge status={r.Status} /> },
            { key: "IsOverdue", label: "متأخرة", render: (r) => r.IsOverdue ? <AlertTriangle className="w-4 h-4" style={{ color: tokens.dangerBorder }} /> : "—" },
            { key: "StartDate", label: "تاريخ البداية", render: (r) => new Date(r.StartDate).toLocaleDateString("ar-SA") },
          ]}
        />
      )}
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 6: Cashier
   ═══════════════════════════════════════════════════════════════════════════════ */
function CashierTab({ isAdmin }: { isAdmin: boolean }) {
  const [sessions, setSessions] = useState<CashierSession[]>([]);
  const [loading, setLoading] = useState(true);
  const [closeSession, setCloseSession] = useState<CashierSession | null>(null);
  const [actualCash, setActualCash] = useState("");
  const [actualCard, setActualCard] = useState("");
  const [actualBank, setActualBank] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [confirmReconcile, setConfirmReconcile] = useState<string | null>(null);

  const fetchSessions = useCallback(async () => {
    try { setLoading(true); const { data: responseData } = await api.get<{ data: CashierSession[]; total: number }>("/api/cashier-sessions"); setSessions(responseData.data); } catch { toast.error("فشل في تحميل ورديات الصندوق"); } finally { setLoading(false); }
  }, []);

  useEffect(() => { fetchSessions(); }, [fetchSessions]);

  const openCloseSession = async (s: CashierSession) => {
    setCloseSession(s);
    // Fetch session detail to get accurate per-bucket expected values
    // Never initialize actual amounts from undefined — fetch first
    try {
      const { data: detail } = await api.get<{
        ExpectedClosingCash: number;
        ExpectedClosingCard: number;
        ExpectedClosingBank: number;
      }>(`/api/cashier-sessions/${s.Id}`);
      setActualCash(String(detail.ExpectedClosingCash ?? 0));
      setActualCard(String(detail.ExpectedClosingCard ?? 0));
      setActualBank(String(detail.ExpectedClosingBank ?? 0));
    } catch {
      // Fallback: use list values if available
      setActualCash(String(s.ExpectedClosingCash ?? 0));
      setActualCard(String(s.ExpectedClosingCard ?? 0));
      setActualBank(String(s.ExpectedClosingBank ?? 0));
    }
  };

  const handleClose = async () => {
    if (!closeSession) return;
    try {
      setSubmitting(true);
      const payload: CloseSessionRequest = {
        ActualClosingCash: Number(actualCash) || 0,
        ActualClosingCard: Number(actualCard) || 0,
        ActualClosingBank: Number(actualBank) || 0,
      };
      await api.post(`/api/cashier-sessions/close`, payload);
      toast.success("تم إقفال الوردية بنجاح");
      setCloseSession(null);
      fetchSessions();
    } catch (err) { toast.error(extractErrorMessage(err, "فشل في إقفال الوردية")); } finally { setSubmitting(false); }
  };

  const handleReconcile = async () => {
    if (!confirmReconcile) return;
    try {
      setSubmitting(true);
      await api.patch(`/api/cashier-sessions/${confirmReconcile}/reconcile`);
      toast.success("تم تسوية الوردية بنجاح");
      setConfirmReconcile(null);
      fetchSessions();
    } catch (err) { toast.error(extractErrorMessage(err, "فشل في التسوية")); } finally { setSubmitting(false); }
  };

  const openSession = sessions.find((s) => s.Status === "Open");

  return (
    <div className="p-6 space-y-4">
      <SectionHeader title="الصندوق" action={
        <div className="flex items-center gap-2">
          {openSession && (
            <button onClick={() => openCloseSession(openSession)} style={btnDanger}>
              <Lock className="w-4 h-4" /> إقفال الوردية
            </button>
          )}
          <button onClick={fetchSessions} className="w-8 h-8 rounded-md flex items-center justify-center" style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }} title="تحديث"><RefreshCw className="w-4 h-4" /></button>
        </div>
      } />

      {/* Current session info */}
      {openSession && (
        <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.successBg, borderColor: tokens.successBorder }}>
          <div className="flex items-center gap-2 mb-2">
            <CircleDot className="w-4 h-4" style={{ color: tokens.successBorder }} />
            <h4 className="text-sm font-bold" style={{ color: tokens.successBorder }}>وردية مفتوحة</h4>
          </div>
          <div className="grid grid-cols-3 gap-4 text-sm">
            <div><span style={{ color: tokens.textTertiary }}>الكاشر:</span> <span className="font-bold">{openSession.CashierName}</span></div>
            <div><span style={{ color: tokens.textTertiary }}>الافتتاح:</span> <span className="font-bold">{new Date(openSession.OpenedAt).toLocaleString("ar-SA")}</span></div>
            <div><span style={{ color: tokens.textTertiary }}>رصيد الافتتاح:</span> <span className="font-bold">{formatYER(openSession.OpeningBalance)}</span></div>
          </div>
        </div>
      )}

      {loading ? <LoadingSkeleton /> : sessions.length === 0 ? <EmptyState icon={Vault} message="لا توجد ورديات صندوق" /> : (
        <DataTable<CashierSession>
          keyField="Id"
          data={sessions}
          columns={[
            { key: "CashierName", label: "الكاشر" },
            { key: "OpenedAt", label: "وقت الافتتاح", render: (r) => new Date(r.OpenedAt).toLocaleString("ar-SA") },
            { key: "ClosingTime", label: "وقت الإقفال", render: (r) => r.ClosingTime ? new Date(r.ClosingTime).toLocaleString("ar-SA") : "—" },
            { key: "OpeningBalance", label: "رصيد الافتتاح", render: (r) => formatYER(r.OpeningBalance) },
            { key: "ShortageOrSurplus", label: "عجز/فائض", render: (r) => {
              if (r.ShortageOrSurplus == null) return "—";
              if (r.ShortageOrSurplus < 0) return <span style={{ color: tokens.dangerBorder, fontWeight: 700 }}>عجز {formatYER(Math.abs(r.ShortageOrSurplus))}</span>;
              if (r.ShortageOrSurplus > 0) return <span style={{ color: tokens.successBorder, fontWeight: 700 }}>فائض {formatYER(r.ShortageOrSurplus)}</span>;
              return <span style={{ color: tokens.successBorder }}>✓</span>;
            }},
            { key: "Status", label: "الحالة", render: (r) => <StatusBadge status={r.Status} /> },
            { key: "actions", label: "إجراءات", render: (r) => (
              <div className="flex items-center gap-1">
                {r.Status === "Open" && <button onClick={(e) => { e.stopPropagation(); openCloseSession(r); }} style={{ color: tokens.dangerBorder }} className="w-7 h-7 rounded-md flex items-center justify-center" title="إقفال"><Lock className="w-3.5 h-3.5" /></button>}
                {r.Status === "Closed" && isAdmin && <button onClick={(e) => { e.stopPropagation(); setConfirmReconcile(r.Id); }} style={{ color: tokens.brand }} className="w-7 h-7 rounded-md flex items-center justify-center" title="تسوية"><Calculator className="w-3.5 h-3.5" /></button>}
              </div>
            )},
          ]}
        />
      )}

      {/* Close session modal with actual counts */}
      <Modal open={!!closeSession} onClose={() => setCloseSession(null)} title="إقفال الوردية">
        {closeSession && (
          <div className="space-y-4">
            <div className="rounded-md p-3" style={{ backgroundColor: tokens.infoBg, border: `1px solid ${tokens.infoBorder}` }}>
              <p className="text-xs" style={{ color: tokens.infoText }}>
                أدخل العد الفعلي لكل وسيلة دفع. سيتم حساب العجز/الفائض تلقائياً بناءً على القيم المتوقعة.
              </p>
            </div>

            <div className="space-y-3">
              <div className="grid grid-cols-2 gap-3">
                <div><span className="text-xs" style={{ color: tokens.textTertiary }}>النقدي المتوقع</span><p className="text-sm font-bold">{formatYER(closeSession.ExpectedClosingCash)}</p></div>
                <div>
                  <label style={labelStyle}>النقدي الفعلي <span style={{ color: tokens.dangerBorder }}>*</span></label>
                  <input type="number" min="0" step="0.01" value={actualCash} onChange={(e) => setActualCash(e.target.value)} dir="ltr" style={inputStyle} />
                </div>
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div><span className="text-xs" style={{ color: tokens.textTertiary }}>البطاقة المتوقعة</span><p className="text-sm font-bold">{formatYER(closeSession.ExpectedClosingCard)}</p></div>
                <div>
                  <label style={labelStyle}>البطاقة الفعلية <span style={{ color: tokens.dangerBorder }}>*</span></label>
                  <input type="number" min="0" step="0.01" value={actualCard} onChange={(e) => setActualCard(e.target.value)} dir="ltr" style={inputStyle} />
                </div>
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div><span className="text-xs" style={{ color: tokens.textTertiary }}>البنكي المتوقع</span><p className="text-sm font-bold">{formatYER(closeSession.ExpectedClosingBank)}</p></div>
                <div>
                  <label style={labelStyle}>البنكي الفعلي <span style={{ color: tokens.dangerBorder }}>*</span></label>
                  <input type="number" min="0" step="0.01" value={actualBank} onChange={(e) => setActualBank(e.target.value)} dir="ltr" style={inputStyle} />
                </div>
              </div>
            </div>

            {/* Shortage/Surplus preview */}
            {(() => {
              const cashDiff = (Number(actualCash) || 0) - closeSession.ExpectedClosingCash;
              const cardDiff = (Number(actualCard) || 0) - closeSession.ExpectedClosingCard;
              const bankDiff = (Number(actualBank) || 0) - closeSession.ExpectedClosingBank;
              const hasDiff = cashDiff !== 0 || cardDiff !== 0 || bankDiff !== 0;
              if (!hasDiff) return null;
              return (
                <div className="rounded-md p-3" style={{ backgroundColor: tokens.warningBg, border: `1px solid ${tokens.warningBorder}` }}>
                  <h4 className="text-xs font-bold mb-2" style={{ color: tokens.warningText }}>الفروقات</h4>
                  <div className="space-y-1 text-xs">
                    {cashDiff !== 0 && <div className="flex justify-between"><span style={{ color: tokens.warningText }}>نقدي:</span><span style={{ color: cashDiff < 0 ? tokens.dangerBorder : tokens.successBorder, fontWeight: 700 }}>{cashDiff < 0 ? "عجز" : "فائض"} {formatYER(Math.abs(cashDiff))}</span></div>}
                    {cardDiff !== 0 && <div className="flex justify-between"><span style={{ color: tokens.warningText }}>بطاقة:</span><span style={{ color: cardDiff < 0 ? tokens.dangerBorder : tokens.successBorder, fontWeight: 700 }}>{cardDiff < 0 ? "عجز" : "فائض"} {formatYER(Math.abs(cardDiff))}</span></div>}
                    {bankDiff !== 0 && <div className="flex justify-between"><span style={{ color: tokens.warningText }}>بنكي:</span><span style={{ color: bankDiff < 0 ? tokens.dangerBorder : tokens.successBorder, fontWeight: 700 }}>{bankDiff < 0 ? "عجز" : "فائض"} {formatYER(Math.abs(bankDiff))}</span></div>}
                  </div>
                </div>
              );
            })()}

            <div className="flex gap-3 pt-2 border-t" style={{ borderColor: tokens.border }}>
              <button onClick={() => setCloseSession(null)} style={btnGhost}>إلغاء</button>
              <button onClick={handleClose} disabled={submitting} style={{ ...btnDanger, opacity: submitting ? 0.6 : 1 }}>
                {submitting && <Loader2 className="w-4 h-4 animate-spin" />}
                {submitting ? "جارٍ الإقفال..." : "إقفال الوردية"}
              </button>
            </div>
          </div>
        )}
      </Modal>

      <ConfirmDialog open={!!confirmReconcile} onClose={() => setConfirmReconcile(null)} onConfirm={handleReconcile} title="تسوية الوردية" message="هل أنت متأكد من تسوية هذه الوردية؟ هذا الإجراء متاح فقط للمسؤول." confirmLabel="تسوية" />
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 7: Treasuries
   ═══════════════════════════════════════════════════════════════════════════════ */
function TreasuriesTab() {
  const [treasuries, setTreasuries] = useState<Treasury[]>([]);
  const [transfers, setTransfers] = useState<VaultTransfer[]>([]);
  const [loading, setLoading] = useState(true);
  const [showCreateTreasury, setShowCreateTreasury] = useState(false);
  const [showCreateTransfer, setShowCreateTransfer] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [confirmApprove, setConfirmApprove] = useState<{ id: string; action: "approve" | "reject" } | null>(null);

  // Create treasury form
  const [tName, setTName] = useState("");
  const [tType, setTType] = useState("Vault");
  const [tBalance, setTBalance] = useState("0");

  // Create transfer form
  const [srcId, setSrcId] = useState("");
  const [dstId, setDstId] = useState("");
  const [trAmount, setTrAmount] = useState("");
  const [trDepositSource, setTrDepositSource] = useState("");
  const [trNotes, setTrNotes] = useState("");

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      const [tRes, trRes] = await Promise.all([
        api.get<{ data: Treasury[] }>("/api/treasuries"),
        api.get<{ data: VaultTransfer[] }>("/api/vault-transfers"),
      ]);
      setTreasuries(tRes.data.data ?? []);
      setTransfers(trRes.data.data ?? (Array.isArray(trRes.data) ? trRes.data as unknown as VaultTransfer[] : []));
    } catch { toast.error("فشل في تحميل بيانات الخزائن"); } finally { setLoading(false); }
  }, []);

  useEffect(() => { fetchData(); }, [fetchData]);

  const handleCreateTreasury = async () => {
    if (!tName.trim()) { toast.error("يرجى إدخال اسم الخزينة"); return; }
    try {
      setSubmitting(true);
      const payload: CreateTreasuryRequest = { Name: tName, Type: tType, OpeningBalance: Number(tBalance) || 0 };
      await api.post("/api/treasuries", payload);
      toast.success("تم إنشاء الخزينة بنجاح");
      setShowCreateTreasury(false);
      setTName(""); setTType("Vault"); setTBalance("0");
      fetchData();
    } catch (err) { toast.error(extractErrorMessage(err, "فشل في إنشاء الخزينة")); } finally { setSubmitting(false); }
  };

  const handleCreateTransfer = async () => {
    if (!srcId || !dstId || !trAmount || Number(trAmount) <= 0) {
      toast.error("يرجى ملء جميع الحقول المطلوبة");
      return;
    }
    if (srcId === dstId) { toast.error("لا يمكن التحويل من وإلى نفس الخزينة"); return; }
    try {
      setSubmitting(true);
      const payload: CreateTransferRequest = {
        SourceTreasuryId: srcId,
        DestinationTreasuryId: dstId,
        Amount: Number(trAmount),
        DepositSource: trDepositSource || undefined,
        Notes: trNotes || undefined,
      };
      await api.post("/api/vault-transfers", payload);
      toast.success("تم إنشاء التحويل بنجاح");
      setShowCreateTransfer(false);
      setSrcId(""); setDstId(""); setTrAmount(""); setTrDepositSource(""); setTrNotes("");
      fetchData();
    } catch (err) { toast.error(extractErrorMessage(err, "فشل في إنشاء التحويل")); } finally { setSubmitting(false); }
  };

  const handleTransferAction = async () => {
    if (!confirmApprove) return;
    try {
      setSubmitting(true);
      const url = confirmApprove.action === "approve"
        ? `/api/vault-transfers/${confirmApprove.id}/approve`
        : `/api/vault-transfers/${confirmApprove.id}/reject`;
      await api.post(url);
      toast.success(confirmApprove.action === "approve" ? "تم اعتماد التحويل" : "تم رفض التحويل");
      setConfirmApprove(null);
      fetchData();
    } catch (err) { toast.error(extractErrorMessage(err, "فشل في تنفيذ الإجراء")); } finally { setSubmitting(false); }
  };

  const handleRecalculate = async (id: string) => {
    try {
      await api.post(`/api/treasuries/${id}/recalculate`);
      toast.success("تم إعادة حساب رصيد الخزينة");
      fetchData();
    } catch (err) { toast.error(extractErrorMessage(err, "فشل في إعادة الحساب")); }
  };

  return (
    <div className="p-6 space-y-6">
      {/* Treasuries */}
      <div>
        <SectionHeader title="الخزائن" action={
          <div className="flex items-center gap-2">
            <button onClick={() => setShowCreateTreasury(true)} style={btnPrimary}><Plus className="w-4 h-4" /> خزينة جديدة</button>
            <button onClick={fetchData} className="w-8 h-8 rounded-md flex items-center justify-center" style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }} title="تحديث"><RefreshCw className="w-4 h-4" /></button>
          </div>
        } />

        {loading ? <LoadingSkeleton /> : treasuries.length === 0 ? <EmptyState icon={Landmark} message="لا توجد خزائن" /> : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {treasuries.map((t) => (
              <div key={t.Id} className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
                <div className="flex items-center justify-between mb-2">
                  <h4 className="text-sm font-bold" style={{ color: tokens.textPrimary }}>{t.Name}</h4>
                  <span className="text-[11px] px-2 py-0.5 rounded-full" style={{ backgroundColor: tokens.brandLight, color: tokens.brand }}>{TREASURY_TYPES.find((x) => x.value === t.Type)?.label ?? t.Type}</span>
                </div>
                <p className="text-lg font-bold mb-1" style={{ color: tokens.successBorder }}>{formatYER(t.Balance)}</p>
                <button onClick={() => handleRecalculate(t.Id)} className="text-xs flex items-center gap-1" style={{ color: tokens.brand }}>
                  <Calculator className="w-3 h-3" /> إعادة حساب
                </button>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Transfers */}
      <div>
        <SectionHeader title="التحويلات" action={
          <button onClick={() => setShowCreateTransfer(true)} style={btnPrimary}><ArrowRightLeft className="w-4 h-4" /> تحويل جديد</button>
        } />

        {loading ? <LoadingSkeleton /> : transfers.length === 0 ? <EmptyState icon={ArrowRightLeft} message="لا توجد تحويلات" /> : (
          <DataTable<VaultTransfer>
            keyField="Id"
            data={transfers}
            columns={[
              { key: "SourceTreasuryName", label: "من" },
              { key: "DestinationTreasuryName", label: "إلى" },
              { key: "Amount", label: "المبلغ", render: (r) => formatYER(r.Amount) },
              { key: "Status", label: "الحالة", render: (r) => <StatusBadge status={r.Status} /> },
              { key: "RequestedBy", label: "بواسطة" },
              { key: "RequestedAt", label: "التاريخ", render: (r) => new Date(r.RequestedAt).toLocaleString("ar-SA") },
              { key: "actions", label: "إجراءات", render: (r) => r.Status === "Pending" ? (
                <div className="flex items-center gap-1">
                  <button onClick={(e) => { e.stopPropagation(); setConfirmApprove({ id: r.Id, action: "approve" }); }} className="w-7 h-7 rounded-md flex items-center justify-center" style={{ color: tokens.successBorder }} title="اعتماد"><ThumbsUp className="w-3.5 h-3.5" /></button>
                  <button onClick={(e) => { e.stopPropagation(); setConfirmApprove({ id: r.Id, action: "reject" }); }} className="w-7 h-7 rounded-md flex items-center justify-center" style={{ color: tokens.dangerBorder }} title="رفض"><ThumbsDown className="w-3.5 h-3.5" /></button>
                </div>
              ) : null },
            ]}
          />
        )}
      </div>

      {/* Create Treasury Modal */}
      <Modal open={showCreateTreasury} onClose={() => setShowCreateTreasury(false)} title="خزينة جديدة">
        <div className="space-y-4">
          <div><label style={labelStyle}>اسم الخزينة <span style={{ color: tokens.dangerBorder }}>*</span></label><input value={tName} onChange={(e) => setTName(e.target.value)} placeholder="مثال: الصندوق الرئيسي" style={inputStyle} /></div>
          <div><label style={labelStyle}>النوع</label><select value={tType} onChange={(e) => setTType(e.target.value)} style={inputStyle}>{TREASURY_TYPES.map((t) => (<option key={t.value} value={t.value}>{t.label}</option>))}</select></div>
          <div><label style={labelStyle}>الرصيد الافتتاحي</label><input type="number" min="0" step="0.01" value={tBalance} onChange={(e) => setTBalance(e.target.value)} dir="ltr" style={inputStyle} /></div>
          <div className="flex gap-3 pt-2 border-t" style={{ borderColor: tokens.border }}>
            <button onClick={() => setShowCreateTreasury(false)} style={btnGhost}>إلغاء</button>
            <button onClick={handleCreateTreasury} disabled={submitting} style={{ ...btnPrimary, opacity: submitting ? 0.6 : 1 }}>{submitting ? "جارٍ الحفظ..." : "إنشاء"}</button>
          </div>
        </div>
      </Modal>

      {/* Create Transfer Modal */}
      <Modal open={showCreateTransfer} onClose={() => setShowCreateTransfer(false)} title="تحويل جديد">
        <div className="space-y-4">
          <div><label style={labelStyle}>الخزينة المصدر <span style={{ color: tokens.dangerBorder }}>*</span></label><select value={srcId} onChange={(e) => setSrcId(e.target.value)} style={inputStyle}><option value="">— اختر —</option>{treasuries.map((t) => (<option key={t.Id} value={t.Id}>{t.Name} ({formatYER(t.Balance)})</option>))}</select></div>
          <div><label style={labelStyle}>الخزينة الوجهة <span style={{ color: tokens.dangerBorder }}>*</span></label><select value={dstId} onChange={(e) => setDstId(e.target.value)} style={inputStyle}><option value="">— اختر —</option>{treasuries.map((t) => (<option key={t.Id} value={t.Id}>{t.Name} ({formatYER(t.Balance)})</option>))}</select></div>
          <div><label style={labelStyle}>المبلغ <span style={{ color: tokens.dangerBorder }}>*</span></label><input type="number" min="0" step="0.01" value={trAmount} onChange={(e) => setTrAmount(e.target.value)} dir="ltr" style={inputStyle} /></div>
          {/* Deposit source for external transfers */}
          {(() => { const srcT = treasuries.find((t) => t.Id === srcId); return srcT?.Type === "External" || (srcId && !treasuries.find((t) => t.Id === srcId)); })() ? (
            <div><label style={labelStyle}>مصدر الإيداع</label><select value={trDepositSource} onChange={(e) => setTrDepositSource(e.target.value)} style={inputStyle}><option value="">— اختر —</option>{DEPOSIT_SOURCES.map((d) => (<option key={d.value} value={d.value}>{d.label}</option>))}</select></div>
          ) : null}
          <div><label style={labelStyle}>ملاحظات</label><input value={trNotes} onChange={(e) => setTrNotes(e.target.value)} placeholder="ملاحظات اختيارية..." style={inputStyle} /></div>
          <div className="flex gap-3 pt-2 border-t" style={{ borderColor: tokens.border }}>
            <button onClick={() => setShowCreateTransfer(false)} style={btnGhost}>إلغاء</button>
            <button onClick={handleCreateTransfer} disabled={submitting} style={{ ...btnPrimary, opacity: submitting ? 0.6 : 1 }}>{submitting ? "جارٍ الحفظ..." : "إنشاء تحويل"}</button>
          </div>
        </div>
      </Modal>

      <ConfirmDialog
        open={!!confirmApprove}
        onClose={() => setConfirmApprove(null)}
        onConfirm={handleTransferAction}
        title={confirmApprove?.action === "approve" ? "اعتماد التحويل" : "رفض التحويل"}
        message={confirmApprove?.action === "approve" ? "هل أنت متأكد من اعتماد هذا التحويل؟ سيتم تحديث أرصدة الخزائن." : "هل أنت متأكد من رفض هذا التحويل؟"}
        confirmLabel={confirmApprove?.action === "approve" ? "اعتماد" : "رفض"}
        danger={confirmApprove?.action === "reject"}
      />
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 8: Expenses
   ═══════════════════════════════════════════════════════════════════════════════ */
function ExpensesTab() {
  const [data, setData] = useState<ExpenseListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [showCreate, setShowCreate] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [confirmAction, setConfirmAction] = useState<{ id: string; action: "approve" | "reject" | "delete" } | null>(null);

  // Create form
  const [eTitle, setETitle] = useState("");
  const [eCategory, setECategory] = useState("Other");
  const [eAmount, setEAmount] = useState("");
  const [eMethod, setEMethod] = useState("cash");
  const [eDate, setEDate] = useState(new Date().toISOString().slice(0, 10));

  const fetchData = useCallback(async () => {
    try { setLoading(true); const { data: responseData } = await api.get<{ data: ExpenseListItem[]; total: number }>("/api/expenses"); setData(responseData.data); } catch { toast.error("فشل في تحميل المصروفات"); } finally { setLoading(false); }
  }, []);

  useEffect(() => { fetchData(); }, [fetchData]);

  const handleCreate = async () => {
    if (!eTitle.trim() || !eAmount || Number(eAmount) <= 0) {
      toast.error("يرجى ملء جميع الحقول المطلوبة");
      return;
    }
    try {
      setSubmitting(true);
      const payload: CreateExpenseRequest = {
        Title: eTitle,
        Category: eCategory,
        Amount: Number(eAmount),
        PaymentMethod: eMethod,
        ExpenseDate: eDate,
      };
      await api.post("/api/expenses", payload);
      toast.success("تم إنشاء المصروف بنجاح");
      setShowCreate(false);
      setETitle(""); setECategory("Other"); setEAmount(""); setEMethod("cash"); setEDate(new Date().toISOString().slice(0, 10));
      fetchData();
    } catch (err) { toast.error(extractErrorMessage(err, "فشل في إنشاء المصروف")); } finally { setSubmitting(false); }
  };

  const handleAction = async () => {
    if (!confirmAction) return;
    try {
      setSubmitting(true);
      if (confirmAction.action === "approve") {
        await api.post(`/api/expenses/${confirmAction.id}/approve`);
        toast.success("تم اعتماد المصروف");
      } else if (confirmAction.action === "reject") {
        await api.post(`/api/expenses/${confirmAction.id}/reject`);
        toast.success("تم رفض المصروف");
      } else if (confirmAction.action === "delete") {
        await api.delete(`/api/expenses/${confirmAction.id}`);
        toast.success("تم حذف/عكس المصروف");
      }
      setConfirmAction(null);
      fetchData();
    } catch (err) { toast.error(extractErrorMessage(err, "فشل في تنفيذ الإجراء")); } finally { setSubmitting(false); }
  };

  return (
    <div className="p-6 space-y-4">
      <SectionHeader title="المصروفات" action={
        <div className="flex items-center gap-2">
          <button onClick={() => setShowCreate(true)} style={btnPrimary}><Plus className="w-4 h-4" /> مصروف جديد</button>
          <button onClick={fetchData} className="w-8 h-8 rounded-md flex items-center justify-center" style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }} title="تحديث"><RefreshCw className="w-4 h-4" /></button>
        </div>
      } />

      {loading ? <LoadingSkeleton /> : data.length === 0 ? <EmptyState icon={TrendingDown} message="لا توجد مصروفات" /> : (
        <DataTable<ExpenseListItem>
          keyField="Id"
          data={data}
          columns={[
            { key: "Title", label: "العنوان" },
            { key: "Category", label: "الفئة", render: (r) => EXPENSE_CATEGORIES.find((c) => c.value === r.Category)?.label ?? r.Category },
            { key: "Amount", label: "المبلغ", render: (r) => formatYER(r.Amount) },
            { key: "PaymentMethod", label: "طريقة الدفع", render: (r) => PAYMENT_METHODS.find((m) => m.value === r.PaymentMethod)?.label ?? r.PaymentMethod },
            { key: "ExpenseDate", label: "التاريخ", render: (r) => new Date(r.ExpenseDate).toLocaleDateString("ar-SA") },
            { key: "Status", label: "الحالة", render: (r) => <StatusBadge status={r.Status} /> },
            { key: "actions", label: "إجراءات", render: (r) => (
              <div className="flex items-center gap-1">
                {r.Status === "Pending" && (
                  <>
                    <button onClick={(e) => { e.stopPropagation(); setConfirmAction({ id: r.Id, action: "approve" }); }} className="w-7 h-7 rounded-md flex items-center justify-center" style={{ color: tokens.successBorder }} title="اعتماد"><ThumbsUp className="w-3.5 h-3.5" /></button>
                    <button onClick={(e) => { e.stopPropagation(); setConfirmAction({ id: r.Id, action: "reject" }); }} className="w-7 h-7 rounded-md flex items-center justify-center" style={{ color: tokens.dangerBorder }} title="رفض"><ThumbsDown className="w-3.5 h-3.5" /></button>
                  </>
                )}
                {!r.IsReversal && <button onClick={(e) => { e.stopPropagation(); setConfirmAction({ id: r.Id, action: "delete" }); }} className="w-7 h-7 rounded-md flex items-center justify-center" style={{ color: tokens.dangerBorder }} title="حذف/عكس"><Trash2 className="w-3.5 h-3.5" /></button>}
              </div>
            )},
          ]}
        />
      )}

      {/* Create Expense Modal */}
      <Modal open={showCreate} onClose={() => setShowCreate(false)} title="مصروف جديد">
        <div className="space-y-4">
          <div><label style={labelStyle}>العنوان <span style={{ color: tokens.dangerBorder }}>*</span></label><input value={eTitle} onChange={(e) => setETitle(e.target.value)} placeholder="وصف المصروف" style={inputStyle} /></div>
          <div><label style={labelStyle}>الفئة</label><select value={eCategory} onChange={(e) => setECategory(e.target.value)} style={inputStyle}>{EXPENSE_CATEGORIES.map((c) => (<option key={c.value} value={c.value}>{c.label}</option>))}</select></div>
          <div><label style={labelStyle}>المبلغ <span style={{ color: tokens.dangerBorder }}>*</span></label><input type="number" min="0" step="0.01" value={eAmount} onChange={(e) => setEAmount(e.target.value)} dir="ltr" style={inputStyle} /></div>
          <div><label style={labelStyle}>طريقة الدفع</label><select value={eMethod} onChange={(e) => setEMethod(e.target.value)} style={inputStyle}>{PAYMENT_METHODS.map((m) => (<option key={m.value} value={m.value}>{m.label}</option>))}</select></div>
          <div><label style={labelStyle}>تاريخ المصروف</label><input type="date" value={eDate} onChange={(e) => setEDate(e.target.value)} style={inputStyle} /></div>
          <div className="flex gap-3 pt-2 border-t" style={{ borderColor: tokens.border }}>
            <button onClick={() => setShowCreate(false)} style={btnGhost}>إلغاء</button>
            <button onClick={handleCreate} disabled={submitting} style={{ ...btnPrimary, opacity: submitting ? 0.6 : 1 }}>{submitting ? "جارٍ الحفظ..." : "إنشاء"}</button>
          </div>
        </div>
      </Modal>

      <ConfirmDialog
        open={!!confirmAction}
        onClose={() => setConfirmAction(null)}
        onConfirm={handleAction}
        title={confirmAction?.action === "approve" ? "اعتماد المصروف" : confirmAction?.action === "reject" ? "رفض المصروف" : "حذف/عكس المصروف"}
        message={confirmAction?.action === "approve" ? "هل أنت متأكد من اعتماد هذا المصروف؟" : confirmAction?.action === "reject" ? "هل أنت متأكد من رفض هذا المصروف؟" : "هل أنت متأكد من حذف/عكس هذا المصروف؟ سيتم إنشاء قيد عكسي."}
        confirmLabel={confirmAction?.action === "approve" ? "اعتماد" : confirmAction?.action === "reject" ? "رفض" : "حذف"}
        danger={confirmAction?.action !== "approve"}
      />
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 9: Suppliers
   ═══════════════════════════════════════════════════════════════════════════════ */
function SuppliersTab() {
  const [suppliers, setSuppliers] = useState<SupplierListItem[]>([]);
  const [bills, setBills] = useState<SupplierBill[]>([]);
  const [loading, setLoading] = useState(true);
  const [showCreateBill, setShowCreateBill] = useState(false);
  const [showPayBill, setShowPayBill] = useState<SupplierBill | null>(null);
  const [submitting, setSubmitting] = useState(false);

  // Create bill form
  const [bSupplier, setBSupplier] = useState("");
  const [bDesc, setBDesc] = useState("");
  const [bAmount, setBAmount] = useState("");
  const [bDueDate, setBDueDate] = useState("");

  // Pay installment form
  const [payAmount, setPayAmount] = useState("");
  const [payMethod, setPayMethod] = useState("cash");

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      const [sRes, bRes] = await Promise.all([
        api.get<{ data: SupplierListItem[]; total: number }>("/api/suppliers"),
        api.get<{ data: SupplierBill[]; total: number }>("/api/supplier-bills"),
      ]);
      setSuppliers(sRes.data.data ?? (Array.isArray(sRes.data) ? sRes.data as unknown as SupplierListItem[] : []));
      setBills(bRes.data.data ?? (Array.isArray(bRes.data) ? bRes.data as unknown as SupplierBill[] : []));
    } catch { toast.error("فشل في تحميل بيانات الموردين"); } finally { setLoading(false); }
  }, []);

  useEffect(() => { fetchData(); }, [fetchData]);

  const handleCreateBill = async () => {
    if (!bSupplier || !bDesc.trim() || !bAmount || Number(bAmount) <= 0 || !bDueDate) {
      toast.error("يرجى ملء جميع الحقول المطلوبة");
      return;
    }
    try {
      setSubmitting(true);
      const payload: CreateSupplierBillRequest = {
        SupplierId: bSupplier,
        Description: bDesc,
        TotalAmount: Number(bAmount),
        DueDate: bDueDate,
      };
      await api.post("/api/supplier-bills", payload);
      toast.success("تم إنشاء فاتورة المورد بنجاح");
      setShowCreateBill(false);
      setBSupplier(""); setBDesc(""); setBAmount(""); setBDueDate("");
      fetchData();
    } catch (err) { toast.error(extractErrorMessage(err, "فشل في إنشاء الفاتورة")); } finally { setSubmitting(false); }
  };

  const handlePayBill = async () => {
    if (!showPayBill || !payAmount || Number(payAmount) <= 0) {
      toast.error("يرجى إدخال المبلغ");
      return;
    }
    if (Number(payAmount) > showPayBill.Balance) {
      toast.error(`المبلغ يتجاوز المستحق (${formatYER(showPayBill.Balance)})`);
      return;
    }
    try {
      setSubmitting(true);
      const payload: PaySupplierBillRequest = {
        Amount: Number(payAmount),
        PaymentMethod: payMethod,
      };
      await api.post(`/api/supplier-bills/${showPayBill.Id}/pay`, payload);
      toast.success("تم سداد القسط بنجاح");
      setShowPayBill(null);
      setPayAmount(""); setPayMethod("cash");
      fetchData();
    } catch (err) { toast.error(extractErrorMessage(err, "فشل في سداد القسط")); } finally { setSubmitting(false); }
  };

  return (
    <div className="p-6 space-y-6">
      {/* Suppliers */}
      <div>
        <SectionHeader title="الموردون" action={
          <button onClick={fetchData} className="w-8 h-8 rounded-md flex items-center justify-center" style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }} title="تحديث"><RefreshCw className="w-4 h-4" /></button>
        } />
        {loading ? <LoadingSkeleton /> : suppliers.length === 0 ? <EmptyState icon={Truck} message="لا يوجد موردون" /> : (
          <DataTable<SupplierListItem>
            keyField="Id"
            data={suppliers}
            columns={[
              { key: "Name", label: "الاسم" },
              { key: "ContactPerson", label: "جهة الاتصال", render: (r) => r.ContactPerson ?? "—" },
              { key: "Phone", label: "الهاتف", render: (r) => r.Phone ?? "—" },
              { key: "TotalBilled", label: "إجمالي الفواتير", render: (r) => formatYER(r.TotalBilled) },
              { key: "TotalPaid", label: "المدفوع", render: (r) => formatYER(r.TotalPaid) },
              { key: "Balance", label: "الرصيد", render: (r) => <span style={{ color: r.Balance > 0 ? tokens.dangerBorder : tokens.successBorder, fontWeight: 700 }}>{formatYER(r.Balance)}</span> },
            ]}
          />
        )}
      </div>

      {/* Supplier Bills */}
      <div>
        <SectionHeader title="فواتير الموردين" action={
          <button onClick={() => setShowCreateBill(true)} style={btnPrimary}><Plus className="w-4 h-4" /> فاتورة جديدة</button>
        } />
        {loading ? <LoadingSkeleton /> : bills.length === 0 ? <EmptyState icon={FileText} message="لا توجد فواتير موردين" /> : (
          <DataTable<SupplierBill>
            keyField="Id"
            data={bills}
            columns={[
              { key: "SupplierName", label: "المورد" },
              { key: "Description", label: "الوصف" },
              { key: "TotalAmount", label: "الإجمالي", render: (r) => formatYER(r.TotalAmount) },
              { key: "PaidAmount", label: "المدفوع", render: (r) => formatYER(r.PaidAmount) },
              { key: "Balance", label: "المتبقي", render: (r) => <span style={{ color: r.Balance > 0 ? tokens.dangerBorder : tokens.successBorder, fontWeight: 700 }}>{formatYER(r.Balance)}</span> },
              { key: "DueDate", label: "تاريخ الاستحقاق", render: (r) => new Date(r.DueDate).toLocaleDateString("ar-SA") },
              { key: "Status", label: "الحالة", render: (r) => <StatusBadge status={r.Status} /> },
              { key: "actions", label: "إجراءات", render: (r) => r.Balance > 0 ? (
                <button onClick={(e) => { e.stopPropagation(); setShowPayBill(r); setPayAmount(""); }} className="w-7 h-7 rounded-md flex items-center justify-center" style={{ color: tokens.brand }} title="سداد قسط"><DollarSign className="w-3.5 h-3.5" /></button>
              ) : null },
            ]}
          />
        )}
      </div>

      {/* Create Supplier Bill Modal */}
      <Modal open={showCreateBill} onClose={() => setShowCreateBill(false)} title="فاتورة مورد جديدة">
        <div className="space-y-4">
          <div><label style={labelStyle}>المورد <span style={{ color: tokens.dangerBorder }}>*</span></label><select value={bSupplier} onChange={(e) => setBSupplier(e.target.value)} style={inputStyle}><option value="">— اختر —</option>{suppliers.map((s) => (<option key={s.Id} value={s.Id}>{s.Name}</option>))}</select></div>
          <div><label style={labelStyle}>الوصف <span style={{ color: tokens.dangerBorder }}>*</span></label><input value={bDesc} onChange={(e) => setBDesc(e.target.value)} placeholder="وصف الفاتورة" style={inputStyle} /></div>
          <div><label style={labelStyle}>المبلغ <span style={{ color: tokens.dangerBorder }}>*</span></label><input type="number" min="0" step="0.01" value={bAmount} onChange={(e) => setBAmount(e.target.value)} dir="ltr" style={inputStyle} /></div>
          <div><label style={labelStyle}>تاريخ الاستحقاق <span style={{ color: tokens.dangerBorder }}>*</span></label><input type="date" value={bDueDate} onChange={(e) => setBDueDate(e.target.value)} style={inputStyle} /></div>
          <div className="flex gap-3 pt-2 border-t" style={{ borderColor: tokens.border }}>
            <button onClick={() => setShowCreateBill(false)} style={btnGhost}>إلغاء</button>
            <button onClick={handleCreateBill} disabled={submitting} style={{ ...btnPrimary, opacity: submitting ? 0.6 : 1 }}>{submitting ? "جارٍ الحفظ..." : "إنشاء"}</button>
          </div>
        </div>
      </Modal>

      {/* Pay Installment Modal */}
      <Modal open={!!showPayBill} onClose={() => setShowPayBill(null)} title={`سداد قسط — ${showPayBill?.Description ?? ""}`}>
        {showPayBill && (
          <div className="space-y-4">
            <div className="rounded-md p-3" style={{ backgroundColor: tokens.infoBg, border: `1px solid ${tokens.infoBorder}` }}>
              <p className="text-xs" style={{ color: tokens.infoText }}>المتبقي: <strong>{formatYER(showPayBill.Balance)}</strong></p>
            </div>
            <div><label style={labelStyle}>المبلغ <span style={{ color: tokens.dangerBorder }}>*</span></label><input type="number" min="0" max={showPayBill.Balance} step="0.01" value={payAmount} onChange={(e) => setPayAmount(e.target.value)} dir="ltr" style={inputStyle} /></div>
            <div><label style={labelStyle}>طريقة الدفع</label><select value={payMethod} onChange={(e) => setPayMethod(e.target.value)} style={inputStyle}>{PAYMENT_METHODS.map((m) => (<option key={m.value} value={m.value}>{m.label}</option>))}</select></div>
            <div className="flex gap-3 pt-2 border-t" style={{ borderColor: tokens.border }}>
              <button onClick={() => setShowPayBill(null)} style={btnGhost}>إلغاء</button>
              <button onClick={handlePayBill} disabled={submitting} style={{ ...btnPrimary, opacity: submitting ? 0.6 : 1 }}>{submitting ? "جارٍ السداد..." : "سداد"}</button>
            </div>
          </div>
        )}
      </Modal>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 10: Audit (with sub-tabs)
   ═══════════════════════════════════════════════════════════════════════════════ */
function AuditTab() {
  const [subTab, setSubTab] = useState<"pl" | "daily" | "balances">("pl");

  const subTabs = [
    { key: "pl" as const, label: "الأرباح والخسائر", icon: BarChart3 },
    { key: "daily" as const, label: "ملخص الكاش اليومي", icon: Receipt },
    { key: "balances" as const, label: "أرصدة الحسابات", icon: Landmark },
  ];

  return (
    <div className="p-6 space-y-4">
      {/* Sub-tab bar */}
      <div className="flex items-center gap-1 border-b" style={{ borderColor: tokens.border }}>
        {subTabs.map((st) => {
          const Icon = st.icon;
          const isActive = subTab === st.key;
          return (
            <button key={st.key} onClick={() => setSubTab(st.key)} className="flex items-center gap-1.5 px-3 py-2 text-xs font-medium transition-colors relative" style={{ color: isActive ? tokens.brand : tokens.textSecondary, borderBottom: isActive ? `2px solid ${tokens.brand}` : "2px solid transparent" }}>
              <Icon className="w-3.5 h-3.5" /><span>{st.label}</span>
            </button>
          );
        })}
      </div>

      {subTab === "pl" && <PLSubTab />}
      {subTab === "daily" && <DailyCashSubTab />}
      {subTab === "balances" && <AccountBalancesSubTab />}
    </div>
  );
}

/* ── P&L Sub-tab ── */
function PLSubTab() {
  const [data, setData] = useState<ProfitLossData | null>(null);
  const [loading, setLoading] = useState(false);
  const [from, setFrom] = useState(() => { const d = new Date(); return new Date(d.getFullYear(), d.getMonth(), 1).toISOString().slice(0, 10); });
  const [to, setTo] = useState(() => new Date().toISOString().slice(0, 10));

  const fetchPL = useCallback(async () => {
    try {
      setLoading(true);
      const { data } = await api.get<ProfitLossData>("/api/finance-v3/profit-loss", { params: { from, to } });
      setData(data);
    } catch { toast.error("فشل في تحميل تقرير الأرباح والخسائر"); } finally { setLoading(false); }
  }, [from, to]);

  useEffect(() => { fetchPL(); }, [fetchPL]);

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-3">
        <div><label style={labelStyle}>من</label><input type="date" value={from} onChange={(e) => setFrom(e.target.value)} style={{ ...inputStyle, width: 160 }} /></div>
        <div><label style={labelStyle}>إلى</label><input type="date" value={to} onChange={(e) => setTo(e.target.value)} style={{ ...inputStyle, width: 160 }} /></div>
        <button onClick={fetchPL} style={{ ...btnPrimary, marginTop: 18 }}><Eye className="w-4 h-4" /> عرض</button>
      </div>

      {loading ? <LoadingSkeleton rows={4} /> : data ? (
        <div className="space-y-4">
          {/* Accrued section */}
          <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
            <h4 className="text-sm font-semibold mb-3" style={{ color: tokens.textPrimary }}>الأساس المستحق</h4>
            <div className="grid grid-cols-3 gap-4">
              <div className="text-center"><p className="text-[11px]" style={{ color: tokens.textTertiary }}>الإيرادات المستحقة</p><p className="text-lg font-bold" style={{ color: tokens.successBorder }}>{formatYER(data.AccruedRevenue)}</p></div>
              <div className="text-center"><p className="text-[11px]" style={{ color: tokens.textTertiary }}>المصروفات المستحقة</p><p className="text-lg font-bold" style={{ color: tokens.dangerBorder }}>{formatYER(data.AccruedExpenses)}</p></div>
              <div className="text-center"><p className="text-[11px]" style={{ color: tokens.textTertiary }}>صافي الربح المستحق</p><p className="text-lg font-bold" style={{ color: data.AccruedNetProfit >= 0 ? tokens.successBorder : tokens.dangerBorder }}>{formatYER(data.AccruedNetProfit)}</p></div>
            </div>
          </div>

          {/* Cash section */}
          <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
            <h4 className="text-sm font-semibold mb-3" style={{ color: tokens.textPrimary }}>الأساس النقدي</h4>
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              <div className="text-center"><p className="text-[11px]" style={{ color: tokens.textTertiary }}>التحصيلات</p><p className="text-sm font-bold" style={{ color: tokens.successBorder }}>{formatYER(data.CashCollections)}</p></div>
              <div className="text-center"><p className="text-[11px]" style={{ color: tokens.textTertiary }}>المرتجعات</p><p className="text-sm font-bold" style={{ color: tokens.warningText }}>{formatYER(data.CashRefunds)}</p></div>
              <div className="text-center"><p className="text-[11px]" style={{ color: tokens.textTertiary }}>صافي التحصيل</p><p className="text-sm font-bold" style={{ color: tokens.brand }}>{formatYER(data.NetCashCollections)}</p></div>
              <div className="text-center"><p className="text-[11px]" style={{ color: tokens.textTertiary }}>صافي الربح النقدي</p><p className="text-sm font-bold" style={{ color: data.CashNetProfit >= 0 ? tokens.successBorder : tokens.dangerBorder }}>{formatYER(data.CashNetProfit)}</p></div>
            </div>
          </div>

          {/* Cost breakdown */}
          <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
            <h4 className="text-sm font-semibold mb-3" style={{ color: tokens.textPrimary }}>تفصيل التكاليف</h4>
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              <div className="text-center"><p className="text-[11px]" style={{ color: tokens.textTertiary }}>مصروفات تشغيلية</p><p className="text-sm font-bold" style={{ color: tokens.dangerBorder }}>{formatYER(data.OperatingExpenses)}</p></div>
              <div className="text-center"><p className="text-[11px]" style={{ color: tokens.textTertiary }}>رواتب</p><p className="text-sm font-bold" style={{ color: tokens.dangerBorder }}>{formatYER(data.SalaryPayments)}</p></div>
              <div className="text-center"><p className="text-[11px]" style={{ color: tokens.textTertiary }}>عمولات أطباء</p><p className="text-sm font-bold" style={{ color: tokens.dangerBorder }}>{formatYER(data.DoctorCommissions)}</p></div>
              <div className="text-center"><p className="text-[11px]" style={{ color: tokens.textTertiary }}>مدفوعات موردين</p><p className="text-sm font-bold" style={{ color: tokens.dangerBorder }}>{formatYER(data.SupplierPayments)}</p></div>
            </div>
            <div className="mt-3 pt-3 border-t text-center" style={{ borderColor: tokens.border }}>
              <p className="text-[11px]" style={{ color: tokens.textTertiary }}>إجمالي التكاليف</p>
              <p className="text-base font-bold" style={{ color: tokens.dangerBorder }}>{formatYER(data.TotalCosts)}</p>
            </div>
          </div>

          {/* Margin */}
          <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
            <div className="flex items-center justify-between">
              <span className="text-sm font-semibold" style={{ color: tokens.textPrimary }}>هامش الربح</span>
              <span className="text-xl font-bold" style={{ color: data.ProfitMargin >= 0 ? tokens.successBorder : tokens.dangerBorder }}>{data.ProfitMargin.toFixed(1)}%</span>
            </div>
          </div>
        </div>
      ) : (
        <EmptyState icon={BarChart3} message="اختر فترة واضغط عرض لتحميل التقرير" />
      )}
    </div>
  );
}

/* ── Daily Cash Sub-tab ── */
function DailyCashSubTab() {
  const [data, setData] = useState<DailyCashSummary | null>(null);
  const [loading, setLoading] = useState(false);
  const [date, setDate] = useState(() => new Date().toISOString().slice(0, 10));

  const fetchDaily = useCallback(async () => {
    try {
      setLoading(true);
      const { data } = await api.get<DailyCashSummary>("/api/finance-v3/daily-cash-summary", { params: { date } });
      setData(data);
    } catch { toast.error("فشل في تحميل ملخص الكاش اليومي"); } finally { setLoading(false); }
  }, [date]);

  useEffect(() => { fetchDaily(); }, [fetchDaily]);

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-3">
        <div><label style={labelStyle}>التاريخ</label><input type="date" value={date} onChange={(e) => setDate(e.target.value)} style={{ ...inputStyle, width: 180 }} /></div>
        <button onClick={fetchDaily} style={{ ...btnPrimary, marginTop: 18 }}><Eye className="w-4 h-4" /> عرض</button>
      </div>

      {loading ? <LoadingSkeleton rows={4} /> : data ? (
        <div className="space-y-4">
          {/* Summary cards */}
          <div className="grid grid-cols-3 gap-4">
            <KpiCard label="التدفقات الداخلة" value={formatYER(data.TotalInflow)} color={tokens.successBorder} icon={<Receipt className="w-4 h-4" />} />
            <KpiCard label="التدفقات الخارجة" value={formatYER(data.TotalOutflow)} color={tokens.dangerBorder} icon={<TrendingDown className="w-4 h-4" />} />
            <KpiCard label="صافي الكاش" value={formatYER(data.NetCash)} color={data.NetCash >= 0 ? tokens.successBorder : tokens.dangerBorder} icon={<Vault className="w-4 h-4" />} />
          </div>

          <div className="grid grid-cols-3 gap-4">
            <KpiCard label="عدد المعاملات" value={formatNumber(data.TransactionCount)} color={tokens.brand} icon={<ClipboardCheck className="w-4 h-4" />} />
            <KpiCard label="معاملات عكسية" value={formatNumber(data.ReversalCount)} color={tokens.warningBorder} icon={<AlertTriangle className="w-4 h-4" />} />
            <KpiCard label="قيود محاسبية" value={formatNumber(data.JournalEntryCount)} color={tokens.brand} icon={<FileText className="w-4 h-4" />} />
          </div>

          {/* By category */}
          {data.ByCategory && data.ByCategory.length > 0 && (
            <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
              <h4 className="text-sm font-semibold mb-3" style={{ color: tokens.textPrimary }}>حسب الفئة</h4>
              <div className="overflow-x-auto">
                <table className="w-full text-xs">
                  <thead><tr style={{ backgroundColor: tokens.cardHover }}>
                    <th className="text-right px-3 py-2">النوع</th>
                    <th className="text-right px-3 py-2">الفئة</th>
                    <th className="text-right px-3 py-2">عكسي</th>
                    <th className="text-right px-3 py-2">العدد</th>
                    <th className="text-right px-3 py-2">الإجمالي</th>
                  </tr></thead>
                  <tbody>
                    {data.ByCategory.map((cat, idx) => (
                      <tr key={idx} style={{ borderBottom: `1px solid ${tokens.border}` }}>
                        <td className="px-3 py-2">{cat.Type}</td>
                        <td className="px-3 py-2">{cat.Category}</td>
                        <td className="px-3 py-2">{cat.IsReversal ? "نعم" : "—"}</td>
                        <td className="px-3 py-2">{cat.Count}</td>
                        <td className="px-3 py-2 font-bold" style={{ color: cat.IsReversal ? tokens.dangerBorder : tokens.successBorder }}>{formatYER(cat.Total)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* By payment method */}
          {data.ByPaymentMethod && data.ByPaymentMethod.length > 0 && (
            <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
              <h4 className="text-sm font-semibold mb-3" style={{ color: tokens.textPrimary }}>حسب طريقة الدفع</h4>
              <div className="overflow-x-auto">
                <table className="w-full text-xs">
                  <thead><tr style={{ backgroundColor: tokens.cardHover }}>
                    <th className="text-right px-3 py-2">طريقة الدفع</th>
                    <th className="text-right px-3 py-2">العدد</th>
                    <th className="text-right px-3 py-2">الإجمالي</th>
                  </tr></thead>
                  <tbody>
                    {data.ByPaymentMethod.map((pm, idx) => (
                      <tr key={idx} style={{ borderBottom: `1px solid ${tokens.border}` }}>
                        <td className="px-3 py-2">{PAYMENT_METHODS.find((m) => m.value === pm.PaymentMethod)?.label ?? pm.PaymentMethod}</td>
                        <td className="px-3 py-2">{pm.Count}</td>
                        <td className="px-3 py-2 font-bold">{formatYER(pm.Total)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </div>
      ) : (
        <EmptyState icon={Receipt} message="اختر تاريخاً واضغط عرض" />
      )}
    </div>
  );
}

/* ── Account Balances Sub-tab ── */
function AccountBalancesSubTab() {
  const [data, setData] = useState<AccountBalancesData | null>(null);
  const [loading, setLoading] = useState(false);

  const fetchBalances = useCallback(async () => {
    try { setLoading(true); const { data } = await api.get<AccountBalancesData>("/api/finance-v3/account-balances"); setData(data); } catch { toast.error("فشل في تحميل أرصدة الحسابات"); } finally { setLoading(false); }
  }, []);

  useEffect(() => { fetchBalances(); }, [fetchBalances]);

  return (
    <div className="space-y-4">
      <div className="flex justify-end">
        <button onClick={fetchBalances} style={btnPrimary}><RefreshCw className="w-4 h-4" /> تحديث</button>
      </div>

      {loading ? <LoadingSkeleton rows={4} /> : data ? (
        <div className="space-y-4">
          {/* Summary cards */}
          <div className="grid grid-cols-2 md:grid-cols-5 gap-4">
            <KpiCard label="إجمالي الأصول" value={formatYER(data.TotalAssets)} color={tokens.brand} icon={<Vault className="w-4 h-4" />} />
            <KpiCard label="إجمالي الإيرادات" value={formatYER(data.TotalRevenue)} color={tokens.successBorder} icon={<Receipt className="w-4 h-4" />} />
            <KpiCard label="إجمالي المصروفات" value={formatYER(data.TotalExpenses)} color={tokens.dangerBorder} icon={<TrendingDown className="w-4 h-4" />} />
            <KpiCard label="إجمالي المستحقات" value={formatYER(data.TotalReceivables)} color={tokens.warningBorder} icon={<HandCoins className="w-4 h-4" />} />
            <KpiCard label="إجمالي الالتزامات" value={formatYER(data.TotalPayables)} color={tokens.dangerBorder} icon={<Truck className="w-4 h-4" />} />
          </div>

          {/* Account balances table */}
          {data.AccountBalances && data.AccountBalances.length > 0 && (
            <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
              <h4 className="text-sm font-semibold mb-3" style={{ color: tokens.textPrimary }}>أرصدة الحسابات</h4>
              <div className="overflow-x-auto">
                <table className="w-full text-xs">
                  <thead><tr style={{ backgroundColor: tokens.cardHover }}>
                    <th className="text-right px-3 py-2">نوع الحساب</th>
                    <th className="text-right px-3 py-2">مدين</th>
                    <th className="text-right px-3 py-2">دائن</th>
                    <th className="text-right px-3 py-2">صافي الرصيد</th>
                    <th className="text-right px-3 py-2">عدد القيود</th>
                  </tr></thead>
                  <tbody>
                    {data.AccountBalances.map((ab, idx) => (
                      <tr key={idx} style={{ borderBottom: `1px solid ${tokens.border}` }}>
                        <td className="px-3 py-2 font-medium">{ab.AccountType}</td>
                        <td className="px-3 py-2">{formatYER(ab.TotalDebit)}</td>
                        <td className="px-3 py-2">{formatYER(ab.TotalCredit)}</td>
                        <td className="px-3 py-2 font-bold" style={{ color: ab.NetBalance >= 0 ? tokens.successBorder : tokens.dangerBorder }}>{formatYER(ab.NetBalance)}</td>
                        <td className="px-3 py-2">{ab.EntryCount}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* Treasuries */}
          {data.Treasuries && data.Treasuries.length > 0 && (
            <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
              <h4 className="text-sm font-semibold mb-3" style={{ color: tokens.textPrimary }}>الخزائن</h4>
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
                {data.Treasuries.map((t) => (
                  <div key={t.Id} className="rounded-md border p-3" style={{ borderColor: tokens.border }}>
                    <div className="flex items-center justify-between mb-1">
                      <span className="text-sm font-medium" style={{ color: tokens.textPrimary }}>{t.Name}</span>
                      <span className="text-[11px] px-2 py-0.5 rounded-full" style={{ backgroundColor: tokens.brandLight, color: tokens.brand }}>{t.Type}</span>
                    </div>
                    <p className="text-base font-bold" style={{ color: tokens.successBorder }}>{formatYER(t.Balance)}</p>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      ) : (
        <EmptyState icon={Landmark} message="اضغط تحديث لعرض أرصدة الحسابات" />
      )}
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════════
   Finance V3 Financial Center — Main Page
   ═══════════════════════════════════════════════════════════════════════════════ */
export default function FinanceV3Page() {
  const { user } = useAuthStore();
  const [activeTab, setActiveTab] = useState("overview");

  /* ── Access gate: Admin / Accountant only ── */
  const isAuthorized = user?.role === "Admin" || user?.role === "Accountant";
  const isAdmin = user?.role === "Admin";

  if (!isAuthorized) {
    return <AccessDenied />;
  }

  return (
    <div className="min-h-screen flex flex-col" style={{ backgroundColor: tokens.bg, direction: "rtl" }}>
      {/* ═══ Top Header ═══ */}
      <header className="flex items-center gap-4 px-5 py-2.5 border-b" style={{ backgroundColor: tokens.card, borderColor: tokens.border, boxShadow: tokens.shadow2 }}>
        <div className="flex items-center gap-2.5">
          <div className="w-8 h-8 rounded-md flex items-center justify-center" style={{ backgroundColor: tokens.brand }}>
            <Wallet className="w-4 h-4" style={{ color: tokens.textOnBrand }} />
          </div>
          <div>
            <h1 className="text-sm font-bold leading-tight" style={{ color: tokens.textPrimary }}>المركز المالي</h1>
            <p className="text-[11px]" style={{ color: tokens.textTertiary }}>Finance V3</p>
          </div>
        </div>
        <div className="flex-1" />
        <span className="text-xs" style={{ color: tokens.textSecondary }}>{todayArabic()}</span>
        <div className="w-px h-5" style={{ backgroundColor: tokens.border }} />
        <span className="text-xs font-medium" style={{ color: tokens.textSecondary }}>الفرع الرئيسي</span>
        <div className="w-px h-5" style={{ backgroundColor: tokens.border }} />
        <div className="flex items-center gap-1.5">
          <CircleDot className="w-3 h-3" style={{ color: tokens.textTertiary }} />
          <span className="text-xs" style={{ color: tokens.textTertiary }}>لا وردية مفتوحة</span>
        </div>
        <div className="w-px h-5" style={{ backgroundColor: tokens.border }} />
        <button className="w-7 h-7 rounded-md flex items-center justify-center transition-colors" style={{ color: tokens.textSecondary }} onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = tokens.cardHover; }} onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }} title="الإشعارات"><Bell className="w-4 h-4" /></button>
      </header>

      {/* ═══ Tabs ═══ */}
      <div className="flex items-center border-b px-5 overflow-x-auto" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
        {TABS.map((tab) => {
          const Icon = tab.icon;
          const isActive = activeTab === tab.key;
          return (
            <button key={tab.key} onClick={() => setActiveTab(tab.key)} className="flex items-center gap-1.5 px-3 py-2.5 text-xs font-medium whitespace-nowrap transition-colors relative" style={{ color: isActive ? tokens.brand : tokens.textSecondary, borderBottom: isActive ? `2px solid ${tokens.brand}` : "2px solid transparent" }} onMouseEnter={(e) => { if (!isActive) e.currentTarget.style.color = tokens.textPrimary; }} onMouseLeave={(e) => { if (!isActive) e.currentTarget.style.color = tokens.textSecondary; }}>
              <Icon className="w-3.5 h-3.5" /><span>{tab.label}</span>
            </button>
          );
        })}
      </div>

      {/* ═══ Main Content ═══ */}
      <div className="flex-1 overflow-y-auto">
        {activeTab === "overview" && <OverviewTab />}
        {activeTab === "patient-acct" && <PatientAccountsTab />}
        {activeTab === "invoices" && <InvoicesTab />}
        {activeTab === "collections" && <CollectionsTab />}
        {activeTab === "contracts" && <ContractsTab />}
        {activeTab === "cashier" && <CashierTab isAdmin={isAdmin} />}
        {activeTab === "treasuries" && <TreasuriesTab />}
        {activeTab === "expenses" && <ExpensesTab />}
        {activeTab === "suppliers" && <SuppliersTab />}
        {activeTab === "audit" && <AuditTab />}
      </div>
    </div>
  );
}
