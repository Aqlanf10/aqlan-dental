/**
 * Custom hooks for the Doctor Clinic Workspace.
 * Reuses shared types from daily-operations where possible.
 */

"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import api from "@/lib/api";
import type { RoomOption } from "../../daily-operations/_lib/constants";
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
// Admin (includeAll=true): fetches ALL active clinical patients across all doctors/rooms.
// Doctor (doctorId): fetches only patients assigned to that specific doctor.
export function useDoctorPatientsToday(opts: { doctorId?: string; includeAll?: boolean } = {}) {
  const { doctorId, includeAll = false } = opts;

  return useQuery<DoctorPatientItem[]>({
    queryKey: includeAll
      ? ["doctor-clinic", "patients", "all"]
      : ["doctor-clinic", "patients", doctorId],
    queryFn: async () => {
      // Admin: omit doctorId to get all active patients
      // Doctor: send doctorId to filter to their patients
      if (!includeAll && !doctorId) return [];
      const qs = new URLSearchParams();
      if (!includeAll && doctorId) {
        qs.set("doctorId", doctorId);
      }
      const { data } = await api.get(`/api/patient-journey/today?${qs.toString()}`);
      return data;
    },
    enabled: includeAll || !!doctorId,
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
        chiefComplaint?: string;
        treatmentDone?: string;
        diagnosis?: string;
        nextVisitPlan?: string;
        instructions?: string;
        extraoralExamination?: string;
        intraoralExamination?: string;
        suggestedServiceId?: string;
        additionalServicesText?: string;
        followUpDate?: string;
        amountDue?: number;
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
      queryClient.invalidateQueries({ queryKey: ["visits"] });
    },
  });
}

export function useUpdateVisit() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (params: {
      visitId: string;
      patientId?: string;
      body: {
        chiefComplaint?: string;
        diagnosis?: string;
        treatmentDone?: string;
        instructions?: string;
        nextVisitPlan?: string;
        cost?: number;
        nextVisitDate?: string;
        // S1: New fields to prevent data loss on interim save
        serviceId?: string;
        extraoralExamination?: string;
        intraoralExamination?: string;
        additionalServicesText?: string;
        handoffNotes?: string;
      };
    }) => {
      const { data } = await api.put(`/api/visits/${params.visitId}`, params.body);
      return data;
    },
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: ["doctor-clinic"] });
      queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
      queryClient.invalidateQueries({ queryKey: ["patient-journey"] });
      queryClient.invalidateQueries({ queryKey: ["visits"] });
      if (variables.patientId) {
        queryClient.invalidateQueries({ queryKey: ["doctor-clinic", "summary", variables.patientId] });
      }
    },
  });
}

// ─── Create Draft Invoice (for reception checkout flow) ────────────────────
export function useDoctorCreatePrescription() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (body: {
      patientId: string;
      doctorId?: string;
      visitId?: string;
      diagnosis?: string;
      notes?: string;
      drugs: Array<{
        name: string;
        dose: string;
        frequency: string;
        duration: string;
        notes?: string;
      }>;
    }) => {
      const { data } = await api.post("/api/prescriptions", body);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["doctor-clinic"] });
      queryClient.invalidateQueries({ queryKey: ["patient-journey"] });
      queryClient.invalidateQueries({ queryKey: ["prescriptions"] });
      queryClient.invalidateQueries({ queryKey: ["visits"] });
    },
  });
}

export function useDoctorCreateLabOrder() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (body: {
      patientId: string;
      applianceType: string;
      labName?: string;
      expectedDate?: string;
      priority?: string;
      instructions?: string;
      doctorId?: string;
      shade?: string;
      restorationType?: string;
      visitId?: string;
    }) => {
      const { data } = await api.post("/api/lab-orders", body);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["doctor-clinic"] });
      queryClient.invalidateQueries({ queryKey: ["patient-journey"] });
      queryClient.invalidateQueries({ queryKey: ["daily-ops", "lab-orders"] });
      queryClient.invalidateQueries({ queryKey: ["lab-orders"] });
      queryClient.invalidateQueries({ queryKey: ["visits"] });
    },
  });
}

export function useDoctorUpdateToothCondition() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (params: {
      patientId: string;
      body: {
        toothNumber: string;
        condition?: string | null;
        surfacesAffected?: string | null;
        treatmentDone?: string | null;
        notes?: string | null;
      };
    }) => {
      const { data } = await api.put(`/api/dental-chart/${params.patientId}/teeth`, params.body);
      return data;
    },
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: ["doctor-clinic"] });
      queryClient.invalidateQueries({ queryKey: ["patient-journey"] });
      queryClient.invalidateQueries({ queryKey: ["dental-chart", variables.patientId] });
    },
  });
}

export function useDoctorCreateGeneralTreatment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (body: {
      patientId: string;
      visitId?: string;
      treatmentType: string;
      toothNumber?: string;
      materialUsed?: string;
      anesthesiaType?: string;
      cost?: number;
      doctorId?: string;
      notes?: string;
    }) => {
      const { data } = await api.post("/api/general-treatments", body);
      return data;
    },
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: ["doctor-clinic"] });
      queryClient.invalidateQueries({ queryKey: ["patient-journey"] });
      queryClient.invalidateQueries({ queryKey: ["general-treatments", variables.patientId] });
    },
  });
}

export function useDoctorCreateFollowUpAppointment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (body: {
      patientId: string;
      doctorId: string;
      appointmentDate: string;
      startTime: string;
      durationMinutes?: number;
      appointmentType?: string;
      serviceId?: string;
      notes?: string;
    }) => {
      const { data } = await api.post("/api/appointments", {
        ...body,
        durationMinutes: body.durationMinutes ?? 30,
        appointmentType: body.appointmentType ?? "FollowUp",
      });
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["doctor-clinic"] });
      queryClient.invalidateQueries({ queryKey: ["patient-journey"] });
      queryClient.invalidateQueries({ queryKey: ["appointments"] });
      queryClient.invalidateQueries({ queryKey: ["visits"] });
    },
  });
}

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
      queryClient.invalidateQueries({ queryKey: ["visits"] });
      queryClient.invalidateQueries({ queryKey: ["patient-journey"] });
      queryClient.invalidateQueries({ queryKey: ["prescriptions"] });
    },
  });
}
