"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import {
  User, Activity, Scissors, Stethoscope, Pill, Wallet, Calendar,
  ChevronLeft, FileText,
} from "lucide-react";
import api from "@/lib/api";
import { cn, formatArabicDate, formatYemeniRiyal } from "@/lib/utils";
import type { PatientProfile } from "@/types/patient";
import type { GeneralTreatment } from "@/types/dental";
import type { PrescriptionListItem } from "@/types/prescription";
import type { Contract } from "@/types/finance";

// ─── Sub-interfaces ────────────────────────────────────────────────────────────
interface OrthoCaseBrief {
  id: string;
  caseNumber: string;
  applianceType?: string;
  status: string;
  stagePercentage: number;
  doctorName?: string;
}

interface SurgeryCaseBrief {
  id: string;
  caseNumber: string;
  surgeryType: string;
  status: string;
  doctorName?: string;
}

interface ReferralBrief {
  id: string;
  toSpecialty?: string;
  reason: string;
  status: string;
  referralDate: string;
}

interface AppointmentBrief {
  id: string;
  appointmentDate: string;
  appointmentTime?: string;
  doctorName?: string;
  status: string;
  notes?: string;
}

// ─── Status Labels ─────────────────────────────────────────────────────────────
const ORTHO_STATUS: Record<string, string> = { active: "نشطة", completed: "مكتملة", cancelled: "ملغاة" };
const SURGERY_STATUS: Record<string, string> = { scheduled: "مجدولة", in_progress: "جارية", completed: "مكتملة", cancelled: "ملغاة" };
const REFERRAL_STATUS: Record<string, string> = { pending: "قيد الانتظار", accepted: "مقبول", completed: "مكتمل", cancelled: "ملغى" };
const APPOINTMENT_STATUS: Record<string, string> = {
  Scheduled: "مجدول", Confirmed: "مؤكد", Arrived: "وصل",
  InProgress: "جارٍ", Completed: "مكتمل", Cancelled: "ملغى", NoShow: "لم يحضر",
};

// ─── Quick Nav Items ───────────────────────────────────────────────────────────
interface QuickNavItem {
  key: string;
  label: string;
  icon: typeof User;
  count?: number;
  color: string;
}

interface Props {
  patientId: string;
  patient: PatientProfile;
}

export function UnifiedPatientFile({ patientId, patient }: Props) {
  const [orthoCases, setOrthoCases] = useState<OrthoCaseBrief[]>([]);
  const [surgeryCases, setSurgeryCases] = useState<SurgeryCaseBrief[]>([]);
  const [recentTreatments, setRecentTreatments] = useState<GeneralTreatment[]>([]);
  const [recentPrescriptions, setRecentPrescriptions] = useState<PrescriptionListItem[]>([]);
  const [pendingReferrals, setPendingReferrals] = useState<ReferralBrief[]>([]);
  const [contracts, setContracts] = useState<Contract[]>([]);
  const [upcomingAppointments, setUpcomingAppointments] = useState<AppointmentBrief[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchAll = async () => {
      setLoading(true);
      try {
        const results = await Promise.allSettled([
          api.get<OrthoCaseBrief[]>(`/api/ortho-cases?patientId=${patientId}&pageSize=10`).then((r) => r.data),
          api.get<{ data: SurgeryCaseBrief[] }>(`/api/surgery-cases?patientId=${patientId}&pageSize=10`).then((r) => r.data.data ?? []),
          api.get<GeneralTreatment[]>(`/api/general-treatments/${patientId}`).then((r) => r.data.slice(0, 5)),
          api.get<PrescriptionListItem[]>(`/api/prescriptions?patientId=${patientId}`).then((r) => r.data.slice(0, 5)),
          api.get<{ data: ReferralBrief[] }>(`/api/referrals?patientId=${patientId}&status=pending`).then((r) => r.data.data ?? []),
          api.get<Contract[]>(`/api/contracts?patientId=${patientId}`).then((r) => r.data),
          api.get<AppointmentBrief[]>(`/api/appointments?patientId=${patientId}&upcoming=true&limit=5`).then((r) => r.data),
        ]);

        if (results[0].status === "fulfilled") setOrthoCases(results[0].value);
        if (results[1].status === "fulfilled") setSurgeryCases(results[1].value);
        if (results[2].status === "fulfilled") setRecentTreatments(results[2].value);
        if (results[3].status === "fulfilled") setRecentPrescriptions(results[3].value);
        if (results[4].status === "fulfilled") setPendingReferrals(results[4].value);
        if (results[5].status === "fulfilled") setContracts(results[5].value);
        if (results[6].status === "fulfilled") setUpcomingAppointments(results[6].value);
      } catch {
        // ignore
      } finally {
        setLoading(false);
      }
    };
    fetchAll();
  }, [patientId]);

  // Financial summary calculation
  const totalContractAmount = contracts.reduce((sum, c) => sum + c.totalAmount, 0);
  const totalPaid = contracts.reduce((sum, c) => sum + c.paidAmount, 0);
  const totalRemaining = contracts.reduce((sum, c) => sum + c.remainingAmount, 0);
  const hasOverdue = contracts.some((c) => c.status === "overdue" || c.status === "defaulted");

  // Quick nav items
  const quickNav: QuickNavItem[] = [
    { key: "ortho", label: "التقويم", icon: Activity, count: orthoCases.length, color: "text-purple-600 bg-purple-50" },
    { key: "surgery", label: "الجراحة", icon: Scissors, count: surgeryCases.length, color: "text-red-600 bg-red-50" },
    { key: "general", label: "العام", icon: Stethoscope, count: recentTreatments.length, color: "text-teal-600 bg-teal-50" },
    { key: "prescriptions", label: "الوصفات", icon: Pill, count: recentPrescriptions.length, color: "text-rose-600 bg-rose-50" },
    { key: "finance", label: "المالية", icon: Wallet, color: hasOverdue ? "text-red-600 bg-red-50" : "text-green-600 bg-green-50" },
    { key: "appointments", label: "المواعيد", icon: Calendar, count: upcomingAppointments.length, color: "text-blue-600 bg-blue-50" },
  ];

  if (loading) {
    return (
      <div className="space-y-4 animate-pulse">
        {Array.from({ length: 6 }).map((_, i) => (
          <div key={i} className="h-24 bg-gray-100 rounded-xl" />
        ))}
      </div>
    );
  }

  return (
    <div className="space-y-5">
      {/* Quick Navigation */}
      <div className="grid grid-cols-3 sm:grid-cols-6 gap-2">
        {quickNav.map((nav) => (
          <a
            key={nav.key}
            href={`#section-${nav.key}`}
            className={cn(
              "flex flex-col items-center gap-1.5 p-3 rounded-xl border border-gray-100 hover:border-gray-200 transition",
              nav.color
            )}
          >
            <nav.icon className="w-5 h-5" />
            <span className="text-xs font-medium">{nav.label}</span>
            {nav.count !== undefined && nav.count > 0 && (
              <span className="text-[10px] font-bold bg-white/60 rounded-full px-1.5 py-0.5">
                {nav.count}
              </span>
            )}
          </a>
        ))}
      </div>

      {/* Patient Info Card */}
      <div id="section-patient" className="bg-white rounded-xl border border-gray-200 shadow-sm p-4">
        <div className="flex items-start gap-4">
          <div className="w-12 h-12 clinic-gradient rounded-xl flex items-center justify-center text-white text-lg font-extrabold flex-shrink-0">
            {patient.firstName.charAt(0)}
          </div>
          <div className="flex-1">
            <h3 className="font-bold text-gray-900">
              {patient.firstName} {patient.middleName ?? ""} {patient.lastName}
            </h3>
            <div className="flex flex-wrap gap-3 mt-1 text-xs text-gray-500">
              <span className="font-mono bg-gray-100 px-1.5 py-0.5 rounded">{patient.patientNumber}</span>
              {patient.phone && <span dir="ltr">{patient.phone}</span>}
              {patient.primaryDoctorName && <span>طبيب: {patient.primaryDoctorName}</span>}
            </div>
          </div>
          <Link
            href={`/patients/${patientId}`}
            className="flex items-center gap-1 text-xs text-clinic-teal hover:underline flex-shrink-0"
          >
            الملف الكامل
            <ChevronLeft className="w-3 h-3" />
          </Link>
        </div>
      </div>

      {/* Active Ortho Cases */}
      {orthoCases.length > 0 && (
        <div id="section-ortho" className="bg-white rounded-xl border border-gray-200 shadow-sm p-4 space-y-3">
          <div className="flex items-center justify-between">
            <h4 className="text-sm font-bold text-gray-900 flex items-center gap-2">
              <Activity className="w-4 h-4 text-purple-600" />
              الحالات التقويمية النشطة
            </h4>
            <Link href={`/ortho?patientId=${patientId}`} className="text-xs text-clinic-teal hover:underline">
              عرض الكل
            </Link>
          </div>
          <div className="space-y-2">
            {orthoCases.map((c) => (
              <Link
                key={c.id}
                href={`/ortho/${c.id}`}
                className="flex items-center justify-between p-3 bg-gray-50 rounded-lg hover:bg-purple-50 hover:border-purple-200 border border-transparent transition"
              >
                <div className="flex items-center gap-2">
                  <Activity className="w-3.5 h-3.5 text-purple-600 flex-shrink-0" />
                  <span className="text-sm font-medium text-gray-900">{c.caseNumber}</span>
                  {c.applianceType && <span className="text-xs text-gray-500">{c.applianceType}</span>}
                </div>
                <div className="flex items-center gap-3">
                  <div className="flex items-center gap-1.5">
                    <div className="w-16 h-1.5 bg-gray-200 rounded-full overflow-hidden">
                      <div className="h-full bg-purple-500 rounded-full" style={{ width: `${c.stagePercentage}%` }} />
                    </div>
                    <span className="text-xs text-gray-500">{c.stagePercentage}%</span>
                  </div>
                  <span className={cn(
                    "text-xs px-1.5 py-0.5 rounded-full font-medium",
                    c.status === "active" ? "bg-purple-50 text-purple-700" : "bg-gray-100 text-gray-500"
                  )}>
                    {ORTHO_STATUS[c.status] ?? c.status}
                  </span>
                </div>
              </Link>
            ))}
          </div>
        </div>
      )}

      {/* Active Surgery Cases */}
      {surgeryCases.length > 0 && (
        <div id="section-surgery" className="bg-white rounded-xl border border-gray-200 shadow-sm p-4 space-y-3">
          <div className="flex items-center justify-between">
            <h4 className="text-sm font-bold text-gray-900 flex items-center gap-2">
              <Scissors className="w-4 h-4 text-red-600" />
              الحالات الجراحية
            </h4>
            <Link href={`/surgery?patientId=${patientId}`} className="text-xs text-clinic-teal hover:underline">
              عرض الكل
            </Link>
          </div>
          <div className="space-y-2">
            {surgeryCases.map((c) => (
              <Link
                key={c.id}
                href={`/surgery/${c.id}`}
                className="flex items-center justify-between p-3 bg-gray-50 rounded-lg hover:bg-red-50 hover:border-red-200 border border-transparent transition"
              >
                <div className="flex items-center gap-2">
                  <Scissors className="w-3.5 h-3.5 text-red-600 flex-shrink-0" />
                  <span className="text-sm font-medium text-gray-900">{c.caseNumber}</span>
                  <span className="text-xs text-gray-500">{c.surgeryType}</span>
                </div>
                <span className={cn(
                  "text-xs px-1.5 py-0.5 rounded-full font-medium",
                  c.status === "completed" ? "bg-green-50 text-green-700" :
                  c.status === "in_progress" ? "bg-yellow-50 text-yellow-700" :
                  "bg-gray-100 text-gray-500"
                )}>
                  {SURGERY_STATUS[c.status] ?? c.status}
                </span>
              </Link>
            ))}
          </div>
        </div>
      )}

      {/* Recent General Treatments */}
      <div id="section-general" className="bg-white rounded-xl border border-gray-200 shadow-sm p-4 space-y-3">
        <div className="flex items-center justify-between">
          <h4 className="text-sm font-bold text-gray-900 flex items-center gap-2">
            <Stethoscope className="w-4 h-4 text-teal-600" />
            آخر المعالجات
          </h4>
          <Link href="/general" className="text-xs text-clinic-teal hover:underline">
            عرض الكل
          </Link>
        </div>
        {recentTreatments.length === 0 ? (
          <p className="text-sm text-gray-400 text-center py-4">لا توجد معالجات</p>
        ) : (
          <div className="space-y-2">
            {recentTreatments.map((t) => (
              <div key={t.id} className="flex items-center justify-between p-2.5 bg-gray-50 rounded-lg text-sm">
                <div className="flex items-center gap-2">
                  <span className="text-gray-900 font-medium">{t.treatmentType}</span>
                  {t.toothNumber && <span className="text-xs font-mono text-gray-500">سن {t.toothNumber}</span>}
                </div>
                <div className="flex items-center gap-2">
                  {t.cost ? <span className="text-xs font-mono text-green-700">{formatYemeniRiyal(t.cost)}</span> : null}
                  <span className="text-xs text-gray-400">{formatArabicDate(t.createdAt)}</span>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Recent Prescriptions */}
      <div id="section-prescriptions" className="bg-white rounded-xl border border-gray-200 shadow-sm p-4 space-y-3">
        <div className="flex items-center justify-between">
          <h4 className="text-sm font-bold text-gray-900 flex items-center gap-2">
            <Pill className="w-4 h-4 text-rose-600" />
            آخر الوصفات
          </h4>
          <Link href={`/prescriptions?patientId=${patientId}`} className="text-xs text-clinic-teal hover:underline">
            عرض الكل
          </Link>
        </div>
        {recentPrescriptions.length === 0 ? (
          <p className="text-sm text-gray-400 text-center py-4">لا توجد وصفات</p>
        ) : (
          <div className="space-y-2">
            {recentPrescriptions.map((p) => (
              <div key={p.id} className="flex items-center justify-between p-2.5 bg-gray-50 rounded-lg text-sm">
                <div className="flex items-center gap-2">
                  <span className="text-gray-900 font-medium">
                    {p.diagnosis ?? "وصفة طبية"}
                  </span>
                  <span className="text-xs text-gray-500">{p.drugCount} دواء</span>
                </div>
                <div className="flex items-center gap-2">
                  {p.doctorName && <span className="text-xs text-gray-500">{p.doctorName}</span>}
                  <span className="text-xs text-gray-400">{formatArabicDate(p.createdAt)}</span>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Pending Referrals */}
      {pendingReferrals.length > 0 && (
        <div className="bg-white rounded-xl border border-orange-200 shadow-sm p-4 space-y-3">
          <h4 className="text-sm font-bold text-gray-900 flex items-center gap-2">
            <FileText className="w-4 h-4 text-orange-600" />
            إحالات قيد الانتظار
          </h4>
          <div className="space-y-2">
            {pendingReferrals.map((r) => (
              <div key={r.id} className="flex items-center justify-between p-2.5 bg-orange-50 rounded-lg text-sm border border-orange-100">
                <div>
                  <span className="text-gray-900 font-medium">{r.reason}</span>
                  {r.toSpecialty && <span className="text-xs text-gray-500 mr-2">← {r.toSpecialty}</span>}
                </div>
                <span className="text-xs px-1.5 py-0.5 rounded-full bg-yellow-50 text-yellow-700 font-medium">
                  {REFERRAL_STATUS[r.status] ?? r.status}
                </span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Financial Summary */}
      <div id="section-finance" className="bg-white rounded-xl border border-gray-200 shadow-sm p-4 space-y-3">
        <div className="flex items-center justify-between">
          <h4 className="text-sm font-bold text-gray-900 flex items-center gap-2">
            <Wallet className="w-4 h-4 text-green-600" />
            الملخص المالي
          </h4>
          <Link href={`/finance?patientId=${patientId}`} className="text-xs text-clinic-teal hover:underline">
            عرض الكل
          </Link>
        </div>
        <div className="grid grid-cols-3 gap-3">
          <div className="bg-green-50 rounded-lg p-3">
            <p className="text-xs text-green-600">إجمالي العقود</p>
            <p className="text-lg font-bold text-green-700">{formatYemeniRiyal(totalContractAmount)}</p>
          </div>
          <div className="bg-teal-50 rounded-lg p-3">
            <p className="text-xs text-teal-600">المدفوع</p>
            <p className="text-lg font-bold text-teal-700">{formatYemeniRiyal(totalPaid)}</p>
          </div>
          <div className={cn("rounded-lg p-3", totalRemaining > 0 ? "bg-orange-50" : "bg-gray-50")}>
            <p className={cn("text-xs", totalRemaining > 0 ? "text-orange-600" : "text-gray-500")}>المتبقي</p>
            <p className={cn("text-lg font-bold", totalRemaining > 0 ? "text-orange-700" : "text-gray-600")}>
              {formatYemeniRiyal(totalRemaining)}
            </p>
          </div>
        </div>
        {hasOverdue && (
          <div className="flex items-center gap-2 text-xs text-red-700 bg-red-50 border border-red-200 rounded-lg p-2.5">
            <Wallet className="w-3.5 h-3.5" />
            توجد دفعات متأخرة — يرجى المتابعة
          </div>
        )}
      </div>

      {/* Upcoming Appointments */}
      <div id="section-appointments" className="bg-white rounded-xl border border-gray-200 shadow-sm p-4 space-y-3">
        <div className="flex items-center justify-between">
          <h4 className="text-sm font-bold text-gray-900 flex items-center gap-2">
            <Calendar className="w-4 h-4 text-blue-600" />
            المواعيد القادمة
          </h4>
          <Link href={`/appointments?patientId=${patientId}`} className="text-xs text-clinic-teal hover:underline">
            عرض الكل
          </Link>
        </div>
        {upcomingAppointments.length === 0 ? (
          <p className="text-sm text-gray-400 text-center py-4">لا توجد مواعيد قادمة</p>
        ) : (
          <div className="space-y-2">
            {upcomingAppointments.map((a) => (
              <div key={a.id} className="flex items-center justify-between p-2.5 bg-gray-50 rounded-lg text-sm">
                <div className="flex items-center gap-2">
                  <Calendar className="w-3.5 h-3.5 text-blue-600 flex-shrink-0" />
                  <span className="text-gray-900 font-medium">{formatArabicDate(a.appointmentDate)}</span>
                  {a.appointmentTime && <span className="text-xs text-gray-500" dir="ltr">{a.appointmentTime}</span>}
                  {a.doctorName && <span className="text-xs text-gray-500">{a.doctorName}</span>}
                </div>
                <span className={cn(
                  "text-xs px-1.5 py-0.5 rounded-full font-medium",
                  a.status === "Confirmed" ? "bg-teal-50 text-teal-700" :
                  a.status === "Scheduled" ? "bg-blue-50 text-blue-700" :
                  "bg-gray-100 text-gray-600"
                )}>
                  {APPOINTMENT_STATUS[a.status] ?? a.status}
                </span>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
