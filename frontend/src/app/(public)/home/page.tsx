"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import {
  Microscope,
  GraduationCap,
  ShieldCheck,
  Users,
  Activity,
  CheckCircle2,
  MessageCircle,
  Phone,
  Stethoscope,
  ClipboardList,
  ChevronLeft,
  ArrowLeft,
  Loader2,
} from "lucide-react";
import {
  SERVICES,
  FALLBACK_SETTINGS,
  DESIGN,
  type PublicDoctor,
} from "@/lib/public-constants";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "";

const TRUST_STRIP = [
  { icon: Microscope, label: "تخطيط رقمي متطور" },
  { icon: GraduationCap, label: "إشراف أكاديمي وبحثي" },
  { icon: ShieldCheck, label: "تعقيم وفق بروتوكولات دقيقة" },
  { icon: Users, label: "فريق متعدد التخصصات" },
];

const ABOUT_CARDS = [
  { icon: Activity, title: "تشخيص دقيق", subtitle: "تقنيات تشخيص متطورة", color: "#0284c7" },
  { icon: Users, title: "فريق متخصص", subtitle: "خبرة أكاديمية وسريرية", color: "#059669" },
  { icon: ShieldCheck, title: "تعقيم كامل", subtitle: "بروتوكولات سلامة دقيقة", color: "#7C3AED" },
  { icon: ClipboardList, title: "خطط علاج واضحة", subtitle: "متابعة مستمرة لكل حالة", color: "#FF8C00" },
];

// ─── Component ────────────────────────────────────────────────────────────────
export default function PublicHomePage() {
  const [s, setSettings] = useState<Record<string, string>>(FALLBACK_SETTINGS);
  const [doctors, setDoctors] = useState<PublicDoctor[]>([]);
  const [doctorsLoaded, setDoctorsLoaded] = useState(false);

  // Fetch website settings
  useEffect(() => {
    fetch(`${API_URL}/api/public/website-settings`, {
      method: "GET",
      headers: { "Accept-Language": "ar" },
    })
      .then((res) => {
        if (!res.ok) throw new Error("Failed");
        return res.json();
      })
      .then((data) => {
        const merged: Record<string, string> = { ...FALLBACK_SETTINGS };
        for (const key of Object.keys(FALLBACK_SETTINGS)) {
          merged[key] = data?.[key] ?? FALLBACK_SETTINGS[key];
        }
        setSettings(merged);
      })
      .catch(() => {
        // Fallback already set — homepage still works
      });
  }, []);

  // Fetch public doctors from API
  useEffect(() => {
    fetch(`${API_URL}/api/public/doctors`)
      .then((res) => (res.ok ? res.json() : []))
      .then((data) => {
        setDoctors(Array.isArray(data) ? data : []);
        setDoctorsLoaded(true);
      })
      .catch(() => {
        setDoctorsLoaded(true);
      });
  }, []);

  // Safe accessor with fallback
  const get = (key: string): string => s[key] ?? FALLBACK_SETTINGS[key] ?? "";

  // Resolve image URL: if relative (from backend /uploads), prepend API base
  const resolveImg = (url: string | null | undefined): string | null => {
    if (!url || url.trim() === "") return null;
    if (url.startsWith("http")) return url;
    return `${API_URL}${url}`;
  };

  const heroImgUrl = resolveImg(get("heroImageUrl"));

  return (
    <div dir="rtl">
      {/* ═══════════════════════════════════════ HERO ══════════════════════════════════════ */}
      <section className="relative text-white overflow-hidden" style={{ backgroundColor: DESIGN.dark }}>
        {/* Hero image overlay (if uploaded) */}
        {heroImgUrl && (
          <div
            className="absolute inset-0 bg-cover bg-center opacity-20"
            style={{ backgroundImage: `url(${heroImgUrl})` }}
          />
        )}
        {/* Subtle dot pattern */}
        <div
          className="absolute inset-0 opacity-5"
          style={{
            backgroundImage: "radial-gradient(circle, #87CEEB 1px, transparent 1px)",
            backgroundSize: "32px 32px",
          }}
        />
        {/* Gradient glow */}
        <div
          className="absolute top-0 right-0 w-96 h-96 rounded-full opacity-10 blur-3xl"
          style={{ background: "radial-gradient(circle, #87CEEB, transparent)" }}
        />
        <div
          className="absolute bottom-0 left-0 w-80 h-80 rounded-full opacity-10 blur-3xl"
          style={{ background: "radial-gradient(circle, #FF8C00, transparent)" }}
        />

        <div className="relative max-w-7xl mx-auto px-4 sm:px-6 py-20 md:py-28">
          {/* Badge */}
          <div className="flex justify-center md:justify-start mb-8">
            <div
              className="inline-flex items-center gap-2 px-4 py-1.5 rounded-full border text-sm"
              style={{ backgroundColor: "rgba(135,206,235,0.08)", borderColor: "rgba(135,206,235,0.25)", color: "#87CEEB" }}
            >
              <Activity className="w-4 h-4" />
              <span className="font-semibold tracking-wide">{get("marketingSlogan")}</span>
            </div>
          </div>

          {/* Headline */}
          <h1 className="text-4xl sm:text-5xl md:text-6xl font-extrabold leading-tight text-center md:text-right mb-6 max-w-3xl md:mx-0 mx-auto">
            {get("heroTitle")}
          </h1>

          <p
            className="text-base sm:text-lg md:text-xl leading-relaxed text-center md:text-right mb-10 max-w-2xl md:mx-0 mx-auto"
            style={{ color: DESIGN.muted }}
          >
            {get("heroSubtitle")}
          </p>

          {/* CTA Buttons */}
          <div className="flex flex-col sm:flex-row gap-3 justify-center md:justify-start mb-6">
            <Link
              href="/home/book"
              className="inline-flex items-center justify-center gap-2 px-8 py-4 rounded-2xl font-bold text-lg text-white shadow-xl transition-opacity hover:opacity-90"
              style={{ backgroundColor: DESIGN.accent }}
            >
              {get("bookingButtonText")}
              <ChevronLeft className="w-5 h-5" />
            </Link>
            <a
              href={`https://wa.me/${get("whatsapp")}`}
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex items-center justify-center gap-2 px-6 py-4 rounded-2xl font-semibold text-white border transition-colors hover:bg-white/10"
              style={{ borderColor: "rgba(255,255,255,0.15)", backgroundColor: "rgba(255,255,255,0.05)" }}
            >
              <MessageCircle className="w-5 h-5 text-green-400" />
              {get("whatsappButtonText")}
            </a>
            <a
              href={`tel:${get("phone").replace(/-/g, "")}`}
              className="inline-flex items-center justify-center gap-2 px-6 py-4 rounded-2xl font-medium transition-colors hover:bg-white/5"
              style={{ color: "rgba(255,255,255,0.65)", borderColor: "rgba(255,255,255,0.1)" }}
            >
              <Phone className="w-4 h-4" />
              {get("phone")}
            </a>
          </div>

          {/* Subtle portal link */}
          <div className="flex justify-center md:justify-start">
            <Link
              href="/portal/login"
              className="text-sm transition-colors hover:text-white"
              style={{ color: "rgba(135,206,235,0.6)" }}
            >
              بوابة المرضى ←
            </Link>
          </div>

          {/* Stats */}
          <div className="mt-16 grid grid-cols-2 md:grid-cols-4 gap-4">
            {[
              { number: `+${doctorsLoaded ? doctors.length : 5}`, label: "أطباء متخصصون" },
              { number: "+8", label: "سنوات خبرة" },
              { number: "+3000", label: "حالة علاجية" },
              { number: "6", label: "أيام عمل أسبوعياً" },
            ].map((stat) => (
              <div
                key={stat.label}
                className="rounded-2xl p-5 text-center"
                style={{ backgroundColor: "rgba(255,255,255,0.05)", border: "1px solid rgba(255,255,255,0.08)" }}
              >
                <div className="text-3xl font-extrabold mb-1" style={{ color: DESIGN.accent }}>
                  {stat.number}
                </div>
                <div className="text-sm" style={{ color: DESIGN.muted }}>
                  {stat.label}
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* ═══════════════════════════════════════ TRUST STRIP ═══════════════════════════════ */}
      <section className="bg-white border-b border-slate-100">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 py-10">
          <div className="grid grid-cols-2 md:grid-cols-4 gap-6">
            {TRUST_STRIP.map(({ icon: Icon, label }) => (
              <div key={label} className="flex flex-col sm:flex-row items-center gap-3 text-center sm:text-right">
                <div
                  className="w-12 h-12 rounded-2xl flex items-center justify-center flex-shrink-0"
                  style={{ backgroundColor: "rgba(135,206,235,0.12)" }}
                >
                  <Icon className="w-6 h-6" style={{ color: DESIGN.primaryLight }} />
                </div>
                <span className="text-sm font-bold text-slate-700 leading-tight">{label}</span>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* ═══════════════════════════════════════ ABOUT ══════════════════════════════════════ */}
      <section id="about" className="py-20 bg-white">
        <div className="max-w-7xl mx-auto px-4 sm:px-6">
          <div className="grid lg:grid-cols-2 gap-14 items-center">
            <div className="space-y-6">
              <div className="text-xs font-bold uppercase tracking-widest" style={{ color: DESIGN.primaryLight }}>
                عن المركز
              </div>
              <h2 className="text-3xl sm:text-4xl font-extrabold text-slate-900 leading-tight">
                رعاية تضع المريض أولاً، مع خبرة أكاديمية وتقنيات حديثة
              </h2>
              <div className="w-16 h-1.5 rounded-full" style={{ backgroundColor: DESIGN.primaryLight }} />
              <p className="text-lg text-slate-500 leading-relaxed">
                {get("aboutText")}
              </p>
              <div className="grid grid-cols-2 gap-3 pt-2">
                {["تشخيص دقيق", "متابعة دورية", "فريق متعدد التخصصات", "خطط علاج واضحة"].map((item) => (
                  <div key={item} className="flex items-center gap-2 text-sm font-semibold text-slate-700">
                    <CheckCircle2 className="w-4 h-4 flex-shrink-0" style={{ color: DESIGN.primaryLight }} />
                    {item}
                  </div>
                ))}
              </div>
            </div>

            {/* Visual cards */}
            <div className="grid grid-cols-2 gap-4">
              {ABOUT_CARDS.map(({ icon: Icon, title, subtitle, color }) => (
                <div
                  key={title}
                  className="rounded-3xl p-6 flex flex-col items-center text-center gap-3 shadow-sm border border-slate-100 hover:shadow-md transition-shadow"
                  style={{ backgroundColor: `${color}0a` }}
                >
                  <div
                    className="w-14 h-14 rounded-2xl flex items-center justify-center"
                    style={{ backgroundColor: `${color}18` }}
                  >
                    <Icon className="w-7 h-7" style={{ color }} />
                  </div>
                  <div>
                    <div className="font-bold text-slate-800 text-sm">{title}</div>
                    <div className="text-xs text-slate-500 mt-0.5">{subtitle}</div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </section>

      {/* ═══════════════════════════════════════ SERVICES ════════════════════════════════════ */}
      <section id="services" style={{ backgroundColor: DESIGN.surface }} className="py-20">
        <div className="max-w-7xl mx-auto px-4 sm:px-6">
          <div className="text-center mb-14 space-y-4">
            <div className="text-xs font-bold uppercase tracking-widest" style={{ color: DESIGN.primaryLight }}>
              خدماتنا التخصصية
            </div>
            <h2 className="text-3xl sm:text-4xl font-extrabold text-slate-900">
              {get("servicesSectionTitle")}
            </h2>
            <div className="w-16 h-1.5 rounded-full mx-auto" style={{ backgroundColor: DESIGN.primaryLight }} />
          </div>

          <div className="grid sm:grid-cols-2 lg:grid-cols-4 gap-6">
            {SERVICES.map(({ icon: Icon, title, desc }) => (
              <Link
                key={title}
                href={`/home/book?serviceType=${encodeURIComponent(title)}`}
                className="bg-white rounded-3xl p-6 border border-slate-100 hover:border-slate-200 hover:shadow-lg transition-all duration-300 text-right group block"
              >
                <div
                  className="w-14 h-14 rounded-2xl flex items-center justify-center mb-5"
                  style={{ backgroundColor: "rgba(135,206,235,0.12)" }}
                >
                  <Icon className="w-7 h-7 group-hover:scale-110 transition-transform duration-200" style={{ color: DESIGN.primaryLight }} />
                </div>
                <h3 className="text-base font-bold text-slate-900 mb-2 group-hover:text-[#0284c7] transition-colors">
                  {title}
                </h3>
                <p className="text-slate-500 text-sm leading-relaxed">{desc}</p>
              </Link>
            ))}
          </div>

          {/* View all services */}
          <div className="mt-10 text-center">
            <Link
              href="/home/services"
              className="inline-flex items-center gap-2 px-6 py-3 rounded-xl font-bold text-sm border-2 transition-colors hover:bg-slate-50"
              style={{ borderColor: DESIGN.primaryLight, color: DESIGN.primary }}
            >
              عرض كل الخدمات
              <ArrowLeft className="w-4 h-4" />
            </Link>
          </div>
        </div>
      </section>

      {/* ═══════════════════════════════════════ TEAM ════════════════════════════════════════ */}
      <section id="team" className="py-20 bg-white">
        <div className="max-w-7xl mx-auto px-4 sm:px-6">
          <div className="text-center mb-14 space-y-4">
            <div className="text-xs font-bold uppercase tracking-widest" style={{ color: DESIGN.primaryLight }}>
              الفريق الطبي
            </div>
            <h2 className="text-3xl sm:text-4xl font-extrabold text-slate-900">
              نخبة من الأطباء المتخصصين
            </h2>
            <div className="w-16 h-1.5 rounded-full mx-auto" style={{ backgroundColor: DESIGN.primaryLight }} />
          </div>

          {!doctorsLoaded ? (
            <div className="flex justify-center py-8">
              <Loader2 className="w-6 h-6 animate-spin text-slate-400" />
            </div>
          ) : doctors.length > 0 ? (
            <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-6">
              {doctors.slice(0, 6).map((doctor) => (
                <div
                  key={doctor.id}
                  className="bg-white rounded-3xl border border-slate-100 hover:shadow-lg transition-all duration-300 overflow-hidden"
                  style={{ borderTop: `3px solid ${doctor.color}` }}
                >
                  <div className="p-6 flex items-start gap-4">
                    <div
                      className="w-14 h-14 rounded-2xl flex items-center justify-center text-white font-bold text-lg flex-shrink-0"
                      style={{ backgroundColor: doctor.color }}
                    >
                      {doctor.avatarInitials}
                    </div>
                    <div className="min-w-0 flex-1">
                      <div className="font-bold text-slate-900 text-base">{doctor.name}</div>
                      <div className="text-sm font-semibold mt-0.5 mb-3" style={{ color: doctor.color }}>
                        {doctor.specialty}
                      </div>
                      <Link
                        href={`/home/book?doctorId=${doctor.id}`}
                        className="inline-flex items-center gap-1.5 text-xs font-bold px-4 py-2 rounded-lg text-white transition-opacity hover:opacity-90"
                        style={{ backgroundColor: DESIGN.accent }}
                      >
                        احجز مع الطبيب
                        <ChevronLeft className="w-3.5 h-3.5" />
                      </Link>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <p className="text-center text-slate-400">لا يوجد أطباء متاحون حالياً.</p>
          )}

          {/* View all doctors */}
          {doctors.length > 0 && (
            <div className="mt-10 text-center">
              <Link
                href="/home/doctors"
                className="inline-flex items-center gap-2 px-6 py-3 rounded-xl font-bold text-sm border-2 transition-colors hover:bg-slate-50"
                style={{ borderColor: DESIGN.primaryLight, color: DESIGN.primary }}
              >
                عرض كل الأطباء
                <ArrowLeft className="w-4 h-4" />
              </Link>
            </div>
          )}
        </div>
      </section>

      {/* ═══════════════════════════════════════ CTA ═════════════════════════════════════════ */}
      <section
        className="py-20 text-white"
        style={{ backgroundColor: DESIGN.dark }}
      >
        <div className="max-w-3xl mx-auto px-4 sm:px-6 text-center">
          <div
            className="w-16 h-16 rounded-3xl flex items-center justify-center mx-auto mb-6"
            style={{ backgroundColor: "rgba(135,206,235,0.12)" }}
          >
            <Stethoscope className="w-8 h-8" style={{ color: DESIGN.primaryLight }} />
          </div>
          <h2 className="text-3xl sm:text-4xl font-extrabold mb-4 leading-tight">
            جاهز تبدأ رحلتك نحو ابتسامة أجمل؟
          </h2>
          <p className="text-lg mb-3 leading-relaxed" style={{ color: DESIGN.muted }}>
            احجز موعدك الآن، وسيقوم فريق المركز بالتواصل معك لتأكيد الموعد وترتيب زيارتك.
          </p>
          <div className="flex items-center justify-center gap-2 text-sm mb-10" style={{ color: "#64748b" }}>
            <Activity className="w-4 h-4" style={{ color: DESIGN.primaryLight }} />
            {get("address")}
            <span className="mx-2">·</span>
            {get("workingHours")}
          </div>
          <div className="flex flex-col sm:flex-row gap-4 justify-center">
            <Link
              href="/home/book"
              className="inline-flex items-center justify-center gap-2 px-8 py-4 rounded-2xl font-bold text-lg text-white shadow-xl transition-opacity hover:opacity-90"
              style={{ backgroundColor: DESIGN.accent }}
            >
              {get("bookingButtonText")}
              <ChevronLeft className="w-5 h-5" />
            </Link>
            <a
              href={`https://wa.me/${get("whatsapp")}`}
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex items-center justify-center gap-2 px-8 py-4 rounded-2xl font-semibold text-white border transition-colors hover:bg-white/10"
              style={{ borderColor: "rgba(255,255,255,0.15)", backgroundColor: "rgba(255,255,255,0.05)" }}
            >
              <MessageCircle className="w-5 h-5 text-green-400" />
              {get("whatsappButtonText")}
            </a>
          </div>
        </div>
      </section>
    </div>
  );
}
