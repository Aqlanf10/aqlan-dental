"use client";

import Link from "next/link";
import { User, Stethoscope } from "lucide-react";
import type { PatientProfile } from "@/types/patient";
import { GENDER_LABELS } from "@/lib/utils";
import { cn } from "@/lib/utils";

interface OrthoCase { id: string; caseNumber: string; applianceType?: string; status: string; stagePercentage: number; doctorName?: string; }
interface SurgeryCase { id: string; caseNumber: string; surgeryType: string; status: string; doctorName?: string; }

const ORTHO_STATUS_LABELS: Record<string, string> = { active: "نشطة", completed: "مكتملة", cancelled: "ملغاة" };
const SURGERY_STATUS_LABELS: Record<string, string> = { scheduled: "مجدولة", in_progress: "جارية", completed: "مكتملة", cancelled: "ملغاة" };

interface BasicInfoTabProps {
  patient: PatientProfile;
  orthoCases: OrthoCase[];
  surgeryCases: SurgeryCase[];
}

export function BasicInfoTab({ patient, orthoCases, surgeryCases }: BasicInfoTabProps) {
  return (
    <div className="space-y-4" dir="rtl">
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

      {/* Cases */}
      {(orthoCases.length > 0 || surgeryCases.length > 0) && (
        <div className="mt-4 pt-4 border-t border-gray-100 space-y-4">
          {orthoCases.length > 0 && (
            <div>
              <p className="text-xs font-semibold text-gray-400 uppercase tracking-wide mb-2">الحالات التقويمية</p>
              <div className="space-y-2">
                {orthoCases.map((c) => (
                  <Link key={c.id} href={`/ortho/${c.id}`}
                    className="flex items-center justify-between p-2.5 bg-gray-50 rounded-lg hover:bg-clinic-blue-50 hover:border-clinic-blue-100 border border-transparent transition"
                  >
                    <div className="flex items-center gap-2">
                      <Stethoscope className="w-3.5 h-3.5 text-clinic-blue flex-shrink-0" />
                      <span className="text-sm font-medium text-gray-900">{c.caseNumber}</span>
                      {c.applianceType && <span className="text-xs text-gray-500">{c.applianceType}</span>}
                    </div>
                    <div className="flex items-center gap-3">
                      <div className="flex items-center gap-1.5">
                        <div className="w-16 h-1.5 bg-gray-200 rounded-full overflow-hidden">
                          <div className="h-full bg-clinic-blue rounded-full" style={{ width: `${c.stagePercentage}%` }} />
                        </div>
                        <span className="text-xs text-gray-500">{c.stagePercentage}%</span>
                      </div>
                      <span className={cn("text-xs px-1.5 py-0.5 rounded-full font-medium",
                        c.status === "active" ? "bg-clinic-blue-50 text-clinic-blue" : "bg-gray-100 text-gray-500"
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
                      <User className="w-3.5 h-3.5 text-red-600 flex-shrink-0" />
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
      )}
    </div>
  );
}
