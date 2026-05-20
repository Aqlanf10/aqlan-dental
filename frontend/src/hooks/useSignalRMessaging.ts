"use client";

import { useEffect, useRef, useCallback, useState } from "react";
import { HubConnectionBuilder, HubConnection, LogLevel } from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import { useAuthStore } from "@/stores/authStore";

const HUB_URL = process.env.NEXT_PUBLIC_API_URL
  ? `${process.env.NEXT_PUBLIC_API_URL}/hubs/messaging`
  : "/hubs/messaging";

/**
 * Hook لإدارة اتصال SignalR للمراسلة والإشعارات الفورية.
 * يتصل تلقائياً عند تسجيل الدخول وينقطع عند تسجيل الخروج.
 * يستمع لجميع أحداث الرسائل والإشعارات ويحدّث React Query cache.
 */
export function useSignalRMessaging() {
  const connectionRef = useRef<HubConnection | null>(null);
  const queryClient = useQueryClient();
  const { user } = useAuthStore();
  const [token, setToken] = useState<string | null>(null);
  const [isConnected, setIsConnected] = useState(false);

  // قراءة التوكن من localStorage عند التحميل
  useEffect(() => {
    const t = localStorage.getItem("access_token");
    setToken(t);
  }, [user]);

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

      // تعديل رسالة
      connection.on("MessageEdited", () => {
        queryClient.invalidateQueries({ queryKey: ["conversation"] });
        queryClient.invalidateQueries({ queryKey: ["conversations"] });
      });

      // حذف رسالة
      connection.on("MessageDeleted", () => {
        queryClient.invalidateQueries({ queryKey: ["conversation"] });
        queryClient.invalidateQueries({ queryKey: ["conversations"] });
      });

      // تحديث قائمة المحادثات (مثلاً إضافة مشارك جديد)
      connection.on("ConversationsUpdated", () => {
        queryClient.invalidateQueries({ queryKey: ["conversations"] });
      });

      // إعادة الاتصال
      connection.onreconnected(() => {
        setIsConnected(true);
        queryClient.invalidateQueries({ queryKey: ["conversations"] });
        queryClient.invalidateQueries({ queryKey: ["unreadCount"] });
        queryClient.invalidateQueries({ queryKey: ["notificationUnreadCount"] });
      });

      // انقطاع الاتصال
      connection.onclose(() => {
        setIsConnected(false);
      });

      await connection.start();
      connectionRef.current = connection;
      setIsConnected(true);
    } catch (err) {
      console.warn("SignalR connection failed, falling back to polling:", err);
      setIsConnected(false);
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
    setIsConnected(false);
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
    isConnected,
  };
}
