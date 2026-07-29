import { Suspense } from "react";
import { useSearchParams } from "@/lib/nextNavCompat";
import Link from "@/lib/nextLinkCompat";
import { ArrowRight } from "lucide-react";
import { RadiologyOrderForm } from "@/components/radiology/RadiologyOrderForm";

function NewRadiologyOrderContent() {
  const params = useSearchParams();
  const patientId   = params.get("patientId")   ?? undefined;
  const patientName = params.get("patientName") ?? undefined;

  return (
    <div className="space-y-5 max-w-3xl">
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Link href="/prescriptions" className="hover:text-clinic-blue transition">الوصفات</Link>
        <span>/</span>
        <span className="text-gray-900 font-medium">طلب أشعة جديد</span>
      </div>
      <div className="flex items-center gap-3">
        <Link href="/prescriptions" className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 transition text-gray-500">
          <ArrowRight className="w-4 h-4" />
        </Link>
        <div>
          <h1 className="text-2xl font-extrabold text-gray-900">طلب أشعة خارجي</h1>
          <p className="text-sm text-gray-500 mt-0.5">تُطبع الإحالة بالإنجليزية ويأخذها المريض إلى مركز الأشعة</p>
        </div>
      </div>
      <RadiologyOrderForm defaultPatientId={patientId} defaultPatientName={patientName} />
    </div>
  );
}

export default function NewRadiologyOrderPage() {
  return (
    <Suspense>
      <NewRadiologyOrderContent />
    </Suspense>
  );
}
