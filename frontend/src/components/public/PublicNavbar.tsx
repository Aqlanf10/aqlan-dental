"use client";
import { useState, useEffect } from "react";
import Link from "next/link";
import { Menu, X, ChevronDown, User } from "lucide-react";

export function PublicNavbar() {
  const [menuOpen, setMenuOpen] = useState(false);
  const [loginOpen, setLoginOpen] = useState(false);

  useEffect(() => {
    if (!loginOpen) return;
    const close = (e: MouseEvent) => {
      if (!(e.target as Element).closest("[data-login-dropdown]")) setLoginOpen(false);
    };
    document.addEventListener("click", close);
    return () => document.removeEventListener("click", close);
  }, [loginOpen]);

  useEffect(() => {
    document.body.style.overflow = menuOpen ? "hidden" : "";
    return () => { document.body.style.overflow = ""; };
  }, [menuOpen]);

  const navLinks = [
    { href: "#services", label: "خدماتنا" },
    { href: "#about", label: "عن المركز" },
    { href: "#team", label: "الفريق الطبي" },
    { href: "#contact", label: "تواصل معنا" },
  ];

  return (
    <header className="fixed w-full z-50 bg-white/95 backdrop-blur-md border-b border-slate-100 shadow-sm">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 h-20 flex items-center justify-between" dir="rtl">
        {/* Logo */}
        <Link href="/home" className="flex items-center gap-3 flex-shrink-0">
          <img
            src="/logo.png"
            alt="مركز الدكتور عقلان الكامل"
            className="h-11 w-auto object-contain"
          />
          <div className="hidden sm:block">
            <div className="font-extrabold text-[15px] text-slate-900 leading-tight">
              مركز الدكتور عقلان الكامل
            </div>
            <div className="text-[10px] font-bold tracking-wider" style={{ color: "#87CEEB" }}>
              لتقويم وزراعة وتجميل الأسنان
            </div>
          </div>
        </Link>

        {/* Desktop nav */}
        <nav className="hidden lg:flex items-center gap-5 text-sm font-semibold text-slate-600">
          {navLinks.map((link) => (
            <a
              key={link.href}
              href={link.href}
              className="hover:text-slate-900 transition-colors py-1"
            >
              {link.label}
            </a>
          ))}

          {/* Login dropdown */}
          <div className="relative" data-login-dropdown>
            <button
              onClick={() => setLoginOpen(!loginOpen)}
              className="flex items-center gap-1.5 text-sm font-semibold text-slate-600 hover:text-slate-900 transition-colors py-2 px-3 rounded-xl hover:bg-slate-50 border border-slate-200"
            >
              <User className="w-4 h-4" />
              تسجيل الدخول
              <ChevronDown
                className={`w-3.5 h-3.5 transition-transform duration-200 ${loginOpen ? "rotate-180" : ""}`}
              />
            </button>
            {loginOpen && (
              <div className="absolute left-0 top-full mt-2 w-44 bg-white rounded-2xl shadow-xl border border-slate-100 py-1.5 z-50">
                <Link
                  href="/portal/login"
                  onClick={() => setLoginOpen(false)}
                  className="flex items-center gap-2.5 px-4 py-2.5 text-sm text-slate-700 hover:bg-slate-50 transition-colors"
                >
                  <span className="w-2 h-2 rounded-full flex-shrink-0" style={{ backgroundColor: "#FF8C00" }} />
                  بوابة المرضى
                </Link>
                <div className="mx-3 border-t border-slate-100" />
                <Link
                  href="/login"
                  onClick={() => setLoginOpen(false)}
                  className="flex items-center gap-2.5 px-4 py-2.5 text-sm text-slate-700 hover:bg-slate-50 transition-colors"
                >
                  <span className="w-2 h-2 rounded-full bg-slate-400 flex-shrink-0" />
                  دخول الكادر
                </Link>
              </div>
            )}
          </div>

          <Link
            href="/home/book"
            className="px-5 py-2.5 rounded-xl font-bold text-white text-sm shadow-md hover:opacity-90 transition-opacity"
            style={{ backgroundColor: "#FF8C00" }}
          >
            احجز موعدك
          </Link>
        </nav>

        {/* Mobile hamburger */}
        <button
          onClick={() => setMenuOpen(!menuOpen)}
          className="lg:hidden w-10 h-10 flex items-center justify-center rounded-xl text-slate-700 hover:bg-slate-100 transition-colors"
          aria-label="القائمة"
        >
          {menuOpen ? <X className="w-6 h-6" /> : <Menu className="w-6 h-6" />}
        </button>
      </div>

      {/* Mobile menu overlay */}
      {menuOpen && (
        <>
          <div
            className="lg:hidden fixed inset-0 top-20 bg-black/40 z-40"
            onClick={() => setMenuOpen(false)}
          />
          <div
            className="lg:hidden fixed top-20 right-0 left-0 z-50 bg-white border-t border-slate-100 shadow-2xl max-h-[calc(100vh-5rem)] overflow-y-auto"
            dir="rtl"
          >
            <nav className="max-w-7xl mx-auto px-6 py-4 flex flex-col gap-1">
              {navLinks.map((link) => (
                <a
                  key={link.href}
                  href={link.href}
                  onClick={() => setMenuOpen(false)}
                  className="py-3 px-4 text-slate-700 font-semibold hover:bg-slate-50 rounded-xl transition-colors text-base"
                >
                  {link.label}
                </a>
              ))}
              <div className="border-t border-slate-100 my-2" />
              <div className="px-4 py-1 text-[11px] text-slate-400 font-bold uppercase tracking-wider">
                تسجيل الدخول
              </div>
              <Link
                href="/portal/login"
                onClick={() => setMenuOpen(false)}
                className="py-3 px-4 text-slate-700 font-semibold hover:bg-orange-50 rounded-xl transition-colors flex items-center gap-2.5"
              >
                <span className="w-2 h-2 rounded-full flex-shrink-0" style={{ backgroundColor: "#FF8C00" }} />
                بوابة المرضى
              </Link>
              <Link
                href="/login"
                onClick={() => setMenuOpen(false)}
                className="py-3 px-4 text-slate-700 font-semibold hover:bg-slate-50 rounded-xl transition-colors flex items-center gap-2.5"
              >
                <span className="w-2 h-2 rounded-full bg-slate-300 flex-shrink-0" />
                دخول الكادر
              </Link>
              <div className="border-t border-slate-100 my-2" />
              <Link
                href="/home/book"
                onClick={() => setMenuOpen(false)}
                className="py-3.5 px-4 text-white font-bold rounded-xl text-center text-base"
                style={{ backgroundColor: "#FF8C00" }}
              >
                احجز موعدك الآن
              </Link>
            </nav>
          </div>
        </>
      )}
    </header>
  );
}
