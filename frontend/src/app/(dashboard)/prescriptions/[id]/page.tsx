"use client";
import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { ArrowRight, Printer, Trash2 } from "lucide-react";
import type { Prescription } from "@/types/prescription";
import api from "@/lib/api";
import { PrescriptionPrint } from "@/components/prescriptions/PrescriptionPrint";
import { formatArabicDate } from "@/lib/utils";

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
          <div key={i} className="h-20 bg-[#eef3f9] rounded-xl" />
        ))}
      </div>
    );
  }

  if (!prescription) {
    return <div className="text-center py-20 text-[#94a3b8] text-sm">الوصفة غير موجودة</div>;
  }

  return (
    <div className="space-y-5 max-w-2xl">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-[#64748b]">
        <Link href="/prescriptions" className="hover:text-accent-blue transition">الوصفات</Link>
        <span>/</span>
        <Link href={`/patients/${prescription.patientId}`} className="hover:text-accent-blue transition">
          {prescription.patientName}
        </Link>
        <span>/</span>
        <span className="text-[#0d2137] font-medium">{formatArabicDate(prescription.createdAt)}</span>
      </div>

      {/* Toolbar */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Link href="/prescriptions" className="p-1.5 rounded-lg border border-[#e8f0f9] hover:bg-[#f7fafd] transition text-[#64748b]">
            <ArrowRight className="w-4 h-4" />
          </Link>
          <h1 className="text-2xl font-extrabold text-[#0d2137]">وصفة طبية</h1>
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={() => window.print()}
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-accent-blue text-white hover:bg-blue-hover transition"
          >
            <Printer className="w-4 h-4" />
            طباعة
          </button>
          <button
            onClick={handleDelete}
            disabled={deleting}
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg border border-[#ef444430] text-[#ef4444] hover:bg-[#ef444418] transition disabled:opacity-50"
          >
            <Trash2 className="w-4 h-4" />
            حذف
          </button>
        </div>
      </div>

      {/* Prescription view (screen + print) */}
      <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card p-6 print:border-0 print:shadow-none print:p-0">
        <PrescriptionPrint prescription={prescription} />
      </div>
    </div>
  );
}
