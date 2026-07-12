"use client";

import { useEffect, useRef, useState } from "react";
import { AlertTriangle, Bell, CheckCheck, RefreshCw, Trash2 } from "lucide-react";
import { useRouter } from "next/navigation";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import api from "@/lib/api";
import { extractErrorMessage } from "@/lib/errors";
import { financeV3CollectionsUrl, financeV3ContractsUrl } from "@/lib/financeRoutes";

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
  Patient: (id) => `/patients/${id}`,
  Payment: () => financeV3CollectionsUrl(),
  Contract: () => financeV3ContractsUrl(),
  LabOrder: () => "/lab",
  OrthoCase: (id) => `/ortho/${id}`,
};

const UNREAD_COUNT_QUERY_KEY = ["notificationUnreadCount"] as const;

function timeAgo(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const min = Math.floor(diff / 60_000);
  if (min < 1) return "الآن";
  if (min < 60) return `منذ ${min} دقيقة`;
  const hr = Math.floor(min / 60);
  if (hr < 24) return `منذ ${hr} ساعة`;
  return `منذ ${Math.floor(hr / 24)} يوم`;
}

export function TopbarNotifications() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const rootRef = useRef<HTMLDivElement>(null);
  const actionInFlightRef = useRef<string | null>(null);
  const loadControllerRef = useRef<AbortController | null>(null);
  const loadRequestSequenceRef = useRef(0);

  const [notifications, setNotifications] = useState<NotificationItem[]>([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [pendingAction, setPendingAction] = useState<string | null>(null);

  const { data: unreadData } = useQuery({
    queryKey: UNREAD_COUNT_QUERY_KEY,
    queryFn: async ({ signal }) => {
      const { data } = await api.get<{ count: number }>("/api/notifications/unread-count", {
        signal,
      });
      return data;
    },
    staleTime: 120_000,
    refetchInterval: false,
    refetchOnWindowFocus: true,
  });

  useEffect(() => {
    if (unreadData?.count !== undefined) setUnreadCount(unreadData.count);
  }, [unreadData]);

  const cancelActiveLoad = () => {
    loadRequestSequenceRef.current += 1;
    loadControllerRef.current?.abort();
    loadControllerRef.current = null;
    setLoading(false);
  };

  const cancelUnreadCountRefresh = async () => {
    if (typeof queryClient.cancelQueries !== "function") return;

    await queryClient.cancelQueries(
      { queryKey: UNREAD_COUNT_QUERY_KEY },
      { silent: true },
    );
  };

  const loadNotifications = async () => {
    const requestId = ++loadRequestSequenceRef.current;
    loadControllerRef.current?.abort();
    const controller = new AbortController();
    loadControllerRef.current = controller;

    setLoading(true);
    setLoadError(null);
    setActionError(null);

    try {
      const { data } = await api.get<{ data: NotificationItem[]; unreadCount: number }>(
        "/api/notifications?page=1&pageSize=15",
        { signal: controller.signal },
      );

      if (controller.signal.aborted || requestId !== loadRequestSequenceRef.current) return;
      setNotifications(data.data);
      setUnreadCount(data.unreadCount);
      setLoadError(null);
    } catch (error) {
      if (controller.signal.aborted || requestId !== loadRequestSequenceRef.current) return;
      setLoadError(extractErrorMessage(error, "تعذّر تحميل الإشعارات"));
    } finally {
      if (requestId === loadRequestSequenceRef.current) {
        setLoading(false);
        if (loadControllerRef.current === controller) loadControllerRef.current = null;
      }
    }
  };

  const closeDropdown = () => {
    cancelActiveLoad();
    setOpen(false);
  };

  const toggleOpen = () => {
    if (open) {
      closeDropdown();
      return;
    }

    setOpen(true);
    void loadNotifications();
  };

  const beginAction = (key: string): boolean => {
    if (actionInFlightRef.current) return false;
    actionInFlightRef.current = key;
    setPendingAction(key);
    setActionError(null);
    return true;
  };

  const finishAction = (key: string) => {
    if (actionInFlightRef.current !== key) return;
    actionInFlightRef.current = null;
    setPendingAction(null);
  };

  const markAllRead = async () => {
    const actionKey = "read-all";
    if (!beginAction(actionKey)) return;

    try {
      await api.put("/api/notifications/read-all");
      cancelActiveLoad();
      await cancelUnreadCountRefresh();
      setNotifications((current) =>
        current.map((notification) => ({ ...notification, isRead: true })),
      );
      setUnreadCount(0);
      queryClient.invalidateQueries({ queryKey: UNREAD_COUNT_QUERY_KEY });
    } catch (error) {
      setActionError(extractErrorMessage(error, "تعذّر تعليم الإشعارات كمقروءة"));
    } finally {
      finishAction(actionKey);
    }
  };

  const markRead = async (id: string): Promise<boolean> => {
    const actionKey = `read:${id}`;
    if (!beginAction(actionKey)) return false;

    try {
      await api.put(`/api/notifications/${id}/read`);
      cancelActiveLoad();
      await cancelUnreadCountRefresh();
      setNotifications((current) =>
        current.map((notification) =>
          notification.id === id ? { ...notification, isRead: true } : notification,
        ),
      );
      setUnreadCount((current) => Math.max(0, current - 1));
      queryClient.invalidateQueries({ queryKey: UNREAD_COUNT_QUERY_KEY });
      return true;
    } catch (error) {
      setActionError(extractErrorMessage(error, "تعذّر تعليم الإشعار كمقروء"));
      return false;
    } finally {
      finishAction(actionKey);
    }
  };

  const handleNotificationClick = async (notification: NotificationItem) => {
    if (!notification.isRead) await markRead(notification.id);
    if (!notification.relatedEntity || !notification.relatedId) return;

    const urlForEntity = ENTITY_URL[notification.relatedEntity];
    if (!urlForEntity) return;

    closeDropdown();
    router.push(urlForEntity(notification.relatedId));
  };

  const deleteNotification = async (notification: NotificationItem) => {
    const actionKey = `delete:${notification.id}`;
    if (!beginAction(actionKey)) return;

    try {
      await api.delete(`/api/notifications/${notification.id}`);
      cancelActiveLoad();
      await cancelUnreadCountRefresh();
      setNotifications((current) =>
        current.filter((item) => item.id !== notification.id),
      );
      if (!notification.isRead) {
        setUnreadCount((current) => Math.max(0, current - 1));
        queryClient.invalidateQueries({ queryKey: UNREAD_COUNT_QUERY_KEY });
      }
    } catch (error) {
      setActionError(extractErrorMessage(error, "تعذّر حذف الإشعار"));
    } finally {
      finishAction(actionKey);
    }
  };

  useEffect(() => {
    const handleOutside = (event: MouseEvent) => {
      if (rootRef.current && !rootRef.current.contains(event.target as Node)) {
        loadRequestSequenceRef.current += 1;
        loadControllerRef.current?.abort();
        loadControllerRef.current = null;
        setLoading(false);
        setOpen(false);
      }
    };
    document.addEventListener("mousedown", handleOutside);
    return () => document.removeEventListener("mousedown", handleOutside);
  }, []);

  useEffect(() => {
    return () => {
      loadRequestSequenceRef.current += 1;
      loadControllerRef.current?.abort();
      loadControllerRef.current = null;
    };
  }, []);

  const showInitialSkeleton = loading && notifications.length === 0;
  const mutationPending = pendingAction !== null;

  return (
    <div ref={rootRef} className="relative">
      <button
        type="button"
        onClick={toggleOpen}
        aria-label="الإشعارات"
        aria-expanded={open}
        className="relative w-[38px] h-[38px] rounded-lg flex items-center justify-center transition-colors"
        style={{ background: "#eef3f9", color: "#64748b" }}
      >
        <Bell className="w-[18px] h-[18px]" />
        {unreadCount > 0 && (
          <span
            data-testid="notification-unread-indicator"
            className="absolute rounded-full"
            style={{
              top: 6,
              right: 6,
              width: 8,
              height: 8,
              background: "#ef4444",
              border: "2px solid #fff",
            }}
          />
        )}
      </button>

      {open && (
        <div
          className="absolute top-full mt-2 end-0 w-[340px] bg-white rounded-xl z-50 overflow-hidden"
          style={{ boxShadow: "0 8px 30px rgba(0,0,0,0.12)", border: "1px solid #e8f0f9" }}
        >
          <div className="flex items-center justify-between px-4 py-3" style={{ borderBottom: "1px solid #e8f0f9" }}>
            <span className="font-bold text-sm" style={{ color: "#0d2137" }}>الإشعارات</span>
            {unreadCount > 0 && (
              <button
                type="button"
                onClick={() => void markAllRead()}
                disabled={mutationPending}
                className="flex items-center gap-1 text-xs transition disabled:cursor-not-allowed disabled:opacity-60"
                style={{ color: "#3d7ab5" }}
              >
                <CheckCheck className="w-3.5 h-3.5" />
                {pendingAction === "read-all" ? "جارٍ التحديث..." : "تحديد الكل كمقروء"}
              </button>
            )}
          </div>

          <div className="max-h-96 overflow-y-auto">
            {showInitialSkeleton ? (
              <div className="space-y-3 p-4" aria-label="جارٍ تحميل الإشعارات">
                {[1, 2, 3].map((item) => (
                  <div key={item} className="h-12 rounded-lg animate-pulse" style={{ background: "#f1f5f9" }} />
                ))}
              </div>
            ) : (
              <>
                {loadError && (
                  <div
                    role="alert"
                    className="m-3 flex items-start gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2.5 text-xs text-red-700"
                  >
                    <AlertTriangle className="mt-0.5 h-4 w-4 flex-shrink-0" />
                    <div className="min-w-0 flex-1">
                      <p className="font-bold">{loadError}</p>
                      <p className="mt-0.5 text-[11px] text-red-600">
                        {notifications.length > 0
                          ? "لم يتم استبدال الإشعارات المحمّلة سابقًا."
                          : "يمكنك إعادة المحاولة دون فقدان أي بيانات."}
                      </p>
                    </div>
                    <button
                      type="button"
                      onClick={() => void loadNotifications()}
                      disabled={loading}
                      className="inline-flex items-center gap-1 rounded-md border border-red-200 bg-white px-2 py-1 font-bold disabled:opacity-60"
                    >
                      <RefreshCw className={`h-3.5 w-3.5 ${loading ? "animate-spin" : ""}`} />
                      إعادة المحاولة
                    </button>
                  </div>
                )}

                {actionError && (
                  <div
                    role="alert"
                    className="m-3 flex items-center gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2.5 text-xs font-bold text-red-700"
                  >
                    <AlertTriangle className="h-4 w-4 flex-shrink-0" />
                    {actionError}
                  </div>
                )}

                {loading && notifications.length > 0 && (
                  <div className="px-4 py-2 text-center text-[11px] font-semibold text-slate-500">
                    جارٍ تحديث الإشعارات...
                  </div>
                )}

                {notifications.length === 0 && !loadError ? (
                  <div className="py-10 text-center text-sm" style={{ color: "#94a3b8" }}>
                    <Bell className="w-8 h-8 mx-auto mb-2 opacity-30" />
                    لا توجد إشعارات
                  </div>
                ) : (
                  notifications.map((notification) => (
                    <div
                      key={notification.id}
                      onClick={() => void handleNotificationClick(notification)}
                      aria-busy={pendingAction === `read:${notification.id}`}
                      className="flex items-start gap-3 px-4 py-3 cursor-pointer group transition"
                      style={{
                        background: !notification.isRead ? "#eef3f9" : "transparent",
                        borderBottom: "1px solid #f1f5f9",
                      }}
                      onMouseEnter={(event) => (event.currentTarget.style.background = "#f7fafd")}
                      onMouseLeave={(event) =>
                        (event.currentTarget.style.background = !notification.isRead ? "#eef3f9" : "transparent")
                      }
                    >
                      <div
                        className="w-2 h-2 rounded-full mt-1.5 flex-shrink-0"
                        style={{ background: !notification.isRead ? "#3d7ab5" : "#cbd5e1" }}
                      />
                      <div className="flex-1 min-w-0">
                        <p className={`text-[13px] ${!notification.isRead ? "font-semibold" : ""}`} style={{ color: "#0d2137" }}>
                          {notification.title}
                        </p>
                        <p className="text-xs mt-0.5 line-clamp-2" style={{ color: "#94a3b8" }}>
                          {notification.body}
                        </p>
                        <p className="text-[11px] mt-1" style={{ color: "#94a3b8" }}>
                          {timeAgo(notification.createdAt)}
                        </p>
                      </div>
                      <button
                        type="button"
                        aria-label={`حذف الإشعار ${notification.title ?? ""}`}
                        disabled={mutationPending}
                        onClick={(event) => {
                          event.stopPropagation();
                          void deleteNotification(notification);
                        }}
                        className="opacity-0 group-hover:opacity-100 p-1 rounded transition disabled:cursor-not-allowed disabled:opacity-40"
                        style={{ color: "#94a3b8" }}
                      >
                        <Trash2 className="w-3.5 h-3.5" />
                      </button>
                    </div>
                  ))
                )}
              </>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
