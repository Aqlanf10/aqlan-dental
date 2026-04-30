"use client";
import { Bell, Search } from "lucide-react";
import { useAuthStore } from "@/stores/authStore";

export function Topbar() {
  const { user } = useAuthStore();

  return (
    <header className="h-14 bg-white border-b border-gray-200 flex items-center justify-between px-4 lg:px-6 flex-shrink-0">
      {/* Branch name */}
      <div className="flex items-center gap-2">
        {/* Spacer for mobile hamburger (fixed positioned) */}
        <div className="lg:hidden w-10" />
        <div className="w-2 h-2 rounded-full bg-green-500" />
        <span className="text-sm text-gray-600 font-medium">
          مركز د. عقلان الكامل — تعز
        </span>
      </div>

      {/* Right controls */}
      <div className="flex items-center gap-3">
        {/* Search */}
        <div className="relative hidden md:block">
          <Search className="w-4 h-4 absolute top-1/2 -translate-y-1/2 end-3 text-gray-400" />
          <input
            type="search"
            placeholder="بحث..."
            className="h-9 pe-9 ps-4 text-sm rounded-lg border border-gray-200 bg-gray-50 focus:outline-none focus:ring-2 focus:ring-clinic-teal w-56"
          />
        </div>

        {/* Notifications */}
        <button className="relative w-9 h-9 rounded-lg hover:bg-gray-100 flex items-center justify-center text-gray-500 transition-colors">
          <Bell className="w-5 h-5" />
          <span className="absolute top-1.5 end-1.5 w-2 h-2 bg-red-500 rounded-full" />
        </button>

        {/* User avatar */}
        <div
          className="w-9 h-9 rounded-full flex items-center justify-center text-white text-sm font-bold cursor-pointer"
          style={{ backgroundColor: user?.doctorColor ?? "#0E7490" }}
          title={user?.doctorName ?? user?.username}
        >
          {user?.doctorInitials ?? user?.username?.charAt(0).toUpperCase() ?? "م"}
        </div>
      </div>
    </header>
  );
}
