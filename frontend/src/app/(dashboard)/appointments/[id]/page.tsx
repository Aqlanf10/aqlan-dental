"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import {
  ArrowRight, Calendar, Clock, User, Stethoscope,
  FileText, Trash2, Send, Pencil, PlayCircle, Mail
} from "lucide-react";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";

interface AppointmentDetail {
  id: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  doctorId: string;
  doctorName: string;
  doctorColor?: string;
  appointmentDate: string;
  startTime: string;
  endTime: string;
  durationMinutes: number;
  appointmentType: string;
  specialty?: string;
  status: string;
  notes?: string;
  roomName?: string | null;
  arrivedAt?: string | null;
  calledAt?: string | null;
  inRoomAt?: string | null;
}

const STATUS_LABELS: Record<string, string> = {
  Scheduled: "مجدول",
  Confirmed: "مؤكد",
  Arrived: "وصل",
  Waiting: "في الانتظار",
  Called: "تم النداء",
  InRoom: "داخل الغرفة",
  InProgress: "جاري العلاج",
  Completed: "مكتمل",
  Cancelled: "ملغي",
  NoShow: "لم يحضر",
};

const STATUS_COLORS: Record<string, string> = {
  Scheduled: "bg-blue-50 text-blue-700 border-blue-200",
  Confirmed: "bg-indigo-50 text-indigo-700 border-indigo-200",
  Arrived: "bg-cyan-50 text-cyan-700 border-cyan-200",
  Waiting: "bg-amber-50 text-amber-700 border-amber-200",
  Called: "bg-orange-50 text-orange-700 border-orange-200",
  InRoom: "bg-purple-50 text-purple-700 border-purple-200",
  InProgress: "bg-green-50 text-green-700 border-green-200",
  Completed: "bg-emerald-50 text-emerald-700 border-emerald-200",
  Cancelled: "bg-red-50 text-red-600 border-red-200",
  NoShow: "bg-gray-100 text-gray-500 border-gray-200",
};

export default function AppointmentDetailPage() {
  const params = useParams();
  const router = useRouter();
  const id = params.id as string;
  const [apt, setApt] = useState<AppointmentDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [hasEmail, setHasEmail] = useState<boolean | null>(null);

  useEffect(() => {
    api.get<AppointmentDetail>(`/api/appointments/${id}`)
      .then((r) => setApt(r.data))
      .catch(() => toast.error("فشل تحميل بيانات الموعد"))
      .finally(() => setLoading(false));

    // Check email availability for the patient
    api.get<{ hasEmail: boolean }>(`/api/appointments/${id}/email-available`)
      .then((r) => setHasEmail(r.data.hasEmail))
      .catch(() => setHasEmail(false));
  }, [id]);

  const handleDelete = async () => {
    if (!confirm("هل أنت متأكد من حذف هذا الموعد؟")) return;
    setActionLoading("delete");
    try {
      await api.delete(`/api/appointments/${id}`);
      toast.success("تم حذف الموعد");
      router.push("/appointments");
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? "فشل حذف الموعد");
    } finally {
      setActionLoading(null);
    }
  };

  const handleSendReminder = async () => {
    setActionLoading("reminder");
    try {
      const { data } = await api.post(`/api/appointments/${id}/send-reminder`);
      toast.success(data.message ?? "تم إرسال التذكير");
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? "فشل إرسال التذكير");
    } finally {
      setActionLoading(null);
    }
  };

  const handleSendEmailReminder = async () => {
    setActionLoading("emailReminder");
    try {
      const { data } = await api.post(`/api/appointments/${id}/send-email-reminder`);
      toast.success(data.message ?? "تم إرسال تذكير الموعد بنجاح");
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? "تعذر إرسال التذكير، حاول مرة أخرى");
    } finally {
      setActionLoading(null);
    }
  };

  const handleStartVisit = async () => {
    setActionLoading("visit");
    try {
      const { data } = await api.post(`/api/appointments/${id}/start-visit`);
      toast.success(data.message ?? "تم إنشاء الزيارة");
      setApt((prev) => prev ? { ...prev, status: "InProgress" } : prev);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? "فشل إنشاء الزيارة");
    } finally {
      setActionLoading(null);
    }
  };

  const handleStatusChange = async (status: string) => {
    try {
      await api.put(`/api/appointments/${id}/status`, { status });
      setApt((prev) => prev ? { ...prev, status } : prev);
      toast.success("تم تحديث الحالة");
    } catch {
      toast.error("فشل تحديث الحالة");
    }
  };

  if (loading) {
    return (
      <div className="flex justify-center py-20">
        <div className="text-sm" style={{ color: "#94a3b8" }}>جارٍ التحميل...</div>
      </div>
    );
  }

  if (!apt) {
    return (
      <div className="text-center py-20" style={{ color: "#94a3b8" }}>
        <p>الموعد غير موجود</p>
        <Link href="/appointments" className="text-sm mt-2 inline-block" style={{ color: "#3d7ab5" }}>
          العودة للمواعيد
        </Link>
      </div>
    );
  }

  const canStartVisit = ["Scheduled", "Confirmed", "Arrived", "Waiting", "Called", "InRoom"].includes(apt.status);
  const canDelete = !["InProgress", "Completed"].includes(apt.status);

  return (
    <div className="max-w-3xl mx-auto space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-4">
        <div className="flex items-center gap-3">
          <Link
            href="/appointments"
            className="w-9 h-9 rounded-lg flex items-center justify-center transition-colors"
            style={{ background: "#eef3f9", color: "#0d2137" }}
          >
            <ArrowRight className="w-4 h-4" />
          </Link>
          <div>
            <h1 className="text-xl font-bold" style={{ color: "#0d2137" }}>
              تفاصيل الموعد
            </h1>
            <p className="text-xs" style={{ color: "#64748b" }}>
              {apt.patientName} — {apt.appointmentDate}
            </p>
          </div>
        </div>
        <span
          className={`text-sm px-3 py-1.5 rounded-lg border font-medium ${
            STATUS_COLORS[apt.status] ?? "bg-gray-50 text-gray-600"
          }`}
        >
          {STATUS_LABELS[apt.status] ?? apt.status}
        </span>
      </div>

      {/* Main Card */}
      <div className="rounded-2xl border p-6 space-y-5" style={{ background: "#fff", borderColor: "#e2e8f0" }}>
        {/* Patient & Doctor */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
          <InfoRow icon={User} label="المريض" value={apt.patientName} subValue={apt.patientNumber}
            linkHref={`/patients/${apt.patientId}`} />
          <InfoRow icon={Stethoscope} label="الطبيب" value={apt.doctorName}
            colorDot={apt.doctorColor} />
        </div>

        {/* Date & Time */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-5">
          <InfoRow icon={Calendar} label="التاريخ" value={apt.appointmentDate} />
          <InfoRow icon={Clock} label="الوقت" value={`${apt.startTime} – ${apt.endTime}`} />
          <InfoRow icon={Clock} label="المدة" value={`${apt.durationMinutes} دقيقة`} />
        </div>

        {/* Type & Specialty */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
          <InfoRow icon={FileText} label="نوع الموعد" value={apt.appointmentType} />
          {apt.specialty && <InfoRow icon={Stethoscope} label="التخصص" value={apt.specialty} />}
        </div>

        {/* Room */}
        {apt.roomName && (
          <InfoRow icon={Calendar} label="الغرفة" value={apt.roomName} />
        )}

        {/* Notes */}
        {apt.notes && (
          <div>
            <label className="text-xs font-medium block mb-1" style={{ color: "#64748b" }}>ملاحظات</label>
            <p className="text-sm" style={{ color: "#0d2137" }}>{apt.notes}</p>
          </div>
        )}
      </div>

      {/* Actions */}
      <div className="flex flex-wrap gap-3">
        <Link
          href={`/appointments/${id}/edit`}
          className="flex items-center gap-2 px-4 py-2.5 rounded-xl text-sm font-medium text-white transition-opacity hover:opacity-90"
          style={{ background: "#3d7ab5" }}
        >
          <Pencil className="w-4 h-4" />
          تعديل
        </Link>

        {canStartVisit && (
          <button
            onClick={handleStartVisit}
            disabled={actionLoading === "visit"}
            className="flex items-center gap-2 px-4 py-2.5 rounded-xl text-sm font-medium text-white transition-opacity hover:opacity-90 disabled:opacity-50"
            style={{ background: "#059669" }}
          >
            <PlayCircle className="w-4 h-4" />
            {actionLoading === "visit" ? "جارٍ الإنشاء..." : "بدء الزيارة"}
          </button>
        )}

        <button
          onClick={handleSendReminder}
          disabled={actionLoading === "reminder"}
          className="flex items-center gap-2 px-4 py-2.5 rounded-xl text-sm font-medium text-white transition-opacity hover:opacity-90 disabled:opacity-50"
          style={{ background: "#f5922e" }}
        >
          <Send className="w-4 h-4" />
          {actionLoading === "reminder" ? "جارٍ الإرسال..." : "إرسال تذكير واتساب"}
        </button>

        {hasEmail ? (
          <button
            onClick={handleSendEmailReminder}
            disabled={actionLoading === "emailReminder"}
            className="flex items-center gap-2 px-4 py-2.5 rounded-xl text-sm font-medium text-white transition-opacity hover:opacity-90 disabled:opacity-50"
            style={{ background: "#0E7490" }}
          >
            <Mail className="w-4 h-4" />
            {actionLoading === "emailReminder" ? "جارٍ الإرسال..." : "إرسال تذكير بالإيميل"}
          </button>
        ) : (
          <button
            disabled
            title="لا يوجد بريد إلكتروني لهذا المريض"
            className="flex items-center gap-2 px-4 py-2.5 rounded-xl text-sm font-medium text-white opacity-50 cursor-not-allowed"
            style={{ background: "#0E7490" }}
          >
            <Mail className="w-4 h-4" />
            إرسال تذكير بالإيميل
          </button>
        )}

        {canDelete && (
          <button
            onClick={handleDelete}
            disabled={actionLoading === "delete"}
            className="flex items-center gap-2 px-4 py-2.5 rounded-xl text-sm font-medium text-white transition-opacity hover:opacity-90 disabled:opacity-50"
            style={{ background: "#dc2626" }}
          >
            <Trash2 className="w-4 h-4" />
            {actionLoading === "delete" ? "جارٍ الحذف..." : "حذف"}
          </button>
        )}

        {/* Quick Status Changes */}
        {apt.status === "Scheduled" && (
          <button onClick={() => handleStatusChange("Confirmed")} className="px-4 py-2.5 rounded-xl text-sm font-medium transition-colors" style={{ background: "#eef3f9", color: "#0d2137" }}>تأكيد</button>
        )}
        {["Scheduled", "Confirmed"].includes(apt.status) && (
          <button onClick={() => handleStatusChange("Cancelled")} className="px-4 py-2.5 rounded-xl text-sm font-medium transition-colors" style={{ background: "#fef2f2", color: "#991b1b" }}>إلغاء</button>
        )}
        {apt.status === "Cancelled" && (
          <button onClick={() => handleStatusChange("Scheduled")} className="px-4 py-2.5 rounded-xl text-sm font-medium transition-colors" style={{ background: "#eef3f9", color: "#0d2137" }}>إعادة جدولة</button>
        )}
      </div>
    </div>
  );
}

function InfoRow({ icon: Icon, label, value, subValue, linkHref, colorDot }: {
  icon: React.ElementType;
  label: string;
  value: string;
  subValue?: string;
  linkHref?: string;
  colorDot?: string;
}) {
  const content = (
    <div>
      <label className="text-xs font-medium block mb-1" style={{ color: "#64748b" }}>{label}</label>
      <div className="flex items-center gap-2">
        {colorDot && (
          <div className="w-2.5 h-2.5 rounded-full flex-shrink-0" style={{ backgroundColor: colorDot }} />
        )}
        <span className="text-sm font-medium" style={{ color: "#0d2137" }}>{value}</span>
        {subValue && (
          <span className="text-xs" style={{ color: "#94a3b8" }}>{subValue}</span>
        )}
      </div>
    </div>
  );

  return (
    <div className="flex items-start gap-3">
      <div className="w-8 h-8 rounded-lg flex items-center justify-center flex-shrink-0" style={{ background: "#eef3f9" }}>
        <Icon className="w-4 h-4" style={{ color: "#3d7ab5" }} />
      </div>
      {linkHref ? (
        <Link href={linkHref} className="hover:underline">{content}</Link>
      ) : (
        content
      )}
    </div>
  );
}
