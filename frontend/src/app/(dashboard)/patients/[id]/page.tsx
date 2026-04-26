"use client";
import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import {
  User, FileText, Stethoscope, Clock, Phone, MapPin, Pencil, Grid3x3,
  Calendar, Activity, Wallet, Pill, Plus, Scissors,
} from "lucide-react";
import type { PatientProfile } from "@/types/patient";
import api from "@/lib/api";
import { cn, GENDER_LABELS, formatArabicDate, APPOINTMENT_STATUS_LABELS } from "@/lib/utils";
import { DentalChart } from "@/components/dental/DentalChart";
import { TreatmentHistory } from "@/components/dental/TreatmentHistory";

interface PatientSummary {
  totalAppointments:   number;
  completedAppointments: number;
  activeOrthoCases:    number;
  totalPaid:           number;
  totalOutstanding:    number;
  prescriptionsCount:  number;
}

// ─── Patient Timeline Component ───────────────────────────────────────────────
interface TimelineEvent {
  type: string;
  id: string;
  date: string;
  title: string;
  description: string;
  status?: string;
}

const STATUS_COLORS: Record<string, string> = {
  Scheduled: "bg-blue-100 text-blue-700",
  Confirmed: "bg-teal-100 text-teal-700",
  Arrived: "bg-yellow-100 text-yellow-700",
  InProgress: "bg-purple-100 text-purple-700",
  Completed: "bg-green-100 text-green-700",
  Cancelled: "bg-gray-100 text-gray-500",
  NoShow: "bg-red-100 text-red-700",
};

function PatientTimeline({ patientId }: { patientId: string }) {
  const [events, setEvents] = useState<TimelineEvent[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<TimelineEvent[]>(`/api/patients/${patientId}/timeline`)
      .then((r) => setEvents(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [patientId]);

  if (loading) {
    return (
      <div className="space-y-3 animate-pulse">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-16 bg-gray-100 rounded-lg" />
        ))}
      </div>
    );
  }

  if (!events.length) {
    return (
      <div className="text-center py-12 text-gray-400">
        <Clock className="w-10 h-10 mx-auto mb-2 opacity-30" />
        <p className="text-sm">لا يوجد سجل زمني بعد</p>
      </div>
    );
  }

  return (
    <div className="relative">
      <div className="absolute right-[19px] top-0 bottom-0 w-0.5 bg-gray-100" />
      <div className="space-y-4">
        {events.map((ev) => (
          <div key={ev.id} className="flex gap-4 relative">
            <div className="w-10 h-10 rounded-full bg-white border-2 border-clinic-teal flex items-center justify-center flex-shrink-0 z-10">
              <Clock className="w-4 h-4 text-clinic-teal" />
            </div>
            <div className="flex-1 bg-gray-50 rounded-lg p-3 border border-gray-100">
              <div className="flex items-center justify-between gap-2 flex-wrap">
                <span className="font-semibold text-sm text-gray-900">{ev.title}</span>
                {ev.status && (
                  <span className={cn("text-xs px-2 py-0.5 rounded-full font-medium", STATUS_COLORS[ev.status] ?? "bg-gray-100 text-gray-600")}>
                    {APPOINTMENT_STATUS_LABELS[ev.status] ?? ev.status}
                  </span>
                )}
              </div>
              <p className="text-xs text-gray-500 mt-0.5">{ev.description}</p>
              <p className="text-xs text-gray-400 mt-1">{formatArabicDate(ev.date)}</p>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

// ─── Patient Cases Panel ──────────────────────────────────────────────────────
interface OrthoCase { id: string; caseNumber: string; applianceType?: string; status: string; stagePercentage: number; doctorName?: string; }
interface SurgeryCase { id: string; caseNumber: string; surgeryType: string; status: string; doctorName?: string; }

const ORTHO_STATUS_LABELS: Record<string, string> = { active: "نشطة", completed: "مكتملة", cancelled: "ملغاة" };
const SURGERY_STATUS_LABELS: Record<string, string> = { scheduled: "مجدولة", in_progress: "جارية", completed: "مكتملة", cancelled: "ملغاة" };

function PatientCasesPanel({ patientId }: { patientId: string }) {
  const [orthoCases, setOrthoCases] = useState<OrthoCase[]>([]);
  const [surgeryCases, setSurgeryCases] = useState<SurgeryCase[]>([]);

  useEffect(() => {
    api.get<OrthoCase[]>(`/api/ortho-cases?patientId=${patientId}&pageSize=10`)
      .then((r) => setOrthoCases(r.data))
      .catch(() => {});
    api.get<{ data: SurgeryCase[] }>(`/api/surgery-cases?patientId=${patientId}&pageSize=10`)
      .then((r) => setSurgeryCases(r.data.data ?? []))
      .catch(() => {});
  }, [patientId]);

  if (!orthoCases.length && !surgeryCases.length) return null;

  return (
    <div className="mt-4 pt-4 border-t border-gray-100 space-y-4">
      {orthoCases.length > 0 && (
        <div>
          <p className="text-xs font-semibold text-gray-400 uppercase tracking-wide mb-2">الحالات التقويمية</p>
          <div className="space-y-2">
            {orthoCases.map((c) => (
              <Link key={c.id} href={`/ortho/${c.id}`}
                className="flex items-center justify-between p-2.5 bg-gray-50 rounded-lg hover:bg-teal-50 hover:border-teal-200 border border-transparent transition"
              >
                <div className="flex items-center gap-2">
                  <Activity className="w-3.5 h-3.5 text-teal-600 flex-shrink-0" />
                  <span className="text-sm font-medium text-gray-900">{c.caseNumber}</span>
                  {c.applianceType && <span className="text-xs text-gray-500">{c.applianceType}</span>}
                </div>
                <div className="flex items-center gap-3">
                  <div className="flex items-center gap-1.5">
                    <div className="w-16 h-1.5 bg-gray-200 rounded-full overflow-hidden">
                      <div className="h-full bg-clinic-teal rounded-full" style={{ width: `${c.stagePercentage}%` }} />
                    </div>
                    <span className="text-xs text-gray-500">{c.stagePercentage}%</span>
                  </div>
                  <span className={cn("text-xs px-1.5 py-0.5 rounded-full font-medium",
                    c.status === "active" ? "bg-teal-50 text-teal-700" : "bg-gray-100 text-gray-500"
                  )}>
                    {ORTHO_STATUS_LABELS[c.status] ?? c.status}
                  </span>
                </div>
              </Link>
            ))}
          </div>
        </div>
      )}

      {surgeryCases.length > 0 && (
        <div>
          <p className="text-xs font-semibold text-gray-400 uppercase tracking-wide mb-2">الحالات الجراحية</p>
          <div className="space-y-2">
            {surgeryCases.map((c) => (
              <Link key={c.id} href={`/surgery/${c.id}`}
                className="flex items-center justify-between p-2.5 bg-gray-50 rounded-lg hover:bg-red-50 hover:border-red-200 border border-transparent transition"
              >
                <div className="flex items-center gap-2">
                  <Scissors className="w-3.5 h-3.5 text-red-600 flex-shrink-0" />
                  <span className="text-sm font-medium text-gray-900">{c.caseNumber}</span>
                  <span className="text-xs text-gray-500">{c.surgeryType}</span>
                </div>
                <span className={cn("text-xs px-1.5 py-0.5 rounded-full font-medium",
                  c.status === "completed" ? "bg-green-50 text-green-700" :
                  c.status === "in_progress" ? "bg-yellow-50 text-yellow-700" :
                  "bg-gray-100 text-gray-500"
                )}>
                  {SURGERY_STATUS_LABELS[c.status] ?? c.status}
                </span>
              </Link>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

type Tab = "info" | "medical" | "dental" | "chart" | "timeline";

const TABS: { key: Tab; label: string; icon: typeof User }[] = [
  { key: "info",     label: "المعلومات العامة", icon: User },
  { key: "medical",  label: "التاريخ الطبي",    icon: FileText },
  { key: "dental",   label: "التاريخ السني",    icon: Stethoscope },
  { key: "chart",    label: "المخطط السني",     icon: Grid3x3 },
  { key: "timeline", label: "السجل الزمني",     icon: Clock },
];

export default function PatientProfilePage() {
  const { id } = useParams<{ id: string }>();
  const [patient, setPatient]   = useState<PatientProfile | null>(null);
  const [summary, setSummary]   = useState<PatientSummary | null>(null);
  const [loading, setLoading]   = useState(true);
  const [activeTab, setActiveTab] = useState<Tab>("info");

  useEffect(() => {
    api.get<PatientProfile>(`/api/patients/${id}`)
      .then((r) => setPatient(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
    api.get<PatientSummary>(`/api/patients/${id}/summary`)
      .then((r) => setSummary(r.data))
      .catch(() => {});
  }, [id]);

  if (loading) {
    return (
      <div className="space-y-4 animate-pulse">
        <div className="h-28 bg-gray-100 rounded-xl" />
        <div className="h-64 bg-gray-100 rounded-xl" />
      </div>
    );
  }

  if (!patient) {
    return (
      <div className="text-center py-20 text-gray-400">
        المريض غير موجود
      </div>
    );
  }

  return (
    <div className="space-y-5 max-w-5xl">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Link href="/patients" className="hover:text-clinic-teal transition">المرضى</Link>
        <span>/</span>
        <span className="text-gray-900 font-medium">{patient.firstName} {patient.lastName}</span>
      </div>

      {/* Banner */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-4">
        {/* Top row */}
        <div className="flex items-start justify-between gap-4">
          <div className="flex items-start gap-4">
            <div className="w-14 h-14 clinic-gradient rounded-2xl flex items-center justify-center text-white text-xl font-extrabold flex-shrink-0">
              {patient.firstName.charAt(0)}
            </div>
            <div>
              <div className="flex items-center gap-3 flex-wrap">
                <h1 className="text-xl font-extrabold text-gray-900">
                  {patient.firstName} {patient.middleName} {patient.lastName}
                </h1>
                <span className="font-mono text-xs bg-gray-100 px-2.5 py-1 rounded text-gray-600">
                  {patient.patientNumber}
                </span>
                <span className="text-xs bg-green-100 text-green-700 px-2 py-0.5 rounded-full font-medium">
                  نشط
                </span>
              </div>
              <div className="mt-2 flex flex-wrap items-center gap-4 text-sm text-gray-500">
                {patient.gender && (
                  <span className="flex items-center gap-1">
                    <User className="w-3.5 h-3.5" />
                    {GENDER_LABELS[patient.gender]} {patient.age ? `· ${patient.age} سنة` : ""}
                  </span>
                )}
                {patient.phone && (
                  <span className="flex items-center gap-1 font-mono" dir="ltr">
                    <Phone className="w-3.5 h-3.5" />
                    {patient.phone}
                  </span>
                )}
                {patient.address && (
                  <span className="flex items-center gap-1">
                    <MapPin className="w-3.5 h-3.5" />
                    {patient.address}
                  </span>
                )}
                {patient.primaryDoctorName && (
                  <span className="flex items-center gap-1">
                    <Stethoscope className="w-3.5 h-3.5" />
                    {patient.primaryDoctorName}
                  </span>
                )}
              </div>
              <p className="text-xs text-gray-400 mt-1">
                تسجيل: {formatArabicDate(patient.createdAt)}
              </p>
            </div>
          </div>
          <Link
            href={`/patients/${id}/edit`}
            className="flex items-center gap-1.5 px-3 py-1.5 text-sm border border-gray-200 rounded-lg hover:bg-gray-50 transition text-gray-600 flex-shrink-0"
          >
            <Pencil className="w-3.5 h-3.5" />
            تعديل
          </Link>
        </div>

        {/* Quick stats row */}
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3 pt-1 border-t border-gray-50">
          {[
            { icon: Calendar, label: "المواعيد",     value: summary?.totalAppointments   ?? "—", color: "text-blue-600",   bg: "bg-blue-50" },
            { icon: Calendar, label: "مكتملة",       value: summary?.completedAppointments ?? "—", color: "text-green-600",  bg: "bg-green-50" },
            { icon: Activity, label: "تقويم نشط",   value: summary?.activeOrthoCases    ?? "—", color: "text-purple-600", bg: "bg-purple-50" },
            { icon: Wallet,   label: "مدفوع",        value: summary ? `${summary.totalPaid.toLocaleString()}` : "—", color: "text-teal-600",   bg: "bg-teal-50" },
            { icon: Wallet,   label: "متبقي",        value: summary ? `${summary.totalOutstanding.toLocaleString()}` : "—", color: "text-orange-600", bg: "bg-orange-50" },
            { icon: Pill,     label: "الوصفات",      value: summary?.prescriptionsCount  ?? "—", color: "text-rose-600",   bg: "bg-rose-50" },
          ].map(({ icon: Icon, label, value, color, bg }) => (
            <div key={label} className={cn("rounded-lg px-3 py-2 flex items-center gap-2", bg)}>
              <Icon className={cn("w-4 h-4 flex-shrink-0", color)} />
              <div className="min-w-0">
                <p className="text-xs text-gray-500 truncate">{label}</p>
                <p className={cn("text-sm font-bold truncate", color)}>{value}</p>
              </div>
            </div>
          ))}
        </div>

        {/* Action buttons */}
        <div className="flex flex-wrap gap-2 pt-1 border-t border-gray-50">
          <Link
            href={`/appointments/new?patientId=${id}&patientName=${encodeURIComponent(patient.firstName + " " + patient.lastName)}`}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 transition"
          >
            <Plus className="w-3.5 h-3.5" />
            موعد جديد
          </Link>
          <Link
            href={`/prescriptions/new?patientId=${id}&patientName=${encodeURIComponent(patient.firstName + " " + patient.lastName)}`}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg border border-gray-200 text-gray-600 hover:bg-gray-50 transition"
          >
            <Pill className="w-3.5 h-3.5" />
            وصفة طبية
          </Link>
          <Link
            href={`/finance/contracts/new?patientId=${id}&patientName=${encodeURIComponent(patient.firstName + " " + patient.lastName)}`}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg border border-gray-200 text-gray-600 hover:bg-gray-50 transition"
          >
            <Wallet className="w-3.5 h-3.5" />
            عقد جديد
          </Link>
          <Link
            href={`/ortho/new?patientId=${id}&patientName=${encodeURIComponent(patient.firstName + " " + patient.lastName)}`}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg border border-gray-200 text-gray-600 hover:bg-gray-50 transition"
          >
            <Activity className="w-3.5 h-3.5" />
            حالة تقويمية
          </Link>
          <Link
            href={`/surgery/new?patientId=${id}&patientName=${encodeURIComponent(patient.firstName + " " + patient.lastName)}`}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg border border-gray-200 text-gray-600 hover:bg-gray-50 transition"
          >
            <Scissors className="w-3.5 h-3.5" />
            حالة جراحية
          </Link>
        </div>
      </div>

      {/* Tabs */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
        <div className="flex border-b border-gray-100 overflow-x-auto">
          {TABS.map(({ key, label, icon: Icon }) => (
            <button
              key={key}
              onClick={() => setActiveTab(key)}
              className={cn(
                "flex items-center gap-2 px-5 py-3.5 text-sm font-medium whitespace-nowrap border-b-2 transition",
                activeTab === key
                  ? "border-clinic-teal text-clinic-teal"
                  : "border-transparent text-gray-500 hover:text-gray-900"
              )}
            >
              <Icon className="w-4 h-4" />
              {label}
            </button>
          ))}
        </div>

        <div className="p-5">
          {activeTab === "info" && (
            <div>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {[
                  ["الاسم الكامل", `${patient.firstName} ${patient.middleName ?? ""} ${patient.lastName}`.trim()],
                  ["رقم المريض", patient.patientNumber],
                  ["الجنس", GENDER_LABELS[patient.gender ?? ""] ?? "—"],
                  ["العمر", patient.age ? `${patient.age} سنة` : "—"],
                  ["تاريخ الميلاد", patient.dateOfBirth ?? "—"],
                  ["الهاتف", patient.phone ?? "—"],
                  ["واتساب", patient.whatsApp ?? "—"],
                  ["العنوان", patient.address ?? "—"],
                  ["المهنة", patient.occupation ?? "—"],
                  ["مصدر الإحالة", patient.referralSource ?? "—"],
                  ["الطبيب المعالج", patient.primaryDoctorName ?? "—"],
                  ["الفرع", patient.branchName ?? "—"],
                ].map(([label, value]) => (
                  <div key={label} className="border-b border-gray-50 pb-3 last:border-0">
                    <p className="text-xs text-gray-400 mb-0.5">{label}</p>
                    <p className="text-sm font-medium text-gray-900">{value}</p>
                  </div>
                ))}
              </div>
              <PatientCasesPanel patientId={id} />
            </div>
          )}

          {activeTab === "medical" && (
            <div className="space-y-3">
              {!patient.medicalHistory ? (
                <p className="text-gray-400 text-sm">لا يوجد تاريخ طبي مسجّل</p>
              ) : (
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  {[
                    ["الأمراض المزمنة", patient.medicalHistory.chronicDiseases],
                    ["الأدوية الحالية", patient.medicalHistory.currentMedications],
                    ["حساسية الأدوية", patient.medicalHistory.drugAllergies],
                    ["اضطرابات النزيف", patient.medicalHistory.bleedingDisorders ? "نعم" : "لا"],
                    ["الحمل", patient.medicalHistory.isPregnant === "yes" ? "نعم" : patient.medicalHistory.isPregnant === "no" ? "لا" : "لا ينطبق"],
                    ["مشاكل TMJ", patient.medicalHistory.tmjProblems ? "نعم" : "لا"],
                    ["العمليات السابقة", patient.medicalHistory.previousSurgeries],
                    ["ملاحظات", patient.medicalHistory.notes],
                  ].map(([label, value]) => (
                    <div key={label} className="border-b border-gray-50 pb-3">
                      <p className="text-xs text-gray-400 mb-0.5">{label}</p>
                      <p className="text-sm font-medium text-gray-900">{value ?? "—"}</p>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}

          {activeTab === "dental" && (
            <div className="space-y-3">
              {!patient.dentalHistory ? (
                <p className="text-gray-400 text-sm">لا يوجد تاريخ سني مسجّل</p>
              ) : (
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  {[
                    ["الشكوى الرئيسية", patient.dentalHistory.chiefComplaint],
                    ["العلاجات السابقة", patient.dentalHistory.previousTreatments],
                    ["التنفس الفموي", patient.dentalHistory.mouthBreathing ? "نعم" : "لا"],
                    ["صرير الأسنان", patient.dentalHistory.bruxism ? "نعم" : "لا"],
                    ["مص الإبهام", patient.dentalHistory.thumbSucking ? "نعم" : "لا"],
                    ["وضع اللسان الخاطئ", patient.dentalHistory.tongueThrusing ? "نعم" : "لا"],
                    ["ملاحظات", patient.dentalHistory.notes],
                  ].map(([label, value]) => (
                    <div key={label} className="border-b border-gray-50 pb-3">
                      <p className="text-xs text-gray-400 mb-0.5">{label}</p>
                      <p className="text-sm font-medium text-gray-900">{value ?? "—"}</p>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}

          {activeTab === "chart" && (
            <div className="space-y-6">
              <DentalChart patientId={id} />
              <div className="border-t border-gray-100 pt-6">
                <TreatmentHistory patientId={id} />
              </div>
            </div>
          )}

          {activeTab === "timeline" && <PatientTimeline patientId={id} />}
        </div>
      </div>
    </div>
  );
}
