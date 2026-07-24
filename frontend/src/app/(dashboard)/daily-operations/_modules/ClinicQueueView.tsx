"use client";

import { useState, useEffect, useCallback, useRef } from "react";
import api from "@/lib/api";
import { extractErrorMessage } from "@/lib/errors";
import { useDoctors } from "@/hooks/useDoctors";
import {
  RefreshCw, Loader2, UserPlus, Stethoscope,
  MapPin, Clock, XCircle, PhoneCall,
  DoorOpen, Play, Square, Volume2,
  Wifi, WifiOff, ArrowUp, ArrowDown,
  ChevronDown, ChevronUp, BarChart3, Send, AlertTriangle,
} from "lucide-react";
import { NAVY, BLUE } from "../_lib/constants";
import type { TodayJourneyItem } from "../_lib/constants";
import { useQueryClient } from "@tanstack/react-query";
import { HubConnectionBuilder, type HubConnection, LogLevel } from "@microsoft/signalr";
import { useAuthStore } from "@/stores/authStore";
import { buildAnnouncementText } from "@/lib/clinic-display-announcement";
import { buildQueueReorderPayload } from "@/lib/clinicQueueReorder";

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
  status: "Waiting" | "Called" | "InRoom" | "InProgress" | "Completed" | "Cancelled" | "NoShow";
  statusArabic: string;
  priority: "Normal" | "Urgent" | "VIP" | "Emergency";
  priorityArabic: string;
  sortOrder: number;
  recallCount: number;
  calledAt?: string;
  inRoomAt?: string;
  startedAt?: string;
  completedAt?: string;
  noShowAt?: string;
  notes?: string;
  position?: number;
  estimatedWaitMinutes?: number;
  createdAt: string;
}

interface DbRoom { id: string; arabicName: string; }
interface DoctorOption { id: string; name: string; specialty?: string; }

interface QueueAnalytics {
  avgServiceMinutes?: number;
  avgWaitMinutes?: number;
  completionRate?: number;
  noShowRate?: number;
}

const STATUS_CONFIG: Record<string, { label: string; color: string; bg: string }> = {
  Waiting:    { label: "في الانتظار",  color: "#b45309", bg: "#fffbeb" },
  Called:     { label: "تم النداء",    color: "#1d4ed8", bg: "#eff6ff" },
  InRoom:     { label: "داخل الغرفة",  color: "#7c3aed", bg: "#f5f3ff" },
  InProgress: { label: "قيد المعالجة", color: "#059669", bg: "#ecfdf5" },
  Completed:  { label: "مكتمل",        color: "#6b7280", bg: "#f9fafb" },
  Cancelled:  { label: "ملغي",         color: "#dc2626", bg: "#fef2f2" },
  NoShow:     { label: "لم يحضر",      color: "#9333ea", bg: "#faf5ff" },
};

const PRIORITY_CONFIG: Record<string, { label: string; color: string; bg: string; icon?: string }> = {
  Normal:    { label: "عادي",   color: "#6b7280", bg: "#f3f4f6" },
  Urgent:    { label: "عاجل",   color: "#d97706", bg: "#fffbeb" },
  VIP:       { label: "مميز",   color: "#7c3aed", bg: "#f5f3ff" },
  Emergency: { label: "حالة إسعافية", color: "#dc2626", bg: "#fef2f2" },
};

/** Map ClinicQueueItem → TodayJourneyItem for context menu compatibility */
function queueItemToJourney(q: ClinicQueueItem): TodayJourneyItem {
  const statusMap: Record<string, string> = {
    Waiting: "Waiting", Called: "Called", InRoom: "InRoom",
    InProgress: "InProgress", Completed: "Completed", Cancelled: "Cancelled", NoShow: "NoShow",
  };
  const nextActionMap: Record<string, string> = {
    Waiting: "CallPatient", Called: "EnterRoom", InRoom: "StartVisit",
    InProgress: "Checkout", Completed: "None", Cancelled: "None", NoShow: "None",
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

/** Convert a number to Arabic-Indic digits */
function toArabicNum(n: number): string {
  return n.toString().replace(/\d/g, d => "٠١٢٣٤٥٦٧٨٩"[parseInt(d)]);
}

// NAV-CEPH-FIX (Part 2): relative hub URL → Next.js rewrite proxies /hubs/* to the backend
// (same-origin). See useSignalRClinicQueue.ts for the full rationale.
const HUB_URL = "/hubs/messaging";

/* ─── Component ────────────────────────────────────────────────────────────── */
interface ClinicQueueViewProps {
  searchQuery: string;
  onContextMenu?: (e: React.MouseEvent, item: TodayJourneyItem) => void;
  onOpenSidePanel?: (item: TodayJourneyItem) => void;
}

export default function ClinicQueueView({ searchQuery, onContextMenu, onOpenSidePanel }: ClinicQueueViewProps) {
  const [items, setItems] = useState<ClinicQueueItem[]>([]);
  const [rooms, setRooms] = useState<DbRoom[]>([]);
  // FE-13: useDoctors() replaces useState + fetch.
  const { data: doctors = [] } = useDoctors();
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [doctorFilter, setDoctorFilter] = useState("");
  const [signalrConnected, setSignalrConnected] = useState(false);
  const [voiceEnabled, setVoiceEnabled] = useState(true);

  // Analytics
  const [analytics, setAnalytics] = useState<QueueAnalytics | null>(null);
  const [showAnalytics, setShowAnalytics] = useState(false);

  // Priority dropdown open state (keyed by item id)
  const [priorityDropdownOpen, setPriorityDropdownOpen] = useState<string | null>(null);

  const queryClient = useQueryClient();
  const { user } = useAuthStore();
  const connectionRef = useRef<HubConnection | null>(null);
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const priorityDropdownRef = useRef<HTMLDivElement | null>(null);
  const queueControllerRef = useRef<AbortController | null>(null);
  const queueRequestSequenceRef = useRef(0);
  // SEQ-38: when a mutation succeeded but the follow-up queue reload failed,
  // the action stays locked (the card still shows stale status — re-enabling
  // would allow a duplicate/invalid second submit). The next SUCCESSFUL queue
  // fetch (e.g. the retry button) clears the lock along with the error.
  const pendingSyncLockRef = useRef(false);

  // ── Close priority dropdown on outside click ──
  useEffect(() => {
    const handleClick = (e: MouseEvent) => {
      if (priorityDropdownOpen && priorityDropdownRef.current && !priorityDropdownRef.current.contains(e.target as Node)) {
        setPriorityDropdownOpen(null);
      }
    };
    document.addEventListener("mousedown", handleClick);
    return () => document.removeEventListener("mousedown", handleClick);
  }, [priorityDropdownOpen]);

  // ── Fetch queue data ──
  const fetchQueue = useCallback(async (initialLoad = false) => {
    const requestId = ++queueRequestSequenceRef.current;
    queueControllerRef.current?.abort();
    const controller = new AbortController();
    queueControllerRef.current = controller;

    if (initialLoad) setLoading(true);
    else setRefreshing(true);

    try {
      const params = new URLSearchParams();
      if (doctorFilter) params.set("doctorId", doctorFilter);
      const qs = params.toString() ? `?${params.toString()}` : "";
      const { data } = await api.get<ClinicQueueItem[]>(`/api/clinic-queue/today${qs}`, {
        signal: controller.signal,
      });

      if (controller.signal.aborted || requestId !== queueRequestSequenceRef.current) return false;
      setItems(data);
      setLoadError(null);
      if (pendingSyncLockRef.current) {
        pendingSyncLockRef.current = false;
        setActionLoading(null);
      }
      return true;
    } catch (error) {
      if (controller.signal.aborted || requestId !== queueRequestSequenceRef.current) return false;
      setLoadError(extractErrorMessage(error, "تعذر تحميل قائمة الانتظار"));
      return false;
    } finally {
      if (requestId === queueRequestSequenceRef.current) {
        setLoading(false);
        setRefreshing(false);
        if (queueControllerRef.current === controller) queueControllerRef.current = null;
      }
    }
  }, [doctorFilter]);

  // ── Fetch analytics ──
  const fetchAnalytics = useCallback(async () => {
    try {
      const params = new URLSearchParams();
      if (doctorFilter) params.set("doctorId", doctorFilter);
      const qs = params.toString() ? `?${params.toString()}` : "";
      const { data } = await api.get<QueueAnalytics>(`/api/clinic-queue/analytics${qs}`);
      setAnalytics(data);
    } catch { /* ignore */ }
  }, [doctorFilter]);

  // ── Initial load + rooms ──
  useEffect(() => {
    setItems([]);
    setLoadError(null);
    fetchQueue(true);
    fetchAnalytics();
    api.get<DbRoom[]>("/api/clinic-queue/rooms").then(r => setRooms(r.data)).catch(() => {});
  }, [fetchQueue, fetchAnalytics]);

  useEffect(() => {
    return () => {
      queueRequestSequenceRef.current += 1;
      queueControllerRef.current?.abort();
      queueControllerRef.current = null;
    };
  }, []);

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
          fetchAnalytics();
          queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
          queryClient.invalidateQueries({ queryKey: ["patient-journey"] });
        });

        // Patient called — one refresh is sufficient; the items effect handles voice announcement.
        connection.on("PatientCalled", () => {
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
        });

        connection.onreconnected(() => {
          setSignalrConnected(true);
          fetchQueue();
          fetchAnalytics();
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
  }, [user, fetchQueue, fetchAnalytics, queryClient, voiceEnabled]);

  // ── Fallback polling (only when SignalR is disconnected) ──
  useEffect(() => {
    if (signalrConnected) return; // No polling needed when SignalR is live
    const interval = setInterval(() => { fetchQueue(); fetchAnalytics(); }, 15000);
    return () => clearInterval(interval);
  }, [signalrConnected, fetchQueue, fetchAnalytics]);

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

  const syncQueueViewsAfterMutation = useCallback(async () => {
    queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
    queryClient.invalidateQueries({ queryKey: ["clinic-queue"] });
    // Server truth = the queue reload; analytics failing must not hold the lock.
    const [queueSynced] = await Promise.all([fetchQueue(), fetchAnalytics()]);
    return queueSynced === true;
  }, [queryClient, fetchQueue, fetchAnalytics]);

  const queueAction = async (url: string, body?: object, method: "post" | "patch" = "post") => {
    setActionLoading(url);
    try {
      if (method === "patch") {
        await api.patch(url, body);
      } else {
        await api.post(url, body);
      }
      if (await syncQueueViewsAfterMutation()) setActionLoading(null);
      else pendingSyncLockRef.current = true; // stay locked until server truth arrives
    } catch {
      /* SEQ-36 surfaces the mutation failure */
      setActionLoading(null);
    }
  };

  // ── Voice call button handler ──
  const handleCallWithVoice = async (item: ClinicQueueItem) => {
    setActionLoading(item.id);
    try {
      await api.post(`/api/clinic-queue/${item.id}/call`, { roomName: rooms[0]?.arabicName });
      if (await syncQueueViewsAfterMutation()) setActionLoading(null);
      else pendingSyncLockRef.current = true; // stay locked until server truth arrives
    } catch {
      /* SEQ-36 surfaces the mutation failure */
      setActionLoading(null);
    }
  };

  // ── Manual voice announce for a specific patient ──
  const announcePatient = (item: ClinicQueueItem) => {
    const text = buildAnnouncementText(item.patientName, item.patientNumber, item.roomName || "");
    speakArabic(text);
  };

  // ── Move item up/down in waiting queue ──
  const handleReorder = async (item: ClinicQueueItem, direction: "up" | "down") => {
    const waitingItems = items
      .filter(i => i.status === "Waiting")
      .sort((a, b) => a.sortOrder - b.sortOrder);
    const idx = waitingItems.findIndex(i => i.id === item.id);
    if (idx < 0) return;
    if (direction === "up" && idx === 0) return;
    if (direction === "down" && idx === waitingItems.length - 1) return;

    const swapIdx = direction === "up" ? idx - 1 : idx + 1;
    const swapItem = waitingItems[swapIdx];

    // Optimistically swap sort orders locally
    const newItems = [...items];
    const itemIdx = newItems.findIndex(i => i.id === item.id);
    const swapItemIdx = newItems.findIndex(i => i.id === swapItem.id);
    if (itemIdx >= 0 && swapItemIdx >= 0) {
      const tempSort = newItems[itemIdx].sortOrder;
      newItems[itemIdx] = { ...newItems[itemIdx], sortOrder: newItems[swapItemIdx].sortOrder };
      newItems[swapItemIdx] = { ...newItems[swapItemIdx], sortOrder: tempSort };
      setItems(newItems);
    }

    try {
      await api.post(
        "/api/clinic-queue/reorder",
        buildQueueReorderPayload(
          { id: item.id, sortOrder: swapItem.sortOrder },
          { id: swapItem.id, sortOrder: item.sortOrder },
        ),
      );
      fetchQueue(); // sync with backend
    } catch {
      fetchQueue(); // revert on error
    }
  };

  // ── Change priority ──
  const handlePriorityChange = async (item: ClinicQueueItem, newPriority: string) => {
    setPriorityDropdownOpen(null);
    if (newPriority === item.priority) return;
    await queueAction(`/api/clinic-queue/${item.id}/priority`, { priority: newPriority }, "patch");
  };

  // ── Send SMS notification ──
  const handleNotify = async (item: ClinicQueueItem) => {
    await queueAction(`/api/clinic-queue/${item.id}/notify`);
  };

  // Filter + search
  const activeItems = items.filter(i => !["Completed", "Cancelled", "NoShow"].includes(i.status));
  const noShowItems = items.filter(i => i.status === "NoShow");
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
    noShow: items.filter(i => i.status === "NoShow").length,
  };

  // Waiting items sorted by sortOrder for up/down logic
  const waitingSorted = items
    .filter(i => i.status === "Waiting")
    .sort((a, b) => a.sortOrder - b.sortOrder);

  const hasVisibleQueueItems =
    filtered(activeItems).length > 0 ||
    filtered(completedItems).length > 0 ||
    filtered(noShowItems).length > 0;

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
            { label: "غياب", count: counts.noShow, color: "#9333ea" },
          ].map(c => (
            <span key={c.label} className="flex items-center gap-1 px-2 py-1 rounded-lg text-[11px] font-bold"
              style={{ background: c.color + "15", color: c.color }}>
              {c.count} {c.label}
            </span>
          ))}
        </div>

        <div className="flex-1" />

        {/* Analytics toggle */}
        <button onClick={() => { setShowAnalytics(!showAnalytics); if (!showAnalytics && !analytics) fetchAnalytics(); }}
          className="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-gray-100 transition"
          title="إحصائيات الانتظار">
          <BarChart3 className="w-4 h-4" style={{ color: showAnalytics ? BLUE : "#94a3b8" }} />
        </button>

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

        <button
          type="button"
          aria-label="تحديث قائمة الانتظار"
          onClick={() => fetchQueue()}
          disabled={loading || refreshing}
          className="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-gray-100 transition disabled:opacity-60"
        >
          <RefreshCw className={`w-4 h-4 ${loading || refreshing ? "animate-spin" : ""}`} style={{ color: "#64748b" }} />
        </button>
      </div>

      {/* Analytics panel (collapsible) */}
      {showAnalytics && (
        <div className="flex-shrink-0 px-3 py-2 border-b" style={{ borderColor: "#f1f5f9", background: "#f8fafc" }}>
          <div className="flex items-center gap-2 mb-1">
            <BarChart3 className="w-3.5 h-3.5" style={{ color: BLUE }} />
            <span className="text-[11px] font-bold" style={{ color: NAVY }}>إحصائيات الانتظار</span>
            <button onClick={() => setShowAnalytics(false)} className="mr-auto">
              <ChevronUp className="w-3.5 h-3.5" style={{ color: "#94a3b8" }} />
            </button>
          </div>
          <div className="grid grid-cols-4 gap-2">
            <div className="rounded-lg p-2 text-center" style={{ background: "#fff" }}>
              <div className="text-[10px] mb-0.5" style={{ color: "#64748b" }}>متوسط وقت الخدمة</div>
              <div className="text-sm font-bold" style={{ color: NAVY }}>
                {analytics?.avgServiceMinutes != null ? `${toArabicNum(Math.round(analytics.avgServiceMinutes))} د` : "—"}
              </div>
            </div>
            <div className="rounded-lg p-2 text-center" style={{ background: "#fff" }}>
              <div className="text-[10px] mb-0.5" style={{ color: "#64748b" }}>متوسط وقت الانتظار</div>
              <div className="text-sm font-bold" style={{ color: NAVY }}>
                {analytics?.avgWaitMinutes != null ? `${toArabicNum(Math.round(analytics.avgWaitMinutes))} د` : "—"}
              </div>
            </div>
            <div className="rounded-lg p-2 text-center" style={{ background: "#fff" }}>
              <div className="text-[10px] mb-0.5" style={{ color: "#64748b" }}>نسبة الإكمال</div>
              <div className="text-sm font-bold" style={{ color: "#059669" }}>
                {analytics?.completionRate != null ? `${toArabicNum(Math.round(analytics.completionRate))}%` : "—"}
              </div>
            </div>
            <div className="rounded-lg p-2 text-center" style={{ background: "#fff" }}>
              <div className="text-[10px] mb-0.5" style={{ color: "#64748b" }}>نسبة عدم الحضور</div>
              <div className="text-sm font-bold" style={{ color: "#9333ea" }}>
                {analytics?.noShowRate != null ? `${toArabicNum(Math.round(analytics.noShowRate))}%` : "—"}
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Queue list */}
      <div className="flex-1 overflow-auto p-3 space-y-2">
        {!loading && loadError && (
          <div
            role="alert"
            className="flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 px-3 py-3 text-sm text-red-700"
          >
            <AlertTriangle className="mt-0.5 h-4 w-4 flex-shrink-0" />
            <div className="min-w-0 flex-1">
              <p className="font-bold">{loadError}</p>
              <p className="mt-0.5 text-xs text-red-600">
                {items.length > 0
                  ? "تم إبقاء بيانات الانتظار المحملة سابقًا حتى ينجح التحديث."
                  : "تعذر التأكد من قائمة الانتظار الحالية؛ لا تُعامل الشاشة كقائمة فارغة."}
              </p>
            </div>
            <button
              type="button"
              onClick={() => fetchQueue(items.length === 0)}
              disabled={loading || refreshing}
              className="inline-flex items-center gap-1 rounded-lg border border-red-200 bg-white px-2.5 py-1.5 text-xs font-bold disabled:opacity-60"
            >
              <RefreshCw className={`h-3.5 w-3.5 ${loading || refreshing ? "animate-spin" : ""}`} />
              إعادة المحاولة
            </button>
          </div>
        )}

        {!loading && refreshing && items.length > 0 && (
          <div className="flex items-center justify-center gap-2 py-1 text-xs font-semibold text-slate-500">
            <Loader2 className="h-3.5 w-3.5 animate-spin" />
            جارٍ تحديث قائمة الانتظار...
          </div>
        )}

        {loading ? (
          <div className="flex items-center justify-center py-20">
            <Loader2 className="w-8 h-8 animate-spin" style={{ color: BLUE }} />
          </div>
        ) : loadError && !hasVisibleQueueItems ? null : !hasVisibleQueueItems ? (
          <div className="text-center py-20" style={{ color: "#94a3b8" }}>
            <UserPlus className="w-12 h-12 mx-auto mb-3 opacity-30" />
            <p className="font-medium">لا يوجد مرضى في الانتظار</p>
          </div>
        ) : (
          <>
            {filtered(activeItems).map(item => {
              const cfg = STATUS_CONFIG[item.status] || STATUS_CONFIG.Waiting;
              const priCfg = PRIORITY_CONFIG[item.priority] || PRIORITY_CONFIG.Normal;
              const isLoading = actionLoading !== null && actionLoading.includes(item.id);
              const waitingIdx = waitingSorted.findIndex(i => i.id === item.id);
              const isWaiting = item.status === "Waiting";
              return (
                <div key={item.id} className="rounded-xl border p-3 hover:shadow-sm transition cursor-pointer"
                  style={{ background: "#fff", borderColor: item.priority === "Emergency" ? "#fecaca" : item.priority === "Urgent" ? "#fde68a" : "#e5e7eb" }}
                  onContextMenu={(e) => {
                    e.preventDefault();
                    onContextMenu?.(e, queueItemToJourney(item));
                  }}
                  onClick={() => onOpenSidePanel?.(queueItemToJourney(item))}>
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 mb-1 flex-wrap">
                        {isWaiting && (
                          <span className="flex items-center gap-0.5">
                            <button
                              onClick={(e) => { e.stopPropagation(); handleReorder(item, "up"); }}
                              disabled={waitingIdx <= 0}
                              className="w-5 h-5 rounded flex items-center justify-center hover:bg-gray-100 transition disabled:opacity-30"
                              title="نقل لأعلى"
                            >
                              <ArrowUp className="w-3 h-3" style={{ color: NAVY }} />
                            </button>
                            <button
                              onClick={(e) => { e.stopPropagation(); handleReorder(item, "down"); }}
                              disabled={waitingIdx < 0 || waitingIdx >= waitingSorted.length - 1}
                              className="w-5 h-5 rounded flex items-center justify-center hover:bg-gray-100 transition disabled:opacity-30"
                              title="نقل لأسفل"
                            >
                              <ArrowDown className="w-3 h-3" style={{ color: NAVY }} />
                            </button>
                          </span>
                        )}

                        <span className="font-bold text-sm" style={{ color: NAVY }}>{item.patientName}</span>
                        <span className="text-[11px] px-1.5 py-0.5 rounded-md font-mono" style={{ background: "#f1f5f9", color: "#64748b" }}>{item.patientNumber}</span>

                        {item.priority !== "Normal" && (
                          <span className="text-[10px] px-1.5 py-0.5 rounded-full font-bold flex items-center gap-0.5" style={{ background: priCfg.bg, color: priCfg.color }}>
                            {item.priority === "Emergency" && <AlertTriangle className="w-2.5 h-2.5" />}
                            {priCfg.label}
                          </span>
                        )}

                        {item.recallCount > 0 && (
                          <span className="text-[10px] px-1.5 py-0.5 rounded-full font-bold" style={{ background: "#fef3c7", color: "#d97706" }}>
                            نداء {toArabicNum(item.recallCount)}
                          </span>
                        )}

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
                        {isWaiting && item.position != null && (
                          <span className="text-[11px] font-semibold" style={{ color: "#b45309" }}>
                            رقم {toArabicNum(item.position)}
                            {item.estimatedWaitMinutes != null ? ` — ~${toArabicNum(item.estimatedWaitMinutes)} دقيقة` : ""}
                          </span>
                        )}
                      </div>
                    </div>
                    <div className="flex items-center gap-1.5 flex-shrink-0 flex-wrap justify-end">
                      {item.status === "Waiting" && (
                        <>
                          <button onClick={(e) => { e.stopPropagation(); handleCallWithVoice(item); }}
                            disabled={isLoading} className="px-2.5 py-1 rounded-lg text-[11px] font-bold text-white transition hover:opacity-80 disabled:opacity-50 flex items-center gap-1" style={{ background: BLUE }}>
                            <PhoneCall className="w-3 h-3" />نداء
                          </button>
                          <button onClick={(e) => { e.stopPropagation(); announcePatient(item); }}
                            disabled={isLoading} className="px-2.5 py-1 rounded-lg text-[11px] font-bold text-white transition hover:opacity-80 disabled:opacity-50" style={{ background: "#059669" }} title="نداء صوتي فقط (بدون تغيير الحالة)">
                            <Volume2 className="w-3 h-3" />
                          </button>
                          <button onClick={(e) => { e.stopPropagation(); queueAction(`/api/clinic-queue/${item.id}/no-show`); }}
                            disabled={isLoading} className="px-2.5 py-1 rounded-lg text-[11px] font-bold text-white transition hover:opacity-80 disabled:opacity-50 flex items-center gap-1" style={{ background: "#9333ea" }} title="لم يحضر">
                            لم يحضر
                          </button>
                          <button onClick={(e) => { e.stopPropagation(); queueAction(`/api/clinic-queue/${item.id}/cancel`); }}
                            disabled={isLoading} className="px-2.5 py-1 rounded-lg text-[11px] font-bold text-white transition hover:opacity-80 disabled:opacity-50" style={{ background: "#ef4444" }}>
                            <XCircle className="w-3 h-3 inline ml-1" />إلغاء
                          </button>
                        </>
                      )}
                      {item.status === "Called" && (
                        <>
                          <button onClick={(e) => { e.stopPropagation(); queueAction(`/api/clinic-queue/${item.id}/enter-room`); }}
                            disabled={isLoading} className="px-2.5 py-1 rounded-lg text-[11px] font-bold text-white transition hover:opacity-80 disabled:opacity-50" style={{ background: "#7c3aed" }}>
                            <DoorOpen className="w-3 h-3 inline ml-1" />إدخال
                          </button>
                          <button onClick={(e) => { e.stopPropagation(); queueAction(`/api/clinic-queue/${item.id}/recall`); }}
                            disabled={isLoading} className="px-2.5 py-1 rounded-lg text-[11px] font-bold text-white transition hover:opacity-80 disabled:opacity-50 flex items-center gap-1" style={{ background: "#d97706" }} title="إعادة نداء">
                            <PhoneCall className="w-3 h-3" />إعادة نداء
                          </button>
                          <button onClick={(e) => { e.stopPropagation(); queueAction(`/api/clinic-queue/${item.id}/no-show`); }}
                            disabled={isLoading} className="px-2.5 py-1 rounded-lg text-[11px] font-bold text-white transition hover:opacity-80 disabled:opacity-50 flex items-center gap-1" style={{ background: "#9333ea" }} title="لم يحضر">
                            لم يحضر
                          </button>
                        </>
                      )}
                      {item.status === "InRoom" && (
                        <button onClick={(e) => { e.stopPropagation(); queueAction(`/api/clinic-queue/${item.id}/start`); }}
                          disabled={isLoading} className="px-2.5 py-1 rounded-lg text-[11px] font-bold text-white transition hover:opacity-80 disabled:opacity-50" style={{ background: "#059669" }}>
                          <Play className="w-3 h-3 inline ml-1" />بدء
                        </button>
                      )}
                      {item.status === "InProgress" && (
                        <button onClick={(e) => { e.stopPropagation(); queueAction(`/api/clinic-queue/${item.id}/complete`); }}
                          disabled={isLoading} className="px-2.5 py-1 rounded-lg text-[11px] font-bold text-white transition hover:opacity-80 disabled:opacity-50" style={{ background: NAVY }}>
                          <Square className="w-3 h-3 inline ml-1" />إنهاء
                        </button>
                      )}

                      <div className="relative" ref={priorityDropdownOpen === item.id ? priorityDropdownRef : null} onClick={(e) => e.stopPropagation()}>
                        <button
                          onClick={(e) => { e.stopPropagation(); setPriorityDropdownOpen(priorityDropdownOpen === item.id ? null : item.id); }}
                          disabled={isLoading}
                          className="px-2 py-1 rounded-lg text-[10px] font-bold transition hover:opacity-80 disabled:opacity-50 flex items-center gap-0.5"
                          style={{ background: priCfg.bg, color: priCfg.color, border: `1px solid ${priCfg.color}30` }}
                          title="تغيير الأولوية"
                        >
                          {priCfg.label}
                          <ChevronDown className="w-2.5 h-2.5" />
                        </button>
                        {priorityDropdownOpen === item.id && (
                          <div className="absolute top-full mt-1 left-0 z-50 rounded-lg shadow-lg border py-1 min-w-[100px]" style={{ background: "#fff", borderColor: "#e5e7eb" }}>
                            {Object.entries(PRIORITY_CONFIG).map(([key, val]) => (
                              <button
                                key={key}
                                onClick={(e) => { e.stopPropagation(); handlePriorityChange(item, key); }}
                                className="w-full text-right px-3 py-1.5 text-[11px] font-semibold hover:bg-gray-50 transition flex items-center gap-2"
                                style={{ color: item.priority === key ? val.color : "#374151" }}
                              >
                                <span className="w-2 h-2 rounded-full" style={{ background: val.color }} />
                                {val.label}
                                {item.priority === key && <span className="mr-auto text-[10px]" style={{ color: val.color }}>✓</span>}
                              </button>
                            ))}
                          </div>
                        )}
                      </div>

                      <button onClick={(e) => { e.stopPropagation(); handleNotify(item); }}
                        disabled={isLoading} className="px-2 py-1 rounded-lg text-[11px] font-bold transition hover:opacity-80 disabled:opacity-50 flex items-center gap-1"
                        style={{ background: "#eff6ff", color: "#3b82f6", border: "1px solid #bfdbfe" }}
                        title="إرسال إشعار SMS">
                        <Send className="w-3 h-3" />
                      </button>
                    </div>
                  </div>
                </div>
              );
            })}

            {filtered(noShowItems).length > 0 && (
              <div className="pt-3">
                <div className="text-[11px] font-bold mb-2 flex items-center gap-1.5" style={{ color: "#9333ea" }}>
                  <AlertTriangle className="w-3 h-3" />
                  لم يحضروا ({toArabicNum(filtered(noShowItems).length)})
                </div>
                {filtered(noShowItems).map(item => {
                  const cfg = STATUS_CONFIG.NoShow;
                  return (
                    <div key={item.id} className="rounded-xl border p-3 mb-2 opacity-60 cursor-pointer" style={{ background: "#fff", borderColor: "#e5e7eb" }}
                      onContextMenu={(e) => {
                        e.preventDefault();
                        onContextMenu?.(e, queueItemToJourney(item));
                      }}
                      onClick={() => onOpenSidePanel?.(queueItemToJourney(item))}>
                      <div className="flex items-center gap-2">
                        <span className="font-bold text-sm" style={{ color: NAVY }}>{item.patientName}</span>
                        <span className="text-[11px] px-1.5 py-0.5 rounded-md font-mono" style={{ background: "#f1f5f9", color: "#64748b" }}>{item.patientNumber}</span>
                        <span className="text-[11px] px-2 py-0.5 rounded-full font-bold" style={{ background: cfg.bg, color: cfg.color }}>
                          {item.statusArabic || cfg.label}
                        </span>
                        {item.noShowAt && (
                          <span className="text-[10px]" style={{ color: "#94a3b8" }}>
                            <Clock className="w-3 h-3 inline ml-0.5" />
                            {new Date(item.noShowAt).toLocaleTimeString("ar-YE", { hour: "2-digit", minute: "2-digit" })}
                          </span>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            )}

            {filtered(completedItems).length > 0 && (
              <div className="pt-3">
                <div className="text-[11px] font-bold mb-2" style={{ color: "#94a3b8" }}>المنتهون ({toArabicNum(filtered(completedItems).length)})</div>
                {filtered(completedItems).map(item => {
                  const cfg = STATUS_CONFIG[item.status] || STATUS_CONFIG.Completed;
                  return (
                    <div key={item.id} className="rounded-xl border p-3 mb-2 opacity-60 cursor-pointer" style={{ background: "#fff", borderColor: "#e5e7eb" }}
                      onContextMenu={(e) => {
                        e.preventDefault();
                        onContextMenu?.(e, queueItemToJourney(item));
                      }}
                      onClick={() => onOpenSidePanel?.(queueItemToJourney(item))}>
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
