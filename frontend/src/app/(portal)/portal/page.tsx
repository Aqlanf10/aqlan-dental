"use client";
import { useEffect, useState } from "react";
import { Calendar, Clock, User, CreditCard, Activity, ChevronLeft, Phone, MessageCircle, Pill, MapPin } from "lucide-react";
import { usePatientAuthStore } from "@/stores/patientAuthStore";
import portalApi from "@/lib/portalApi";
import type { PatientPortalDashboard } from "@/types/patientPortal";
import { cn, formatYemeniRiyal } from "@/lib/utils";
import Link from "next/link";

export default function PortalDashboard() {
  const { profile, logout } = usePatientAuthStore();
  const [dashboard, setDashboard] = useState<PatientPortalDashboard | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    portalApi.get<PatientPortalDashboard>("/api/portal/dashboard")
      .then((r) => setDashboard(r.data))
      .catch((err) => {
        const msg = err?.response?.data?.message;
        setError(msg || "حدث خطأ في تحميل لوحة التحكم");
      })
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="w-10 h-10 border-4 border-clinic-blue border-t-transparent rounded-full animate-spin" />
      </div>
    );
  }

  return (
    <div className="pb-24">
      {/* Header */}
      <div className="clinic-gradient px-5 pt-10 pb-8 text-white">
        <div className="flex items-center justify-between mb-4">
          <div>
            <p className="text-white/70 text-xs">مرحباً بك</p>
            <h1 className="text-xl font-extrabold">{profile?.fullName || "المريض"}</h1>
          </div>
          <button
            onClick={logout}
            className="text-xs text-white/70 hover:text-white bg-white/10 px-3 py-1.5 rounded-lg transition"
          >
            خروج
          </button>
        </div>

        {/* Quick Stats */}
        <div className="grid grid-cols-3 gap-3">
          <div className="bg-white/15 rounded-xl p-3 text-center backdrop-blur-sm">
            <Calendar className="w-5 h-5 mx-auto mb-1 text-white/80" />
            <p className="text-lg font-bold">{dashboard?.upcomingAppointments ?? 0}</p>
            <p className="text-[10px] text-white/70">موعد قادم</p>
          </div>
          <div className="bg-white/15 rounded-xl p-3 text-center backdrop-blur-sm">
            <Activity className="w-5 h-5 mx-auto mb-1 text-white/80" />
            <p className="text-lg font-bold">{dashboard?.completedTreatments ?? 0}</p>
            <p className="text-[10px] text-white/70">علاج مكتمل</p>
          </div>
          <div className="bg-white/15 rounded-xl p-3 text-center backdrop-blur-sm">
            <CreditCard className="w-5 h-5 mx-auto mb-1 text-white/80" />
            <p className="text-lg font-bold">{dashboard?.finance?.activeContracts ?? 0}</p>
            <p className="text-[10px] text-white/70">عقد نشط</p>
          </div>
        </div>
      </div>

      <div className="px-4 -mt-4 space-y-4">
        {/* Error Message */}
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-2xl p-4 text-center">
            {error}
            <button onClick={() => { setError(null); setLoading(true); portalApi.get<PatientPortalDashboard>("/api/portal/dashboard").then((r) => setDashboard(r.data)).catch((err) => setError(err?.response?.data?.message || "حدث خطأ")).finally(() => setLoading(false)); }} className="block mx-auto mt-2 text-xs text-red-600 hover:text-red-800 underline">إعادة المحاولة</button>
          </div>
        )}
        {/* Next Appointment Card */}
        {dashboard?.nextAppointment ? (
          <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4">
            <div className="flex items-center justify-between mb-3">
              <h3 className="font-bold text-gray-900 flex items-center gap-2">
                <Calendar className="w-4 h-4 text-clinic-blue" />
                الموعد القادم
              </h3>
              <span className={cn(
                "text-xs px-2 py-0.5 rounded-full font-medium",
                dashboard.nextAppointment.status === "Scheduled" ? "bg-blue-100 text-blue-700" :
                dashboard.nextAppointment.status === "Confirmed" ? "bg-green-100 text-green-700" :
                dashboard.nextAppointment.status === "Completed" ? "bg-gray-100 text-gray-700" :
                "bg-red-100 text-red-700"
              )}>
                {dashboard.nextAppointment.status === "Scheduled" ? "مجدول" :
                 dashboard.nextAppointment.status === "Confirmed" ? "مؤكد" :
                 dashboard.nextAppointment.status === "Completed" ? "مكتمل" : "ملغي"}
              </span>
            </div>
            <div className="space-y-2">
              <div className="flex items-center gap-2 text-sm text-gray-600">
                <Clock className="w-3.5 h-3.5 text-gray-400" />
                <span>{dashboard.nextAppointment.appointmentDate}</span>
                <span className="text-gray-400">|</span>
                <span>{dashboard.nextAppointment.startTime} - {dashboard.nextAppointment.endTime}</span>
              </div>
              <div className="flex items-center gap-2 text-sm text-gray-600">
                <User className="w-3.5 h-3.5 text-gray-400" />
                <span>{dashboard.nextAppointment.doctorName}</span>
              </div>
              <div className="text-sm text-gray-700 font-medium mt-2 bg-gray-50 rounded-lg p-2">
                {dashboard.nextAppointment.appointmentType}
              </div>
            </div>
          </div>
        ) : (
          <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-6 text-center">
            <Calendar className="w-10 h-10 text-gray-300 mx-auto mb-2" />
            <p className="text-sm text-gray-500">لا يوجد مواعيد قادمة</p>
            <Link href="/portal/appointments" className="text-xs text-clinic-blue hover:underline mt-1 inline-block">
              احجز موعد جديد
            </Link>
          </div>
        )}

        {/* Quick Actions */}
        <div className="grid grid-cols-2 gap-3">
          <Link href="/portal/appointments" className="bg-clinic-blue/5 border border-clinic-blue/10 rounded-2xl p-4 text-center hover:bg-clinic-blue/10 transition">
            <Calendar className="w-6 h-6 text-clinic-blue mx-auto mb-2" />
            <p className="text-sm font-bold text-clinic-blue">طلب موعد</p>
          </Link>
          <Link href="/portal/appointments" className="bg-clinic-orange/5 border border-clinic-orange/10 rounded-2xl p-4 text-center hover:bg-clinic-orange/10 transition">
            <Clock className="w-6 h-6 text-clinic-orange mx-auto mb-2" />
            <p className="text-sm font-bold text-clinic-orange">عرض المواعيد</p>
          </Link>
          <Link href="/portal/finance" className="bg-green-50 border border-green-100 rounded-2xl p-4 text-center hover:bg-green-100 transition">
            <CreditCard className="w-6 h-6 text-green-600 mx-auto mb-2" />
            <p className="text-sm font-bold text-green-700">عرض المالية</p>
          </Link>
          <Link href="/portal/prescriptions" className="bg-purple-50 border border-purple-100 rounded-2xl p-4 text-center hover:bg-purple-100 transition">
            <Pill className="w-6 h-6 text-purple-600 mx-auto mb-2" />
            <p className="text-sm font-bold text-purple-700">الوصفات</p>
          </Link>
        </div>

        {/* Financial Summary */}
        <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4">
          <div className="flex items-center justify-between mb-3">
            <h3 className="font-bold text-gray-900 flex items-center gap-2">
              <CreditCard className="w-4 h-4 text-clinic-blue" />
              الملخص المالي
            </h3>
            <Link href="/portal/finance" className="text-xs text-clinic-blue hover:underline flex items-center gap-1">
              التفاصيل <ChevronLeft className="w-3 h-3" />
            </Link>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="bg-green-50 rounded-xl p-3 text-center">
              <p className="text-xs text-green-600 mb-1">المدفوع</p>
              <p className="text-lg font-bold text-green-700">{formatYemeniRiyal(dashboard?.finance?.totalPaid ?? 0)}</p>
            </div>
            <div className="bg-red-50 rounded-xl p-3 text-center">
              <p className="text-xs text-red-600 mb-1">المتبقي</p>
              <p className="text-lg font-bold text-red-700">{formatYemeniRiyal(dashboard?.finance?.totalOutstanding ?? 0)}</p>
            </div>
          </div>
        </div>

        {/* Latest Prescription */}
        {dashboard?.latestPrescription && (
          <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4">
            <div className="flex items-center justify-between mb-3">
              <h3 className="font-bold text-gray-900 flex items-center gap-2">
                <Pill className="w-4 h-4 text-purple-600" />
                آخر وصفة طبية
              </h3>
              <Link href="/portal/prescriptions" className="text-xs text-clinic-blue hover:underline flex items-center gap-1">
                الكل <ChevronLeft className="w-3 h-3" />
              </Link>
            </div>
            <div className="space-y-2">
              {dashboard.latestPrescription.diagnosis && (
                <p className="text-sm text-gray-700 bg-purple-50 rounded-lg p-2">{dashboard.latestPrescription.diagnosis}</p>
              )}
              {dashboard.latestPrescription.drugs.length > 0 && (
                <div className="flex flex-wrap gap-1.5">
                  {dashboard.latestPrescription.drugs.slice(0, 3).map((drug, i) => (
                    <span key={i} className="text-xs bg-blue-50 text-blue-700 px-2 py-0.5 rounded-full">
                      {drug.name}
                    </span>
                  ))}
                  {dashboard.latestPrescription.drugs.length > 3 && (
                    <span className="text-xs text-gray-400">+{dashboard.latestPrescription.drugs.length - 3} أخرى</span>
                  )}
                </div>
              )}
              <p className="text-xs text-gray-400">
                {dashboard.latestPrescription.doctorName} · {dashboard.latestPrescription.createdAt}
              </p>
            </div>
          </div>
        )}

        {/* Recent Payments */}
        {dashboard?.finance?.recentPayments && dashboard.finance.recentPayments.length > 0 && (
          <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4">
            <h3 className="font-bold text-gray-900 mb-3">آخر المدفوعات</h3>
            <div className="space-y-2">
              {dashboard.finance.recentPayments.slice(0, 3).map((p) => (
                <div key={p.id} className="flex items-center justify-between py-2 border-b border-gray-50 last:border-0">
                  <div>
                    <p className="text-sm font-medium text-gray-900">{formatYemeniRiyal(p.amount)}</p>
                    <p className="text-xs text-gray-400">{p.createdAt}</p>
                  </div>
                  <span className="text-xs bg-gray-100 text-gray-600 px-2 py-0.5 rounded-full">
                    {p.paymentMethod}
                  </span>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Clinic Contact Info */}
        {dashboard?.clinicInfo && (
          <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4">
            <h3 className="font-bold text-gray-900 mb-3 flex items-center gap-2">
              <MapPin className="w-4 h-4 text-clinic-blue" />
              تواصل مع المركز
            </h3>
            <div className="space-y-3">
              <p className="text-sm font-medium text-gray-900">{dashboard.clinicInfo.clinicName}</p>
              {dashboard.clinicInfo.workingHours && (
                <p className="text-xs text-gray-500">{dashboard.clinicInfo.workingHours}</p>
              )}
              <div className="flex gap-2">
                {dashboard.clinicInfo.phone && (
                  <a
                    href={`tel:${dashboard.clinicInfo.phone}`}
                    className="flex items-center gap-1.5 text-xs bg-green-50 text-green-700 px-3 py-2 rounded-lg hover:bg-green-100 transition"
                  >
                    <Phone className="w-3.5 h-3.5" />
                    اتصال
                  </a>
                )}
                {dashboard.clinicInfo.whatsapp && (
                  <a
                    href={`https://wa.me/${dashboard.clinicInfo.whatsapp.replace(/[^0-9]/g, "")}`}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="flex items-center gap-1.5 text-xs bg-green-50 text-green-700 px-3 py-2 rounded-lg hover:bg-green-100 transition"
                  >
                    <MessageCircle className="w-3.5 h-3.5" />
                    واتساب
                  </a>
                )}
              </div>
            </div>
            {/* Direct messaging link */}
            <div className="mt-3 pt-3 border-t border-gray-100">
              <Link
                href="/portal/messages"
                className="flex items-center gap-2 text-xs text-teal-600 hover:text-teal-700 font-semibold transition"
              >
                <MessageCircle className="w-4 h-4" />
                تواصل معنا مباشرة عبر الرسائل
              </Link>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
