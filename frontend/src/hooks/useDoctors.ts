import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import api from "@/lib/api";

export interface DoctorDto {
  id: string;
  name: string;
  specialty?: string;
  licenseNumber?: string;
  color?: string;
  avatarInitials?: string;
  branchId?: string;
  branchName?: string;
  userName?: string;
  userId?: string;
  isActive?: boolean;
  compensationType?: string;
  defaultCommissionPercentage?: number;
  compensationNotes?: string;
  createdAt?: string;
  updatedAt?: string;
  scheduleCount?: number;
}

export interface CreateDoctorRequest {
  name: string;
  specialty?: string;
  licenseNumber?: string;
  branchId?: string;
  color?: string;
  avatarInitials?: string;
  userId?: string;
  compensationType?: string;
  defaultCommissionPercentage?: number;
  compensationNotes?: string;
}

export interface UpdateDoctorRequest {
  name?: string;
  specialty?: string;
  licenseNumber?: string;
  branchId?: string;
  color?: string;
  avatarInitials?: string;
  compensationType?: string;
  defaultCommissionPercentage?: number;
  compensationNotes?: string;
}

/** Hook: Fetch all active doctors (backward compatible) */
export function useDoctors() {
  return useQuery({
    queryKey: ["doctors"],
    queryFn: async () => {
      const { data } = await api.get<DoctorDto[]>("/api/doctors");
      return data;
    },
    staleTime: 60_000,
  });
}

/** Hook: Fetch doctors with filters */
export function useDoctorsFiltered(status?: string, branchId?: string) {
  return useQuery({
    queryKey: ["doctors", status, branchId],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (status) params.set("status", status);
      if (branchId) params.set("branchId", branchId);
      const { data } = await api.get<DoctorDto[]>(
        `/api/doctors${params.toString() ? `?${params}` : ""}`
      );
      return data;
    },
    staleTime: 30_000,
  });
}

/** Hook: Fetch a single doctor */
export function useDoctor(id: string | undefined) {
  return useQuery({
    queryKey: ["doctors", id],
    queryFn: async () => {
      if (!id) return null;
      const { data } = await api.get<DoctorDto>(`/api/doctors/${id}`);
      return data;
    },
    enabled: !!id,
    staleTime: 30_000,
  });
}

/** Mutation: Create doctor */
export function useCreateDoctor() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: CreateDoctorRequest) => {
      const { data } = await api.post<DoctorDto>("/api/doctors", req);
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["doctors"] });
    },
  });
}

/** Mutation: Update doctor */
export function useUpdateDoctor() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, ...req }: UpdateDoctorRequest & { id: string }) => {
      const { data } = await api.put<DoctorDto>(`/api/doctors/${id}`, req);
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["doctors"] });
    },
  });
}

/** Mutation: Toggle doctor status */
export function useToggleDoctorStatus() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const { data } = await api.put(`/api/doctors/${id}/status`);
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["doctors"] });
    },
  });
}

/** Mutation: Delete doctor (soft-delete) */
export function useDeleteDoctor() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const { data } = await api.delete(`/api/doctors/${id}`);
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["doctors"] });
    },
  });
}
