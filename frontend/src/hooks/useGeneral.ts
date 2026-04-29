import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import api from "@/lib/api";
import type {
  DentalChart,
  ToothCondition,
  GeneralTreatment,
  CreateGeneralTreatmentRequest,
  PerioAssessment,
  CreatePerioAssessmentRequest,
  UpdatePerioAssessmentRequest,
} from "@/types/dental";

// ─── Dental Chart Hooks ────────────────────────────────────────────────────────

/** Hook: Fetch dental chart for a patient */
export function useDentalChart(patientId: string | null) {
  return useQuery({
    queryKey: ["dental-chart", patientId],
    queryFn: async () => {
      const { data } = await api.get<DentalChart>(`/api/dental-chart/${patientId}`);
      return data;
    },
    enabled: !!patientId,
    staleTime: 30_000,
  });
}

/** Hook: Update tooth condition */
export function useUpdateTooth() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({
      patientId,
      tooth,
    }: {
      patientId: string;
      tooth: ToothCondition;
    }) => {
      const { data } = await api.put<ToothCondition>(
        `/api/dental-chart/${patientId}/teeth`,
        tooth
      );
      return data;
    },
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({
        queryKey: ["dental-chart", variables.patientId],
      });
    },
  });
}

// ─── General Treatment Hooks ───────────────────────────────────────────────────

/** Hook: Fetch general treatments for a patient */
export function useGeneralTreatments(patientId: string | null) {
  return useQuery({
    queryKey: ["general-treatments", patientId],
    queryFn: async () => {
      const { data } = await api.get<GeneralTreatment[]>(
        `/api/general-treatments/${patientId}`
      );
      return data;
    },
    enabled: !!patientId,
    staleTime: 30_000,
  });
}

/** Hook: Fetch recent general treatments */
export function useRecentTreatments(limit: number = 50) {
  return useQuery({
    queryKey: ["recent-treatments", limit],
    queryFn: async () => {
      const { data } = await api.get<GeneralTreatment[]>(
        `/api/general/recent-treatments?limit=${limit}`
      );
      return data;
    },
    staleTime: 30_000,
  });
}

/** Hook: Create general treatment */
export function useCreateTreatment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (treatment: CreateGeneralTreatmentRequest) => {
      const { data } = await api.post<GeneralTreatment>(
        "/api/general-treatments",
        treatment
      );
      return data;
    },
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({
        queryKey: ["general-treatments", variables.patientId],
      });
      queryClient.invalidateQueries({ queryKey: ["recent-treatments"] });
    },
  });
}

/** Hook: Update general treatment */
export function useUpdateTreatment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({
      id,
      ...treatment
    }: Partial<GeneralTreatment> & { id: string }) => {
      const { data } = await api.put<GeneralTreatment>(
        `/api/general-treatments/${id}`,
        treatment
      );
      return data;
    },
    onSuccess: (data) => {
      queryClient.invalidateQueries({
        queryKey: ["general-treatments", data.patientId],
      });
      queryClient.invalidateQueries({ queryKey: ["recent-treatments"] });
    },
  });
}

/** Hook: Delete general treatment */
export function useDeleteTreatment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      await api.delete(`/api/general-treatments/${id}`);
      return id;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["general-treatments"] });
      queryClient.invalidateQueries({ queryKey: ["recent-treatments"] });
    },
  });
}

// ─── Perio Assessment Hooks ────────────────────────────────────────────────────

/** Hook: Fetch latest perio assessment for a patient */
export function usePerioAssessment(patientId: string | null) {
  return useQuery({
    queryKey: ["perio-assessment", patientId],
    queryFn: async () => {
      const { data } = await api.get<PerioAssessment>(
        `/api/perio/${patientId}`
      );
      return data;
    },
    enabled: !!patientId,
    staleTime: 30_000,
  });
}

/** Hook: Fetch all perio assessments for a patient */
export function usePerioHistory(patientId: string | null) {
  return useQuery({
    queryKey: ["perio-history", patientId],
    queryFn: async () => {
      const { data } = await api.get<PerioAssessment[]>(
        `/api/perio/${patientId}/history`
      );
      return data;
    },
    enabled: !!patientId,
    staleTime: 30_000,
  });
}

/** Hook: Create perio assessment */
export function useCreatePerioAssessment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (assessment: CreatePerioAssessmentRequest) => {
      const { data } = await api.post<PerioAssessment>(
        `/api/perio/${assessment.patientId}`,
        assessment
      );
      return data;
    },
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({
        queryKey: ["perio-assessment", variables.patientId],
      });
      queryClient.invalidateQueries({
        queryKey: ["perio-history", variables.patientId],
      });
    },
  });
}

/** Hook: Update perio assessment */
export function useUpdatePerioAssessment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (assessment: UpdatePerioAssessmentRequest) => {
      const { data } = await api.put<PerioAssessment>(
        `/api/perio/${assessment.id}`,
        assessment
      );
      return data;
    },
    onSuccess: (data) => {
      queryClient.invalidateQueries({
        queryKey: ["perio-assessment", data.patientId],
      });
      queryClient.invalidateQueries({
        queryKey: ["perio-history", data.patientId],
      });
    },
  });
}

/** Hook: Delete perio assessment */
export function useDeletePerioAssessment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, patientId }: { id: string; patientId: string }) => {
      await api.delete(`/api/perio/${id}`);
      return { id, patientId };
    },
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({
        queryKey: ["perio-assessment", variables.patientId],
      });
      queryClient.invalidateQueries({
        queryKey: ["perio-history", variables.patientId],
      });
    },
  });
}
