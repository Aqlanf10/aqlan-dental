"use client";

import { useState, useEffect, useCallback, Suspense } from "react";
import { useSearchParams } from "next/navigation";
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
  Bell,
  CircleDot,
  BookOpen,
} from "lucide-react";
import { useAuthStore } from "@/stores/authStore";
import { api } from "@/lib/api";
import { AccessDenied, tokens } from "./components/FinanceSharedUI";
import { todayArabic } from "./components/FinanceHelpers";
import { OverviewTab } from "./components/OverviewTab";
import { PatientAccountsTab } from "./components/PatientAccountsTab";
import { InvoicesTab } from "./components/InvoicesTab";
import { CollectionsTab } from "./components/CollectionsTab";
import { ContractsTab } from "./components/ContractsTab";
import { CashierTab } from "./components/CashierTab";
import { TreasuriesTab } from "./components/TreasuriesTab";
import { ExpensesTab } from "./components/ExpensesTab";
import { SuppliersTab } from "./components/SuppliersTab";
import { AuditTab } from "./components/AuditTab";
import { JournalTab } from "./components/JournalTab";

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
  { key: "journal",        label: "قيود اليومية",      icon: BookOpen },
  { key: "audit",          label: "سجل المراجعة",      icon: ClipboardCheck },
];

/* ═══════════════════════════════════════════════════════════════════════════════
   Finance V3 Financial Center — Main Page
   ═══════════════════════════════════════════════════════════════════════════════ */
const VALID_TABS = new Set(TABS.map((t) => t.key));

function FinanceV3PageInner() {
  const searchParams = useSearchParams();
  const tabFromUrl = searchParams.get("tab");
  const initialTab = tabFromUrl && VALID_TABS.has(tabFromUrl) ? tabFromUrl : "overview";

  const { user } = useAuthStore();
  const [activeTab, setActiveTab] = useState(initialTab);
  const [hasActiveSession, setHasActiveSession] = useState(false);
  const [activeSessionInfo, setActiveSessionInfo] = useState<{ cashierName: string; openedAt: string } | null>(null);

  /* ── Access gate: Admin / Accountant only ── */
  const isAuthorized = user?.role === "Admin" || user?.role === "Accountant";
  const isAdmin = user?.role === "Admin";

  /* ── Fetch active cashier session status ── */
  const fetchActiveSession = useCallback(async () => {
    try {
      const { data } = await api.get<{ hasActiveSession: boolean; cashierName?: string; openedAt?: string }>("/api/finance-v3/cashier-sessions/active");
      setHasActiveSession(data?.hasActiveSession ?? false);
      if (data?.hasActiveSession) {
        setActiveSessionInfo({ cashierName: data.cashierName ?? "", openedAt: data.openedAt ?? "" });
      } else {
        setActiveSessionInfo(null);
      }
    } catch {
      setHasActiveSession(false);
      setActiveSessionInfo(null);
    }
  }, []);

  useEffect(() => { fetchActiveSession(); }, [fetchActiveSession]);

  useEffect(() => {
    if (tabFromUrl && VALID_TABS.has(tabFromUrl)) setActiveTab(tabFromUrl);
  }, [tabFromUrl]);

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
          {hasActiveSession ? (
            <>
              <CircleDot className="w-3 h-3" style={{ color: tokens.successBorder }} />
              <span className="text-xs font-medium" style={{ color: tokens.successBorder }}>وردية مفتوحة</span>
              {activeSessionInfo?.cashierName && (
                <span className="text-xs" style={{ color: tokens.textTertiary }}>({activeSessionInfo.cashierName})</span>
              )}
            </>
          ) : (
            <>
              <CircleDot className="w-3 h-3" style={{ color: tokens.textTertiary }} />
              <span className="text-xs" style={{ color: tokens.textTertiary }}>لا وردية مفتوحة</span>
            </>
          )}
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
        {activeTab === "journal" && <JournalTab />}
        {activeTab === "audit" && <AuditTab />}
      </div>
    </div>
  );
}

export default function FinanceV3Page() {
  return (
    <Suspense fallback={<div className="min-h-screen flex items-center justify-center text-sm text-slate-500">جاري التحميل...</div>}>
      <FinanceV3PageInner />
    </Suspense>
  );
}
