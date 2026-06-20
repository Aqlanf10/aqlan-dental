"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { PrintLayout } from "@/components/print/PrintLayout";
import api from "@/lib/api";
import { formatArabicDate, GENDER_LABELS } from "@/lib/utils";
import type { PatientProfile } from "@/types/patient";
import type { PatientFinanceSummary } from "@/types/finance";

// ─── Types ────────────────────────────────────────────────────────────────────

interface PatientSummary {
  totalAppointments: number;
  completedAppointments: number;
  activeOrthoCases: number;
  totalPaid: number;
  totalOutstanding: number;
  prescriptionsCount: number;
  lastVisitDate?: string;
  lastVisitDoctor?: string;
  lastVisitDiagnosis?: string;
  nextAppointmentDate?: string;
  nextAppointmentTime?: string;
  nextAppointmentType?: string;
  nextAppointmentDoctor?: string;
  chiefComplaint?: string;
  currentDiagnosis?: string;
  nextPlannedStep?: string;
  medicalAlerts?: string[];
}

interface TimelineEvent {
  type: string;
  id: string;
  date: string;
  title: string;
  description: string;
  status?: string;
}

// ─── Labels ───────────────────────────────────────────────────────────────────

const EVENT_TYPE_LABELS: Record<string, string> = {
  appointment: "موعد",
  visit: "زيارة",
  payment: "دفعة",
  document: "مستند",
  photo: "صورة",
  radiograph: "أشعة",
};

const FINANCIAL_STATUS_LABELS: Record<string, string> = {
  no_plan: "لا توجد خطة مالية",
  paid_full: "مدفوع بالكامل",
  has_balance: "عليه متبقي",
  overdue: "متأخر",
};

// ─── Component ────────────────────────────────────────────────────────────────

export default function PatientSummaryPrintPage() {
  const { id } = useParams<{ id: string }>();
  const [patient, setPatient] = useState<PatientProfile | null>(null);
  const [summary, setSummary] = useState<PatientSummary | null>(null);
  const [finance, setFinance] = useState<PatientFinanceSummary | null>(null);
  const [timeline, setTimeline] = useState<TimelineEvent[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      api.get<PatientProfile>(`/api/patients/${id}`).then((r) => r.data).catch(() => null),
      api.get<PatientSummary>(`/api/patients/${id}/summary`).then((r) => r.data).catch(() => null),
      api.get<PatientFinanceSummary>(`/api/patients/${id}/finance-summary`).then((r) => r.data).catch(() => null),
      api.get<TimelineEvent[]>(`/api/patients/${id}/timeline`).then((r) => r.data).catch(() => []),
    ]).then(([p, s, f, t]) => {
      setPatient(p);
      setSummary(s);
      setFinance(f);
      setTimeline(t);
    }).finally(() => setLoading(false));
  }, [id]);

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <div className="w-8 h-8 border-2 border-[#3d7ab5] border-t-transparent rounded-full animate-spin mx-auto mb-3" />
          <p className="text-sm text-[#64748b]">جاري تحميل التقرير...</p>
        </div>
      </div>
    );
  }

  if (!patient) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <p className="text-lg font-bold text-red-500 mb-2">المريض غير موجود</p>
          <button onClick={() => window.history.back()} className="text-sm text-[#3d7ab5] hover:underline">رجوع</button>
        </div>
      </div>
    );
  }

  const patientName = `${patient.firstName} ${patient.middleName ? patient.middleName + " " : ""}${patient.lastName}`;

  return (
    <PrintLayout
      title="ملخص ملف المريض"
      subtitle={`${patientName} — ${patient.patientNumber}`}
      backUrl={`/patients/${id}`}
    >
      <div style={{ fontSize: "13px", lineHeight: 1.8 }}>

        {/* ═══ Patient Information ═══ */}
        <div className="avoid-break" style={{ marginBottom: "20px" }}>
          <h3 style={{ fontSize: "14px", fontWeight: 700, color: "#3d7ab5", borderBottom: "1px solid #e8f0f9", paddingBottom: "6px", marginBottom: "10px" }}>
            معلومات المريض
          </h3>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "4px 24px" }}>
            <div><span style={{ color: "#64748b" }}>رقم المريض: </span><strong>{patient.patientNumber}</strong></div>
            <div><span style={{ color: "#64748b" }}>الاسم الكامل: </span><strong>{patientName}</strong></div>
            <div><span style={{ color: "#64748b" }}>الهاتف: </span><strong>{patient.phone || "—"}</strong></div>
            <div><span style={{ color: "#64748b" }}>العمر: </span><strong>{patient.age ? `${patient.age} سنة` : "—"}</strong></div>
            <div><span style={{ color: "#64748b" }}>الجنس: </span><strong>{patient.gender ? GENDER_LABELS[patient.gender] ?? patient.gender : "—"}</strong></div>
            <div><span style={{ color: "#64748b" }}>الطبيب الرئيسي: </span><strong>{patient.primaryDoctorName || "غير محدد"}</strong></div>
            {patient.branchName && (
              <div><span style={{ color: "#64748b" }}>الفرع: </span><strong>{patient.branchName}</strong></div>
            )}
          </div>
        </div>

        {/* ═══ Medical Alerts ═══ */}
        {summary?.medicalAlerts && summary.medicalAlerts.length > 0 && (
          <div className="avoid-break" style={{ marginBottom: "20px", padding: "10px 14px", border: "1.5px solid #fecaca", borderRadius: "8px", background: "#fef2f2" }}>
            <h4 style={{ fontSize: "13px", fontWeight: 700, color: "#dc2626", marginBottom: "6px" }}>تنبيهات طبية</h4>
            <div style={{ display: "flex", flexWrap: "wrap", gap: "6px" }}>
              {summary.medicalAlerts.map((alert, idx) => (
                <span key={idx} style={{ fontSize: "11px", padding: "2px 8px", borderRadius: "999px", background: "#fee2e2", color: "#b91c1c", fontWeight: 600 }}>
                  {alert}
                </span>
              ))}
            </div>
          </div>
        )}

        {/* ═══ Clinical Summary ═══ */}
        <div className="avoid-break" style={{ marginBottom: "20px" }}>
          <h3 style={{ fontSize: "14px", fontWeight: 700, color: "#3d7ab5", borderBottom: "1px solid #e8f0f9", paddingBottom: "6px", marginBottom: "10px" }}>
            الملخص السريري
          </h3>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "6px 24px" }}>
            <div><span style={{ color: "#64748b" }}>الشكوى الرئيسية: </span><strong>{summary?.chiefComplaint || patient.dentalHistory?.chiefComplaint || "غير مسجلة"}</strong></div>
            <div><span style={{ color: "#64748b" }}>التشخيص الحالي: </span><strong>{summary?.currentDiagnosis || "غير مسجل"}</strong></div>
            <div><span style={{ color: "#64748b" }}>الخطوة المخططة: </span><strong>{summary?.nextPlannedStep || "غير محددة"}</strong></div>
            <div><span style={{ color: "#64748b" }}>حالة العلاج: </span><strong>
              {summary?.activeOrthoCases && summary.activeOrthoCases > 0
                ? `تقويم نشط (${summary.activeOrthoCases})`
                : "لا يوجد علاج نشط"}
            </strong></div>
          </div>
        </div>

        {/* ═══ Latest Visit ═══ */}
        <div className="avoid-break" style={{ marginBottom: "20px" }}>
          <h3 style={{ fontSize: "14px", fontWeight: 700, color: "#3d7ab5", borderBottom: "1px solid #e8f0f9", paddingBottom: "6px", marginBottom: "10px" }}>
            آخر زيارة
          </h3>
          {summary?.lastVisitDate ? (
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "4px 24px" }}>
              <div><span style={{ color: "#64748b" }}>التاريخ: </span><strong>{formatArabicDate(summary.lastVisitDate)}</strong></div>
              <div><span style={{ color: "#64748b" }}>الطبيب: </span><strong>{summary.lastVisitDoctor || "—"}</strong></div>
              <div style={{ gridColumn: "1 / -1" }}><span style={{ color: "#64748b" }}>التشخيص: </span><strong>{summary.lastVisitDiagnosis || "—"}</strong></div>
            </div>
          ) : (
            <p style={{ color: "#94a3b8" }}>لا توجد زيارات سابقة</p>
          )}
        </div>

        {/* ═══ Next Appointment ═══ */}
        <div className="avoid-break" style={{ marginBottom: "20px" }}>
          <h3 style={{ fontSize: "14px", fontWeight: 700, color: "#3d7ab5", borderBottom: "1px solid #e8f0f9", paddingBottom: "6px", marginBottom: "10px" }}>
            الموعد القادم
          </h3>
          {summary?.nextAppointmentDate ? (
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "4px 24px" }}>
              <div><span style={{ color: "#64748b" }}>التاريخ: </span><strong>{formatArabicDate(summary.nextAppointmentDate)}</strong></div>
              <div><span style={{ color: "#64748b" }}>الوقت: </span><strong>{summary.nextAppointmentTime || "—"}</strong></div>
              <div><span style={{ color: "#64748b" }}>الطبيب: </span><strong>{summary.nextAppointmentDoctor || "—"}</strong></div>
              <div><span style={{ color: "#64748b" }}>النوع: </span><strong>{summary.nextAppointmentType || "—"}</strong></div>
            </div>
          ) : (
            <p style={{ color: "#94a3b8" }}>لا يوجد موعد قادم</p>
          )}
        </div>

        {/* ═══ Financial Summary ═══ */}
        <div className="avoid-break" style={{ marginBottom: "20px" }}>
          <h3 style={{ fontSize: "14px", fontWeight: 700, color: "#3d7ab5", borderBottom: "1px solid #e8f0f9", paddingBottom: "6px", marginBottom: "10px" }}>
            الملخص المالي
          </h3>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "4px 24px" }}>
            <div><span style={{ color: "#64748b" }}>تكلفة العلاج: </span><strong>{finance?.totalTreatmentCost?.toLocaleString() ?? summary?.totalPaid?.toLocaleString() ?? "0"} ر.ي</strong></div>
            <div><span style={{ color: "#64748b" }}>المدفوع: </span><strong>{finance?.totalPaid?.toLocaleString() ?? summary?.totalPaid?.toLocaleString() ?? "0"} ر.ي</strong></div>
            <div><span style={{ color: "#64748b" }}>المتبقي: </span><strong>{finance?.outstandingBalance?.toLocaleString() ?? summary?.totalOutstanding?.toLocaleString() ?? "0"} ر.ي</strong></div>
            <div><span style={{ color: "#64748b" }}>الحالة المالية: </span><strong>{FINANCIAL_STATUS_LABELS[finance?.financialStatus ?? "no_plan"] ?? "غير محددة"}</strong></div>
            {(finance?.overdueAmount ?? 0) > 0 && (
              <div><span style={{ color: "#dc2626" }}>المتأخر: </span><strong style={{ color: "#dc2626" }}>{finance!.overdueAmount.toLocaleString()} ر.ي</strong></div>
            )}
          </div>
        </div>

        {/* ═══ Recent Timeline ═══ */}
        {timeline.length > 0 && (
          <div className="avoid-break">
            <h3 style={{ fontSize: "14px", fontWeight: 700, color: "#3d7ab5", borderBottom: "1px solid #e8f0f9", paddingBottom: "6px", marginBottom: "10px" }}>
              السجل الزمني الأخير
            </h3>
            <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "12px" }}>
              <thead>
                <tr style={{ borderBottom: "2px solid #e8f0f9" }}>
                  <th style={{ textAlign: "right", padding: "6px 8px", color: "#64748b", fontWeight: 600 }}>التاريخ</th>
                  <th style={{ textAlign: "right", padding: "6px 8px", color: "#64748b", fontWeight: 600 }}>النوع</th>
                  <th style={{ textAlign: "right", padding: "6px 8px", color: "#64748b", fontWeight: 600 }}>العنوان</th>
                  <th style={{ textAlign: "right", padding: "6px 8px", color: "#64748b", fontWeight: 600 }}>التفاصيل</th>
                </tr>
              </thead>
              <tbody>
                {timeline.slice(0, 15).map((ev) => (
                  <tr key={`${ev.type}-${ev.id}`} style={{ borderBottom: "1px solid #f1f5f9" }}>
                    <td style={{ padding: "5px 8px", color: "#0d2137" }}>{formatArabicDate(ev.date)}</td>
                    <td style={{ padding: "5px 8px" }}>
                      <span style={{ fontSize: "10px", padding: "1px 6px", borderRadius: "4px", background: "#f1f5f9", color: "#64748b", fontWeight: 500 }}>
                        {EVENT_TYPE_LABELS[ev.type] ?? ev.type}
                      </span>
                    </td>
                    <td style={{ padding: "5px 8px", fontWeight: 600, color: "#0d2137" }}>{ev.title}</td>
                    <td style={{ padding: "5px 8px", color: "#64748b" }}>{ev.description}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </PrintLayout>
  );
}
