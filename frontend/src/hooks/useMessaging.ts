"use client";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import api from "@/lib/api";
import type {
  ConversationListItem,
  ConversationDetail,
  CreateConversationRequest,
  SendMessageRequest,
  UnreadCount,
  ConversationFilter,
  Message,
  MessagingStats,
} from "@/types/messaging";

// ─── جلب محادثاتي ────────────────────────────────────────────────────────────
export function useConversations(
  page = 1,
  search?: string,
  filter?: ConversationFilter
) {
  return useQuery({
    queryKey: ["conversations", page, search, filter],
    queryFn: async () => {
      const params = new URLSearchParams({ page: String(page), pageSize: "20" });
      if (search) params.set("search", search);

      // Map frontend filter to backend type parameter
      if (filter === "StaffToStaff" || filter === "StaffToPatient" || filter === "PatientFacing") {
        params.set("type", filter);
      }

      const { data } = await api.get(`/api/messages/conversations?${params}`);

      // Client-side filtering for "unread" (backend doesn't have unread filter yet)
      let conversations = (data.data ?? []) as ConversationListItem[];
      const totalCount = data.totalCount ?? 0;

      if (filter === "unread") {
        conversations = conversations.filter((c) => c.unreadCount > 0);
      }

      return {
        data: conversations,
        totalCount: filter === "unread" ? conversations.length : totalCount,
        page: data.page ?? page,
        pageSize: data.pageSize ?? 20,
        totalPages: data.totalPages ?? 1,
      };
    },
    staleTime: 5_000,
    refetchInterval: 60_000, // تم تقليله من 15s لأن SignalR يدفع التحديثات فورياً
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
    refetchInterval: conversationId ? 30_000 : false, // تم تقليله من 4s لأن SignalR يدفع الرسائل فورياً
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
    refetchInterval: 60_000,
    refetchOnWindowFocus: true,
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

// ─── إنشاء/جلب محادثة PatientFacing (مرئية للمريض) ──────────────────────────
export function usePatientConversation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (patientId: string) => {
      const { data } = await api.post(
        `/api/messages/conversations/patient/${patientId}`
      );
      return data as ConversationDetail;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["conversations"] });
      queryClient.invalidateQueries({ queryKey: ["unreadCount"] });
    },
  });
}

// ─── إنشاء/جلب محادثة StaffToPatient (داخلية — لا تظهر للمريض) ──────────────
export function useInternalPatientConversation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (patientId: string) => {
      const { data } = await api.post(
        `/api/messages/internal-patient/${patientId}`
      );
      return data as ConversationDetail;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["conversations"] });
      queryClient.invalidateQueries({ queryKey: ["unreadCount"] });
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

// ─── تعديل رسالة ─────────────────────────────────────────────────────────────
export function useEditMessage(conversationId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ messageId, content }: { messageId: string; content: string }) => {
      const { data } = await api.put<Message>(
        `/api/messages/conversations/${conversationId}/messages/${messageId}`,
        { content }
      );
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["conversation", conversationId] });
    },
  });
}

// ─── إحصائيات المراسلة ───────────────────────────────────────────────────────
export function useMessagingStats() {
  return useQuery({
    queryKey: ["messagingStats"],
    queryFn: async () => {
      const { data } = await api.get("/api/messages/stats");
      return data as MessagingStats;
    },
    staleTime: 30_000,
  });
}

// ─── حذف رسالة ────────────────────────────────────────────────────────────────
export function useDeleteMessage(conversationId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (messageId: string) => {
      await api.delete(`/api/messages/conversations/${conversationId}/messages/${messageId}`);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["conversation", conversationId] });
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
