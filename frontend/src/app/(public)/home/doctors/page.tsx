"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import {
  ChevronRight,
  Loader2,
  AlertCircle,
  CalendarDays,
  Users,
} from "lucide-react";
import type { PublicDoctor } from "@/lib/public-constants";
import { DESIGN } from "@/lib/public-constants";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "";

export default function DoctorsPage() {
  const [doctors, setDoctors] = useState<PublicDoctor[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    async function fetchDoctors() {
      try {
        const res = await fetch(`${API_URL}/api/public/doctors`);
        if (!res.ok) throw new Error("فشل تحميل قائمة الأطباء");
        const data = await res.json();
        setDoctors(Array.isArray(data) ? data : []);
      } catch {
        setError("تعذّر تحميل قائمة الأطباء. يرجى المحاولة لاحقاً.");
      } finally {
        setLoading(false);
      }
    }
    fetchDoctors();
  }, []);

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
          className="absolute top-0 right-0 w-80 h-80 rounded-full opacity-10 blur-3xl"
          style={{ background: "radial-gradient(circle, #87CEEB, transparent)" }}
        />

        <div className="relative max-w-4xl mx-auto px-4 sm:px-6 py-16 md:py-20 text-center">
          <div className="flex justify-center mb-6">
            <div
              className="w-16 h-16 rounded-2xl flex items-center justify-center"
              style={{ backgroundColor: "rgba(135,206,235,0.12)" }}
            >
              <Users className="w-8 h-8" style={{ color: DESIGN.primaryLight }} />
            </div>
          </div>
          <h1 className="text-3xl sm:text-4xl md:text-5xl font-extrabold mb-4">
            فريقنا الطبي
          </h1>
          <p className="text-base sm:text-lg max-w-2xl mx-auto leading-relaxed" style={{ color: DESIGN.muted }}>
            نخبة من الأطباء لخدمتكم في تقويم وزراعة وتجميل الأسنان
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

      {/* ─── Doctors Grid ─── */}
      <section style={{ backgroundColor: DESIGN.surface }} className="py-16 md:py-20">
        <div className="max-w-7xl mx-auto px-4 sm:px-6">
          {loading && (
            <div className="flex flex-col items-center justify-center py-20 gap-3">
              <Loader2 className="w-8 h-8 animate-spin" style={{ color: DESIGN.primaryLight }} />
              <span className="text-slate-500 font-semibold">جاري تحميل قائمة الأطباء...</span>
            </div>
          )}

          {error && !loading && (
            <div className="max-w-md mx-auto bg-amber-50 border border-amber-200 rounded-2xl p-6 text-center">
              <AlertCircle className="w-8 h-8 text-amber-500 mx-auto mb-3" />
              <p className="text-amber-700 font-semibold">{error}</p>
            </div>
          )}

          {!loading && !error && doctors.length === 0 && (
            <div className="max-w-md mx-auto text-center py-16">
              <Users className="w-12 h-12 text-slate-300 mx-auto mb-4" />
              <p className="text-slate-500">لا يوجد أطباء متاحون حالياً.</p>
            </div>
          )}

          {!loading && !error && doctors.length > 0 && (
            <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-6">
              {doctors.map((doctor) => (
                <div
                  key={doctor.id}
                  className="bg-white rounded-3xl border border-slate-100 hover:shadow-lg transition-all duration-300 overflow-hidden"
                  style={{ borderTop: `4px solid ${doctor.color}` }}
                >
                  {/* Card body */}
                  <div className="p-6 flex flex-col items-center text-center">
                    {/* Avatar */}
                    <div
                      className="w-20 h-20 rounded-2xl flex items-center justify-center text-white font-bold text-2xl mb-5 shadow-md"
                      style={{ backgroundColor: doctor.color }}
                    >
                      {doctor.avatarInitials}
                    </div>
                    {/* Name */}
                    <h3 className="text-lg font-extrabold text-slate-900 mb-1">
                      {doctor.name}
                    </h3>
                    {/* Specialty */}
                    <p
                      className="text-sm font-bold mb-4"
                      style={{ color: doctor.color }}
                    >
                      {doctor.specialty}
                    </p>
                    {/* Booking CTA */}
                    <Link
                      href={`/home/book?doctorId=${doctor.id}`}
                      className="w-full inline-flex items-center justify-center gap-2 px-5 py-3 rounded-xl font-bold text-white text-sm transition-opacity hover:opacity-90 shadow-sm"
                      style={{ backgroundColor: DESIGN.accent }}
                    >
                      احجز مع الطبيب
                      <CalendarDays className="w-4 h-4" />
                    </Link>
                  </div>
                </div>
              ))}
            </div>
          )}

          {/* Back link */}
          {!loading && (
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
          )}
        </div>
      </section>
    </div>
  );
}
