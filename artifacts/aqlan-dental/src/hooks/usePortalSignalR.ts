
import { useEffect, useRef, useCallback, useState } from "react";
import { HubConnectionBuilder, HubConnection, LogLevel } from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";

// NAV-CEPH-FIX (Part 2): relative hub URL → the Next.js rewrite proxies /hubs/* to the backend
// (same-origin). The patient-portal SignalR connection passes the JWT via accessTokenFactory
// (not cookies), so cross-origin auth wasn't the issue — but a relative URL still avoids
// cross-origin WebSocket handshake complications in production (Vercel → Railway) and works
// identically in dev (the rewrite points to localhost:5000). @microsoft/signalr resolves
// relative URLs against the page origin.
const HUB_URL = "/hubs/messaging";

/**
 * Hook لإدارة اتصال SignalR لبوابة المريض.
 * يتصل تلقائياً عند تسجيل الدخول وينقطع عند تسجيل الخروج.
 * يستمع لأحداث الرسائل الجديدة ويحدّث React Query cache.
 */
export function usePortalSignalR(accessToken: string | null) {
  const connectionRef = useRef<HubConnection | null>(null);
  const queryClient = useQueryClient();
  const [isConnected, setIsConnected] = useState(false);

  const connect = useCallback(async () => {
    if (connectionRef.current?.state === "Connected") return;
    if (!accessToken) return;

    try {
      const connection = new HubConnectionBuilder()
        .withUrl(HUB_URL, {
          accessTokenFactory: () => accessToken,
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(LogLevel.Warning)
        .build();

      // رسالة جديدة
      connection.on("NewMessage", () => {
        queryClient.invalidateQueries({ queryKey: ["portalConversations"] });
        queryClient.invalidateQueries({ queryKey: ["portalConversation"] });
        queryClient.invalidateQueries({ queryKey: ["portalUnreadCount"] });
      });

      // تحديث عدد غير المقروء
      connection.on("UnreadCountUpdated", () => {
        queryClient.invalidateQueries({ queryKey: ["portalUnreadCount"] });
      });

      // إشعار جديد
      connection.on("NewNotification", () => {
        queryClient.invalidateQueries({ queryKey: ["portalUnreadCount"] });
      });

      // تعديل/حذف رسالة
      connection.on("MessageEdited", () => {
        queryClient.invalidateQueries({ queryKey: ["portalConversation"] });
      });
      connection.on("MessageDeleted", () => {
        queryClient.invalidateQueries({ queryKey: ["portalConversation"] });
      });

      // إعادة الاتصال
      connection.onreconnected(() => {
        setIsConnected(true);
        queryClient.invalidateQueries({ queryKey: ["portalConversations"] });
        queryClient.invalidateQueries({ queryKey: ["portalUnreadCount"] });
      });

      connection.onclose(() => {
        setIsConnected(false);
      });

      await connection.start();
      connectionRef.current = connection;
      setIsConnected(true);
    } catch (err) {
      console.warn("Portal SignalR connection failed, falling back to polling:", err);
      setIsConnected(false);
    }
  }, [accessToken, queryClient]);

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

  useEffect(() => {
    if (accessToken) {
      connect();
    } else {
      disconnect();
    }

    return () => {
      disconnect();
    };
  }, [accessToken, connect, disconnect]);

  return { isConnected };
}
