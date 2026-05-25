"use client";

import { useEffect, useRef, useCallback, useState } from "react";
import { HubConnectionBuilder, HubConnection, LogLevel } from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import { useAuthStore } from "@/stores/authStore";

const HUB_URL = process.env.NEXT_PUBLIC_API_URL
  ? `${process.env.NEXT_PUBLIC_API_URL}/hubs/messaging`
  : "/hubs/messaging";

/**
 * Hook لإدارة اتصال SignalR لأحداث الطابور والعيادة.
 * يستمع لأحداث: نداء مريض، وصول مريض، تسليم للاستقبال، اكتمال زيارة.
 * يشغّل صوت تنبيه ويحدّث React Query cache تلقائياً.
 */
export function useSignalRClinicQueue() {
  const connectionRef = useRef<HubConnection | null>(null);
  const queryClient = useQueryClient();
  const { user } = useAuthStore();
  const [token, setToken] = useState<string | null>(null);
  const [isConnected, setIsConnected] = useState(false);

  // Sound ref — lazy loaded
  const audioRef = useRef<HTMLAudioElement | null>(null);

  const playNotification = useCallback((type: "queue" | "handoff" | "arrival" = "queue") => {
    try {
      if (!audioRef.current) {
        audioRef.current = new Audio("/notify.mp3");
        audioRef.current.volume = 0.6;
      }
      audioRef.current.play().catch(() => {
        // Browser may block autoplay until user interaction
      });
    } catch {
      // ignore
    }
  }, []);

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

      // ─── Clinic Queue Events ────────────────────────────────────────

      // مريض أضيف للطابور
      connection.on("QueueUpdated", () => {
        queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
        queryClient.invalidateQueries({ queryKey: ["clinic-queue"] });
        queryClient.invalidateQueries({ queryKey: ["patient-journey"] });
      });

      // نداء مريض من الطابور
      connection.on("PatientCalled", () => {
        queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
        queryClient.invalidateQueries({ queryKey: ["clinic-queue"] });
        playNotification("queue");
      });

      // وصول مريض جديد
      connection.on("PatientArrived", () => {
        queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
        queryClient.invalidateQueries({ queryKey: ["patient-journey"] });
        playNotification("arrival");
      });

      // تسليم من الطبيب للاستقبال (Handoff)
      connection.on("HandoffToReception", () => {
        queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
        queryClient.invalidateQueries({ queryKey: ["patient-journey"] });
        playNotification("handoff");
      });

      // اكتمال زيارة
      connection.on("VisitCompleted", () => {
        queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
        queryClient.invalidateQueries({ queryKey: ["patient-journey"] });
        queryClient.invalidateQueries({ queryKey: ["finance"] });
      });

      // إشعار عام (نستخدم نفس الحدث من messaging hub)
      connection.on("NewNotification", () => {
        queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
        queryClient.invalidateQueries({ queryKey: ["notificationUnreadCount"] });
      });

      // Reconnect
      connection.onreconnected(() => {
        setIsConnected(true);
        queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
        queryClient.invalidateQueries({ queryKey: ["clinic-queue"] });
      });

      connection.onclose(() => {
        setIsConnected(false);
      });

      await connection.start();
      connectionRef.current = connection;
      setIsConnected(true);
    } catch (err) {
      console.warn("Clinic Queue SignalR connection failed:", err);
      setIsConnected(false);
    }
  }, [token, queryClient, playNotification]);

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
    isConnected,
    playNotification,
  };
}
