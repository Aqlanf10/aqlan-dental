import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import api from "@/lib/api";

interface NotificationItem {
  id: string;
  type?: string;
  title?: string;
  body?: string;
  isRead: boolean;
  createdAt: string;
  relatedEntity?: string;
  relatedId?: string;
}

interface NotificationsResponse {
  data: NotificationItem[];
  unreadCount: number;
}

/** Polling hook for notification unread count */
export function useNotificationUnreadCount() {
  return useQuery({
    queryKey: ["notificationUnreadCount"],
    queryFn: async () => {
      const { data } = await api.get<{ count: number }>("/api/notifications/unread-count");
      return data;
    },
    staleTime: 20_000,
    refetchInterval: 120_000, // تم تقليله من 25s لأن SignalR يدفع الإشعارات فورياً
    refetchOnWindowFocus: true,
  });
}

/** Hook for fetching notifications list */
export function useNotifications(page = 1, pageSize = 15) {
  return useQuery({
    queryKey: ["notifications", page, pageSize],
    queryFn: async () => {
      const { data } = await api.get<NotificationsResponse>(
        `/api/notifications?page=${page}&pageSize=${pageSize}`
      );
      return data;
    },
    staleTime: 10_000,
  });
}

/** Mark a single notification as read */
export function useMarkNotificationRead() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      await api.put(`/api/notifications/${id}/read`);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["notificationUnreadCount"] });
      queryClient.invalidateQueries({ queryKey: ["notifications"] });
    },
  });
}

/** Mark all notifications as read */
export function useMarkAllNotificationsRead() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      await api.put("/api/notifications/read-all");
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["notificationUnreadCount"] });
      queryClient.invalidateQueries({ queryKey: ["notifications"] });
    },
  });
}

/** Delete a notification */
export function useDeleteNotification() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      await api.delete(`/api/notifications/${id}`);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["notificationUnreadCount"] });
      queryClient.invalidateQueries({ queryKey: ["notifications"] });
    },
  });
}
