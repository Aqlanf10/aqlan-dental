"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import {
  Smile, Sparkles, Zap, Award, Heart, ShieldCheck, ClipboardList,
  CheckCircle2, MessageCircle, Phone, Activity, Microscope,
  GraduationCap, Users, Stethoscope, Scissors,
  Plus, MapPin, ChevronLeft,
} from "lucide-react";

// ─── Fallback defaults (used if API fails) ───────────────────────────────────
const FALLBACK: Record<string, string> = {
  clinicName: "مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان",
  heroTitle: "ابتسامة تجمع بين دقة العلم ولمسة الفن",
  heroSubtitle: "مركز الدكتور عقلان الكامل يقدم رعاية متكاملة في تقويم وزراعة وتجميل الأسنان، مع تشخيص دقيق وخطط علاج واضحة ومتابعة مستمرة لكل حالة.",
  marketingSlogan: "قيادة طبية… وابتسامة بثقة",
  aboutText: "يقدم مركز الدكتور عقلان الكامل خدمات تخصصية شاملة في تقويم وزراعة وتجميل الأسنان، معتمدين على تشخيص دقيق، وخطط علاج واضحة، ومتابعة مستمرة للحالات للمساعدة في الوصول إلى نتائج علاجية دقيقة ومناسبة لكل حالة.",
  phone: "04-253028",
  whatsapp: "967770245745",
  address: "تعز، اليمن — شارع التحرير الأعلى",
  workingHours: "السبت – الخميس: 8 ص – 8 م",
  facebook: "",
  instagram: "",
  servicesSectionTitle: "حلول طبية متكاملة لابتسامة صحية وواثقة",
  bookingButtonText: "احجز موعدك الآن",
  whatsappButtonText: "تواصل عبر الواتساب",
};

const SERVICES = [
  { icon: Smile, title: "تقويم الأسنان", desc: "تقويم معدني وشفاف وخطط علاجية مخصصة للبالغين والأطفال." },
  { icon: Plus, title: "زراعة الأسنان", desc: "تعويض الأسنان المفقودة بزراعات تساعد على استعادة الوظيفة والمظهر." },
  { icon: Sparkles, title: "تجميل الأسنان", desc: "قشور تجميلية، تبييض، وتحسين شكل الابتسامة بخطة مناسبة لكل حالة." },
  { icon: Zap, title: "علاج العصب", desc: "علاج القنوات الجذرية بدقة للحفاظ على الأسنان وتقليل الألم." },
  { icon: Award, title: "تركيبات الأسنان", desc: "تيجان وجسور زيركون وبورسلان بتصميم وظيفي وجمالي." },
  { icon: Heart, title: "طب أسنان الأطفال", desc: "رعاية وقائية وعلاجية للأطفال في بيئة مريحة ولطيفة." },
  { icon: Scissors, title: "جراحة الفم والأسنان", desc: "خلع ضرس العقل وبعض إجراءات جراحة الفم واللثة حسب الحالة." },
  { icon: ClipboardList, title: "الكشف والاستشارات", desc: "فحص شامل، تشخيص واضح، وخطة علاجية مرتبة قبل بدء العلاج." },
];

const TEAM = [
  { name: "د. عقلان الكامل", role: "أخصائي تقويم الأسنان", desc: "متخصص في التقويم الثابت والشفاف وخطط العلاج المخصصة للبالغين والأطفال.", initials: "عك", color: "#0284c7" },
  { name: "د. عائشة غازي", role: "طب الأسنان العام والتجميلي", desc: "خبرة واسعة في طب الأسنان العام، التجميل، والعلاجات الترميمية.", initials: "عغ", color: "#FF8C00" },
  { name: "د. إيمان الكامل", role: "طب الأسنان العام", desc: "رعاية لطيفة ومتخصصة لجميع أفراد العائلة من الأطفال حتى كبار السن.", initials: "إك", color: "#059669" },
  { name: "د. هشام القدسي", role: "طب الأسنان العام", desc: "متخصص في معالجة جذور الأسنان وترميمها وعلاج أمراض اللثة.", initials: "هق", color: "#7C3AED" },
  { name: "د. خلدون البرهي", role: "جراحة الفم والوجه والفكين", desc: "خبير في جراحة الفك والوجه، زراعة الأسنان، وخلع الضروس المعقدة.", initials: "خب", color: "#DC2626" },
];

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
  const [s, setSettings] = useState<Record<string, string>>(FALLBACK);

  useEffect(() => {
    const apiBase = process.env.NEXT_PUBLIC_API_URL ?? "";
    fetch(`${apiBase}/api/public/website-settings`, {
      method: "GET",
      headers: { "Accept-Language": "ar" },
    })
      .then((res) => {
        if (!res.ok) throw new Error("Failed");
        return res.json();
      })
      .then((data) => {
        // Merge with fallback for null safety
        const merged: Record<string, string> = { ...FALLBACK };
        for (const key of Object.keys(FALLBACK)) {
          merged[key] = data?.[key] ?? FALLBACK[key];
        }
        setSettings(merged);
      })
      .catch(() => {
        // Fallback already set — homepage still works
      });
  }, []);

  // Safe accessor with fallback
  const get = (key: string): string => s[key] ?? FALLBACK[key] ?? "";

  // Resolve image URL: if relative (from backend /uploads), keep relative — the Next.js rewrite
  // proxies /uploads/* same-origin (NAV-CEPH-FIX Part 2).
  const resolveImg = (url: string | null | undefined): string | null => {
    if (!url || url.trim() === "") return null;
    if (url.startsWith("http")) return url;
    return url;
  };

  const heroImgUrl = resolveImg(get("heroImageUrl"));

  return (
    <div>
      {/* ═══════════════════════════════════════ HERO ══════════════════════════════════════ */}
      <section className="relative text-white overflow-hidden" style={{ backgroundColor: "#0F172A" }}>
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
            style={{ color: "#94a3b8" }}
          >
            {get("heroSubtitle")}
          </p>

          {/* CTA Buttons */}
          <div className="flex flex-col sm:flex-row gap-3 justify-center md:justify-start mb-6">
            <Link
              href="/home/book"
              className="inline-flex items-center justify-center gap-2 px-8 py-4 rounded-2xl font-bold text-lg text-white shadow-xl transition-opacity hover:opacity-90"
              style={{ backgroundColor: "#FF8C00" }}
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
              { number: "+5", label: "أطباء متخصصون" },
              { number: "+8", label: "سنوات خبرة" },
              { number: "+3000", label: "حالة علاجية" },
              { number: "6", label: "أيام عمل أسبوعياً" },
            ].map((stat) => (
              <div
                key={stat.label}
                className="rounded-2xl p-5 text-center"
                style={{ backgroundColor: "rgba(255,255,255,0.05)", border: "1px solid rgba(255,255,255,0.08)" }}
              >
                <div className="text-3xl font-extrabold mb-1" style={{ color: "#FF8C00" }}>
                  {stat.number}
                </div>
                <div className="text-sm" style={{ color: "#94a3b8" }}>
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
                  <Icon className="w-6 h-6" style={{ color: "#87CEEB" }} />
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
              <div className="text-xs font-bold uppercase tracking-widest" style={{ color: "#87CEEB" }}>
                عن المركز
              </div>
              <h2 className="text-3xl sm:text-4xl font-extrabold text-slate-900 leading-tight">
                رعاية تضع المريض أولاً، مع خبرة أكاديمية وتقنيات حديثة
              </h2>
              <div className="w-16 h-1.5 rounded-full" style={{ backgroundColor: "#87CEEB" }} />
              <p className="text-lg text-slate-500 leading-relaxed">
                {get("aboutText")}
              </p>
              <div className="grid grid-cols-2 gap-3 pt-2">
                {["تشخيص دقيق", "متابعة دورية", "فريق متعدد التخصصات", "خطط علاج واضحة"].map((item) => (
                  <div key={item} className="flex items-center gap-2 text-sm font-semibold text-slate-700">
                    <CheckCircle2 className="w-4 h-4 flex-shrink-0" style={{ color: "#87CEEB" }} />
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
      <section id="services" style={{ backgroundColor: "#F8FAFC" }} className="py-20">
        <div className="max-w-7xl mx-auto px-4 sm:px-6">
          <div className="text-center mb-14 space-y-4">
            <div className="text-xs font-bold uppercase tracking-widest" style={{ color: "#87CEEB" }}>
              خدماتنا التخصصية
            </div>
            <h2 className="text-3xl sm:text-4xl font-extrabold text-slate-900">
              {get("servicesSectionTitle")}
            </h2>
            <div className="w-16 h-1.5 rounded-full mx-auto" style={{ backgroundColor: "#87CEEB" }} />
          </div>

          <div className="grid sm:grid-cols-2 lg:grid-cols-4 gap-6">
            {SERVICES.map(({ icon: Icon, title, desc }) => (
              <div
                key={title}
                className="bg-white rounded-3xl p-6 border border-slate-100 hover:border-slate-200 hover:shadow-lg transition-all duration-300 text-right group"
              >
                <div
                  className="w-14 h-14 rounded-2xl flex items-center justify-center mb-5"
                  style={{ backgroundColor: "rgba(135,206,235,0.12)" }}
                >
                  <Icon className="w-7 h-7 group-hover:scale-110 transition-transform duration-200" style={{ color: "#87CEEB" }} />
                </div>
                <h3 className="text-base font-bold text-slate-900 mb-2 group-hover:text-[#0284c7] transition-colors">
                  {title}
                </h3>
                <p className="text-slate-500 text-sm leading-relaxed">{desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* ═══════════════════════════════════════ TEAM ════════════════════════════════════════ */}
      <section id="team" className="py-20 bg-white">
        <div className="max-w-7xl mx-auto px-4 sm:px-6">
          <div className="text-center mb-14 space-y-4">
            <div className="text-xs font-bold uppercase tracking-widest" style={{ color: "#87CEEB" }}>
              الفريق الطبي
            </div>
            <h2 className="text-3xl sm:text-4xl font-extrabold text-slate-900">
              نخبة من الأطباء المتخصصين
            </h2>
            <div className="w-16 h-1.5 rounded-full mx-auto" style={{ backgroundColor: "#87CEEB" }} />
          </div>

          <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-6">
            {TEAM.map((doctor) => (
              <div
                key={doctor.name}
                className="bg-white rounded-3xl border border-slate-100 hover:shadow-lg transition-all duration-300 overflow-hidden"
                style={{ borderTop: `3px solid ${doctor.color}` }}
              >
                <div className="p-6 flex items-start gap-4">
                  <div
                    className="w-14 h-14 rounded-2xl flex items-center justify-center text-white font-bold text-lg flex-shrink-0"
                    style={{ backgroundColor: doctor.color }}
                  >
                    {doctor.initials}
                  </div>
                  <div className="min-w-0">
                    <div className="font-bold text-slate-900 text-base">{doctor.name}</div>
                    <div className="text-sm font-semibold mt-0.5 mb-2" style={{ color: doctor.color }}>
                      {doctor.role}
                    </div>
                    <div className="text-sm text-slate-500 leading-relaxed">{doctor.desc}</div>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* ═══════════════════════════════════════ CTA ═════════════════════════════════════════ */}
      <section
        className="py-20 text-white"
        style={{ backgroundColor: "#0F172A" }}
      >
        <div className="max-w-3xl mx-auto px-4 sm:px-6 text-center">
          <div
            className="w-16 h-16 rounded-3xl flex items-center justify-center mx-auto mb-6"
            style={{ backgroundColor: "rgba(135,206,235,0.12)" }}
          >
            <Stethoscope className="w-8 h-8" style={{ color: "#87CEEB" }} />
          </div>
          <h2 className="text-3xl sm:text-4xl font-extrabold mb-4 leading-tight">
            جاهز تبدأ رحلتك نحو ابتسامة أجمل؟
          </h2>
          <p className="text-lg mb-3 leading-relaxed" style={{ color: "#94a3b8" }}>
            احجز موعدك الآن، وسيقوم فريق المركز بالتواصل معك لتأكيد الموعد وترتيب زيارتك.
          </p>
          <div className="flex items-center justify-center gap-2 text-sm mb-10" style={{ color: "#64748b" }}>
            <MapPin className="w-4 h-4" style={{ color: "#87CEEB" }} />
            {get("address")}
            <span className="mx-2">·</span>
            {get("workingHours")}
          </div>
          <div className="flex flex-col sm:flex-row gap-4 justify-center">
            <Link
              href="/home/book"
              className="inline-flex items-center justify-center gap-2 px-8 py-4 rounded-2xl font-bold text-lg text-white shadow-xl transition-opacity hover:opacity-90"
              style={{ backgroundColor: "#FF8C00" }}
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
