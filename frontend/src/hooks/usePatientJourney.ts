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
      qc.invalidateQueries({ queryKey: ["payments"] });
      qc.invalidateQueries({ queryKey: ["invoices"] });
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

// ─── New: Queue Actions (React Query mutations) ─────────────────────────

export function useJourneyCallPatient() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (queueItemId: string) => {
      const { data } = await api.post(`/api/clinic-queue/${queueItemId}/call`);
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["patient-journey"] });
      qc.invalidateQueries({ queryKey: ["clinic-queue"] });
    },
  });
}

export function useJourneyEnterRoom() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (queueItemId: string) => {
      const { data } = await api.post(`/api/clinic-queue/${queueItemId}/enter-room`);
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["patient-journey"] });
      qc.invalidateQueries({ queryKey: ["clinic-queue"] });
    },
  });
}

export function useJourneyCancelQueue() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (queueItemId: string) => {
      const { data } = await api.post(`/api/clinic-queue/${queueItemId}/cancel`);
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["patient-journey"] });
      qc.invalidateQueries({ queryKey: ["clinic-queue"] });
      qc.invalidateQueries({ queryKey: ["appointments"] });
    },
  });
}

export function useJourneyChangeRoom() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (params: { queueItemId: string; roomId: string }) => {
      const { data } = await api.patch(
        `/api/clinic-queue/${params.queueItemId}/room`,
        { roomId: params.roomId }
      );
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["patient-journey"] });
      qc.invalidateQueries({ queryKey: ["clinic-queue"] });
    },
  });
}

// ─── New: Appointment Status Actions ────────────────────────────────────

export function useJourneyUpdateAppointmentStatus() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (params: { appointmentId: string; status: string }) => {
      const { data } = await api.put(
        `/api/appointments/${params.appointmentId}/status`,
        { status: params.status }
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

// ─── New: Payment Recording ─────────────────────────────────────────────

export function useJourneyCreatePayment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (body: {
      patientId: string;
      amount: number;
      paymentMethod?: string;
      contractId?: string;
      invoiceId?: string;
      serviceDescription?: string;
      specialty?: string;
      doctorId?: string;
      notes?: string;
    }) => {
      const { data } = await api.post("/api/finance-v3/payments", body);
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["patient-journey"] });
      qc.invalidateQueries({ queryKey: ["finance"] });
      qc.invalidateQueries({ queryKey: ["payments"] });
      qc.invalidateQueries({ queryKey: ["invoices"] });
    },
  });
}

// ─── New: Invoice Issuing ───────────────────────────────────────────────

export function useJourneyIssueInvoice() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (invoiceId: string) => {
      const { data } = await api.patch(`/api/finance-v3/invoices/${invoiceId}/issue`);
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["patient-journey"] });
      qc.invalidateQueries({ queryKey: ["invoices"] });
      qc.invalidateQueries({ queryKey: ["finance"] });
    },
  });
}

// ─── New: Visit Update ──────────────────────────────────────────────────

export function useJourneyUpdateVisit() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (params: {
      visitId: string;
      body: {
        chiefComplaint?: string;
        clinicalNotes?: string;
        treatmentDone?: string;
        diagnosis?: string;
        instructions?: string;
        nextVisitPlan?: string;
        cost?: number;
      };
    }) => {
      const { data } = await api.put(
        `/api/visits/${params.visitId}`,
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

// ─── New: SMS ───────────────────────────────────────────────────────────

export function useJourneySendSms() {
  return useMutation({
    mutationFn: async (body: {
      to: string;
      message: string;
      patientId?: string;
    }) => {
      const { data } = await api.post("/api/sms/send", body);
      return data;
    },
  });
}

export function useJourneySendAppointmentReminder() {
  return useMutation({
    mutationFn: async (appointmentId: string) => {
      const { data } = await api.post(
        `/api/appointments/${appointmentId}/send-reminder`
      );
      return data;
    },
  });
}

// ─── New: Prescription ──────────────────────────────────────────────────

export function useJourneyCreatePrescription() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (body: {
      patientId: string;
      doctorId?: string;
      diagnosis?: string;
      notes?: string;
      items: Array<{
        medicationName: string;
        dosage?: string;
        frequency?: string;
        duration?: string;
        notes?: string;
      }>;
    }) => {
      const { data } = await api.post("/api/prescriptions", body);
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["patient-journey"] });
      qc.invalidateQueries({ queryKey: ["prescriptions"] });
    },
  });
}

// ─── New: Create Next Appointment ───────────────────────────────────────

export function useJourneyCreateAppointment() {
  const qc = useQueryClient();
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
      qc.invalidateQueries({ queryKey: ["patient-journey"] });
      qc.invalidateQueries({ queryKey: ["appointments"] });
    },
  });
}

// ─── New: Estimated Wait ────────────────────────────────────────────────

export function useQueueEstimatedWait(queueItemId: string | null | undefined) {
  return useQuery({
    queryKey: ["clinic-queue", "estimated-wait", queueItemId],
    queryFn: async () => {
      if (!queueItemId) return null;
      const { data } = await api.get(
        `/api/clinic-queue/estimated-wait?queueItemId=${queueItemId}`
      );
      return data as { estimatedMinutes: number; position: number };
    },
    enabled: !!queueItemId,
    staleTime: 15_000,
    refetchInterval: 30_000,
  });
}
