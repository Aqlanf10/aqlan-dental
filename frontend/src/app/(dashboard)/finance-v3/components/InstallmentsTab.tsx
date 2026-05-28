"use client";

import { useState, useEffect, useCallback } from "react";
import {
  CalendarClock,
  RefreshCw,
  ChevronDown,
  ChevronUp,
} from "lucide-react";
import { api } from "@/lib/api";
import { toast } from "@/stores/toastStore";
import type {
  InstallmentPlanDto,
  ContractListItem,
} from "./types";
import {
  SectionHeader,
  LoadingSkeleton,
  EmptyState,
  DataTable,
  StatusBadge,
  tokens,
  inputStyle,
} from "./FinanceSharedUI";
import {
  formatYER,
  safeFormatDate,
  extractErrorMessage,
  safeArray,
} from "./FinanceHelpers";
import InstallmentCard from "./InstallmentCard";

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab: Installments — إدارة خطط التقسيط والأقساط
   ═══════════════════════════════════════════════════════════════════════════════ */

export function InstallmentsTab() {
  const [contracts, setContracts] = useState<ContractListItem[]>([]);
  const [plans, setPlans] = useState<Record<string, InstallmentPlanDto>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [expandedContract, setExpandedContract] = useState<string | null>(null);
  const [planLoading, setPlanLoading] = useState<string | null>(null);

  // ── جلب العقود ──
  const fetchContracts = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const { data: responseData } = await api.get<{
        data: ContractListItem[];
        total: number;
      }>("/api/finance-v3/contracts", { params: { status: "Active" } });
      const contractsList = safeArray(
        responseData?.data ??
          (Array.isArray(responseData)
            ? (responseData as unknown as ContractListItem[])
            : undefined)
      );
      setContracts(contractsList);
    } catch (err) {
      setError(extractErrorMessage(err, "فشل في تحميل العقود"));
      toast.error("فشل في تحميل العقود");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchContracts();
  }, [fetchContracts]);

  // ── جلب خطة التقسيط لعقد محدد ──
  const fetchPlan = async (contractId: string) => {
    try {
      setPlanLoading(contractId);
      const { data } = await api.get<InstallmentPlanDto>(
        `/api/finance-v3/contracts/${contractId}/installments`
      );
      if (data) {
        setPlans((prev) => ({ ...prev, [contractId]: data }));
      }
    } catch {
      // 404 means no plan yet — that's fine
      setPlans((prev) => ({ ...prev, [contractId]: null as unknown as InstallmentPlanDto }));
    } finally {
      setPlanLoading(null);
    }
  };

  // ── توسيع/طي عقد لعرض خطة التقسيط ──
  const toggleExpand = (contractId: string) => {
    if (expandedContract === contractId) {
      setExpandedContract(null);
    } else {
      setExpandedContract(contractId);
      // جلب الخطة فقط إذا لم تُجلب من قبل
      if (!plans[contractId]) {
        fetchPlan(contractId);
      }
    }
  };

  const filtered = contracts.filter(
    (c) =>
      (c.patientName ?? "").includes(search) ||
      (c.contractNumber ?? "").includes(search)
  );

  return (
    <div className="p-6 space-y-4">
      <SectionHeader
        title="خطط التقسيط"
        action={
          <div className="flex items-center gap-2">
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="بحث بالاسم أو رقم العقد..."
              style={{ ...inputStyle, width: 240, fontSize: 13 }}
            />
            <button
              onClick={fetchContracts}
              className="w-8 h-8 rounded-md flex items-center justify-center"
              style={{
                color: tokens.brand,
                border: `1px solid ${tokens.border}`,
              }}
              title="تحديث"
            >
              <RefreshCw className="w-4 h-4" />
            </button>
          </div>
        }
      />

      {loading ? (
        <LoadingSkeleton />
      ) : error ? (
        <div
          className="rounded-lg border p-4"
          style={{
            backgroundColor: tokens.dangerBg,
            borderColor: tokens.dangerBorder,
          }}
        >
          <p className="text-sm" style={{ color: tokens.dangerText }}>
            {error}
          </p>
          <button
            onClick={fetchContracts}
            className="text-xs font-medium mt-2 underline"
            style={{ color: tokens.brand }}
          >
            إعادة المحاولة
          </button>
        </div>
      ) : filtered.length === 0 ? (
        <EmptyState icon={CalendarClock} message="لا توجد عقود نشطة" />
      ) : (
        <div className="space-y-2">
          {filtered.map((contract) => {
            const isExpanded = expandedContract === contract.id;
            const plan = plans[contract.id];
            const isPlanLoading = planLoading === contract.id;

            return (
              <div
                key={contract.id}
                className="rounded-lg border"
                style={{
                  backgroundColor: tokens.card,
                  borderColor: tokens.border,
                }}
              >
                {/* ── رأس العقد (قابل للنقر) ── */}
                <button
                  className="w-full flex items-center gap-4 p-4 text-right transition-colors"
                  style={{ color: tokens.textPrimary }}
                  onClick={() => toggleExpand(contract.id)}
                  onMouseEnter={(e) => {
                    e.currentTarget.style.backgroundColor = tokens.cardHover;
                  }}
                  onMouseLeave={(e) => {
                    e.currentTarget.style.backgroundColor = "transparent";
                  }}
                >
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-3 mb-1">
                      <span className="text-sm font-bold">
                        {contract.contractNumber}
                      </span>
                      <StatusBadge status={contract.status} />
                      {contract.isOverdue && (
                        <span
                          className="text-[11px] font-semibold px-2 py-0.5 rounded-full"
                          style={{
                            backgroundColor: tokens.dangerBg,
                            color: tokens.dangerText,
                          }}
                        >
                          متأخر
                        </span>
                      )}
                    </div>
                    <p
                      className="text-xs"
                      style={{ color: tokens.textSecondary }}
                    >
                      {contract.patientName} — {formatYER(contract.totalAmount)}
                    </p>
                  </div>
                  <div className="flex items-center gap-3">
                    <span className="text-xs" style={{ color: tokens.textSecondary }}>
                      المستحق: {formatYER(contract.outstandingAmount)}
                    </span>
                    {isExpanded ? (
                      <ChevronUp className="w-4 h-4" style={{ color: tokens.textTertiary }} />
                    ) : (
                      <ChevronDown className="w-4 h-4" style={{ color: tokens.textTertiary }} />
                    )}
                  </div>
                </button>

                {/* ── تفاصيل خطة التقسيط ── */}
                {isExpanded && (
                  <div
                    className="border-t p-4"
                    style={{ borderColor: tokens.border }}
                  >
                    {isPlanLoading ? (
                      <LoadingSkeleton rows={3} />
                    ) : plan && plan.id ? (
                      <div className="space-y-3">
                        {/* ملخص الخطة */}
                        <div
                          className="grid grid-cols-4 gap-3 rounded-lg p-3"
                          style={{ backgroundColor: tokens.brandLight }}
                        >
                          <div>
                            <p className="text-[11px]" style={{ color: tokens.textTertiary }}>
                              الإجمالي
                            </p>
                            <p className="text-sm font-bold" style={{ color: tokens.brand }}>
                              {formatYER(plan.totalAmount)}
                            </p>
                          </div>
                          <div>
                            <p className="text-[11px]" style={{ color: tokens.textTertiary }}>
                              الدفعة المقدمة
                            </p>
                            <p className="text-sm font-bold" style={{ color: tokens.textPrimary }}>
                              {formatYER(plan.downPayment)}
                            </p>
                          </div>
                          <div>
                            <p className="text-[11px]" style={{ color: tokens.textTertiary }}>
                              القسط الشهري
                            </p>
                            <p className="text-sm font-bold" style={{ color: tokens.warningBorder }}>
                              {formatYER(plan.monthlyAmount)}
                            </p>
                          </div>
                          <div>
                            <p className="text-[11px]" style={{ color: tokens.textTertiary }}>
                              الحالة
                            </p>
                            {plan.isCompleted ? (
                              <span className="text-sm font-bold" style={{ color: tokens.successBorder }}>
                                مكتملة
                              </span>
                            ) : (
                              <span className="text-sm font-bold" style={{ color: tokens.brand }}>
                                جارية ({plan.numberOfMonths} شهر)
                              </span>
                            )}
                          </div>
                        </div>

                        {/* الأقساط */}
                        <div className="space-y-2">
                          <p className="text-xs font-semibold" style={{ color: tokens.textSecondary }}>
                            الأقساط المجدولة
                          </p>
                          {(plan.installments ?? []).map((inst) => (
                            <InstallmentCard
                              key={inst.id}
                              installment={inst}
                              onPaid={() => fetchPlan(contract.id)}
                            />
                          ))}
                          {(plan.installments ?? []).length === 0 && (
                            <p className="text-xs text-center py-4" style={{ color: tokens.textTertiary }}>
                              لا توجد أقساط مجدولة
                            </p>
                          )}
                        </div>
                      </div>
                    ) : (
                      <div className="text-center py-6">
                        <p className="text-sm" style={{ color: tokens.textSecondary }}>
                          لا توجد خطة تقسيط لهذا العقد
                        </p>
                        <p className="text-xs mt-1" style={{ color: tokens.textTertiary }}>
                          يمكن إنشاء خطة تقسيط من صفحة العقود
                        </p>
                      </div>
                    )}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
