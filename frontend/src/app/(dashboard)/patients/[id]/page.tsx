"use client";
import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { ArrowRight, User, FileText, Stethoscope, Clock, Phone, MapPin, Pencil } from "lucide-react";
import type { PatientProfile } from "@/types/patient";
import api from "@/lib/api";
import { cn, GENDER_LABELS, formatArabicDate } from "@/lib/utils";

type Tab = "info" | "medical" | "dental" | "timeline";

const TABS: { key: Tab; label: string; icon: typeof User }[] = [
  { key: "info",     label: "المعلومات العامة", icon: User },
  { key: "medical",  label: "التاريخ الطبي",    icon: FileText },
  { key: "dental",   label: "التاريخ السني",    icon: Stethoscope },
  { key: "timeline", label: "السجل الزمني",     icon: Clock },
];

export default function PatientProfilePage() {
  const { id } = useParams<{ id: string }>();
  const [patient, setPatient] = useState<PatientProfile | null>(null);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<Tab>("info");

  useEffect(() => {
    api.get<PatientProfile>(`/api/patients/${id}`)
      .then((r) => setPatient(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
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
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
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

          {activeTab === "timeline" && (
            <div className="text-center py-12 text-gray-400">
              <Clock className="w-10 h-10 mx-auto mb-2 opacity-30" />
              <p className="text-sm">السجل الزمني — قيد التطوير</p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
