/**
 * Custom hooks for the Daily Operations page.
 * Reuses existing usePatientJourney mutations where possible.
 */

"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import api from "@/lib/api";
import type { TodayJourneyItem, DoctorOption, BranchOption, RoomOption, ServiceOption } from "./constants";
import type { DailyJourneySummary } from "@/types/journey";
import type { DashboardStats } from "@/types/dashboard";

// ─── Today's Journey Items ───────────────────────────────────────────────────
export function useTodayJourneyItems(params: {
  date?: string;
  status?: string;
  doctorId?: string;
  serviceId?: string;
  roomId?: string;
}) {
  return useQuery<TodayJourneyItem[]>({
    queryKey: ["daily-ops", "today", params],
    queryFn: async () => {
      const qs = new URLSearchParams();
      if (params.date) qs.set("date", params.date);
      if (params.status) qs.set("status", params.status);
      if (params.doctorId) qs.set("doctorId", params.doctorId);
      if (params.serviceId) qs.set("serviceId", params.serviceId);
      if (params.roomId) qs.set("roomId", params.roomId);
      const { data } = await api.get(`/api/patient-journey/today?${qs.toString()}`);
      return data;
    },
    staleTime: 15_000,
    refetchInterval: 30_000,
  });
}

// ─── Daily Summary for a specific patient ────────────────────────────────────
export function usePatientSummary(patientId: string | null) {
  return useQuery<DailyJourneySummary>({
    queryKey: ["daily-ops", "summary", patientId],
    queryFn: async () => {
      if (!patientId) throw new Error("No patientId");
      const { data } = await api.get(`/api/patient-journey/${patientId}/daily-summary`);
      return data;
    },
    enabled: !!patientId,
    staleTime: 30_000,
  });
}

// ─── Doctors list ────────────────────────────────────────────────────────────
export function useDoctors() {
  return useQuery<DoctorOption[]>({
    queryKey: ["daily-ops", "doctors"],
    queryFn: async () => {
      const { data } = await api.get("/api/doctors?status=active");
      return (data as { id: string; name: string; specialty?: string }[]).map(d => ({ id: d.id, name: d.name, specialty: d.specialty }));
    },
    staleTime: 60_000,
  });
}

// ─── Branches list ───────────────────────────────────────────────────────────
export function useBranches() {
  return useQuery<BranchOption[]>({
    queryKey: ["daily-ops", "branches"],
    queryFn: async () => {
      const { data } = await api.get("/api/branches?status=active");
      return (data as { id: string; name: string }[]).map(b => ({ id: b.id, name: b.name }));
    },
    staleTime: 60_000,
  });
}

// ─── Rooms ───────────────────────────────────────────────────────────────────
export function useRooms() {
  return useQuery<RoomOption[]>({
    queryKey: ["daily-ops", "rooms"],
    queryFn: async () => {
      const { data } = await api.get("/api/settings/rooms/active");
      return (data as { id: string; arabicName: string }[]).map(r => ({ id: r.id, arabicName: r.arabicName }));
    },
    staleTime: 60_000,
  });
}

// ─── Services ────────────────────────────────────────────────────────────────
export function useServices() {
  return useQuery<ServiceOption[]>({
    queryKey: ["daily-ops", "services"],
    queryFn: async () => {
      const { data } = await api.get("/api/settings/services/active");
      return (data as { id: string; arabicName: string; defaultPrice?: number; requiresConsultationFee?: boolean }[]).map(s => ({
        id: s.id,
        arabicName: s.arabicName,
        defaultPrice: s.defaultPrice,
        requiresConsultationFee: s.requiresConsultationFee,
      }));
    },
    staleTime: 60_000,
  });
}

// ─── Dashboard stats ─────────────────────────────────────────────────────────
export function useDashboardStats() {
  return useQuery<DashboardStats>({
    queryKey: ["daily-ops", "stats"],
    queryFn: async () => {
      const { data } = await api.get("/api/dashboard/stats");
      return data;
    },
    staleTime: 30_000,
    refetchInterval: 60_000,
  });
}

// ─── Clinic settings (name for WhatsApp) ─────────────────────────────────────
export function useClinicSettings() {
  return useQuery<{ clinicName: string }>({
    queryKey: ["daily-ops", "settings"],
    queryFn: async () => {
      const { data } = await api.get("/api/settings");
      const settings = data as Record<string, string>;
      return { clinicName: settings["ClinicName"] || settings["clinicName"] || "مركز الدكتور عقلان الكامل" };
    },
    staleTime: 120_000,
  });
}

// ─── Mutations ───────────────────────────────────────────────────────────────


export function useIntake() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (params: {
      appointmentId: string;
      body: { serviceId?: string; chiefComplaint?: string; notes?: string; roomId?: string };
    }) => {
      const { data } = await api.post(`/api/patient-journey/${params.appointmentId}/intake`, params.body);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
      queryClient.invalidateQueries({ queryKey: ["patient-journey"] });
    },
  });
}

export function useSendToQueue() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (params: { appointmentId: string; body?: { roomId?: string; notes?: string } }) => {
      const { data } = await api.post(`/api/patient-journey/${params.appointmentId}/send-to-queue`, params.body ?? {});
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
      queryClient.invalidateQueries({ queryKey: ["patient-journey"] });
      queryClient.invalidateQueries({ queryKey: ["clinic-queue"] });
    },
  });
}

export function useCallPatient() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (queueItemId: string) => {
      const { data } = await api.post(`/api/clinic-queue/${queueItemId}/call`);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
      queryClient.invalidateQueries({ queryKey: ["clinic-queue"] });
    },
  });
}

export function useEnterRoom() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (queueItemId: string) => {
      const { data } = await api.post(`/api/clinic-queue/${queueItemId}/enter-room`);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
      queryClient.invalidateQueries({ queryKey: ["clinic-queue"] });
    },
  });
}

export function useUpdateAppointmentStatus() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (params: { appointmentId: string; status: string }) => {
      const { data } = await api.put(`/api/appointments/${params.appointmentId}/status`, { status: params.status });
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
      queryClient.invalidateQueries({ queryKey: ["patient-journey"] });
      queryClient.invalidateQueries({ queryKey: ["appointments"] });
    },
  });
}

export function useCreatePayment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (body: {
      patientId: string;
      amount: number;
      paymentMethod?: string;
      contractId?: string;
      invoiceId?: string;
      serviceDescription?: string;
      doctorId?: string;
      notes?: string;
    }) => {
      const { data } = await api.post("/api/payments", body);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
      queryClient.invalidateQueries({ queryKey: ["finance"] });
      queryClient.invalidateQueries({ queryKey: ["payments"] });
    },
  });
}

export function useCheckout() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (params: {
      appointmentId: string;
      body: {
        paymentAmount?: number;
        paymentMethod?: string;
        notes?: string;
        nextAppointmentDate?: string;
        nextServiceId?: string;
      };
    }) => {
      const { data } = await api.post(`/api/patient-journey/${params.appointmentId}/checkout`, params.body);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
      queryClient.invalidateQueries({ queryKey: ["patient-journey"] });
      queryClient.invalidateQueries({ queryKey: ["finance"] });
    },
  });
}

export function useHandoff() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (params: {
      visitId: string;
      body: {
        treatmentDone?: string;
        diagnosis?: string;
        nextVisitPlan?: string;
        instructions?: string;
        followUpDate?: string;
        amountDue?: number;
        notes?: string;
      };
    }) => {
      const { data } = await api.post(`/api/patient-journey/${params.visitId}/handoff-to-reception`, params.body);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
      queryClient.invalidateQueries({ queryKey: ["patient-journey"] });
    },
  });
}

export function useCancelQueue() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (queueItemId: string) => {
      const { data } = await api.post(`/api/clinic-queue/${queueItemId}/cancel`);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
      queryClient.invalidateQueries({ queryKey: ["clinic-queue"] });
    },
  });
}

export function useChangeRoom() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (params: { queueItemId: string; roomId: string }) => {
      const { data } = await api.patch(`/api/clinic-queue/${params.queueItemId}/room`, { roomId: params.roomId });
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
      queryClient.invalidateQueries({ queryKey: ["clinic-queue"] });
    },
  });
}

export function useCreateAppointment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (body: {
      patientId: string;
      doctorId: string;
      appointmentDate: string;
      startTime: string;
      endTime?: string;
      serviceId?: string;
      appointmentType?: string;
      notes?: string;
    }) => {
      const { data } = await api.post("/api/appointments", body);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
      queryClient.invalidateQueries({ queryKey: ["appointments"] });
    },
  });
}

export function useCompleteVisit() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (params: {
      queueItemId: string;
    }) => {
      const { data } = await api.post(`/api/clinic-queue/${params.queueItemId}/complete`);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
      queryClient.invalidateQueries({ queryKey: ["clinic-queue"] });
    },
  });
}
