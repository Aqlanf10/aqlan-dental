"use client";

import { useState } from "react";
import { ShieldAlert, LogOut, Loader2 } from "lucide-react";
import { useAuthStore } from "@/stores/authStore";

export function ImpersonationBanner() {
  const { isImpersonating, user, stopImpersonation } = useAuthStore();
  const [stopping, setStopping] = useState(false);

  if (!isImpersonating) return null;

  const handleStop = async () => {
    setStopping(true);
    try {
      await stopImpersonation();
    } finally {
      setStopping(false);
    }
  };

  return (
    <div
      className="fixed top-0 inset-x-0 z-[60] flex items-center justify-center gap-3 py-2 px-4 text-sm font-medium"
      style={{
        background: "linear-gradient(90deg, #f59e0b 0%, #d97706 100%)",
        color: "#1a1a1a",
        direction: "rtl",
      }}
    >
      <ShieldAlert className="w-4 h-4 flex-shrink-0" />
      <span>
        أنت الآن داخل النظام كـ <strong>{user?.username ?? "مستخدم"}</strong>
        {user?.doctorName && ` (${user.doctorName})`} بواسطة المدير
      </span>
      <button
        onClick={handleStop}
        disabled={stopping}
        className="inline-flex items-center gap-1.5 px-3 py-1 text-xs font-bold rounded-lg transition ms-2"
        style={{
          background: "rgba(0,0,0,0.15)",
          color: "#1a1a1a",
        }}
        onMouseEnter={(e) => (e.currentTarget.style.background = "rgba(0,0,0,0.3)")}
        onMouseLeave={(e) => (e.currentTarget.style.background = "rgba(0,0,0,0.15)")}
      >
        {stopping ? (
          <Loader2 className="w-3.5 h-3.5 animate-spin" />
        ) : (
          <LogOut className="w-3.5 h-3.5" />
        )}
        العودة لحساب المدير
      </button>
    </div>
  );
}
