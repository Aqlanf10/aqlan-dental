"use client";

import { useEffect, useRef, useCallback, useState } from "react";
import { HubConnectionBuilder, HubConnection, LogLevel } from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import { useAuthStore } from "@/stores/authStore";
import { getAccessToken } from "@/lib/api";

const HUB_URL = "/hubs/messaging";

/**
 * Hook لإدارة اتصال SignalR للمراسلة والإشعارات الفورية.
 * يتصل تلقائياً عند تسجيل الدخول وينقطع عند تسجيل الخروج.
 */
export function useSignalRMessaging() {
  const connectionRef = useRef<HubConnection | null>(null);
  const queryClient = useQueryClient();
  const { user } = useAuthStore();
  const [isConnected, setIsConnected] = useState(false);

  const connect = useCallback(async () => {
    if (connectionRef.current?.state === "Connected") return;
    if (!getAccessToken()) return;

    try {
      const connection = new HubConnectionBuilder()
        .withUrl(HUB_URL, {
          // Resolve at connection/reconnection time so a rotated in-memory access
          // token is used without ever persisting it in localStorage.
          accessTokenFactory: () => getAccessToken() ?? "",
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(LogLevel.Warning)
        .build();

      connection.on("NewMessage", () => {
        queryClient.invalidateQueries({ queryKey: ["conversations"] });
        queryClient.invalidateQueries({ queryKey: ["conversation"] });
        queryClient.invalidateQueries({ queryKey: ["unreadCount"] });
      });

      connection.on("UnreadCountUpdated", () => {
        queryClient.invalidateQueries({ queryKey: ["unreadCount"] });
      });

      connection.on("NewNotification", () => {
        queryClient.invalidateQueries({ queryKey: ["notificationUnreadCount"] });
        queryClient.invalidateQueries({ queryKey: ["notifications"] });
      });

      connection.on("MessageEdited", () => {
        queryClient.invalidateQueries({ queryKey: ["conversation"] });
        queryClient.invalidateQueries({ queryKey: ["conversations"] });
      });

      connection.on("MessageDeleted", () => {
        queryClient.invalidateQueries({ queryKey: ["conversation"] });
        queryClient.invalidateQueries({ queryKey: ["conversations"] });
      });

      connection.on("ConversationsUpdated", () => {
        queryClient.invalidateQueries({ queryKey: ["conversations"] });
      });

      connection.onreconnected(() => {
        setIsConnected(true);
        queryClient.invalidateQueries({ queryKey: ["conversations"] });
        queryClient.invalidateQueries({ queryKey: ["unreadCount"] });
        queryClient.invalidateQueries({ queryKey: ["notificationUnreadCount"] });
      });

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
  }, [queryClient]);

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

  const joinConversation = useCallback(async (conversationId: string) => {
    if (connectionRef.current?.state === "Connected") {
      try {
        await connectionRef.current.invoke("JoinConversation", conversationId);
      } catch {
        // ignore — will use polling fallback
      }
    }
  }, []);

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
    if (user && getAccessToken()) {
      void connect();
    } else {
      void disconnect();
    }

    return () => {
      void disconnect();
    };
  }, [user, connect, disconnect]);

  return {
    joinConversation,
    leaveConversation,
    isConnected,
  };
}
