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
} from "lucide-react";

/* ─── Types ────────────────────────────────────────────────────────────────── */
interface DisplayData {
  latestCalled: {
    queueItemId: string;
    patientNumber: string;
    patientName: string;
    doctorName: string;
    roomName: string;
    calledAt: string;
  } | null;
  waitingCount: number;
  waitingList: {
    queueItemId: string;
    patientNumber: string;
    patientName: string;
    doctorName: string;
    status: string;
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
  }[];
}

/* ─── Constants ────────────────────────────────────────────────────────────── */
const STATUS_DISPLAY: Record<string, { label: string; color: string; bg: string; dotColor: string }> = {
  Waiting:    { label: "في الانتظار",  color: "text-amber-300",  bg: "bg-amber-900/30",  dotColor: "bg-amber-400" },
  Called:     { label: "تم النداء",    color: "text-blue-300",   bg: "bg-blue-900/30",   dotColor: "bg-blue-400" },
  InRoom:     { label: "داخل الغرفة",  color: "text-purple-300", bg: "bg-purple-900/30", dotColor: "bg-purple-400" },
  InProgress: { label: "قيد المعالجة", color: "text-teal-300",   bg: "bg-teal-900/30",   dotColor: "bg-teal-400" },
  Completed:  { label: "مكتمل",        color: "text-gray-400",   bg: "bg-gray-800/30",   dotColor: "bg-gray-500" },
  Cancelled:  { label: "ملغى",         color: "text-red-400",    bg: "bg-red-900/30",    dotColor: "bg-red-400" },
};

const REFRESH_INTERVAL = 20_000; // 20 seconds
const VOICE_STORAGE_KEY = "aqlan-voice-enabled";
const RECEPTION_FALLBACK = "الاستقبال";

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

/**
 * Build the privacy-safe Arabic announcement text for a patient.
 *
 * Allowed spoken fields: patient name, file number (if name unavailable), room name.
 * Forbidden spoken fields: phone, diagnosis, payment, balance, treatment notes,
 *   medical history, private notes.
 *
 * Fallbacks:
 *   - If patient name is missing → "صاحب الملف رقم [fileNumber]"
 *   - If room is missing → "الاستقبال"
 */
function buildAnnouncementText(patientName: string, patientNumber: string, roomName: string): string {
  const displayName = patientName?.trim() || `صاحب الملف رقم ${patientNumber}`;
  const displayRoom = roomName?.trim() || RECEPTION_FALLBACK;
  return `المريض ${displayName}، يرجى التوجه إلى ${displayRoom}`;
}

/* ─── Arabic Speech Utility ────────────────────────────────────────────────── */

const VOICE_ERROR_MSG = "تعذر تشغيل النداء الصوتي. تأكد من صوت الجهاز والمتصفح.";

/**
 * Speak an Arabic text using the browser SpeechSynthesis API.
 * - Cancels any ongoing speech first
 * - Prefers an Arabic voice (lang starts with "ar")
 * - Falls back to default voice with utterance.lang = "ar-SA"
 * - Returns true if speech was started, false on failure
 */
function speakArabic(text: string, voices: SpeechSynthesisVoice[]): boolean {
  if (typeof window === "undefined" || !window.speechSynthesis) {
    return false;
  }

  try {
    window.speechSynthesis.cancel();

    const utterance = new SpeechSynthesisUtterance(text);
    utterance.lang = "ar-SA";
    utterance.rate = 0.9;
    utterance.pitch = 1;
    utterance.volume = 1;

    // Prefer Arabic voice
    const arabicVoice = voices.find((v) => v.lang.startsWith("ar"));
    if (arabicVoice) {
      utterance.voice = arabicVoice;
    }

    utterance.onstart = () => {
      console.log("[Voice] Speaking:", text);
    };
    utterance.onend = () => {
      console.log("[Voice] Speech ended");
    };
    utterance.onerror = (e) => {
      console.warn("[Voice] Speech error:", e.error);
    };

    window.speechSynthesis.speak(utterance);
    return true;
  } catch (err) {
    console.warn("[Voice] speakArabic failed:", err);
    return false;
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
  // speechSynthesis.getVoices() may return [] initially on Chrome.
  // We must also listen to the onvoiceschanged event.
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
        const hasArabic = available.some((v) => v.lang.startsWith("ar"));
        setArabicVoiceAvailable(hasArabic);
      }
    };

    // Try immediately
    loadVoices();

    // Listen for async voice loading (Chrome loads voices async)
    window.speechSynthesis.onvoiceschanged = loadVoices;

    return () => {
      window.speechSynthesis.onvoiceschanged = null;
    };
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

  // ─── Test voice (speaks a confirmation phrase) ──────────────────────────
  const testVoice = useCallback(() => {
    if (typeof window === "undefined" || !window.speechSynthesis) {
      setVoiceError("النداء الصوتي غير مدعوم في هذا المتصفح");
      return;
    }
    setVoiceError(null);
    const ok = speakArabic("تم تفعيل النداء الصوتي بنجاح", voices);
    if (!ok) {
      setVoiceError(VOICE_ERROR_MSG);
    }
  }, [voices]);

  // ─── Enable voice (audible, not silent) ─────────────────────────────────
  const enableVoice = useCallback(() => {
    if (!voiceSupported) return;

    setVoiceError(null);
    // Speak an audible phrase — this satisfies browser autoplay policy
    // because it is triggered by a user gesture (button click)
    const ok = speakArabic("تم تفعيل النداء الصوتي", voices);
    if (!ok) {
      setVoiceError(VOICE_ERROR_MSG);
      return;
    }

    setVoiceEnabled(true);
    setVoiceStatus("active");
    localStorage.setItem(VOICE_STORAGE_KEY, "true");
  }, [voiceSupported, voices]);

  // ─── Disable voice ──────────────────────────────────────────────────────
  const disableVoice = useCallback(() => {
    setVoiceEnabled(false);
    setVoiceStatus("inactive");
    localStorage.setItem(VOICE_STORAGE_KEY, "false");
    window.speechSynthesis?.cancel();
  }, []);

  // ─── Announce a called patient (auto-trigger on new call) ───────────────
  // This is the automatic announcement that respects repeat prevention.
  // It should only fire when the newest Called queue item changes or
  // calledAt changes for the same item because staff called again.
  const announce = useCallback(
    (patientName: string, patientNumber: string, roomName: string, queueItemId: string, calledAt: string) => {
      if (!voiceEnabled || !voiceSupported) return false;

      // Prevent repeat announcement for the same patient call
      const last = lastAnnouncedRef.current;
      if (last && last.queueItemId === queueItemId && last.calledAt === calledAt) {
        return false;
      }
      lastAnnouncedRef.current = { queueItemId, calledAt };

      const text = buildAnnouncementText(patientName, patientNumber, roomName);
      const ok = speakArabic(text, voices);
      if (!ok) {
        setVoiceError(VOICE_ERROR_MSG);
      }
      return ok;
    },
    [voiceEnabled, voiceSupported, voices]
  );

  // ─── Manual repeat announcement (bypasses dedup check) ──────────────────
  // This is triggered by the "إعادة النداء" button and always speaks
  // the current patient announcement, even if they were already announced.
  // It does NOT change backend state, does NOT update CalledAt,
  // and does NOT create a new queue item.
  const repeatAnnounce = useCallback(
    (patientName: string, patientNumber: string, roomName: string) => {
      if (!voiceEnabled || !voiceSupported) return false;

      const text = buildAnnouncementText(patientName, patientNumber, roomName);
      const ok = speakArabic(text, voices);
      if (!ok) {
        setVoiceError(VOICE_ERROR_MSG);
      }
      return ok;
    },
    [voiceEnabled, voiceSupported, voices]
  );

  // ─── Announce current patient after enabling (for already-called patients) ──
  const announceCurrent = useCallback(
    (patientName: string, patientNumber: string, roomName: string, queueItemId: string, calledAt: string) => {
      if (!voiceEnabled || !voiceSupported) return false;

      // Always announce on enable, even if same patient — but update dedup ref
      lastAnnouncedRef.current = { queueItemId, calledAt };

      const text = buildAnnouncementText(patientName, patientNumber, roomName);
      const ok = speakArabic(text, voices);
      if (!ok) {
        setVoiceError(VOICE_ERROR_MSG);
      }
      return ok;
    },
    [voiceEnabled, voiceSupported, voices]
  );

  return {
    voiceEnabled,
    voiceSupported,
    voiceStatus,
    voiceError,
    setVoiceError,
    arabicVoiceAvailable,
    testVoice,
    enableVoice,
    disableVoice,
    announce,
    repeatAnnounce,
    announceCurrent,
  };
}

/* ─── Main Page ────────────────────────────────────────────────────────────── */
export default function ClinicDisplayPage() {
  const [data, setData] = useState<DisplayData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null);
  const [now, setNow] = useState<Date>(new Date());
  const [pulseKey, setPulseKey] = useState(0);
  const prevLatestCalledRef = useRef<string | null>(null);

  const {
    voiceEnabled,
    voiceSupported,
    voiceStatus,
    voiceError,
    setVoiceError,
    arabicVoiceAvailable,
    testVoice,
    enableVoice,
    disableVoice,
    announce,
    repeatAnnounce,
    announceCurrent,
  } = useArabicVoiceAnnouncement();

  // Track whether we already announced the current patient after enabling
  const announcedAfterEnableRef = useRef(false);

  const fetchDisplay = useCallback(async () => {
    try {
      const apiBase = process.env.NEXT_PUBLIC_API_URL ?? "";
      const res = await fetch(`${apiBase}/api/clinic-queue/display`, {
        cache: "no-store",
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const json: DisplayData = await res.json();

      // Only trigger pulse animation when the latest called patient actually changes
      const newKey = json.latestCalled?.queueItemId ?? null;
      if (newKey !== prevLatestCalledRef.current) {
        prevLatestCalledRef.current = newKey;
        setPulseKey((k) => k + 1);

        // Trigger voice announcement for new Called patient
        // Note: roomName may be empty — buildAnnouncementText handles fallback to "الاستقبال"
        if (json.latestCalled) {
          announce(
            json.latestCalled.patientName,
            json.latestCalled.patientNumber,
            json.latestCalled.roomName,
            json.latestCalled.queueItemId,
            json.latestCalled.calledAt
          );
        }
      }

      setData(json);
      setLastUpdated(new Date());
      setError(null);
    } catch {
      setError("تعذّر تحميل البيانات");
    } finally {
      setLoading(false);
    }
  }, [announce]);

  // After enabling voice, if there's a current Called patient, announce it once
  useEffect(() => {
    if (voiceEnabled && data?.latestCalled && !announcedAfterEnableRef.current) {
      announcedAfterEnableRef.current = true;
      // Small delay to ensure the speech engine is ready after enable
      const timer = setTimeout(() => {
        announceCurrent(
          data.latestCalled!.patientName,
          data.latestCalled!.patientNumber,
          data.latestCalled!.roomName,
          data.latestCalled!.queueItemId,
          data.latestCalled!.calledAt
        );
      }, 300);
      return () => clearTimeout(timer);
    }
  }, [voiceEnabled, data?.latestCalled, announceCurrent]);

  // Reset the after-enable flag when voice is disabled
  useEffect(() => {
    if (!voiceEnabled) {
      announcedAfterEnableRef.current = false;
    }
  }, [voiceEnabled]);

  // Initial fetch + auto-refresh
  useEffect(() => {
    fetchDisplay();
    const refreshInterval = setInterval(fetchDisplay, REFRESH_INTERVAL);
    return () => clearInterval(refreshInterval);
  }, [fetchDisplay]);

  // Live clock every second
  useEffect(() => {
    const clockInterval = setInterval(() => setNow(new Date()), 1_000);
    return () => clearInterval(clockInterval);
  }, []);

  const isFullyEmpty = data && data.waitingCount === 0 && !data.latestCalled && data.recentlyCalled.length === 0;

  /* Voice status text */
  const voiceStatusText = voiceStatus === "active"
    ? "النداء الصوتي مفعل"
    : voiceStatus === "unsupported"
      ? "النداء الصوتي غير مدعوم في هذا المتصفح"
      : "النداء الصوتي متوقف";

  /* Repeat announcement handler — always works for current patient, even without room */
  const handleRepeatAnnounce = useCallback(() => {
    if (!data?.latestCalled) return;

    if (!voiceEnabled) {
      // Prompt user to enable voice first
      setVoiceError("يرجى تفعيل النداء الصوتي أولاً لإعادة النداء.");
      return;
    }

    setVoiceError(null);
    repeatAnnounce(
      data.latestCalled.patientName,
      data.latestCalled.patientNumber,
      data.latestCalled.roomName
    );
  }, [data?.latestCalled, repeatAnnounce, voiceEnabled, setVoiceError]);

  return (
    <div
      dir="rtl"
      className="min-h-screen bg-[#0F172A] text-white flex flex-col"
      style={{ fontFamily: "'Segoe UI', Tahoma, Arial, sans-serif" }}
    >
      {/* ── Header ─────────────────────────────────────────────── */}
      <header className="flex items-center justify-between px-8 md:px-16 py-6 border-b border-white/10 bg-[#0c1322]">
        <div className="flex items-center gap-5">
          <div className="w-14 h-14 rounded-full bg-[#0E7490] flex items-center justify-center text-2xl font-bold shadow-lg shadow-cyan-900/40">
            ع
          </div>
          <div>
            <h1 className="text-2xl md:text-4xl font-bold text-white leading-tight">
              مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان
            </h1>
            <p className="text-lg md:text-xl text-teal-300 mt-1">
              شاشة الطابور
            </p>
          </div>
        </div>

        {/* Live clock */}
        <div className="text-left">
          <p className="text-5xl md:text-6xl font-mono font-bold text-teal-300 tabular-nums">
            {formatClock(now)}
          </p>
          <p className="text-sm text-gray-400 mt-1">
            {now.toLocaleDateString("ar-SA", {
              weekday: "long",
              year: "numeric",
              month: "long",
              day: "numeric",
            })}
          </p>
        </div>
      </header>

      {/* ── Voice Control Bar ────────────────────────────────────── */}
      <div className="px-8 md:px-16 py-2 bg-[#0c1322] border-b border-white/5 flex items-center justify-between flex-wrap gap-2">
        <div className="flex items-center gap-3">
          {voiceEnabled ? (
            <Volume2 className="w-5 h-5 text-teal-400" />
          ) : (
            <VolumeX className="w-5 h-5 text-gray-500" />
          )}
          <span className={`text-sm font-medium ${voiceStatus === "active" ? "text-teal-400" : voiceStatus === "unsupported" ? "text-gray-500" : "text-gray-400"}`}>
            {voiceStatusText}
          </span>
          {/* Arabic voice availability indicator */}
          {voiceStatus === "active" && arabicVoiceAvailable === true && (
            <span className="text-xs text-teal-500">— الصوت العربي متاح</span>
          )}
          {voiceStatus === "active" && arabicVoiceAvailable === false && (
            <span className="text-xs text-amber-500">— سيتم استخدام الصوت الافتراضي</span>
          )}
        </div>
        <div className="flex items-center gap-2 flex-wrap">
          {/* Test voice button — always visible when speech is supported */}
          {voiceSupported && (
            <button
              onClick={testVoice}
              className="px-4 py-1.5 rounded-lg text-sm font-medium bg-white/10 text-gray-300 hover:bg-white/20 transition flex items-center gap-1.5"
            >
              <Volume2 className="w-4 h-4" />
              اختبار الصوت
            </button>
          )}
          {voiceSupported && !voiceEnabled && (
            <button
              onClick={enableVoice}
              className="px-4 py-1.5 rounded-lg text-sm font-bold bg-teal-700 text-white hover:bg-teal-600 transition"
            >
              تفعيل النداء الصوتي
            </button>
          )}
          {voiceSupported && voiceEnabled && (
            <button
              onClick={disableVoice}
              className="px-4 py-1.5 rounded-lg text-sm font-medium bg-white/10 text-gray-300 hover:bg-white/20 transition"
            >
              إيقاف النداء الصوتي
            </button>
          )}
        </div>
      </div>

      {/* Voice error message */}
      {voiceError && (
        <div className="px-8 md:px-16 py-2 bg-red-900/30 border-b border-red-800/30">
          <p className="text-sm text-red-300">{voiceError}</p>
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
            <button
              onClick={fetchDisplay}
              className="mt-4 px-6 py-3 rounded-xl bg-teal-700 text-white font-bold hover:bg-teal-600 transition"
            >
              إعادة المحاولة
            </button>
          </div>
        ) : isFullyEmpty ? (
          /* Fully empty clinic — friendly message */
          <div className="flex flex-col items-center justify-center h-full gap-4 py-20">
            <div className="w-24 h-24 rounded-full bg-white/5 flex items-center justify-center mb-4">
              <CheckCircle2 className="w-14 h-14 text-teal-500/60" />
            </div>
            <p className="text-3xl font-bold text-gray-300">لا يوجد مرضى منادون حالياً</p>
            <p className="text-lg text-gray-500 mt-2">سيتم تحديث الشاشة تلقائياً عند إضافة مرضى</p>
          </div>
        ) : data ? (
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-8 h-full">
            {/* ── Left Column: Latest Called + Waiting Count ── */}
            <div className="lg:col-span-1 space-y-8">
              {/* Latest Called — Prominent */}
              {data.latestCalled ? (
                <div
                  key={pulseKey}
                  className="rounded-3xl bg-gradient-to-br from-teal-900/70 to-cyan-900/50 border border-teal-600/40 p-8 md:p-10 shadow-xl shadow-teal-900/20 animate-[fadeIn_0.6s_ease]"
                >
                  <div className="flex items-center gap-3 mb-6">
                    <Volume2 className="w-8 h-8 text-teal-300 animate-pulse" />
                    <span className="text-2xl font-bold text-teal-300">النداء الأخير</span>
                  </div>
                  <div className="text-center space-y-4">
                    <div className="text-5xl md:text-7xl font-extrabold text-white leading-tight">
                      {data.latestCalled.patientName || `ملف رقم ${data.latestCalled.patientNumber}`}
                    </div>
                    {data.latestCalled.patientName && (
                      <div className="text-2xl md:text-3xl text-teal-200 font-mono">
                        رقم الملف: {data.latestCalled.patientNumber}
                      </div>
                    )}
                    <div className="flex items-center justify-center gap-5 mt-6">
                      <div className="flex items-center gap-3 bg-teal-800/60 px-6 py-3 rounded-2xl">
                        <MapPin className="w-6 h-6 text-teal-300" />
                        <span className="text-2xl font-bold text-teal-200">
                          {data.latestCalled.roomName || RECEPTION_FALLBACK}
                        </span>
                      </div>
                      {data.latestCalled.doctorName && (
                        <div className="flex items-center gap-3 bg-teal-800/60 px-6 py-3 rounded-2xl">
                          <Stethoscope className="w-6 h-6 text-teal-300" />
                          <span className="text-xl text-teal-200">{data.latestCalled.doctorName}</span>
                        </div>
                      )}
                    </div>
                    <div className="flex items-center justify-center gap-2 text-base text-gray-400 mt-3">
                      <Clock className="w-4 h-4" />
                      {formatTimeAgo(data.latestCalled.calledAt)}
                    </div>

                    {/* Replay announcement button — always visible when there is a called patient */}
                    <button
                      onClick={handleRepeatAnnounce}
                      disabled={!voiceEnabled}
                      className={`mt-5 px-8 py-3 rounded-xl font-bold text-lg transition flex items-center gap-3 mx-auto ${
                        voiceEnabled
                          ? "bg-teal-600 text-white hover:bg-teal-500 shadow-lg shadow-teal-900/30 active:scale-95"
                          : "bg-white/10 text-gray-400 cursor-not-allowed"
                      }`}
                      title={voiceEnabled ? "إعادة النداء الصوتي للمريض الحالي" : "فعّل النداء الصوتي أولاً"}
                    >
                      <Volume2 className="w-6 h-6" />
                      إعادة النداء
                    </button>
                  </div>
                </div>
              ) : (
                <div className="rounded-3xl bg-white/5 border border-white/10 p-8 md:p-10 text-center">
                  <PhoneCall className="w-12 h-12 text-gray-500 mx-auto mb-4" />
                  <p className="text-2xl text-gray-400">لم يتم نداء أحد بعد</p>
                </div>
              )}

              {/* Waiting Count */}
              <div className="rounded-3xl bg-white/5 border border-white/10 p-6">
                <div className="flex items-center gap-4 mb-5">
                  <Users className="w-8 h-8 text-amber-400" />
                  <span className="text-2xl font-bold text-amber-300">قائمة الانتظار</span>
                </div>
                <div className="text-center">
                  <span className="text-7xl font-extrabold text-amber-300">{data.waitingCount}</span>
                  <p className="text-xl text-gray-400 mt-2">مريض في الانتظار</p>
                </div>
                {data.waitingList.length > 0 && (
                  <div className="mt-5 space-y-3 max-h-60 overflow-y-auto">
                    {data.waitingList.map((w, i) => (
                      <div
                        key={w.queueItemId || i}
                        className="flex items-center justify-between px-4 py-3 rounded-xl bg-white/5"
                      >
                        <div className="flex items-center gap-3">
                          <span className="text-sm text-gray-500 w-7 text-center font-bold">{i + 1}</span>
                          <span className="text-lg font-medium text-gray-200">{w.patientName}</span>
                          <span className="text-sm text-gray-500 font-mono">{w.patientNumber}</span>
                        </div>
                        {w.doctorName && (
                          <span className="text-sm text-gray-400 flex items-center gap-1">
                            <Stethoscope className="w-3 h-3" />
                            {w.doctorName}
                          </span>
                        )}
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>

            {/* ── Right Column: Recently Called ── */}
            <div className="lg:col-span-2">
              <div className="rounded-3xl bg-white/5 border border-white/10 p-6 h-full">
                <div className="flex items-center gap-4 mb-6">
                  <PhoneCall className="w-8 h-8 text-blue-400" />
                  <span className="text-2xl font-bold text-blue-300">تم النداء مؤخراً</span>
                </div>
                {data.recentlyCalled.length === 0 ? (
                  <div className="text-center py-20">
                    <CheckCircle2 className="w-16 h-16 text-gray-600 mx-auto mb-4" />
                    <p className="text-2xl text-gray-500">لا يوجد نداءات حديثة</p>
                  </div>
                ) : (
                  <div className="space-y-4">
                    {data.recentlyCalled.map((item, i) => {
                      const cfg = getStatusDisplay(item.status);
                      return (
                        <div
                          key={item.queueItemId || i}
                          className="flex items-center gap-5 px-6 py-5 rounded-2xl bg-white/5 border border-white/5 hover:bg-white/10 transition-colors"
                        >
                          {/* Room */}
                          <div className="flex items-center justify-center w-20 h-14 rounded-xl bg-cyan-900/50 border border-cyan-700/30">
                            <span className="text-xl font-bold text-cyan-300">{item.roomName || RECEPTION_FALLBACK}</span>
                          </div>

                          {/* Patient info */}
                          <div className="flex-1 min-w-0">
                            <div className="flex items-center gap-3 flex-wrap">
                              <span className="text-2xl font-bold text-white">{item.patientName}</span>
                              <span className="text-base text-gray-500 font-mono">{item.patientNumber}</span>
                            </div>
                            <div className="flex items-center gap-4 mt-2">
                              {item.doctorName && (
                                <span className="flex items-center gap-1.5 text-base text-gray-400">
                                  <Stethoscope className="w-4 h-4" />
                                  {item.doctorName}
                                </span>
                              )}
                              <span className="text-sm text-gray-500 flex items-center gap-1">
                                <Clock className="w-3.5 h-3.5" />
                                {formatTimeAgo(item.calledAt)}
                              </span>
                            </div>
                          </div>

                          {/* Status badge */}
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
        ) : null}
      </main>

      {/* ── Footer ─────────────────────────────────────────────── */}
      <footer className="border-t border-white/10 px-8 md:px-16 py-4 flex items-center justify-between text-gray-500 text-base bg-[#0c1322]">
        <span>
          {lastUpdated
            ? `آخر تحديث: ${formatClock(lastUpdated)}`
            : "جاري التحميل…"}
        </span>
        <span>
          يتحدث تلقائياً كل ٢٠ ثانية — مركز الدكتور عقلان الكامل
        </span>
      </footer>

      {/* Global CSS for fadeIn animation */}
      <style jsx global>{`
        @keyframes fadeIn {
          from { opacity: 0; transform: scale(0.97); }
          to   { opacity: 1; transform: scale(1); }
        }
      `}</style>
    </div>
  );
}
