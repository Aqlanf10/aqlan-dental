import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import api from "@/lib/api";
import type {
  Conversation,
  Message,
  DoctorForMessaging,
  CreateConversationRequest,
  SendMessageRequest,
} from "@/types/messaging";

// ── Get all conversations for current user ────────────────────────────────
export function useConversations() {
  return useQuery({
    queryKey: ["conversations"],
    queryFn: async () => {
      const { data } = await api.get<Conversation[]>("/api/messages/conversations");
      return data;
    },
    staleTime: 10_000,
  });
}

// ── Get messages in a conversation ────────────────────────────────────────
export function useMessages(conversationId: string | null) {
  return useQuery({
    queryKey: ["messages", conversationId],
    queryFn: async () => {
      const { data } = await api.get<Message[]>(
        `/api/messages/conversations/${conversationId}/messages`
      );
      return data;
    },
    enabled: !!conversationId,
    staleTime: 5_000,
  });
}

// ── Get doctors/staff for new conversation ────────────────────────────────
export function useDoctorsForMessaging() {
  return useQuery({
    queryKey: ["doctors-for-messaging"],
    queryFn: async () => {
      const { data } = await api.get<DoctorForMessaging[]>("/api/messages/doctors");
      return data;
    },
    staleTime: 60_000,
  });
}

// ── Get total unread count ────────────────────────────────────────────────
export function useMessageUnreadCount() {
  return useQuery({
    queryKey: ["message-unread-count"],
    queryFn: async () => {
      const { data } = await api.get<{ count: number }>("/api/messages/unread-count");
      return data.count;
    },
    staleTime: 15_000,
  });
}

// ── Create a new conversation ─────────────────────────────────────────────
export function useCreateConversation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (req: CreateConversationRequest) => {
      const { data } = await api.post<Conversation>("/api/messages/conversations", req);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["conversations"] });
    },
  });
}

// ── Send a message ────────────────────────────────────────────────────────
export function useSendMessage() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ conversationId, req }: { conversationId: string; req: SendMessageRequest }) => {
      const { data } = await api.post<Message>(
        `/api/messages/conversations/${conversationId}/messages`,
        req
      );
      return data;
    },
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: ["messages", variables.conversationId] });
      queryClient.invalidateQueries({ queryKey: ["conversations"] });
      queryClient.invalidateQueries({ queryKey: ["message-unread-count"] });
    },
  });
}

// ── Mark conversation as read ─────────────────────────────────────────────
export function useMarkAsRead() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (conversationId: string) => {
      await api.put(`/api/messages/conversations/${conversationId}/read`);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["conversations"] });
      queryClient.invalidateQueries({ queryKey: ["message-unread-count"] });
    },
  });
}
