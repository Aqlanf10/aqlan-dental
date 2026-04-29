import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import api from "@/lib/api";
import type { PaginatedResponse } from "@/types/api";

// ─── Types ──────────────────────────────────────────────────────────────────────

export interface AuditLogEntry {
  id: string;
  action: string;
  resource: string;
  resourceId?: string;
  userId: string;
  userName?: string;
  ipAddress?: string;
  timestamp: string;
  details?: string;
}

export interface AuditLogFilters {
  page?: number;
  pageSize?: number;
  action?: string;
  resource?: string;
  userId?: string;
  from?: string;
  to?: string;
  search?: string;
}

export interface BranchDto {
  id: string;
  name: string;
  address?: string;
  phone?: string;
  isMain: boolean;
  userCount?: number;
  createdAt: string;
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

export interface RolePermissionDto {
  id: string;
  role: string;
  resource: string;
  canView: boolean;
  canCreate: boolean;
  canEdit: boolean;
  canDelete: boolean;
  canExport: boolean;
  canApprove: boolean;
}

export interface CreateRolePermissionRequest {
  role: string;
  resource: string;
  canView?: boolean;
  canCreate?: boolean;
  canEdit?: boolean;
  canDelete?: boolean;
  canExport?: boolean;
  canApprove?: boolean;
}

export interface UpdateRolePermissionRequest {
  canView?: boolean;
  canCreate?: boolean;
  canEdit?: boolean;
  canDelete?: boolean;
  canExport?: boolean;
  canApprove?: boolean;
}

export interface BulkUpdateRolePermissionItem {
  id: string;
  canView?: boolean;
  canCreate?: boolean;
  canEdit?: boolean;
  canDelete?: boolean;
  canExport?: boolean;
  canApprove?: boolean;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

// ─── Audit Log Hooks ────────────────────────────────────────────────────────────

/** Hook: Fetch paginated audit logs */
export function useAuditLogs(params: AuditLogFilters = {}) {
  return useQuery({
    queryKey: ["audit-logs", params],
    queryFn: async () => {
      const qs = new URLSearchParams();
      if (params.page) qs.set("page", String(params.page));
      if (params.pageSize) qs.set("pageSize", String(params.pageSize));
      if (params.action) qs.set("action", params.action);
      if (params.resource) qs.set("resource", params.resource);
      if (params.userId) qs.set("userId", params.userId);
      if (params.from) qs.set("from", params.from);
      if (params.to) qs.set("to", params.to);
      if (params.search) qs.set("search", params.search);

      const { data } = await api.get<PaginatedResponse<AuditLogEntry>>(
        `/api/audit-logs?${qs.toString()}`
      );
      return data;
    },
    staleTime: 15_000,
  });
}

/** Hook: Export audit logs as CSV */
export function useExportAuditLogs(params: Omit<AuditLogFilters, "page" | "pageSize"> = {}) {
  return useMutation({
    mutationFn: async (exportParams?: Omit<AuditLogFilters, "page" | "pageSize">) => {
      const p = exportParams ?? params;
      const qs = new URLSearchParams();
      if (p.action) qs.set("action", p.action);
      if (p.resource) qs.set("resource", p.resource);
      if (p.userId) qs.set("userId", p.userId);
      if (p.from) qs.set("from", p.from);
      if (p.to) qs.set("to", p.to);
      if (p.search) qs.set("search", p.search);
      qs.set("export", "csv");

      const { data } = await api.get<string>(`/api/audit-logs?${qs.toString()}`, {
        responseType: "blob" as never,
      });
      return data;
    },
  });
}

// ─── Branch Hooks ───────────────────────────────────────────────────────────────

/** Hook: Fetch all branches */
export function useBranches() {
  return useQuery({
    queryKey: ["branches"],
    queryFn: async () => {
      const { data } = await api.get<BranchDto[]>("/api/branches");
      return data;
    },
    staleTime: 60_000,
  });
}

/** Hook: Create branch */
export function useCreateBranch() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (req: CreateBranchRequest) => {
      const { data } = await api.post<BranchDto>("/api/branches", req);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["branches"] });
    },
  });
}

/** Hook: Update branch */
export function useUpdateBranch() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, ...req }: UpdateBranchRequest & { id: string }) => {
      const { data } = await api.put<BranchDto>(`/api/branches/${id}`, req);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["branches"] });
    },
  });
}

/** Hook: Delete branch */
export function useDeleteBranch() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      await api.delete(`/api/branches/${id}`);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["branches"] });
    },
  });
}

// ─── Role Permission Hooks ──────────────────────────────────────────────────────

/** Hook: Fetch role permissions */
export function useRolePermissions(role?: string) {
  return useQuery({
    queryKey: ["role-permissions", role],
    queryFn: async () => {
      const qs = new URLSearchParams();
      if (role) qs.set("role", role);
      const { data } = await api.get<RolePermissionDto[]>(
        `/api/role-permissions?${qs.toString()}`
      );
      return data;
    },
    staleTime: 60_000,
  });
}

/** Hook: Update single role permission */
export function useUpdateRolePermission() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, ...req }: UpdateRolePermissionRequest & { id: string }) => {
      const { data } = await api.put<RolePermissionDto>(`/api/role-permissions/${id}`, req);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["role-permissions"] });
    },
  });
}

/** Hook: Bulk update role permissions */
export function useBulkUpdateRolePermissions() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (items: BulkUpdateRolePermissionItem[]) => {
      const { data } = await api.put<RolePermissionDto[]>("/api/role-permissions/bulk", { items });
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["role-permissions"] });
    },
  });
}

/** Hook: Create role permission */
export function useCreateRolePermission() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (req: CreateRolePermissionRequest) => {
      const { data } = await api.post<RolePermissionDto>("/api/role-permissions", req);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["role-permissions"] });
    },
  });
}

/** Hook: Delete role permission */
export function useDeleteRolePermission() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      await api.delete(`/api/role-permissions/${id}`);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["role-permissions"] });
    },
  });
}

// ─── Change Password Hook ───────────────────────────────────────────────────────

/** Hook: Change current user password */
export function useChangePassword() {
  return useMutation({
    mutationFn: async (req: ChangePasswordRequest) => {
      const { data } = await api.post<{ message: string }>(
        "/api/users/me/change-password",
        req
      );
      return data;
    },
  });
}
