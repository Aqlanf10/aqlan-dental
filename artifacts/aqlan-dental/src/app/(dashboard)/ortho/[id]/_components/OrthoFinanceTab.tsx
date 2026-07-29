
import Link from "@/lib/nextLinkCompat";
import { useQuery } from "@tanstack/react-query";
import { CalendarClock, Receipt, Wallet } from "lucide-react";
import api from "@/lib/api";
import { useAuthStore } from "@/stores/authStore";
import { canViewPatientFinance } from "@/lib/roles";
import {
  cn,
  formatArabicDate,
  formatYemeniRiyal,
} from "@/lib/utils";
import { financeV3ContractsUrl } from "@/lib/financeRoutes";
import { useOrthoCase, useOrthoOverview } from "@/hooks/useOrtho";

/**
 * Sprint 15 — Orthodontic financial overview panel.
 *
 * The audit (Sprint 15) said: "لا توجد لوحة تجمع رسوم حالة التقويم مقابل المحصل
 * مقابل المتأخر في عرض واحد" — there was no single panel that brings together the
 * ortho case fee vs. collected vs. remaining.
 *
 * This tab now shows:
 *  - Total treatment contract/fees (contract total when a contract is linked,
 *    otherwise the OrthoCase.TotalFee which is always visible).
 *  - Paid amount (YER only — the backend `finance-summary` filters
 *    `Currency == null || Currency == "YER"` so foreign-currency payments are
 *    not mixed into the YER total).
 *  - Remaining amount.
 *  - Last payment (date + amount + currency symbol).
 *  - Related invoices/payments count.
 *
 * Permissions: a Doctor/Orthodontist without finance access
 * (`canViewPatientFinance === false`) sees a clinical-safe summary — just the
 * ortho case fee (always visible from the OrthoCase itself) and the contract
 * remaining amount — without detailed per-payment numbers. Admin/Accountant/
 * Reception see the full panel.
 *
 * No backend changes: reuses `GET /api/patients/{patientId}/finance-summary`
 * (existing endpoint) — does NOT duplicate finance logic. The OrthoOverview
 * React Query cache (shared with the page shell) supplies the contract totals.
 */
export function OrthoFinanceTab({
  caseId,
  patientId,
}: {
  caseId: string;
  patientId: string;
}) {
  const { data: overview } = useOrthoOverview(caseId);
  const { data: orthoCase } = useOrthoCase(caseId);
  const { user } = useAuthStore();
  const canSeeFinanceDetails = canViewPatientFinance(user?.role);

  // Patient finance summary — already filtered YER-only on the backend.
  // Disabled for users who can't view patient finance (Doctor without access)
  // so we don't even fire the request.
  const { data: financeSummary, isLoading: financeLoading } = useQuery({
    queryKey: ["patient-finance-summary", patientId],
    queryFn: async () =>
      (await api.get<PatientFinanceSummary>(`/api/patients/${patientId}/finance-summary`)).data,
    enabled: !!patientId && canSeeFinanceDetails,
    staleTime: 30_000,
  });

  // ── Sources of truth ────────────────────────────────────────────────
  // Contract totals come from the OrthoOverview (already loaded by the page
  // shell). YER-only paid + last payment + payments count come from the
  // patient finance summary. When there is no linked contract, we fall back
  // to the OrthoCase.TotalFee (always visible from the case itself).
  const contractTotal = overview?.contractTotal ?? orthoCase?.totalFee ?? 0;
  const contractRemaining = overview?.contractRemaining ?? null;
  const yerPaid = financeSummary?.totalPaid ?? null;
  // For the "remaining" line: prefer the YER-only computation
  // (totalContract - totalPaidYer) when we have the YER paid; fall back to the
  // overview's contractRemaining (which uses contract-scoped payments sum).
  const remaining =
    yerPaid != null
      ? Math.max(0, contractTotal - yerPaid)
      : contractRemaining ?? contractTotal;

  const lastPayment = financeSummary?.latestPayment ?? null;
  const paymentsCount = financeSummary?.totalPaymentsCount ?? 0;
  const activeContractsCount = financeSummary?.activeContractsCount ?? 0;

  // Doctor-without-finance-safe summary: only the case fee (always visible)
  // and the contract remaining amount. No per-payment figures.
  if (!canSeeFinanceDetails) {
    return (
      <div className="space-y-5">
        <div className="rounded-lg border border-gray-200 bg-white p-5">
          <p className="text-sm font-semibold text-gray-900">
            رسوم حالة التقويم
          </p>
          <p className="mt-2 text-2xl font-bold text-gray-900">
            {formatYemeniRiyal(contractTotal)}
          </p>
          {contractRemaining != null && (
            <p className="mt-3 text-sm text-gray-500">
              المتبقي على العقد:{" "}
              <span className="font-semibold text-red-600">
                {formatYemeniRiyal(contractRemaining)}
              </span>
            </p>
          )}
        </div>
        <p className="text-xs text-gray-400">
          لعرض التفاصيل المالية الكاملة (المدفوعات، آخر دفعة، الفواتير المرتبطة)،
          تواصل مع الإدارة أو المحاسب.
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-5">
      {/* Summary cards — total / paid / remaining */}
      <div className="grid gap-5 md:grid-cols-3">
        <FinanceCard
          label="إجمالي رسوم التقويم"
          value={formatYemeniRiyal(contractTotal)}
          icon={<Wallet className="h-4 w-4 text-clinic-blue" />}
        />
        <FinanceCard
          label="المحصّل (ر.ي)"
          value={yerPaid != null ? formatYemeniRiyal(yerPaid) : "—"}
          valueClassName="text-green-600"
          loading={financeLoading}
        />
        <FinanceCard
          label="المتبقي"
          value={formatYemeniRiyal(remaining)}
          valueClassName="text-red-600"
        />
      </div>

      {/* Last payment + counts */}
      <div className="grid gap-5 md:grid-cols-2">
        <div className="rounded-lg border border-gray-200 bg-white p-5">
          <p className="flex items-center gap-2 text-sm font-semibold text-gray-900">
            <CalendarClock className="h-4 w-4 text-clinic-blue" />
            آخر دفعة
          </p>
          {lastPayment ? (
            <div className="mt-3 space-y-1.5 text-sm">
              <div className="flex items-baseline justify-between gap-2">
                <span className="text-gray-500">التاريخ</span>
                <span className="font-medium text-gray-900">
                  {formatArabicDate(lastPayment.paymentDate)}
                </span>
              </div>
              <div className="flex items-baseline justify-between gap-2">
                <span className="text-gray-500">المبلغ</span>
                <span className="font-bold text-green-700">
                  {formatPaymentAmount(lastPayment.amount, lastPayment.currency)}
                </span>
              </div>
              {lastPayment.paymentMethod && (
                <div className="flex items-baseline justify-between gap-2">
                  <span className="text-gray-500">طريقة الدفع</span>
                  <span className="text-gray-700">
                    {lastPayment.paymentMethod}
                  </span>
                </div>
              )}
            </div>
          ) : (
            <p className="mt-3 text-sm text-gray-400">
              {financeLoading ? "جارٍ التحميل..." : "لا توجد دفعات مسجلة بعد."}
            </p>
          )}
        </div>

        <div className="rounded-lg border border-gray-200 bg-white p-5">
          <p className="flex items-center gap-2 text-sm font-semibold text-gray-900">
            <Receipt className="h-4 w-4 text-clinic-blue" />
            المرتبطات المالية
          </p>
          <div className="mt-3 space-y-2 text-sm">
            <div className="flex items-baseline justify-between gap-2">
              <span className="text-gray-500">العقود النشطة</span>
              <span className="font-semibold text-gray-900">
                {activeContractsCount}
              </span>
            </div>
            <div className="flex items-baseline justify-between gap-2">
              <span className="text-gray-500">إجمالي الدفعات</span>
              <span className="font-semibold text-gray-900">
                {paymentsCount}
              </span>
            </div>
            {overview?.contractId && (
              <div className="flex items-baseline justify-between gap-2">
                <span className="text-gray-500">العقد المرتبط بالحالة</span>
                <span className="font-semibold text-clinic-blue">نعم</span>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Quick actions */}
      <div className="rounded-lg border border-gray-200 bg-white p-5">
        <div className="flex flex-wrap gap-3">
          {overview?.contractId && (
            <Link
              href={financeV3ContractsUrl(patientId)}
              className="rounded-lg bg-clinic-blue px-4 py-2 text-sm font-medium text-white"
            >
              فتح العقد
            </Link>
          )}
          <Link
            href={financeV3ContractsUrl(patientId, { relatedCaseId: caseId })}
            className="rounded-lg border border-gray-200 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
          >
            إنشاء عقد تقويم
          </Link>
          <Link
            href={financeV3ContractsUrl(patientId)}
            className="rounded-lg border border-gray-200 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
          >
            مالية المريض
          </Link>
        </div>
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ */
/*  Helpers                                                            */
/* ------------------------------------------------------------------ */

function FinanceCard({
  label,
  value,
  valueClassName,
  icon,
  loading,
}: {
  label: string;
  value: string;
  valueClassName?: string;
  icon?: React.ReactNode;
  loading?: boolean;
}) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white p-5">
      <p className="flex items-center gap-2 text-sm text-gray-500">
        {icon}
        {label}
      </p>
      <p
        className={cn(
          "mt-2 text-2xl font-bold text-gray-900",
          valueClassName,
          loading && "animate-pulse text-gray-300",
        )}
      >
        {loading ? "—" : value}
      </p>
    </div>
  );
}

/**
 * Format a payment amount with its currency symbol. YER (or null/empty — the
 * legacy default) uses the Arabic-Yemeni riyal formatter. Foreign currencies
 * (SAR, USD, …) are formatted with their ISO code suffix so the doctor never
 * mistakes a USD payment for a YER one — consistent with the backend rule
 * that foreign-currency payments are excluded from YER totals.
 */
function formatPaymentAmount(amount: number, currency?: string | null): string {
  const code = (currency ?? "").toUpperCase();
  if (code === "" || code === "YER") {
    return formatYemeniRiyal(amount);
  }
  // Foreign currency — show the ISO code explicitly (no exchange-rate mixing).
  return `${new Intl.NumberFormat("ar-YE", {
    maximumFractionDigits: 2,
  }).format(amount)} ${code}`;
}

/* ------------------------------------------------------------------ */
/*  Local types — mirror the existing `PatientFinanceSummary` shape    */
/*  from `@/types/finance` but add the optional `currency` field that  */
/*  the backend already returns (PaymentDto.Currency) so we can show   */
/*  the currency symbol next to the last payment amount without        */
/*  touching the shared type.                                          */
/* ------------------------------------------------------------------ */
interface LatestPayment {
  id: string;
  amount: number;
  currency?: string | null;
  paymentDate: string;
  paymentMethod?: string;
  serviceDescription?: string;
}

interface PatientFinanceSummary {
  totalTreatmentCost: number;
  totalPaid: number;
  outstandingBalance: number;
  overdueAmount: number;
  latestPayment: LatestPayment | null;
  financialStatus: "no_plan" | "paid" | "has_balance" | "overdue" | "on_track";
  activeContractsCount: number;
  totalPaymentsCount: number;
}
