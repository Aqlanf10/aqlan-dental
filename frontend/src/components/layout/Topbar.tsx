"use client";

import { Search } from "lucide-react";
import { useAuthStore } from "@/stores/authStore";
import { SmartAlertsPanel } from "./SmartAlertsPanel";
import { usePathname, useRouter } from "next/navigation";
import { useState, useEffect, useRef } from "react";

/* ─── Page title map based on route ──────────────────────────────────────────── */
const PAGE_TITLES: Record<string, string> = {
  "/": "لوحة التحكم",
  "/patients": "المرضى",
  "/appointments": "المواعيد",
  "/ortho": "التقويم",
  "/ceph": "السيفالومتري المتقدم",
  "/vto": "محاكاة العلاج VTO",
  "/general": "طب الأسنان العام",
  "/surgery": "جراحة الوجه والفكين",
  "/messaging": "الرسائل",
  "/sms": "تذكيرات SMS",
  "/recall": "نظام الاستدعاء",
  "/finance": "المالية",
  "/finance/expenses": "المصروفات",
  "/finance/contracts": "العقود",
  "/finance/payments": "المدفوعات",
  "/finance/overdue": "المتأخرات",
  "/prescriptions": "الوصفات الطبية",
  "/referrals": "الإحالات",
  "/lab": "طلبات المختبر",
  "/inventory": "المخزون",
  "/reports": "التقارير والإحصائيات",
  "/settings": "الإعدادات",
  "/audit": "سجل التدقيق",
  "/profile": "حسابي",
};

function getPageTitle(pathname: string): string {
  // Exact match first
  if (PAGE_TITLES[pathname]) return PAGE_TITLES[pathname];
  // Prefix match (most specific)
  const match = Object.entries(PAGE_TITLES)
    .filter(([path]) => path !== "/" && pathname.startsWith(path))
    .sort((a, b) => b[0].length - a[0].length)[0];
  return match ? match[1] : "لوحة التحكم";
}

/* ─── Live Clock ─────────────────────────────────────────────────────────────── */
function LiveClock() {
  const [now, setNow] = useState(new Date());

  useEffect(() => {
    const timer = setInterval(() => setNow(new Date()), 1000);
    return () => clearInterval(timer);
  }, []);

  const timeStr = now.toLocaleTimeString("ar-SA", {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hour12: false,
  });

  const dateStr = now.toLocaleDateString("ar-SA", {
    weekday: "long",
    year: "numeric",
    month: "long",
    day: "numeric",
  });

  return (
    <div className="bg-[#f0f5fb] rounded-[10px] px-[14px] py-1 border border-[#dce8f5] flex items-center gap-3">
      <span className="text-[18px] font-extrabold text-[#0d2137] font-mono tracking-[1px]">
        {timeStr}
      </span>
      <span className="text-[10px] font-semibold text-[#94a3b8]">{dateStr}</span>
    </div>
  );
}

/* ─── Topbar Component ───────────────────────────────────────────────────────── */
export function Topbar() {
  const { user } = useAuthStore();
  const pathname = usePathname();
  const router = useRouter();
  const pageTitle = getPageTitle(pathname);
  const [searchQuery, setSearchQuery] = useState("");
  const [searchOpen, setSearchOpen] = useState(false);
  const searchInputRef = useRef<HTMLInputElement>(null);

  const searchRoutes = [
    { path: "/patients", label: "المرضى" },
    { path: "/appointments", label: "المواعيد" },
    { path: "/ortho", label: "التقويم" },
    { path: "/ceph", label: "السيفالومتري" },
    { path: "/general", label: "طب الأسنان العام" },
    { path: "/surgery", label: "جراحة الوجه والفكين" },
    { path: "/finance", label: "المالية" },
    { path: "/prescriptions", label: "الوصفات الطبية" },
    { path: "/referrals", label: "الإحالات" },
    { path: "/lab", label: "طلبات المختبر" },
    { path: "/inventory", label: "المخزون" },
    { path: "/reports", label: "التقارير" },
    { path: "/settings", label: "الإعدادات" },
    { path: "/messaging", label: "الرسائل" },
    { path: "/sms", label: "تذكيرات SMS" },
    { path: "/recall", label: "نظام الاستدعاء" },
    { path: "/vto", label: "VTO" },
    { path: "/audit", label: "سجل التدقيق" },
    { path: "/profile", label: "حسابي" },
  ];

  const filteredRoutes = searchQuery
    ? searchRoutes.filter((r) => r.label.includes(searchQuery))
    : [];

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    if (filteredRoutes.length > 0) {
      router.push(filteredRoutes[0].path);
      setSearchQuery("");
      setSearchOpen(false);
    }
  };

  return (
    <header className="h-16 bg-white border-b border-[#e8f0f9] flex items-center justify-between px-6 flex-shrink-0">
      {/* Right side: page title + clock */}
      <div className="flex items-center gap-4">
        {/* Mobile hamburger spacer */}
        <div className="lg:hidden w-10" />
        <h1 className="text-[18px] font-extrabold text-[#0d2137]">{pageTitle}</h1>
        <div className="hidden sm:block">
          <LiveClock />
        </div>
      </div>

      {/* Left side: search, notifications, user */}
      <div className="flex items-center gap-3">
        {/* Search */}
        <div className="relative hidden md:block">
          <Search className="w-4 h-4 absolute top-1/2 -translate-y-1/2 end-3 text-[#94a3b8]" />
          <input
            ref={searchInputRef}
            type="search"
            placeholder="بحث..."
            value={searchQuery}
            onChange={(e) => {
              setSearchQuery(e.target.value);
              setSearchOpen(true);
            }}
            onFocus={() => setSearchOpen(true)}
            onBlur={() => setTimeout(() => setSearchOpen(false), 200)}
            onKeyDown={(e) => {
              if (e.key === "Enter") handleSearch(e);
              if (e.key === "Escape") {
                setSearchQuery("");
                setSearchOpen(false);
              }
            }}
            className="h-9 pe-9 ps-4 text-[13px] rounded-lg border-[1.5px] border-[#dce8f5] bg-[#f7fafd] focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]/30 focus:border-[#3d7ab5] w-[220px] placeholder:text-[#94a3b8]"
          />
          {/* Search dropdown */}
          {searchOpen && filteredRoutes.length > 0 && (
            <div className="absolute top-full mt-1 right-0 w-[220px] bg-white border border-[#dce8f5] rounded-xl shadow-lg z-50 overflow-hidden">
              {filteredRoutes.map((route) => (
                <button
                  key={route.path}
                  onMouseDown={() => {
                    router.push(route.path);
                    setSearchQuery("");
                    setSearchOpen(false);
                  }}
                  className="w-full text-right px-4 py-2.5 text-sm text-[#0d2137] hover:bg-[#f0f5fb] transition-colors flex items-center gap-2"
                >
                  <Search className="w-3.5 h-3.5 text-[#94a3b8]" />
                  {route.label}
                </button>
              ))}
            </div>
          )}
        </div>

        {/* Notifications */}
        <SmartAlertsPanel />

        {/* User avatar */}
        <div
          className="w-[38px] h-[38px] rounded-full flex items-center justify-center text-white text-[13px] font-bold cursor-pointer border-2 border-[#dce8f5]"
          style={{ backgroundColor: user?.doctorColor ?? "#3d7ab5" }}
          title={user?.doctorName ?? user?.username}
        >
          {user?.doctorInitials ?? user?.username?.charAt(0).toUpperCase() ?? "م"}
        </div>
      </div>
    </header>
  );
}
