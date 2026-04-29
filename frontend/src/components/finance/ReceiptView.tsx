"use client";
import { useState } from "react";
import { Printer, FileDown } from "lucide-react";
import type { Payment } from "@/types/finance";
import { formatYemeniRiyal, formatArabicDate } from "@/lib/utils";
import { generateReceiptPdf } from "@/utils/receiptPdf";

interface Props {
  payment: Payment;
  clinicName?: string;
  clinicAddress?: string;
}

export function ReceiptView({
  payment,
  clinicName = "مركز د. عقلان الكامل لطب وتقويم الأسنان",
  clinicAddress = "تعز، اليمن — شارع التحرير الأعلى",
}: Props) {
  const [generatingPdf, setGeneratingPdf] = useState(false);

  const methodLabel: Record<string, string> = {
    cash: "نقداً",
    bank_transfer: "تحويل بنكي",
    card: "بطاقة",
  };

  const handlePrintPdf = async () => {
    setGeneratingPdf(true);
    try {
      const doc = generateReceiptPdf(payment, clinicName, clinicAddress);
      const filename = `سند_${payment.receiptNumber ?? payment.id}.pdf`;
      doc.save(filename);
    } catch (err) {
      console.error("Failed to generate receipt PDF:", err);
    } finally {
      setGeneratingPdf(false);
    }
  };

  return (
    <>
      {/* Print styles */}
      <style>{`
        @media print {
          body > *:not(.receipt-print) { display: none !important; }
          .receipt-print { display: block !important; }
          .no-print { display: none !important; }
        }
      `}</style>

      <div className="receipt-print bg-white border-2 border-[#0d2137] rounded-lg p-6 max-w-md mx-auto font-sans" dir="rtl">
        {/* Header */}
        <div className="text-center border-b-2 border-[#0d2137] pb-4 mb-4">
          <h2 className="text-lg font-extrabold text-[#0d2137]">{clinicName}</h2>
          <p className="text-xs text-[#64748b] mt-1">{clinicAddress}</p>
          <p className="text-sm font-bold mt-2 text-[#64748b]">سند قبض</p>
        </div>

        {/* Receipt number & date */}
        <div className="flex items-center justify-between text-sm mb-4">
          <div>
            <span className="text-[#64748b]">رقم السند: </span>
            <span className="font-mono font-bold">{payment.receiptNumber ?? "—"}</span>
          </div>
          <div>
            <span className="text-[#64748b]">التاريخ: </span>
            <span className="font-semibold">{formatArabicDate(payment.paymentDate)}</span>
          </div>
        </div>

        {/* Patient & amount */}
        <div className="bg-[#f7fafd] rounded-lg p-4 space-y-3 text-sm mb-4">
          <div className="flex justify-between">
            <span className="text-[#64748b]">استلمنا من: </span>
            <span className="font-bold text-[#0d2137]">{payment.patientName}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-[#64748b]">المبلغ: </span>
            <span className="font-bold font-mono text-lg text-[#22c55e]">{formatYemeniRiyal(payment.amount)}</span>
          </div>
          {payment.serviceDescription && (
            <div className="flex justify-between">
              <span className="text-[#64748b]">مقابل: </span>
              <span className="font-medium">{payment.serviceDescription}</span>
            </div>
          )}
          <div className="flex justify-between">
            <span className="text-[#64748b]">طريقة الدفع: </span>
            <span className="font-medium">{methodLabel[payment.paymentMethod ?? "cash"] ?? payment.paymentMethod}</span>
          </div>
          {payment.doctorName && (
            <div className="flex justify-between">
              <span className="text-[#64748b]">الطبيب: </span>
              <span className="font-medium">{payment.doctorName}</span>
            </div>
          )}
        </div>

        {/* PDF + Print buttons */}
        <div className="flex gap-2 mb-4 no-print">
          <button
            onClick={handlePrintPdf}
            disabled={generatingPdf}
            className="flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-lg bg-accent-blue text-white hover:bg-blue-hover disabled:opacity-60 transition flex-1 justify-center"
          >
            <FileDown className="w-4 h-4" />
            {generatingPdf ? "جارٍ التوليد..." : "طباعة PDF"}
          </button>
          <button
            onClick={() => window.print()}
            className="flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-lg border border-[#e8f0f9] hover:bg-[#f7fafd] transition flex-1 justify-center"
          >
            <Printer className="w-4 h-4" />
            طباعة
          </button>
        </div>

        {/* Signature area */}
        <div className="flex justify-between text-xs text-[#64748b] pt-4 border-t border-dashed border-[#dce8f5]">
          <div>
            <p>توقيع المستلم:</p>
            <div className="mt-4 border-b border-[#94a3b8] w-32" />
          </div>
          <div className="text-center">
            <p>ختم المركز</p>
            <div className="mt-4 w-16 h-16 border-2 border-dashed border-[#dce8f5] rounded-full mx-auto" />
          </div>
          <div className="text-end">
            <p>توقيع المريض:</p>
            <div className="mt-4 border-b border-[#94a3b8] w-32" />
          </div>
        </div>
      </div>
    </>
  );
}
