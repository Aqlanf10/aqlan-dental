"use client";
import { useEffect, useState } from "react";
import { printScreen } from "@/lib/printUtils";
import { useParams } from "next/navigation";
import { printScreen } from "@/lib/printUtils";
import Link from "next/link";
import { printScreen } from "@/lib/printUtils";
import { ArrowRight, Printer, Trash2 } from "lucide-react";
import { printScreen } from "@/lib/printUtils";
import type { Prescription } from "@/types/prescription";
import { printScreen } from "@/lib/printUtils";
import api from "@/lib/api";
import { printScreen } from "@/lib/printUtils";
import { PrescriptionPrint } from "@/components/prescriptions/PrescriptionPrint";
import { printScreen } from "@/lib/printUtils";
import { formatArabicDate } from "@/lib/utils";
import { printScreen } from "@/lib/printUtils";

export default function PrescriptionDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [prescription, setPrescription] = useState<Prescription | null>(null);
  const [loading, setLoading] = useState(true);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    api.get<Prescription>(`/api/prescriptions/${id}`)
      .then((r) => setPrescription(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [id]);

  const handleDelete = async () => {
    if (!confirm("هل أنت متأكد من حذف هذه الوصفة؟")) return;
    setDeleting(true);
    try {
      await api.delete(`/api/prescriptions/${id}`);
      window.location.href = "/prescriptions";
    } catch {
      setDeleting(false);
    }
  };

  if (loading) {
    return (
      <div className="max-w-2xl space-y-4 animate-pulse">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-20 bg-gray-100 rounded-xl" />
        ))}
      </div>
    );
  }

  if (!prescription) {
    return <div className="text-center py-20 text-gray-400 text-sm">الوصفة غير موجودة</div>;
  }

  return (
    <div className="space-y-5 max-w-2xl">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Link href="/prescriptions" className="hover:text-clinic-blue transition">الوصفات</Link>
        <span>/</span>
        <Link href={`/patients/${prescription.patientId}`} className="hover:text-clinic-blue transition">
          {prescription.patientName}
        </Link>
        <span>/</span>
        <span className="text-gray-900 font-medium">{formatArabicDate(prescription.createdAt)}</span>
      </div>

      {/* Toolbar */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Link href="/prescriptions" className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 transition text-gray-500">
            <ArrowRight className="w-4 h-4" />
          </Link>
          <h1 className="text-2xl font-extrabold text-gray-900">وصفة طبية</h1>
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={() => printScreen()}
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
          >
            <Printer className="w-4 h-4" />
            طباعة
          </button>
          <button
            onClick={handleDelete}
            disabled={deleting}
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg border border-red-200 text-red-600 hover:bg-red-50 transition disabled:opacity-50"
          >
            <Trash2 className="w-4 h-4" />
            حذف
          </button>
        </div>
      </div>

      {/* Prescription view (screen + print) */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-6 print:border-0 print:shadow-none print:p-0">
        <PrescriptionPrint prescription={prescription} />
      </div>
    </div>
  );
}
