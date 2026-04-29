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

      <div className="receipt-print bg-white border-2 border-gray-900 rounded-lg p-6 max-w-md mx-auto font-sans" dir="rtl">
        {/* Header */}
        <div className="text-center border-b-2 border-gray-900 pb-4 mb-4">
          <h2 className="text-lg font-extrabold text-gray-900">{clinicName}</h2>
          <p className="text-xs text-gray-600 mt-1">{clinicAddress}</p>
          <p className="text-sm font-bold mt-2 text-gray-700">سند قبض</p>
        </div>

        {/* Receipt number & date */}
        <div className="flex items-center justify-between text-sm mb-4">
          <div>
            <span className="text-gray-500">رقم السند: </span>
            <span className="font-mono font-bold">{payment.receiptNumber ?? "—"}</span>
          </div>
          <div>
            <span className="text-gray-500">التاريخ: </span>
            <span className="font-semibold">{formatArabicDate(payment.paymentDate)}</span>
          </div>
        </div>

        {/* Patient & amount */}
        <div className="bg-gray-50 rounded-lg p-4 space-y-3 text-sm mb-4">
          <div className="flex justify-between">
            <span className="text-gray-500">استلمنا من: </span>
            <span className="font-bold text-gray-900">{payment.patientName}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-gray-500">المبلغ: </span>
            <span className="font-bold font-mono text-lg text-green-700">{formatYemeniRiyal(payment.amount)}</span>
          </div>
          {payment.serviceDescription && (
            <div className="flex justify-between">
              <span className="text-gray-500">مقابل: </span>
              <span className="font-medium">{payment.serviceDescription}</span>
            </div>
          )}
          <div className="flex justify-between">
            <span className="text-gray-500">طريقة الدفع: </span>
            <span className="font-medium">{methodLabel[payment.paymentMethod ?? "cash"] ?? payment.paymentMethod}</span>
          </div>
          {payment.doctorName && (
            <div className="flex justify-between">
              <span className="text-gray-500">الطبيب: </span>
              <span className="font-medium">{payment.doctorName}</span>
            </div>
          )}
        </div>

        {/* PDF + Print buttons */}
        <div className="flex gap-2 mb-4 no-print">
          <button
            onClick={handlePrintPdf}
            disabled={generatingPdf}
            className="flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60 transition flex-1 justify-center"
          >
            <FileDown className="w-4 h-4" />
            {generatingPdf ? "جارٍ التوليد..." : "طباعة PDF"}
          </button>
          <button
            onClick={() => window.print()}
            className="flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-lg border border-gray-300 hover:bg-gray-50 transition flex-1 justify-center"
          >
            <Printer className="w-4 h-4" />
            طباعة
          </button>
        </div>

        {/* Signature area */}
        <div className="flex justify-between text-xs text-gray-500 pt-4 border-t border-dashed border-gray-300">
          <div>
            <p>توقيع المستلم:</p>
            <div className="mt-4 border-b border-gray-400 w-32" />
          </div>
          <div className="text-center">
            <p>ختم المركز</p>
            <div className="mt-4 w-16 h-16 border-2 border-dashed border-gray-300 rounded-full mx-auto" />
          </div>
          <div className="text-end">
            <p>توقيع المريض:</p>
            <div className="mt-4 border-b border-gray-400 w-32" />
          </div>
        </div>
      </div>
    </>
  );
}
