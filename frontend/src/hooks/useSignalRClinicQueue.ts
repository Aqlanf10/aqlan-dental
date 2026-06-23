"use client";

import { useEffect, useRef, useCallback, useState } from "react";
import { HubConnectionBuilder, type HubConnection, LogLevel } from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import { useAuthStore } from "@/stores/authStore";

const HUB_URL = process.env.NEXT_PUBLIC_API_URL
  ? `${process.env.NEXT_PUBLIC_API_URL}/hubs/messaging`
  : "/hubs/messaging";

/**
 * Hook لإدارة اتصال SignalR لأحداث الطابور والعيادة.
 * يستمع لأحداث: نداء مريض، تحديث الطابور.
 * إشعارات النظام العامة تُدار من useSignalRMessaging داخل dashboard layout
 * حتى لا تُعالَج مرتين عند فتح صفحات التشغيل اليومي أو عيادة الطبيب.
 * يشغّل صوت تنبيه ويحدّث React Query cache تلقائياً.
 */
interface UseSignalRClinicQueueOptions {
  enabled?: boolean;
  playSoundOnPatientCalled?: boolean;
}

export function useSignalRClinicQueue(options: UseSignalRClinicQueueOptions = {}) {
  const enabled = options.enabled ?? true;
  const playSoundOnPatientCalled = options.playSoundOnPatientCalled ?? false;
  const connectionRef = useRef<HubConnection | null>(null);
  const queryClient = useQueryClient();
  const { user } = useAuthStore();
  const [token, setToken] = useState<string | null>(null);
  const [isConnected, setIsConnected] = useState(false);

  // Sound ref — lazy loaded
  const audioRef = useRef<HTMLAudioElement | null>(null);

  const playNotification = useCallback(() => {
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
        if (playSoundOnPatientCalled) {
          playNotification();
        }
      });

      // تحديث رحلة المريض (وصول/طابور/زيارة/تحصيل/خروج/موعد/دفع) — يُدفع من
      // CheckoutService + AppointmentsController + VisitsController + PaymentsController
      // بعد كل عملية تعديل ناجحة. يُلغي صلاحية كل الاستعلامات المرتبطة فورًا حتى
      // تُعاد جلب البيانات الملتزمة دون انتظار polling.
      connection.on("JourneyUpdated", () => {
        queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
        queryClient.invalidateQueries({ queryKey: ["patient-journey"] });
        queryClient.invalidateQueries({ queryKey: ["clinic-queue"] });
        queryClient.invalidateQueries({ queryKey: ["finance"] });
      });

      // Reconnect
      connection.onreconnected(() => {
        setIsConnected(true);
        queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
        queryClient.invalidateQueries({ queryKey: ["clinic-queue"] });
        queryClient.invalidateQueries({ queryKey: ["patient-journey"] });
        queryClient.invalidateQueries({ queryKey: ["finance"] });
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
  }, [token, queryClient, playNotification, playSoundOnPatientCalled]);

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
    if (enabled && user && token) {
      connect();
    } else {
      disconnect();
    }
    return () => {
      disconnect();
    };
  }, [enabled, user, token, connect, disconnect]);

  return {
    isConnected,
    playNotification,
  };
}
