import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type {
  LineItemCommission, UpdateLineItemCommissionRequest,
  CommissionReport, DoctorCommissionPayment,
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
  paymentStatus?: string;
  serviceCategory?: string;
} | null) {
  return useQuery({
    queryKey: ["commissions", "report", params],
    queryFn: async () => {
      const p = new URLSearchParams({ from: params!.from, to: params!.to });
      if (params?.doctorId)         p.append("doctorId", params.doctorId);
      if (params?.branchId)         p.append("branchId", params.branchId);
      if (params?.commissionStatus) p.append("commissionStatus", params.commissionStatus);
      if (params?.paymentStatus)    p.append("paymentStatus", params.paymentStatus);
      if (params?.serviceCategory)  p.append("serviceCategory", params.serviceCategory);
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
