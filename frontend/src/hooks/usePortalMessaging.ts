"use client";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import portalApi from "@/lib/portalApi";

// ─── Types matching backend DTOs (ASP.NET Core serializes to camelCase) ───────

export interface PortalParticipant {
  userId: string;
  username: string;
  displayName?: string;
  role?: string;
  avatarInitials?: string;
  color?: string;
  isAdmin: boolean;
}

export interface PortalMessage {
  id: string;
  conversationId: string;
  senderId: string;
  senderName: string;
  senderInitials?: string;
  senderColor?: string;
  content: string;
  isSystemMessage: boolean;
  isReadByMe: boolean;
  createdAt: string;
}

export interface PortalConversationListItem {
  id: string;
  title: string;
  isGroup: boolean;
  conversationType: string;
  lastMessageAt?: string | null;
  lastMessagePreview?: string | null;
  unreadCount: number;
  otherParticipant?: PortalParticipant;
  participants: PortalParticipant[];
}

export interface PortalConversationDetail {
  id: string;
  title: string;
  isGroup: boolean;
  conversationType: string;
  participants: PortalParticipant[];
  messages: PortalMessage[];
  createdAt: string;
}

export interface PortalUnreadCount {
  totalUnread: number;
  unreadConversations: number;
}

// ─── Hooks ────────────────────────────────────────────────────────────────────

export function usePortalConversations() {
  return useQuery({
    queryKey: ["portalConversations"],
    queryFn: async () => {
      const { data } = await portalApi.get<{ data: PortalConversationListItem[] }>(
        "/api/portal/messages/conversations"
      );
      return (data.data ?? []) as PortalConversationListItem[];
    },
    staleTime: 10_000,
    refetchInterval: 15_000,
  });
}

export function usePortalConversation(conversationId: string | null) {
  return useQuery({
    queryKey: ["portalConversation", conversationId],
    queryFn: async () => {
      if (!conversationId) return null;
      const { data } = await portalApi.get<PortalConversationDetail>(
        `/api/portal/messages/conversations/${conversationId}`
      );
      return data;
    },
    enabled: !!conversationId,
    staleTime: 3_000,
    refetchInterval: conversationId ? 8_000 : false,
  });
}

export function usePortalUnreadCount() {
  return useQuery({
    queryKey: ["portalUnreadCount"],
    queryFn: async () => {
      const { data } = await portalApi.get<PortalUnreadCount>(
        "/api/portal/messages/unread-count"
      );
      return data;
    },
    staleTime: 15_000,
    refetchInterval: 20_000,
  });
}

export function usePortalSendMessage(conversationId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (content: string) => {
      const { data } = await portalApi.post<PortalMessage>(
        `/api/portal/messages/conversations/${conversationId}/messages`,
        { content }
      );
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["portalConversation", conversationId] });
      queryClient.invalidateQueries({ queryKey: ["portalConversations"] });
      queryClient.invalidateQueries({ queryKey: ["portalUnreadCount"] });
    },
  });
}

export function usePortalMarkAsRead(conversationId: string | null) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      if (!conversationId) return;
      await portalApi.post(`/api/portal/messages/conversations/${conversationId}/read`);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["portalConversations"] });
      queryClient.invalidateQueries({ queryKey: ["portalUnreadCount"] });
    },
  });
}

export function usePortalStartConversation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (initialMessage?: string) => {
      const { data } = await portalApi.post<PortalConversationDetail>(
        "/api/portal/messages/conversations",
        initialMessage ? { initialMessage } : {}
      );
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["portalConversations"] });
      queryClient.invalidateQueries({ queryKey: ["portalUnreadCount"] });
    },
  });
}
