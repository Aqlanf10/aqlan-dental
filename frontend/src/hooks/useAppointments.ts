import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import api from "@/lib/api";
import type { Appointment, CreateAppointmentRequest } from "@/types/appointment";

interface AppointmentFilters {
  date?: string;
  doctorId?: string;
  status?: string;
  startDate?: string;
  endDate?: string;
}

/** Hook: Fetch appointments with filters */
export function useAppointments(filters: AppointmentFilters = {}) {
  return useQuery({
    queryKey: ["appointments", filters],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (filters.date) params.set("date", filters.date);
      if (filters.doctorId) params.set("doctorId", filters.doctorId);
      if (filters.status) params.set("status", filters.status);
      if (filters.startDate) params.set("startDate", filters.startDate);
      if (filters.endDate) params.set("endDate", filters.endDate);

      const { data } = await api.get<Appointment[]>(
        `/api/appointments?${params.toString()}`
      );
      return data;
    },
    staleTime: 15_000,
  });
}

/** Hook: Fetch today's appointments */
export function useTodayAppointments() {
  return useQuery({
    queryKey: ["appointments", "today"],
    queryFn: async () => {
      const { data } = await api.get<Appointment[]>("/api/appointments/today");
      return data;
    },
    staleTime: 10_000,
    refetchInterval: 60_000, // Auto-refresh every minute
  });
}

/** Hook: Create appointment */
export function useCreateAppointment() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (appointment: CreateAppointmentRequest) => {
      const { data } = await api.post<Appointment>("/api/appointments", appointment);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["appointments"] });
    },
  });
}

/** Hook: Update appointment status */
export function useUpdateAppointmentStatus() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      id,
      status,
    }: {
      id: string;
      status: string;
    }) => {
      const { data } = await api.put(`/api/appointments/${id}/status`, { status });
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["appointments"] });
    },
  });
}

/** Hook: Check appointment conflict
 *  Verified to work with the POST /api/appointments/check-conflict endpoint (Phase 8, P8-3).
 *  Previously unused (dead code); now active and available for form validation.
 */
export function useCheckConflict() {
  return useMutation({
    mutationFn: async ({
      doctorId,
      date,
      startTime,
      durationMinutes,
      excludeId,
    }: {
      doctorId: string;
      date: string;
      startTime: string;
      durationMinutes: number;
      excludeId?: string;
    }) => {
      const { data } = await api.post("/api/appointments/check-conflict", {
        doctorId,
        date,
        startTime,
        durationMinutes,
        excludeId,
      });
      return data;
    },
  });
}
