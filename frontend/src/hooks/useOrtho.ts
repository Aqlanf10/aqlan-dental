import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import api from "@/lib/api";
import type { OrthoCase, CreateOrthoCaseRequest, CreateOrthoVisitRequest, ModelAnalysisDto, BoltonResult, BoltonCalculationRequest, TreatmentPlanDto, TreatmentPlanListDto } from "@/types/ortho";

interface OrthoCaseFilters {
  page?: number;
  pageSize?: number;
  doctorId?: string;
  status?: string;
  search?: string;
  patientId?: string;
}

/** Hook: Fetch ortho cases list */
export function useOrthoCases(filters: OrthoCaseFilters = {}) {
  return useQuery({
    queryKey: ["ortho-cases", filters],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (filters.page) params.set("page", String(filters.page));
      if (filters.pageSize) params.set("pageSize", String(filters.pageSize));
      if (filters.doctorId) params.set("doctorId", filters.doctorId);
      if (filters.status) params.set("status", filters.status);
      if (filters.search) params.set("search", filters.search);
      if (filters.patientId) params.set("patientId", filters.patientId);

      const { data } = await api.get<OrthoCase[]>(`/api/ortho-cases?${params.toString()}`);
      return data;
    },
    staleTime: 30_000,
  });
}

/** Hook: Fetch single ortho case */
export function useOrthoCase(id: string | null) {
  return useQuery({
    queryKey: ["ortho-case", id],
    queryFn: async () => {
      const { data } = await api.get<OrthoCase>(`/api/ortho-cases/${id}`);
      return data;
    },
    enabled: !!id,
    staleTime: 30_000,
  });
}

/** Hook: Create ortho case */
export function useCreateOrthoCase() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (orthoCase: CreateOrthoCaseRequest) => {
      const { data } = await api.post<{ id: string }>("/api/ortho-cases", orthoCase);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["ortho-cases"] });
    },
  });
}

/** Hook: Add ortho visit */
export function useAddOrthoVisit() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ caseId, visit }: { caseId: string; visit: CreateOrthoVisitRequest }) => {
      const { data } = await api.post(`/api/ortho-cases/${caseId}/visits`, visit);
      return data;
    },
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: ["ortho-case", variables.caseId] });
    },
  });
}

/** Hook: Update treatment stage status */
export function useUpdateTreatmentStage() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      caseId,
      stageId,
      status,
    }: {
      caseId: string;
      stageId: string;
      status: string;
    }) => {
      const { data } = await api.put(`/api/ortho-cases/${caseId}/stages/${stageId}`, { status });
      return data;
    },
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: ["ortho-case", variables.caseId] });
    },
  });
}

/** Hook: Save clinical exam */
export function useSaveClinicalExam() {
  return useMutation({
    mutationFn: async ({
      caseId,
      exam,
    }: {
      caseId: string;
      exam: Record<string, unknown>;
    }) => {
      const { data } = await api.put(`/api/ortho-cases/${caseId}/clinical-exam`, exam);
      return data;
    },
  });
}

/** Hook: Save treatment plan */
export function useSaveTreatmentPlan() {
  return useMutation({
    mutationFn: async ({
      caseId,
      plan,
    }: {
      caseId: string;
      plan: Record<string, unknown>;
    }) => {
      const { data } = await api.put(`/api/ortho-cases/${caseId}/treatment-plan`, plan);
      return data;
    },
  });
}

/** Hook: Save extraction decision */
export function useSaveExtractionDecision() {
  return useMutation({
    mutationFn: async ({
      caseId,
      decision,
      doctorNotes,
    }: {
      caseId: string;
      decision: string;
      doctorNotes?: string;
    }) => {
      const { data } = await api.put(`/api/ortho-cases/${caseId}/extraction-decision`, {
        decision,
        doctorNotes,
      });
      return data;
    },
  });
}

/** Hook: Create retention record */
export function useCreateRetention() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      caseId,
      retention,
    }: {
      caseId: string;
      retention: {
        debondDate?: string;
        upperRetainer?: string;
        lowerRetainer?: string;
        instructions?: string;
      };
    }) => {
      const { data } = await api.post(`/api/ortho-cases/${caseId}/retention`, retention);
      return data;
    },
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: ["ortho-case", variables.caseId] });
    },
  });
}

/** Hook: Add retention visit */
export function useAddRetentionVisit() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      caseId,
      visit,
    }: {
      caseId: string;
      visit: {
        visitDate: string;
        period?: string;
        toothStability?: string;
        retainerStatus?: string;
        notes?: string;
      };
    }) => {
      const { data } = await api.post(`/api/ortho-cases/${caseId}/retention/visits`, visit);
      return data;
    },
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: ["ortho-case", variables.caseId] });
    },
  });
}

// ─── Model Analysis Hooks ──────────────────────────────────────────────────────

/** Hook: Fetch model analysis for an ortho case */
export function useModelAnalysis(caseId: string | null) {
  return useQuery({
    queryKey: ["ortho-case", caseId, "model-analysis"],
    queryFn: async () => {
      const { data } = await api.get<ModelAnalysisDto>(`/api/ortho-cases/${caseId}/model-analysis`);
      return data;
    },
    enabled: !!caseId,
    staleTime: 30_000,
  });
}

/** Hook: Calculate Bolton ratios */
export function useCalculateBolton() {
  return useMutation({
    mutationFn: async ({ caseId, widths }: { caseId: string; widths: BoltonCalculationRequest }) => {
      const { data } = await api.post<BoltonResult>(`/api/ortho-cases/${caseId}/bolton-calculation`, widths);
      return data;
    },
  });
}

/** Hook: Save model analysis */
export function useSaveModelAnalysis() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ caseId, analysis }: { caseId: string; analysis: Partial<ModelAnalysisDto> }) => {
      const { data } = await api.put(`/api/ortho-cases/${caseId}/model-analysis`, analysis);
      return data;
    },
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: ["ortho-case", variables.caseId, "model-analysis"] });
    },
  });
}

// ─── Treatment Plan A/B Hooks ──────────────────────────────────────────────────

/** Hook: Fetch all treatment plans (A + B) */
export function useTreatmentPlans(caseId: string | null) {
  return useQuery({
    queryKey: ["ortho-case", caseId, "treatment-plans"],
    queryFn: async () => {
      const { data } = await api.get<TreatmentPlanListDto>(`/api/ortho-cases/${caseId}/treatment-plans`);
      return data;
    },
    enabled: !!caseId,
    staleTime: 30_000,
  });
}

/** Hook: Save a treatment plan (supports PlanLabel) */
export function useSaveTreatmentPlanAB() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ caseId, plan }: { caseId: string; plan: Partial<TreatmentPlanDto> }) => {
      const { data } = await api.put<TreatmentPlanDto>(`/api/ortho-cases/${caseId}/treatment-plan`, plan);
      return data;
    },
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: ["ortho-case", variables.caseId, "treatment-plans"] });
      queryClient.invalidateQueries({ queryKey: ["ortho-case", variables.caseId] });
    },
  });
}

/** Hook: Select active treatment plan */
export function useSelectTreatmentPlan() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ caseId, planId }: { caseId: string; planId: string }) => {
      const { data } = await api.put(`/api/ortho-cases/${caseId}/treatment-plan/${planId}/select`);
      return data;
    },
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: ["ortho-case", variables.caseId, "treatment-plans"] });
      queryClient.invalidateQueries({ queryKey: ["ortho-case", variables.caseId] });
    },
  });
}
