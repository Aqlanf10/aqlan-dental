import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import api from "@/lib/api";
import type { PatientListItem, PatientProfile, CreatePatientRequest } from "@/types/patient";

interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

interface PatientFilters {
  search?: string;
  page?: number;
  pageSize?: number;
  gender?: string;
  doctorId?: string;
  isActive?: boolean;
}

/** Hook: Fetch paginated patients list */
export function usePatients(filters: PatientFilters = {}) {
  return useQuery({
    queryKey: ["patients", filters],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (filters.search) params.set("search", filters.search);
      if (filters.page) params.set("page", String(filters.page));
      if (filters.pageSize) params.set("pageSize", String(filters.pageSize));
      if (filters.gender) params.set("gender", filters.gender);
      if (filters.doctorId) params.set("doctorId", filters.doctorId);
      if (filters.isActive !== undefined) params.set("isActive", String(filters.isActive));

      const { data } = await api.get<PaginatedResponse<PatientListItem>>(
        `/api/patients?${params.toString()}`
      );
      return data;
    },
    staleTime: 30_000,
  });
}

/** Hook: Fetch single patient profile */
export function usePatient(id: string | null) {
  return useQuery({
    queryKey: ["patient", id],
    queryFn: async () => {
      const { data } = await api.get<PatientProfile>(`/api/patients/${id}`);
      return data;
    },
    enabled: !!id,
    staleTime: 30_000,
  });
}

/** Hook: Create patient */
export function useCreatePatient() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (patient: CreatePatientRequest) => {
      const { data } = await api.post<PatientProfile>("/api/patients", patient);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["patients"] });
    },
  });
}

/** Hook: Update patient */
export function useUpdatePatient() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ id, ...patient }: CreatePatientRequest & { id: string }) => {
      const { data } = await api.put<PatientProfile>(`/api/patients/${id}`, patient);
      return data;
    },
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: ["patients"] });
      queryClient.invalidateQueries({ queryKey: ["patient", variables.id] });
    },
  });
}

/** Hook: Delete (archive) patient */
export function useDeletePatient() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: string) => {
      await api.delete(`/api/patients/${id}`);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["patients"] });
    },
  });
}

/** Hook: Fetch patient timeline */
export function usePatientTimeline(id: string | null) {
  return useQuery({
    queryKey: ["patient-timeline", id],
    queryFn: async () => {
      const { data } = await api.get(`/api/patients/${id}/timeline`);
      return data;
    },
    enabled: !!id,
    staleTime: 30_000,
  });
}
