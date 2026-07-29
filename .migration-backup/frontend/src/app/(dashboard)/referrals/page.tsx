"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { ArrowLeftRight, Plus, AlertTriangle } from "lucide-react";
import api from "@/lib/api";
import { cn, formatArabicDate } from "@/lib/utils";

interface Referral {
  id: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  fromDoctorName: string;
  fromDoctorColor?: string;
  toDoctorName: string;
  toDoctorColor?: string;
  reason?: string;
  priority: string;
  notes?: string;
  status: string;
  createdAt: string;
  acceptedAt?: string;
}

const STATUS_LABELS: Record<string, string> = {
  pending: "معلّقة", accepted: "مقبولة", completed: "مكتملة",
};
const STATUS_COLORS: Record<string, string> = {
  pending:   "bg-yellow-50 text-yellow-700 border-yellow-200",
  accepted:  "bg-blue-50 text-blue-700 border-blue-200",
  completed: "bg-green-50 text-green-700 border-green-200",
};
const PRIORITY_LABELS: Record<string, string> = {
  normal: "عادية", urgent: "عاجلة", emergency: "طارئة",
};
const PRIORITY_COLORS: Record<string, string> = {
  normal: "text-gray-500", urgent: "text-orange-600", emergency: "text-red-600",
};

export default function ReferralsPage() {
  const [referrals, setReferrals] = useState<Referral[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(false);
  const [filter, setFilter] = useState("");

  const load = () => {
    setLoading(true);
    setLoadError(false);
    const params = filter ? `?status=${filter}` : "";
    // The API returns a paginated envelope { data, total, page, pageSize } —
    // unwrap it (and tolerate a bare array for backward compatibility).
    api.get<{ data: Referral[] } | Referral[]>(`/api/referrals${params}`)
      .then((r) => {
        const payload = r.data;
        setReferrals(Array.isArray(payload) ? payload : payload?.data ?? []);
      })
      .catch(() => setLoadError(true))
      .finally(() => setLoading(false));
  };

  useEffect(load, [filter]);

  const handleStatusChange = async (id: string, action: "accept" | "complete") => {
    try {
      await api.put(`/api/referrals/${id}/${action}`);
      load();
    } catch (e) { console.error("[Referral] Failed to update referral status:", e); }
  };

  return (
    <div className="space-y-5 max-w-5xl">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-extrabold text-gray-900">الإحالات</h1>
          <p className="text-sm text-gray-500 mt-0.5">الإحالات الداخلية بين الأطباء</p>
        </div>
        <Link href="/referrals/new"
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
        >
          <Plus className="w-4 h-4" />
          إحالة جديدة
        </Link>
      </div>

      {/* Filter tabs */}
      <div className="flex items-center gap-2 flex-wrap">
        {["", "pending", "accepted", "completed"].map((s) => (
          <button key={s} onClick={() => setFilter(s)}
            className={cn(
              "px-3 py-1.5 text-sm rounded-lg border transition font-medium",
              filter === s
                ? "bg-clinic-blue text-white border-clinic-blue"
                : "border-gray-200 text-gray-600 hover:bg-gray-50"
            )}
          >
            {s === "" ? "الكل" : STATUS_LABELS[s]}
          </button>
        ))}
      </div>

      {loading ? (
        <div className="space-y-2 animate-pulse">
          {Array.from({ length: 4 }).map((_, i) => <div key={i} className="h-20 bg-gray-100 rounded-xl" />)}
        </div>
      ) : loadError ? (
        <div className="text-center py-16 bg-red-50 border border-red-200 rounded-xl">
          <AlertTriangle className="w-10 h-10 mx-auto mb-3 text-red-400" />
          <p className="text-sm font-bold text-red-700">تعذر تحميل الإحالات من الخادم</p>
          <button onClick={load}
            className="mt-3 px-4 py-2 text-sm font-medium rounded-lg bg-red-600 text-white hover:opacity-90 transition">
            إعادة المحاولة
          </button>
        </div>
      ) : referrals.length === 0 ? (
        <div className="text-center py-20 text-gray-400">
          <ArrowLeftRight className="w-12 h-12 mx-auto mb-3 opacity-30" />
          <p className="text-sm">لا توجد إحالات</p>
        </div>
      ) : (
        <div className="space-y-3">
          {referrals.map((r) => (
            <div key={r.id} className="bg-white rounded-xl border border-gray-200 shadow-sm p-4">
              <div className="flex items-start justify-between gap-3 flex-wrap">
                <div className="flex-1 min-w-0">
                  {/* Patient + flow */}
                  <div className="flex items-center gap-2 flex-wrap">
                    <Link href={`/patients/${r.patientId}`} className="font-semibold text-gray-900 hover:text-clinic-blue">
                      {r.patientName}
                    </Link>
                    <span className="font-mono text-xs text-gray-400">{r.patientNumber}</span>
                    {r.priority !== "normal" && (
                      <span className={cn("flex items-center gap-1 text-xs font-medium", PRIORITY_COLORS[r.priority])}>
                        <AlertTriangle className="w-3 h-3" />
                        {PRIORITY_LABELS[r.priority]}
                      </span>
                    )}
                  </div>

                  <div className="mt-2 flex items-center gap-2 flex-wrap text-sm">
                    <div className="flex items-center gap-1.5">
                      <div className="w-2 h-2 rounded-full" style={{ backgroundColor: r.fromDoctorColor ?? "#2563EB" }} />
                      <span className="text-gray-600">{r.fromDoctorName}</span>
                    </div>
                    <ArrowLeftRight className="w-3.5 h-3.5 text-gray-400" />
                    <div className="flex items-center gap-1.5">
                      <div className="w-2 h-2 rounded-full" style={{ backgroundColor: r.toDoctorColor ?? "#2563EB" }} />
                      <span className="text-gray-600 font-medium">{r.toDoctorName}</span>
                    </div>
                  </div>

                  {r.reason && (
                    <p className="mt-1.5 text-sm text-gray-700">{r.reason}</p>
                  )}
                  {r.notes && (
                    <p className="mt-1 text-xs text-gray-500">{r.notes}</p>
                  )}

                  <p className="mt-1.5 text-xs text-gray-400">
                    {formatArabicDate(r.createdAt)}
                    {r.acceptedAt && ` · قُبلت: ${formatArabicDate(r.acceptedAt)}`}
                  </p>
                </div>

                <div className="flex flex-col items-end gap-2 flex-shrink-0">
                  <span className={cn(
                    "text-xs px-2.5 py-1 rounded-full font-medium border",
                    STATUS_COLORS[r.status] ?? "bg-gray-100 text-gray-600 border-gray-200"
                  )}>
                    {STATUS_LABELS[r.status] ?? r.status}
                  </span>
                  {r.status === "pending" && (
                    <button onClick={() => handleStatusChange(r.id, "accept")}
                      className="text-xs px-3 py-1 rounded-lg bg-blue-50 text-blue-700 hover:bg-blue-100 font-medium transition"
                    >
                      قبول
                    </button>
                  )}
                  {r.status === "accepted" && (
                    <button onClick={() => handleStatusChange(r.id, "complete")}
                      className="text-xs px-3 py-1 rounded-lg bg-green-50 text-green-700 hover:bg-green-100 font-medium transition"
                    >
                      إكمال
                    </button>
                  )}
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
