"use client";

import Link from "next/link";
import { ChevronRight, CalendarDays, Sparkles } from "lucide-react";
import { SERVICES, DESIGN } from "@/lib/public-constants";

export default function ServicesPage() {
  return (
    <div dir="rtl">
      {/* ─── Hero ─── */}
      <section className="relative text-white overflow-hidden" style={{ backgroundColor: DESIGN.dark }}>
        {/* Dot pattern */}
        <div
          className="absolute inset-0 opacity-5"
          style={{
            backgroundImage: "radial-gradient(circle, #87CEEB 1px, transparent 1px)",
            backgroundSize: "32px 32px",
          }}
        />
        {/* Glow */}
        <div
          className="absolute top-0 left-0 w-80 h-80 rounded-full opacity-10 blur-3xl"
          style={{ background: "radial-gradient(circle, #FF8C00, transparent)" }}
        />

        <div className="relative max-w-4xl mx-auto px-4 sm:px-6 py-16 md:py-20 text-center">
          <div className="flex justify-center mb-6">
            <div
              className="w-16 h-16 rounded-2xl flex items-center justify-center"
              style={{ backgroundColor: "rgba(135,206,235,0.12)" }}
            >
              <Sparkles className="w-8 h-8" style={{ color: DESIGN.primaryLight }} />
            </div>
          </div>
          <h1 className="text-3xl sm:text-4xl md:text-5xl font-extrabold mb-4">
            خدماتنا
          </h1>
          <p className="text-base sm:text-lg max-w-2xl mx-auto leading-relaxed" style={{ color: DESIGN.muted }}>
            رعاية متكاملة لصحة وجمال ابتسامتك
          </p>
          <Link
            href="/home/book"
            className="mt-8 inline-flex items-center justify-center gap-2 px-8 py-4 rounded-2xl font-bold text-lg text-white shadow-xl transition-opacity hover:opacity-90"
            style={{ backgroundColor: DESIGN.accent }}
          >
            احجز موعدك الآن
            <CalendarDays className="w-5 h-5" />
          </Link>
        </div>
      </section>

      {/* ─── Services Grid ─── */}
      <section style={{ backgroundColor: DESIGN.surface }} className="py-16 md:py-20">
        <div className="max-w-7xl mx-auto px-4 sm:px-6">
          <div className="grid sm:grid-cols-2 lg:grid-cols-4 gap-6">
            {SERVICES.map(({ icon: Icon, title, desc }) => (
              <div
                key={title}
                className="bg-white rounded-3xl p-6 border border-slate-100 hover:border-slate-200 hover:shadow-lg transition-all duration-300 text-right group flex flex-col"
              >
                <div
                  className="w-14 h-14 rounded-2xl flex items-center justify-center mb-5"
                  style={{ backgroundColor: "rgba(135,206,235,0.12)" }}
                >
                  <Icon
                    className="w-7 h-7 group-hover:scale-110 transition-transform duration-200"
                    style={{ color: DESIGN.primaryLight }}
                  />
                </div>
                <h3 className="text-base font-bold text-slate-900 mb-2 group-hover:text-[#0284c7] transition-colors">
                  {title}
                </h3>
                <p className="text-slate-500 text-sm leading-relaxed mb-5 flex-1">
                  {desc}
                </p>
                <Link
                  href={`/home/book?serviceType=${encodeURIComponent(title)}`}
                  className="inline-flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl font-bold text-white text-sm transition-opacity hover:opacity-90 shadow-sm w-full"
                  style={{ backgroundColor: DESIGN.accent }}
                >
                  احجز الآن
                  <CalendarDays className="w-4 h-4" />
                </Link>
              </div>
            ))}
          </div>

          {/* Back link */}
          <div className="mt-12 text-center">
            <Link
              href="/home"
              className="inline-flex items-center gap-1.5 text-sm font-semibold transition-colors hover:text-slate-900"
              style={{ color: DESIGN.primary }}
            >
              <ChevronRight className="w-4 h-4" />
              العودة للرئيسية
            </Link>
          </div>
        </div>
      </section>
    </div>
  );
}
