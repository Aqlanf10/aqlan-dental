import Link from "next/link";
import { ArrowRight, CheckCircle2, FileText, ImageIcon, Ruler, ShieldCheck, Target, Upload } from "lucide-react";

const checks = [
  { title: "الصورة السيفالومترية", icon: ImageIcon, items: ["الصورة واضحة وغير مقلوبة", "الرأس في وضع جانبي صحيح", "الحواف التشريحية ظاهرة", "لا توجد قصّة تمنع تحديد النقاط"] },
  { title: "المعايرة", icon: Ruler, items: ["تمت معايرة المسطرة أو المقياس", "pixels/mm محفوظة", "لا يتم إصدار تقرير قبل المعايرة عند وجود قياسات خطية"] },
  { title: "النقاط Landmarks", icon: Target, items: ["تمت مراجعة 24 نقطة", "النقاط الناتجة من AI تعتبر مسودة فقط", "النقاط الصعبة راجعها الطبيب يدويًا", "لا يوجد اعتماد تلقائي"] },
  { title: "القياسات والتشخيص", icon: FileText, items: ["تم الضغط على حفظ وحساب", "القياسات غير قديمة بعد تعديل النقاط", "التشخيص السيفالومتري راجعه الطبيب", "المعايير المستخدمة مناسبة للحالة"] },
  { title: "التصدير والاعتماد", icon: Upload, items: ["PDF يصدر من بيانات محفوظة فقط", "VTO يفتح بعد حفظ المعالم", "التقرير لا يغني عن التشخيص السريري", "احتفظ بصورة المصدر بدون تعديل"] },
];

export default function CephQualityPage() {
  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-extrabold text-gray-900"><ShieldCheck className="h-6 w-6 text-clinic-blue" />فحص جودة السيفالو قبل الاعتماد</h1>
          <p className="mt-1 text-sm text-gray-500">قائمة عملية لضمان أن التحليل، التشخيص، PDF و VTO مبنية على بيانات محفوظة ومراجعة.</p>
        </div>
        <Link href="/ceph" className="inline-flex items-center gap-2 rounded-lg border border-gray-200 px-3 py-2 text-xs font-medium text-gray-700 hover:bg-gray-50"><ArrowRight className="h-4 w-4" />العودة للسيفالو</Link>
      </div>

      <div className="rounded-2xl border border-blue-100 bg-blue-50 p-4 text-sm leading-7 text-blue-900">
        <b>قاعدة 10/10:</b> لا تعتمد أي تقرير سيفالومتري إلا بعد صورة واضحة، معايرة صحيحة، نقاط مراجعة، حفظ وحساب، ثم مراجعة الطبيب للتشخيص والخطة.
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        {checks.map((section) => {
          const Icon = section.icon;
          return (
            <section key={section.title} className="rounded-2xl border border-gray-200 bg-white p-4 shadow-sm">
              <h2 className="flex items-center gap-2 text-base font-bold text-clinic-navy"><span className="grid h-9 w-9 place-items-center rounded-xl bg-clinic-blue-50 text-clinic-blue"><Icon className="h-5 w-5" /></span>{section.title}</h2>
              <ul className="mt-3 space-y-2">
                {section.items.map((item) => <li key={item} className="flex items-start gap-2 text-sm leading-6 text-gray-700"><CheckCircle2 className="mt-1 h-4 w-4 shrink-0 text-emerald-600" />{item}</li>)}
              </ul>
            </section>
          );
        })}
      </div>
    </div>
  );
}
