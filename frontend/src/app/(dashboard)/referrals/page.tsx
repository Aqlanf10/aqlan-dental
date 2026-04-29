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
  pending:   "bg-[#f59e0b18] text-[#f59e0b] border-yellow-200",
  accepted:  "bg-[#3d7ab518] text-accent-blue border-blue-200",
  completed: "bg-[#22c55e18] text-[#22c55e] border-green-200",
};
const PRIORITY_LABELS: Record<string, string> = {
  normal: "عادية", urgent: "عاجلة", emergency: "طارئة",
};
const PRIORITY_COLORS: Record<string, string> = {
  normal: "text-[#64748b]", urgent: "text-[#f5922e]", emergency: "text-[#ef4444]",
};

export default function ReferralsPage() {
  const [referrals, setReferrals] = useState<Referral[]>([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState("");

  const load = () => {
    setLoading(true);
    const params = filter ? `?status=${filter}` : "";
    api.get<{ data: Referral[]; total: number; page: number; pageSize: number }>(`/api/referrals${params}`)
      .then((r) => setReferrals(r.data.data ?? []))
      .catch(() => {})
      .finally(() => setLoading(false));
  };

  useEffect(load, [filter]);

  const handleStatusChange = async (id: string, action: "accept" | "complete") => {
    try {
      await api.put(`/api/referrals/${id}/${action}`);
      load();
    } catch {}
  };

  return (
    <div className="space-y-5 max-w-5xl">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-extrabold text-[#0d2137]">الإحالات</h1>
          <p className="text-sm text-[#64748b] mt-0.5">الإحالات الداخلية بين الأطباء</p>
        </div>
        <Link href="/referrals/new"
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-accent-blue text-white hover:bg-blue-hover transition"
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
                ? "bg-accent-blue text-white border-accent-blue"
                : "border-[#e8f0f9] text-[#64748b] hover:bg-[#f7fafd]"
            )}
          >
            {s === "" ? "الكل" : STATUS_LABELS[s]}
          </button>
        ))}
      </div>

      {loading ? (
        <div className="space-y-2 animate-pulse">
          {Array.from({ length: 4 }).map((_, i) => <div key={i} className="h-20 bg-[#eef3f9] rounded-xl" />)}
        </div>
      ) : referrals.length === 0 ? (
        <div className="text-center py-20 text-[#94a3b8]">
          <ArrowLeftRight className="w-12 h-12 mx-auto mb-3 opacity-30" />
          <p className="text-sm">لا توجد إحالات</p>
        </div>
      ) : (
        <div className="space-y-3">
          {referrals.map((r) => (
            <div key={r.id} className="bg-white rounded-xl border border-[#e8f0f9] shadow-card p-4">
              <div className="flex items-start justify-between gap-3 flex-wrap">
                <div className="flex-1 min-w-0">
                  {/* Patient + flow */}
                  <div className="flex items-center gap-2 flex-wrap">
                    <Link href={`/patients/${r.patientId}`} className="font-semibold text-[#0d2137] hover:text-accent-blue">
                      {r.patientName}
                    </Link>
                    <span className="font-mono text-xs text-[#94a3b8]">{r.patientNumber}</span>
                    {r.priority !== "normal" && (
                      <span className={cn("flex items-center gap-1 text-xs font-medium", PRIORITY_COLORS[r.priority])}>
                        <AlertTriangle className="w-3 h-3" />
                        {PRIORITY_LABELS[r.priority]}
                      </span>
                    )}
                  </div>

                  <div className="mt-2 flex items-center gap-2 flex-wrap text-sm">
                    <div className="flex items-center gap-1.5">
                      <div className="w-2 h-2 rounded-full" style={{ backgroundColor: r.fromDoctorColor ?? "#3d7ab5" }} />
                      <span className="text-[#64748b]">{r.fromDoctorName}</span>
                    </div>
                    <ArrowLeftRight className="w-3.5 h-3.5 text-[#94a3b8]" />
                    <div className="flex items-center gap-1.5">
                      <div className="w-2 h-2 rounded-full" style={{ backgroundColor: r.toDoctorColor ?? "#3d7ab5" }} />
                      <span className="text-[#64748b] font-medium">{r.toDoctorName}</span>
                    </div>
                  </div>

                  {r.reason && (
                    <p className="mt-1.5 text-sm text-[#64748b]">{r.reason}</p>
                  )}
                  {r.notes && (
                    <p className="mt-1 text-xs text-[#64748b]">{r.notes}</p>
                  )}

                  <p className="mt-1.5 text-xs text-[#94a3b8]">
                    {formatArabicDate(r.createdAt)}
                    {r.acceptedAt && ` · قُبلت: ${formatArabicDate(r.acceptedAt)}`}
                  </p>
                </div>

                <div className="flex flex-col items-end gap-2 flex-shrink-0">
                  <span className={cn(
                    "text-xs px-[10px] py-[2px] rounded-full font-medium border",
                    STATUS_COLORS[r.status] ?? "bg-[#eef3f9] text-[#64748b] border-[#e8f0f9]"
                  )}>
                    {STATUS_LABELS[r.status] ?? r.status}
                  </span>
                  {r.status === "pending" && (
                    <button onClick={() => handleStatusChange(r.id, "accept")}
                      className="text-xs px-3 py-1 rounded-lg bg-[#3d7ab518] text-accent-blue hover:bg-blue-100 font-medium transition"
                    >
                      قبول
                    </button>
                  )}
                  {r.status === "accepted" && (
                    <button onClick={() => handleStatusChange(r.id, "complete")}
                      className="text-xs px-3 py-1 rounded-lg bg-[#22c55e18] text-[#22c55e] hover:bg-[#22c55e30] font-medium transition"
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
