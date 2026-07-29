"use client";

import { KeyRound, LogOut, MessageCircle } from "lucide-react";
import { memo, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { useAuthStore } from "@/stores/authStore";
import type { UserDto } from "@/types/auth";
import api from "@/lib/api";
import { useUnreadCount } from "@/hooks/useMessaging";
import { TopbarSearch } from "@/components/layout/TopbarSearch";
import { TopbarNotifications } from "@/components/layout/TopbarNotifications";

/* ─── Live Clock — matches ZIP ─────────────────────────────────────────────── */
// FE-31: Wrapped in React.memo so the parent Topbar does not re-render every second
// when the clock's internal `now` state updates (the clock has no props anyway).
const LiveClock = memo(function LiveClock() {
  const [now, setNow] = useState(new Date());
  useEffect(() => {
    const timer = setInterval(() => setNow(new Date()), 1000);
    return () => clearInterval(timer);
  }, []);

  const days = ["الأحد", "الإثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت"];
  const months = ["يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو", "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"];
  const hours = now.getHours().toString().padStart(2, "0");
  const minutes = now.getMinutes().toString().padStart(2, "0");
  const seconds = now.getSeconds().toString().padStart(2, "0");
  const dayName = days[now.getDay()];
  const dateString = `${now.getDate()} ${months[now.getMonth()]} ${now.getFullYear()}`;

  return (
    <div
      className="hidden md:flex flex-col items-center rounded-[10px] px-3 py-1"
      style={{ background: "#eef3f9", border: "1px solid #dce8f5", minWidth: 140 }}
    >
      <div
        className="text-lg font-extrabold leading-tight"
        style={{ color: "#0d2137", letterSpacing: 1, fontFamily: "monospace" }}
      >
        {hours}
        <span
          style={{
            opacity: now.getSeconds() % 2 === 0 ? 1 : 0.3,
            transition: "opacity 0.3s",
          }}
        >
          :
        </span>
        {minutes}
        <span className="text-xs opacity-50">:{seconds}</span>
      </div>
      <div className="text-[10px] font-semibold" style={{ color: "#94a3b8" }}>
        {dayName} · {dateString}
      </div>
    </div>
  );
});

export function Topbar() {
  const { user } = useAuthStore();
  const router = useRouter();
  const { data: messageUnreadData } = useUnreadCount();

  return (
    <header
      className="h-16 flex items-center justify-between px-6 flex-shrink-0"
      style={{ background: "#fff", borderBottom: "1px solid #e8f0f9" }}
    >
      <div className="text-lg font-extrabold" style={{ color: "#0d2137" }}>
        مركز د. عقلان الكامل — تعز
      </div>

      <div className="flex items-center gap-3.5">
        <LiveClock />

        <button
          type="button"
          onClick={() => router.push("/messages")}
          className="relative w-[38px] h-[38px] rounded-lg flex items-center justify-center transition-colors"
          style={{ background: "#eef3f9", color: "#64748b" }}
          title="الرسائل"
        >
          <MessageCircle className="w-[18px] h-[18px]" />
          {messageUnreadData && messageUnreadData.totalUnread > 0 && (
            <span
              className="absolute rounded-full flex items-center justify-center text-white text-[9px] font-bold leading-none"
              style={{
                top: 4,
                right: 4,
                minWidth: 16,
                height: 16,
                background: "#3d7ab5",
                padding: "0 3px",
              }}
            >
              {messageUnreadData.totalUnread > 99 ? "99+" : messageUnreadData.totalUnread}
            </span>
          )}
        </button>

        <TopbarSearch />
        <TopbarNotifications />
        <UserMenu user={user} router={router} />
      </div>
    </header>
  );
}

function UserMenu({ user, router }: { user: UserDto | null; router: ReturnType<typeof useRouter> }) {
  const { logout } = useAuthStore();
  const [open, setOpen] = useState(false);
  const [changePasswordOpen, setChangePasswordOpen] = useState(false);
  const [passwordForm, setPasswordForm] = useState({ current: "", next: "", confirm: "" });
  const [passwordError, setPasswordError] = useState("");
  const [passwordSaving, setPasswordSaving] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handleOutside = (event: MouseEvent) => {
      if (rootRef.current && !rootRef.current.contains(event.target as Node)) {
        setOpen(false);
        setChangePasswordOpen(false);
      }
    };
    document.addEventListener("mousedown", handleOutside);
    return () => document.removeEventListener("mousedown", handleOutside);
  }, []);

  const handleLogout = async () => {
    await logout();
    router.replace("/login");
  };

  const handleChangePassword = async (event: React.FormEvent) => {
    event.preventDefault();
    if (passwordForm.next !== passwordForm.confirm) {
      setPasswordError("كلمة المرور الجديدة غير متطابقة");
      return;
    }
    if (passwordForm.next.length < 8) {
      setPasswordError("يجب أن تكون 8 أحرف على الأقل");
      return;
    }

    setPasswordSaving(true);
    setPasswordError("");
    try {
      await api.post("/api/users/me/change-password", {
        currentPassword: passwordForm.current,
        newPassword: passwordForm.next,
      });
      setChangePasswordOpen(false);
      setOpen(false);
      setPasswordForm({ current: "", next: "", confirm: "" });
    } catch (error: unknown) {
      const message = (error as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setPasswordError(message ?? "حدث خطأ");
    } finally {
      setPasswordSaving(false);
    }
  };

  return (
    <div ref={rootRef} className="relative">
      <button
        type="button"
        onClick={() => {
          setOpen((current) => !current);
          setChangePasswordOpen(false);
        }}
        className="flex items-center gap-1.5 rounded-lg transition px-1 py-0.5"
        onMouseEnter={(event) => (event.currentTarget.style.background = "#f7fafd")}
        onMouseLeave={(event) => (event.currentTarget.style.background = "transparent")}
      >
        <div
          className="w-[38px] h-[38px] rounded-full flex items-center justify-center text-white text-[13px] font-bold"
          style={{
            backgroundColor: user?.doctorColor ?? "#3d7ab5",
            border: "2px solid #dce8f5",
          }}
        >
          {user?.doctorInitials ?? user?.username?.charAt(0).toUpperCase() ?? "م"}
        </div>
      </button>

      {open && (
        <div
          className="absolute top-full mt-2 end-0 w-56 bg-white rounded-xl z-50 overflow-hidden"
          style={{ boxShadow: "0 8px 30px rgba(0,0,0,0.12)", border: "1px solid #e8f0f9" }}
        >
          <div
            className="px-4 py-3"
            style={{ background: "#f7fafd", borderBottom: "1px solid #f1f5f9" }}
          >
            <p className="font-bold text-sm" style={{ color: "#0d2137" }}>
              {user?.doctorName ?? user?.username}
            </p>
            <p className="text-xs mt-0.5" style={{ color: "#94a3b8" }}>
              {user?.username}
            </p>
          </div>

          {!changePasswordOpen ? (
            <div className="py-1">
              <button
                type="button"
                onClick={() => setChangePasswordOpen(true)}
                className="w-full flex items-center gap-2.5 px-4 py-2.5 text-sm transition text-start"
                style={{ color: "#0d2137" }}
                onMouseEnter={(event) => (event.currentTarget.style.background = "#f7fafd")}
                onMouseLeave={(event) => (event.currentTarget.style.background = "transparent")}
              >
                <KeyRound className="w-[15px] h-[15px]" style={{ color: "#64748b" }} />
                تغيير كلمة المرور
              </button>
              <div style={{ borderTop: "1px solid #f1f5f9" }}>
                <button
                  type="button"
                  onClick={handleLogout}
                  className="w-full flex items-center gap-2.5 px-4 py-2.5 text-sm transition text-start"
                  style={{ color: "#ef4444" }}
                  onMouseEnter={(event) => (event.currentTarget.style.background = "#fef2f2")}
                  onMouseLeave={(event) => (event.currentTarget.style.background = "transparent")}
                >
                  <LogOut className="w-[15px] h-[15px]" style={{ color: "#ef4444" }} />
                  تسجيل الخروج
                </button>
              </div>
            </div>
          ) : (
            <form onSubmit={handleChangePassword} className="p-4 space-y-3">
              <p className="text-sm font-semibold" style={{ color: "#0d2137" }}>
                تغيير كلمة المرور
              </p>
              {passwordError && (
                <p
                  className="text-xs px-3 py-1.5 rounded-lg"
                  style={{ color: "#ef4444", background: "#fef2f2" }}
                >
                  {passwordError}
                </p>
              )}
              <input
                type="password"
                placeholder="كلمة المرور الحالية"
                value={passwordForm.current}
                onChange={(event) =>
                  setPasswordForm({ ...passwordForm, current: event.target.value })
                }
                className="w-full text-sm px-3 py-2 rounded-lg outline-none"
                style={{ border: "1px solid #dce8f5" }}
                onFocus={(event) => (event.currentTarget.style.borderColor = "#3d7ab5")}
                onBlur={(event) => (event.currentTarget.style.borderColor = "#dce8f5")}
              />
              <input
                type="password"
                placeholder="كلمة المرور الجديدة (8 أحرف+)"
                value={passwordForm.next}
                onChange={(event) => setPasswordForm({ ...passwordForm, next: event.target.value })}
                className="w-full text-sm px-3 py-2 rounded-lg outline-none"
                style={{ border: "1px solid #dce8f5" }}
                onFocus={(event) => (event.currentTarget.style.borderColor = "#3d7ab5")}
                onBlur={(event) => (event.currentTarget.style.borderColor = "#dce8f5")}
              />
              <input
                type="password"
                placeholder="تأكيد كلمة المرور الجديدة"
                value={passwordForm.confirm}
                onChange={(event) =>
                  setPasswordForm({ ...passwordForm, confirm: event.target.value })
                }
                className="w-full text-sm px-3 py-2 rounded-lg outline-none"
                style={{ border: "1px solid #dce8f5" }}
                onFocus={(event) => (event.currentTarget.style.borderColor = "#3d7ab5")}
                onBlur={(event) => (event.currentTarget.style.borderColor = "#dce8f5")}
              />
              <div className="flex gap-2 pt-1">
                <button
                  type="submit"
                  disabled={passwordSaving}
                  className="flex-1 py-2 text-sm font-medium rounded-lg text-white transition"
                  style={{ background: "#3d7ab5" }}
                  onMouseEnter={(event) => (event.currentTarget.style.background = "#2d5e8e")}
                  onMouseLeave={(event) => (event.currentTarget.style.background = "#3d7ab5")}
                >
                  {passwordSaving ? "جارٍ الحفظ..." : "حفظ"}
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setChangePasswordOpen(false);
                    setPasswordError("");
                  }}
                  className="flex-1 py-2 text-sm font-medium rounded-lg transition"
                  style={{ border: "1px solid #dce8f5", color: "#64748b" }}
                  onMouseEnter={(event) => (event.currentTarget.style.background = "#f7fafd")}
                  onMouseLeave={(event) => (event.currentTarget.style.background = "transparent")}
                >
                  إلغاء
                </button>
              </div>
            </form>
          )}
        </div>
      )}
    </div>
  );
}
