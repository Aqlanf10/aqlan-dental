"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { PrintLayout } from "@/components/print/PrintLayout";
import api from "@/lib/api";
import { formatArabicDate } from "@/lib/utils";
import type { PatientProfile } from "@/types/patient";
import type { PatientFinanceSummary, Payment, Contract } from "@/types/finance";

// ─── Labels ───────────────────────────────────────────────────────────────────

const CONTRACT_STATUS: Record<string, string> = {
  active: "نشط",
  completed: "مكتمل",
  cancelled: "ملغى",
  overdue: "متأخر",
};

const PAYMENT_METHOD_LABELS: Record<string, string> = {
  cash: "نقد", Cash: "نقد",
  card: "بطاقة", Card: "بطاقة",
  transfer: "تحويل", Transfer: "تحويل",
  bank_transfer: "تحويل بنكي", BankTransfer: "تحويل بنكي",
  other: "أخرى", Other: "أخرى",
};

const FINANCIAL_STATUS_LABELS: Record<string, string> = {
  no_plan: "لا توجد خطة مالية",
  paid_full: "مدفوع بالكامل",
  has_balance: "عليه متبقي",
  overdue: "متأخر",
};

// ─── Component ────────────────────────────────────────────────────────────────

export default function FinancialStatementPrintPage() {
  const { id } = useParams<{ id: string }>();
  const [patient, setPatient] = useState<PatientProfile | null>(null);
  const [finance, setFinance] = useState<PatientFinanceSummary | null>(null);
  const [contracts, setContracts] = useState<Contract[]>([]);
  const [payments, setPayments] = useState<Payment[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      api.get<PatientProfile>(`/api/patients/${id}`).then((r) => r.data).catch(() => null),
      api.get<PatientFinanceSummary>(`/api/patients/${id}/finance-summary`).then((r) => r.data).catch(() => null),
      api.get<Contract[]>(`/api/contracts?patientId=${id}`).then((r) => r.data).catch(() => []),
      api.get<Payment[]>(`/api/payments?patientId=${id}`).then((r) => r.data).catch(() => []),
    ]).then(([p, f, c, pm]) => {
      setPatient(p);
      setFinance(f);
      setContracts(c);
      setPayments(pm);
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
      title="كشف حساب مالي"
      subtitle={`${patientName} — ${patient.patientNumber}`}
      backUrl={`/patients/${id}?tab=finance`}
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

        {/* ═══ Payment Summary ═══ */}
        <div className="avoid-break" style={{ marginBottom: "20px" }}>
          <h3 style={{ fontSize: "14px", fontWeight: 700, color: "#3d7ab5", borderBottom: "1px solid #e8f0f9", paddingBottom: "6px", marginBottom: "10px" }}>
            ملخص الحساب
          </h3>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr 1fr", gap: "4px 16px" }}>
            <div style={{ padding: "8px 12px", background: "#f7fafd", borderRadius: "6px", border: "1px solid #e8f0f9" }}>
              <p style={{ color: "#64748b", fontSize: "11px", margin: 0 }}>تكلفة العلاج</p>
              <p style={{ fontWeight: 700, color: "#0d2137", margin: 0 }}>{finance?.totalTreatmentCost?.toLocaleString() ?? "0"} ر.ي</p>
            </div>
            <div style={{ padding: "8px 12px", background: "#f0faf0", borderRadius: "6px", border: "1px solid #d1f0d1" }}>
              <p style={{ color: "#166534", fontSize: "11px", margin: 0 }}>المدفوع</p>
              <p style={{ fontWeight: 700, color: "#166534", margin: 0 }}>{finance?.totalPaid?.toLocaleString() ?? "0"} ر.ي</p>
            </div>
            <div style={{ padding: "8px 12px", background: "#fff8f0", borderRadius: "6px", border: "1px solid #fde8c8" }}>
              <p style={{ color: "#92400e", fontSize: "11px", margin: 0 }}>المتبقي</p>
              <p style={{ fontWeight: 700, color: "#92400e", margin: 0 }}>{finance?.outstandingBalance?.toLocaleString() ?? "0"} ر.ي</p>
            </div>
            <div style={{ padding: "8px 12px", background: finance?.overdueAmount && finance.overdueAmount > 0 ? "#fef2f2" : "#f7fafd", borderRadius: "6px", border: `1px solid ${finance?.overdueAmount && finance.overdueAmount > 0 ? "#fecaca" : "#e8f0f9"}` }}>
              <p style={{ color: finance?.overdueAmount && finance.overdueAmount > 0 ? "#dc2626" : "#64748b", fontSize: "11px", margin: 0 }}>المتأخر</p>
              <p style={{ fontWeight: 700, color: finance?.overdueAmount && finance.overdueAmount > 0 ? "#dc2626" : "#94a3b8", margin: 0 }}>{finance?.overdueAmount ?? 0 > 0 ? finance!.overdueAmount.toLocaleString() : "لا يوجد"} ر.ي</p>
            </div>
          </div>
          <div style={{ marginTop: "8px" }}>
            <span style={{ color: "#64748b" }}>الحالة المالية: </span>
            <strong>{FINANCIAL_STATUS_LABELS[finance?.financialStatus ?? "no_plan"] ?? "غير محددة"}</strong>
          </div>
        </div>

        {/* ═══ Contracts Summary ═══ */}
        {contracts.length > 0 && (
          <div className="avoid-break" style={{ marginBottom: "20px" }}>
            <h3 style={{ fontSize: "14px", fontWeight: 700, color: "#3d7ab5", borderBottom: "1px solid #e8f0f9", paddingBottom: "6px", marginBottom: "10px" }}>
              العقود ({contracts.length})
            </h3>
            <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "12px" }}>
              <thead>
                <tr style={{ borderBottom: "2px solid #e8f0f9" }}>
                  <th style={{ textAlign: "right", padding: "6px 8px", color: "#64748b", fontWeight: 600 }}>التخصص</th>
                  <th style={{ textAlign: "right", padding: "6px 8px", color: "#64748b", fontWeight: 600 }}>المبلغ</th>
                  <th style={{ textAlign: "right", padding: "6px 8px", color: "#64748b", fontWeight: 600 }}>الخصم</th>
                  <th style={{ textAlign: "right", padding: "6px 8px", color: "#64748b", fontWeight: 600 }}>المدفوع</th>
                  <th style={{ textAlign: "right", padding: "6px 8px", color: "#64748b", fontWeight: 600 }}>المتبقي</th>
                  <th style={{ textAlign: "right", padding: "6px 8px", color: "#64748b", fontWeight: 600 }}>الحالة</th>
                </tr>
              </thead>
              <tbody>
                {contracts.map((c) => (
                  <tr key={c.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                    <td style={{ padding: "5px 8px" }}>{c.specialty || "—"}</td>
                    <td style={{ padding: "5px 8px", fontWeight: 600 }}>{c.totalAmount.toLocaleString()} ر.ي</td>
                    <td style={{ padding: "5px 8px" }}>{c.discountAmount && c.discountAmount > 0 ? `${c.discountAmount.toLocaleString()} ر.ي` : "—"}</td>
                    <td style={{ padding: "5px 8px", color: "#166534", fontWeight: 600 }}>{c.paidAmount.toLocaleString()} ر.ي</td>
                    <td style={{ padding: "5px 8px", color: c.remainingAmount > 0 ? "#92400e" : "#166534", fontWeight: 600 }}>{c.remainingAmount.toLocaleString()} ر.ي</td>
                    <td style={{ padding: "5px 8px" }}>
                      <span style={{ fontSize: "10px", padding: "1px 6px", borderRadius: "4px", background: "#f1f5f9", color: "#64748b" }}>
                        {CONTRACT_STATUS[c.status] ?? c.status}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {/* ═══ Payments List ═══ */}
        {payments.length > 0 ? (
          <div className="avoid-break">
            <h3 style={{ fontSize: "14px", fontWeight: 700, color: "#3d7ab5", borderBottom: "1px solid #e8f0f9", paddingBottom: "6px", marginBottom: "10px" }}>
              المدفوعات ({payments.length})
            </h3>
            <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "12px" }}>
              <thead>
                <tr style={{ borderBottom: "2px solid #e8f0f9" }}>
                  <th style={{ textAlign: "right", padding: "6px 8px", color: "#64748b", fontWeight: 600 }}>التاريخ</th>
                  <th style={{ textAlign: "right", padding: "6px 8px", color: "#64748b", fontWeight: 600 }}>المبلغ</th>
                  <th style={{ textAlign: "right", padding: "6px 8px", color: "#64748b", fontWeight: 600 }}>الطريقة</th>
                  <th style={{ textAlign: "right", padding: "6px 8px", color: "#64748b", fontWeight: 600 }}>وصف الخدمة</th>
                  <th style={{ textAlign: "right", padding: "6px 8px", color: "#64748b", fontWeight: 600 }}>الطبيب</th>
                  <th style={{ textAlign: "right", padding: "6px 8px", color: "#64748b", fontWeight: 600 }}>رقم الإيصال</th>
                </tr>
              </thead>
              <tbody>
                {payments.map((p) => (
                  <tr key={p.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                    <td style={{ padding: "5px 8px" }}>{formatArabicDate(p.paymentDate)}</td>
                    <td style={{ padding: "5px 8px", fontWeight: 600, color: "#166534" }}>{p.amount.toLocaleString()} ر.ي</td>
                    <td style={{ padding: "5px 8px" }}>{PAYMENT_METHOD_LABELS[p.paymentMethod ?? "cash"] ?? p.paymentMethod ?? "—"}</td>
                    <td style={{ padding: "5px 8px", color: "#64748b" }}>{p.serviceDescription || "—"}</td>
                    <td style={{ padding: "5px 8px" }}>{p.doctorName || "—"}</td>
                    <td style={{ padding: "5px 8px", fontFamily: "monospace", fontSize: "11px" }}>{p.receiptNumber || "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <div style={{ textAlign: "center", padding: "20px", color: "#94a3b8" }}>
            لا توجد دفعات مسجلة
          </div>
        )}
      </div>
    </PrintLayout>
  );
}
