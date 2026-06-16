"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { ArrowRight, CheckCircle2, FileText, ImageIcon, Loader2, Ruler, ShieldAlert, Target } from "lucide-react";
import api from "@/lib/api";
import type { CephAnalysis } from "@/types/ceph";
import { ANALYSIS_TYPE_AR } from "@/types/ceph";
import { cn, formatArabicDate } from "@/lib/utils";

function Row({ label, ok, detail }: { label: string; ok: boolean; detail: string }) {
  return (
    <li className={cn("rounded-xl border p-3", ok ? "border-emerald-100 bg-emerald-50" : "border-amber-100 bg-amber-50")}>
      <div className="flex items-start gap-2">
        <CheckCircle2 className={cn("mt-1 h-4 w-4 shrink-0", ok ? "text-emerald-700" : "text-amber-700")} />
        <div>
          <div className={cn("text-sm font-bold", ok ? "text-emerald-800" : "text-amber-800")}>{label}</div>
          <div className="mt-1 text-xs leading-5 text-gray-600">{detail}</div>
        </div>
      </div>
    </li>
  );
}

export default function CephAnalysisQualityPage() {
  const { id } = useParams<{ id: string }>();
  const [analysis, setAnalysis] = useState<CephAnalysis | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<CephAnalysis>(`/api/ceph/${id}`)
      .then((res) => setAnalysis(res.data))
      .catch(() => setAnalysis(null))
      .finally(() => setLoading(false));
  }, [id]);

  const checks = useMemo(() => {
    if (!analysis) return [];
    return [
      { label: "الصورة موجودة", ok: Boolean(analysis.xrayFileUrl), detail: "يجب رفع صورة سيفالومترية واضحة قبل أي تحليل." },
      { label: "المعايرة محفوظة", ok: Boolean(analysis.pixelsPerMm && analysis.pixelsPerMm > 0), detail: "القياسات الخطية و PDF لا تكون موثوقة بدون معايرة صحيحة." },
      { label: "النقاط مكتملة", ok: (analysis.landmarks?.length ?? 0) >= 24, detail: `${analysis.landmarks?.length ?? 0}/24 نقطة. راجع نقاط AI يدويًا قبل الحفظ.` },
      { label: "القياسات محسوبة", ok: (analysis.measurements?.length ?? 0) > 0, detail: `${analysis.measurements?.length ?? 0} قياس محفوظ. اضغط حفظ وحساب بعد أي تعديل.` },
      { label: "التشخيص مراجع", ok: Boolean(analysis.diagnosis?.finalDiagnosis), detail: "التشخيص السيفالومتري مساند فقط ولا يغني عن التشخيص السريري الكامل." },
      { label: "اعتماد الطبيب", ok: Boolean(analysis.diagnosis?.doctorApproved), detail: "لا تعتمد تقرير أو VTO تعليمي كقرار علاجي نهائي بدون اعتماد الطبيب." },
    ];
  }, [analysis]);

  const passed = checks.filter((item) => item.ok).length;
  const ready = checks.length > 0 && passed === checks.length;

  if (loading) return <div className="grid h-64 place-items-center"><Loader2 className="h-8 w-8 animate-spin text-clinic-blue" /></div>;
  if (!analysis) return <div className="py-20 text-center text-gray-400">التحليل غير موجود</div>;

  return (
    <div className="mx-auto max-w-5xl space-y-5" dir="rtl">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-extrabold text-gray-900"><ShieldAlert className="h-6 w-6 text-clinic-blue" />فحص جودة تحليل السيفالو</h1>
          <p className="mt-1 text-sm text-gray-500">{analysis.patientName} · {ANALYSIS_TYPE_AR[analysis.analysisType] ?? analysis.analysisType} · {formatArabicDate(analysis.analysisDate)}</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Link href={`/ceph/${id}`} className="inline-flex items-center gap-2 rounded-lg bg-clinic-blue px-3 py-2 text-xs font-bold text-white"><ArrowRight className="h-4 w-4" />فتح التحليل</Link>
          <Link href="/ceph" className="inline-flex items-center gap-2 rounded-lg border border-gray-200 px-3 py-2 text-xs font-medium text-gray-700 hover:bg-gray-50">قائمة السيفالو</Link>
        </div>
      </div>

      <div className={cn("rounded-2xl border p-4", ready ? "border-emerald-200 bg-emerald-50" : "border-amber-200 bg-amber-50")}>
        <div className="flex flex-wrap items-center gap-3 text-sm">
          <span className="font-extrabold">الجاهزية: {passed}/{checks.length}</span>
          <span>{ready ? "جاهز للتقرير بعد مراجعة الطبيب" : "توجد نقاط تحتاج إكمال قبل الاعتماد"}</span>
        </div>
      </div>

      <div className="grid gap-3 md:grid-cols-2">
        <Row label="الصورة والمعايرة" ok={Boolean(analysis.xrayFileUrl && analysis.pixelsPerMm)} detail="مصدر القياسات يبدأ بصورة واضحة ومعايرة صحيحة." />
        <Row label="المعالم والقياسات" ok={(analysis.landmarks?.length ?? 0) >= 24 && (analysis.measurements?.length ?? 0) > 0} detail="ضع 24 نقطة ثم احفظ واحسب قبل PDF أو VTO." />
        <Row label="PDF" ok={(analysis.measurements?.length ?? 0) > 0} detail="التقرير يجب أن يصدر من قياسات محفوظة، لا من تعديلات غير محفوظة." />
        <Row label="VTO" ok={(analysis.landmarks?.length ?? 0) >= 24 && !analysis.aiAssisted} detail="إذا كانت النقاط AI-assisted فراجعها يدويًا قبل استخدام VTO كمرجع." />
      </div>

      <section className="rounded-2xl border border-gray-200 bg-white p-4 shadow-sm">
        <h2 className="mb-3 flex items-center gap-2 text-base font-bold text-clinic-navy"><Target className="h-5 w-5 text-clinic-blue" />Checklist تفصيلي</h2>
        <ul className="grid gap-2 md:grid-cols-2">
          {checks.map((item) => <Row key={item.label} {...item} />)}
        </ul>
      </section>

      <div className="grid gap-3 md:grid-cols-3">
        <div className="rounded-xl border border-gray-200 bg-white p-3 text-xs leading-6 text-gray-600"><ImageIcon className="mb-2 h-5 w-5 text-clinic-blue" />الصورة الأصلية تبقى محفوظة ولا تُعدّل تشخيصيًا.</div>
        <div className="rounded-xl border border-gray-200 bg-white p-3 text-xs leading-6 text-gray-600"><Ruler className="mb-2 h-5 w-5 text-clinic-blue" />المعايرة شرط للقياسات الخطية الدقيقة.</div>
        <div className="rounded-xl border border-gray-200 bg-white p-3 text-xs leading-6 text-gray-600"><FileText className="mb-2 h-5 w-5 text-clinic-blue" />التقرير السيفالومتري جزء من ملف الحالة وليس بديلًا للفحص السريري.</div>
      </div>
    </div>
  );
}
