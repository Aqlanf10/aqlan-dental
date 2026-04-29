"use client";

import { Sparkles, ArrowRight, BarChart3, Info } from "lucide-react";
import Link from "next/link";

export default function VtoPage() {
  const vtoFeatures = [
    {
      id: "ceph-simulate",
      title: "محاكاة السيفالومتري",
      description: "تحليل سيفالومتري تفاعلي مع محاكاة تحريك الأسنان والفكين ورؤية التأثير على القياسات السيفالومترية في الوقت الفعلي",
      icon: BarChart3,
      href: "/ceph",
      color: "bg-blue-50 text-blue-600 border-blue-200",
    },
    {
      id: "ai-vto",
      title: "VTO بالذكاء الاصطناعي",
      description: "اقتراحات تلقائية لمحاكاة العلاج بناءً على بيانات الحالة والتحليل السيفالومتري باستخدام الذكاء الاصطناعي",
      icon: Sparkles,
      href: "/ortho",
      color: "bg-purple-50 text-purple-600 border-purple-200",
    },
    {
      id: "treatment-plans",
      title: "مقارنة خطط العلاج",
      description: "إنشاء خطط علاج بديلة (أ و ب) ومقارنة النتائج المتوقعة لكل خطة مع اختيار الخطة الأمثل",
      icon: ArrowRight,
      href: "/ortho",
      color: "bg-green-50 text-green-600 border-green-200",
    },
  ];

  return (
    <div className="space-y-8">
      <div>
        <h2 className="text-2xl font-extrabold text-[#0d2137]">VTO — محاكاة العلاج</h2>
        <p className="text-sm text-gray-500 mt-1">
          Visual Treatment Objective — أداة محاكاة نتائج العلاج التقويمي قبل البدء
        </p>
      </div>

      <div className="bg-[#f0f5fb] border border-[#dce8f5] rounded-xl p-5">
        <div className="flex items-start gap-3">
          <Info className="w-5 h-5 text-[#3d7ab5] flex-shrink-0 mt-0.5" />
          <div>
            <p className="text-sm font-bold text-[#0d2137] mb-1">ما هو VTO؟</p>
            <p className="text-sm text-gray-600 leading-relaxed">
              VTO (Visual Treatment Objective) هي أداة تنبؤية تسمح بتخيل شكل الأسنان والفكين بعد العلاج التقويمي. يمكنك محاكاة تحريك الأسنان، سحب الفك، وتغيير زوايا الميلان لرؤية التأثير على الوجه والابتسامة. تساعد هذه الأداة في اختيار خطة العلاج الأمثل وإشراك المريض في اتخاذ القرار.
            </p>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-5">
        {vtoFeatures.map((feature) => (
          <Link key={feature.id} href={feature.href} className="block group">
            <div className={`rounded-2xl border p-6 transition-all hover:shadow-lg ${feature.color}`}>
              <feature.icon className="w-10 h-10 mb-4" />
              <h3 className="text-lg font-extrabold text-[#0d2137] mb-2 group-hover:text-[#3d7ab5] transition-colors">
                {feature.title}
              </h3>
              <p className="text-sm text-gray-600 leading-relaxed">{feature.description}</p>
              <div className="flex items-center gap-1 mt-4 text-sm font-bold text-[#3d7ab5] group-hover:gap-2 transition-all">
                <span>فتح</span>
                <ArrowRight className="w-4 h-4" />
              </div>
            </div>
          </Link>
        ))}
      </div>

      <div className="bg-white border border-[#e8f0f9] rounded-2xl p-6">
        <h3 className="text-lg font-extrabold text-[#0d2137] mb-4">حالات التقويم النشطة</h3>
        <p className="text-sm text-gray-500 mb-4">اختر حالة تقويم لبدء محاكاة VTO.</p>
        <div className="flex items-center gap-3">
          <Link href="/ortho" className="flex items-center gap-2 px-5 py-2.5 bg-[#3d7ab5] text-white rounded-xl text-sm font-bold hover:bg-[#2d5e8e] transition-colors">
            <Sparkles className="w-4 h-4" />عرض حالات التقويم
          </Link>
          <Link href="/ceph" className="flex items-center gap-2 px-5 py-2.5 bg-white border border-[#3d7ab5] text-[#3d7ab5] rounded-xl text-sm font-bold hover:bg-[#f0f5fb] transition-colors">
            <BarChart3 className="w-4 h-4" />التحليل السيفالومتري
          </Link>
        </div>
      </div>

      <div className="bg-gradient-to-br from-[#0d2137] to-[#1a3a5c] rounded-2xl p-8 text-white">
        <h3 className="text-lg font-extrabold mb-6">خطوات عمل VTO</h3>
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
          {[
            { step: 1, title: "تحليل سيفالومتري", desc: "رفع صورة سيفالومتري وتحديد المعالم التشريحية" },
            { step: 2, title: "التشخيص", desc: "تحليل القياسات وتحديد المشاكل التقويمية" },
            { step: 3, title: "محاكاة العلاج", desc: "تحريك الأسنان والفكين ورؤية التأثير المتوقع" },
            { step: 4, title: "اختيار الخطة", desc: "مقارنة خطط العلاج واختيار الأفضل" },
          ].map((item) => (
            <div key={item.step} className="text-center">
              <div className="w-12 h-12 rounded-full bg-white/10 border border-white/20 flex items-center justify-center mx-auto mb-3">
                <span className="text-lg font-extrabold">{item.step}</span>
              </div>
              <p className="text-sm font-bold mb-1">{item.title}</p>
              <p className="text-xs text-white/60">{item.desc}</p>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
