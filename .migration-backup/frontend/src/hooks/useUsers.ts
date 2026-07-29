import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import api from "@/lib/api";

// ─── Types ────────────────────────────────────────────────────────────────────

export interface UserDetailDto {
  id: string;
  username: string;
  email?: string;
  role: string;
  branchId?: string;
  doctorName?: string;
  doctorSpecialty?: string;
  doctorColor?: string;
  doctorInitials?: string;
  isActive: boolean;
  deletedAt?: string | null;
  lastLoginAt?: string;
  mustChangePassword?: boolean;
  createdAt?: string;
}

export interface CreateUserRequest {
  username: string;
  password: string;
  role: string;
  email?: string;
  doctorName?: string;
  doctorSpecialty?: string;
  doctorColor?: string;
}

export interface EditUserRequest {
  username?: string;
  email?: string;
  role?: string;
  doctorName?: string;
  doctorSpecialty?: string;
  doctorColor?: string;
}

export interface PasswordResetRequestDto {
  id: string;
  userId: string;
  username: string;
  reason: string;
  status: string;
  createdAt: string;
  notes?: string;
}

export interface PermissionDto {
  resource: string;
  action: string;
  key: string;
  label: string;
}

export interface RolePermissionDto {
  role: string;
  permissions: string[];
}

export interface PatientPortalAccountListItem {
  patientId: string;
  patientNumber: string;
  patientName: string;
  phone?: string;
  username: string;
  accountActive: boolean;
  mustChangePassword: boolean;
  lastLogin?: string | null;
  linkedUserId?: string | null;
}

// ─── Queries ──────────────────────────────────────────────────────────────────

/** Hook: Fetch all users */
export function useUsers() {
  return useQuery({
    queryKey: ["users"],
    queryFn: async () => {
      const { data } = await api.get<UserDetailDto[]>("/api/users");
      return data;
    },
    staleTime: 30_000,
  });
}

/** Hook: Fetch patient portal accounts separately from staff users */
export function usePatientPortalAccounts() {
  return useQuery({
    queryKey: ["patient-portal-accounts"],
    queryFn: async () => {
      const { data } = await api.get<PatientPortalAccountListItem[]>("/api/users/patient-portal-accounts");
      return data;
    },
    staleTime: 30_000,
  });
}

/** Hook: Fetch a single user */
export function useUser(id: string | undefined) {
  return useQuery({
    queryKey: ["users", id],
    queryFn: async () => {
      if (!id) return null;
      const { data } = await api.get<UserDetailDto>(`/api/users/${id}`);
      return data;
    },
    enabled: !!id,
    staleTime: 30_000,
  });
}

/** Hook: Fetch pending password reset requests */
export function usePasswordResetRequests() {
  return useQuery({
    queryKey: ["password-reset-requests"],
    queryFn: async () => {
      const { data } = await api.get<PasswordResetRequestDto[]>("/api/users/password-reset-requests");
      return data;
    },
    staleTime: 30_000,
  });
}

/** Hook: Fetch all permissions */
export function usePermissions() {
  return useQuery({
    queryKey: ["permissions"],
    queryFn: async () => {
      const { data } = await api.get<PermissionDto[]>("/api/permissions");
      return data;
    },
    staleTime: 60_000,
  });
}

/** Hook: Fetch role-specific permissions */
export function useRolePermissions(role: string | undefined) {
  return useQuery({
    queryKey: ["role-permissions", role],
    queryFn: async () => {
      if (!role) return null;
      const { data } = await api.get<RolePermissionDto>(`/api/roles/${role}/permissions`);
      return data;
    },
    enabled: !!role,
    staleTime: 30_000,
  });
}

// ─── Mutations ────────────────────────────────────────────────────────────────

/** Mutation: Create user */
export function useCreateUser() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: CreateUserRequest) => {
      const { data } = await api.post<UserDetailDto>("/api/users", req);
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["users"] });
    },
  });
}

/** Mutation: Edit user */
export function useEditUser() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, ...req }: EditUserRequest & { id: string }) => {
      const { data } = await api.put<UserDetailDto>(`/api/users/${id}`, req);
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["users"] });
    },
  });
}

/** Mutation: Update user role */
export function useUpdateUserRole() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, role }: { id: string; role: string }) => {
      const { data } = await api.put(`/api/users/${id}/role`, { role });
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["users"] });
    },
  });
}

/** Mutation: Toggle user active status */
export function useToggleUserStatus() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const { data } = await api.put(`/api/users/${id}/status`, {});
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["users"] });
    },
  });
}

/** Mutation: Soft delete user */
export function useDeleteUser() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const { data } = await api.delete(`/api/users/${id}`);
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["users"] });
    },
  });
}

/** Mutation: Restore deleted user */
export function useRestoreUser() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const { data } = await api.post(`/api/users/${id}/restore`);
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["users"] });
    },
  });
}

/** Mutation: Admin reset user password → returns temporary password */
export function useResetUserPassword() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const { data } = await api.post<{ temporaryPassword?: string; tempPassword?: string }>(`/api/users/${id}/reset-password`);
      return { temporaryPassword: data.temporaryPassword ?? data.tempPassword ?? "" };
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["users"] });
    },
  });
}

/** Mutation: Approve password reset request → returns temporary password */
export function useApproveResetRequest() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const { data } = await api.post<{ temporaryPassword?: string; tempPassword?: string }>(`/api/users/password-reset-requests/${id}/approve`);
      return { temporaryPassword: data.temporaryPassword ?? data.tempPassword ?? "" };
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["password-reset-requests"] });
      qc.invalidateQueries({ queryKey: ["users"] });
    },
  });
}

/** Mutation: Reject password reset request */
export function useRejectResetRequest() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, notes }: { id: string; notes?: string }) => {
      const { data } = await api.post(`/api/users/password-reset-requests/${id}/reject`, { notes });
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["password-reset-requests"] });
    },
  });
}

/** Mutation: Update role permissions */
export function useUpdateRolePermissions() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ role, permissions }: { role: string; permissions: string[] }) => {
      const { data } = await api.put(`/api/roles/${role}/permissions`, { permissions });
      return data;
    },
    onSuccess: (_data, variables) => {
      qc.invalidateQueries({ queryKey: ["role-permissions", variables.role] });
      qc.invalidateQueries({ queryKey: ["permissions"] });
    },
  });
}

// ─── Email Stats Types & Hooks ─────────────────────────────────────────────

export interface EmailStatsResponse {
  today: {
    sent: number;
    failed: number;
    byCategory: { category: string; sent: number; failed: number }[];
  };
  yesterday: { sent: number };
  week: { sent: number; failed: number };
  limit: {
    dailyLimit: number;
    used: number;
    remaining: number;
    percentage: number;
    isNearLimit: boolean;
    isAtLimit: boolean;
  };
  recentEmails: {
    id: string;
    toEmail: string;
    subject: string;
    category: string;
    provider: string | null;
    isSent: boolean;
    errorMessage: string | null;
    createdAt: string;
    relatedEntityType: string | null;
    relatedEntityId: string | null;
  }[];
}

export interface EmailHistoryResponse {
  total: number;
  page: number;
  pageSize: number;
  emails: {
    id: string;
    toEmail: string;
    subject: string;
    category: string;
    provider: string | null;
    isSent: boolean;
    errorMessage: string | null;
    externalId: string | null;
    createdAt: string;
    relatedEntityType: string | null;
    relatedEntityId: string | null;
  }[];
}

/** Hook: Fetch email statistics */
export function useEmailStats() {
  return useQuery({
    queryKey: ["email-stats"],
    queryFn: async () => {
      const { data } = await api.get<EmailStatsResponse>("/api/email-stats");
      return data;
    },
    staleTime: 60_000,
  });
}

/** Hook: Fetch email history */
export function useEmailHistory(
  fromDate?: string,
  toDate?: string,
  category?: string,
  page = 1,
) {
  return useQuery({
    queryKey: ["email-history", fromDate, toDate, category, page],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (fromDate) params.set("fromDate", fromDate);
      if (toDate) params.set("toDate", toDate);
      if (category) params.set("category", category);
      params.set("page", String(page));
      const { data } = await api.get<EmailHistoryResponse>(`/api/email-stats/history?${params}`);
      return data;
    },
    staleTime: 30_000,
  });
}
