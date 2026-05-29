"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { PrintLayout } from "@/components/print/PrintLayout";
import api from "@/lib/api";
import { formatArabicDate, formatYemeniRiyal } from "@/lib/utils";
import type { PatientProfile } from "@/types/patient";
import type { PatientFinanceSummary, Payment } from "@/types/finance";

// ─── Labels ───────────────────────────────────────────────────────────────────

const PAYMENT_METHOD_LABELS: Record<string, string> = {
  cash: "نقداً",
  bank_transfer: "تحويل بنكي",
  card: "بطاقة",
};

// ─── Component ────────────────────────────────────────────────────────────────

export default function PaymentReceiptPrintPage() {
  const { id, paymentId } = useParams<{ id: string; paymentId: string }>();
  const [patient, setPatient] = useState<PatientProfile | null>(null);
  const [payment, setPayment] = useState<Payment | null>(null);
  const [finance, setFinance] = useState<PatientFinanceSummary | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      api.get<PatientProfile>(`/api/patients/${id}`).then((r) => r.data).catch(() => null),
      api.get<Payment>(`/api/finance-v3/payments/${paymentId}`).then((r) => r.data).catch(() => null),
      api.get<PatientFinanceSummary>(`/api/finance-v3/patients/${id}/finance-summary`).then((r) => r.data).catch(() => null),
    ]).then(([p, pm, f]) => {
      setPatient(p);
      setPayment(pm);
      setFinance(f);
    }).finally(() => setLoading(false));
  }, [id, paymentId]);

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen" dir="rtl">
        <div className="text-center">
          <div className="w-8 h-8 border-2 border-[#3d7ab5] border-t-transparent rounded-full animate-spin mx-auto mb-3" />
          <p className="text-sm text-[#64748b]">جاري تحميل الإيصال...</p>
        </div>
      </div>
    );
  }

  if (!patient || !payment) {
    return (
      <div className="flex items-center justify-center min-h-screen" dir="rtl">
        <div className="text-center">
          <p className="text-lg font-bold text-red-500 mb-2">{!patient ? "المريض غير موجود" : "الدفعة غير موجودة"}</p>
          <button onClick={() => window.history.back()} className="text-sm text-[#3d7ab5] hover:underline">رجوع</button>
        </div>
      </div>
    );
  }

  const patientName = `${patient.firstName} ${patient.middleName ? patient.middleName + " " : ""}${patient.lastName}`;

  return (
    <PrintLayout
      title="سند قبض"
      subtitle={payment.receiptNumber ? `رقم السند: ${payment.receiptNumber}` : undefined}
      backUrl={`/patients/${id}?tab=payments`}
    >
      <div style={{ fontSize: "13px", lineHeight: 1.8, maxWidth: "600px", margin: "0 auto" }}>

        {/* ═══ Receipt Number & Date ═══ */}
        <div className="avoid-break" style={{ marginBottom: "20px", display: "flex", justifyContent: "space-between", borderBottom: "1px solid #e8f0f9", paddingBottom: "12px" }}>
          <div>
            <span style={{ color: "#64748b", fontSize: "12px" }}>رقم السند</span>
            <p style={{ fontWeight: 700, fontFamily: "monospace", color: "#0d2137", margin: 0 }}>{payment.receiptNumber || "—"}</p>
          </div>
          <div style={{ textAlign: "left" }}>
            <span style={{ color: "#64748b", fontSize: "12px" }}>التاريخ</span>
            <p style={{ fontWeight: 600, color: "#0d2137", margin: 0 }}>{formatArabicDate(payment.paymentDate)}</p>
          </div>
        </div>

        {/* ═══ Receipt Body ═══ */}
        <div className="avoid-break" style={{ marginBottom: "24px", padding: "16px", background: "#f7fafd", borderRadius: "8px", border: "1px solid #e8f0f9" }}>
          <div style={{ display: "grid", gap: "10px" }}>
            <div style={{ display: "flex", justifyContent: "space-between" }}>
              <span style={{ color: "#64748b" }}>استلمنا من: </span>
              <strong style={{ color: "#0d2137" }}>{payment.patientName || patientName}</strong>
            </div>
            <div style={{ display: "flex", justifyContent: "space-between" }}>
              <span style={{ color: "#64748b" }}>رقم المريض: </span>
              <strong style={{ color: "#0d2137" }}>{patient.patientNumber}</strong>
            </div>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <span style={{ color: "#64748b" }}>المبلغ: </span>
              <span style={{ fontWeight: 800, fontSize: "20px", color: "#166534", fontFamily: "monospace" }}>{formatYemeniRiyal(payment.amount)}</span>
            </div>
            {payment.serviceDescription && (
              <div style={{ display: "flex", justifyContent: "space-between" }}>
                <span style={{ color: "#64748b" }}>مقابل: </span>
                <strong style={{ color: "#0d2137" }}>{payment.serviceDescription}</strong>
              </div>
            )}
            <div style={{ display: "flex", justifyContent: "space-between" }}>
              <span style={{ color: "#64748b" }}>طريقة الدفع: </span>
              <strong style={{ color: "#0d2137" }}>{PAYMENT_METHOD_LABELS[payment.paymentMethod ?? "cash"] ?? payment.paymentMethod ?? "نقداً"}</strong>
            </div>
            {payment.doctorName && (
              <div style={{ display: "flex", justifyContent: "space-between" }}>
                <span style={{ color: "#64748b" }}>الطبيب: </span>
                <strong style={{ color: "#0d2137" }}>د. {payment.doctorName}</strong>
              </div>
            )}
            {payment.notes && (
              <div style={{ display: "flex", justifyContent: "space-between" }}>
                <span style={{ color: "#64748b" }}>ملاحظات: </span>
                <span style={{ color: "#0d2137" }}>{payment.notes}</span>
              </div>
            )}
            {finance && (
              <div style={{ display: "flex", justifyContent: "space-between", borderTop: "1px dashed #dce8f5", paddingTop: "8px", marginTop: "4px" }}>
                <span style={{ color: "#64748b" }}>الرصيد المتبقي: </span>
                <strong style={{ color: finance.outstandingBalance > 0 ? "#92400e" : "#166534" }}>{finance.outstandingBalance.toLocaleString()} ر.ي</strong>
              </div>
            )}
          </div>
        </div>

        {/* ═══ Signature Lines ═══ */}
        <div className="avoid-break" style={{ display: "flex", justifyContent: "space-between", paddingTop: "24px", borderTop: "1px dashed #dce8f5" }}>
          <div style={{ textAlign: "center" }}>
            <p style={{ color: "#64748b", fontSize: "12px", margin: 0 }}>المستلم</p>
            <div style={{ width: "120px", borderBottom: "1px solid #94a3b8", marginTop: "32px" }} />
          </div>
          <div style={{ textAlign: "center" }}>
            <p style={{ color: "#64748b", fontSize: "12px", margin: 0 }}>المحاسب / الإدارة</p>
            <div style={{ width: "120px", borderBottom: "1px solid #94a3b8", marginTop: "32px" }} />
          </div>
          <div style={{ textAlign: "center" }}>
            <p style={{ color: "#64748b", fontSize: "12px", margin: 0 }}>ختم المركز</p>
            <div style={{ width: "56px", height: "56px", border: "2px dashed #dce8f5", borderRadius: "50%", marginTop: "12px", marginLeft: "auto", marginRight: "auto" }} />
          </div>
        </div>
      </div>
    </PrintLayout>
  );
}
