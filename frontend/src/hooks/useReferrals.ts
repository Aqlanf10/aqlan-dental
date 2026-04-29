import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import api from "@/lib/api";
import type { Referral, CreateReferralRequest, ReferralFilters } from "@/types/referral";

/** Hook: Fetch referrals list (paginated) */
export function useReferrals(filters: ReferralFilters = {}) {
  return useQuery({
    queryKey: ["referrals", filters],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (filters.page) params.set("page", String(filters.page));
      if (filters.pageSize) params.set("pageSize", String(filters.pageSize));
      if (filters.status) params.set("status", filters.status);
      if (filters.patientId) params.set("patientId", filters.patientId);
      if (filters.doctorId) params.set("doctorId", filters.doctorId);
      if (filters.search) params.set("search", filters.search);

      const { data } = await api.get<{ data: Referral[]; total: number }>(
        `/api/referrals?${params.toString()}`
      );
      return data;
    },
    staleTime: 30_000,
  });
}

/** Hook: Create referral */
export function useCreateReferral() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (referral: CreateReferralRequest) => {
      const { data } = await api.post<{ id: string }>("/api/referrals", referral);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["referrals"] });
    },
  });
}

/** Hook: Accept referral */
export function useAcceptReferral() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const { data } = await api.put(`/api/referrals/${id}/accept`);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["referrals"] });
    },
  });
}

/** Hook: Complete referral */
export function useCompleteReferral() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const { data } = await api.put(`/api/referrals/${id}/complete`);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["referrals"] });
    },
  });
}

/** Hook: Delete referral */
export function useDeleteReferral() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      await api.delete(`/api/referrals/${id}`);
      return id;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["referrals"] });
    },
  });
}
