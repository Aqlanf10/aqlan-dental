"use client";
import { useEffect, useState } from "react";
import { Calendar, Plus, Clock, User, X, Check, ChevronLeft } from "lucide-react";
import portalApi from "@/lib/portalApi";
import { usePatientAuthStore } from "@/stores/patientAuthStore";
import type { PatientAppointment, PortalDoctor } from "@/types/patientPortal";
import { cn } from "@/lib/utils";
import Link from "next/link";

const APPOINTMENT_TYPES = ["فحص", "تنظيف", "حشو", "قلع", "معالجة جذر", "تقويم", "أخرى"];

export default function PortalAppointmentsPage() {
  const { profile } = usePatientAuthStore();
  const [appointments, setAppointments] = useState<PatientAppointment[]>([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [doctors, setDoctors] = useState<PortalDoctor[]>([]);
  
  // Form state
  const [apptDate, setApptDate] = useState("");
  const [apptTime, setApptTime] = useState("");
  const [apptType, setApptType] = useState("");
  const [doctorId, setDoctorId] = useState("");
  const [notes, setNotes] = useState("");
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");
  const [filter, setFilter] = useState<"all" | "upcoming" | "past">("all");

  useEffect(() => {
    portalApi.get<PatientAppointment[]>("/api/portal/appointments?limit=50")
      .then((r) => setAppointments(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));

    portalApi.get<PortalDoctor[]>("/api/doctors")
      .then((r) => setDoctors(r.data))
      .catch(() => {});
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!apptDate || !apptTime || !apptType) {
      setFormError("التاريخ والوقت ونوع الموعد مطلوبين");
      return;
    }
    setSaving(true);
    setFormError("");
    try {
      const { data } = await portalApi.post<PatientAppointment>("/api/portal/appointments", {
        appointmentDate: apptDate,
        startTime: apptTime,
        appointmentType: apptType,
        doctorId: doctorId || undefined,
        notes: notes || undefined,
      });
      setAppointments((prev) => [data, ...prev]);
      setShowForm(false);
      setApptDate(""); setApptTime(""); setApptType(""); setDoctorId(""); setNotes("");
    } catch (err: any) {
      setFormError(err.response?.data?.message || "حدث خطأ");
    } finally {
      setSaving(false);
    }
  };

  const handleCancel = async (id: string) => {
    if (!confirm("هل تريد إلغاء هذا الموعد؟")) return;
    try {
      await portalApi.delete(`/api/portal/appointments/${id}`);
      setAppointments((prev) => prev.map((a) => a.id === id ? { ...a, status: "Cancelled" } : a));
    } catch {}
  };

  const now = new Date();
  const filteredAppts = appointments.filter((a) => {
    if (filter === "upcoming") return new Date(a.appointmentDate) >= now && a.status === "Scheduled";
    if (filter === "past") return new Date(a.appointmentDate) < now || a.status !== "Scheduled";
    return true;
  });

  return (
    <div className="pb-20">
      {/* Header */}
      <div className="bg-white border-b border-gray-100 px-5 pt-10 pb-4">
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-2">
            <Link href="/portal" className="text-gray-400 hover:text-gray-600">
              <ChevronLeft className="w-5 h-5" />
            </Link>
            <h1 className="text-xl font-extrabold text-gray-900">المواعيد</h1>
          </div>
          <button
            onClick={() => setShowForm(!showForm)}
            className={cn(
              "flex items-center gap-1.5 px-3 py-2 rounded-lg text-sm font-medium transition",
              showForm ? "bg-gray-100 text-gray-600" : "bg-teal-600 text-white hover:bg-teal-700"
            )}
          >
            {showForm ? <X className="w-4 h-4" /> : <Plus className="w-4 h-4" />}
            {showForm ? "إغلاق" : "حجز"}
          </button>
        </div>

        {/* Filter Tabs */}
        <div className="flex gap-2">
          {[
            { key: "all" as const, label: "الكل" },
            { key: "upcoming" as const, label: "قادمة" },
            { key: "past" as const, label: "سابقة" },
          ].map((f) => (
            <button
              key={f.key}
              onClick={() => setFilter(f.key)}
              className={cn(
                "px-4 py-1.5 text-xs font-medium rounded-full transition",
                filter === f.key ? "bg-teal-100 text-teal-700" : "bg-gray-100 text-gray-500"
              )}
            >
              {f.label}
            </button>
          ))}
        </div>
      </div>

      <div className="px-4 mt-4 space-y-3">
        {/* New Appointment Form */}
        {showForm && (
          <form onSubmit={handleSubmit} className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4 space-y-3">
            <h3 className="font-bold text-gray-900">حجز موعد جديد</h3>
            {formError && (
              <div className="text-sm text-red-600 bg-red-50 border border-red-200 rounded-lg p-2">{formError}</div>
            )}
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">التاريخ</label>
              <input type="date" value={apptDate} onChange={(e) => setApptDate(e.target.value)}
                className="w-full px-3 py-2 text-sm rounded-lg border border-gray-300 focus:ring-2 focus:ring-teal-500 focus:outline-none" dir="ltr" />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">الوقت</label>
              <input type="time" value={apptTime} onChange={(e) => setApptTime(e.target.value)}
                className="w-full px-3 py-2 text-sm rounded-lg border border-gray-300 focus:ring-2 focus:ring-teal-500 focus:outline-none" dir="ltr" />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">نوع الموعد</label>
              <select value={apptType} onChange={(e) => setApptType(e.target.value)}
                className="w-full px-3 py-2 text-sm rounded-lg border border-gray-300 focus:ring-2 focus:ring-teal-500 focus:outline-none">
                <option value="">اختر...</option>
                {APPOINTMENT_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
              </select>
            </div>
            {doctors.length > 0 && (
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">الطبيب</label>
                <select value={doctorId} onChange={(e) => setDoctorId(e.target.value)}
                  className="w-full px-3 py-2 text-sm rounded-lg border border-gray-300 focus:ring-2 focus:ring-teal-500 focus:outline-none">
                  <option value="">تلقائي</option>
                  {doctors.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
                </select>
              </div>
            )}
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">ملاحظات</label>
              <input value={notes} onChange={(e) => setNotes(e.target.value)}
                className="w-full px-3 py-2 text-sm rounded-lg border border-gray-300 focus:ring-2 focus:ring-teal-500 focus:outline-none"
                placeholder="ملاحظات إضافية..." />
            </div>
            <button type="submit" disabled={saving}
              className="w-full py-2.5 text-sm font-semibold rounded-lg bg-teal-600 text-white hover:bg-teal-700 disabled:opacity-60 transition">
              {saving ? "جارٍ الحجز..." : "تأكيد الحجز"}
            </button>
          </form>
        )}

        {/* Appointments List */}
        {loading ? (
          <div className="space-y-3 animate-pulse">
            {Array.from({ length: 5 }).map((_, i) => (
              <div key={i} className="bg-white rounded-2xl h-24" />
            ))}
          </div>
        ) : filteredAppts.length === 0 ? (
          <div className="text-center py-16">
            <Calendar className="w-12 h-12 text-gray-300 mx-auto mb-3" />
            <p className="text-sm text-gray-500">لا توجد مواعيد</p>
          </div>
        ) : (
          filteredAppts.map((appt) => (
            <div key={appt.id} className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4">
              <div className="flex items-start justify-between">
                <div className="flex-1">
                  <div className="flex items-center gap-2 mb-2">
                    <span className={cn(
                      "text-xs px-2 py-0.5 rounded-full font-medium",
                      appt.status === "Scheduled" ? "bg-blue-100 text-blue-700" :
                      appt.status === "Completed" ? "bg-green-100 text-green-700" :
                      "bg-red-100 text-red-700"
                    )}>
                      {appt.status === "Scheduled" ? "مؤكد" :
                       appt.status === "Completed" ? "مكتمل" : "ملغي"}
                    </span>
                    <span className="text-sm font-medium text-gray-900">{appt.appointmentType}</span>
                  </div>
                  <div className="space-y-1">
                    <div className="flex items-center gap-1.5 text-xs text-gray-500">
                      <Calendar className="w-3 h-3" /> {appt.appointmentDate}
                    </div>
                    <div className="flex items-center gap-1.5 text-xs text-gray-500">
                      <Clock className="w-3 h-3" /> {appt.startTime} - {appt.endTime}
                    </div>
                    <div className="flex items-center gap-1.5 text-xs text-gray-500">
                      <User className="w-3 h-3" /> {appt.doctorName}
                    </div>
                  </div>
                </div>
                {appt.status === "Scheduled" && (
                  <button
                    onClick={() => handleCancel(appt.id)}
                    className="text-xs text-red-500 hover:text-red-700 bg-red-50 px-2 py-1 rounded-lg transition"
                  >
                    إلغاء
                  </button>
                )}
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  );
}
