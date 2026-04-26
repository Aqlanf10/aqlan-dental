"use client";
import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { Scissors, User, CheckCircle, Clock, XCircle, PlayCircle } from "lucide-react";
import api from "@/lib/api";
import { cn, formatArabicDate } from "@/lib/utils";

interface SurgeryCase {
  id: string;
  caseNumber: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  doctorName?: string;
  doctorColor?: string;
  surgeryType: string;
  teethInvolved?: string;
  status: string;
  createdAt: string;
}

const STATUS_LABELS: Record<string, string> = {
  scheduled:   "مجدولة",
  in_progress: "جارية",
  completed:   "مكتملة",
  cancelled:   "ملغاة",
  postponed:   "مؤجلة",
};
const STATUS_COLORS: Record<string, string> = {
  scheduled:   "bg-blue-50 text-blue-700 border-blue-200",
  in_progress: "bg-yellow-50 text-yellow-700 border-yellow-200",
  completed:   "bg-green-50 text-green-700 border-green-200",
  cancelled:   "bg-gray-100 text-gray-500 border-gray-200",
  postponed:   "bg-orange-50 text-orange-700 border-orange-200",
};

const NEXT_STATUSES: Record<string, { status: string; label: string; icon: typeof PlayCircle; color: string }[]> = {
  scheduled: [
    { status: "in_progress", label: "بدء الجراحة",   icon: PlayCircle, color: "bg-yellow-500 hover:bg-yellow-600 text-white" },
    { status: "postponed",   label: "تأجيل",          icon: Clock,      color: "bg-orange-100 hover:bg-orange-200 text-orange-700 border border-orange-200" },
    { status: "cancelled",   label: "إلغاء",          icon: XCircle,    color: "bg-gray-100 hover:bg-gray-200 text-gray-600 border border-gray-200" },
  ],
  in_progress: [
    { status: "completed",   label: "إكمال الجراحة", icon: CheckCircle, color: "bg-green-600 hover:bg-green-700 text-white" },
    { status: "cancelled",   label: "إلغاء",          icon: XCircle,    color: "bg-gray-100 hover:bg-gray-200 text-gray-600 border border-gray-200" },
  ],
  postponed: [
    { status: "scheduled",   label: "إعادة جدولة",   icon: Clock,      color: "bg-blue-500 hover:bg-blue-600 text-white" },
    { status: "cancelled",   label: "إلغاء",          icon: XCircle,    color: "bg-gray-100 hover:bg-gray-200 text-gray-600 border border-gray-200" },
  ],
};

export default function SurgeryDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [surgeryCase, setSurgeryCase] = useState<SurgeryCase | null>(null);
  const [loading, setLoading] = useState(true);
  const [updating, setUpdating] = useState(false);

  useEffect(() => {
    api.get<SurgeryCase>(`/api/surgery-cases/${id}`)
      .then((r) => setSurgeryCase(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [id]);

  const updateStatus = async (status: string) => {
    if (!surgeryCase || updating) return;
    setUpdating(true);
    try {
      await api.put(`/api/surgery-cases/${id}/status`, { status });
      setSurgeryCase((prev) => prev ? { ...prev, status } : prev);
    } catch {}
    finally { setUpdating(false); }
  };

  if (loading) {
    return (
      <div className="space-y-4 animate-pulse max-w-3xl">
        <div className="h-8 bg-gray-100 rounded w-40" />
        <div className="h-32 bg-gray-100 rounded-xl" />
        <div className="h-48 bg-gray-100 rounded-xl" />
      </div>
    );
  }

  if (!surgeryCase) {
    return <div className="text-center py-20 text-gray-400">الحالة الجراحية غير موجودة</div>;
  }

  const nextActions = NEXT_STATUSES[surgeryCase.status] ?? [];

  return (
    <div className="space-y-5 max-w-3xl">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Link href="/surgery" className="hover:text-clinic-teal transition">الجراحة</Link>
        <span>/</span>
        <span className="text-gray-900 font-medium">{surgeryCase.caseNumber}</span>
      </div>

      {/* Banner */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
        <div className="flex items-start justify-between gap-4 flex-wrap">
          <div className="flex items-start gap-4">
            <div
              className="w-12 h-12 rounded-xl flex items-center justify-center text-white flex-shrink-0"
              style={{ backgroundColor: surgeryCase.doctorColor ?? "#DC2626" }}
            >
              <Scissors className="w-6 h-6" />
            </div>
            <div>
              <div className="flex items-center gap-3 flex-wrap">
                <h1 className="text-xl font-extrabold text-gray-900">{surgeryCase.patientName}</h1>
                <span className="font-mono text-xs bg-gray-100 px-2.5 py-1 rounded text-gray-600">
                  {surgeryCase.caseNumber}
                </span>
                <span className={cn(
                  "text-xs px-2.5 py-0.5 rounded-full font-medium border",
                  STATUS_COLORS[surgeryCase.status] ?? "bg-gray-100 text-gray-500 border-gray-200"
                )}>
                  {STATUS_LABELS[surgeryCase.status] ?? surgeryCase.status}
                </span>
              </div>
              <div className="mt-2 flex flex-wrap items-center gap-4 text-sm text-gray-500">
                <span className="font-medium text-gray-700">{surgeryCase.surgeryType}</span>
                {surgeryCase.doctorName && <span>{surgeryCase.doctorName}</span>}
                {surgeryCase.teethInvolved && (
                  <span className="font-mono text-xs">الأسنان: {surgeryCase.teethInvolved}</span>
                )}
                <span className="text-xs">{formatArabicDate(surgeryCase.createdAt)}</span>
              </div>
            </div>
          </div>
          <Link
            href={`/patients/${surgeryCase.patientId}`}
            className="flex items-center gap-1.5 px-3 py-1.5 text-sm border border-gray-200 rounded-lg hover:bg-gray-50 transition text-gray-600"
          >
            <User className="w-3.5 h-3.5" />
            ملف المريض
          </Link>
        </div>
      </div>

      {/* Case details */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
        <h2 className="font-bold text-gray-900 mb-4">تفاصيل الحالة</h2>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {[
            ["رقم الحالة", surgeryCase.caseNumber],
            ["المريض", surgeryCase.patientName],
            ["رقم المريض", surgeryCase.patientNumber],
            ["الطبيب الجراح", surgeryCase.doctorName ?? "—"],
            ["نوع الجراحة", surgeryCase.surgeryType],
            ["الأسنان المعنية", surgeryCase.teethInvolved ?? "—"],
            ["تاريخ الإنشاء", formatArabicDate(surgeryCase.createdAt)],
            ["الحالة", STATUS_LABELS[surgeryCase.status] ?? surgeryCase.status],
          ].map(([label, value]) => (
            <div key={label} className="border-b border-gray-50 pb-3 last:border-0">
              <p className="text-xs text-gray-400 mb-0.5">{label}</p>
              <p className="text-sm font-medium text-gray-900">{value}</p>
            </div>
          ))}
        </div>
      </div>

      {/* Status actions */}
      {nextActions.length > 0 && (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
          <h2 className="font-bold text-gray-900 mb-3">تحديث الحالة</h2>
          <div className="flex flex-wrap gap-2">
            {nextActions.map(({ status, label, icon: Icon, color }) => (
              <button
                key={status}
                onClick={() => updateStatus(status)}
                disabled={updating}
                className={cn(
                  "flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg transition disabled:opacity-50",
                  color
                )}
              >
                <Icon className="w-4 h-4" />
                {label}
              </button>
            ))}
          </div>
        </div>
      )}

      {surgeryCase.status === "completed" && (
        <div className="bg-green-50 border border-green-200 rounded-xl p-4 flex items-center gap-3">
          <CheckCircle className="w-5 h-5 text-green-600 flex-shrink-0" />
          <p className="text-sm font-medium text-green-800">تمت الجراحة بنجاح</p>
        </div>
      )}
    </div>
  );
}
