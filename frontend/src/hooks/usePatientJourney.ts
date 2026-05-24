"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import api from "@/lib/api";
import type { DailyJourneySummary } from "@/types/journey";

// ─── Daily Summary ──────────────────────────────────────────────────────

export function useDailyJourneySummary(patientId: string | null) {
  return useQuery<DailyJourneySummary>({
    queryKey: ["patient-journey", "daily-summary", patientId],
    queryFn: async () => {
      if (!patientId) throw new Error("No patientId");
      const { data } = await api.get<DailyJourneySummary>(
        `/api/patient-journey/${patientId}/daily-summary`
      );
      return data;
    },
    enabled: !!patientId,
    staleTime: 30_000,
    refetchInterval: 60_000,
  });
}

// ─── Journey Actions ────────────────────────────────────────────────────

export function useJourneyIntake() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (params: {
      appointmentId: string;
      body: { serviceId?: string; chiefComplaint?: string; notes?: string; roomId?: string; requiresConsultationFee?: boolean; consultationFeeAmount?: number };
    }) => {
      const { data } = await api.post(
        `/api/patient-journey/${params.appointmentId}/intake`,
        params.body
      );
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["patient-journey"] });
      qc.invalidateQueries({ queryKey: ["appointments"] });
      qc.invalidateQueries({ queryKey: ["clinic-queue"] });
    },
  });
}

export function useJourneySendToQueue() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (params: {
      appointmentId: string;
      body?: { roomId?: string; notes?: string };
    }) => {
      const { data } = await api.post(
        `/api/patient-journey/${params.appointmentId}/send-to-queue`,
        params.body ?? {}
      );
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["patient-journey"] });
      qc.invalidateQueries({ queryKey: ["appointments"] });
      qc.invalidateQueries({ queryKey: ["clinic-queue"] });
    },
  });
}

export function useJourneyStartVisit() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (appointmentId: string) => {
      const { data } = await api.post(
        `/api/patient-journey/${appointmentId}/start-visit`
      );
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["patient-journey"] });
      qc.invalidateQueries({ queryKey: ["visits"] });
    },
  });
}

export function useJourneyHandoff() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (params: {
      visitId: string;
      body: {
        treatmentDone?: string;
        diagnosis?: string;
        nextVisitPlan?: string;
        instructions?: string;
        suggestedServiceId?: string;
        followUpDate?: string;
        amountDue?: number;
        notes?: string;
      };
    }) => {
      const { data } = await api.post(
        `/api/patient-journey/${params.visitId}/handoff-to-reception`,
        params.body
      );
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["patient-journey"] });
      qc.invalidateQueries({ queryKey: ["visits"] });
    },
  });
}

export function useJourneyCheckout() {
  const qc = useQueryClient();
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
      const { data } = await api.post(
        `/api/patient-journey/${params.appointmentId}/checkout`,
        params.body
      );
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["patient-journey"] });
      qc.invalidateQueries({ queryKey: ["appointments"] });
      qc.invalidateQueries({ queryKey: ["finance"] });
    },
  });
}

export function useJourneyCreateDraftInvoice() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (visitId: string) => {
      const { data } = await api.post(
        `/api/patient-journey/${visitId}/create-draft-invoice`
      );
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["patient-journey"] });
      qc.invalidateQueries({ queryKey: ["invoices"] });
    },
  });
}
