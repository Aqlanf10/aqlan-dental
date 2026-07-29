
import { useEffect, useState } from "react";
import Link from "@/lib/nextLinkCompat";
import { AlertTriangle, ArrowRight, CheckCircle2, Database, FileSpreadsheet, Images, Loader2, LockKeyhole } from "lucide-react";
import api from "@/lib/api";

interface WebCephMigrationStatus {
  configured: boolean;
  partnerAgreementRequired: boolean;
  premiumRequired: boolean;
  requestLimitPerMinute: number;
  apiTransferable: string[];
  exportRequired: string[];
  notice: string;
}

export default function WebCephMigrationPage() {
  const [status, setStatus] = useState<WebCephMigrationStatus | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api.get<WebCephMigrationStatus>("/api/ceph/webceph-migration/status")
      .then((response) => setStatus(response.data))
      .catch(() => setError("تعذر قراءة حالة موصل WebCeph"));
  }, []);

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-extrabold text-gray-900">
            <Database className="h-6 w-6 text-cyan-700" />
            مركز ترحيل بيانات WebCeph
          </h1>
          <p className="mt-1 text-sm text-gray-500">نقل بيانات الحساب المملوكة للعيادة عبر القنوات الرسمية والقابلة للتدقيق.</p>
        </div>
        <Link href="/ceph" className="inline-flex items-center gap-2 self-start rounded-md border border-gray-200 px-3 py-2 text-xs font-medium text-gray-700 hover:bg-gray-50">
          <ArrowRight className="h-4 w-4" />
          العودة للسيفالو
        </Link>
      </header>

      {error ? (
        <div role="alert" className="flex items-center gap-2 rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800">
          <AlertTriangle className="h-4 w-4" />{error}
        </div>
      ) : !status ? (
        <div className="flex min-h-40 items-center justify-center text-sm text-gray-500"><Loader2 className="me-2 h-5 w-5 animate-spin" />جارٍ فحص الموصل...</div>
      ) : (
        <>
          <section className={`rounded-md border px-4 py-3 ${status.configured ? "border-emerald-200 bg-emerald-50 text-emerald-900" : "border-amber-200 bg-amber-50 text-amber-900"}`}>
            <div className="flex items-start gap-2">
              {status.configured ? <CheckCircle2 className="mt-0.5 h-5 w-5" /> : <LockKeyhole className="mt-0.5 h-5 w-5" />}
              <div><h2 className="text-sm font-bold">{status.configured ? "مفتاح الشريك مضبوط على الخادم" : "Partner API غير مهيأ بعد"}</h2><p className="mt-1 text-xs leading-6">{status.notice}</p></div>
            </div>
          </section>

          <div className="grid gap-4 md:grid-cols-2">
            <section className="rounded-md border border-gray-200 bg-white p-4">
              <div className="flex items-center gap-2"><Images className="h-5 w-5 text-blue-700" /><h2 className="text-base font-bold text-gray-900">المسار الرسمي عبر Partner API</h2></div>
              <p className="mt-2 text-xs leading-6 text-gray-600">ينقل المرضى والسجلات والصور بعد اتفاق الشراكة، عضوية Premium، وضبط المفتاح كسر خادمي. الحد المنشور هو {status.requestLimitPerMinute} طلبًا في الدقيقة.</p>
              <div className="mt-3 flex flex-wrap gap-2">
                {status.apiTransferable.map((item) => <span key={item} className="rounded bg-blue-50 px-2 py-1 text-[10px] font-bold text-blue-700">{item}</span>)}
              </div>
            </section>

            <section className="rounded-md border border-gray-200 bg-white p-4">
              <div className="flex items-center gap-2"><FileSpreadsheet className="h-5 w-5 text-cyan-700" /><h2 className="text-base font-bold text-gray-900">المسار السريري عبر التصدير</h2></div>
              <p className="mt-2 text-xs leading-6 text-gray-600">WebCeph لا يوفّر النقاط أو نتائج التحليل والبيانات التشخيصية عبر API. استيراد Landmark Table متاح الآن داخل كل تحليل جانبي بعد المعايرة وتحديد S.</p>
              <div className="mt-3 flex flex-wrap gap-2">
                {status.exportRequired.map((item) => <span key={item} className="rounded bg-cyan-50 px-2 py-1 text-[10px] font-bold text-cyan-700">{item}</span>)}
              </div>
            </section>
          </div>

          <section className="rounded-md border border-gray-200 bg-gray-50 px-4 py-3 text-xs leading-6 text-gray-700">
            لا تُستخدم جلسة المتصفح أو كلمة مرور WebCeph للترحيل. عند استلام WebCeph لعقد الشريك النهائي، يُضبط `WEBCEPH_PARTNER_API_KEY` في بيئة الخادم ويُنفّذ النقل مع معاينة، مطابقة مرضى، وتدقيق قبل أي كتابة.
          </section>

          <a href="https://webceph.com/en/api/" target="_blank" rel="noreferrer" className="inline-flex items-center gap-2 text-xs font-bold text-cyan-700 hover:underline">وثيقة WebCeph API الرسمية</a>
        </>
      )}
    </div>
  );
}
