import type { Metadata } from "next";
import Link from "next/link";
import { Phone, MessageCircle, MapPin, Clock, ChevronDown } from "lucide-react";

export const metadata: Metadata = {
  title: "مركز د. عقلان الكامل لطب وتقويم الأسنان — تعز، اليمن",
  description: "مركز متخصص في طب وتقويم الأسنان في تعز، اليمن. نقدم خدمات شاملة من تقويم الأسنان إلى الجراحة التقنية بأيدي نخبة من الأطباء المتخصصين.",
};

export default function PublicLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen flex flex-col bg-white">
      {/* ── Navbar ── */}
      <header className="sticky top-0 z-50 bg-clinic-navy shadow-md">
        <div className="max-w-6xl mx-auto px-4 py-3 flex items-center justify-between">
          <Link href="/home" className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-full bg-clinic-blue flex items-center justify-center text-white font-bold text-lg">
              ع
            </div>
            <div className="text-white">
              <div className="font-bold text-base leading-tight">مركز د. عقلان الكامل</div>
              <div className="text-xs text-blue-200 leading-tight">لطب وتقويم الأسنان</div>
            </div>
          </Link>
          <nav className="hidden md:flex items-center gap-6 text-sm text-blue-100">
            <a href="#services" className="hover:text-white transition-colors">خدماتنا</a>
            <a href="#team" className="hover:text-white transition-colors">فريق الأطباء</a>
            <a href="#contact" className="hover:text-white transition-colors">تواصل معنا</a>
          </nav>
          <div className="flex items-center gap-2">
            {/* Login dropdown — desktop */}
            <div className="relative group hidden md:block">
              <button className="flex items-center gap-1.5 text-sm text-blue-100 hover:text-white transition-colors px-3 py-2 rounded-lg hover:bg-white/10 border border-white/20">
                تسجيل الدخول
                <ChevronDown className="w-3.5 h-3.5 transition-transform group-hover:rotate-180" />
              </button>
              <div className="absolute top-full left-0 mt-1 w-44 bg-white rounded-xl shadow-dropdown py-1.5 opacity-0 invisible group-hover:opacity-100 group-hover:visible transition-all duration-150 z-50">
                <Link
                  href="/portal/login"
                  className="flex items-center gap-2.5 px-4 py-2.5 text-sm text-gray-700 hover:bg-clinic-blue-50 hover:text-clinic-blue transition-colors"
                >
                  <span className="w-2 h-2 rounded-full bg-clinic-orange flex-shrink-0" />
                  بوابة المرضى
                </Link>
                <div className="mx-3 border-t border-gray-100" />
                <Link
                  href="/login"
                  className="flex items-center gap-2.5 px-4 py-2.5 text-sm text-gray-700 hover:bg-clinic-blue-50 hover:text-clinic-blue transition-colors"
                >
                  <span className="w-2 h-2 rounded-full bg-clinic-blue flex-shrink-0" />
                  دخول الكادر
                </Link>
              </div>
            </div>

            <Link
              href="/home/book"
              className="bg-clinic-orange hover:bg-orange-500 text-white text-sm font-semibold px-4 py-2 rounded-lg transition-colors"
            >
              احجز موعدك
            </Link>
          </div>
        </div>

        {/* Login strip — mobile only */}
        <div className="md:hidden border-t border-white/10 px-4 py-2 flex items-center justify-end gap-5" dir="rtl">
          <span className="text-blue-300/60 text-xs">تسجيل الدخول:</span>
          <Link href="/portal/login" className="text-blue-100 hover:text-white text-xs font-medium transition-colors">
            بوابة المرضى
          </Link>
          <Link href="/login" className="text-blue-100 hover:text-white text-xs font-medium transition-colors">
            دخول الكادر
          </Link>
        </div>
      </header>

      <main className="flex-1">
        {children}
      </main>

      {/* ── Footer ── */}
      <footer id="contact" className="bg-clinic-navy text-blue-100" dir="rtl">
        {/* Main footer grid */}
        <div className="max-w-6xl mx-auto px-4 pt-14 pb-10 grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-10">

          {/* Col 1 — About */}
          <div>
            <div className="flex items-center gap-3 mb-4">
              <div className="w-10 h-10 rounded-full bg-clinic-blue flex items-center justify-center text-white font-bold text-lg flex-shrink-0">
                ع
              </div>
              <div>
                <div className="text-white font-bold text-sm leading-tight">مركز د. عقلان الكامل</div>
                <div className="text-xs text-blue-300 leading-tight">لطب وتقويم الأسنان</div>
              </div>
            </div>
            <p className="text-blue-200 text-sm leading-relaxed mb-4">
              مركز متخصص في طب وتقويم الأسنان بتعز
            </p>
            <div className="flex items-center gap-2 text-sm text-blue-200">
              <Clock className="w-4 h-4 text-clinic-orange flex-shrink-0" />
              السبت – الخميس: 8 ص – 8 م
            </div>
          </div>

          {/* Col 2 — Services */}
          <div>
            <h4 className="text-white font-semibold mb-4 text-sm">خدماتنا</h4>
            <ul className="space-y-2.5">
              {[
                "تقويم الأسنان",
                "زراعة الأسنان",
                "تجميل الأسنان",
                "علاج العصب",
                "جراحة الفم",
              ].map((service) => (
                <li key={service}>
                  <a
                    href="#services"
                    className="text-blue-200 hover:text-white transition-colors text-sm inline-flex items-center gap-1.5"
                  >
                    <span className="w-1 h-1 rounded-full bg-clinic-orange inline-block flex-shrink-0" />
                    {service}
                  </a>
                </li>
              ))}
            </ul>
          </div>

          {/* Col 3 — Quick Links */}
          <div>
            <h4 className="text-white font-semibold mb-4 text-sm">روابط سريعة</h4>
            <ul className="space-y-2.5">
              <li>
                <Link
                  href="/home"
                  className="text-blue-200 hover:text-white transition-colors text-sm inline-flex items-center gap-1.5"
                >
                  <span className="w-1 h-1 rounded-full bg-clinic-blue-500 inline-block flex-shrink-0" />
                  الرئيسية
                </Link>
              </li>
              <li>
                <Link
                  href="/home/book"
                  className="text-blue-200 hover:text-white transition-colors text-sm inline-flex items-center gap-1.5"
                >
                  <span className="w-1 h-1 rounded-full bg-clinic-blue-500 inline-block flex-shrink-0" />
                  احجز موعد
                </Link>
              </li>
              <li>
                <a
                  href="#team"
                  className="text-blue-200 hover:text-white transition-colors text-sm inline-flex items-center gap-1.5"
                >
                  <span className="w-1 h-1 rounded-full bg-clinic-blue-500 inline-block flex-shrink-0" />
                  فريق الأطباء
                </a>
              </li>
              <li>
                <Link
                  href="/portal/login"
                  className="text-blue-200 hover:text-white transition-colors text-sm inline-flex items-center gap-2"
                >
                  <span className="w-1 h-1 rounded-full bg-clinic-blue-500 inline-block flex-shrink-0" />
                  بوابة المريض
                  <span className="bg-clinic-orange text-white text-xs px-1.5 py-0.5 rounded-full font-bold leading-none">
                    جديد
                  </span>
                </Link>
              </li>
              <li>
                <Link
                  href="/login"
                  className="text-blue-200 hover:text-white transition-colors text-sm inline-flex items-center gap-1.5"
                >
                  <span className="w-1 h-1 rounded-full bg-blue-400/60 inline-block flex-shrink-0" />
                  دخول الكادر
                </Link>
              </li>
            </ul>
          </div>

          {/* Col 4 — Contact */}
          <div>
            <h4 className="text-white font-semibold mb-4 text-sm">تواصل معنا</h4>
            <ul className="space-y-3">
              <li>
                <a
                  href="tel:04253028"
                  className="text-blue-200 hover:text-white transition-colors text-sm inline-flex items-center gap-2"
                >
                  <Phone className="w-4 h-4 text-clinic-blue-500 flex-shrink-0" />
                  04-253028
                </a>
              </li>
              <li>
                <a
                  href="tel:770245745"
                  className="text-blue-200 hover:text-white transition-colors text-sm inline-flex items-center gap-2"
                >
                  <Phone className="w-4 h-4 text-clinic-blue-500 flex-shrink-0" />
                  770-245745
                </a>
              </li>
              <li>
                <a
                  href="https://wa.me/967770245745"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-blue-200 hover:text-white transition-colors text-sm inline-flex items-center gap-2"
                >
                  <MessageCircle className="w-4 h-4 text-green-400 flex-shrink-0" />
                  واتساب
                </a>
              </li>
              <li>
                <div className="text-blue-200 text-sm flex items-start gap-2">
                  <MapPin className="w-4 h-4 text-clinic-orange flex-shrink-0 mt-0.5" />
                  تعز — شارع التحرير الأعلى
                </div>
              </li>
            </ul>
          </div>
        </div>

        {/* Bottom bar */}
        <div className="border-t border-blue-900">
          <div className="max-w-6xl mx-auto px-4 py-5 text-center text-xs text-blue-400">
            © 2025 مركز د. عقلان الكامل لطب وتقويم الأسنان. جميع الحقوق محفوظة.
          </div>
        </div>
      </footer>
    </div>
  );
}
