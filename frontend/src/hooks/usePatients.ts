import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import api from "@/lib/api";
import type { PatientListItem, PatientProfile, CreatePatientRequest } from "@/types/patient";
import type { PatientPortalCredentials, CreatePortalAccountResponse, PatientPasswordResetResponse } from "@/types/patientPortal";
import type { PaginatedResponse } from "@/types/api";

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

/** Hook: Restore archived patient */
export function useRestorePatient() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: string) => {
      await api.post(`/api/patients/${id}/restore`);
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

/** Hook: Check for duplicate patients by phone/whatsApp/name+DOB */
export function useCheckDuplicate() {
  return useMutation({
    mutationFn: async (params: {
      phone?: string;
      whatsApp?: string;
      firstName?: string;
      lastName?: string;
      dateOfBirth?: string;
      excludeId?: string;
    }) => {
      const qs = new URLSearchParams();
      if (params.phone) qs.set("phone", params.phone);
      if (params.whatsApp) qs.set("whatsApp", params.whatsApp);
      if (params.firstName) qs.set("firstName", params.firstName);
      if (params.lastName) qs.set("lastName", params.lastName);
      if (params.dateOfBirth) qs.set("dateOfBirth", params.dateOfBirth);
      if (params.excludeId) qs.set("excludeId", params.excludeId);
      const { data } = await api.get(`/api/patients/check-duplicate?${qs}`);
      return data as { isDuplicate: boolean; matches: Array<{ id: string; patientNumber: string; fullName: string; phone?: string; matchType: string }> };
    },
  });
}

/** Hook: Fetch patient summary */
export function usePatientSummary(id: string | null) {
  return useQuery({
    queryKey: ["patient-summary", id],
    queryFn: async () => {
      const { data } = await api.get(`/api/patients/${id}/summary`);
      return data;
    },
    enabled: !!id,
    staleTime: 30_000,
  });
}

// ── Portal Access Hooks ──────────────────────────────────────────────────

/** Hook: Fetch patient portal credentials (staff only) */
export function usePatientPortalCredentials(patientId: string | null) {
  return useQuery({
    queryKey: ["patient-portal-credentials", patientId],
    queryFn: async () => {
      const { data } = await api.get<PatientPortalCredentials>(`/api/portal/credentials/${patientId}`);
      return data;
    },
    enabled: !!patientId,
    staleTime: 30_000,
  });
}

/** Hook: Create portal account for a patient */
export function useCreatePortalAccount() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (patientId: string) => {
      const { data } = await api.post<CreatePortalAccountResponse>(`/api/portal/credentials/${patientId}/create`);
      return data;
    },
    onSuccess: (_data, patientId) => {
      queryClient.invalidateQueries({ queryKey: ["patient-portal-credentials", patientId] });
    },
  });
}

/** Hook: Reset portal account password */
export function useResetPortalPassword() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (patientId: string) => {
      const { data } = await api.post<PatientPasswordResetResponse>(`/api/portal/credentials/${patientId}/reset-password`);
      return data;
    },
    onSuccess: (_data, patientId) => {
      queryClient.invalidateQueries({ queryKey: ["patient-portal-credentials", patientId] });
    },
  });
}
