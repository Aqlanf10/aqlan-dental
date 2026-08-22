"use client";
import { printScreen } from "@/lib/printUtils";
import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { ArrowRight, Printer, Trash2 } from "lucide-react";
import type { RadiologyOrder } from "@/types/radiologyOrder";
import { studyTypeLabelAr } from "@/types/radiologyOrder";
import api from "@/lib/api";
import { RadiologyReferralPrint } from "@/components/radiology/RadiologyReferralPrint";
import { useClinicBranding, printIdentity } from "@/hooks/useClinicBranding";

export default function RadiologyOrderDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [order, setOrder] = useState<RadiologyOrder | null>(null);
  const branding = useClinicBranding();
  // CORE-REQ-006: identity in the language the clinic selected for printed forms.
  const print = printIdentity(branding);
  const [loading, setLoading] = useState(true);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    api.get<RadiologyOrder>(`/api/radiology-orders/${id}`)
      .then((r) => setOrder(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [id]);

  const handleDelete = async () => {
    if (!confirm("هل أنت متأكد من حذف طلب الأشعة؟")) return;
    setDeleting(true);
    try {
      await api.delete(`/api/radiology-orders/${id}`);
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

  if (!order) {
    return <div className="text-center py-20 text-gray-400 text-sm">طلب الأشعة غير موجود</div>;
  }

  return (
    <div className="space-y-5 max-w-2xl">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-gray-500 print:hidden">
        <Link href="/prescriptions" className="hover:text-clinic-blue transition">الوصفات</Link>
        <span>/</span>
        <Link href={`/patients/${order.patientId}`} className="hover:text-clinic-blue transition">
          {order.patientName}
        </Link>
        <span>/</span>
        <span className="text-gray-900 font-medium">{studyTypeLabelAr(order.studyType)}</span>
      </div>

      {/* Toolbar */}
      <div className="flex items-center justify-between print:hidden">
        <div className="flex items-center gap-3">
          <Link href="/prescriptions" className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 transition text-gray-500">
            <ArrowRight className="w-4 h-4" />
          </Link>
          <h1 className="text-2xl font-extrabold text-gray-900">إحالة أشعة</h1>
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

      {/* Referral view (screen + print) */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-6 print:border-0 print:shadow-none print:p-0">
        <RadiologyReferralPrint
          order={order}
          clinicName={print.clinicName}
          clinicAddress={print.clinicAddress}
          clinicPhone={branding.phone}
          leadDoctor={print.leadDoctor}
          leadDoctorCredentials={print.leadDoctorCredentials}
        />
      </div>
    </div>
  );
}
