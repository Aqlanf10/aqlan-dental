"use client";
import { Bell, Search, X, User, Calendar, GitBranch, CheckCheck, Trash2, LogOut, KeyRound, ChevronDown } from "lucide-react";
import { useAuthStore } from "@/stores/authStore";
import { useRouter } from "next/navigation";
import api from "@/lib/api";
import { useEffect, useRef, useState, useCallback } from "react";

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
const TYPE_ICON: Record<string, string> = {
  appointment: "📅",
  payment:     "💰",
  lab:         "🧪",
  system:      "🔔",
};

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

  /* ── Fetch unread count on mount ── */
  useEffect(() => {
    api.get<{ count: number }>("/api/notifications/unread-count")
      .then(r => setUnreadCount(r.data.count))
      .catch(() => {});
  }, []);

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
    await api.put("/api/notifications/read-all").catch(() => {});
    setNotifications(prev => prev.map(n => ({ ...n, isRead: true })));
    setUnreadCount(0);
  };

  const markRead = async (id: string) => {
    await api.put(`/api/notifications/${id}/read`).catch(() => {});
    setNotifications(prev => prev.map(n => n.id === id ? { ...n, isRead: true } : n));
    setUnreadCount(prev => Math.max(0, prev - 1));
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
    await api.delete(`/api/notifications/${id}`).catch(() => {});
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

  const hasResults = searchResults && (
    searchResults.patients.length > 0 ||
    searchResults.appointments.length > 0 ||
    searchResults.orthoCases.length > 0
  );

  return (
    <header className="h-14 bg-white border-b border-gray-200 flex items-center justify-between px-4 lg:px-6 flex-shrink-0">
      {/* Branch name */}
      <div className="flex items-center gap-2">
        <div className="lg:hidden w-10" />
        <div className="w-2 h-2 rounded-full bg-green-500" />
        <span className="text-sm text-gray-600 font-medium">مركز د. عقلان الكامل — تعز</span>
      </div>

      {/* Right controls */}
      <div className="flex items-center gap-3">

        {/* ── Search ── */}
        <div ref={searchRef} className="relative hidden md:block">
          <Search className="w-4 h-4 absolute top-1/2 -translate-y-1/2 end-3 text-gray-400 pointer-events-none" />
          {query && (
            <button
              onClick={() => { setQuery(""); setSearchResults(null); setSearchOpen(false); }}
              className="absolute top-1/2 -translate-y-1/2 start-2 text-gray-400 hover:text-gray-600"
            >
              <X className="w-3.5 h-3.5" />
            </button>
          )}
          <input
            type="search"
            value={query}
            onChange={handleSearchInput}
            onFocus={() => hasResults && setSearchOpen(true)}
            placeholder="بحث عن مريض، موعد..."
            className="h-9 pe-9 ps-4 text-sm rounded-lg border border-gray-200 bg-gray-50 focus:outline-none focus:ring-2 focus:ring-clinic-teal w-60 transition"
          />

          {/* Search dropdown */}
          {searchOpen && (
            <div className="absolute top-full mt-1 end-0 w-80 bg-white rounded-xl border border-gray-200 shadow-lg z-50 overflow-hidden">
              {searching ? (
                <div className="p-4 text-center text-sm text-gray-400">جارٍ البحث...</div>
              ) : !hasResults ? (
                <div className="p-4 text-center text-sm text-gray-400">لا توجد نتائج لـ &ldquo;{query}&rdquo;</div>
              ) : (
                <div className="py-1">
                  {/* Patients */}
                  {searchResults!.patients.length > 0 && (
                    <div>
                      <div className="px-3 py-1.5 text-xs font-semibold text-gray-400 bg-gray-50">المرضى</div>
                      {searchResults!.patients.map(p => (
                        <button
                          key={p.id}
                          onClick={() => { router.push(`/patients/${p.id}`); setSearchOpen(false); setQuery(""); }}
                          className="w-full flex items-center gap-3 px-3 py-2.5 hover:bg-gray-50 text-start transition"
                        >
                          <div className="w-7 h-7 rounded-full bg-teal-100 flex items-center justify-center flex-shrink-0">
                            <User className="w-3.5 h-3.5 text-clinic-teal" />
                          </div>
                          <div>
                            <p className="text-sm font-medium text-gray-900">{p.fullName}</p>
                            <p className="text-xs text-gray-400">{p.patientNumber} {p.phoneNumber && `· ${p.phoneNumber}`}</p>
                          </div>
                        </button>
                      ))}
                    </div>
                  )}
                  {/* Ortho cases */}
                  {searchResults!.orthoCases.length > 0 && (
                    <div>
                      <div className="px-3 py-1.5 text-xs font-semibold text-gray-400 bg-gray-50">التقويم</div>
                      {searchResults!.orthoCases.map(c => (
                        <button
                          key={c.id}
                          onClick={() => { router.push(`/ortho/${c.id}`); setSearchOpen(false); setQuery(""); }}
                          className="w-full flex items-center gap-3 px-3 py-2.5 hover:bg-gray-50 text-start transition"
                        >
                          <div className="w-7 h-7 rounded-full bg-purple-100 flex items-center justify-center flex-shrink-0">
                            <GitBranch className="w-3.5 h-3.5 text-purple-600" />
                          </div>
                          <div>
                            <p className="text-sm font-medium text-gray-900">{c.caseNumber} — {c.patientName}</p>
                            <p className="text-xs text-gray-400">{c.status}</p>
                          </div>
                        </button>
                      ))}
                    </div>
                  )}
                  {/* Appointments */}
                  {searchResults!.appointments.length > 0 && (
                    <div>
                      <div className="px-3 py-1.5 text-xs font-semibold text-gray-400 bg-gray-50">المواعيد</div>
                      {searchResults!.appointments.map(a => (
                        <button
                          key={a.id}
                          onClick={() => { router.push(`/appointments`); setSearchOpen(false); setQuery(""); }}
                          className="w-full flex items-center gap-3 px-3 py-2.5 hover:bg-gray-50 text-start transition"
                        >
                          <div className="w-7 h-7 rounded-full bg-blue-100 flex items-center justify-center flex-shrink-0">
                            <Calendar className="w-3.5 h-3.5 text-blue-600" />
                          </div>
                          <div>
                            <p className="text-sm font-medium text-gray-900">{a.patientName}</p>
                            <p className="text-xs text-gray-400">{a.appointmentDate} {a.doctorName && `· ${a.doctorName}`}</p>
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
            className="relative w-9 h-9 rounded-lg hover:bg-gray-100 flex items-center justify-center text-gray-500 transition-colors"
          >
            <Bell className="w-5 h-5" />
            {unreadCount > 0 && (
              <span className="absolute -top-0.5 -end-0.5 min-w-[18px] h-[18px] bg-red-500 rounded-full flex items-center justify-center text-white text-[10px] font-bold px-1">
                {unreadCount > 99 ? "99+" : unreadCount}
              </span>
            )}
          </button>

          {/* Notifications dropdown */}
          {notifOpen && (
            <div className="absolute top-full mt-2 end-0 w-96 bg-white rounded-xl border border-gray-200 shadow-xl z-50 overflow-hidden">
              {/* Header */}
              <div className="flex items-center justify-between px-4 py-3 border-b border-gray-100">
                <h3 className="text-sm font-bold text-gray-900">
                  الإشعارات {unreadCount > 0 && <span className="text-xs text-clinic-teal">({unreadCount} غير مقروء)</span>}
                </h3>
                {unreadCount > 0 && (
                  <button
                    onClick={markAllRead}
                    className="flex items-center gap-1 text-xs text-clinic-teal hover:opacity-80 transition"
                  >
                    <CheckCheck className="w-3.5 h-3.5" />
                    تحديد الكل كمقروء
                  </button>
                )}
              </div>

              {/* List */}
              <div className="max-h-96 overflow-y-auto divide-y divide-gray-50">
                {notifLoading ? (
                  <div className="space-y-3 p-4">
                    {[1,2,3].map(i => <div key={i} className="h-12 bg-gray-100 rounded-lg animate-pulse" />)}
                  </div>
                ) : notifications.length === 0 ? (
                  <div className="py-10 text-center text-sm text-gray-400">
                    <Bell className="w-8 h-8 mx-auto mb-2 opacity-30" />
                    لا توجد إشعارات
                  </div>
                ) : (
                  notifications.map(n => (
                    <div
                      key={n.id}
                      onClick={() => handleNotifClick(n)}
                      className={`flex items-start gap-3 px-4 py-3 hover:bg-gray-50 transition cursor-pointer group ${!n.isRead ? "bg-blue-50/40" : ""}`}
                    >
                      <span className="text-lg flex-shrink-0 mt-0.5">
                        {TYPE_ICON[n.type ?? "system"] ?? "🔔"}
                      </span>
                      <div className="flex-1 min-w-0">
                        <p className={`text-sm ${!n.isRead ? "font-semibold text-gray-900" : "text-gray-700"}`}>
                          {n.title}
                        </p>
                        <p className="text-xs text-gray-500 mt-0.5 line-clamp-2">{n.body}</p>
                        <p className="text-xs text-gray-400 mt-1">{timeAgo(n.createdAt)}</p>
                      </div>
                      <div className="flex items-center gap-1 flex-shrink-0">
                        {!n.isRead && <span className="w-2 h-2 rounded-full bg-clinic-teal" />}
                        <button
                          onClick={(e) => { e.stopPropagation(); deleteNotif(n.id); }}
                          className="opacity-0 group-hover:opacity-100 p-1 rounded text-gray-400 hover:text-red-500 transition"
                        >
                          <Trash2 className="w-3.5 h-3.5" />
                        </button>
                      </div>
                    </div>
                  ))
                )}
              </div>

              {/* Footer */}
              {notifications.length > 0 && (
                <div className="px-4 py-2.5 border-t border-gray-100 text-center">
                  <button
                    onClick={() => setNotifOpen(false)}
                    className="text-xs text-gray-400 hover:text-gray-600 transition"
                  >
                    إغلاق
                  </button>
                </div>
              )}
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
function UserMenu({ user, router }: { user: ReturnType<typeof useAuthStore>["user"]; router: ReturnType<typeof useRouter> }) {
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
        className="flex items-center gap-1.5 rounded-lg hover:bg-gray-100 transition px-1 py-0.5"
      >
        <div
          className="w-8 h-8 rounded-full flex items-center justify-center text-white text-sm font-bold"
          style={{ backgroundColor: user?.doctorColor ?? "#0E7490" }}
        >
          {user?.doctorInitials ?? user?.username?.charAt(0).toUpperCase() ?? "م"}
        </div>
        <ChevronDown className="w-3 h-3 text-gray-400" />
      </button>

      {open && (
        <div className="absolute top-full mt-2 end-0 w-72 bg-white rounded-xl border border-gray-200 shadow-xl z-50 overflow-hidden">
          {/* User info */}
          <div className="px-4 py-3 border-b border-gray-100 flex items-center gap-3">
            <div
              className="w-10 h-10 rounded-full flex items-center justify-center text-white text-sm font-bold flex-shrink-0"
              style={{ backgroundColor: user?.doctorColor ?? "#0E7490" }}
            >
              {user?.doctorInitials ?? user?.username?.charAt(0).toUpperCase() ?? "م"}
            </div>
            <div className="min-w-0">
              <p className="text-sm font-bold text-gray-900 truncate">{user?.doctorName ?? user?.username}</p>
              <p className="text-xs text-gray-400 truncate font-mono">{user?.username}</p>
            </div>
          </div>

          {!changePw ? (
            <div className="py-1">
              <button
                onClick={() => setChangePw(true)}
                className="w-full flex items-center gap-2.5 px-4 py-2.5 text-sm text-gray-700 hover:bg-gray-50 transition text-start"
              >
                <KeyRound className="w-4 h-4 text-gray-400" />
                تغيير كلمة المرور
              </button>
              <div className="border-t border-gray-100 my-1" />
              <button
                onClick={handleLogout}
                className="w-full flex items-center gap-2.5 px-4 py-2.5 text-sm text-red-600 hover:bg-red-50 transition text-start"
              >
                <LogOut className="w-4 h-4" />
                تسجيل الخروج
              </button>
            </div>
          ) : (
            <form onSubmit={handleChangePw} className="p-4 space-y-3">
              <p className="text-sm font-semibold text-gray-800">تغيير كلمة المرور</p>
              {pwError && <p className="text-xs text-red-600 bg-red-50 px-3 py-1.5 rounded-lg">{pwError}</p>}
              <input
                type="password" placeholder="كلمة المرور الحالية"
                value={pwForm.current} onChange={(e) => setPwForm({ ...pwForm, current: e.target.value })}
                className="w-full text-sm border border-gray-200 rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-clinic-teal"
              />
              <input
                type="password" placeholder="كلمة المرور الجديدة (8 أحرف+)"
                value={pwForm.next} onChange={(e) => setPwForm({ ...pwForm, next: e.target.value })}
                className="w-full text-sm border border-gray-200 rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-clinic-teal"
              />
              <input
                type="password" placeholder="تأكيد كلمة المرور الجديدة"
                value={pwForm.confirm} onChange={(e) => setPwForm({ ...pwForm, confirm: e.target.value })}
                className="w-full text-sm border border-gray-200 rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-clinic-teal"
              />
              <div className="flex gap-2 pt-1">
                <button type="submit" disabled={pwSaving}
                  className="flex-1 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60 transition"
                >
                  {pwSaving ? "جارٍ الحفظ..." : "حفظ"}
                </button>
                <button type="button" onClick={() => { setChangePw(false); setPwError(""); }}
                  className="flex-1 py-2 text-sm font-medium rounded-lg border border-gray-200 text-gray-600 hover:bg-gray-50 transition"
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
