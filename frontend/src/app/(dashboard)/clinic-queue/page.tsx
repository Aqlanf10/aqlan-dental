"use client";

import { useState, useEffect, useCallback } from "react";
import { QueueItem } from "@/types/appointment";
import api from "@/lib/api";

/* ─── Constants ──────────────────────────────────────────────────────────────── */
const ROOM_NAMES = ["غرفة 1", "غرفة 2", "غرفة 3"];

const STATUS_LABELS: Record<string, string> = {
  Scheduled: "مجدول",
  Confirmed: "مؤكد",
  Arrived: "وصل",
  Waiting: "في الانتظار",
  Called: "تم النداء",
  InRoom: "داخل الغرفة",
  InProgress: "قيد الزيارة",
  Completed: "مكتمل",
  Cancelled: "ملغى",
  NoShow: "لم يحضر",
};

const STATUS_COLORS: Record<string, string> = {
  Scheduled: "bg-gray-100 text-gray-700",
  Confirmed: "bg-blue-50 text-blue-700",
  Arrived: "bg-cyan-50 text-cyan-700",
  Waiting: "bg-amber-50 text-amber-700",
  Called: "bg-orange-50 text-orange-700",
  InRoom: "bg-purple-50 text-purple-700",
  InProgress: "bg-green-50 text-green-700",
  Completed: "bg-emerald-50 text-emerald-700",
  Cancelled: "bg-red-50 text-red-600",
  NoShow: "bg-gray-100 text-gray-500",
};

const FILTER_OPTIONS = [
  { value: "all", label: "الكل" },
  { value: "Waiting", label: "في الانتظار" },
  { value: "Called", label: "تم النداء" },
  { value: "InRoom", label: "داخل الغرفة" },
  { value: "InProgress", label: "قيد الزيارة" },
  { value: "Completed", label: "مكتمل" },
];

/* ─── Component ──────────────────────────────────────────────────────────────── */
export default function ClinicQueuePage() {
  const [items, setItems] = useState<QueueItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState("all");
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [roomModalFor, setRoomModalFor] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  /* ── Fetch today's queue ─────────────────────────────────────────────────── */
  const fetchQueue = useCallback(async () => {
    try {
      const { data } = await api.get("/api/clinic-queue/today");
      setItems(data);
      setError(null);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "خطأ غير معروف");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchQueue();
    const interval = setInterval(fetchQueue, 15000);
    return () => clearInterval(interval);
  }, [fetchQueue]);

  /* ── Queue action helper ─────────────────────────────────────────────────── */
  const queueAction = async (url: string, method = "POST", body?: object) => {
    setActionLoading(url);
    setSuccessMsg(null);
    try {
      const config = method === "PUT" ? api.put : api.post;
      const res = await config(url, body);
      setSuccessMsg(res.data.message || "تم بنجاح");
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

  /* ── Filtered + searched items ───────────────────────────────────────────── */
  const filtered = items.filter((item) => {
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
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-4">
        <div>
          <h1 className="text-2xl font-bold" style={{ color: "#0F1B2D" }}>
            طابور العيادة
          </h1>
          <p className="text-sm mt-1" style={{ color: "#64748b" }}>
            إدارة مواعيد اليوم وتنقل المرضى
          </p>
        </div>
        <button
          onClick={fetchQueue}
          className="px-4 py-2 text-sm font-medium rounded-xl transition-colors"
          style={{ background: "#eef3f9", color: "#0F1B2D" }}
        >
          تحديث
        </button>
      </div>

      {/* Success / Error messages */}
      {successMsg && (
        <div
          className="px-4 py-3 rounded-xl text-sm font-medium"
          style={{ background: "#ecfdf5", color: "#065f46" }}
        >
          {successMsg}
        </div>
      )}
      {error && (
        <div
          className="px-4 py-3 rounded-xl text-sm font-medium"
          style={{ background: "#fef2f2", color: "#991b1b" }}
        >
          {error}
        </div>
      )}

      {/* Search + Filter */}
      <div className="flex flex-wrap gap-3 items-center">
        <input
          type="text"
          placeholder="بحث بالاسم أو رقم المريض..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="px-4 py-2.5 rounded-xl text-sm border flex-1 min-w-[200px] focus:outline-none focus:ring-2"
          style={{
            borderColor: "#e2e8f0",
            direction: "rtl",
          }}
        />
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

      {/* Queue List */}
      {loading ? (
        <div className="text-center py-12" style={{ color: "#94a3b8" }}>
          جارٍ تحميل الطابور...
        </div>
      ) : filtered.length === 0 ? (
        <div className="text-center py-12" style={{ color: "#94a3b8" }}>
          لا توجد مواعيد مطابقة
        </div>
      ) : (
        <div className="grid gap-3">
          {filtered.map((item) => (
            <QueueCard
              key={item.appointmentId}
              item={item}
              actionLoading={actionLoading}
              onArrive={() =>
                queueAction(`/api/clinic-queue/arrive/${item.appointmentId}`)
              }
              onCall={() => setRoomModalFor(item.appointmentId)}
              onSendToRoom={() =>
                queueAction(`/api/clinic-queue/send-to-room/${item.appointmentId}`)
              }
              onStartVisit={() =>
                queueAction(`/api/appointments/${item.appointmentId}/start-visit`)
              }
              onComplete={() =>
                queueAction(`/api/appointments/${item.appointmentId}/status`, "PUT", {
                  status: "Completed",
                })
              }
            />
          ))}
        </div>
      )}

      {/* Room selector modal */}
      {roomModalFor && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center"
          style={{ background: "rgba(0,0,0,0.4)" }}
          onClick={() => setRoomModalFor(null)}
        >
          <div
            className="rounded-2xl p-6 w-80 shadow-2xl"
            style={{ background: "#fff" }}
            onClick={(e) => e.stopPropagation()}
          >
            <h3 className="text-lg font-bold mb-4" style={{ color: "#0F1B2D" }}>
              اختيار الغرفة
            </h3>
            <div className="grid grid-cols-3 gap-3">
              {ROOM_NAMES.map((room) => (
                <button
                  key={room}
                  onClick={() => {
                    queueAction(`/api/clinic-queue/call/${roomModalFor}`, "POST", {
                      roomName: room,
                    });
                    setRoomModalFor(null);
                  }}
                  disabled={actionLoading !== null}
                  className="py-3 rounded-xl text-sm font-bold transition-colors hover:opacity-90 disabled:opacity-50"
                  style={{ background: "#3d7ab5", color: "#fff" }}
                >
                  {room}
                </button>
              ))}
            </div>
            <button
              onClick={() => setRoomModalFor(null)}
              className="mt-4 w-full py-2 rounded-xl text-sm font-medium"
              style={{ background: "#f1f5f9", color: "#475569" }}
            >
              إلغاء
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

/* ─── Queue Card ─────────────────────────────────────────────────────────────── */
function QueueCard({
  item,
  actionLoading,
  onArrive,
  onCall,
  onSendToRoom,
  onStartVisit,
  onComplete,
}: {
  item: QueueItem;
  actionLoading: string | null;
  onArrive: () => void;
  onCall: () => void;
  onSendToRoom: () => void;
  onStartVisit: () => void;
  onComplete: () => void;
}) {
  const isLoading =
    actionLoading !== null && actionLoading.includes(item.appointmentId);

  const canArrive = ["Scheduled", "Confirmed"].includes(item.status);
  const canCall = ["Waiting", "Arrived", "Scheduled", "Confirmed"].includes(item.status);
  const canSendToRoom = item.status === "Called";
  const canStartVisit = ["Waiting", "Called", "InRoom", "Arrived", "Scheduled", "Confirmed"].includes(item.status);
  const canComplete = item.status === "InProgress";

  return (
    <div
      className="rounded-2xl p-4 border transition-shadow hover:shadow-md"
      style={{
        background: "#fff",
        borderColor: "#e2e8f0",
      }}
    >
      <div className="flex items-start justify-between gap-3 flex-wrap">
        {/* Patient info */}
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 mb-1">
            <span className="font-bold text-sm" style={{ color: "#0F1B2D" }}>
              {item.patientName}
            </span>
            <span
              className="text-xs px-2 py-0.5 rounded-md font-medium"
              style={{ background: "#f1f5f9", color: "#64748b" }}
            >
              {item.patientNumber}
            </span>
          </div>
          <div className="flex items-center gap-3 text-xs" style={{ color: "#64748b" }}>
            <span>الدكتور: {item.doctorName}</span>
            <span>الساعة: {item.appointmentTime}</span>
          </div>
        </div>

        {/* Status + Room */}
        <div className="flex items-center gap-2 flex-wrap">
          {item.roomName && (
            <span
              className="text-xs px-2.5 py-1 rounded-lg font-bold"
              style={{ background: "#3d7ab5", color: "#fff" }}
            >
              {item.roomName}
            </span>
          )}
          <span
            className={`text-xs px-2.5 py-1 rounded-lg font-medium ${
              STATUS_COLORS[item.status] || "bg-gray-100 text-gray-600"
            }`}
          >
            {STATUS_LABELS[item.status] || item.status}
          </span>
        </div>
      </div>

      {/* Action buttons */}
      <div className="flex gap-2 mt-3 flex-wrap">
        {canArrive && (
          <ActionButton label="وصل" onClick={onArrive} loading={isLoading} color="#f5922e" />
        )}
        {canCall && (
          <ActionButton label="نداء" onClick={onCall} loading={isLoading} color="#3d7ab5" />
        )}
        {canSendToRoom && (
          <ActionButton label="إلى الغرفة" onClick={onSendToRoom} loading={isLoading} color="#7c3aed" />
        )}
        {canStartVisit && (
          <ActionButton label="بدء الزيارة" onClick={onStartVisit} loading={isLoading} color="#059669" />
        )}
        {canComplete && (
          <ActionButton label="إكمال" onClick={onComplete} loading={isLoading} color="#0F1B2D" />
        )}
      </div>
    </div>
  );
}

/* ─── Action Button ──────────────────────────────────────────────────────────── */
function ActionButton({
  label,
  onClick,
  loading,
  color,
}: {
  label: string;
  onClick: () => void;
  loading: boolean;
  color: string;
}) {
  return (
    <button
      onClick={onClick}
      disabled={loading}
      className="px-3 py-1.5 rounded-lg text-xs font-bold text-white transition-opacity hover:opacity-90 disabled:opacity-50"
      style={{ background: color }}
    >
      {loading ? "..." : label}
    </button>
  );
}
