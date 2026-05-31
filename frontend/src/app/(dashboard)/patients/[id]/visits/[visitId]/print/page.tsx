"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { PrintLayout } from "@/components/print/PrintLayout";
import api from "@/lib/api";
import { formatArabicDate } from "@/lib/utils";
import type { PatientProfile } from "@/types/patient";

// ─── Types ────────────────────────────────────────────────────────────────────

interface AppointmentInfo {
  appointmentDate: string;
  appointmentTime: string;
  appointmentType: string;
  appointmentStatus: string;
  doctorName?: string;
}

interface VisitDto {
  id: string;
  patientId: string;
  appointmentId?: string;
  visitDate: string;
  visitType?: string;
  specialty?: string;
  doctorId?: string;
  doctorName?: string;
  chiefComplaint?: string;
  clinicalNotes?: string;
  treatmentDone?: string;
  diagnosis?: string;
  instructions?: string;
  nextVisitPlan?: string;
  cost?: number;
  nextVisitDate?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  appointment?: AppointmentInfo | null;
}

// ─── Labels ───────────────────────────────────────────────────────────────────

const VISIT_TYPES: Record<string, string> = {
  Consultation: "استشارة",
  FollowUp: "متابعة",
  Emergency: "طوارئ",
  Treatment: "علاج",
  Review: "مراجعة",
};

const SPECIALTY_LABELS: Record<string, string> = {
  General: "طب أسنان عام",
  Orthodontics: "تقويم أسنان",
  OralSurgery: "جراحة فم",
  Periodontics: "لثة",
  Endodontics: "علاج عصب",
  Prosthodontics: "تركيبات",
};

// ─── Component ────────────────────────────────────────────────────────────────

export default function VisitPrintPage() {
  const { id, visitId } = useParams<{ id: string; visitId: string }>();
  const [patient, setPatient] = useState<PatientProfile | null>(null);
  const [visit, setVisit] = useState<VisitDto | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      api.get<PatientProfile>(`/api/patients/${id}`).then((r) => r.data).catch(() => null),
      api.get<VisitDto>(`/api/visits/${visitId}`).then((r) => r.data).catch(() => null),
    ]).then(([p, v]) => {
      setPatient(p);
      setVisit(v);
    }).finally(() => setLoading(false));
  }, [id, visitId]);

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen" dir="rtl">
        <div className="text-center">
          <div className="w-8 h-8 border-2 border-[#3d7ab5] border-t-transparent rounded-full animate-spin mx-auto mb-3" />
          <p className="text-sm text-[#64748b]">جاري تحميل التقرير...</p>
        </div>
      </div>
    );
  }

  if (!patient || !visit) {
    return (
      <div className="flex items-center justify-center min-h-screen" dir="rtl">
        <div className="text-center">
          <p className="text-lg font-bold text-red-500 mb-2">{!patient ? "المريض غير موجود" : "الزيارة غير موجودة"}</p>
          <button onClick={() => window.history.back()} className="text-sm text-[#3d7ab5] hover:underline">رجوع</button>
        </div>
      </div>
    );
  }

  const patientName = `${patient.firstName} ${patient.middleName ? patient.middleName + " " : ""}${patient.lastName}`;

  return (
    <PrintLayout
      title="تقرير زيارة سريرية"
      subtitle={`${patientName} — ${formatArabicDate(visit.visitDate)}`}
      backUrl={`/patients/${id}?tab=visits`}
    >
      <div style={{ fontSize: "13px", lineHeight: 1.8 }}>

        {/* ═══ Patient Info ═══ */}
        <div className="avoid-break" style={{ marginBottom: "20px" }}>
          <h3 style={{ fontSize: "14px", fontWeight: 700, color: "#3d7ab5", borderBottom: "1px solid #e8f0f9", paddingBottom: "6px", marginBottom: "10px" }}>
            معلومات المريض
          </h3>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "4px 24px" }}>
            <div><span style={{ color: "#64748b" }}>رقم المريض: </span><strong>{patient.patientNumber}</strong></div>
            <div><span style={{ color: "#64748b" }}>الاسم: </span><strong>{patientName}</strong></div>
            <div><span style={{ color: "#64748b" }}>الهاتف: </span><strong>{patient.phone || "—"}</strong></div>
          </div>
        </div>

        {/* ═══ Visit Info ═══ */}
        <div className="avoid-break" style={{ marginBottom: "20px" }}>
          <h3 style={{ fontSize: "14px", fontWeight: 700, color: "#3d7ab5", borderBottom: "1px solid #e8f0f9", paddingBottom: "6px", marginBottom: "10px" }}>
            معلومات الزيارة
          </h3>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "4px 24px" }}>
            <div><span style={{ color: "#64748b" }}>تاريخ الزيارة: </span><strong>{formatArabicDate(visit.visitDate)}</strong></div>
            <div><span style={{ color: "#64748b" }}>الطبيب: </span><strong>{visit.doctorName || "—"}</strong></div>
            <div><span style={{ color: "#64748b" }}>التخصص: </span><strong>{visit.specialty ? SPECIALTY_LABELS[visit.specialty] ?? visit.specialty : "—"}</strong></div>
            <div><span style={{ color: "#64748b" }}>نوع الزيارة: </span><strong>{visit.visitType ? VISIT_TYPES[visit.visitType] ?? visit.visitType : "—"}</strong></div>
            {visit.cost != null && visit.cost > 0 && (
              <div><span style={{ color: "#64748b" }}>التكلفة: </span><strong>{visit.cost.toLocaleString()} ر.ي</strong></div>
            )}
            {visit.appointment && (
              <div><span style={{ color: "#64748b" }}>موعد مرتبط: </span><strong>{formatArabicDate(visit.appointment.appointmentDate)} · {visit.appointment.appointmentTime}</strong></div>
            )}
          </div>
        </div>

        {/* ═══ Clinical Content ═══ */}
        <div className="avoid-break" style={{ marginBottom: "20px" }}>
          <h3 style={{ fontSize: "14px", fontWeight: 700, color: "#3d7ab5", borderBottom: "1px solid #e8f0f9", paddingBottom: "6px", marginBottom: "10px" }}>
            المحتوى السريري
          </h3>
          <div style={{ display: "grid", gap: "12px" }}>
            {visit.chiefComplaint && (
              <div style={{ padding: "8px 12px", border: "1px solid #fde8c8", borderRadius: "6px", background: "#fff8f0" }}>
                <span style={{ color: "#92400e", fontWeight: 600, fontSize: "12px" }}>الشكوى الرئيسية: </span>
                <span style={{ color: "#0d2137" }}>{visit.chiefComplaint}</span>
              </div>
            )}
            {visit.diagnosis && (
              <div style={{ padding: "8px 12px", border: "1px solid #fde8c8", borderRadius: "6px", background: "#fff8f0" }}>
                <span style={{ color: "#92400e", fontWeight: 600, fontSize: "12px" }}>التشخيص: </span>
                <span style={{ color: "#0d2137" }}>{visit.diagnosis}</span>
              </div>
            )}
            {visit.clinicalNotes && (
              <div style={{ padding: "8px 12px", border: "1px solid #e8f0f9", borderRadius: "6px", background: "#f7fafd" }}>
                <span style={{ color: "#3d7ab5", fontWeight: 600, fontSize: "12px" }}>ملاحظات سريرية: </span>
                <span style={{ color: "#0d2137", whiteSpace: "pre-wrap" }}>{visit.clinicalNotes}</span>
              </div>
            )}
            {visit.treatmentDone && (
              <div style={{ padding: "8px 12px", border: "1px solid #d1f0d1", borderRadius: "6px", background: "#f0faf0" }}>
                <span style={{ color: "#166534", fontWeight: 600, fontSize: "12px" }}>العلاج المنجز: </span>
                <span style={{ color: "#0d2137", whiteSpace: "pre-wrap" }}>{visit.treatmentDone}</span>
              </div>
            )}
            {visit.instructions && (
              <div style={{ padding: "8px 12px", border: "1px solid #d1daf0", borderRadius: "6px", background: "#f0f4ff" }}>
                <span style={{ color: "#1e40af", fontWeight: 600, fontSize: "12px" }}>التعليمات للمريض: </span>
                <span style={{ color: "#0d2137", whiteSpace: "pre-wrap" }}>{visit.instructions}</span>
              </div>
            )}
            {visit.nextVisitPlan && (
              <div style={{ padding: "8px 12px", border: "1px solid #e8d1f0", borderRadius: "6px", background: "#faf0ff" }}>
                <span style={{ color: "#7e22ce", fontWeight: 600, fontSize: "12px" }}>خطة الزيارة القادمة: </span>
                <span style={{ color: "#0d2137", whiteSpace: "pre-wrap" }}>{visit.nextVisitPlan}</span>
                {visit.nextVisitDate && (
                  <span style={{ color: "#7e22ce", fontSize: "12px" }}> — الموعد: {formatArabicDate(visit.nextVisitDate)}</span>
                )}
              </div>
            )}
          </div>
        </div>

        {/* ═══ Meta Info ═══ */}
        <div className="avoid-break">
          <div style={{ display: "flex", gap: "16px", fontSize: "11px", color: "#94a3b8", borderTop: "1px solid #f1f5f9", paddingTop: "8px" }}>
            <span>أُنشئ: {formatArabicDate(visit.createdAt)}</span>
            {visit.createdAt !== visit.updatedAt && (
              <span>آخر تعديل: {formatArabicDate(visit.updatedAt)}</span>
            )}
          </div>
        </div>
      </div>
    </PrintLayout>
  );
}
