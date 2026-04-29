import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import api from "@/lib/api";
import type {
  Prescription,
  PrescriptionListItem,
  CreatePrescriptionRequest,
} from "@/types/prescription";

/** Hook: Fetch prescriptions list (optionally filtered by patient) */
export function usePrescriptions(patientId?: string) {
  return useQuery({
    queryKey: ["prescriptions", patientId],
    queryFn: async () => {
      const url = patientId
        ? `/api/prescriptions?patientId=${patientId}`
        : "/api/prescriptions";
      const { data } = await api.get<PrescriptionListItem[]>(url);
      return data;
    },
    staleTime: 30_000,
  });
}

/** Hook: Fetch single prescription */
export function usePrescription(id: string | null) {
  return useQuery({
    queryKey: ["prescription", id],
    queryFn: async () => {
      const { data } = await api.get<Prescription>(`/api/prescriptions/${id}`);
      return data;
    },
    enabled: !!id,
    staleTime: 30_000,
  });
}

/** Hook: Create prescription */
export function useCreatePrescription() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (prescription: CreatePrescriptionRequest) => {
      const { data } = await api.post<{ id: string }>(
        "/api/prescriptions",
        prescription
      );
      return data;
    },
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({
        queryKey: ["prescriptions", variables.patientId],
      });
      queryClient.invalidateQueries({ queryKey: ["prescriptions"] });
    },
  });
}

/** Hook: Delete prescription */
export function useDeletePrescription() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, patientId }: { id: string; patientId?: string }) => {
      await api.delete(`/api/prescriptions/${id}`);
      return { id, patientId };
    },
    onSuccess: (_data, variables) => {
      if (variables.patientId) {
        queryClient.invalidateQueries({
          queryKey: ["prescriptions", variables.patientId],
        });
      }
      queryClient.invalidateQueries({ queryKey: ["prescriptions"] });
    },
  });
}
