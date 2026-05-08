import type { Metadata } from "next";
import Link from "next/link";

export const metadata: Metadata = {
  title: "مركز د. عقلان الكامل لطب وتقويم الأسنان — تعز، اليمن",
  description: "مركز متخصص في طب وتقويم الأسنان في تعز، اليمن. نقدم خدمات شاملة من تقويم الأسنان إلى الجراحة التقنية بأيدي نخبة من الأطباء المتخصصين.",
};

export default function PublicLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen flex flex-col bg-white">
      {/* Navbar */}
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
          <Link
            href="/home/book"
            className="bg-clinic-orange hover:bg-orange-500 text-white text-sm font-semibold px-4 py-2 rounded-lg transition-colors"
          >
            احجز موعدك
          </Link>
        </div>
      </header>

      <main className="flex-1">
        {children}
      </main>

      {/* Footer */}
      <footer id="contact" className="bg-clinic-navy text-blue-100 py-10">
        <div className="max-w-6xl mx-auto px-4 grid grid-cols-1 md:grid-cols-3 gap-8">
          <div>
            <div className="text-white font-bold text-lg mb-2">مركز د. عقلان الكامل</div>
            <div className="text-sm text-blue-200">لطب وتقويم الأسنان</div>
            <div className="text-sm mt-3 text-blue-200">تعز، اليمن — شارع التحرير الأعلى</div>
          </div>
          <div>
            <div className="text-white font-semibold mb-3">تواصل معنا</div>
            <div className="text-sm space-y-1">
              <div>📞 04-253028</div>
              <div>🕐 السبت – الخميس: 8 ص – 8 م</div>
            </div>
          </div>
          <div>
            <div className="text-white font-semibold mb-3">روابط سريعة</div>
            <div className="text-sm space-y-2">
              <div><a href="#services" className="hover:text-white transition-colors">خدماتنا</a></div>
              <div><a href="#team" className="hover:text-white transition-colors">فريق الأطباء</a></div>
              <div><Link href="/home/book" className="hover:text-white transition-colors">حجز موعد</Link></div>
            </div>
          </div>
        </div>
        <div className="max-w-6xl mx-auto px-4 mt-8 pt-6 border-t border-blue-800 text-center text-xs text-blue-300">
          © {new Date().getFullYear()} مركز د. عقلان الكامل لطب وتقويم الأسنان. جميع الحقوق محفوظة.
        </div>
      </footer>
    </div>
  );
}
