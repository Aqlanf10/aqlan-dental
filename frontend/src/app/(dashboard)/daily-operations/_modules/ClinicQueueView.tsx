"use client";

import { useState, useEffect, useCallback, useRef } from "react";
import api from "@/lib/api";
import {
  RefreshCw, Loader2, UserPlus, Stethoscope,
  MapPin, Clock, XCircle, PhoneCall,
  DoorOpen, Play, Square, Volume2,
  Wifi, WifiOff,
} from "lucide-react";
import { NAVY, BLUE } from "../_lib/constants";
import type { TodayJourneyItem } from "../_lib/constants";
import { useQueryClient } from "@tanstack/react-query";
import { HubConnectionBuilder, type HubConnection, LogLevel } from "@microsoft/signalr";
import { useAuthStore } from "@/stores/authStore";
import { buildAnnouncementText } from "@/lib/clinic-display-announcement";

/* ─── Types ────────────────────────────────────────────────────────────────── */
interface ClinicQueueItem {
  id: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  appointmentId?: string;
  appointmentTime?: string;
  visitId?: string;
  doctorName: string;
  doctorId?: string;
  roomName?: string;
  serviceName?: string;
  status: "Waiting" | "Called" | "InRoom" | "InProgress" | "Completed" | "Cancelled";
  statusArabic: string;
  calledAt?: string;
  inRoomAt?: string;
  startedAt?: string;
  completedAt?: string;
  createdAt: string;
}

interface DbRoom { id: string; arabicName: string; }
interface DoctorOption { id: string; name: string; specialty?: string; }

const STATUS_CONFIG: Record<string, { label: string; color: string; bg: string }> = {
  Waiting:    { label: "في الانتظار",  color: "#b45309", bg: "#fffbeb" },
  Called:     { label: "تم النداء",    color: "#1d4ed8", bg: "#eff6ff" },
  InRoom:     { label: "داخل الغرفة",  color: "#7c3aed", bg: "#f5f3ff" },
  InProgress: { label: "قيد المعالجة", color: "#059669", bg: "#ecfdf5" },
  Completed:  { label: "مكتمل",        color: "#6b7280", bg: "#f9fafb" },
  Cancelled:  { label: "ملغي",         color: "#dc2626", bg: "#fef2f2" },
};

/** Map ClinicQueueItem → TodayJourneyItem for context menu compatibility */
function queueItemToJourney(q: ClinicQueueItem): TodayJourneyItem {
  const statusMap: Record<string, string> = {
    Waiting: "Waiting", Called: "Called", InRoom: "InRoom",
    InProgress: "InProgress", Completed: "Completed", Cancelled: "Cancelled",
  };
  const nextActionMap: Record<string, string> = {
    Waiting: "CallPatient", Called: "EnterRoom", InRoom: "StartVisit",
    InProgress: "Checkout", Completed: "None", Cancelled: "None",
  };
  return {
    appointmentId: q.appointmentId ?? "",
    patientId: q.patientId,
    patientName: q.patientName,
    patientNumber: q.patientNumber,
    appointmentTime: q.appointmentTime ?? "",
    appointmentStatus: statusMap[q.status] ?? q.status,
    doctorId: q.doctorId ?? "",
    doctorName: q.doctorName,
    serviceName: q.serviceName,
    roomName: q.roomName,
    queueItemId: q.id,
    queueStatus: q.status,
    visitId: q.visitId,
    nextAction: nextActionMap[q.status] ?? "None",
  };
}

/* ─── Arabic Speech Utility ────────────────────────────────────────────────── */
function speakArabic(text: string): boolean {
  if (typeof window === "undefined" || !window.speechSynthesis) return false;
  try {
    window.speechSynthesis.cancel();
    const utterance = new SpeechSynthesisUtterance(text);
    utterance.lang = "ar-SA";
    utterance.rate = 0.9;
    utterance.pitch = 1;
    utterance.volume = 1;
    const voices = window.speechSynthesis.getVoices();
    const arabicVoice = voices.find((v) => v.lang.startsWith("ar"));
    if (arabicVoice) utterance.voice = arabicVoice;
    window.speechSynthesis.speak(utterance);
    return true;
  } catch { return false; }
}

const HUB_URL = process.env.NEXT_PUBLIC_API_URL
  ? `${process.env.NEXT_PUBLIC_API_URL}/hubs/messaging`
  : "/hubs/messaging";

/* ─── Component ────────────────────────────────────────────────────────────── */
interface ClinicQueueViewProps {
  searchQuery: string;
  onContextMenu?: (e: React.MouseEvent, item: TodayJourneyItem) => void;
}

export default function ClinicQueueView({ searchQuery, onContextMenu }: ClinicQueueViewProps) {
  const [items, setItems] = useState<ClinicQueueItem[]>([]);
  const [rooms, setRooms] = useState<DbRoom[]>([]);
  const [doctors, setDoctors] = useState<DoctorOption[]>([]);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [doctorFilter, setDoctorFilter] = useState("");
  const [signalrConnected, setSignalrConnected] = useState(false);
  const [voiceEnabled, setVoiceEnabled] = useState(true);

  const queryClient = useQueryClient();
  const { user } = useAuthStore();
  const connectionRef = useRef<HubConnection | null>(null);
  const audioRef = useRef<HTMLAudioElement | null>(null);

  // ── Fetch queue data ──
  const fetchQueue = useCallback(async () => {
    try {
      const params = new URLSearchParams();
      if (doctorFilter) params.set("doctorId", doctorFilter);
      const qs = params.toString() ? `?${params.toString()}` : "";
      const { data } = await api.get<ClinicQueueItem[]>(`/api/clinic-queue/today${qs}`);
      setItems(data);
    } catch { /* ignore */ } finally { setLoading(false); }
  }, [doctorFilter]);

  // ── Initial load + rooms/doctors ──
  useEffect(() => {
    fetchQueue();
    api.get<DbRoom[]>("/api/clinic-queue/rooms").then(r => setRooms(r.data)).catch(() => {});
    api.get<DoctorOption[]>("/api/doctors").then(r => setDoctors(r.data ?? [])).catch(() => {});
  }, [fetchQueue]);

  // ── SignalR real-time connection ──
  useEffect(() => {
    const token = localStorage.getItem("access_token");
    if (!user || !token) return;

    const connect = async () => {
      if (connectionRef.current?.state === "Connected") return;

      try {
        const connection = new HubConnectionBuilder()
          .withUrl(HUB_URL, { accessTokenFactory: () => token })
          .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
          .configureLogging(LogLevel.Warning)
          .build();

        // Queue updated — refresh data instantly
        connection.on("QueueUpdated", () => {
          fetchQueue();
          queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
          queryClient.invalidateQueries({ queryKey: ["patient-journey"] });
        });

        // Patient called — refresh + play notification sound + voice announcement
        connection.on("PatientCalled", (payload: { id?: string; patientId?: string; roomName?: string }) => {
          fetchQueue();
          queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
          queryClient.invalidateQueries({ queryKey: ["clinic-queue"] });

          // Play notification sound
          try {
            if (!audioRef.current) {
              audioRef.current = new Audio("/notify.mp3");
              audioRef.current.volume = 0.6;
            }
            audioRef.current.play().catch(() => {});
          } catch { /* ignore */ }

          // Voice announcement for the called patient
          if (voiceEnabled && payload?.id) {
            // Fetch the called patient info for voice announcement
            fetchQueue(); // data already refreshed above
          }
        });

        connection.onreconnected(() => {
          setSignalrConnected(true);
          fetchQueue();
        });
        connection.onclose(() => setSignalrConnected(false));

        await connection.start();
        connectionRef.current = connection;
        setSignalrConnected(true);
      } catch (err) {
        console.warn("ClinicQueueView SignalR failed:", err);
        setSignalrConnected(false);
      }
    };

    connect();

    return () => {
      if (connectionRef.current) {
        connectionRef.current.stop().catch(() => {});
        connectionRef.current = null;
      }
      setSignalrConnected(false);
    };
  }, [user, fetchQueue, queryClient, voiceEnabled]);

  // ── Fallback polling (only when SignalR is disconnected) ──
  useEffect(() => {
    if (signalrConnected) return; // No polling needed when SignalR is live
    const interval = setInterval(fetchQueue, 15000);
    return () => clearInterval(interval);
  }, [signalrConnected, fetchQueue]);

  // ── Voice announcement when a patient is called ──
  const lastCalledRef = useRef<string | null>(null);
  useEffect(() => {
    if (!voiceEnabled) return;
    const calledItem = items.find(i => i.status === "Called" && i.calledAt);
    if (!calledItem || !calledItem.calledAt) return;
    // Only announce if this is a new call (different from last announced)
    const callKey = `${calledItem.id}-${calledItem.calledAt}`;
    if (lastCalledRef.current === callKey) return;
    lastCalledRef.current = callKey;
    const text = buildAnnouncementText(calledItem.patientName, calledItem.patientNumber, calledItem.roomName || "");
    speakArabic(text);
  }, [items, voiceEnabled]);

  const queueAction = async (url: string, body?: object) => {
    setActionLoading(url);
    try {
      await api.post(url, body);
      fetchQueue();
    } catch { /* ignore */ }
    finally { setActionLoading(null); }
  };

  // ── Voice call button handler ──
  const handleCallWithVoice = async (item: ClinicQueueItem) => {
    setActionLoading(item.id);
    try {
      await api.post(`/api/clinic-queue/${item.id}/call`, { roomName: rooms[0]?.arabicName });
      // Voice announcement will be triggered by the SignalR PatientCalled event + useEffect above
    } catch { /* ignore */ }
    finally { setActionLoading(null); }
  };

  // ── Manual voice announce for a specific patient ──
  const announcePatient = (item: ClinicQueueItem) => {
    const text = buildAnnouncementText(item.patientName, item.patientNumber, item.roomName || "");
    speakArabic(text);
  };

  // Filter + search
  const activeItems = items.filter(i => !["Completed", "Cancelled"].includes(i.status));
  const completedItems = items.filter(i => ["Completed", "Cancelled"].includes(i.status));

  const filtered = (list: ClinicQueueItem[]) => {
    if (!searchQuery.trim()) return list;
    const q = searchQuery.trim().toLowerCase();
    return list.filter(i => i.patientName.toLowerCase().includes(q) || i.patientNumber.toLowerCase().includes(q));
  };

  const counts = {
    waiting: items.filter(i => i.status === "Waiting").length,
    called: items.filter(i => i.status === "Called").length,
    inRoom: items.filter(i => i.status === "InRoom").length,
    inProgress: items.filter(i => i.status === "InProgress").length,
  };

  return (
    <div className="flex flex-col h-full">
      {/* Stats + Filter bar */}
      <div className="flex-shrink-0 flex items-center gap-3 px-3 py-2 border-b" style={{ borderColor: "#f1f5f9", background: "#fff" }}>
        {/* Counter chips */}
        <div className="flex items-center gap-1.5">
          {[
            { label: "انتظار", count: counts.waiting, color: "#f59e0b" },
            { label: "نداء", count: counts.called, color: "#3b82f6" },
            { label: "غرفة", count: counts.inRoom, color: "#8b5cf6" },
            { label: "علاج", count: counts.inProgress, color: "#10b981" },
          ].map(c => (
            <span key={c.label} className="flex items-center gap-1 px-2 py-1 rounded-lg text-[11px] font-bold"
              style={{ background: c.color + "15", color: c.color }}>
              {c.count} {c.label}
            </span>
          ))}
        </div>

        <div className="flex-1" />

        {/* Voice toggle */}
        <button onClick={() => setVoiceEnabled(!voiceEnabled)}
          className="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-gray-100 transition"
          title={voiceEnabled ? "إيقاف النداء الصوتي" : "تفعيل النداء الصوتي"}>
          <Volume2 className="w-4 h-4" style={{ color: voiceEnabled ? "#16a34a" : "#94a3b8" }} />
        </button>

        {/* SignalR status indicator */}
        <div className="flex items-center gap-1" title={signalrConnected ? "متصل لحظياً (SignalR)" : "غير متصل — تحديث كل 15 ثانية"}>
          {signalrConnected ? (
            <Wifi className="w-4 h-4" style={{ color: "#16a34a" }} />
          ) : (
            <WifiOff className="w-4 h-4" style={{ color: "#f59e0b" }} />
          )}
        </div>

        {/* Doctor filter */}
        {doctors.length > 0 && (
          <select value={doctorFilter} onChange={e => setDoctorFilter(e.target.value)}
            className="text-xs font-semibold rounded-lg px-2 py-1.5 outline-none border-0"
            style={{ background: "#f5f7fa", color: NAVY, maxWidth: 120 }}>
            <option value="">كل الأطباء</option>
            {doctors.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
          </select>
        )}

        <button onClick={fetchQueue} className="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-gray-100 transition">
          <RefreshCw className={`w-4 h-4 ${loading ? "animate-spin" : ""}`} style={{ color: "#64748b" }} />
        </button>
      </div>

      {/* Queue list */}
      <div className="flex-1 overflow-auto p-3 space-y-2">
        {loading ? (
          <div className="flex items-center justify-center py-20">
            <Loader2 className="w-8 h-8 animate-spin" style={{ color: BLUE }} />
          </div>
        ) : filtered(activeItems).length === 0 && filtered(completedItems).length === 0 ? (
          <div className="text-center py-20" style={{ color: "#94a3b8" }}>
            <UserPlus className="w-12 h-12 mx-auto mb-3 opacity-30" />
            <p className="font-medium">لا توجد مرضى في الطابور</p>
          </div>
        ) : (
          <>
            {filtered(activeItems).map(item => {
              const cfg = STATUS_CONFIG[item.status] || STATUS_CONFIG.Waiting;
              const isLoading = actionLoading !== null && actionLoading.includes(item.id);
              return (
                <div key={item.id} className="rounded-xl border p-3 hover:shadow-sm transition cursor-context-menu"
                  style={{ background: "#fff", borderColor: "#e5e7eb" }}
                  onContextMenu={(e) => {
                    e.preventDefault();
                    onContextMenu?.(e, queueItemToJourney(item));
                  }}>
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 mb-1 flex-wrap">
                        <span className="font-bold text-sm" style={{ color: NAVY }}>{item.patientName}</span>
                        <span className="text-[11px] px-1.5 py-0.5 rounded-md font-mono" style={{ background: "#f1f5f9", color: "#64748b" }}>{item.patientNumber}</span>
                        {item.roomName && (
                          <span className="text-[11px] px-2 py-0.5 rounded-lg font-bold flex items-center gap-1" style={{ background: BLUE, color: "#fff" }}>
                            <MapPin className="w-3 h-3" />{item.roomName}
                          </span>
                        )}
                        <span className="text-[11px] px-2 py-0.5 rounded-full font-bold" style={{ background: cfg.bg, color: cfg.color }}>
                          {item.statusArabic || cfg.label}
                        </span>
                      </div>
                      <div className="flex items-center gap-3 text-xs flex-wrap" style={{ color: "#64748b" }}>
                        <span className="flex items-center gap-1"><Stethoscope className="w-3 h-3" />{item.doctorName || "—"}</span>
                        {item.appointmentTime && <span className="flex items-center gap-1"><Clock className="w-3 h-3" />{item.appointmentTime}</span>}
                      </div>
                    </div>
                    <div className="flex items-center gap-1.5 flex-shrink-0">
                      {item.status === "Waiting" && (
                        <>
                          <button onClick={() => handleCallWithVoice(item)}
                            disabled={isLoading} className="px-2.5 py-1 rounded-lg text-[11px] font-bold text-white transition hover:opacity-80 disabled:opacity-50 flex items-center gap-1" style={{ background: BLUE }}>
                            <PhoneCall className="w-3 h-3" />نداء
                          </button>
                          <button onClick={() => announcePatient(item)}
                            disabled={isLoading} className="px-2.5 py-1 rounded-lg text-[11px] font-bold text-white transition hover:opacity-80 disabled:opacity-50" style={{ background: "#059669" }} title="نداء صوتي فقط (بدون تغيير الحالة)">
                            <Volume2 className="w-3 h-3" />
                          </button>
                          <button onClick={() => queueAction(`/api/clinic-queue/${item.id}/cancel`)}
                            disabled={isLoading} className="px-2.5 py-1 rounded-lg text-[11px] font-bold text-white transition hover:opacity-80 disabled:opacity-50" style={{ background: "#ef4444" }}>
                            <XCircle className="w-3 h-3 inline ml-1" />إلغاء
                          </button>
                        </>
                      )}
                      {item.status === "Called" && (
                        <button onClick={() => queueAction(`/api/clinic-queue/${item.id}/enter-room`)}
                          disabled={isLoading} className="px-2.5 py-1 rounded-lg text-[11px] font-bold text-white transition hover:opacity-80 disabled:opacity-50" style={{ background: "#7c3aed" }}>
                          <DoorOpen className="w-3 h-3 inline ml-1" />إدخال
                        </button>
                      )}
                      {item.status === "InRoom" && (
                        <button onClick={() => queueAction(`/api/clinic-queue/${item.id}/start`)}
                          disabled={isLoading} className="px-2.5 py-1 rounded-lg text-[11px] font-bold text-white transition hover:opacity-80 disabled:opacity-50" style={{ background: "#059669" }}>
                          <Play className="w-3 h-3 inline ml-1" />بدء
                        </button>
                      )}
                      {item.status === "InProgress" && (
                        <button onClick={() => queueAction(`/api/clinic-queue/${item.id}/complete`)}
                          disabled={isLoading} className="px-2.5 py-1 rounded-lg text-[11px] font-bold text-white transition hover:opacity-80 disabled:opacity-50" style={{ background: NAVY }}>
                          <Square className="w-3 h-3 inline ml-1" />إنهاء
                        </button>
                      )}
                    </div>
                  </div>
                </div>
              );
            })}
            {filtered(completedItems).length > 0 && (
              <div className="pt-3">
                <div className="text-[11px] font-bold mb-2" style={{ color: "#94a3b8" }}>المنتهون ({filtered(completedItems).length})</div>
                {filtered(completedItems).map(item => {
                  const cfg = STATUS_CONFIG[item.status] || STATUS_CONFIG.Completed;
                  return (
                    <div key={item.id} className="rounded-xl border p-3 mb-2 opacity-60 cursor-context-menu" style={{ background: "#fff", borderColor: "#e5e7eb" }}
                      onContextMenu={(e) => {
                        e.preventDefault();
                        onContextMenu?.(e, queueItemToJourney(item));
                      }}>
                      <div className="flex items-center gap-2">
                        <span className="font-bold text-sm" style={{ color: NAVY }}>{item.patientName}</span>
                        <span className="text-[11px] px-2 py-0.5 rounded-full font-bold" style={{ background: cfg.bg, color: cfg.color }}>
                          {item.statusArabic || cfg.label}
                        </span>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}
