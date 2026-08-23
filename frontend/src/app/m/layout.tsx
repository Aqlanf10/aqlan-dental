"use client";

import { useEffect, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import Link from "next/link";
import { CalendarDays, FlaskConical, LogOut } from "lucide-react";
import { useAuthStore } from "@/stores/authStore";
import { resolveSessionBoot } from "@/lib/sessionBoot";
import { cn } from "@/lib/utils";

/**
 * The phone screen: a deliberately small app covering the two things the clinic is drowning
 * in — today's appointments and lab work that is late.
 *
 * It is a separate shell rather than the dashboard shrunk down. The dashboard is built around
 * a sidebar, wide tables and a mouse; scaling that to a phone gives something technically
 * responsive and practically unusable at a chairside. Here the whole surface is two lists and
 * a bottom bar reachable with one thumb.
 *
 * Session handling reuses the same bounded gate as the dashboard (CORE-F-014): whatever the
 * network does, this ends in either a rendered screen or a redirect to login — never a
 * spinner that stays forever, which on a phone with patchy signal is not a rare case.
 */
export default function MobileLayout({ children }: { children: React.ReactNode }) {
  const { fetchMe, user } = useAuthStore();
  const router = useRouter();
  const pathname = usePathname();
  const [isReady, setIsReady] = useState(false);

  useEffect(() => {
    const boot = async () => {
      const outcome = await resolveSessionBoot({
        fetchMe,
        isAuthenticated: () => useAuthStore.getState().isAuthenticated,
      });
      if (outcome.status === "redirect-to-login") router.replace("/login");
    };

    void boot().finally(() => setIsReady(true));
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  if (!isReady) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-[#eef3f9]">
        <p className="text-sm text-gray-500">جارٍ التحميل…</p>
      </div>
    );
  }

  const tabs = [
    { href: "/m/appointments", label: "المواعيد", icon: CalendarDays },
    { href: "/m/lab", label: "المعامل", icon: FlaskConical },
  ];

  return (
    <div className="min-h-screen bg-[#eef3f9] flex flex-col" dir="rtl">
      <header className="sticky top-0 z-20 bg-cyan-800 text-white px-4 py-3 flex items-center justify-between">
        <div className="min-w-0">
          <p className="text-sm font-bold truncate">مركز د. عقلان الكامل</p>
          <p className="text-[11px] text-cyan-100 truncate">
            {user?.doctorName ?? user?.username ?? ""}
          </p>
        </div>
        <button
          type="button"
          onClick={async () => {
            await useAuthStore.getState().logout();
            router.replace("/login");
          }}
          aria-label="خروج"
          className="p-2 -m-2 text-cyan-100"
        >
          <LogOut className="w-5 h-5" />
        </button>
      </header>

      {/* pb leaves room for the fixed bar; the extra inset keeps it clear of the home indicator */}
      <main className="flex-1 px-3 pt-3 pb-24">{children}</main>

      <nav
        className="fixed bottom-0 inset-x-0 z-20 bg-white border-t border-gray-200 flex"
        style={{ paddingBottom: "env(safe-area-inset-bottom)" }}
      >
        {tabs.map((tab) => {
          const Icon = tab.icon;
          const active = pathname.startsWith(tab.href);
          return (
            <Link
              key={tab.href}
              href={tab.href}
              className={cn(
                // min-h-14 so the target is comfortably thumb-sized, not a desktop-height row
                "flex-1 min-h-14 flex flex-col items-center justify-center gap-0.5 text-xs",
                active ? "text-cyan-700 font-bold" : "text-gray-500",
              )}
            >
              <Icon className="w-5 h-5" />
              {tab.label}
            </Link>
          );
        })}
      </nav>
    </div>
  );
}
