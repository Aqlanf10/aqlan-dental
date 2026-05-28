"use client";

import { useState, useEffect, useCallback } from "react";
import {
  ShieldCheck,
  RefreshCw,
  Loader2,
} from "lucide-react";
import { api } from "@/lib/api";
import { toast } from "@/stores/toastStore";
import type {
  InsuranceClaimDto,
  SettleInsuranceClaimRequest,
} from "./types";
import { CLAIM_STATUS_MAP } from "./types";
import {
  SectionHeader,
  LoadingSkeleton,
  EmptyState,
  DataTable,
  Modal,
  tokens,
  inputStyle,
  labelStyle,
  btnPrimary,
  btnGhost,
} from "./FinanceSharedUI";
import {
  formatYER,
  safeFormatDate,
  extractErrorMessage,
  safeArray,
} from "./FinanceHelpers";

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab: Insurance — إدارة المطالبات التأمينية
   ═══════════════════════════════════════════════════════════════════════════════ */

export function InsuranceTab() {
  const [claims, setClaims] = useState<InsuranceClaimDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("");

  // ── نافذة التسوية ──
  const [settleClaimId, setSettleClaimId] = useState<string | null>(null);
  const [settleNotes, setSettleNotes] = useState("");
  const [isSettling, setIsSettling] = useState(false);

  // ── جلب المطالبات ──
  const fetchClaims = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const params: Record<string, string> = {};
      if (statusFilter) params.status = statusFilter;

      const { data: responseData } = await api.get<{
        data: InsuranceClaimDto[];
        total: number;
      }>("/api/finance-v3/insurance-claims", { params });

      const claimsList = safeArray(
        responseData?.data ??
          (Array.isArray(responseData)
            ? (responseData as unknown as InsuranceClaimDto[])
            : undefined)
      );
      setClaims(claimsList);
    } catch (err) {
      setError(extractErrorMessage(err, "فشل في تحميل المطالبات التأمينية"));
      toast.error("فشل في تحميل المطالبات التأمينية");
    } finally {
      setLoading(false);
    }
  }, [statusFilter]);

  useEffect(() => {
    fetchClaims();
  }, [fetchClaims]);

  // ── تسوية مطالبة ──
  const handleSettle = async () => {
    if (!settleClaimId) return;
    setIsSettling(true);
    try {
      const payload: SettleInsuranceClaimRequest = {
        referenceNotes: settleNotes || undefined,
      };
      await api.post(
        `/api/finance-v3/insurance-claims/${settleClaimId}/settle`,
        payload
      );
      toast.success("تم تسوية المطالبة التأمينية بنجاح");
      setSettleClaimId(null);
      setSettleNotes("");
      fetchClaims();
    } catch (err) {
      toast.error(extractErrorMessage(err, "فشل في تسوية المطالبة"));
    } finally {
      setIsSettling(false);
    }
  };

  const filtered = claims.filter(
    (c) =>
      (c.insuranceCompanyName ?? "").includes(search) ||
      (c.id ?? "").includes(search)
  );

  // ── إحصائيات سريعة ──
  const totalClaims = claims.length;
  const pendingClaims = claims.filter((c) => c.status === "Pending").length;
  const totalCovered = claims
    .filter((c) => c.status === "Pending" || c.status === "Approved")
    .reduce((sum, c) => sum + (c.coveredAmount ?? 0), 0);

  return (
    <div className="p-6 space-y-4">
      <SectionHeader
        title="المطالبات التأمينية"
        action={
          <div className="flex items-center gap-2">
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="بحث بالشركة أو الرقم..."
              style={{ ...inputStyle, width: 200, fontSize: 13 }}
            />
            <select
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
              style={{ ...inputStyle, width: 140, fontSize: 13 }}
            >
              <option value="">جميع الحالات</option>
              <option value="Pending">قيد الانتظار</option>
              <option value="Approved">معتمدة</option>
              <option value="Rejected">مرفوضة</option>
              <option value="Paid">تم السداد</option>
            </select>
            <button
              onClick={fetchClaims}
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

      {/* ── إحصائيات سريعة ── */}
      <div className="grid grid-cols-3 gap-3">
        <div
          className="rounded-lg border p-3"
          style={{
            backgroundColor: tokens.card,
            borderColor: tokens.border,
          }}
        >
          <p className="text-[11px]" style={{ color: tokens.textTertiary }}>
            إجمالي المطالبات
          </p>
          <p className="text-lg font-bold" style={{ color: tokens.textPrimary }}>
            {totalClaims}
          </p>
        </div>
        <div
          className="rounded-lg border p-3"
          style={{
            backgroundColor: tokens.card,
            borderColor: tokens.border,
          }}
        >
          <p className="text-[11px]" style={{ color: tokens.textTertiary }}>
            قيد الانتظار
          </p>
          <p
            className="text-lg font-bold"
            style={{ color: tokens.warningBorder }}
          >
            {pendingClaims}
          </p>
        </div>
        <div
          className="rounded-lg border p-3"
          style={{
            backgroundColor: tokens.card,
            borderColor: tokens.border,
          }}
        >
          <p className="text-[11px]" style={{ color: tokens.textTertiary }}>
            مبلغ التأمين المستحق
          </p>
          <p className="text-lg font-bold" style={{ color: tokens.brand }}>
            {formatYER(totalCovered)}
          </p>
        </div>
      </div>

      {/* ── جدول المطالبات ── */}
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
            onClick={fetchClaims}
            className="text-xs font-medium mt-2 underline"
            style={{ color: tokens.brand }}
          >
            إعادة المحاولة
          </button>
        </div>
      ) : filtered.length === 0 ? (
        <EmptyState icon={ShieldCheck} message="لا توجد مطالبات تأمينية" />
      ) : (
        <DataTable<InsuranceClaimDto>
          keyField="id"
          data={filtered}
          columns={[
            {
              key: "insuranceCompanyName",
              label: "شركة التأمين",
              render: (r) => r.insuranceCompanyName ?? "—",
            },
            {
              key: "totalAmount",
              label: "إجمالي الفاتورة",
              render: (r) => formatYER(r.totalAmount ?? 0),
            },
            {
              key: "coveredAmount",
              label: "مبلغ التأمين",
              render: (r) => (
                <span style={{ color: tokens.brand, fontWeight: 700 }}>
                  {formatYER(r.coveredAmount ?? 0)}
                </span>
              ),
            },
            {
              key: "patientCoPay",
              label: "تحمل المريض",
              render: (r) => (
                <span style={{ color: tokens.warningBorder, fontWeight: 700 }}>
                  {formatYER(r.patientCoPay ?? 0)}
                </span>
              ),
            },
            {
              key: "status",
              label: "الحالة",
              render: (r) => {
                const cfg = CLAIM_STATUS_MAP[r.status] ?? {
                  bg: tokens.cardHover,
                  text: tokens.textSecondary,
                  label: r.status,
                };
                return (
                  <span
                    className="inline-flex text-[11px] font-semibold px-2 py-0.5 rounded-full"
                    style={{
                      backgroundColor: cfg.bg,
                      color: cfg.text,
                    }}
                  >
                    {cfg.label}
                  </span>
                );
              },
            },
            {
              key: "actions",
              label: "إجراء",
              render: (r) =>
                r.status === "Pending" || r.status === "Approved" ? (
                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      setSettleClaimId(r.id);
                    }}
                    className="px-3 py-1 rounded-md text-xs font-semibold text-white"
                    style={{ backgroundColor: tokens.successBorder }}
                    onMouseEnter={(e) => {
                      e.currentTarget.style.opacity = "0.85";
                    }}
                    onMouseLeave={(e) => {
                      e.currentTarget.style.opacity = "1";
                    }}
                  >
                    تسوية
                  </button>
                ) : (
                  "—"
                ),
            },
          ]}
        />
      )}

      {/* ── نافذة تسوية المطالبة ── */}
      <Modal
        open={!!settleClaimId}
        onClose={() => {
          setSettleClaimId(null);
          setSettleNotes("");
        }}
        title="تسوية المطالبة التأمينية"
      >
        <div className="space-y-4">
          <div
            className="rounded-lg p-3 text-center"
            style={{ backgroundColor: tokens.successBg }}
          >
            <p className="text-xs" style={{ color: tokens.textSecondary }}>
              تأكيد استلام مبلغ التأمين من الشركة
            </p>
          </div>

          <div>
            <label style={labelStyle}>ملاحظات مرجعية (اختياري)</label>
            <input
              type="text"
              value={settleNotes}
              onChange={(e) => setSettleNotes(e.target.value)}
              placeholder="رقم الشيك، رقم الحوالة..."
              style={inputStyle}
            />
          </div>

          <div className="flex items-center gap-3 pt-2">
            <button
              type="button"
              onClick={() => {
                setSettleClaimId(null);
                setSettleNotes("");
              }}
              style={btnGhost}
            >
              إلغاء
            </button>
            <button
              onClick={handleSettle}
              disabled={isSettling}
              style={{
                ...btnPrimary,
                backgroundColor: tokens.successBorder,
                opacity: isSettling ? 0.5 : 1,
                cursor: isSettling ? "not-allowed" : "pointer",
              }}
            >
              {isSettling ? (
                <span className="flex items-center gap-2">
                  <Loader2 className="w-4 h-4 animate-spin" />
                  جاري التسوية...
                </span>
              ) : (
                "تأكيد التسوية"
              )}
            </button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
