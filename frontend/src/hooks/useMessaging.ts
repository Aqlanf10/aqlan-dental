"use client";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import api from "@/lib/api";
import type {
  ConversationListItem,
  ConversationDetail,
  CreateConversationRequest,
  SendMessageRequest,
  UnreadCount,
} from "@/types/messaging";

// ─── جلب محادثاتي ────────────────────────────────────────────────────────────
export function useConversations(page = 1, search?: string) {
  return useQuery({
    queryKey: ["conversations", page, search],
    queryFn: async () => {
      const params = new URLSearchParams({ page: String(page), pageSize: "20" });
      if (search) params.set("search", search);
      const { data } = await api.get(`/api/messages/conversations?${params}`);
      return data as {
        data: ConversationListItem[];
        totalCount: number;
        page: number;
        pageSize: number;
        totalPages: number;
      };
    },
    staleTime: 5_000,
    refetchInterval: 15_000, // refresh conversation list every 15s
  });
}

// ─── جلب تفاصيل محادثة (مع polling) ────────────────────────────────────────
export function useConversation(conversationId: string | null, page = 1) {
  return useQuery({
    queryKey: ["conversation", conversationId, page],
    queryFn: async () => {
      if (!conversationId) return null;
      const { data } = await api.get(
        `/api/messages/conversations/${conversationId}?page=${page}&pageSize=50`
      );
      return data as ConversationDetail;
    },
    enabled: !!conversationId,
    staleTime: 2_000,
    refetchInterval: conversationId ? 4_000 : false, // poll every 4s when conversation is open
  });
}

// ─── عدد الرسائل غير المقروءة ────────────────────────────────────────────────
export function useUnreadCount() {
  return useQuery({
    queryKey: ["unreadCount"],
    queryFn: async () => {
      const { data } = await api.get("/api/messages/unread-count");
      return data as UnreadCount;
    },
    staleTime: 10_000,
    refetchInterval: 20_000,
  });
}

// ─── إنشاء محادثة ────────────────────────────────────────────────────────────
export function useCreateConversation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (req: CreateConversationRequest) => {
      const { data } = await api.post("/api/messages/conversations", req);
      return data as ConversationDetail;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["conversations"] });
    },
  });
}

// ─── إنشاء/جلب محادثة مريض (StaffToPatient) ─────────────────────────────────
export function usePatientConversation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (patientId: string) => {
      const { data } = await api.post(`/api/messages/conversations/patient/${patientId}`);
      return data as ConversationDetail;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["conversations"] });
    },
  });
}

// ─── إرسال رسالة ──────────────────────────────────────────────────────────────
export function useSendMessage(conversationId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (req: SendMessageRequest) => {
      const { data } = await api.post(
        `/api/messages/conversations/${conversationId}/messages`,
        req
      );
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ["conversation", conversationId],
      });
      queryClient.invalidateQueries({ queryKey: ["conversations"] });
      queryClient.invalidateQueries({ queryKey: ["unreadCount"] });
    },
  });
}

// ─── تحديد كمقروء ────────────────────────────────────────────────────────────
export function useMarkAsRead(conversationId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      if (!conversationId) return;
      await api.post(`/api/messages/conversations/${conversationId}/read`);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["conversations"] });
      queryClient.invalidateQueries({ queryKey: ["unreadCount"] });
    },
  });
}
