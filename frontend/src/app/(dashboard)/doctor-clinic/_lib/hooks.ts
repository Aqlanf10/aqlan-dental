/**
 * Custom hooks for the Doctor Clinic Workspace.
 * Reuses shared types from daily-operations where possible.
 */

"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import api from "@/lib/api";
import type { ServiceOption, DoctorOption, RoomOption } from "../../daily-operations/_lib/constants";
import type { DailyJourneySummary } from "@/types/journey";

// ─── Service option (with price) ────────────────────────────────────────────
export interface ServiceWithPrice {
  id: string;
  arabicName: string;
  defaultPrice?: number;
  requiresConsultationFee?: boolean;
}

// ─── Doctor's patients today ────────────────────────────────────────────────
export interface DoctorPatientItem {
  appointmentId: string;
  patientId: string;
  patientName: string;
  patientNumber?: string;
  appointmentTime: string;
  appointmentStatus: string;
  doctorId: string;
  doctorName: string;
  serviceId?: string;
  serviceName?: string;
  roomName?: string;
  roomId?: string;
  queueItemId?: string;
  queueStatus?: string;
  visitId?: string;
  visitStatus?: string;
  checkoutStatus?: string;
  nextAction: string;
  hasMedicalAlerts?: boolean;
  chiefComplaint?: string;
  inRoomSince?: string;
  visitCount?: number;
}

// ─── Fetch doctor's patients today ──────────────────────────────────────────
export function useDoctorPatientsToday(doctorId: string | undefined) {
  return useQuery<DoctorPatientItem[]>({
    queryKey: ["doctor-clinic", "patients", doctorId],
    queryFn: async () => {
      if (!doctorId) return [];
      const qs = new URLSearchParams();
      qs.set("doctorId", doctorId);
      const { data } = await api.get(`/api/patient-journey/today?${qs.toString()}`);
      return data;
    },
    enabled: !!doctorId,
    staleTime: 15_000,
    refetchInterval: 30_000,
  });
}

// ─── Patient daily summary (doctor view — no phone/finance) ─────────────────
export function useDoctorPatientSummary(patientId: string | null) {
  return useQuery<DailyJourneySummary>({
    queryKey: ["doctor-clinic", "summary", patientId],
    queryFn: async () => {
      if (!patientId) throw new Error("No patientId");
      const { data } = await api.get(`/api/patient-journey/${patientId}/daily-summary`);
      return data;
    },
    enabled: !!patientId,
    staleTime: 30_000,
  });
}

// ─── Services (for priced procedures) ───────────────────────────────────────
export function useDoctorServices() {
  return useQuery<ServiceWithPrice[]>({
    queryKey: ["doctor-clinic", "services"],
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

// ─── Rooms ──────────────────────────────────────────────────────────────────
export function useDoctorRooms() {
  return useQuery<RoomOption[]>({
    queryKey: ["doctor-clinic", "rooms"],
    queryFn: async () => {
      const { data } = await api.get("/api/settings/rooms/active");
      return (data as { id: string; arabicName: string }[]).map(r => ({ id: r.id, arabicName: r.arabicName }));
    },
    staleTime: 60_000,
  });
}

// ─── Start Visit mutation ───────────────────────────────────────────────────
export function useStartVisit() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (appointmentId: string) => {
      const { data } = await api.post(`/api/patient-journey/${appointmentId}/start-visit`);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["doctor-clinic"] });
      queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
      queryClient.invalidateQueries({ queryKey: ["patient-journey"] });
      queryClient.invalidateQueries({ queryKey: ["clinic-queue"] });
    },
  });
}

// ─── Handoff to Reception mutation ──────────────────────────────────────────
export function useHandoffToReception() {
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
        suggestedServiceId?: string;
        notes?: string;
      };
    }) => {
      const { data } = await api.post(`/api/patient-journey/${params.visitId}/handoff-to-reception`, params.body);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["doctor-clinic"] });
      queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
      queryClient.invalidateQueries({ queryKey: ["patient-journey"] });
      queryClient.invalidateQueries({ queryKey: ["clinic-queue"] });
      queryClient.invalidateQueries({ queryKey: ["finance"] });
    },
  });
}

// ─── Create Draft Invoice (for reception checkout flow) ────────────────────
export function useCreateDraftInvoice() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (visitId: string) => {
      const { data } = await api.post(`/api/patient-journey/${visitId}/create-draft-invoice`);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["doctor-clinic"] });
      queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
      queryClient.invalidateQueries({ queryKey: ["finance"] });
    },
  });
}
