import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import api from "@/lib/api";
import type {
  SurgeryCase,
  CreateSurgeryCaseRequest,
  PreopReport,
  OperativeReport,
  PostopRecord,
  HospitalReferral,
  CreateHospitalReferralRequest,
  SurgeryCaseFilters,
} from "@/types/surgery";

// ─── Surgery Case Hooks ────────────────────────────────────────────────────────

/** Hook: Fetch surgery cases list (paginated) */
export function useSurgeryCases(filters: SurgeryCaseFilters = {}) {
  return useQuery({
    queryKey: ["surgery-cases", filters],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (filters.page) params.set("page", String(filters.page));
      if (filters.pageSize) params.set("pageSize", String(filters.pageSize));
      if (filters.doctorId) params.set("doctorId", filters.doctorId);
      if (filters.status) params.set("status", filters.status);
      if (filters.search) params.set("search", filters.search);
      if (filters.patientId) params.set("patientId", filters.patientId);
      const { data } = await api.get<{ data: SurgeryCase[]; total: number }>(
        `/api/surgery-cases?${params.toString()}`
      );
      return data;
    },
    staleTime: 30_000,
  });
}

/** Hook: Fetch single surgery case */
export function useSurgeryCase(id: string | null) {
  return useQuery({
    queryKey: ["surgery-case", id],
    queryFn: async () => {
      const { data } = await api.get<SurgeryCase>(`/api/surgery-cases/${id}`);
      return data;
    },
    enabled: !!id,
    staleTime: 30_000,
  });
}

/** Hook: Create surgery case */
export function useCreateSurgeryCase() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (surgeryCase: CreateSurgeryCaseRequest) => {
      const { data } = await api.post<{ id: string }>(
        "/api/surgery-cases",
        surgeryCase
      );
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["surgery-cases"] });
    },
  });
}

/** Hook: Update surgery case status */
export function useUpdateSurgeryStatus() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({
      caseId,
      status,
    }: {
      caseId: string;
      status: string;
    }) => {
      const { data } = await api.put(`/api/surgery-cases/${caseId}/status`, {
        status,
      });
      return data;
    },
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({
        queryKey: ["surgery-case", variables.caseId],
      });
      queryClient.invalidateQueries({ queryKey: ["surgery-cases"] });
    },
  });
}

// ─── Pre-op Report Hooks ───────────────────────────────────────────────────────

/** Hook: Fetch pre-op report */
export function usePreopReport(caseId: string | null) {
  return useQuery({
    queryKey: ["surgery-case", caseId, "preop"],
    queryFn: async () => {
      const { data } = await api.get<PreopReport | null>(
        `/api/surgery-cases/${caseId}/preop`
      );
      return data;
    },
    enabled: !!caseId,
    staleTime: 30_000,
  });
}

/** Hook: Save pre-op report */
export function useSavePreopReport() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({
      caseId,
      report,
    }: {
      caseId: string;
      report: Partial<PreopReport>;
    }) => {
      const { data } = await api.put(
        `/api/surgery-cases/${caseId}/preop`,
        report
      );
      return data;
    },
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({
        queryKey: ["surgery-case", variables.caseId, "preop"],
      });
    },
  });
}

// ─── Operative Report Hooks ────────────────────────────────────────────────────

/** Hook: Fetch operative report */
export function useOperativeReport(caseId: string | null) {
  return useQuery({
    queryKey: ["surgery-case", caseId, "operative"],
    queryFn: async () => {
      const { data } = await api.get<OperativeReport | null>(
        `/api/surgery-cases/${caseId}/operative-report`
      );
      return data;
    },
    enabled: !!caseId,
    staleTime: 30_000,
  });
}

/** Hook: Save operative report */
export function useSaveOperativeReport() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({
      caseId,
      report,
    }: {
      caseId: string;
      report: Partial<OperativeReport>;
    }) => {
      const { data } = await api.put(
        `/api/surgery-cases/${caseId}/operative-report`,
        report
      );
      return data;
    },
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({
        queryKey: ["surgery-case", variables.caseId, "operative"],
      });
    },
  });
}

/** Hook: Approve operative report */
export function useApproveOperativeReport() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({
      caseId,
    }: {
      caseId: string;
    }) => {
      const { data } = await api.put(
        `/api/surgery-cases/${caseId}/operative-report/approve`
      );
      return data;
    },
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({
        queryKey: ["surgery-case", variables.caseId, "operative"],
      });
      queryClient.invalidateQueries({
        queryKey: ["surgery-case", variables.caseId],
      });
    },
  });
}

// ─── Post-op Record Hooks ──────────────────────────────────────────────────────

/** Hook: Fetch post-op record */
export function usePostopRecord(caseId: string | null) {
  return useQuery({
    queryKey: ["surgery-case", caseId, "postop"],
    queryFn: async () => {
      const { data } = await api.get<PostopRecord | null>(
        `/api/surgery-cases/${caseId}/postop`
      );
      return data;
    },
    enabled: !!caseId,
    staleTime: 30_000,
  });
}

/** Hook: Save post-op record */
export function useSavePostopRecord() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({
      caseId,
      record,
    }: {
      caseId: string;
      record: Partial<PostopRecord>;
    }) => {
      const { data } = await api.put(
        `/api/surgery-cases/${caseId}/postop`,
        record
      );
      return data;
    },
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({
        queryKey: ["surgery-case", variables.caseId, "postop"],
      });
    },
  });
}

// ─── Hospital Referral Hooks ───────────────────────────────────────────────────

/** Hook: Fetch hospital referrals for a surgery case */
export function useHospitalReferrals(caseId: string | null) {
  return useQuery({
    queryKey: ["surgery-case", caseId, "referrals"],
    queryFn: async () => {
      const { data } = await api.get<HospitalReferral[]>(
        `/api/surgery-cases/${caseId}/hospital-referrals`
      );
      return data;
    },
    enabled: !!caseId,
    staleTime: 30_000,
  });
}

/** Hook: Create hospital referral */
export function useCreateHospitalReferral() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (referral: CreateHospitalReferralRequest) => {
      const { data } = await api.post<HospitalReferral>(
        `/api/surgery-cases/${referral.surgeryCaseId}/hospital-referrals`,
        referral
      );
      return data;
    },
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({
        queryKey: ["surgery-case", variables.surgeryCaseId, "referrals"],
      });
    },
  });
}

/** Hook: Update hospital referral status */
export function useUpdateHospitalReferralStatus() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({
      referralId,
      status,
      notes,
    }: {
      referralId: string;
      status: string;
      notes?: string;
    }) => {
      const { data } = await api.put<HospitalReferral>(
        `/api/hospital-referrals/${referralId}/status`,
        { status, notes }
      );
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["surgery-case"] });
    },
  });
}
