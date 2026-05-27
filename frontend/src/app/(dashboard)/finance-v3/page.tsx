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
  FileMinus,
  Lock,
  Download,
  Search,
  Bell,
  CircleDot,
  CheckCircle2,
  Clock,
  XCircle,
  ChevronLeft,
  Info,
} from "lucide-react";
import { useAuthStore } from "@/stores/authStore";
import { api } from "@/lib/api";

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
   Command Bar button definition
   ═══════════════════════════════════════════════════════════════════════════════ */
interface CommandBtn {
  key: string;
  label: string;
  icon: React.ElementType;
  disabled: boolean;
}

const COMMAND_BUTTONS: CommandBtn[] = [
  { key: "new-receipt",  label: "إيصال جديد",    icon: Plus,      disabled: true },
  { key: "new-expense",  label: "مصروف جديد",    icon: FileMinus, disabled: true },
  { key: "close-shift",  label: "إقفال الوردية",  icon: Lock,      disabled: true },
  { key: "export",       label: "تصدير",          icon: Download,  disabled: true },
  { key: "search",       label: "بحث",            icon: Search,    disabled: true },
];

/* ═══════════════════════════════════════════════════════════════════════════════
   KPI definition
   ═══════════════════════════════════════════════════════════════════════════════ */
interface KpiDef {
  key: string;
  label: string;
  icon: React.ElementType;
}

const KPI_ITEMS: KpiDef[] = [
  { key: "today-revenue",   label: "إيراد اليوم (مستحق)",  icon: Receipt },
  { key: "today-expenses",  label: "التدفقات الخارجة اليوم",      icon: TrendingDown },
  { key: "cash-balance",    label: "رصيد الصندوق",       icon: Vault },
  { key: "outstanding",     label: "المستحقات المعلقة",  icon: HandCoins },
  { key: "open-invoices",   label: "فواتير مفتوحة",      icon: FileText },
  { key: "overdue-contracts", label: "عقود متأخرة",      icon: AlertTriangle },
];

/* ═══════════════════════════════════════════════════════════════════════════════
   Phase status items for review panel
   ═══════════════════════════════════════════════════════════════════════════════ */
interface PhaseStatus {
  key: string;
  label: string;
  status: "completed" | "in-progress" | "pending";
}

const PHASE_STATUS: PhaseStatus[] = [
  { key: "phase1", label: "التدقيق والتأسيس",      status: "completed" },
  { key: "phase2", label: "دفتر الأستاذ المزدوج",  status: "completed" },
  { key: "phase3", label: "الفواتير والتحصيل",      status: "in-progress" },
  { key: "phase4", label: "الصندوق والخزائن",       status: "pending" },
  { key: "phase5", label: "التقارير والاعتماد",      status: "pending" },
  { key: "phase6", label: "ترحيل البيانات",         status: "pending" },
  { key: "phase7", label: "إيقاف النظام القديم",    status: "pending" },
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

function PhaseIcon({ status }: { status: PhaseStatus["status"] }) {
  switch (status) {
    case "completed":
      return <CheckCircle2 className="w-4 h-4" style={{ color: tokens.successBorder }} />;
    case "in-progress":
      return <Clock className="w-4 h-4" style={{ color: tokens.warningBorder }} />;
    case "pending":
      return <XCircle className="w-4 h-4" style={{ color: tokens.textTertiary }} />;
  }
}

/* ═══════════════════════════════════════════════════════════════════════════════
   Access Denied
   ═══════════════════════════════════════════════════════════════════════════════ */
function AccessDenied() {
  return (
    <div
      className="min-h-screen flex items-center justify-center"
      style={{ backgroundColor: tokens.bg, direction: "rtl" }}
    >
      <div
        className="rounded-lg border p-8 max-w-md text-center"
        style={{
          backgroundColor: tokens.card,
          borderColor: tokens.dangerBorder,
          boxShadow: tokens.shadow4,
        }}
      >
        <div
          className="w-14 h-14 rounded-full flex items-center justify-center mx-auto mb-4"
          style={{ backgroundColor: tokens.dangerBg }}
        >
          <ShieldX className="w-7 h-7" style={{ color: tokens.dangerBorder }} />
        </div>
        <h2
          className="text-lg font-bold mb-2"
          style={{ color: tokens.textPrimary }}
        >
          غير مصرح بالوصول
        </h2>
        <p className="text-sm leading-relaxed" style={{ color: tokens.textSecondary }}>
          هذه الشاشة متاحة فقط للمسؤول والمحاسب. إذا كنت تحتاج الوصول إلى
          تسجيل التحصيل، يرجى استخدام شاشة التشغيل اليومي.
        </p>
      </div>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════════
   Empty tab state
   ═══════════════════════════════════════════════════════════════════════════════ */
function EmptyTabState({ tab }: { tab: TabDef }) {
  const Icon = tab.icon;
  return (
    <div className="flex flex-col items-center justify-center py-20 px-6">
      <div
        className="w-16 h-16 rounded-full flex items-center justify-center mb-4"
        style={{ backgroundColor: tokens.brandLight }}
      >
        <Icon className="w-8 h-8" style={{ color: tokens.brand }} />
      </div>
      <h3 className="text-base font-semibold mb-2" style={{ color: tokens.textPrimary }}>
        {tab.label}
      </h3>
      <p className="text-sm text-center max-w-sm leading-relaxed" style={{ color: tokens.textSecondary }}>
        هذا القسم قيد التطوير وسيكون متاحاً في المراحل القادمة من إعادة بناء
        الوحدة المالية. يرجى العودة لاحقاً.
      </p>
      <span
        className="mt-3 text-xs font-semibold px-3 py-1 rounded-full"
        style={{
          backgroundColor: tokens.warningBg,
          color: tokens.warningText,
          border: `1px solid ${tokens.warningBorder}`,
        }}
      >
        قريباً
      </span>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════════
   Dashboard data types
   ═══════════════════════════════════════════════════════════════════════════════ */
interface DashboardData {
  TodayInflow: number;
  TodayOutflow: number;
  TodayNet: number;
  MonthInflow: number;
  MonthOutflow: number;
  MonthNet: number;
  TotalOutstanding: number;
  ContractOutstanding: number;
  InvoiceOutstanding: number;
  TotalTreasuryBalance: number;
  TodayAccruedRevenue: number;
  MonthAccruedRevenue: number;
  JournalEntryCount: number;
  PostedEntryCount: number;
  ReversalEntryCount: number;
  DualWriteCoverage: string;
  PendingExpenses: number;
  PendingTransfers: number;
  Date: string;
}

function formatYER(amount: number): string {
  return amount.toLocaleString("ar-YE", { minimumFractionDigits: 0, maximumFractionDigits: 0 }) + " ر.ي";
}

/* ═══════════════════════════════════════════════════════════════════════════════
   Overview Tab Content — Now fetches live data from FinanceV3 API
   ═══════════════════════════════════════════════════════════════════════════════ */
function OverviewTab() {
  const [data, setData] = useState<DashboardData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchDashboard = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const { data: responseData } = await api.get<DashboardData>("/api/finance-v3/dashboard");
      setData(responseData);
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

  useEffect(() => { fetchDashboard(); }, [fetchDashboard]);

  return (
    <div className="p-6 space-y-6">
      {/* Welcome message */}
      <div>
        <h2 className="text-base font-semibold mb-1" style={{ color: tokens.textPrimary }}>
          المركز المالي
        </h2>
        <p className="text-sm leading-relaxed" style={{ color: tokens.textSecondary }}>
          مرحباً بك في المركز المالي الجديد. هذه الشاشة هي واجهة المحاسبة والمراجعة
          المالية المخصصة للمسؤول والمحاسب. يتم تسجيل تحصيل المرضى من شاشة التشغيل
          اليومي، بينما هذه الشاشة مخصصة للمراجعة والتسوية والتقارير.
        </p>
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
            <button onClick={fetchDashboard} className="text-xs font-medium mt-2 underline" style={{ color: tokens.brand }}>
              إعادة المحاولة
            </button>
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
              <div className="flex justify-between text-xs">
                <span style={{ color: tokens.textSecondary }}>إجمالي القيود</span>
                <span className="font-bold" style={{ color: tokens.textPrimary }}>{data.JournalEntryCount}</span>
              </div>
              <div className="flex justify-between text-xs">
                <span style={{ color: tokens.textSecondary }}>قيود مرحّلة</span>
                <span className="font-bold" style={{ color: tokens.successBorder }}>{data.PostedEntryCount}</span>
              </div>
              <div className="flex justify-between text-xs">
                <span style={{ color: tokens.textSecondary }}>قيود عكسية</span>
                <span className="font-bold" style={{ color: tokens.warningText }}>{data.ReversalEntryCount}</span>
              </div>
              <div className="flex justify-between text-xs">
                <span style={{ color: tokens.textSecondary }}>نسبة التغطية</span>
                <span className="font-bold" style={{ color: tokens.brand }}>{data.DualWriteCoverage}</span>
              </div>
            </div>
          </div>
          <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
            <div className="flex items-center gap-2 mb-3">
              <Clock className="w-4 h-4" style={{ color: tokens.warningBorder }} />
              <h4 className="text-sm font-semibold" style={{ color: tokens.textPrimary }}>إجراءات معلقة</h4>
            </div>
            <div className="space-y-2">
              <div className="flex justify-between text-xs">
                <span style={{ color: tokens.textSecondary }}>مصروفات بانتظار الاعتماد</span>
                <span className="font-bold" style={{ color: data.PendingExpenses > 0 ? tokens.warningText : tokens.textPrimary }}>{data.PendingExpenses}</span>
              </div>
              <div className="flex justify-between text-xs">
                <span style={{ color: tokens.textSecondary }}>تحويلات معلقة</span>
                <span className="font-bold" style={{ color: data.PendingTransfers > 0 ? tokens.warningText : tokens.textPrimary }}>{data.PendingTransfers}</span>
              </div>
              <div className="flex justify-between text-xs">
                <span style={{ color: tokens.textSecondary }}>فواتير غير مدفوعة</span>
                <span className="font-bold" style={{ color: tokens.textPrimary }}>{formatYER(data.InvoiceOutstanding)}</span>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Monthly summary */}
      {data && (
        <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
          <h4 className="text-sm font-semibold mb-3" style={{ color: tokens.textPrimary }}>ملخص الشهر</h4>
          <div className="grid grid-cols-3 gap-4 text-center">
            <div>
              <p className="text-xs" style={{ color: tokens.textTertiary }}>الإيرادات المستحقة</p>
              <p className="text-lg font-bold" style={{ color: tokens.successBorder }}>{formatYER(data.MonthAccruedRevenue)}</p>
            </div>
            <div>
              <p className="text-xs" style={{ color: tokens.textTertiary }}>التدفقات الخارجة</p>
              <p className="text-lg font-bold" style={{ color: tokens.dangerBorder }}>{formatYER(data.MonthOutflow)}</p>
            </div>
            <div>
              <p className="text-xs" style={{ color: tokens.textTertiary }}>صافي التدفق</p>
              <p className="text-lg font-bold" style={{ color: data.MonthNet >= 0 ? tokens.successBorder : tokens.dangerBorder }}>{formatYER(data.MonthNet)}</p>
            </div>
          </div>
        </div>
      )}

      {/* Current status */}
      <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
        <h3 className="text-sm font-semibold mb-3" style={{ color: tokens.textPrimary }}>حالة إعادة البناء</h3>
        <div className="space-y-2">
          {PHASE_STATUS.map((phase) => (
            <div key={phase.key} className="flex items-center gap-3 py-1.5 px-3 rounded-md" style={{ backgroundColor: phase.status === "in-progress" ? tokens.warningBg : "transparent" }}>
              <PhaseIcon status={phase.status} />
              <span className="text-sm flex-1" style={{ color: phase.status === "pending" ? tokens.textTertiary : tokens.textPrimary }}>{phase.label}</span>
              <span className="text-xs font-medium" style={{ color: phase.status === "completed" ? tokens.successBorder : phase.status === "in-progress" ? tokens.warningText : tokens.textTertiary }}>
                {phase.status === "completed" ? "مكتمل" : phase.status === "in-progress" ? "جارٍ التنفيذ" : "لم يبدأ"}
              </span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

/* ── KPI Card component ── */
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

/* ═══════════════════════════════════════════════════════════════════════════════
   Finance V3 Financial Center — Main Page
   ═══════════════════════════════════════════════════════════════════════════════ */
export default function FinanceV3Page() {
  const { user } = useAuthStore();
  const [activeTab, setActiveTab] = useState("overview");

  /* ── Access gate: Admin / Accountant only ────────────────────────────────── */
  const isAuthorized =
    user?.role === "Admin" || user?.role === "Accountant";

  if (!isAuthorized) {
    return <AccessDenied />;
  }

  return (
    <div
      className="min-h-screen flex flex-col"
      style={{ backgroundColor: tokens.bg, direction: "rtl" }}
    >
      {/* ════════════════════════════════════════════════════════════════════════
         Top Header — Compact brand bar
         ════════════════════════════════════════════════════════════════════════ */}
      <header
        className="flex items-center gap-4 px-5 py-2.5 border-b"
        style={{
          backgroundColor: tokens.card,
          borderColor: tokens.border,
          boxShadow: tokens.shadow2,
        }}
      >
        {/* Brand icon + title */}
        <div className="flex items-center gap-2.5">
          <div
            className="w-8 h-8 rounded-md flex items-center justify-center"
            style={{ backgroundColor: tokens.brand }}
          >
            <Wallet className="w-4 h-4" style={{ color: tokens.textOnBrand }} />
          </div>
          <div>
            <h1 className="text-sm font-bold leading-tight" style={{ color: tokens.textPrimary }}>
              المركز المالي
            </h1>
            <p className="text-[11px]" style={{ color: tokens.textTertiary }}>
              Finance V3
            </p>
          </div>
        </div>

        {/* Spacer */}
        <div className="flex-1" />

        {/* Date */}
        <span className="text-xs" style={{ color: tokens.textSecondary }}>
          {todayArabic()}
        </span>

        {/* Divider */}
        <div className="w-px h-5" style={{ backgroundColor: tokens.border }} />

        {/* Branch placeholder */}
        <span className="text-xs font-medium" style={{ color: tokens.textSecondary }}>
          الفرع الرئيسي
        </span>

        {/* Divider */}
        <div className="w-px h-5" style={{ backgroundColor: tokens.border }} />

        {/* Session status */}
        <div className="flex items-center gap-1.5">
          <CircleDot className="w-3 h-3" style={{ color: tokens.textTertiary }} />
          <span className="text-xs" style={{ color: tokens.textTertiary }}>
            لا وردية مفتوحة
          </span>
        </div>

        {/* Divider */}
        <div className="w-px h-5" style={{ backgroundColor: tokens.border }} />

        {/* Notifications */}
        <button
          className="w-7 h-7 rounded-md flex items-center justify-center transition-colors"
          style={{ color: tokens.textSecondary }}
          onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = tokens.cardHover; }}
          onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}
          title="الإشعارات"
        >
          <Bell className="w-4 h-4" />
        </button>
      </header>

      {/* ════════════════════════════════════════════════════════════════════════
         Command Bar — Disabled action buttons
         ════════════════════════════════════════════════════════════════════════ */}
      <div
        className="flex items-center gap-2 px-5 py-2 border-b"
        style={{ backgroundColor: tokens.card, borderColor: tokens.border }}
      >
        {COMMAND_BUTTONS.map((btn) => {
          const Icon = btn.icon;
          return (
            <button
              key={btn.key}
              disabled
              className="flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-medium cursor-not-allowed"
              style={{
                backgroundColor: tokens.cardHover,
                color: tokens.textTertiary,
                border: `1px solid ${tokens.border}`,
                opacity: 0.6,
              }}
              title="هذا الإجراء سيكون متاحاً في المراحل القادمة"
            >
              <Icon className="w-3.5 h-3.5" />
              <span>{btn.label}</span>
              <span
                className="text-[9px] font-bold mr-1 px-1.5 py-0.5 rounded-full"
                style={{
                  backgroundColor: tokens.warningBg,
                  color: tokens.warningText,
                  border: `1px solid ${tokens.warningBorder}`,
                }}
              >
                قريباً
              </span>
            </button>
          );
        })}
      </div>

      {/* ════════════════════════════════════════════════════════════════════════
         KPI Summary Band — Placeholder values only
         ════════════════════════════════════════════════════════════════════════ */}
      <div
        className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 border-b"
        style={{ backgroundColor: tokens.card, borderColor: tokens.border }}
      >
        {KPI_ITEMS.map((kpi) => {
          const Icon = kpi.icon;
          return (
            <div
              key={kpi.key}
              className="flex items-center gap-2.5 px-4 py-3 border-l"
              style={{ borderColor: tokens.border }}
            >
              <Icon className="w-4 h-4 flex-shrink-0" style={{ color: tokens.textTertiary }} />
              <div className="min-w-0">
                <p className="text-[11px] truncate" style={{ color: tokens.textTertiary }}>
                  {kpi.label}
                </p>
                <p className="text-sm font-bold" style={{ color: tokens.textTertiary }}>
                  —
                </p>
              </div>
            </div>
          );
        })}
      </div>

      {/* ════════════════════════════════════════════════════════════════════════
         Tabs
         ════════════════════════════════════════════════════════════════════════ */}
      <div
        className="flex items-center border-b px-5 overflow-x-auto"
        style={{ backgroundColor: tokens.card, borderColor: tokens.border }}
      >
        {TABS.map((tab) => {
          const Icon = tab.icon;
          const isActive = activeTab === tab.key;
          return (
            <button
              key={tab.key}
              onClick={() => setActiveTab(tab.key)}
              className="flex items-center gap-1.5 px-3 py-2.5 text-xs font-medium whitespace-nowrap transition-colors relative"
              style={{
                color: isActive ? tokens.brand : tokens.textSecondary,
                borderBottom: isActive ? `2px solid ${tokens.brand}` : "2px solid transparent",
              }}
              onMouseEnter={(e) => {
                if (!isActive) e.currentTarget.style.color = tokens.textPrimary;
              }}
              onMouseLeave={(e) => {
                if (!isActive) e.currentTarget.style.color = tokens.textSecondary;
              }}
            >
              <Icon className="w-3.5 h-3.5" />
              <span>{tab.label}</span>
            </button>
          );
        })}
      </div>

      {/* ════════════════════════════════════════════════════════════════════════
         Main Content Area
         ════════════════════════════════════════════════════════════════════════ */}
      <div className="flex-1 flex">
        {/* Tab content */}
        <div className="flex-1 overflow-y-auto">
          {activeTab === "overview" ? (
            <OverviewTab />
          ) : (
            <EmptyTabState tab={TABS.find((t) => t.key === activeTab)!} />
          )}
        </div>

        {/* ── Review side panel ────────────────────────────────────────────── */}
        <aside
          className="w-72 border-r hidden xl:block overflow-y-auto"
          style={{ backgroundColor: tokens.card, borderColor: tokens.border }}
        >
          <div className="p-4 space-y-4">
            <h3 className="text-xs font-bold uppercase tracking-wide" style={{ color: tokens.textTertiary }}>
              حالة إعادة البناء
            </h3>

            {/* Phase list */}
            <div className="space-y-2">
              {PHASE_STATUS.map((phase) => (
                <div key={phase.key} className="flex items-center gap-2.5">
                  <PhaseIcon status={phase.status} />
                  <span
                    className="text-xs flex-1"
                    style={{
                      color: phase.status === "pending" ? tokens.textTertiary : tokens.textPrimary,
                    }}
                  >
                    {phase.label}
                  </span>
                </div>
              ))}
            </div>

            {/* Current phase detail */}
            <div
              className="rounded-md border p-3"
              style={{ backgroundColor: tokens.warningBg, borderColor: tokens.warningBorder }}
            >
              <div className="flex items-center gap-2 mb-1.5">
                <Clock className="w-3.5 h-3.5" style={{ color: tokens.warningText }} />
                <span className="text-xs font-bold" style={{ color: tokens.warningText }}>
                  المرحلة ٣: الفواتير والتحصيل
                </span>
              </div>
              <p className="text-[11px] leading-relaxed" style={{ color: tokens.warningText }}>
                جارٍ بناء مسارات API للمالية وربطها بالواجهة. المرحلتان ١ و ٢ مكتملتان
                — دفتر الأستاذ المزدوج والكتابة المزدوجة يعملان الآن.
              </p>
            </div>

            {/* Quick links */}
            <div className="space-y-1.5">
              <h4 className="text-xs font-semibold" style={{ color: tokens.textPrimary }}>
                روابط سريعة
              </h4>
              <a
                href="/daily-operations"
                className="flex items-center gap-2 text-xs py-1.5 px-2 rounded-md transition-colors"
                style={{ color: tokens.brand }}
                onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = tokens.brandLight; }}
                onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}
              >
                <ChevronLeft className="w-3 h-3" />
                التشغيل اليومي (تسجيل التحصيل)
              </a>
              <a
                href="/reports"
                className="flex items-center gap-2 text-xs py-1.5 px-2 rounded-md transition-colors"
                style={{ color: tokens.brand }}
                onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = tokens.brandLight; }}
                onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}
              >
                <ChevronLeft className="w-3 h-3" />
                التقارير
              </a>
            </div>
          </div>
        </aside>
      </div>

      {/* ════════════════════════════════════════════════════════════════════════
         Arabic Notice Banner — Bottom
         ════════════════════════════════════════════════════════════════════════ */}
      <div
        className="px-5 py-2.5 border-t"
        style={{ backgroundColor: tokens.warningBg, borderColor: tokens.warningBorder }}
      >
        <div className="flex items-center gap-2">
          <AlertTriangle className="w-4 h-4 flex-shrink-0" style={{ color: tokens.warningText }} />
          <p className="text-xs" style={{ color: tokens.warningText }}>
            تجري إعادة بناء وحدة المحاسبة والمراجعة المالية. يستمر تسجيل تحصيل
            المرضى من شاشة التشغيل اليومي وفق سير العمل المعتمد. لا تستخدم هذه
            الشاشة لإدخال أو تعديل قيود مالية حتى اكتمال الاعتماد.
          </p>
        </div>
      </div>
    </div>
  );
}
