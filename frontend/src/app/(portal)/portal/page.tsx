"use client";
import { useEffect, useState } from "react";
import { Calendar, Clock, User, CreditCard, Activity, ChevronLeft } from "lucide-react";
import { usePatientAuthStore } from "@/stores/patientAuthStore";
import portalApi from "@/lib/portalApi";
import type { PatientPortalDashboard } from "@/types/patientPortal";
import { cn, formatYemeniRiyal } from "@/lib/utils";
import Link from "next/link";

export default function PortalDashboard() {
  const { profile, logout } = usePatientAuthStore();
  const [dashboard, setDashboard] = useState<PatientPortalDashboard | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    portalApi.get<PatientPortalDashboard>("/api/portal/dashboard")
      .then((r) => setDashboard(r.data))
      .catch(() => {})
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
    <div className="pb-20">
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
          <div className="bg-white/15 rounded-xl p-3 text-center">
            <Calendar className="w-5 h-5 mx-auto mb-1 text-white/80" />
            <p className="text-lg font-bold">{dashboard?.totalAppointments ?? 0}</p>
            <p className="text-[10px] text-white/70">موعد</p>
          </div>
          <div className="bg-white/15 rounded-xl p-3 text-center">
            <Activity className="w-5 h-5 mx-auto mb-1 text-white/80" />
            <p className="text-lg font-bold">{dashboard?.completedTreatments ?? 0}</p>
            <p className="text-[10px] text-white/70">علاج</p>
          </div>
          <div className="bg-white/15 rounded-xl p-3 text-center">
            <CreditCard className="w-5 h-5 mx-auto mb-1 text-white/80" />
            <p className="text-lg font-bold">{dashboard?.finance?.activeContracts ?? 0}</p>
            <p className="text-[10px] text-white/70">عقد</p>
          </div>
        </div>
      </div>

      <div className="px-4 -mt-4 space-y-4">
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
                dashboard.nextAppointment.status === "Completed" ? "bg-green-100 text-green-700" :
                "bg-red-100 text-red-700"
              )}>
                {dashboard.nextAppointment.status === "Scheduled" ? "مؤكد" :
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

        {/* Profile Card */}
        <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4">
          <h3 className="font-bold text-gray-900 mb-3">بياناتي</h3>
          <div className="space-y-2 text-sm">
            <div className="flex justify-between">
              <span className="text-gray-500">رقم المريض</span>
              <span className="font-mono text-gray-900">{profile?.patientNumber}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-500">الجنس</span>
              <span className="text-gray-900">{profile?.gender === "Male" ? "ذكر" : profile?.gender === "Female" ? "أنثى" : "—"}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-500">العمر</span>
              <span className="text-gray-900">{profile?.age ? `${profile.age} سنة` : "—"}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-500">الطبيب</span>
              <span className="text-gray-900">{profile?.primaryDoctorName || "—"}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
