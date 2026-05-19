"use client";

import { useEffect, useRef, useCallback } from "react";
import { HubConnectionBuilder, HubConnection, LogLevel } from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import { useAuthStore } from "@/stores/authStore";

const HUB_URL = process.env.NEXT_PUBLIC_API_URL
  ? `${process.env.NEXT_PUBLIC_API_URL}/hubs/messaging`
  : "/hubs/messaging";

/**
 * Hook لإدارة اتصال SignalR للمراسلة والإشعارات الفورية.
 * يتصل تلقائياً عند تسجيل الدخول وينقطع عند تسجيل الخروج.
 * يستمع لأحداث الرسائل الجديدة والإشعارات ويحدّث React Query cache.
 */
export function useSignalRMessaging() {
  const connectionRef = useRef<HubConnection | null>(null);
  const queryClient = useQueryClient();
  const { user, token } = useAuthStore();

  const connect = useCallback(async () => {
    if (connectionRef.current?.state === "Connected") return;
    if (!token) return;

    try {
      const connection = new HubConnectionBuilder()
        .withUrl(HUB_URL, {
          accessTokenFactory: () => token,
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(LogLevel.Warning)
        .build();

      // ─── استماع الأحداث ────────────────────────────────────────────

      // رسالة جديدة في محادثة
      connection.on("NewMessage", () => {
        // تحديث cache المحادثة المفتوحة وقائمة المحادثات
        queryClient.invalidateQueries({ queryKey: ["conversations"] });
        queryClient.invalidateQueries({ queryKey: ["conversation"] });
        queryClient.invalidateQueries({ queryKey: ["unreadCount"] });
      });

      // تحديث عدد غير المقروء
      connection.on("UnreadCountUpdated", () => {
        queryClient.invalidateQueries({ queryKey: ["unreadCount"] });
      });

      // إشعار جديد
      connection.on("NewNotification", () => {
        queryClient.invalidateQueries({ queryKey: ["notificationUnreadCount"] });
        queryClient.invalidateQueries({ queryKey: ["notifications"] });
      });

      // إعادة الاتصال
      connection.onreconnected(() => {
        queryClient.invalidateQueries({ queryKey: ["conversations"] });
        queryClient.invalidateQueries({ queryKey: ["unreadCount"] });
        queryClient.invalidateQueries({ queryKey: ["notificationUnreadCount"] });
      });

      await connection.start();
      connectionRef.current = connection;
    } catch (err) {
      console.warn("SignalR connection failed, falling back to polling:", err);
    }
  }, [token, queryClient]);

  const disconnect = useCallback(async () => {
    if (connectionRef.current) {
      try {
        await connectionRef.current.stop();
      } catch {
        // ignore
      }
      connectionRef.current = null;
    }
  }, []);

  // الانضمام لمجموعة محادثة
  const joinConversation = useCallback(async (conversationId: string) => {
    if (connectionRef.current?.state === "Connected") {
      try {
        await connectionRef.current.invoke("JoinConversation", conversationId);
      } catch {
        // ignore — will use polling fallback
      }
    }
  }, []);

  // مغادرة مجموعة محادثة
  const leaveConversation = useCallback(async (conversationId: string) => {
    if (connectionRef.current?.state === "Connected") {
      try {
        await connectionRef.current.invoke("LeaveConversation", conversationId);
      } catch {
        // ignore
      }
    }
  }, []);

  useEffect(() => {
    if (user && token) {
      connect();
    } else {
      disconnect();
    }

    return () => {
      disconnect();
    };
  }, [user, token, connect, disconnect]);

  return {
    joinConversation,
    leaveConversation,
    isConnected: connectionRef.current?.state === "Connected",
  };
}
