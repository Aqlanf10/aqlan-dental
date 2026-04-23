"use client";
import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  LayoutDashboard, Users, Calendar, GitBranch, Activity,
  Stethoscope, Scissors, ArrowLeftRight, Wallet,
  BarChart2, Package, FlaskConical, Settings, LogOut, Lock,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { useAuthStore } from "@/stores/authStore";
import { useRouter } from "next/navigation";

const NAV_ITEMS = [
  { href: "/",             label: "لوحة التحكم",       icon: LayoutDashboard, active: true },
  { href: "/patients",     label: "المرضى",             icon: Users,           active: true },
  { href: "/appointments", label: "المواعيد",           icon: Calendar,        active: true },
  { href: "/ortho",        label: "التقويم",            icon: GitBranch,       active: false },
  { href: "/ceph",         label: "السيفالومتري",       icon: Activity,        active: false },
  { href: "/general",      label: "طب الأسنان العام",   icon: Stethoscope,     active: false },
  { href: "/surgery",      label: "الجراحة",            icon: Scissors,        active: false },
  { href: "/referrals",    label: "الإحالات",           icon: ArrowLeftRight,  active: false },
  { href: "/finance",      label: "المالية",            icon: Wallet,          active: false },
  { href: "/reports",      label: "التقارير",           icon: BarChart2,       active: false },
  { href: "/inventory",    label: "المخزون",            icon: Package,         active: false },
  { href: "/lab",          label: "المختبر",            icon: FlaskConical,    active: false },
  { href: "/settings",     label: "الإعدادات",          icon: Settings,        active: true },
];

const ROLE_LABELS: Record<string, string> = {
  Admin: "مدير النظام",
  Orthodontist: "أخصائي تقويم",
  GeneralDentist: "طبيب أسنان",
  OralSurgeon: "جراح وجه وفكين",
  Reception: "استقبال",
  Accountant: "محاسب",
  Assistant: "مساعد",
  BranchManager: "مدير فرع",
};

export function Sidebar() {
  const pathname = usePathname();
  const router = useRouter();
  const { user, logout } = useAuthStore();

  const handleLogout = async () => {
    await logout();
    router.push("/login");
  };

  return (
    <aside className="w-64 bg-white border-l border-gray-200 flex flex-col h-full fixed top-0 right-0 z-40 shadow-sm">
      {/* Logo */}
      <div className="p-4 border-b border-gray-100">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 clinic-gradient rounded-xl flex items-center justify-center flex-shrink-0">
            <Stethoscope className="w-5 h-5 text-white" />
          </div>
          <div className="min-w-0">
            <p className="font-bold text-gray-900 text-sm leading-tight truncate">
              مركز د. عقلان الكامل
            </p>
            <p className="text-xs text-gray-400 truncate">Aqlan Dental Pro</p>
          </div>
        </div>
      </div>

      {/* Navigation */}
      <nav className="flex-1 overflow-y-auto py-3 px-2 space-y-0.5">
        {NAV_ITEMS.map(({ href, label, icon: Icon, active }) => {
          const isCurrent = href === "/"
            ? pathname === "/"
            : pathname.startsWith(href);

          return (
            <div key={href}>
              {active ? (
                <Link
                  href={href}
                  className={cn(
                    "flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-all",
                    isCurrent
                      ? "bg-clinic-teal text-white shadow-sm"
                      : "text-gray-600 hover:bg-gray-100 hover:text-gray-900"
                  )}
                >
                  <Icon className="w-4 h-4 flex-shrink-0" />
                  <span>{label}</span>
                </Link>
              ) : (
                <div className="flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm text-gray-400 cursor-not-allowed select-none">
                  <Icon className="w-4 h-4 flex-shrink-0" />
                  <span className="flex-1">{label}</span>
                  <span className="text-xs bg-gray-100 text-gray-400 px-1.5 py-0.5 rounded flex items-center gap-1">
                    <Lock className="w-2.5 h-2.5" />
                    قريباً
                  </span>
                </div>
              )}
            </div>
          );
        })}
      </nav>

      {/* User footer */}
      <div className="p-3 border-t border-gray-100">
        <div className="flex items-center gap-3 mb-2">
          <div
            className="w-9 h-9 rounded-full flex items-center justify-center text-white text-sm font-bold flex-shrink-0"
            style={{ backgroundColor: user?.doctorColor ?? "#0E7490" }}
          >
            {user?.doctorInitials ?? user?.username?.charAt(0).toUpperCase() ?? "م"}
          </div>
          <div className="min-w-0 flex-1">
            <p className="text-sm font-semibold text-gray-900 truncate">
              {user?.doctorName ?? user?.username}
            </p>
            <p className="text-xs text-gray-400 truncate">
              {ROLE_LABELS[user?.role ?? ""] ?? user?.role}
            </p>
          </div>
        </div>
        <button
          onClick={handleLogout}
          className="w-full flex items-center gap-2 px-3 py-2 text-sm text-red-600 hover:bg-red-50 rounded-lg transition-colors"
        >
          <LogOut className="w-4 h-4" />
          <span>تسجيل الخروج</span>
        </button>
      </div>
    </aside>
  );
}
