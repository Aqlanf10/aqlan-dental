"use client";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import api from "@/lib/api";
import type { WhatsAppDashboard, WhatsAppMessage, WhatsAppTemplate, SendMessageRequest, SendAppointmentReminderRequest } from "@/types/whatsapp";

export function useWhatsAppDashboard() {
  return useQuery({
    queryKey: ["whatsapp", "dashboard"],
    queryFn: async () => {
      const { data } = await api.get<WhatsAppDashboard>("/api/whatsapp/dashboard");
      return data;
    },
  });
}

export function useWhatsAppMessages(patientId?: string, page = 1) {
  return useQuery({
    queryKey: ["whatsapp", "messages", patientId, page],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (patientId) params.set("patientId", patientId);
      params.set("page", page.toString());
      const { data } = await api.get<WhatsAppMessage[]>(`/api/whatsapp/messages?${params}`);
      return data;
    },
  });
}

export function useWhatsAppTemplates() {
  return useQuery({
    queryKey: ["whatsapp", "templates"],
    queryFn: async () => {
      const { data } = await api.get<WhatsAppTemplate[]>("/api/whatsapp/templates");
      return data;
    },
  });
}

export function useSendWhatsAppMessage() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: SendMessageRequest) => {
      const { data } = await api.post<WhatsAppMessage>("/api/whatsapp/send", req);
      return data;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["whatsapp"] }),
  });
}

export function useSendAppointmentReminder() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: SendAppointmentReminderRequest) => {
      const { data } = await api.post<WhatsAppMessage>("/api/whatsapp/appointment-reminder", req);
      return data;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["whatsapp"] }),
  });
}

export function useSendPendingReminders() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      const { data } = await api.post<{ message: string; count: number }>("/api/whatsapp/send-pending-reminders");
      return data;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["whatsapp"] }),
  });
}

export function useRetryWhatsAppMessage() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (messageId: string) => {
      const { data } = await api.post(`/api/whatsapp/messages/${messageId}/retry`);
      return data;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["whatsapp"] }),
  });
}

export function useUpdateWhatsAppTemplate() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, contentTemplate, isActive }: { id: string; contentTemplate: string; isActive: boolean }) => {
      const { data } = await api.put<WhatsAppTemplate>(`/api/whatsapp/templates/${id}`, { contentTemplate, isActive });
      return data;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["whatsapp", "templates"] }),
  });
}
