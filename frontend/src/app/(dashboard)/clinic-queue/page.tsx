"use client";

import { useState, useEffect, useCallback, useRef } from "react";
import { useRouter } from "next/navigation";
import api from "@/lib/api";
import {
  RefreshCw,
  Loader2,
  UserPlus,
  Search,
  User,
  Stethoscope,
  MapPin,
  Clock,
  CheckCircle2,
  XCircle,
  PhoneCall,
  DoorOpen,
  Play,
  Square,
  ArrowRightLeft,
  AlertTriangle,
  ExternalLink,
  Route,
} from "lucide-react";
import type { PatientListItem } from "@/types/patient";
import type { PaginatedResponse } from "@/types/api";
import { WorkflowNav, WORKFLOW_LINKS } from "@/components/shared/WorkflowNav";
import Link from "next/link";

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
  serviceId?: string;
  serviceName?: string;
  status: "Waiting" | "Called" | "InRoom" | "InProgress" | "Completed" | "Cancelled";
  statusArabic: string;
  calledAt?: string;
  inRoomAt?: string;
  startedAt?: string;
  completedAt?: string;
  notes?: string;
  createdAt: string;
}

interface DbRoom {
  id: string;
  arabicName: string;
  englishName?: string;
  code?: string;
  roomType?: string;
}

interface DoctorOption {
  id: string;
  name: string;
  specialty?: string;
  color?: string;
}

interface EstimatedWait {
  averageWaitMinutes: number;
  waitingCount: number;
}

type QueueStatus = ClinicQueueItem["status"];

/* ─── Constants ────────────────────────────────────────────────────────────── */
const STATUS_CONFIG: Record<QueueStatus, { label: string; color: string; bg: string; icon: React.ReactNode }> = {
  Waiting:    { label: "في الانتظار",  color: "text-amber-700",  bg: "bg-amber-50 border-amber-200",  icon: <Clock className="w-3.5 h-3.5" /> },
  Called:     { label: "تم النداء",    color: "text-blue-700",   bg: "bg-blue-50 border-blue-200",    icon: <PhoneCall className="w-3.5 h-3.5" /> },
  InRoom:     { label: "داخل الغرفة",  color: "text-purple-700", bg: "bg-purple-50 border-purple-200", icon: <DoorOpen className="w-3.5 h-3.5" /> },
  InProgress: { label: "جاري العلاج", color: "text-green-700",  bg: "bg-green-50 border-green-200",  icon: <Play className="w-3.5 h-3.5" /> },
  Completed:  { label: "مكتمل",        color: "text-gray-500",   bg: "bg-gray-100 border-gray-200",   icon: <CheckCircle2 className="w-3.5 h-3.5" /> },
  Cancelled:  { label: "ملغي",         color: "text-red-600",    bg: "bg-red-50 border-red-200",      icon: <XCircle className="w-3.5 h-3.5" /> },
};

const FILTER_OPTIONS = [
  { value: "all",        label: "الكل" },
  { value: "Waiting",    label: "في الانتظار" },
  { value: "Called",     label: "تم النداء" },
  { value: "InRoom",     label: "داخل الغرفة" },
  { value: "InProgress", label: "جاري العلاج" },
  { value: "Completed",  label: "مكتمل" },
  { value: "Cancelled",  label: "ملغي" },
] as const;

/* ─── Helpers ──────────────────────────────────────────────────────────────── */
function formatTime(dateStr?: string): string {
  if (!dateStr) return "—";
  return new Date(dateStr).toLocaleTimeString("ar-SA", { hour: "2-digit", minute: "2-digit", hour12: false });
}

/* ─── Main Page ────────────────────────────────────────────────────────────── */
export default function ClinicQueuePage() {
  const router = useRouter();
  const [items, setItems] = useState<ClinicQueueItem[]>([]);
  const [rooms, setRooms] = useState<DbRoom[]>([]);
  const [doctors, setDoctors] = useState<DoctorOption[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState<string>("all");
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);
  const [lastRefresh, setLastRefresh] = useState<Date | null>(null);
  const [doctorFilter, setDoctorFilter] = useState<string>("");
  const [estimatedWait, setEstimatedWait] = useState<EstimatedWait | null>(null);

  // Call dialog
  const [callModalFor, setCallModalFor] = useState<string | null>(null);

  // Change room dialog
  const [changeRoomFor, setChangeRoomFor] = useState<string | null>(null);

  // Cancel confirmation
  const [cancelConfirmFor, setCancelConfirmFor] = useState<string | null>(null);

  // Add to queue panel
  const [showAddPanel, setShowAddPanel] = useState(false);

  /* ── Fetch today's queue ─────────────────────────────────────────────────── */
  const fetchQueue = useCallback(async () => {
    // Skip auto-refresh when a modal is open to avoid disrupting user actions
    if (callModalFor || changeRoomFor || cancelConfirmFor) return;
    try {
      const params = new URLSearchParams();
      if (doctorFilter) params.set("doctorId", doctorFilter);
      const qs = params.toString() ? `?${params.toString()}` : "";
      const { data } = await api.get<ClinicQueueItem[]>(`/api/clinic-queue/today${qs}`);
      setItems(data);
      setError(null);
      setLastRefresh(new Date());
    } catch (err: unknown) {
      // Don't overwrite user-visible errors during background refresh
      if (!loading) {
        if (err && typeof err === "object" && "response" in err) {
          const axiosErr = err as { response?: { data?: { message?: string } } };
          setError(axiosErr.response?.data?.message || "خطأ في تحميل الانتظار");
        } else {
          setError("فشل الاتصال بالخادم");
        }
      }
    } finally {
      setLoading(false);
    }
  }, [callModalFor, changeRoomFor, cancelConfirmFor, loading, doctorFilter]);

  const fetchRooms = useCallback(async () => {
    try {
      const { data } = await api.get<DbRoom[]>("/api/clinic-queue/rooms");
      if (Array.isArray(data) && data.length > 0 && typeof data[0] === "object" && "arabicName" in data[0]) {
        setRooms(data);
      } else if (Array.isArray(data) && data.length > 0 && typeof data[0] === "string") {
        // Backward compat: old API returns string[]
        setRooms((data as unknown as string[]).map((name, i) => ({ id: String(i), arabicName: name })));
      }
    } catch {
      // fallback
      setRooms([
        { id: "1", arabicName: "غرفة 1" },
        { id: "2", arabicName: "غرفة 2" },
        { id: "3", arabicName: "غرفة 3" },
      ]);
    }
  }, []);

  const fetchDoctors = useCallback(async () => {
    try {
      const { data } = await api.get<DoctorOption[]>("/api/doctors");
      setDoctors(data ?? []);
    } catch {
      setDoctors([]);
    }
  }, []);

  const fetchEstimatedWait = useCallback(async () => {
    try {
      const { data } = await api.get<EstimatedWait>("/api/clinic-queue/estimated-wait");
      setEstimatedWait(data);
    } catch {
      setEstimatedWait(null);
    }
  }, []);

  useEffect(() => {
    fetchQueue();
    fetchRooms();
    fetchDoctors();
    fetchEstimatedWait();
    const interval = setInterval(fetchQueue, 15_000);
    return () => clearInterval(interval);
  }, [fetchQueue, fetchRooms, fetchDoctors, fetchEstimatedWait]);

  // Re-fetch when doctor filter changes
  useEffect(() => {
    fetchQueue();
    fetchEstimatedWait();
  }, [doctorFilter]); // eslint-disable-line react-hooks/exhaustive-deps

  // Today's date in Arabic
  const todayDateStr = new Date().toLocaleDateString("ar-SA", {
    weekday: "long",
    year: "numeric",
    month: "long",
    day: "numeric",
  });

  /* ── Queue action helper ─────────────────────────────────────────────────── */
  const queueAction = async (url: string, body?: object) => {
    setActionLoading(url);
    setSuccessMsg(null);
    setError(null);
    try {
      const res = await api.post(url, body);
      setSuccessMsg(res.data?.message || "تم بنجاح");
      await fetchQueue();
      setTimeout(() => setSuccessMsg(null), 3000);
    } catch (err: unknown) {
      if (err && typeof err === "object" && "response" in err) {
        const axiosErr = err as { response?: { data?: { message?: string } } };
        setError(axiosErr.response?.data?.message || "حدث خطأ");
      } else {
        setError("فشل الاتصال بالخادم");
      }
    } finally {
      setActionLoading(null);
    }
  };

  const queuePatch = async (url: string, body?: object) => {
    setActionLoading(url);
    setSuccessMsg(null);
    setError(null);
    try {
      const res = await api.patch(url, body);
      setSuccessMsg(res.data?.message || "تم بنجاح");
      await fetchQueue();
      setTimeout(() => setSuccessMsg(null), 3000);
    } catch (err: unknown) {
      if (err && typeof err === "object" && "response" in err) {
        const axiosErr = err as { response?: { data?: { message?: string } } };
        setError(axiosErr.response?.data?.message || "حدث خطأ");
      } else {
        setError("فشل الاتصال بالخادم");
      }
    } finally {
      setActionLoading(null);
    }
  };

  /* ── Counters ────────────────────────────────────────────────────────────── */
  const waitingCount   = items.filter((i) => i.status === "Waiting").length;
  const calledCount    = items.filter((i) => i.status === "Called").length;
  const inRoomCount    = items.filter((i) => i.status === "InRoom").length;
  const inProgressCount = items.filter((i) => i.status === "InProgress").length;

  /* ── Filtered + searched items ───────────────────────────────────────────── */
  const activeItems = items.filter((i) => !["Completed", "Cancelled"].includes(i.status));
  const completedItems = items.filter((i) => ["Completed", "Cancelled"].includes(i.status));

  const filteredActive = activeItems.filter((item) => {
    if (filter !== "all" && item.status !== filter) return false;
    if (search.trim()) {
      const q = search.trim().toLowerCase();
      return (
        item.patientName.toLowerCase().includes(q) ||
        item.patientNumber.toLowerCase().includes(q)
      );
    }
    return true;
  });

  const filteredCompleted = completedItems.filter((item) => {
    if (filter !== "all" && item.status !== filter) return false;
    if (search.trim()) {
      const q = search.trim().toLowerCase();
      return (
        item.patientName.toLowerCase().includes(q) ||
        item.patientNumber.toLowerCase().includes(q)
      );
    }
    return true;
  });

  /* ── Render ──────────────────────────────────────────────────────────────── */
  return (
    <div dir="rtl" className="p-4 md:p-6 space-y-6 page-content">
      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-4">
        <div>
          <h1 className="text-2xl font-extrabold" style={{ color: "#0d2137" }}>
            الانتظار
          </h1>
          <p className="text-sm mt-1" style={{ color: "#64748b" }}>
            {todayDateStr} — تحديث تلقائي كل ١٥ ثانية
            {lastRefresh && (
              <span className="text-xs opacity-70"> (آخر تحديث: {formatTime(lastRefresh.toISOString())})</span>
            )}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={() => setShowAddPanel(!showAddPanel)}
            className="flex items-center gap-1.5 px-4 py-2 text-sm font-bold text-white rounded-xl transition-colors hover:opacity-90"
            style={{ background: "#3d7ab5" }}
          >
            <UserPlus className="w-4 h-4" />
            إضافة للانتظار
          </button>
          <button
            onClick={fetchQueue}
            disabled={loading}
            className="flex items-center gap-1.5 px-3 py-2 text-sm font-medium rounded-xl transition-colors border border-gray-200 bg-white text-gray-600 hover:bg-gray-50"
          >
            <RefreshCw className={`w-4 h-4 ${loading ? "animate-spin" : ""}`} />
            تحديث
          </button>
        </div>
      </div>

      {/* Workflow Navigation */}
      <WorkflowNav
        links={[
          WORKFLOW_LINKS.backToDailyOps(),
          WORKFLOW_LINKS.appointments(),
          WORKFLOW_LINKS.clinicDisplay(),
          WORKFLOW_LINKS.patientJourney(),
          WORKFLOW_LINKS.payments(),
        ]}
        currentPage="/clinic-queue"
      />

      {/* Success / Error messages */}
      {successMsg && (
        <div className="px-4 py-3 rounded-xl text-sm font-medium bg-emerald-50 text-emerald-700 border border-emerald-200">
          {successMsg}
        </div>
      )}
      {error && (
        <div className="px-4 py-3 rounded-xl text-sm font-medium bg-red-50 text-red-700 border border-red-200 flex items-center justify-between">
          <span>{error}</span>
          <button onClick={() => setError(null)} className="text-red-400 hover:text-red-600 text-lg leading-none">&times;</button>
        </div>
      )}

      {/* Counters */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
        <CounterCard label="في الانتظار" count={waitingCount} icon={<Clock className="w-5 h-5" />} color="#f59e0b" bgColor="#fffbeb" borderColor="#fde68a" />
        <CounterCard label="تم النداء" count={calledCount} icon={<PhoneCall className="w-5 h-5" />} color="#3b82f6" bgColor="#eff6ff" borderColor="#bfdbfe" />
        <CounterCard label="داخل الغرفة" count={inRoomCount} icon={<DoorOpen className="w-5 h-5" />} color="#8b5cf6" bgColor="#f5f3ff" borderColor="#ddd6fe" />
        <CounterCard label="جاري العلاج" count={inProgressCount} icon={<Play className="w-5 h-5" />} color="#10b981" bgColor="#ecfdf5" borderColor="#a7f3d0" />
      </div>

      {/* Estimated wait + Doctor filter */}
      <div className="flex flex-wrap gap-3 items-center">
        {/* Doctor filter dropdown */}
        {doctors.length > 0 && (
          <div className="flex items-center gap-2">
            <Stethoscope className="w-4 h-4" style={{ color: "#3d7ab5" }} />
            <select
              value={doctorFilter}
              onChange={(e) => setDoctorFilter(e.target.value)}
              className="px-3 py-2 rounded-xl text-sm border border-gray-200 focus:border-[#3d7ab5] outline-none focus:ring-2 focus:ring-[#3d7ab5]/20 bg-white"
            >
              <option value="">كل الأطباء</option>
              {doctors.map((d) => (
                <option key={d.id} value={d.id}>{d.name}</option>
              ))}
            </select>
          </div>
        )}

        {/* Estimated wait time card */}
        {estimatedWait && (
          <div
            className="flex items-center gap-3 px-4 py-2.5 rounded-xl border"
            style={{ background: "#eff6ff", borderColor: "#bfdbfe" }}
          >
            <Clock className="w-4 h-4" style={{ color: "#3b82f6" }} />
            <div className="text-sm">
              <span className="font-bold" style={{ color: "#1d4ed8" }}>
                متوسط وقت الانتظار: {estimatedWait.averageWaitMinutes} دقيقة
              </span>
              <span className="mx-2" style={{ color: "#93c5fd" }}>|</span>
              <span style={{ color: "#3b82f6" }}>
                عدد المنتظرين: {estimatedWait.waitingCount}
              </span>
            </div>
          </div>
        )}
      </div>

      {/* Add to Queue Panel */}
      {showAddPanel && (
        <AddToQueuePanel
          rooms={rooms}
          doctors={doctors}
          onAdded={() => {
            fetchQueue();
            fetchEstimatedWait();
            setShowAddPanel(false);
          }}
          onClose={() => setShowAddPanel(false)}
        />
      )}

      {/* Search + Filter */}
      <div className="flex flex-wrap gap-3 items-center">
        <div className="relative flex-1 min-w-[200px]">
          <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400 pointer-events-none" />
          <input
            type="text"
            placeholder="بحث بالاسم أو رقم المريض..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="w-full pr-10 pl-4 py-2.5 rounded-xl text-sm border border-gray-200 focus:border-[#3d7ab5] outline-none focus:ring-2 focus:ring-[#3d7ab5]/20 bg-white"
          />
          {search && (
            <button onClick={() => setSearch("")} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600">&times;</button>
          )}
        </div>
        <div className="flex gap-1.5 flex-wrap">
          {FILTER_OPTIONS.map((opt) => (
            <button
              key={opt.value}
              onClick={() => setFilter(opt.value)}
              className="px-3 py-2 rounded-lg text-xs font-medium transition-colors"
              style={
                filter === opt.value
                  ? { background: "#3d7ab5", color: "#fff" }
                  : { background: "#f1f5f9", color: "#475569" }
              }
            >
              {opt.label}
            </button>
          ))}
        </div>
      </div>

      {/* Active Queue List */}
      {loading ? (
        <div className="flex items-center justify-center py-20">
          <Loader2 className="w-8 h-8 animate-spin" style={{ color: "#3d7ab5" }} />
        </div>
      ) : filteredActive.length === 0 && filteredCompleted.length === 0 ? (
        <div className="text-center py-20" style={{ color: "#94a3b8" }}>
          <UserPlus className="w-12 h-12 mx-auto mb-3 opacity-30" />
          <p className="font-medium">لا توجد مرضى في الانتظار</p>
          <p className="text-sm mt-1">اضغط &quot;إضافة للانتظار&quot; لإضافة مريض جديد</p>
        </div>
      ) : (
        <>
          {/* Active items */}
          {filteredActive.length > 0 && (
            <div className="space-y-3">
              <h2 className="text-sm font-bold" style={{ color: "#64748b" }}>
                الانتظار النشط ({filteredActive.length})
              </h2>
              <div className="grid gap-3">
                {filteredActive.map((item) => (
                  <QueueCard
                    key={item.id}
                    item={item}
                    actionLoading={actionLoading}
                    onCall={() => setCallModalFor(item.id)}
                    onEnterRoom={() => queueAction(`/api/clinic-queue/${item.id}/enter-room`)}
                    onStart={() => queueAction(`/api/clinic-queue/${item.id}/start`)}
                    onOpenVisit={() => { if (item.visitId) router.push(`/visits/${item.visitId}`); }}
                    onComplete={() => queueAction(`/api/clinic-queue/${item.id}/complete`)}
                    onCancel={() => setCancelConfirmFor(item.id)}
                    onChangeRoom={() => setChangeRoomFor(item.id)}
                  />
                ))}
              </div>
            </div>
          )}

          {/* Completed / Cancelled items */}
          {filteredCompleted.length > 0 && (
            <div className="space-y-3">
              <h2 className="text-sm font-bold" style={{ color: "#64748b" }}>
                المنتهون ({filteredCompleted.length})
              </h2>
              <div className="grid gap-3">
                {filteredCompleted.map((item) => (
                  <QueueCard
                    key={item.id}
                    item={item}
                    actionLoading={actionLoading}
                    onCall={() => {}}
                    onEnterRoom={() => {}}
                    onStart={() => {}}
                    onOpenVisit={() => { if (item.visitId) router.push(`/visits/${item.visitId}`); }}
                    onComplete={() => {}}
                    onCancel={() => {}}
                    onChangeRoom={() => {}}
                  />
                ))}
              </div>
            </div>
          )}
        </>
      )}

      {/* Call Dialog */}
      {callModalFor && (
        <RoomSelectDialog
          title="نداء المريض"
          description="اختر الغرفة للنداء"
          rooms={rooms}
          onConfirm={(room) => {
            queueAction(`/api/clinic-queue/${callModalFor}/call`, { roomName: room });
            setCallModalFor(null);
          }}
          onCancel={() => setCallModalFor(null)}
          loading={actionLoading !== null}
        />
      )}

      {/* Change Room Dialog */}
      {changeRoomFor && (
        <RoomSelectDialog
          title="تغيير الغرفة"
          description="اختر الغرفة الجديدة"
          rooms={rooms}
          onConfirm={(room) => {
            queuePatch(`/api/clinic-queue/${changeRoomFor}/room`, { roomName: room });
            setChangeRoomFor(null);
          }}
          onCancel={() => setChangeRoomFor(null)}
          loading={actionLoading !== null}
        />
      )}

      {/* Cancel Confirmation */}
      {cancelConfirmFor && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/50 backdrop-blur-sm" onClick={() => setCancelConfirmFor(null)} />
          <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-sm p-6 space-y-4">
            <div className="flex items-start gap-4">
              <div className="flex-shrink-0 w-10 h-10 rounded-full bg-red-100 flex items-center justify-center">
                <AlertTriangle className="w-5 h-5 text-red-600" />
              </div>
              <div>
                <h3 className="text-base font-bold text-gray-900">إلغاء من الانتظار</h3>
                <p className="text-sm text-gray-600 mt-1">هل أنت متأكد من إلغاء هذا المريض من الانتظار؟</p>
              </div>
            </div>
            <div className="flex items-center justify-end gap-3 pt-2">
              <button
                onClick={() => setCancelConfirmFor(null)}
                className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 transition"
              >
                تراجع
              </button>
              <button
                onClick={() => {
                  queueAction(`/api/clinic-queue/${cancelConfirmFor}/cancel`);
                  setCancelConfirmFor(null);
                }}
                disabled={actionLoading !== null}
                className="px-4 py-2 text-sm font-medium text-white rounded-lg bg-red-600 hover:bg-red-700 transition disabled:opacity-50"
              >
                إلغاء من الانتظار
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

/* ─── Counter Card ──────────────────────────────────────────────────────────── */
function CounterCard({
  label,
  count,
  icon,
  color,
  bgColor,
  borderColor,
}: {
  label: string;
  count: number;
  icon: React.ReactNode;
  color: string;
  bgColor: string;
  borderColor: string;
}) {
  return (
    <div
      className="rounded-xl p-4 border flex items-center gap-3"
      style={{ background: bgColor, borderColor }}
    >
      <div className="w-10 h-10 rounded-lg flex items-center justify-center" style={{ background: `${color}20`, color }}>
        {icon}
      </div>
      <div>
        <div className="text-2xl font-bold" style={{ color }}>{count}</div>
        <div className="text-xs font-medium" style={{ color: "#64748b" }}>{label}</div>
      </div>
    </div>
  );
}

/* ─── Queue Card ─────────────────────────────────────────────────────────────── */
function QueueCard({
  item,
  actionLoading,
  onCall,
  onEnterRoom,
  onStart,
  onOpenVisit,
  onComplete,
  onCancel,
  onChangeRoom,
}: {
  item: ClinicQueueItem;
  actionLoading: string | null;
  onCall: () => void;
  onEnterRoom: () => void;
  onStart: () => void;
  onOpenVisit: () => void;
  onComplete: () => void;
  onCancel: () => void;
  onChangeRoom: () => void;
}) {
  const isLoading = actionLoading !== null && actionLoading.includes(item.id);
  const cfg = STATUS_CONFIG[item.status] || STATUS_CONFIG.Waiting;
  const isTerminal = item.status === "Completed" || item.status === "Cancelled";

  return (
    <div
      className={`rounded-2xl p-4 border transition-shadow hover:shadow-md ${isTerminal ? "opacity-60" : ""}`}
      style={{ background: "#fff", borderColor: "#e2e8f0" }}
    >
      <div className="flex items-start justify-between gap-3 flex-wrap">
        {/* Patient info */}
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 mb-1 flex-wrap">
            <User className="w-4 h-4 flex-shrink-0" style={{ color: "#3d7ab5" }} />
            <span className="font-bold text-sm" style={{ color: "#0d2137" }}>
              {item.patientName}
            </span>
            <span
              className="text-xs px-2 py-0.5 rounded-md font-medium font-mono"
              style={{ background: "#f1f5f9", color: "#64748b" }}
            >
              {item.patientNumber}
            </span>
          </div>
          <div className="flex items-center gap-3 text-xs flex-wrap" style={{ color: "#64748b" }}>
            <span className="flex items-center gap-1">
              <Stethoscope className="w-3 h-3" />
              {item.doctorName || "—"}
            </span>
            {item.serviceName && (
              <span className="flex items-center gap-1 text-[#3d7ab5] font-medium">
                {item.serviceName}
              </span>
            )}
            {item.appointmentTime && (
              <span className="flex items-center gap-1">
                <Clock className="w-3 h-3" />
                {item.appointmentTime}
              </span>
            )}
            {item.calledAt && (
              <span className="flex items-center gap-1">
                النداء: {formatTime(item.calledAt)}
              </span>
            )}
            {item.visitId && (item.status === "InProgress" || item.status === "Completed") && (
              <button
                onClick={onOpenVisit}
                className="flex items-center gap-1 text-[#3d7ab5] hover:underline font-medium"
              >
                <ExternalLink className="w-3 h-3" />
                فتح الزيارة
              </button>
            )}
            {item.appointmentId && !isTerminal && (
              <Link
                href="/patient-journey"
                className="flex items-center gap-1 text-[#22c55e] hover:underline font-medium"
              >
                <Route className="w-3 h-3" />
                فتح رحلة المرضى
              </Link>
            )}
          </div>
        </div>

        {/* Status + Room */}
        <div className="flex items-center gap-2 flex-wrap">
          {item.roomName && (
            <span
              className="text-xs px-2.5 py-1 rounded-lg font-bold flex items-center gap-1"
              style={{ background: "#3d7ab5", color: "#fff" }}
            >
              <MapPin className="w-3 h-3" />
              {item.roomName}
            </span>
          )}
          <span
            className={`inline-flex items-center gap-1 px-2.5 py-1 rounded-lg text-xs font-semibold border ${cfg.bg} ${cfg.color}`}
          >
            {cfg.icon}
            {item.statusArabic || cfg.label}
          </span>
        </div>
      </div>

      {/* Action buttons — only shown for non-terminal statuses */}
      {!isTerminal && (
        <div className="flex gap-2 mt-3 flex-wrap">
          {item.status === "Waiting" && (
            <>
              <ActionButton label="نداء المريض" onClick={onCall} loading={isLoading} color="#3d7ab5" icon={<PhoneCall className="w-3 h-3" />} />
              <ActionButton label="تغيير الغرفة" onClick={onChangeRoom} loading={isLoading} color="#64748b" icon={<ArrowRightLeft className="w-3 h-3" />} />
              <ActionButton label="إلغاء" onClick={onCancel} loading={isLoading} color="#ef4444" icon={<XCircle className="w-3 h-3" />} />
            </>
          )}
          {item.status === "Called" && (
            <>
              <ActionButton label="إدخال إلى الغرفة" onClick={onEnterRoom} loading={isLoading} color="#7c3aed" icon={<DoorOpen className="w-3 h-3" />} />
              <ActionButton label="تغيير الغرفة" onClick={onChangeRoom} loading={isLoading} color="#64748b" icon={<ArrowRightLeft className="w-3 h-3" />} />
              <ActionButton label="إلغاء" onClick={onCancel} loading={isLoading} color="#ef4444" icon={<XCircle className="w-3 h-3" />} />
            </>
          )}
          {item.status === "InRoom" && (
            <>
              <ActionButton label="بدء الزيارة" onClick={onStart} loading={isLoading} color="#059669" icon={<Play className="w-3 h-3" />} />
              <ActionButton label="تغيير الغرفة" onClick={onChangeRoom} loading={isLoading} color="#64748b" icon={<ArrowRightLeft className="w-3 h-3" />} />
              <ActionButton label="إلغاء" onClick={onCancel} loading={isLoading} color="#ef4444" icon={<XCircle className="w-3 h-3" />} />
            </>
          )}
          {item.status === "InProgress" && (
            <>
              <ActionButton label="فتح الزيارة" onClick={onOpenVisit} loading={isLoading} color="#3d7ab5" icon={<ExternalLink className="w-3 h-3" />} />
              <ActionButton label="إنهاء" onClick={onComplete} loading={isLoading} color="#0d2137" icon={<Square className="w-3 h-3" />} />
            </>
          )}
        </div>
      )}

      {/* Terminal items: show open visit if available */}
      {isTerminal && item.visitId && (
        <div className="mt-2">
          <button
            onClick={onOpenVisit}
            className="inline-flex items-center gap-1.5 text-xs font-medium text-[#3d7ab5] hover:underline"
          >
            <ExternalLink className="w-3 h-3" />
            فتح الزيارة
          </button>
        </div>
      )}
    </div>
  );
}

/* ─── Action Button ──────────────────────────────────────────────────────────── */
function ActionButton({
  label,
  onClick,
  loading,
  color,
  icon,
}: {
  label: string;
  onClick: () => void;
  loading: boolean;
  color: string;
  icon?: React.ReactNode;
}) {
  return (
    <button
      onClick={onClick}
      disabled={loading}
      className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-bold text-white transition-opacity hover:opacity-90 disabled:opacity-50"
      style={{ background: color }}
    >
      {loading ? <Loader2 className="w-3 h-3 animate-spin" /> : icon}
      {label}
    </button>
  );
}

/* ─── Room Select Dialog ─────────────────────────────────────────────────────── */
function RoomSelectDialog({
  title,
  description,
  rooms,
  onConfirm,
  onCancel,
  loading,
}: {
  title: string;
  description: string;
  rooms: DbRoom[];
  onConfirm: (room: string) => void;
  onCancel: () => void;
  loading: boolean;
}) {
  const roomNames = rooms.map((r) => r.arabicName);
  const [selectedRoom, setSelectedRoom] = useState(roomNames[0] || "غرفة 1");

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/50 backdrop-blur-sm" onClick={onCancel} />
      <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-sm p-6" onClick={(e) => e.stopPropagation()}>
        <h3 className="text-lg font-bold mb-1" style={{ color: "#0d2137" }}>{title}</h3>
        <p className="text-sm mb-4" style={{ color: "#64748b" }}>{description}</p>
        <div className="grid grid-cols-3 gap-3">
          {roomNames.map((room) => (
            <button
              key={room}
              onClick={() => setSelectedRoom(room)}
              className="py-3 rounded-xl text-sm font-bold transition-all border-2"
              style={
                selectedRoom === room
                  ? { background: "#3d7ab5", color: "#fff", borderColor: "#3d7ab5" }
                  : { background: "#f8fafc", color: "#475569", borderColor: "#e2e8f0" }
              }
            >
              {room}
            </button>
          ))}
        </div>
        <div className="flex gap-2 mt-4">
          <button
            onClick={() => onConfirm(selectedRoom)}
            disabled={loading}
            className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white transition-colors hover:opacity-90 disabled:opacity-50"
            style={{ background: "#3d7ab5" }}
          >
            {loading ? <Loader2 className="w-4 h-4 animate-spin mx-auto" /> : "تأكيد"}
          </button>
          <button
            onClick={onCancel}
            className="flex-1 py-2.5 rounded-xl text-sm font-medium border border-gray-200 hover:bg-gray-50 transition"
            style={{ color: "#475569" }}
          >
            إلغاء
          </button>
        </div>
      </div>
    </div>
  );
}

/* ─── Add To Queue Panel ─────────────────────────────────────────────────────── */
function AddToQueuePanel({
  rooms,
  doctors,
  onAdded,
  onClose,
}: {
  rooms: DbRoom[];
  doctors: DoctorOption[];
  onAdded: () => void;
  onClose: () => void;
}) {
  const [patientSearch, setPatientSearch] = useState("");
  const [patientResults, setPatientResults] = useState<PatientListItem[]>([]);
  const [selectedPatient, setSelectedPatient] = useState<PatientListItem | null>(null);
  const [searching, setSearching] = useState(false);
  const [selectedRoom, setSelectedRoom] = useState("");
  const [selectedDoctorId, setSelectedDoctorId] = useState("");
  const [adding, setAdding] = useState(false);
  const [addError, setAddError] = useState<string | null>(null);
  const searchRef = useRef<HTMLDivElement>(null);
  const [showDropdown, setShowDropdown] = useState(false);

  // Search patients with debounce
  useEffect(() => {
    if (patientSearch.length < 2) {
      setPatientResults([]);
      setShowDropdown(false);
      return;
    }
    setSearching(true);
    const t = setTimeout(() => {
      api
        .get<PaginatedResponse<PatientListItem>>(`/api/patients?search=${encodeURIComponent(patientSearch)}&pageSize=8`)
        .then((r) => {
          setPatientResults(r.data?.data ?? []);
          setShowDropdown(true);
        })
        .catch(() => setPatientResults([]))
        .finally(() => setSearching(false));
    }, 300);
    return () => clearTimeout(t);
  }, [patientSearch]);

  // Close dropdown on outside click
  useEffect(() => {
    if (!showDropdown) return;
    const h = (e: MouseEvent) => {
      if (searchRef.current && !searchRef.current.contains(e.target as Node)) setShowDropdown(false);
    };
    document.addEventListener("mousedown", h);
    return () => document.removeEventListener("mousedown", h);
  }, [showDropdown]);

  const handleAdd = async () => {
    if (!selectedPatient) return;
    setAdding(true);
    setAddError(null);
    try {
      await api.post("/api/clinic-queue", {
        patientId: selectedPatient.id,
        roomName: selectedRoom || undefined,
        doctorId: selectedDoctorId || undefined,
      });
      onAdded();
    } catch (err: unknown) {
      if (err && typeof err === "object" && "response" in err) {
        const axiosErr = err as { response?: { data?: { message?: string } } };
        setAddError(axiosErr.response?.data?.message || "حدث خطأ أثناء الإضافة");
      } else {
        setAddError("فشل الاتصال بالخادم");
      }
    } finally {
      setAdding(false);
    }
  };

  return (
    <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-5 space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-base font-bold" style={{ color: "#0d2137" }}>إضافة مريض للانتظار</h2>
        <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-xl leading-none">&times;</button>
      </div>

      {addError && (
        <div className="px-3 py-2 rounded-lg text-sm font-medium bg-red-50 text-red-700 border border-red-200">{addError}</div>
      )}

      {/* Patient Search */}
      <div ref={searchRef} className="relative">
        <label className="block text-sm font-semibold mb-1.5" style={{ color: "#0d2137" }}>المريض</label>
        <div className="relative">
          <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400 pointer-events-none" />
          {searching && (
            <Loader2 className="absolute left-3 top-1/2 -translate-y-1/2 w-3.5 h-3.5 animate-spin" style={{ color: "#3d7ab5" }} />
          )}
          <input
            type="text"
            value={selectedPatient ? `${selectedPatient.fullName} (${selectedPatient.patientNumber})` : patientSearch}
            onChange={(e) => {
              if (selectedPatient) {
                setSelectedPatient(null);
                setPatientSearch(e.target.value);
              } else {
                setPatientSearch(e.target.value);
              }
            }}
            onFocus={() => { if (patientSearch.length >= 2 && patientResults.length > 0) setShowDropdown(true); }}
            placeholder="ابحث بالاسم أو رقم المريض..."
            className="w-full pr-10 pl-4 py-2.5 rounded-xl text-sm border border-gray-200 focus:border-[#3d7ab5] outline-none focus:ring-2 focus:ring-[#3d7ab5]/20 bg-white"
          />
        </div>
        {showDropdown && !selectedPatient && (
          <div className="absolute z-50 w-full mt-1 bg-white rounded-lg border border-[#e8f0f9] shadow-xl max-h-52 overflow-y-auto">
            {searching && <p className="px-3 py-3 text-sm text-gray-400 text-center">جارٍ البحث...</p>}
            {!searching && patientResults.length === 0 && (
              <p className="px-3 py-3 text-sm text-gray-400 text-center">لا توجد نتائج</p>
            )}
            {!searching && patientResults.map((p) => (
              <button
                key={p.id}
                type="button"
                onMouseDown={() => {
                  setSelectedPatient(p);
                  setPatientSearch("");
                  setShowDropdown(false);
                }}
                className="w-full text-start px-3 py-2.5 text-sm hover:bg-gray-50 flex items-center justify-between transition"
              >
                <span className="font-medium" style={{ color: "#0d2137" }}>{p.fullName}</span>
                <span className="text-xs text-gray-400 font-mono">{p.patientNumber}</span>
              </button>
            ))}
          </div>
        )}
      </div>

      {/* Doctor selection */}
      {doctors.length > 0 && (
        <div>
          <label className="block text-sm font-semibold mb-1.5" style={{ color: "#0d2137" }}>
            الطبيب <span className="font-normal text-gray-400">(مطلوب)</span>
          </label>
          <select
            value={selectedDoctorId}
            onChange={(e) => setSelectedDoctorId(e.target.value)}
            className="w-full px-3 py-2.5 rounded-xl text-sm border border-gray-200 focus:border-[#3d7ab5] outline-none focus:ring-2 focus:ring-[#3d7ab5]/20 bg-white"
          >
            <option value="">اختر الطبيب...</option>
            {doctors.map((d) => (
              <option key={d.id} value={d.id}>{d.name}{d.specialty ? ` — ${d.specialty}` : ""}</option>
            ))}
          </select>
        </div>
      )}

      {/* Room Selector */}
      <div>
        <label className="block text-sm font-semibold mb-1.5" style={{ color: "#0d2137" }}>الغرفة <span className="font-normal text-gray-400">(اختياري)</span></label>
        <div className="flex gap-2 flex-wrap">
          <button
            onClick={() => setSelectedRoom("")}
            className="px-3 py-2 rounded-lg text-xs font-medium transition-all border-2"
            style={
              !selectedRoom
                ? { background: "#3d7ab5", color: "#fff", borderColor: "#3d7ab5" }
                : { background: "#f8fafc", color: "#475569", borderColor: "#e2e8f0" }
            }
          >
            تلقائي
          </button>
          {rooms.map((room) => (
            <button
              key={room.id}
              onClick={() => setSelectedRoom(room.arabicName)}
              className="px-3 py-2 rounded-lg text-xs font-medium transition-all border-2"
              style={
                selectedRoom === room.arabicName
                  ? { background: "#3d7ab5", color: "#fff", borderColor: "#3d7ab5" }
                  : { background: "#f8fafc", color: "#475569", borderColor: "#e2e8f0" }
              }
            >
              {room.arabicName}
            </button>
          ))}
        </div>
      </div>

      {/* Submit */}
      <div className="flex gap-2">
        <button
          onClick={handleAdd}
          disabled={!selectedPatient || adding}
          className="flex-1 flex items-center justify-center gap-2 py-2.5 rounded-xl text-sm font-bold text-white transition-colors hover:opacity-90 disabled:opacity-50 disabled:cursor-not-allowed"
          style={{ background: "#3d7ab5" }}
        >
          {adding ? <Loader2 className="w-4 h-4 animate-spin" /> : <UserPlus className="w-4 h-4" />}
          إضافة للانتظار
        </button>
        <button
          onClick={onClose}
          className="px-6 py-2.5 rounded-xl text-sm font-medium border border-gray-200 hover:bg-gray-50 transition"
          style={{ color: "#475569" }}
        >
          إلغاء
        </button>
      </div>
    </div>
  );
}
