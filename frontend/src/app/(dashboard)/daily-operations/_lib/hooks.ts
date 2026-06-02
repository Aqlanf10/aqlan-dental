/**
 * Custom hooks for the Daily Operations page.
 * Reuses existing usePatientJourney mutations where possible.
 */

"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import api from "@/lib/api";
import { useAuthStore } from "@/stores/authStore";
import { canViewFinanceReports } from "@/lib/roles";
import type { TodayJourneyItem, DoctorOption, BranchOption, RoomOption, ServiceOption, FinanceSummaryData } from "./constants";
import type { DailyJourneySummary } from "@/types/journey";
import type { DashboardStats } from "@/types/dashboard";

let lastHandoffToReceptionAt = 0;

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

// ─── Estimated Wait Time ─────────────────────────────────────────────────────
export function useEstimatedWait(queueItemId?: string) {
  return useQuery<{ estimatedMinutes: number; patientsAhead: number }>({
    queryKey: ["daily-ops", "estimated-wait", queueItemId],
    queryFn: async () => {
      const qs = new URLSearchParams();
      if (queueItemId) qs.set("queueItemId", queueItemId);
      const { data } = await api.get(`/api/clinic-queue/estimated-wait?${qs.toString()}`);
      return data;
    },
    enabled: !!queueItemId,
    staleTime: 15_000,
    refetchInterval: 30_000,
  });
}

// ─── General Queue Wait Time (no specific patient) ───────────────────────────
export function useQueueWaitTime() {
  return useQuery<{ estimatedMinutes: number; patientsAhead: number }>({
    queryKey: ["daily-ops", "queue-wait"],
    queryFn: async () => {
      const { data } = await api.get("/api/clinic-queue/estimated-wait");
      return data;
    },
    staleTime: 15_000,
    refetchInterval: 30_000,
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
      if (Date.now() - lastHandoffToReceptionAt < 5_000) {
        return { skipped: true, reason: "recent_handoff_to_reception" };
      }
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
        proposedProcedure?: string;
      };
    }) => {
      const { data } = await api.post(`/api/patient-journey/${params.visitId}/handoff-to-reception`, params.body);
      lastHandoffToReceptionAt = Date.now();
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
      queryClient.invalidateQueries({ queryKey: ["patient-journey"] });
      queryClient.invalidateQueries({ queryKey: ["clinic-queue"] });
      queryClient.invalidateQueries({ queryKey: ["finance"] });
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
    mutationFn: async (params: { queueItemId: string; roomName: string }) => {
      const { data } = await api.patch(`/api/clinic-queue/${params.queueItemId}/room`, { roomName: params.roomName });
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
      // Compute durationMinutes from startTime and endTime
      let durationMinutes: number | undefined;
      if (body.endTime && body.startTime) {
        const [sh, sm] = body.startTime.split(":").map(Number);
        const [eh, em] = body.endTime.split(":").map(Number);
        if (!isNaN(sh) && !isNaN(sm) && !isNaN(eh) && !isNaN(em)) {
          durationMinutes = (eh * 60 + em) - (sh * 60 + sm);
          if (durationMinutes <= 0) durationMinutes = undefined;
        }
      }
      // eslint-disable-next-line @typescript-eslint/no-unused-vars
      const { endTime, ...rest } = body;
      const { data } = await api.post("/api/appointments", {
        ...rest,
        durationMinutes: durationMinutes ?? 30,
        appointmentType: body.appointmentType || "Consultation",
      });
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

// ─── Walk-in: Quick Patient + Appointment + Intake + Queue ──────────────────
export function useWalkInPatient() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (params: {
      patientName: string;
      patientPhone: string;
      doctorId: string;
      serviceId?: string;
      branchId?: string;
      notes?: string;
    }) => {
      // Step 1: Check for duplicate patient by phone before creating
      let patientId: string | undefined;
      try {
        const { data: existingPatient } = await api.get(`/api/patients/check-duplicate?phone=${encodeURIComponent(params.patientPhone)}`);
        if (existingPatient?.id) {
          patientId = existingPatient.id;
        }
      } catch {
        // Endpoint may not exist or no match found — proceed to create
      }

      // Step 2: Create new patient only if no existing one was found
      if (!patientId) {
        const patientRes = await api.post("/api/patients", {
          fullName: params.patientName,
          phone: params.patientPhone,
          branchId: params.branchId || undefined,
        });
        patientId = patientRes.data?.id;
      }

      if (!patientId) throw new Error("فشل إنشاء المريض");

      const today = new Date().toISOString().split("T")[0];
      const now = new Date();
      const startTime = `${String(now.getHours()).padStart(2, "0")}:${String(now.getMinutes()).padStart(2, "0")}`;
      const endMinutes = now.getMinutes() + 30;
      const endHour = now.getHours() + Math.floor(endMinutes / 60);
      const endTime = `${String(endHour).padStart(2, "0")}:${String(endMinutes % 60).padStart(2, "0")}`;

      const apptRes = await api.post("/api/appointments", {
        patientId,
        doctorId: params.doctorId,
        appointmentDate: today,
        startTime,
        endTime,
        serviceId: params.serviceId || undefined,
        appointmentType: "Consultation",
        notes: params.notes || "مريض مشي (Walk-in)",
      });
      const appointmentId = apptRes.data?.id;

      if (!appointmentId) throw new Error("فشل إنشاء الموعد");

      await api.post(`/api/patient-journey/${appointmentId}/intake`, {
        serviceId: params.serviceId || undefined,
        chiefComplaint: params.notes || undefined,
        notes: params.notes || "مريض مشي (Walk-in)",
      });

      const queueRes = await api.post(`/api/patient-journey/${appointmentId}/send-to-queue`, {
        notes: params.notes || "مريض مشي (Walk-in)",
      });

      return { patientId, appointmentId, queueItemId: queueRes.data?.id };
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
      queryClient.invalidateQueries({ queryKey: ["patient-journey"] });
      queryClient.invalidateQueries({ queryKey: ["clinic-queue"] });
      queryClient.invalidateQueries({ queryKey: ["patients"] });
      queryClient.invalidateQueries({ queryKey: ["appointments"] });
    },
  });
}

function mapFinanceSummaryPayload(data: Record<string, unknown>): FinanceSummaryData {
  return {
    todayCollected: (data.todayInflow as number) ?? 0,
    monthCollected: (data.monthInflow as number) ?? 0,
    totalOutstanding: (data.totalOutstanding as number) ?? 0,
    activeContracts: (data.activeContracts as number) ?? 0,
    unpaidInvoicesCount: (data.unpaidInvoicesCount as number) ?? 0,
    draftInvoicesCount: (data.draftInvoicesCount as number) ?? 0,
    overdueAmount: (data.overdueAmount as number) ?? 0,
    pendingCommissionsAmount: (data.pendingCommissionsAmount as number) ?? 0,
    recentPayments: (data.recentPayments as FinanceSummaryData["recentPayments"]) ?? [],
    recentInvoices: (data.recentInvoices as FinanceSummaryData["recentInvoices"]) ?? [],
  };
}

// ─── Finance Summary — V3 dashboard (Admin/Accountant) or cashier daily-summary (Reception) ─
export function useFinanceSummary() {
  const { user } = useAuthStore();
  const useFullDashboard = canViewFinanceReports(user?.role);

  return useQuery<FinanceSummaryData>({
    queryKey: ["daily-ops", "finance-summary", useFullDashboard],
    queryFn: async () => {
      const url = useFullDashboard
        ? "/api/finance-v3/dashboard"
        : "/api/cashier-sessions/daily-summary";
      const { data } = await api.get(url);
      return mapFinanceSummaryPayload(data as Record<string, unknown>);
    },
    staleTime: 30_000,
    refetchInterval: 60_000,
  });
}

// ─── Tomorrow's Appointments (for bulk SMS reminders) ──────────────────────
export function useTomorrowAppointments(doctorId?: string) {
  const tomorrow = new Date();
  tomorrow.setDate(tomorrow.getDate() + 1);
  const tomorrowStr = tomorrow.toISOString().split("T")[0];

  return useQuery<TodayJourneyItem[]>({
    queryKey: ["daily-ops", "tomorrow", tomorrowStr, doctorId],
    queryFn: async () => {
      const qs = new URLSearchParams();
      qs.set("date", tomorrowStr);
      if (doctorId) qs.set("doctorId", doctorId);
      const { data } = await api.get(`/api/patient-journey/today?${qs.toString()}`);
      return data;
    },
    staleTime: 60_000,
    enabled: false,
  });
}

// ─── Send Bulk SMS Reminders ───────────────────────────────────────────────
export function useSendBulkSmsReminders() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (params: {
      appointmentIds: string[];
    }) => {
      const results = await Promise.allSettled(
        params.appointmentIds.map(id =>
          api.post(`/api/appointments/${id}/send-reminder`)
        )
      );
      const succeeded = results.filter(r => r.status === "fulfilled").length;
      const failed = results.filter(r => r.status === "rejected").length;
      return { succeeded, failed };
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
    },
  });
}

// ─── Create Draft Invoice (for reception checkout from doctor handoff) ────
export function useCreateDraftInvoice() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (visitId: string) => {
      const { data } = await api.post(`/api/patient-journey/${visitId}/create-draft-invoice`);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
      queryClient.invalidateQueries({ queryKey: ["patient-journey"] });
      queryClient.invalidateQueries({ queryKey: ["finance"] });
    },
  });
}

export interface CashierSession {
  id: string;
  sessionNumber: string;
  openedAt: string;
  cashierName: string;
  expectedClosingCash: number;
  expectedClosingCard: number;
  expectedClosingBank: number;
  hasActiveSession?: boolean;
}

// ─── Active Cashier Session Check ──────────────────────────────────────────
export function useActiveCashierSession() {
  return useQuery<CashierSession | null>({
    queryKey: ["daily-ops", "active-cashier-session"],
    queryFn: async () => {
      try {
        const { data } = await api.get<{ hasActiveSession: boolean; id?: string } & CashierSession>("/api/cashier-sessions/active");
        if (!data?.hasActiveSession || !data.id) return null;
        return data;
      } catch {
        return null;
      }
    },
    staleTime: 15_000,
    refetchInterval: 30_000,
  });
}
