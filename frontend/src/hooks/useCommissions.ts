import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import api from "@/lib/api";
import type {
  LineItemCommission, UpdateLineItemCommissionRequest,
  CommissionReport, DoctorCommissionPayment,
  CommissionAdjustment, CommissionAdjustmentStatus,
  CommissionResyncResult, DoctorSettlementSummary,
} from "@/types/commission";

export function useInvoiceCommissions(invoiceId: string | undefined) {
  return useQuery({
    queryKey: ["commissions", "invoice", invoiceId],
    queryFn: async () => {
      const { data } = await api.get<LineItemCommission[]>(`/api/commissions/invoices/${invoiceId}`);
      return data;
    },
    enabled: !!invoiceId,
  });
}

export function useLineItemCommission(lineItemId: string | undefined) {
  return useQuery({
    queryKey: ["commissions", "line-item", lineItemId],
    queryFn: async () => {
      const { data } = await api.get<LineItemCommission>(`/api/commissions/line-items/${lineItemId}`);
      return data;
    },
    enabled: !!lineItemId,
  });
}

export function useUpdateCommissionCosts() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ lineItemId, req }: { lineItemId: string; req: UpdateLineItemCommissionRequest }) => {
      const { data } = await api.patch<LineItemCommission>(`/api/commissions/line-items/${lineItemId}/costs`, req);
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["commissions"] });
    },
  });
}

export function useApproveCommission() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ lineItemId, notes }: { lineItemId: string; notes?: string }) => {
      const { data } = await api.post<LineItemCommission>(
        `/api/commissions/line-items/${lineItemId}/approve`,
        { notes }
      );
      return data;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["commissions"] }),
  });
}

export function useUnlockCommission() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (lineItemId: string) => {
      const { data } = await api.post<LineItemCommission>(
        `/api/commissions/line-items/${lineItemId}/unlock`
      );
      return data;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["commissions"] }),
  });
}

export function useAutoFillCommission() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (lineItemId: string) => {
      const { data } = await api.post<LineItemCommission>(
        `/api/commissions/line-items/${lineItemId}/auto-fill`
      );
      return data;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["commissions"] }),
  });
}

export function useCommissionReport(params: {
  from: string; to: string;
  doctorId?: string; branchId?: string;
  commissionStatus?: string;
} | null) {
  return useQuery({
    queryKey: ["commissions", "report", params],
    queryFn: async () => {
      const p = new URLSearchParams({ from: params!.from, to: params!.to });
      if (params?.doctorId)         p.append("doctorId", params.doctorId);
      if (params?.branchId)         p.append("branchId", params.branchId);
      if (params?.commissionStatus) p.append("commissionStatus", params.commissionStatus);
      const { data } = await api.get<CommissionReport>(`/api/commissions/report?${p}`);
      return data;
    },
    enabled: !!params,
  });
}

export function useCommissionPayments(doctorId?: string) {
  return useQuery({
    queryKey: ["commissions", "payments", doctorId],
    queryFn: async () => {
      const p = doctorId ? `?doctorId=${doctorId}` : "";
      const { data } = await api.get<DoctorCommissionPayment[]>(`/api/commissions/payments${p}`);
      return data;
    },
  });
}

export function useRecordCommissionPayment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: {
      doctorId: string; amount: number; paymentDate: string;
      paymentMethod?: string; referenceNumber?: string; notes?: string;
      lineItemIds?: string[];
    }) => {
      const { data } = await api.post<DoctorCommissionPayment>("/api/commissions/payments", req);
      return data;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["commissions"] }),
  });
}

// ── Commission adjustments (CORE-FIN-LAB-ADJ) ────────────────────────────────

/**
 * Correction lines raised against already-PAID commissions when the actual lab cost changed.
 * The backend scopes a non-admin caller to their own doctor record, so passing no doctorId is
 * safe for every role.
 */
export function useCommissionAdjustments(params?: {
  doctorId?: string;
  status?: CommissionAdjustmentStatus;
}) {
  return useQuery({
    queryKey: ["commissions", "adjustments", params?.doctorId ?? null, params?.status ?? null],
    queryFn: async () => {
      const p = new URLSearchParams();
      if (params?.doctorId) p.append("doctorId", params.doctorId);
      if (params?.status)   p.append("status", params.status);
      const qs = p.toString();
      const { data } = await api.get<CommissionAdjustment[]>(
        `/api/commissions/adjustments${qs ? `?${qs}` : ""}`
      );
      return data;
    },
  });
}

/** What the clinic owes one doctor right now, correction lines included. */
export function useDoctorSettlement(doctorId: string | undefined) {
  return useQuery({
    queryKey: ["commissions", "settlement", doctorId],
    queryFn: async () => {
      const { data } = await api.get<DoctorSettlementSummary>(
        `/api/commissions/doctors/${doctorId}/settlement`
      );
      return data;
    },
    enabled: !!doctorId,
  });
}

/**
 * Admin sweep over historical records. Only touches line items that already carry a lab-order
 * link — it never guesses one, so ambiguous records stay untouched and are reported back in
 * `skipped` rather than silently corrected.
 */
export function useBackfillCommissionAdjustments() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (doctorId?: string) => {
      const qs = doctorId ? `?doctorId=${doctorId}` : "";
      const { data } = await api.post<CommissionResyncResult>(
        `/api/commissions/adjustments/backfill${qs}`
      );
      return data;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["commissions"] }),
  });
}

export function useCancelCommissionAdjustment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ adjustmentId, reason }: { adjustmentId: string; reason: string }) => {
      const { data } = await api.post<CommissionAdjustment>(
        `/api/commissions/adjustments/${adjustmentId}/cancel`,
        { reason }
      );
      return data;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["commissions"] }),
  });
}
