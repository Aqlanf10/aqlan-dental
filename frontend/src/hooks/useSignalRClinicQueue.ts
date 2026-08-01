"use client";

import { useEffect, useRef, useCallback, useState } from "react";
import { HubConnectionBuilder, type HubConnection, LogLevel } from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import { useAuthStore } from "@/stores/authStore";
import { getAccessToken } from "@/lib/api";

const HUB_URL = "/hubs/messaging";

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
  const [isConnected, setIsConnected] = useState(false);
  const audioRef = useRef<HTMLAudioElement | null>(null);

  const playNotification = useCallback(() => {
    try {
      if (!audioRef.current) {
        audioRef.current = new Audio("/notify.mp3");
        audioRef.current.volume = 0.6;
      }
      audioRef.current.play().catch(() => {
        // Browser may block autoplay until user interaction.
      });
    } catch {
      // ignore
    }
  }, []);

  const connect = useCallback(async () => {
    if (connectionRef.current?.state === "Connected") return;
    if (!getAccessToken()) return;

    try {
      const connection = new HubConnectionBuilder()
        .withUrl(HUB_URL, {
          accessTokenFactory: () => getAccessToken() ?? "",
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(LogLevel.Warning)
        .build();

      connection.on("QueueUpdated", () => {
        queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
        queryClient.invalidateQueries({ queryKey: ["clinic-queue"] });
        queryClient.invalidateQueries({ queryKey: ["patient-journey"] });
      });

      connection.on("PatientCalled", () => {
        queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
        queryClient.invalidateQueries({ queryKey: ["clinic-queue"] });
        if (playSoundOnPatientCalled) playNotification();
      });

      connection.on("JourneyUpdated", () => {
        queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
        queryClient.invalidateQueries({ queryKey: ["patient-journey"] });
        queryClient.invalidateQueries({ queryKey: ["clinic-queue"] });
        queryClient.invalidateQueries({ queryKey: ["finance"] });
      });

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
  }, [queryClient, playNotification, playSoundOnPatientCalled]);

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
    if (enabled && user && getAccessToken()) {
      void connect();
    } else {
      void disconnect();
    }
    return () => {
      void disconnect();
    };
  }, [enabled, user, connect, disconnect]);

  return {
    isConnected,
    playNotification,
  };
}
