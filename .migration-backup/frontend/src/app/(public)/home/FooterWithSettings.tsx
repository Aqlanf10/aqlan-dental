"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Phone, MessageCircle, MapPin, Clock } from "lucide-react";

// ─── Helper: resolve image URL ────────────────────────────────────────────────
// NAV-CEPH-FIX (Part 2): relative path → Next.js rewrite proxies /uploads/* same-origin.
function resolveImageUrl(url: string | null | undefined): string | null {
  if (!url || url.trim() === "") return null;
  if (url.startsWith("http")) return url;
  return url;
}

const FALLBACK_SETTINGS: Record<string, string> = {
  clinicName: "مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان",
  phone: "04-253028",
  whatsapp: "967770245745",
  address: "تعز، اليمن — شارع التحرير الأعلى",
  workingHours: "السبت – الخميس: 8 ص – 8 م",
  facebook: "",
  instagram: "",
  logoUrl: "",
};

export function FooterWithSettings() {
  const [s, setSettings] = useState<Record<string, string>>(FALLBACK_SETTINGS);

  useEffect(() => {
    const apiBase = process.env.NEXT_PUBLIC_API_URL ?? "";
    fetch(`${apiBase}/api/public/website-settings`)
      .then((res) => (res.ok ? res.json() : null))
      .then((data) => {
        if (data) {
          const merged: Record<string, string> = { ...FALLBACK_SETTINGS };
          for (const key of Object.keys(FALLBACK_SETTINGS)) {
            merged[key] = data[key] ?? FALLBACK_SETTINGS[key];
          }
          setSettings(merged);
        }
      })
      .catch(() => {
        // Fallback already set
      });
  }, []);

  const resolvedLogo = resolveImageUrl(s.logoUrl);

  return (
    <footer id="contact" style={{ backgroundColor: "#0F172A" }} className="text-slate-400">
      <div className="max-w-7xl mx-auto px-6 pt-16 pb-10 grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-12 border-b border-white/5">

        {/* Col 1 — Brand */}
        <div className="space-y-5">
          <div className="flex items-center gap-3">
            <div className="bg-white rounded-xl p-2 flex-shrink-0 shadow-sm">
              {resolvedLogo ? (
                <>
                {/* eslint-disable-next-line @next/next/no-img-element */}
                <img
                  src={resolvedLogo}
                  alt={s.clinicName}
                  className="h-10 w-auto object-contain"
                  onError={(e) => {
                    (e.target as HTMLImageElement).src = "/logo.png";
                  }}
                />
                </>
              ) : (
                <>
                {/* eslint-disable-next-line @next/next/no-img-element */}
                <img
                  src="/logo.png"
                  alt={s.clinicName}
                  className="h-10 w-auto object-contain"
                />
                </>
              )}
            </div>
            <div>
              <div className="text-white font-extrabold text-sm leading-tight">
                {s.clinicName}
              </div>
              <div
                className="text-[10px] font-bold tracking-wider mt-0.5"
                style={{ color: "#87CEEB" }}
              >
                لتقويم وزراعة وتجميل الأسنان
              </div>
            </div>
          </div>
          <p className="text-sm leading-relaxed text-slate-400">
            مركز متخصص في تقويم وزراعة وتجميل الأسنان في مدينة تعز، يجمع بين الخبرة الأكاديمية والتقنيات الحديثة وخطط العلاج الواضحة.
          </p>
          <div className="flex items-center gap-2.5">
            <a
              href={`https://wa.me/${s.whatsapp}`}
              target="_blank"
              rel="noopener noreferrer"
              className="w-9 h-9 rounded-xl bg-white/5 hover:bg-white/10 flex items-center justify-center text-green-400 transition-colors"
              aria-label="واتساب"
            >
              <MessageCircle className="w-4 h-4" />
            </a>
            {s.facebook && (
              <a
                href={s.facebook}
                target="_blank"
                rel="noopener noreferrer"
                className="w-9 h-9 rounded-xl bg-white/5 hover:bg-white/10 flex items-center justify-center text-blue-400 transition-colors"
                aria-label="فيسبوك"
              >
                <svg className="w-4 h-4" fill="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                  <path d="M24 12.073c0-6.627-5.373-12-12-12s-12 5.373-12 12c0 5.99 4.388 10.954 10.125 11.854v-8.385H7.078v-3.47h3.047V9.43c0-3.007 1.792-4.669 4.533-4.669 1.312 0 2.686.235 2.686.235v2.953H15.83c-1.491 0-1.956.925-1.956 1.874v2.25h3.328l-.532 3.47h-2.796v8.385C19.612 23.027 24 18.062 24 12.073z" />
                </svg>
              </a>
            )}
            {s.instagram && (
              <a
                href={s.instagram}
                target="_blank"
                rel="noopener noreferrer"
                className="w-9 h-9 rounded-xl bg-white/5 hover:bg-white/10 flex items-center justify-center text-pink-400 transition-colors"
                aria-label="انستغرام"
              >
                <svg className="w-4 h-4" fill="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                  <path d="M12.315 2c2.43 0 2.784.013 3.808.06 1.064.049 1.791.218 2.427.465a4.902 4.902 0 011.772 1.153 4.902 4.902 0 011.153 1.772c.247.636.416 1.363.465 2.427.048 1.067.06 1.407.06 4.123v.08c0 2.643-.012 2.987-.06 4.043-.049 1.064-.218 1.791-.465 2.427a4.902 4.902 0 01-1.153 1.772 4.902 4.902 0 01-1.772 1.153c-.636.247-1.363.416-2.427.465-1.067.048-1.407.06-4.123.06h-.08c-2.643 0-2.987-.012-4.043-.06-1.064-.049-1.791-.218-2.427-.465a4.902 4.902 0 01-1.772-1.153 4.902 4.902 0 01-1.153-1.772c-.247-.636-.416-1.363-.465-2.427-.047-1.024-.06-1.379-.06-3.808v-.63c0-2.43.013-2.784.06-3.808.049-1.064.218-1.791.465-2.427a4.902 4.902 0 011.153-1.772A4.902 4.902 0 015.45 2.525c.636-.247 1.363-.416 2.427-.465C8.901 2.013 9.256 2 11.685 2h.63zm-.081 1.802h-.468c-2.456 0-2.784.011-3.807.058-.975.045-1.504.207-1.857.344-.467.182-.8.398-1.15.748-.35.35-.566.683-.748 1.15-.137.353-.3.882-.344 1.857-.047 1.023-.058 1.351-.058 3.807v.468c0 2.456.011 2.784.058 3.807.045.975.207 1.504.344 1.857.182.466.399.8.748 1.15.35.35.683.566 1.15.748.353.137.882.3 1.857.344 1.054.048 1.37.058 4.041.058h.08c2.597 0 2.917-.01 3.96-.058.976-.045 1.505-.207 1.858-.344.466-.182.8-.398 1.15-.748.35-.35.566-.683.748-1.15.137-.353.3-.882.344-1.857.048-1.055.058-1.37.058-4.041v-.08c0-2.597-.01-2.917-.058-3.96-.045-.976-.207-1.505-.344-1.858a3.097 3.097 0 00-.748-1.15 3.098 3.098 0 00-1.15-.748c-.353-.137-.882-.3-1.857-.344-1.023-.047-1.351-.058-3.807-.058zM12 6.865a5.135 5.135 0 110 10.27 5.135 5.135 0 010-10.27zm0 1.802a3.333 3.333 0 100 6.666 3.333 3.333 0 000-6.666zm5.338-3.205a1.2 1.2 0 110 2.4 1.2 1.2 0 010-2.4z" />
                </svg>
              </a>
            )}
            {!s.facebook && (
              <a
                href="#"
                className="w-9 h-9 rounded-xl bg-white/5 hover:bg-white/10 flex items-center justify-center text-blue-400 transition-colors"
                aria-label="فيسبوك"
              >
                <svg className="w-4 h-4" fill="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                  <path d="M24 12.073c0-6.627-5.373-12-12-12s-12 5.373-12 12c0 5.99 4.388 10.954 10.125 11.854v-8.385H7.078v-3.47h3.047V9.43c0-3.007 1.792-4.669 4.533-4.669 1.312 0 2.686.235 2.686.235v2.953H15.83c-1.491 0-1.956.925-1.956 1.874v2.25h3.328l-.532 3.47h-2.796v8.385C19.612 23.027 24 18.062 24 12.073z" />
                </svg>
              </a>
            )}
            {!s.instagram && (
              <a
                href="#"
                className="w-9 h-9 rounded-xl bg-white/5 hover:bg-white/10 flex items-center justify-center text-pink-400 transition-colors"
                aria-label="انستغرام"
              >
                <svg className="w-4 h-4" fill="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                  <path d="M12.315 2c2.43 0 2.784.013 3.808.06 1.064.049 1.791.218 2.427.465a4.902 4.902 0 011.772 1.153 4.902 4.902 0 011.153 1.772c.247.636.416 1.363.465 2.427.048 1.067.06 1.407.06 4.123v.08c0 2.643-.012 2.987-.06 4.043-.049 1.064-.218 1.791-.465 2.427a4.902 4.902 0 01-1.153 1.772 4.902 4.902 0 01-1.772 1.153c-.636.247-1.363.416-2.427.465-1.067.048-1.407.06-4.123.06h-.08c-2.643 0-2.987-.012-4.043-.06-1.064-.049-1.791-.218-2.427-.465a4.902 4.902 0 01-1.772-1.153 4.902 4.902 0 01-1.153-1.772c-.247-.636-.416-1.363-.465-2.427-.047-1.024-.06-1.379-.06-3.808v-.63c0-2.43.013-2.784.06-3.808.049-1.064.218-1.791.465-2.427a4.902 4.902 0 011.153-1.772A4.902 4.902 0 015.45 2.525c.636-.247 1.363-.416 2.427-.465C8.901 2.013 9.256 2 11.685 2h.63zm-.081 1.802h-.468c-2.456 0-2.784.011-3.807.058-.975.045-1.504.207-1.857.344-.467.182-.8.398-1.15.748-.35.35-.566.683-.748 1.15-.137.353-.3.882-.344 1.857-.047 1.023-.058 1.351-.058 3.807v.468c0 2.456.011 2.784.058 3.807.045.975.207 1.504.344 1.857.182.466.399.8.748 1.15.35.35.683.566 1.15.748.353.137.882.3 1.857.344 1.054.048 1.37.058 4.041.058h.08c2.597 0 2.917-.01 3.96-.058.976-.045 1.505-.207 1.858-.344.466-.182.8-.398 1.15-.748.35-.35.566-.683.748-1.15.137-.353.3-.882.344-1.857.048-1.055.058-1.37.058-4.041v-.08c0-2.597-.01-2.917-.058-3.96-.045-.976-.207-1.505-.344-1.858a3.097 3.097 0 00-.748-1.15 3.098 3.098 0 00-1.15-.748c-.353-.137-.882-.3-1.857-.344-1.023-.047-1.351-.058-3.807-.058zM12 6.865a5.135 5.135 0 110 10.27 5.135 5.135 0 010-10.27zm0 1.802a3.333 3.333 0 100 6.666 3.333 3.333 0 000-6.666zm5.338-3.205a1.2 1.2 0 110 2.4 1.2 1.2 0 010-2.4z" />
                </svg>
              </a>
            )}
          </div>
        </div>

        {/* Col 2 — Quick Links */}
        <div className="space-y-5">
          <h4 className="text-white font-bold text-sm">روابط سريعة</h4>
          <ul className="space-y-3 text-sm">
            {(
              [
                { href: "/home", label: "الرئيسية", isLink: true },
                { href: "#services", label: "خدماتنا", isLink: false },
                { href: "/home/book", label: "احجز موعد", isLink: true },
                { href: "/portal/login", label: "بوابة المرضى", isLink: true },
                { href: "/login", label: "دخول الكادر", isLink: true },
              ] as { href: string; label: string; isLink: boolean }[]
            ).map((item) => (
              <li key={item.label}>
                {item.isLink ? (
                  <Link
                    href={item.href}
                    className="text-slate-400 hover:text-white transition-colors flex items-center gap-2"
                  >
                    <span
                      className="w-1 h-1 rounded-full flex-shrink-0"
                      style={{ backgroundColor: "#87CEEB" }}
                    />
                    {item.label}
                  </Link>
                ) : (
                  <a
                    href={item.href}
                    className="text-slate-400 hover:text-white transition-colors flex items-center gap-2"
                  >
                    <span
                      className="w-1 h-1 rounded-full flex-shrink-0"
                      style={{ backgroundColor: "#87CEEB" }}
                    />
                    {item.label}
                  </a>
                )}
              </li>
            ))}
          </ul>
        </div>

        {/* Col 3 — Contact */}
        <div className="space-y-5">
          <h4 className="text-white font-bold text-sm">تواصل معنا</h4>
          <ul className="space-y-3.5 text-sm">
            <li>
              <a
                href={`tel:${s.phone.replace(/-/g, "")}`}
                className="text-slate-400 hover:text-white transition-colors flex items-center gap-2.5"
              >
                <Phone className="w-4 h-4 flex-shrink-0" style={{ color: "#87CEEB" }} />
                {s.phone}
              </a>
            </li>
            <li>
              <a
                href={`https://wa.me/${s.whatsapp}`}
                target="_blank"
                rel="noopener noreferrer"
                className="text-slate-400 hover:text-white transition-colors flex items-center gap-2.5"
              >
                <MessageCircle className="w-4 h-4 flex-shrink-0 text-green-400" />
                واتساب
              </a>
            </li>
            <li className="flex items-start gap-2.5 text-slate-400">
              <MapPin className="w-4 h-4 flex-shrink-0 mt-0.5" style={{ color: "#87CEEB" }} />
              {s.address}
            </li>
          </ul>
        </div>

        {/* Col 4 — Hours */}
        <div className="space-y-5">
          <h4 className="text-white font-bold text-sm">مواعيد العمل</h4>
          <div className="space-y-4 text-sm">
            <div className="flex items-start gap-2.5 text-slate-400">
              <Clock className="w-4 h-4 flex-shrink-0 mt-0.5" style={{ color: "#87CEEB" }} />
              <div>
                <div className="text-white font-semibold">{s.workingHours}</div>
              </div>
            </div>
            <div className="flex items-center gap-2.5">
              <div className="w-4 h-4 flex-shrink-0 flex items-center justify-center">
                <div className="w-2 h-2 rounded-full bg-red-400/60" />
              </div>
              <div className="text-slate-500">
                الجمعة — <span className="text-red-400/80 font-medium">مغلق</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Bottom bar */}
      <div className="max-w-7xl mx-auto px-6 py-5 flex flex-col sm:flex-row items-center justify-between gap-3">
        <div className="text-xs text-slate-600 text-center sm:text-right">
          © {new Date().getFullYear()} {s.clinicName}. جميع الحقوق محفوظة.
        </div>
        <Link
          href="/home/book"
          className="text-xs font-bold px-4 py-2 rounded-lg text-white transition-opacity hover:opacity-90"
          style={{ backgroundColor: "#FF8C00" }}
        >
          احجز موعدك الآن
        </Link>
      </div>
    </footer>
  );
}
