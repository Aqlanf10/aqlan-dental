'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import api from '@/lib/api';
import type {
  AttendanceRecord,
  AttendanceSummary,
  SalaryRecord,
  AdvanceRecord,
  LeaveRecord,
  LeaveBalance,
  EmployeeDocument,
} from '@/types/hr';

// ==================== ATTENDANCE ====================

interface AttendanceFilters {
  employeeId?: string;
  date?: string;
  year?: number;
  month?: number;
  status?: string;
  branchId?: string;
  page?: number;
  pageSize?: number;
}

export function useAttendance(filters: AttendanceFilters = {}) {
  return useQuery({
    queryKey: ['attendance', filters],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (filters.employeeId) params.append('employeeId', filters.employeeId);
      if (filters.date) params.append('date', filters.date);
      if (filters.year) params.append('year', String(filters.year));
      if (filters.month) params.append('month', String(filters.month));
      if (filters.status) params.append('status', filters.status);
      if (filters.branchId) params.append('branchId', filters.branchId);
      if (filters.page) params.append('page', String(filters.page));
      if (filters.pageSize) params.append('pageSize', String(filters.pageSize));
      const { data } = await api.get(`/api/attendance?${params.toString()}`);
      return data;
    },
  });
}

export function useAttendanceSummary(year: number, month: number, branchId?: string) {
  return useQuery({
    queryKey: ['attendance-summary', year, month, branchId],
    queryFn: async () => {
      const params = new URLSearchParams();
      params.append('year', String(year));
      params.append('month', String(month));
      if (branchId) params.append('branchId', branchId);
      const { data } = await api.get(`/api/attendance/summary?${params.toString()}`);
      return data as AttendanceSummary[];
    },
    enabled: !!year && !!month,
  });
}

export function useCheckIn() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { employeeId?: string; userId?: string }) => {
      const { data } = await api.post('/api/attendance/check-in', payload);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['attendance'] });
      queryClient.invalidateQueries({ queryKey: ['attendance-summary'] });
    },
  });
}

export function useCheckOut() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { employeeId?: string; userId?: string }) => {
      const { data } = await api.post('/api/attendance/check-out', payload);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['attendance'] });
      queryClient.invalidateQueries({ queryKey: ['attendance-summary'] });
    },
  });
}

// ==================== SALARIES ====================

interface SalaryFilters {
  employeeId?: string;
  year?: number;
  month?: number;
  branchId?: string;
  isPaid?: boolean;
  page?: number;
  pageSize?: number;
}

export function useSalaries(filters: SalaryFilters = {}) {
  return useQuery({
    queryKey: ['salaries', filters],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (filters.employeeId) params.append('employeeId', filters.employeeId);
      if (filters.year) params.append('year', String(filters.year));
      if (filters.month) params.append('month', String(filters.month));
      if (filters.branchId) params.append('branchId', filters.branchId);
      if (filters.isPaid !== undefined) params.append('isPaid', String(filters.isPaid));
      if (filters.page) params.append('page', String(filters.page));
      if (filters.pageSize) params.append('pageSize', String(filters.pageSize));
      const { data } = await api.get(`/api/salaries?${params.toString()}`);
      return data;
    },
  });
}

export function useGenerateSalary() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { employeeId: string; year: number; month: number }) => {
      const { data } = await api.post('/api/salaries/generate', payload);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['salaries'] });
    },
  });
}

export function useGenerateAllSalaries() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { year: number; month: number; branchId?: string }) => {
      const { data } = await api.post('/api/salaries/generate-all', payload);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['salaries'] });
    },
  });
}

export function usePaySalary() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, payload }: { id: string; payload: { paymentMethod: string } }) => {
      const { data } = await api.put(`/api/salaries/${id}/pay`, payload);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['salaries'] });
    },
  });
}

// ==================== ADVANCES ====================

interface AdvanceFilters {
  employeeId?: string;
  status?: string;
  branchId?: string;
  page?: number;
  pageSize?: number;
}

export function useAdvances(filters: AdvanceFilters = {}) {
  return useQuery({
    queryKey: ['advances', filters],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (filters.employeeId) params.append('employeeId', filters.employeeId);
      if (filters.status) params.append('status', filters.status);
      if (filters.branchId) params.append('branchId', filters.branchId);
      if (filters.page) params.append('page', String(filters.page));
      if (filters.pageSize) params.append('pageSize', String(filters.pageSize));
      const { data } = await api.get(`/api/advances?${params.toString()}`);
      return data;
    },
  });
}

export function useCreateAdvance() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: {
      employeeId: string;
      amount: number;
      reason?: string;
      deductFromMonth?: number;
      deductFromYear?: number;
    }) => {
      const { data } = await api.post('/api/advances', payload);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['advances'] });
    },
  });
}

export function useApproveAdvance() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, payload }: { id: string; payload: { approved: boolean; rejectionReason?: string } }) => {
      const { data } = await api.put(`/api/advances/${id}/approve`, payload);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['advances'] });
    },
  });
}

// ==================== LEAVES ====================

interface LeaveFilters {
  employeeId?: string;
  leaveType?: string;
  status?: string;
  branchId?: string;
  page?: number;
  pageSize?: number;
}

export function useLeaves(filters: LeaveFilters = {}) {
  return useQuery({
    queryKey: ['leaves', filters],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (filters.employeeId) params.append('employeeId', filters.employeeId);
      if (filters.leaveType) params.append('leaveType', filters.leaveType);
      if (filters.status) params.append('status', filters.status);
      if (filters.branchId) params.append('branchId', filters.branchId);
      if (filters.page) params.append('page', String(filters.page));
      if (filters.pageSize) params.append('pageSize', String(filters.pageSize));
      const { data } = await api.get(`/api/leaves?${params.toString()}`);
      return data;
    },
  });
}

export function useLeaveBalance(employeeId: string | undefined) {
  return useQuery({
    queryKey: ['leave-balance', employeeId],
    queryFn: async () => {
      const { data } = await api.get(`/api/leaves/balance/${employeeId}`);
      return data as LeaveBalance;
    },
    enabled: !!employeeId,
  });
}

export function useCreateLeave() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: {
      employeeId: string;
      leaveType: string;
      startDate: string;
      endDate: string;
      reason?: string;
    }) => {
      const { data } = await api.post('/api/leaves', payload);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['leaves'] });
      queryClient.invalidateQueries({ queryKey: ['leave-balance'] });
    },
  });
}

export function useApproveLeave() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, payload }: { id: string; payload: { approved: boolean; rejectionReason?: string } }) => {
      const { data } = await api.put(`/api/leaves/${id}/approve`, payload);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['leaves'] });
      queryClient.invalidateQueries({ queryKey: ['leave-balance'] });
      queryClient.invalidateQueries({ queryKey: ['attendance'] });
    },
  });
}

export function useCancelLeave() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const { data } = await api.put(`/api/leaves/${id}/cancel`);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['leaves'] });
      queryClient.invalidateQueries({ queryKey: ['leave-balance'] });
      queryClient.invalidateQueries({ queryKey: ['attendance'] });
    },
  });
}

// ==================== EMPLOYEE DOCUMENTS ====================

export function useEmployeeDocuments(employeeId: string | undefined) {
  return useQuery({
    queryKey: ['employee-documents', employeeId],
    queryFn: async () => {
      const { data } = await api.get(`/api/employee-documents?employeeId=${employeeId}`);
      return data;
    },
    enabled: !!employeeId,
  });
}

export function useUploadEmployeeDocument() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: {
      employeeId: string;
      documentType: string;
      title: string;
      filePath: string;
      fileName?: string;
      contentType?: string;
      fileSize?: number;
    }) => {
      const { data } = await api.post('/api/employee-documents', payload);
      return data;
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ['employee-documents', variables.employeeId] });
    },
  });
}

export function useDeleteEmployeeDocument() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const { data } = await api.delete(`/api/employee-documents/${id}`);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['employee-documents'] });
    },
  });
}
