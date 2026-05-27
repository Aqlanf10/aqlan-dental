"use client";
import { Bell, Search, X, User, Calendar, GitBranch, CheckCheck, Trash2, LogOut, KeyRound, MessageCircle } from "lucide-react";
import { useAuthStore } from "@/stores/authStore";
import { useRouter } from "next/navigation";
import type { UserDto } from "@/types/auth";
import api from "@/lib/api";
import { useEffect, useRef, useState, useCallback } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useUnreadCount } from "@/hooks/useMessaging";

/* ─── Live Clock — matches ZIP ─────────────────────────────────────────────── */
function LiveClock() {
  const [now, setNow] = useState(new Date());
  useEffect(() => {
    const t = setInterval(() => setNow(new Date()), 1000);
    return () => clearInterval(t);
  }, []);

  const days = ['الأحد','الإثنين','الثلاثاء','الأربعاء','الخميس','الجمعة','السبت'];
  const months = ['يناير','فبراير','مارس','أبريل','مايو','يونيو','يوليو','أغسطس','سبتمبر','أكتوبر','نوفمبر','ديسمبر'];
  const hh = now.getHours().toString().padStart(2, '0');
  const mm = now.getMinutes().toString().padStart(2, '0');
  const ss = now.getSeconds().toString().padStart(2, '0');
  const dayName = days[now.getDay()];
  const dateStr = `${now.getDate()} ${months[now.getMonth()]} ${now.getFullYear()}`;

  return (
    <div
      className="hidden md:flex flex-col items-center rounded-[10px] px-3 py-1"
      style={{ background: "#eef3f9", border: "1px solid #dce8f5", minWidth: 140 }}
    >
      <div className="text-lg font-extrabold leading-tight" style={{ color: "#0d2137", letterSpacing: 1, fontFamily: "monospace" }}>
        {hh}<span style={{ opacity: now.getSeconds() % 2 === 0 ? 1 : 0.3, transition: "opacity 0.3s" }}>:</span>{mm}<span className="text-xs opacity-50">:{ss}</span>
      </div>
      <div className="text-[10px] font-semibold" style={{ color: "#94a3b8" }}>{dayName} · {dateStr}</div>
    </div>
  );
}

/* ─── Types ──────────────────────────────────────────────────────────────── */
interface SearchResult {
  patients:     { id: string; fullName: string; patientNumber: string; phoneNumber?: string }[];
  appointments: { id: string; patientName: string; appointmentType?: string; appointmentDate: string; doctorName?: string }[];
  orthoCases:   { id: string; caseNumber: string; patientName: string; status: string }[];
}

interface NotificationItem {
  id: string;
  type?: string;
  title?: string;
  body?: string;
  isRead: boolean;
  createdAt: string;
  relatedEntity?: string;
  relatedId?: string;
}

const ENTITY_URL: Record<string, (id: string) => string> = {
  Appointment: () => "/appointments",
  Patient:     (id) => `/patients/${id}`,
  Payment:     (id) => `/finance/payments/${id}`,
  Contract:    (id) => `/finance/contracts/${id}`,
  LabOrder:    () => "/lab",
  OrthoCase:   (id) => `/ortho/${id}`,
};

/* ─── Notification type icons ────────────────────────────────────────────── */
function timeAgo(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const min  = Math.floor(diff / 60_000);
  if (min < 1)  return "الآن";
  if (min < 60) return `منذ ${min} دقيقة`;
  const hr = Math.floor(min / 60);
  if (hr < 24)  return `منذ ${hr} ساعة`;
  return `منذ ${Math.floor(hr / 24)} يوم`;
}

/* ─── Component ──────────────────────────────────────────────────────────── */
export function Topbar() {
  const { user } = useAuthStore();
  const router   = useRouter();

  /* Search state */
  const [query,        setQuery]        = useState("");
  const [searchResults, setSearchResults] = useState<SearchResult | null>(null);
  const [searchOpen,   setSearchOpen]   = useState(false);
  const [searching,    setSearching]    = useState(false);
  const searchRef = useRef<HTMLDivElement>(null);
  const searchInputRef = useRef<HTMLInputElement>(null);
  const searchTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  /* Notification state */
  const [notifications, setNotifications] = useState<NotificationItem[]>([]);
  const [unreadCount,   setUnreadCount]   = useState(0);
  const [notifOpen,     setNotifOpen]     = useState(false);
  const [notifLoading,  setNotifLoading]  = useState(false);
  const notifRef = useRef<HTMLDivElement>(null);

  /* ── Search ── */
  const runSearch = useCallback(async (q: string) => {
    if (q.length < 2) { setSearchResults(null); setSearchOpen(false); return; }
    setSearching(true);
    try {
      const { data } = await api.get<SearchResult>(`/api/search?q=${encodeURIComponent(q)}&limit=4`);
      setSearchResults(data);
      setSearchOpen(true);
    } catch {
      setSearchResults(null);
    } finally {
      setSearching(false);
    }
  }, []);

  const handleSearchInput = (e: React.ChangeEvent<HTMLInputElement>) => {
    const val = e.target.value;
    setQuery(val);
    if (searchTimer.current) clearTimeout(searchTimer.current);
    searchTimer.current = setTimeout(() => runSearch(val), 300);
  };

  /* ── Notification unread count — SignalR handles real-time updates ── */
  const { data: notifData } = useQuery({
    queryKey: ["notificationUnreadCount"],
    queryFn: async () => {
      const { data } = await api.get<{ count: number }>("/api/notifications/unread-count");
      return data;
    },
    staleTime: 120_000,       // 2 min — rely on SignalR for invalidation
    refetchInterval: false,   // No polling — SignalR pushes updates instantly
    refetchOnWindowFocus: true, // Safety net on tab focus
  });

  // Keep local unreadCount in sync with the query
  const queryClient = useQueryClient();
  useEffect(() => {
    if (notifData?.count !== undefined) setUnreadCount(notifData.count);
  }, [notifData]);

  /* ── Message unread count with polling ── */
  const { data: msgUnreadData } = useUnreadCount();

  /* ── Open notifications dropdown ── */
  const openNotifications = async () => {
    if (notifOpen) { setNotifOpen(false); return; }
    setNotifOpen(true);
    setNotifLoading(true);
    try {
      const { data } = await api.get<{ data: NotificationItem[]; unreadCount: number }>(
        "/api/notifications?page=1&pageSize=15"
      );
      setNotifications(data.data);
      setUnreadCount(data.unreadCount);
    } catch {
      setNotifications([]);
    } finally {
      setNotifLoading(false);
    }
  };

  const markAllRead = async () => {
    await api.put("/api/notifications/read-all").catch((e) => { console.error("[Topbar] Failed to mark all notifications read:", e); });
    setNotifications(prev => prev.map(n => ({ ...n, isRead: true })));
    setUnreadCount(0);
    queryClient.invalidateQueries({ queryKey: ["notificationUnreadCount"] });
  };

  const markRead = async (id: string) => {
    await api.put(`/api/notifications/${id}/read`).catch((e) => { console.error("[Topbar] Failed to mark notification read:", e); });
    setNotifications(prev => prev.map(n => n.id === id ? { ...n, isRead: true } : n));
    setUnreadCount(prev => Math.max(0, prev - 1));
    queryClient.invalidateQueries({ queryKey: ["notificationUnreadCount"] });
  };

  const handleNotifClick = async (n: NotificationItem) => {
    if (!n.isRead) await markRead(n.id);
    if (n.relatedEntity && n.relatedId) {
      const urlFn = ENTITY_URL[n.relatedEntity];
      if (urlFn) {
        setNotifOpen(false);
        router.push(urlFn(n.relatedId));
      }
    }
  };

  const deleteNotif = async (id: string) => {
    await api.delete(`/api/notifications/${id}`).catch((e) => { console.error("[Topbar] Failed to delete notification:", e); });
    setNotifications(prev => prev.filter(n => n.id !== id));
  };

  /* ── Click outside to close ── */
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (searchRef.current && !searchRef.current.contains(e.target as Node)) {
        setSearchOpen(false);
      }
      if (notifRef.current && !notifRef.current.contains(e.target as Node)) {
        setNotifOpen(false);
      }
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  /* ── Ctrl+K / Cmd+K shortcut to focus search ── */
  useEffect(() => {
    const handleKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key === "k") {
        e.preventDefault();
        searchInputRef.current?.focus();
        searchInputRef.current?.select();
      }
      if (e.key === "Escape") {
        setSearchOpen(false);
        searchInputRef.current?.blur();
      }
    };
    document.addEventListener("keydown", handleKey);
    return () => document.removeEventListener("keydown", handleKey);
  }, []);

  const hasResults = searchResults && (
    searchResults.patients.length > 0 ||
    searchResults.appointments.length > 0 ||
    searchResults.orthoCases.length > 0
  );

  return (
    <header
      className="h-16 flex items-center justify-between px-6 flex-shrink-0"
      style={{ background: "#fff", borderBottom: "1px solid #e8f0f9" }}
    >
      {/* Title */}
      <div className="text-lg font-extrabold" style={{ color: "#0d2137" }}>
        مركز د. عقلان الكامل — تعز
      </div>

      {/* Right controls */}
      <div className="flex items-center gap-3.5">
        {/* Live Clock */}
        <LiveClock />

        {/* ── Message icon with unread badge ── */}
        <button
          onClick={() => router.push("/messages")}
          className="relative w-[38px] h-[38px] rounded-lg flex items-center justify-center transition-colors"
          style={{ background: "#eef3f9", color: "#64748b" }}
          title="الرسائل"
        >
          <MessageCircle className="w-[18px] h-[18px]" />
          {msgUnreadData && msgUnreadData.totalUnread > 0 && (
            <span
              className="absolute rounded-full flex items-center justify-center text-white text-[9px] font-bold leading-none"
              style={{
                top: 4, right: 4,
                minWidth: 16, height: 16,
                background: "#3d7ab5",
                padding: "0 3px",
              }}
            >
              {msgUnreadData.totalUnread > 99 ? "99+" : msgUnreadData.totalUnread}
            </span>
          )}
        </button>

        {/* ── Search ── */}
        <div ref={searchRef} className="relative hidden md:block">
          <input
            ref={searchInputRef}
            type="search"
            value={query}
            onChange={handleSearchInput}
            onFocus={() => hasResults && setSearchOpen(true)}
            placeholder="بحث سريع... (Ctrl+K)"
            className="pe-9 ps-3 py-[7px] text-[13px] rounded-lg outline-none"
            style={{
              width: 220,
              border: "1.5px solid #dce8f5",
              background: "#f7fafd",
              color: "#0d2137",
              direction: "rtl",
              fontFamily: "Tajawal",
            }}
          />
          <span className="absolute left-2.5 top-1/2 -translate-y-1/2" style={{ color: "#94a3b8" }}>
            <Search className="w-[15px] h-[15px]" />
          </span>
          {query && (
            <button
              onClick={() => { setQuery(""); setSearchResults(null); setSearchOpen(false); }}
              className="absolute right-2.5 top-1/2 -translate-y-1/2"
              style={{ color: "#94a3b8" }}
            >
              <X className="w-3.5 h-3.5" />
            </button>
          )}

          {/* Search dropdown */}
          {searchOpen && (
            <div className="absolute top-full mt-2 end-0 w-80 bg-white rounded-xl z-50 overflow-hidden" style={{ boxShadow: "0 8px 30px rgba(0,0,0,0.12)", border: "1px solid #e8f0f9" }}>
              {searching ? (
                <div className="p-4 text-center text-sm" style={{ color: "#94a3b8" }}>جارٍ البحث...</div>
              ) : !hasResults ? (
                <div className="p-4 text-center text-sm" style={{ color: "#94a3b8" }}>لا توجد نتائج لـ &ldquo;{query}&rdquo;</div>
              ) : (
                <div className="py-1">
                  {/* Patients */}
                  {searchResults!.patients.length > 0 && (
                    <div>
                      <div className="px-3 py-1.5 text-xs font-semibold" style={{ color: "#94a3b8", background: "#f7fafd" }}>المرضى</div>
                      {searchResults!.patients.map(p => (
                        <button
                          key={p.id}
                          onClick={() => { router.push(`/patients/${p.id}`); setSearchOpen(false); setQuery(""); }}
                          className="w-full flex items-center gap-3 px-3 py-2.5 transition text-start"
                          onMouseEnter={(e) => (e.currentTarget.style.background = "#f7fafd")}
                          onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
                        >
                          <div className="w-7 h-7 rounded-full flex items-center justify-center flex-shrink-0" style={{ background: "#3d7ab518" }}>
                            <User className="w-3.5 h-3.5" style={{ color: "#3d7ab5" }} />
                          </div>
                          <div>
                            <p className="text-sm font-medium" style={{ color: "#0d2137" }}>{p.fullName}</p>
                            <p className="text-xs" style={{ color: "#94a3b8" }}>{p.patientNumber} {p.phoneNumber && `· ${p.phoneNumber}`}</p>
                          </div>
                        </button>
                      ))}
                    </div>
                  )}
                  {/* Ortho cases */}
                  {searchResults!.orthoCases.length > 0 && (
                    <div>
                      <div className="px-3 py-1.5 text-xs font-semibold" style={{ color: "#94a3b8", background: "#f7fafd" }}>التقويم</div>
                      {searchResults!.orthoCases.map(c => (
                        <button
                          key={c.id}
                          onClick={() => { router.push(`/ortho/${c.id}`); setSearchOpen(false); setQuery(""); }}
                          className="w-full flex items-center gap-3 px-3 py-2.5 transition text-start"
                          onMouseEnter={(e) => (e.currentTarget.style.background = "#f7fafd")}
                          onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
                        >
                          <div className="w-7 h-7 rounded-full flex items-center justify-center flex-shrink-0" style={{ background: "#a855f718" }}>
                            <GitBranch className="w-3.5 h-3.5" style={{ color: "#a855f7" }} />
                          </div>
                          <div>
                            <p className="text-sm font-medium" style={{ color: "#0d2137" }}>{c.caseNumber} — {c.patientName}</p>
                            <p className="text-xs" style={{ color: "#94a3b8" }}>{c.status}</p>
                          </div>
                        </button>
                      ))}
                    </div>
                  )}
                  {/* Appointments */}
                  {searchResults!.appointments.length > 0 && (
                    <div>
                      <div className="px-3 py-1.5 text-xs font-semibold" style={{ color: "#94a3b8", background: "#f7fafd" }}>المواعيد</div>
                      {searchResults!.appointments.map(a => (
                        <button
                          key={a.id}
                          onClick={() => { router.push(`/appointments/${a.id}`); setSearchOpen(false); setQuery(""); }}
                          className="w-full flex items-center gap-3 px-3 py-2.5 transition text-start"
                          onMouseEnter={(e) => (e.currentTarget.style.background = "#f7fafd")}
                          onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
                        >
                          <div className="w-7 h-7 rounded-full flex items-center justify-center flex-shrink-0" style={{ background: "#3d7ab518" }}>
                            <Calendar className="w-3.5 h-3.5" style={{ color: "#3d7ab5" }} />
                          </div>
                          <div>
                            <p className="text-sm font-medium" style={{ color: "#0d2137" }}>{a.patientName}</p>
                            <p className="text-xs" style={{ color: "#94a3b8" }}>{a.appointmentDate} {a.doctorName && `· ${a.doctorName}`}</p>
                          </div>
                        </button>
                      ))}
                    </div>
                  )}
                </div>
              )}
            </div>
          )}
        </div>

        {/* ── Notifications Bell ── */}
        <div ref={notifRef} className="relative">
          <button
            onClick={openNotifications}
            className="relative w-[38px] h-[38px] rounded-lg flex items-center justify-center transition-colors"
            style={{ background: "#eef3f9", color: "#64748b" }}
          >
            <Bell className="w-[18px] h-[18px]" />
            {unreadCount > 0 && (
              <span
                className="absolute rounded-full"
                style={{
                  top: 6, right: 6, width: 8, height: 8,
                  background: "#ef4444",
                  border: "2px solid #fff",
                }}
              />
            )}
          </button>

          {/* Notifications dropdown */}
          {notifOpen && (
            <div
              className="absolute top-full mt-2 end-0 w-[340px] bg-white rounded-xl z-50 overflow-hidden"
              style={{ boxShadow: "0 8px 30px rgba(0,0,0,0.12)", border: "1px solid #e8f0f9" }}
            >
              {/* Header */}
              <div className="flex items-center justify-between px-4 py-3" style={{ borderBottom: "1px solid #e8f0f9" }}>
                <span className="font-bold text-sm" style={{ color: "#0d2137" }}>الإشعارات</span>
                {unreadCount > 0 && (
                  <button
                    onClick={markAllRead}
                    className="flex items-center gap-1 text-xs transition"
                    style={{ color: "#3d7ab5" }}
                  >
                    <CheckCheck className="w-3.5 h-3.5" />
                    تحديد الكل كمقروء
                  </button>
                )}
              </div>

              {/* List */}
              <div className="max-h-96 overflow-y-auto">
                {notifLoading ? (
                  <div className="space-y-3 p-4">
                    {[1,2,3].map(i => <div key={i} className="h-12 rounded-lg animate-pulse" style={{ background: "#f1f5f9" }} />)}
                  </div>
                ) : notifications.length === 0 ? (
                  <div className="py-10 text-center text-sm" style={{ color: "#94a3b8" }}>
                    <Bell className="w-8 h-8 mx-auto mb-2 opacity-30" />
                    لا توجد إشعارات
                  </div>
                ) : (
                  notifications.map(n => (
                    <div
                      key={n.id}
                      onClick={() => handleNotifClick(n)}
                      className="flex items-start gap-3 px-4 py-3 cursor-pointer group transition"
                      style={{
                        background: !n.isRead ? "#eef3f9" : "transparent",
                        borderBottom: "1px solid #f1f5f9",
                      }}
                      onMouseEnter={(e) => (e.currentTarget.style.background = "#f7fafd")}
                      onMouseLeave={(e) => (e.currentTarget.style.background = !n.isRead ? "#eef3f9" : "transparent")}
                    >
                      <div
                        className="w-2 h-2 rounded-full mt-1.5 flex-shrink-0"
                        style={{ background: !n.isRead ? "#3d7ab5" : "#cbd5e1" }}
                      />
                      <div className="flex-1 min-w-0">
                        <p className={`text-[13px] ${!n.isRead ? "font-semibold" : ""}`} style={{ color: "#0d2137" }}>
                          {n.title}
                        </p>
                        <p className="text-xs mt-0.5 line-clamp-2" style={{ color: "#94a3b8" }}>{n.body}</p>
                        <p className="text-[11px] mt-1" style={{ color: "#94a3b8" }}>{timeAgo(n.createdAt)}</p>
                      </div>
                      <button
                        onClick={(e) => { e.stopPropagation(); deleteNotif(n.id); }}
                        className="opacity-0 group-hover:opacity-100 p-1 rounded transition"
                        style={{ color: "#94a3b8" }}
                      >
                        <Trash2 className="w-3.5 h-3.5" />
                      </button>
                    </div>
                  ))
                )}
              </div>
            </div>
          )}
        </div>

        {/* User menu */}
        <UserMenu user={user} router={router} />
      </div>
    </header>
  );
}

/* ─── UserMenu ───────────────────────────────────────────────────────────────── */
function UserMenu({ user, router }: { user: UserDto | null; router: ReturnType<typeof useRouter> }) {
  const { logout } = useAuthStore();
  const [open, setOpen]           = useState(false);
  const [changePw, setChangePw]   = useState(false);
  const [pwForm, setPwForm]       = useState({ current: "", next: "", confirm: "" });
  const [pwError, setPwError]     = useState("");
  const [pwSaving, setPwSaving]   = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const h = (e: MouseEvent) => { if (ref.current && !ref.current.contains(e.target as Node)) { setOpen(false); setChangePw(false); } };
    document.addEventListener("mousedown", h);
    return () => document.removeEventListener("mousedown", h);
  }, []);

  const handleLogout = async () => {
    await logout();
    router.replace("/login");
  };

  const handleChangePw = async (e: React.FormEvent) => {
    e.preventDefault();
    if (pwForm.next !== pwForm.confirm) { setPwError("كلمة المرور الجديدة غير متطابقة"); return; }
    if (pwForm.next.length < 8) { setPwError("يجب أن تكون 8 أحرف على الأقل"); return; }
    setPwSaving(true); setPwError("");
    try {
      await api.post("/api/users/me/change-password", { currentPassword: pwForm.current, newPassword: pwForm.next });
      setChangePw(false); setOpen(false);
      setPwForm({ current: "", next: "", confirm: "" });
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setPwError(msg ?? "حدث خطأ");
    } finally {
      setPwSaving(false);
    }
  };

  return (
    <div ref={ref} className="relative">
      <button
        onClick={() => { setOpen(o => !o); setChangePw(false); }}
        className="flex items-center gap-1.5 rounded-lg transition px-1 py-0.5"
        onMouseEnter={(e) => (e.currentTarget.style.background = "#f7fafd")}
        onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
      >
        <div
          className="w-[38px] h-[38px] rounded-full flex items-center justify-center text-white text-[13px] font-bold"
          style={{ backgroundColor: user?.doctorColor ?? "#3d7ab5", border: "2px solid #dce8f5" }}
        >
          {user?.doctorInitials ?? user?.username?.charAt(0).toUpperCase() ?? "م"}
        </div>
      </button>

      {open && (
        <div
          className="absolute top-full mt-2 end-0 w-56 bg-white rounded-xl z-50 overflow-hidden"
          style={{ boxShadow: "0 8px 30px rgba(0,0,0,0.12)", border: "1px solid #e8f0f9" }}
        >
          {/* User info — matches ZIP */}
          <div className="px-4 py-3" style={{ background: "#f7fafd", borderBottom: "1px solid #f1f5f9" }}>
            <p className="font-bold text-sm" style={{ color: "#0d2137" }}>{user?.doctorName ?? user?.username}</p>
            <p className="text-xs mt-0.5" style={{ color: "#94a3b8" }}>{user?.username}</p>
          </div>

          {!changePw ? (
            <div className="py-1">
              <button
                onClick={() => setChangePw(true)}
                className="w-full flex items-center gap-2.5 px-4 py-2.5 text-sm transition text-start"
                style={{ color: "#0d2137" }}
                onMouseEnter={(e) => (e.currentTarget.style.background = "#f7fafd")}
                onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
              >
                <KeyRound className="w-[15px] h-[15px]" style={{ color: "#64748b" }} />
                تغيير كلمة المرور
              </button>
              <div style={{ borderTop: "1px solid #f1f5f9" }}>
                <button
                  onClick={handleLogout}
                  className="w-full flex items-center gap-2.5 px-4 py-2.5 text-sm transition text-start"
                  style={{ color: "#ef4444" }}
                  onMouseEnter={(e) => (e.currentTarget.style.background = "#fef2f2")}
                  onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
                >
                  <LogOut className="w-[15px] h-[15px]" style={{ color: "#ef4444" }} />
                  تسجيل الخروج
                </button>
              </div>
            </div>
          ) : (
            <form onSubmit={handleChangePw} className="p-4 space-y-3">
              <p className="text-sm font-semibold" style={{ color: "#0d2137" }}>تغيير كلمة المرور</p>
              {pwError && <p className="text-xs px-3 py-1.5 rounded-lg" style={{ color: "#ef4444", background: "#fef2f2" }}>{pwError}</p>}
              <input
                type="password" placeholder="كلمة المرور الحالية"
                value={pwForm.current} onChange={(e) => setPwForm({ ...pwForm, current: e.target.value })}
                className="w-full text-sm px-3 py-2 rounded-lg outline-none"
                style={{ border: "1px solid #dce8f5" }}
                onFocus={(e) => (e.currentTarget.style.borderColor = "#3d7ab5")}
                onBlur={(e) => (e.currentTarget.style.borderColor = "#dce8f5")}
              />
              <input
                type="password" placeholder="كلمة المرور الجديدة (8 أحرف+)"
                value={pwForm.next} onChange={(e) => setPwForm({ ...pwForm, next: e.target.value })}
                className="w-full text-sm px-3 py-2 rounded-lg outline-none"
                style={{ border: "1px solid #dce8f5" }}
                onFocus={(e) => (e.currentTarget.style.borderColor = "#3d7ab5")}
                onBlur={(e) => (e.currentTarget.style.borderColor = "#dce8f5")}
              />
              <input
                type="password" placeholder="تأكيد كلمة المرور الجديدة"
                value={pwForm.confirm} onChange={(e) => setPwForm({ ...pwForm, confirm: e.target.value })}
                className="w-full text-sm px-3 py-2 rounded-lg outline-none"
                style={{ border: "1px solid #dce8f5" }}
                onFocus={(e) => (e.currentTarget.style.borderColor = "#3d7ab5")}
                onBlur={(e) => (e.currentTarget.style.borderColor = "#dce8f5")}
              />
              <div className="flex gap-2 pt-1">
                <button type="submit" disabled={pwSaving}
                  className="flex-1 py-2 text-sm font-medium rounded-lg text-white transition"
                  style={{ background: "#3d7ab5" }}
                  onMouseEnter={(e) => (e.currentTarget.style.background = "#2d5e8e")}
                  onMouseLeave={(e) => (e.currentTarget.style.background = "#3d7ab5")}
                >
                  {pwSaving ? "جارٍ الحفظ..." : "حفظ"}
                </button>
                <button type="button" onClick={() => { setChangePw(false); setPwError(""); }}
                  className="flex-1 py-2 text-sm font-medium rounded-lg transition"
                  style={{ border: "1px solid #dce8f5", color: "#64748b" }}
                  onMouseEnter={(e) => (e.currentTarget.style.background = "#f7fafd")}
                  onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
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
