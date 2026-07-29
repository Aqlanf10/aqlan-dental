import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import api from "@/lib/api";

export interface BranchDto {
  id: string;
  name: string;
  address?: string;
  phone?: string;
  isMain: boolean;
  isActive: boolean;
  doctorsCount?: number;
  patientsCount?: number;
  createdAt?: string;
  updatedAt?: string;
}

export interface CreateBranchRequest {
  name: string;
  address?: string;
  phone?: string;
  isMain?: boolean;
}

export interface UpdateBranchRequest {
  name?: string;
  address?: string;
  phone?: string;
  isMain?: boolean;
}

/** Hook: Fetch branches with optional status filter */
export function useBranches(status?: string) {
  return useQuery({
    queryKey: ["branches", status],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (status) params.set("status", status);
      const { data } = await api.get<BranchDto[]>(
        `/api/branches${params.toString() ? `?${params}` : ""}`
      );
      return data;
    },
    staleTime: 30_000,
  });
}

/** Hook: Fetch a single branch */
export function useBranch(id: string | undefined) {
  return useQuery({
    queryKey: ["branches", id],
    queryFn: async () => {
      if (!id) return null;
      const { data } = await api.get<BranchDto>(`/api/branches/${id}`);
      return data;
    },
    enabled: !!id,
    staleTime: 30_000,
  });
}

/** Mutation: Create branch */
export function useCreateBranch() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: CreateBranchRequest) => {
      const { data } = await api.post<BranchDto>("/api/branches", req);
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["branches"] });
    },
  });
}

/** Mutation: Update branch */
export function useUpdateBranch() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, ...req }: UpdateBranchRequest & { id: string }) => {
      const { data } = await api.put<BranchDto>(`/api/branches/${id}`, req);
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["branches"] });
    },
  });
}

/** Mutation: Toggle branch status */
export function useToggleBranchStatus() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const { data } = await api.put(`/api/branches/${id}/status`);
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["branches"] });
    },
  });
}

/** Mutation: Delete branch (soft-delete) */
export function useDeleteBranch() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const { data } = await api.delete(`/api/branches/${id}`);
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["branches"] });
    },
  });
}
