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
      // GAP-01 FIX: Send both from/to and startDate/endDate for backward compatibility
      // Backend accepts from/to as primary, startDate/endDate as fallback
      if (filters.startDate) params.set("from", filters.startDate);
      else if (filters.date) params.set("from", filters.date);

      if (filters.endDate) params.set("to", filters.endDate);
      else if (filters.date) params.set("to", filters.date);

      if (filters.doctorId) params.set("doctorId", filters.doctorId);
      if (filters.status) params.set("status", filters.status);

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

/** Hook: Check appointment conflict — POST /api/appointments/check-conflict
 *
 *  CORE-APPT-017: this comment used to claim the hook was "now active and available
 *  for form validation". It is not wired into any form — the only importer is its
 *  own structural test. There is therefore NO live warning while the user is picking
 *  a doctor/time; a clash is reported only after submit, via the 409 that
 *  AppointmentForm already renders correctly (with a link to that day's schedule).
 *
 *  Kept because the endpoint works and a pre-submit check is a reasonable future
 *  improvement, but the comment now describes the shipped behaviour rather than an
 *  intent. Wiring it into the form is a feature, not a bug fix, so it is deliberately
 *  not done here.
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
