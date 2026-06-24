"use client";

import { useEffect, useState, useCallback, useRef } from "react";
import {
  Users,
  Volume2,
  VolumeX,
  MapPin,
  Stethoscope,
  CheckCircle2,
  PhoneCall,
  Clock,
  Settings,
  RefreshCw,
  AlertTriangle,
  UserCheck,
} from "lucide-react";
import {
  RECEPTION_FALLBACK,
  buildAnnouncementText,
} from "@/lib/clinic-display-announcement";
import { HubConnectionBuilder, type HubConnection, LogLevel } from "@microsoft/signalr";

/* ─── Types ────────────────────────────────────────────────────────────────── */
interface DisplayData {
  latestCalled: {
    queueItemId: string;
    patientNumber: string;
    patientName: string;
    doctorName: string;
    roomName: string;
    calledAt: string;
    estimatedWaitMinutes?: number;
    recallCount?: number;
    priority?: string;
    priorityArabic?: string;
  } | null;
  waitingCount: number;
  waitingList: {
    queueItemId: string;
    patientNumber: string;
    patientName: string;
    doctorName: string;
    estimatedWaitMinutes?: number;
    status: string;
    position?: number;
    priority?: string;
    priorityArabic?: string;
  }[];
  recentlyCalled: {
    queueItemId: string;
    patientNumber: string;
    patientName: string;
    doctorName: string;
    roomName: string;
    statusArabic: string;
    status: string;
    calledAt: string;
    recallCount?: number;
  }[];
  nowServing?: {
    roomName: string;
    doctorName: string;
    patientName: string;
    patientNumber: string;
    startedAt: string;
  }[];
  averageServiceTimeMinutes?: number;
}

/* ─── Constants ────────────────────────────────────────────────────────────── */
const STATUS_DISPLAY: Record<string, { label: string; color: string; bg: string; dotColor: string }> = {
  Waiting:    { label: "في الانتظار",  color: "text-amber-300",  bg: "bg-amber-900/30",  dotColor: "bg-amber-400" },
  Called:     { label: "تم النداء",    color: "text-blue-300",   bg: "bg-blue-900/30",   dotColor: "bg-blue-400" },
  InRoom:     { label: "داخل الغرفة",  color: "text-purple-300", bg: "bg-purple-900/30", dotColor: "bg-purple-400" },
  InProgress: { label: "جاري العلاج", color: "text-teal-300",   bg: "bg-teal-900/30",   dotColor: "bg-teal-400" },
  Completed:  { label: "مكتمل",        color: "text-gray-400",   bg: "bg-gray-800/30",   dotColor: "bg-gray-500" },
  Cancelled:  { label: "ملغي",         color: "text-red-400",    bg: "bg-red-900/30",    dotColor: "bg-red-400" },
};

const REFRESH_INTERVAL = 20_000; // 20 seconds
const VOICE_STORAGE_KEY = "aqlan-voice-enabled";
const VOICE_ERROR_MSG = "تعذر تشغيل النداء الصوتي. تأكد من صوت الجهاز والمتصفح.";

/* ─── Helpers ──────────────────────────────────────────────────────────────── */
function formatClock(date: Date): string {
  return date.toLocaleTimeString("ar-SA", {
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
  });
}

function formatTimeAgo(dateStr: string): string {
  const diff = Date.now() - new Date(dateStr).getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return "الآن";
  if (mins < 60) return `منذ ${mins} دقيقة`;
  const hours = Math.floor(mins / 60);
  return `منذ ${hours} ساعة`;
}

function getStatusDisplay(status: string) {
  return STATUS_DISPLAY[status] ?? { label: status, color: "text-gray-400", bg: "bg-gray-800/30", dotColor: "bg-gray-500" };
}

function toArabicNumerals(n: number): string {
  const digits = "\u0660\u0661\u0662\u0663\u0664\u0665\u0666\u0667\u0668\u0669";
  return String(n).replace(/\d/g, (d) => digits[parseInt(d)]);
}

function getPriorityStyle(priority: string): { color: string; bg: string } {
  switch (priority) {
    case "Urgent":    return { color: "text-amber-300", bg: "bg-amber-900/40" };
    case "Emergency": return { color: "text-red-300",   bg: "bg-red-900/40" };
    default:          return { color: "text-orange-300", bg: "bg-orange-900/40" };
  }
}

/* NOTE: buildAnnouncementText and formatRoomForSpeech are imported from
   @/lib/clinic-display-announcement.ts — the single source of truth.
   Do NOT create inline duplicates here. */

/* ─── Debug Logger ──────────────────────────────────────────────────────── */

// Debug logger — only logs in development
function debugVoiceLog(...args: unknown[]) {
  if (process.env.NODE_ENV === 'development') {
    console.log('[Voice]', ...args);
  }
}

// Debug warn — only logs in development
function debugVoiceWarn(...args: unknown[]) {
  if (process.env.NODE_ENV === 'development') {
    console.warn('[Voice]', ...args);
  }
}

/* ─── Arabic Speech Utility ────────────────────────────────────────────────── */

/**
 * Speak an Arabic text using the browser SpeechSynthesis API.
 *
 * Rules:
 * - Cancels any ongoing speech first.
 * - Prefers an Arabic voice (lang starts with "ar") when available.
 * - If no Arabic voice exists: speaks using the default voice with lang="ar-SA".
 *   Since the text is already digit-converted to Arabic words, the output
 *   will be reasonable even without a dedicated Arabic voice.
 *   Edge will automatically use its cloud TTS for Arabic.
 * - Does NOT set any error/warning here — the UI handles Arabic voice status.
 * - Returns { ok: true } if speech was started successfully.
 *   Returns { ok: false, reason: string } only on hard failure.
 */
function speakArabic(
  text: string,
  voices: SpeechSynthesisVoice[]
): { ok: boolean; reason?: string } {
  if (typeof window === "undefined" || !window.speechSynthesis) {
    return { ok: false, reason: "SpeechSynthesis not available" };
  }

  try {
    window.speechSynthesis.cancel();

    const utterance = new SpeechSynthesisUtterance(text);
    utterance.lang = "ar-SA";
    utterance.rate = 0.9;
    utterance.pitch = 1;
    utterance.volume = 1;

    // Prefer Arabic voice, but do NOT block if unavailable
    const arabicVoice = voices.find((v) => v.lang.startsWith("ar"));
    if (arabicVoice) {
      utterance.voice = arabicVoice;
      debugVoiceLog("Using Arabic voice:", arabicVoice.name);
    } else {
      // No Arabic voice locally — Edge will use cloud TTS with ar-SA lang
      debugVoiceLog("No Arabic voice locally, using default with lang=ar-SA");
    }

    utterance.onstart = () => debugVoiceLog("Speaking:", text);
    utterance.onend = () => debugVoiceLog("Speech ended");
    utterance.onerror = (e) => debugVoiceWarn("Speech error:", e.error);

    window.speechSynthesis.speak(utterance);
    return { ok: true };
  } catch (err) {
    debugVoiceWarn("speakArabic failed:", err);
    return { ok: false, reason: String(err) };
  }
}

/* ─── Voice Announcement Module ────────────────────────────────────────────── */
function useArabicVoiceAnnouncement() {
  const [voiceEnabled, setVoiceEnabled] = useState(false);
  const [voiceSupported, setVoiceSupported] = useState(true);
  const [voiceStatus, setVoiceStatus] = useState<"active" | "inactive" | "unsupported">("inactive");
  const [voices, setVoices] = useState<SpeechSynthesisVoice[]>([]);
  const [voiceError, setVoiceError] = useState<string | null>(null);
  const [arabicVoiceAvailable, setArabicVoiceAvailable] = useState<boolean | null>(null);
  const lastAnnouncedRef = useRef<{ queueItemId: string; calledAt: string } | null>(null);

  // ─── Load voices reliably ───────────────────────────────────────────────
  useEffect(() => {
    if (typeof window === "undefined" || !window.speechSynthesis) {
      setVoiceSupported(false);
      setVoiceStatus("unsupported");
      return;
    }

    const loadVoices = () => {
      const available = window.speechSynthesis.getVoices();
      if (available.length > 0) {
        setVoices(available);
        setArabicVoiceAvailable(available.some((v) => v.lang.startsWith("ar")));
      }
    };

    loadVoices();
    window.speechSynthesis.onvoiceschanged = loadVoices;
    return () => { window.speechSynthesis.onvoiceschanged = null; };
  }, []);

  // ─── Restore localStorage on mount ──────────────────────────────────────
  useEffect(() => {
    if (!voiceSupported) return;
    const stored = localStorage.getItem(VOICE_STORAGE_KEY);
    if (stored === "true") {
      setVoiceEnabled(true);
      setVoiceStatus("active");
    }
  }, [voiceSupported]);

  // ─── Helper: get fresh voices from browser ──────────────────────────────
  const getFreshVoices = useCallback((): SpeechSynthesisVoice[] => {
    if (typeof window === "undefined" || !window.speechSynthesis) return voices;
    const fresh = window.speechSynthesis.getVoices();
    if (fresh.length > 0 && fresh.length !== voices.length) {
      setVoices(fresh);
      setArabicVoiceAvailable(fresh.some((v) => v.lang.startsWith("ar")));
    }
    return fresh.length > 0 ? fresh : voices;
  }, [voices]);

  // ─── Test voice ─────────────────────────────────────────────────────────
  const testVoice = useCallback(() => {
    if (typeof window === "undefined" || !window.speechSynthesis) {
      setVoiceError("النداء الصوتي غير مدعوم في هذا المتصفح");
      return;
    }
    setVoiceError(null);
    const v = getFreshVoices();
    const result = speakArabic("تم تفعيل النداء الصوتي بنجاح", v);
    if (!result.ok) setVoiceError(VOICE_ERROR_MSG);
  }, [getFreshVoices]);

  // ─── Enable voice ──────────────────────────────────────────────────────
  const enableVoice = useCallback(() => {
    if (!voiceSupported) return;
    setVoiceError(null);
    const v = getFreshVoices();
    const result = speakArabic("تم تفعيل النداء الصوتي", v);
    if (!result.ok) {
      setVoiceError(VOICE_ERROR_MSG);
      return;
    }
    setVoiceEnabled(true);
    setVoiceStatus("active");
    localStorage.setItem(VOICE_STORAGE_KEY, "true");
  }, [voiceSupported, getFreshVoices]);

  // ─── Disable voice ──────────────────────────────────────────────────────
  const disableVoice = useCallback(() => {
    setVoiceEnabled(false);
    setVoiceStatus("inactive");
    localStorage.setItem(VOICE_STORAGE_KEY, "false");
    window.speechSynthesis?.cancel();
  }, []);

  // ─── Announce a called patient (auto-trigger on new call) ───────────────
  const announce = useCallback(
    (patientName: string, patientNumber: string, roomName: string, queueItemId: string, calledAt: string) => {
      if (!voiceEnabled || !voiceSupported) return false;
      const last = lastAnnouncedRef.current;
      if (last && last.queueItemId === queueItemId && last.calledAt === calledAt) return false;
      lastAnnouncedRef.current = { queueItemId, calledAt };

      const text = buildAnnouncementText(patientName, patientNumber, roomName);
      const result = speakArabic(text, voices);
      if (!result.ok) setVoiceError(VOICE_ERROR_MSG);
      return result.ok;
    },
    [voiceEnabled, voiceSupported, voices]
  );

  // ─── Manual repeat announcement (bypasses dedup check) ──────────────────
  const repeatAnnounce = useCallback(
    (patientName: string, patientNumber: string, roomName: string) => {
      if (!voiceEnabled || !voiceSupported) return false;
      const text = buildAnnouncementText(patientName, patientNumber, roomName);
      const result = speakArabic(text, voices);
      if (!result.ok) setVoiceError(VOICE_ERROR_MSG);
      return result.ok;
    },
    [voiceEnabled, voiceSupported, voices]
  );

  // ─── Announce current patient after enabling ────────────────────────────
  const announceCurrent = useCallback(
    (patientName: string, patientNumber: string, roomName: string, queueItemId: string, calledAt: string) => {
      if (!voiceEnabled || !voiceSupported) return false;
      lastAnnouncedRef.current = { queueItemId, calledAt };
      const text = buildAnnouncementText(patientName, patientNumber, roomName);
      const result = speakArabic(text, voices);
      if (!result.ok) setVoiceError(VOICE_ERROR_MSG);
      return result.ok;
    },
    [voiceEnabled, voiceSupported, voices]
  );

  // ─── Re-scan voices (after user installs Arabic voice) ───────────────────
  const rescanVoices = useCallback(() => {
    if (typeof window === "undefined" || !window.speechSynthesis) return;
    const available = window.speechSynthesis.getVoices();
    if (available.length > 0) {
      setVoices(available);
      setArabicVoiceAvailable(available.some((v) => v.lang.startsWith("ar")));
    }
    setVoiceError(null);
  }, []);

  // ─── Open system voice settings to install Arabic voice ─────────────────
  const openVoiceSettings = useCallback(() => {
    const ua = navigator.userAgent.toLowerCase();
    if (ua.includes("edg/")) {
      window.open("edge://settings/languages", "_blank");
    } else if (ua.includes("chrome")) {
      window.open("chrome://settings/languages", "_blank");
    } else if (ua.includes("windows")) {
      window.open("ms-settings:regionlanguage", "_blank");
    } else {
      window.open("https://support.google.com/chrome/answer/3461352", "_blank");
    }
  }, []);

  return {
    voiceEnabled, voiceSupported, voiceStatus, voiceError, setVoiceError,
    arabicVoiceAvailable, testVoice, enableVoice, disableVoice,
    announce, repeatAnnounce, announceCurrent, rescanVoices, openVoiceSettings,
  };
}

/* ─── Custom Context Menu for Right-Click Replay ───────────────────────────── */
interface ContextMenuState {
  visible: boolean; x: number; y: number;
  patientName: string; patientNumber: string; roomName: string;
}

const INITIAL_CONTEXT_MENU: ContextMenuState = {
  visible: false, x: 0, y: 0, patientName: "", patientNumber: "", roomName: "",
};

/* ─── Main Page ────────────────────────────────────────────────────────────── */
export default function ClinicDisplayPage() {
  const [data, setData] = useState<DisplayData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null);
  const [now, setNow] = useState<Date>(new Date());
  const [pulseKey, setPulseKey] = useState(0);
  const [contextMenu, setContextMenu] = useState<ContextMenuState>(INITIAL_CONTEXT_MENU);
  const [signalrConnected, setSignalrConnected] = useState(false);
  const prevLatestCalledRef = useRef<string | null>(null);
  const signalrRef = useRef<HubConnection | null>(null);

  const {
    voiceEnabled, voiceSupported, voiceStatus, voiceError, setVoiceError,
    arabicVoiceAvailable, testVoice, enableVoice, disableVoice,
    announce, repeatAnnounce, announceCurrent, rescanVoices, openVoiceSettings,
  } = useArabicVoiceAnnouncement();

  const announcedAfterEnableRef = useRef(false);

  const fetchDisplay = useCallback(async () => {
    try {
      const apiBase = process.env.NEXT_PUBLIC_API_URL ?? "";
      const res = await fetch(`${apiBase}/api/clinic-queue/display`, { cache: "no-store" });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const json: DisplayData = await res.json();

      const newKey = json.latestCalled?.queueItemId ?? null;
      if (newKey !== prevLatestCalledRef.current) {
        prevLatestCalledRef.current = newKey;
        setPulseKey((k) => k + 1);
        if (json.latestCalled) {
          announce(
            json.latestCalled.patientName, json.latestCalled.patientNumber,
            json.latestCalled.roomName, json.latestCalled.queueItemId, json.latestCalled.calledAt
          );
        }
      }

      setData(json);
      setLastUpdated(new Date());
      setError(null);
    } catch { setError("تعذّر تحميل البيانات"); } finally { setLoading(false); }
  }, [announce]);

  useEffect(() => {
    if (voiceEnabled && data?.latestCalled && !announcedAfterEnableRef.current) {
      announcedAfterEnableRef.current = true;
      const timer = setTimeout(() => {
        announceCurrent(
          data.latestCalled!.patientName, data.latestCalled!.patientNumber,
          data.latestCalled!.roomName, data.latestCalled!.queueItemId, data.latestCalled!.calledAt
        );
      }, 300);
      return () => clearTimeout(timer);
    }
  }, [voiceEnabled, data?.latestCalled, announceCurrent]);

  useEffect(() => { if (!voiceEnabled) announcedAfterEnableRef.current = false; }, [voiceEnabled]);

  useEffect(() => {
    fetchDisplay();
    // SignalR: real-time updates — when connected, polling is reduced
    const refreshInterval = setInterval(fetchDisplay, signalrConnected ? 60_000 : REFRESH_INTERVAL);
    return () => clearInterval(refreshInterval);
  }, [fetchDisplay, signalrConnected]);

  // ── SignalR real-time connection for instant patient call updates ──
  useEffect(() => {
    // NAV-CEPH-FIX (Part 2): relative hub URL → Next.js rewrite proxies to backend (same-origin).
    // The clinic display is unauthenticated, but a relative URL still avoids cross-origin
    // WebSocket handshake complications in production (Vercel→Railway).
    const hubUrl = "/hubs/messaging";

    const connectSignalR = async () => {
      try {
        // Clinic display is unauthenticated — connect without JWT
        // The hub [Authorize] attribute will reject unauthenticated connections,
        // so we gracefully fall back to HTTP polling only.
        const connection = new HubConnectionBuilder()
          .withUrl(hubUrl)
          .withAutomaticReconnect([0, 5000, 15000, 60000])
          .configureLogging(LogLevel.Warning)
          .build();

        connection.on("PatientCalled", () => {
          // Instantly refresh display data when a patient is called
          fetchDisplay();
        });

        connection.on("QueueUpdated", () => {
          fetchDisplay();
        });

        connection.onreconnected(() => {
          setSignalrConnected(true);
          fetchDisplay();
        });
        connection.onclose(() => setSignalrConnected(false));

        await connection.start();
        signalrRef.current = connection;
        setSignalrConnected(true);
      } catch {
        // SignalR auth failed (expected for anonymous display) — fall back to polling
        setSignalrConnected(false);
      }
    };

    connectSignalR();

    return () => {
      if (signalrRef.current) {
        signalrRef.current.stop().catch(() => {});
        signalrRef.current = null;
      }
    };
  }, [fetchDisplay]);

  useEffect(() => {
    const clockInterval = setInterval(() => setNow(new Date()), 1_000);
    return () => clearInterval(clockInterval);
  }, []);

  // Re-scan voices when page regains focus (user returns from settings)
  useEffect(() => {
    const onFocus = () => { rescanVoices(); };
    window.addEventListener("focus", onFocus);
    return () => window.removeEventListener("focus", onFocus);
  }, [rescanVoices]);

  const isFullyEmpty = data && data.waitingCount === 0 && !data.latestCalled && data.recentlyCalled.length === 0 && (data.nowServing?.length ?? 0) === 0;

  const voiceStatusText = voiceStatus === "active"
    ? "النداء الصوتي مفعل"
    : voiceStatus === "unsupported" ? "النداء الصوتي غير مدعوم في هذا المتصفح" : "النداء الصوتي متوقف";

  const handleRepeatAnnounce = useCallback(() => {
    if (!data?.latestCalled) return;
    if (!voiceEnabled) { setVoiceError("يرجى تفعيل النداء الصوتي أولاً لإعادة النداء."); return; }
    setVoiceError(null);
    repeatAnnounce(data.latestCalled.patientName, data.latestCalled.patientNumber, data.latestCalled.roomName);
  }, [data?.latestCalled, repeatAnnounce, voiceEnabled, setVoiceError]);

  const handleReplayPatient = useCallback((patientName: string, patientNumber: string, roomName: string) => {
    if (!voiceEnabled) { setVoiceError("يرجى تفعيل النداء الصوتي أولاً لإعادة النداء."); setContextMenu(INITIAL_CONTEXT_MENU); return; }
    setVoiceError(null); setContextMenu(INITIAL_CONTEXT_MENU);
    repeatAnnounce(patientName, patientNumber, roomName);
  }, [voiceEnabled, setVoiceError, repeatAnnounce]);

  const handlePatientContextMenu = useCallback((e: React.MouseEvent, patientName: string, patientNumber: string, roomName: string) => {
    e.preventDefault();
    setContextMenu({ visible: true, x: e.clientX, y: e.clientY, patientName, patientNumber, roomName });
  }, []);

  useEffect(() => {
    if (!contextMenu.visible) return;
    const close = () => setContextMenu(INITIAL_CONTEXT_MENU);
    document.addEventListener("click", close);
    document.addEventListener("contextmenu", close);
    return () => { document.removeEventListener("click", close); document.removeEventListener("contextmenu", close); };
  }, [contextMenu.visible]);

  return (
    <div className="min-h-screen bg-[#0F172A] text-white flex flex-col" style={{ fontFamily: "'Segoe UI', Tahoma, Arial, sans-serif" }}>
      {/* ── Header ─────────────────────────────────────────────── */}
      <header className="flex items-center justify-between px-8 md:px-16 py-6 border-b border-white/10 bg-[#0c1322]">
        <div className="flex items-center gap-5">
          <div className="w-14 h-14 rounded-full bg-[#3d7ab5] flex items-center justify-center text-2xl font-bold shadow-lg shadow-cyan-900/40">ع</div>
          <div>
            <h1 className="text-2xl md:text-4xl font-bold text-white leading-tight">مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان</h1>
            <p className="text-lg md:text-xl text-teal-300 mt-1">شاشة الانتظار</p>
          </div>
        </div>
        <div className="text-left">
          <p className="text-5xl md:text-6xl font-mono font-bold text-teal-300 tabular-nums">{formatClock(now)}</p>
          <p className="text-sm text-gray-400 mt-1">
            {now.toLocaleDateString("ar-SA", { weekday: "long", year: "numeric", month: "long", day: "numeric" })}
          </p>
        </div>
      </header>

      {/* ── Voice Control Bar ────────────────────────────────────── */}
      <div className="px-8 md:px-16 py-2 bg-[#0c1322] border-b border-white/5 flex items-center justify-between flex-wrap gap-2">
        <div className="flex items-center gap-3">
          {voiceEnabled ? <Volume2 className="w-5 h-5 text-teal-400" /> : <VolumeX className="w-5 h-5 text-gray-500" />}
          <span className={`text-sm font-medium ${voiceStatus === "active" ? "text-teal-400" : voiceStatus === "unsupported" ? "text-gray-500" : "text-gray-400"}`}>
            {voiceStatusText}
          </span>
          {voiceStatus === "active" && arabicVoiceAvailable === true && (
            <span className="text-xs text-teal-500">— الصوت العربي متاح ✓</span>
          )}
          {voiceStatus === "active" && arabicVoiceAvailable === false && (
            <span className="text-xs text-amber-500">— لا يوجد صوت عربي</span>
          )}
        </div>
        <div className="flex items-center gap-2 flex-wrap">
          {voiceSupported && (
            <button onClick={testVoice} className="px-4 py-1.5 rounded-lg text-sm font-medium bg-white/10 text-gray-300 hover:bg-white/20 transition flex items-center gap-1.5">
              <Volume2 className="w-4 h-4" /> اختبار الصوت
            </button>
          )}
          {voiceSupported && !voiceEnabled && (
            <button onClick={enableVoice} className="px-4 py-1.5 rounded-lg text-sm font-bold bg-teal-700 text-white hover:bg-teal-600 transition">
              تفعيل النداء الصوتي
            </button>
          )}
          {voiceSupported && voiceEnabled && (
            <button onClick={disableVoice} className="px-4 py-1.5 rounded-lg text-sm font-medium bg-white/10 text-gray-300 hover:bg-white/20 transition">
              إيقاف النداء الصوتي
            </button>
          )}
        </div>
      </div>

      {/* Voice error message — only for hard errors, NOT for missing Arabic voice */}
      {voiceError && (
        <div className="px-8 md:px-16 py-2 bg-red-900/30 border-b border-red-800/30">
          <p className="text-sm text-red-300">{voiceError}</p>
        </div>
      )}

      {/* Arabic voice setup panel — shown when no Arabic voice detected */}
      {voiceSupported && arabicVoiceAvailable === false && (
        <div className="px-8 md:px-16 py-3 bg-amber-900/20 border-b border-amber-700/20">
          <div className="flex items-center justify-between flex-wrap gap-3">
            <div className="flex items-center gap-3">
              <Settings className="w-5 h-5 text-amber-400" />
              <p className="text-sm text-amber-200">
                الصوت العربي غير مُثبَّت. اضغط &quot;تثبيت الصوت العربي&quot; لفتح الإعدادات، ثم اضغط &quot;إعادة فحص&quot;.
              </p>
            </div>
            <div className="flex items-center gap-2">
              <button
                onClick={openVoiceSettings}
                className="px-4 py-1.5 rounded-lg text-sm font-bold bg-amber-700 text-white hover:bg-amber-600 transition flex items-center gap-1.5"
              >
                <Settings className="w-4 h-4" />
                تثبيت الصوت العربي
              </button>
              <button
                onClick={rescanVoices}
                className="px-4 py-1.5 rounded-lg text-sm font-medium bg-white/10 text-gray-300 hover:bg-white/20 transition flex items-center gap-1.5"
              >
                <RefreshCw className="w-4 h-4" />
                إعادة فحص
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Browser/device helper note */}
      <div className="px-8 md:px-16 py-1.5 bg-[#0c1322] border-b border-white/5">
        <p className="text-xs text-gray-500">
          إذا لم يعمل الصوت، اضغط اختبار الصوت وتأكد من رفع صوت الجهاز والسماح بالصوت في المتصفح.
        </p>
      </div>

      {/* ── Main content ───────────────────────────────────────── */}
      <main className="flex-1 px-8 md:px-16 py-8 overflow-auto">
        {loading && !data ? (
          <div className="flex flex-col items-center justify-center h-64 gap-4">
            <div className="w-12 h-12 border-4 border-teal-500 border-t-transparent rounded-full animate-spin" />
            <p className="text-2xl text-gray-400">جاري التحميل…</p>
          </div>
        ) : error && !data ? (
          <div className="flex flex-col items-center justify-center h-64 gap-4">
            <div className="text-5xl text-red-400 mb-2">⚠</div>
            <p className="text-2xl text-red-400">{error}</p>
            <button onClick={fetchDisplay} className="mt-4 px-6 py-3 rounded-xl bg-teal-700 text-white font-bold hover:bg-teal-600 transition">إعادة المحاولة</button>
          </div>
        ) : isFullyEmpty ? (
          <div className="flex flex-col items-center justify-center h-full gap-4 py-20">
            <div className="w-24 h-24 rounded-full bg-white/5 flex items-center justify-center mb-4"><CheckCircle2 className="w-14 h-14 text-teal-500/60" /></div>
            <p className="text-3xl font-bold text-gray-300">لا يوجد مرضى منادون حالياً</p>
            <p className="text-lg text-gray-500 mt-2">سيتم تحديث الشاشة تلقائياً عند إضافة مرضى</p>
          </div>
        ) : data ? (
          <div className="space-y-8">
            {/* ── Now Serving ──────────────────────────────────────────── */}
            {data.nowServing && data.nowServing.length > 0 && (
              <div>
                <div className="flex items-center gap-4 mb-4">
                  <UserCheck className="w-7 h-7 text-teal-400" />
                  <span className="text-2xl font-bold text-teal-300">يتم الخدمة الآن</span>
                  {data.averageServiceTimeMinutes != null && data.averageServiceTimeMinutes > 0 && (
                    <span className="text-sm text-gray-400">متوسط وقت الخدمة: {toArabicNumerals(data.averageServiceTimeMinutes)} دقيقة</span>
                  )}
                </div>
                <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
                  {data.nowServing.map((room, i) => (
                    <div key={i} className="rounded-2xl bg-gradient-to-br from-teal-900/40 to-cyan-900/30 border border-teal-600/30 p-5 text-center">
                      <div className="text-xl font-bold text-teal-300 mb-1">{room.roomName}</div>
                      <div className="text-base text-gray-300">{room.doctorName}</div>
                      <div className="text-lg font-bold text-white mt-2">{room.patientName || `ملف رقم ${room.patientNumber}`}</div>
                      {room.patientName && <div className="text-sm text-gray-500 font-mono">{room.patientNumber}</div>}
                      <div className="text-xs text-gray-500 mt-2 flex items-center justify-center gap-1"><Clock className="w-3 h-3" />{formatTimeAgo(room.startedAt)}</div>
                    </div>
                  ))}
                </div>
              </div>
            )}
            <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
            {/* ── Left Column ── */}
            <div className="lg:col-span-1 space-y-8">
              {data.latestCalled ? (
                <div key={pulseKey} className="rounded-3xl bg-gradient-to-br from-teal-900/70 to-cyan-900/50 border border-teal-600/40 p-8 md:p-10 shadow-xl shadow-teal-900/20 animate-[fadeIn_0.6s_ease]"
                  onContextMenu={(e) => handlePatientContextMenu(e, data.latestCalled!.patientName, data.latestCalled!.patientNumber, data.latestCalled!.roomName)}>
                  <div className="flex items-center gap-3 mb-6"><Volume2 className="w-8 h-8 text-teal-300 animate-pulse" /><span className="text-2xl font-bold text-teal-300">النداء الأخير</span></div>
                  <div className="text-center space-y-4">
                    <div className="text-5xl md:text-7xl font-extrabold text-white leading-tight animate-pulse">{data.latestCalled.patientName || `ملف رقم ${data.latestCalled.patientNumber}`}</div>
                    {data.latestCalled.patientName && <div className="text-2xl md:text-3xl text-teal-200 font-mono">رقم الملف: {data.latestCalled.patientNumber}</div>}
                    <div className="flex items-center justify-center gap-5 mt-6">
                      <div className="flex items-center gap-3 bg-teal-800/60 px-6 py-3 rounded-2xl"><MapPin className="w-6 h-6 text-teal-300" /><span className="text-2xl font-bold text-teal-200">{data.latestCalled.roomName || RECEPTION_FALLBACK}</span></div>
                      {data.latestCalled.doctorName && <div className="flex items-center gap-3 bg-teal-800/60 px-6 py-3 rounded-2xl"><Stethoscope className="w-6 h-6 text-teal-300" /><span className="text-xl text-teal-200">{data.latestCalled.doctorName}</span></div>}
                    </div>
                    {(data.latestCalled.recallCount ?? 0) > 0 && (
                      <div className="flex items-center justify-center gap-2 mt-3">
                        <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-sm font-bold bg-orange-900/40 text-orange-300 border border-orange-600/20">
                          <PhoneCall className="w-4 h-4" />
                          نداء {toArabicNumerals(data.latestCalled.recallCount!)}
                        </span>
                      </div>
                    )}
                    {data.latestCalled.priority && data.latestCalled.priority !== "Normal" && (
                      <div className="flex items-center justify-center gap-2 mt-2">
                        <span className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-sm font-bold ${getPriorityStyle(data.latestCalled.priority).bg} ${getPriorityStyle(data.latestCalled.priority).color} border border-current/20`}>
                          <AlertTriangle className="w-4 h-4" />
                          {data.latestCalled.priorityArabic}
                        </span>
                      </div>
                    )}
                    {data.latestCalled.estimatedWaitMinutes != null && data.latestCalled.estimatedWaitMinutes > 0 && (
                      <div className="flex items-center justify-center gap-2 mt-3 text-lg text-amber-300">
                        <Clock className="w-5 h-5" />
                        وقت الانتظار المتوقع: {data.latestCalled.estimatedWaitMinutes} دقيقة
                      </div>
                    )}
                    <div className="flex items-center justify-center gap-2 text-base text-gray-400 mt-3"><Clock className="w-4 h-4" />{formatTimeAgo(data.latestCalled.calledAt)}</div>
                    <button onClick={handleRepeatAnnounce} disabled={!voiceEnabled} className={`mt-5 px-8 py-3 rounded-xl font-bold text-lg transition flex items-center gap-3 mx-auto ${voiceEnabled ? "bg-teal-600 text-white hover:bg-teal-500 shadow-lg shadow-teal-900/30 active:scale-95" : "bg-white/10 text-gray-400 cursor-not-allowed"}`} title={voiceEnabled ? "إعادة النداء الصوتي للمريض الحالي" : "فعّل النداء الصوتي أولاً"}>
                      <Volume2 className="w-6 h-6" /> إعادة النداء
                    </button>
                  </div>
                </div>
              ) : (
                <div className="rounded-3xl bg-white/5 border border-white/10 p-8 md:p-10 text-center"><PhoneCall className="w-12 h-12 text-gray-500 mx-auto mb-4" /><p className="text-2xl text-gray-400">لم يتم نداء أحد بعد</p></div>
              )}

              {/* Waiting Count */}
              <div className="rounded-3xl bg-white/5 border border-white/10 p-6">
                <div className="flex items-center gap-4 mb-5"><Users className="w-8 h-8 text-amber-400" /><span className="text-2xl font-bold text-amber-300">قائمة الانتظار</span></div>
                <div className="text-center"><span className="text-7xl font-extrabold text-amber-300">{data.waitingCount}</span><p className="text-xl text-gray-400 mt-2">مريض في الانتظار</p></div>
                {data.waitingList.length > 0 && (
                  <div className="mt-5 space-y-3 max-h-60 overflow-y-auto">
                    {data.waitingList.map((w, i) => (
                      <div key={w.queueItemId || i} className="flex items-center justify-between px-4 py-3 rounded-xl bg-white/5">
                        <div className="flex items-center gap-3">
                          <span className="text-sm text-teal-400 font-bold whitespace-nowrap">رقم {toArabicNumerals(w.position ?? (i + 1))}{w.estimatedWaitMinutes != null && w.estimatedWaitMinutes > 0 ? ` \u2014 ~${toArabicNumerals(w.estimatedWaitMinutes)} دقيقة` : ""}</span>
                          <span className="text-lg font-medium text-gray-200">{w.patientName}</span>
                          <span className="text-sm text-gray-500 font-mono">{w.patientNumber}</span>
                          {w.priority && w.priority !== "Normal" && (
                            <span className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-bold ${getPriorityStyle(w.priority).bg} ${getPriorityStyle(w.priority).color}`}>
                              <AlertTriangle className="w-3 h-3" />{w.priorityArabic}
                            </span>
                          )}
                        </div>
                        <div className="flex items-center gap-3">
                          {w.doctorName && <span className="text-sm text-gray-400 flex items-center gap-1"><Stethoscope className="w-3 h-3" />{w.doctorName}</span>}
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>

            {/* ── Right Column ── */}
            <div className="lg:col-span-2">
              <div className="rounded-3xl bg-white/5 border border-white/10 p-6 h-full">
                <div className="flex items-center gap-4 mb-6"><PhoneCall className="w-8 h-8 text-blue-400" /><span className="text-2xl font-bold text-blue-300">تم النداء مؤخراً</span></div>
                {data.recentlyCalled.length === 0 ? (
                  <div className="text-center py-20"><CheckCircle2 className="w-16 h-16 text-gray-600 mx-auto mb-4" /><p className="text-2xl text-gray-500">لا يوجد نداءات حديثة</p></div>
                ) : (
                  <div className="space-y-4">
                    {data.recentlyCalled.map((item, i) => {
                      const cfg = getStatusDisplay(item.status);
                      const isCalled = item.status === "Called";
                      return (
                        <div key={item.queueItemId || i} className={`flex items-center gap-5 px-6 py-5 rounded-2xl border transition-colors cursor-pointer ${
                          isCalled ? "bg-blue-900/40 border-blue-500/40 ring-2 ring-blue-400/30" : "bg-white/5 border-white/5 hover:bg-white/10"
                        }`}
                          onContextMenu={(e) => handlePatientContextMenu(e, item.patientName, item.patientNumber, item.roomName)}>
                          <div className="flex items-center justify-center w-20 h-14 rounded-xl bg-cyan-900/50 border border-cyan-700/30"><span className="text-xl font-bold text-cyan-300">{item.roomName || RECEPTION_FALLBACK}</span></div>
                          <div className="flex-1 min-w-0">
                            <div className="flex items-center gap-3 flex-wrap">
                              <span className={`text-2xl font-bold text-white ${isCalled ? "animate-pulse" : ""}`}>{item.patientName}</span>
                              <span className="text-base text-gray-500 font-mono">{item.patientNumber}</span>
                              {(item.recallCount ?? 0) > 0 && (
                                <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-bold bg-orange-900/40 text-orange-300">
                                  نداء {toArabicNumerals(item.recallCount!)}
                                </span>
                              )}
                            </div>
                            <div className="flex items-center gap-4 mt-2">
                              {item.doctorName && <span className="flex items-center gap-1.5 text-base text-gray-400"><Stethoscope className="w-4 h-4" />{item.doctorName}</span>}
                              <span className="text-sm text-gray-500 flex items-center gap-1"><Clock className="w-3.5 h-3.5" />{formatTimeAgo(item.calledAt)}</span>
                            </div>
                          </div>
                          <div className={`inline-flex items-center gap-2 px-4 py-2 rounded-full text-base font-medium ${cfg.bg} ${cfg.color}`}>
                            <span className={`w-3 h-3 rounded-full ${cfg.dotColor} ${(item.status === "Called" || item.status === "InProgress") ? "animate-pulse" : ""}`} />
                            {item.statusArabic || cfg.label}
                          </div>
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>
            </div>
          </div>
          </div>
        ) : null}
      </main>

      {/* ── Footer ─────────────────────────────────────────────── */}
      <footer className="border-t border-white/10 px-8 md:px-16 py-4 flex items-center justify-between text-gray-500 text-base bg-[#0c1322]">
        <span>{lastUpdated ? `آخر تحديث: ${formatClock(lastUpdated)}` : "جاري التحميل…"}</span>
        <span>يتحدث تلقائياً كل ٢٠ ثانية — مركز الدكتور عقلان الكامل</span>
      </footer>

      {/* ── Right-Click Context Menu ────────────────────────────── */}
      {contextMenu.visible && (
        <div className="fixed z-[100] min-w-[280px] rounded-xl bg-[#1a2744] border border-white/15 shadow-2xl shadow-black/40 py-2 animate-[fadeIn_0.15s_ease]" style={{ top: contextMenu.y, left: contextMenu.x }}>
          <div className="px-4 py-2.5 border-b border-white/10">
            <p className="text-sm font-bold text-white truncate">{contextMenu.patientName || `ملف رقم ${contextMenu.patientNumber}`}</p>
            {contextMenu.patientName && <p className="text-xs text-gray-400 mt-0.5">رقم الملف: {contextMenu.patientNumber}</p>}
          </div>
          <button onClick={() => handleReplayPatient(contextMenu.patientName, contextMenu.patientNumber, contextMenu.roomName)}
            className={`w-full text-right px-4 py-2.5 flex items-center gap-3 transition-colors ${voiceEnabled ? "text-teal-300 hover:bg-teal-900/40" : "text-gray-500 cursor-not-allowed"}`} disabled={!voiceEnabled}>
            <Volume2 className="w-4 h-4 flex-shrink-0" /><span className="flex-1">إعادة النداء</span>
            {!voiceEnabled && <span className="text-[10px] text-amber-500">يفضّل تفعيل الصوت</span>}
          </button>
          {voiceEnabled && (
            <div className="px-4 py-2 border-t border-white/5">
              <p className="text-[11px] text-gray-500 leading-relaxed">{buildAnnouncementText(contextMenu.patientName, contextMenu.patientNumber, contextMenu.roomName)}</p>
            </div>
          )}
        </div>
      )}

      <style jsx global>{`
        @keyframes fadeIn {
          from { opacity: 0; transform: scale(0.97); }
          to   { opacity: 1; transform: scale(1); }
        }
      `}</style>
    </div>
  );
}
