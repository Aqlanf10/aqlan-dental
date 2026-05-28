"use client";

import { useState, useEffect } from "react";
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
  Loader2,
} from "lucide-react";
import { useAuthStore } from "@/stores/authStore";
import { useBranches } from "@/hooks/useBranches";
import { api } from "@/lib/api";
import { AccessDenied, FinanceTabErrorBoundary, tokens } from "./components/FinanceSharedUI";
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
   Finance V3 Financial Center — Main Page
   ═══════════════════════════════════════════════════════════════════════════════ */
export default function FinanceV3Page() {
  const { user } = useAuthStore();
  const { data: branches } = useBranches("active");
  const [activeTab, setActiveTab] = useState("overview");
  const [activeSession, setActiveSession] = useState<{ sessionNumber: string; openedAt: string } | null>(null);
  const [sessionLoading, setSessionLoading] = useState(true);

  /* ── Access gate: Admin / Accountant only ── */
  const isAuthorized = user?.role === "Admin" || user?.role === "Accountant";
  const isAdmin = user?.role === "Admin";

  /* ── Fetch active session status ── */
  useEffect(() => {
    let cancelled = false;
    const fetchActiveSession = async () => {
      try {
        setSessionLoading(true);
        const { data } = await api.get<{
          hasActiveSession: boolean;
          id?: string;
          sessionNumber?: string;
          openedAt?: string;
          openingTime?: string;
        }>("/api/finance-v3/cashier-sessions/active");
        if (!cancelled) {
          if (data?.hasActiveSession) {
            setActiveSession({
              sessionNumber: data.sessionNumber ?? `CS-${data.id?.slice(0, 8)}`,
              openedAt: data.openedAt ?? data.openingTime ?? "",
            });
          } else {
            setActiveSession(null);
          }
        }
      } catch {
        if (!cancelled) setActiveSession(null);
      } finally {
        if (!cancelled) setSessionLoading(false);
      }
    };
    fetchActiveSession();
    // Poll every 60 seconds to keep status fresh
    const interval = setInterval(fetchActiveSession, 60_000);
    return () => { cancelled = true; clearInterval(interval); };
  }, []);

  /* ── Resolve branch name ── */
  const userBranchId = user?.branchId;
  const branchName = userBranchId
    ? (branches ?? []).find((b) => b.id === userBranchId)?.name ?? "فرع غير معروف"
    : isAdmin ? "جميع الفروع" : "غير محدد";

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
        <span className="text-xs font-medium" style={{ color: tokens.textSecondary }}>{branchName}</span>
        <div className="w-px h-5" style={{ backgroundColor: tokens.border }} />
        <div className="flex items-center gap-1.5">
          {sessionLoading ? (
            <Loader2 className="w-3 h-3 animate-spin" style={{ color: tokens.textTertiary }} />
          ) : activeSession ? (
            <>
              <CircleDot className="w-3 h-3" style={{ color: tokens.successBorder }} />
              <span className="text-xs font-medium" style={{ color: tokens.successBorder }}>وردية مفتوحة: {activeSession.sessionNumber}</span>
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
        {activeTab === "overview" && <FinanceTabErrorBoundary tabName="نظرة عامة"><OverviewTab /></FinanceTabErrorBoundary>}
        {activeTab === "patient-acct" && <FinanceTabErrorBoundary tabName="حسابات المرضى"><PatientAccountsTab /></FinanceTabErrorBoundary>}
        {activeTab === "invoices" && <FinanceTabErrorBoundary tabName="الفواتير"><InvoicesTab /></FinanceTabErrorBoundary>}
        {activeTab === "collections" && <FinanceTabErrorBoundary tabName="التحصيل"><CollectionsTab /></FinanceTabErrorBoundary>}
        {activeTab === "contracts" && <FinanceTabErrorBoundary tabName="العقود"><ContractsTab /></FinanceTabErrorBoundary>}
        {activeTab === "cashier" && <FinanceTabErrorBoundary tabName="الصندوق"><CashierTab isAdmin={isAdmin} /></FinanceTabErrorBoundary>}
        {activeTab === "treasuries" && <FinanceTabErrorBoundary tabName="الخزائن"><TreasuriesTab /></FinanceTabErrorBoundary>}
        {activeTab === "expenses" && <FinanceTabErrorBoundary tabName="المصروفات"><ExpensesTab /></FinanceTabErrorBoundary>}
        {activeTab === "suppliers" && <FinanceTabErrorBoundary tabName="الموردون"><SuppliersTab /></FinanceTabErrorBoundary>}
        {activeTab === "audit" && <FinanceTabErrorBoundary tabName="سجل المراجعة"><AuditTab /></FinanceTabErrorBoundary>}
      </div>
    </div>
  );
}
