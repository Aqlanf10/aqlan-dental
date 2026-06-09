/**
 * JourneyContextMenu — Right-click context menu for patient journey items
 * in the Daily Operations page.
 *
 * Shows journey-specific actions: intake, queue, call, enter room,
 * quick payment, draft invoice, complete visit, book follow-up, WhatsApp, etc.
 * Actions are filtered based on patient status, next action, and user role.
 */

"use client";

import { useEffect, useRef, useCallback } from "react";
import { createPortal } from "react-dom";
import {
  UserCheck, ClipboardList, Phone, DoorOpen, CreditCard,
  CalendarPlus, MessageCircle, UserX, XCircle,
  Eye, CheckCircle, PanelRight, Wallet, Copy,
  FlaskConical, LogOut,
} from "lucide-react";
import type { TodayJourneyItem } from "../_lib/constants";
import { normalizePhone, NAVY } from "../_lib/constants";

export interface ContextMenuPosition {
  x: number;
  y: number;
}

interface MenuItem {
  icon: React.ReactNode;
  label: string;
  action: () => void;
  color?: string;
  divider?: boolean;
  show: boolean;
  shortcut?: string;
  danger?: boolean;
}

interface JourneyContextMenuProps {
  item: TodayJourneyItem | null;
  position: ContextMenuPosition | null;
  isDoctor: boolean;
  canProcessCheckout: boolean;
  onClose: () => void;
  onIntake: (item: TodayJourneyItem) => void;
  onSendToQueue: (item: TodayJourneyItem) => void;
  onCallPatient: (item: TodayJourneyItem) => void;
  onEnterRoom: (item: TodayJourneyItem) => void;
  onQuickPayment: (item: TodayJourneyItem) => void;
  onCreateDraftInvoice: (item: TodayJourneyItem) => void;
  onCompleteVisit: (item: TodayJourneyItem) => void;
  onCreateLabOrder: (item: TodayJourneyItem) => void;
  onLeftWithoutCompletion: (item: TodayJourneyItem) => void;
  onBookAppointment: (item: TodayJourneyItem) => void;
  onWhatsApp: (item: TodayJourneyItem) => void;
  onNoShow: (item: TodayJourneyItem) => void;
  onCancel: (item: TodayJourneyItem) => void;
  onViewPatient: (item: TodayJourneyItem) => void;
  onOpenSidePanel: (item: TodayJourneyItem) => void;
}

export default function JourneyContextMenu({
  item,
  position,
  isDoctor,
  canProcessCheckout,
  onClose,
  onIntake,
  onSendToQueue,
  onCallPatient,
  onEnterRoom,
  onQuickPayment,
  onCreateDraftInvoice,
  onCompleteVisit,
  onCreateLabOrder,
  onLeftWithoutCompletion,
  onBookAppointment,
  onWhatsApp,
  onNoShow,
  onCancel,
  onViewPatient,
  onOpenSidePanel,
}: JourneyContextMenuProps) {
  const menuRef = useRef<HTMLDivElement>(null);

  const close = useCallback(() => {
    onClose();
  }, [onClose]);

  useEffect(() => {
    const handleMouseDown = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        close();
      }
    };
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") close();
    };
    document.addEventListener("mousedown", handleMouseDown);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("mousedown", handleMouseDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [close]);

  if (!item || !position) return null;

  // Adjust position to avoid viewport overflow
  const menuWidth = 240;
  const menuHeight = 450;
  const vw = window.innerWidth;
  const vh = window.innerHeight;
  let x = position.x;
  let y = position.y;
  if (x + menuWidth > vw) x = vw - menuWidth - 8;
  if (y + menuHeight > vh) y = vh - menuHeight - 8;
  if (x < 8) x = 8;
  if (y < 8) y = 8;

  // ── Determine available actions based on status ──
  const canAct =
    item.appointmentStatus !== "Cancelled" &&
    item.appointmentStatus !== "Completed" &&
    item.appointmentStatus !== "NoShow";

  const isReadyForCheckout =
    item.nextAction === "Checkout" || item.checkoutStatus === "ReadyForCheckout";

  const copyPhone = () => {
    if (item.patientPhone) {
      navigator.clipboard.writeText(item.patientPhone).catch(() => {});
      // Could show a toast here but we don't import toast to keep the component pure
    }
    close();
  };

  const menuItems: MenuItem[] = [
    // ── Navigation ──
    {
      icon: <Eye className="w-4 h-4" />,
      label: "فتح ملف المريض",
      action: () => { onViewPatient(item); close(); },
      shortcut: "",
      show: true,
    },
    {
      icon: <PanelRight className="w-4 h-4" />,
      label: "فتح لوحة التفاصيل",
      action: () => { onOpenSidePanel(item); close(); },
      show: true,
      divider: true,
    },

    // ── Journey progression ──
    {
      icon: <UserCheck className="w-4 h-4" />,
      label: "تسجيل وصول (Intake)",
      action: () => { onIntake(item); close(); },
      color: "#16a34a",
      show: canAct && item.nextAction === "Intake",
      shortcut: "",
    },
    {
      icon: <ClipboardList className="w-4 h-4" />,
      label: "إضافة للانتظار",
      action: () => { onSendToQueue(item); close(); },
      color: "#f5922e",
      show: canAct && item.nextAction === "SendToQueue",
    },
    {
      icon: <Phone className="w-4 h-4" />,
      label: "نداء المريض",
      action: () => { onCallPatient(item); close(); },
      color: "#d97706",
      show: canAct && !!item.queueItemId && (item.queueStatus === "Waiting" || item.nextAction === "CallPatient"),
      shortcut: "",
    },
    {
      icon: <DoorOpen className="w-4 h-4" />,
      label: "دخول الغرفة",
      action: () => { onEnterRoom(item); close(); },
      color: "#9333ea",
      show: canAct && !!item.queueItemId && (item.queueStatus === "Called" || item.nextAction === "EnterRoom"),
      divider: true,
    },

    // ── Financial ──
    {
      icon: <CreditCard className="w-4 h-4" />,
      label: "دفعة مالية سريعة",
      action: () => { onQuickPayment(item); close(); },
      color: "#22c55e",
      show: canProcessCheckout,
    },
    {
      icon: <Wallet className="w-4 h-4" />,
      label: "فاتورة مسودة",
      action: () => { onCreateDraftInvoice(item); close(); },
      color: "#3d7ab5",
      show: canProcessCheckout && isReadyForCheckout,
    },
    {
      icon: <CheckCircle className="w-4 h-4" />,
      label: "التحصيل والخروج",
      action: () => { onCompleteVisit(item); close(); },
      color: "#16a34a",
      show: canProcessCheckout && isReadyForCheckout,
      divider: true,
    },

    // ── Clinical operations ──
    {
      icon: <FlaskConical className="w-4 h-4" />,
      label: "طلب معمل",
      action: () => { onCreateLabOrder(item); close(); },
      color: "#0891b2",
      show: canAct && !!item.patientId,
    },

    // ── Communication ──
    {
      icon: <MessageCircle className="w-4 h-4" />,
      label: "واتساب",
      action: () => { onWhatsApp(item); close(); },
      color: "#25D366",
      show: !isDoctor && !!item.patientPhone,
    },
    {
      icon: <Phone className="w-4 h-4" />,
      label: "اتصال هاتفي",
      action: () => {
        const phone = normalizePhone(item.patientPhone ?? "");
        if (phone) window.open(`tel:${phone}`, "_self");
        close();
      },
      show: !!item.patientPhone,
    },
    {
      icon: <Copy className="w-4 h-4" />,
      label: "نسخ رقم الهاتف",
      action: copyPhone,
      show: !!item.patientPhone,
      divider: true,
    },

    // ── Scheduling ──
    {
      icon: <CalendarPlus className="w-4 h-4" />,
      label: "حجز موعد متابعة",
      action: () => { onBookAppointment(item); close(); },
      color: "#3d7ab5",
      show: true,
      divider: true,
    },

    {
      icon: <LogOut className="w-4 h-4" />,
      label: "خرج بدون إكمال",
      action: () => { onLeftWithoutCompletion(item); close(); },
      color: "#dc2626",
      show: canAct && !!item.visitId,
      danger: true,
    },

    // ── Negative actions ──
    {
      icon: <UserX className="w-4 h-4" />,
      label: "تسجيل لم يحضر",
      action: () => { onNoShow(item); close(); },
      color: "#ef4444",
      show: canAct && (item.appointmentStatus === "Scheduled" || item.appointmentStatus === "Confirmed"),
      danger: true,
    },
    {
      icon: <XCircle className="w-4 h-4" />,
      label: "إلغاء الموعد",
      action: () => { onCancel(item); close(); },
      color: "#6b7280",
      show: canAct && item.appointmentStatus !== "NoShow",
      danger: true,
    },
  ].filter((m) => m.show);

  // Status label mapping
  const statusLabel: Record<string, string> = {
    Scheduled: "مجدول",
    Confirmed: "مؤكد",
    Arrived: "وصل",
    Waiting: "في الانتظار",
    Called: "تم النداء",
    InRoom: "داخل الغرفة",
    InProgress: "جاري العلاج",
    Completed: "مكتمل",
    Cancelled: "ملغي",
    NoShow: "لم يحضر",
  };

  const actionLabel: Record<string, string> = {
    Intake: "تسجيل وصول",
    SendToQueue: "إضافة للانتظار",
    CallPatient: "نداء المريض",
    EnterRoom: "دخول الغرفة",
    StartVisit: "بدء الزيارة",
    InProgress: "جاري العلاج",
    Checkout: "الحساب والخروج",
    Handoff: "تسليم للاستقبال",
    None: "لا يوجد إجراء",
  };

  const statusColor: Record<string, string> = {
    Scheduled: "#3d7ab5",
    Confirmed: "#2563eb",
    Arrived: "#16a34a",
    Waiting: "#f5922e",
    Called: "#d97706",
    InRoom: "#9333ea",
    InProgress: "#dc2626",
    Completed: "#16a34a",
    Cancelled: "#6b7280",
    NoShow: "#ef4444",
  };

  const menu = (
    <div
      ref={menuRef}
      style={{ position: "fixed", top: y, left: x, zIndex: 9999, minWidth: menuWidth }}
      className="bg-white rounded-2xl shadow-2xl border py-1 text-sm animate-fade-in-up"
      dir="rtl"
    >
      {/* Header — Patient info */}
      <div className="px-3 py-2.5 border-b" style={{ borderColor: "#f1f5f9" }}>
        <div className="flex items-center gap-2">
          <div
            className="w-8 h-8 rounded-full flex items-center justify-center text-[10px] font-bold"
            style={{ background: NAVY + "12", color: NAVY }}
          >
            {item.patientName
              .split(" ")
              .filter(Boolean)
              .length >= 2
              ? item.patientName.split(" ")[0][0] + item.patientName.split(" ")[1][0]
              : item.patientName[0]}
          </div>
          <div className="flex-1 min-w-0">
            <p className="font-bold text-[13px] truncate" style={{ color: NAVY }}>
              {item.patientName}
            </p>
            <div className="flex items-center gap-1.5 mt-0.5">
              <span
                className="text-[10px] font-bold px-1.5 py-px rounded"
                style={{
                  background: (statusColor[item.appointmentStatus] ?? "#94a3b8") + "12",
                  color: statusColor[item.appointmentStatus] ?? "#94a3b8",
                }}
              >
                {statusLabel[item.appointmentStatus] ?? item.appointmentStatus}
              </span>
              <span className="text-[10px] font-medium" style={{ color: "#94a3b8" }}>
                → {actionLabel[item.nextAction] ?? item.nextAction}
              </span>
            </div>
          </div>
        </div>
      </div>

      {/* Menu items */}
      {menuItems.map((m, i) => (
        <div key={i}>
          {m.divider && i > 0 && <div className="h-px mx-2 my-1" style={{ background: "#f1f5f9" }} />}
          <button
            onClick={m.action}
            className={`w-full flex items-center justify-between gap-3 px-3 py-2 transition-all text-[13px] font-medium ${
              m.danger
                ? "hover:bg-red-50 text-red-600"
                : "hover:bg-gray-50"
            }`}
            style={!m.danger ? { color: m.color ?? "#475569" } : undefined}
          >
            <span className="flex items-center gap-2.5">
              <span className="flex-shrink-0 opacity-70">{m.icon}</span>
              {m.label}
            </span>
            {m.shortcut && (
              <span
                className="text-[10px] font-mono px-1.5 py-px rounded"
                style={{ background: "#f1f5f9", color: "#94a3b8" }}
              >
                {m.shortcut}
              </span>
            )}
          </button>
        </div>
      ))}

      {/* Footer — service info */}
      <div className="px-3 py-2 border-t mt-1" style={{ borderColor: "#f1f5f9" }}>
        <p className="text-[10px]" style={{ color: "#94a3b8" }}>
          {item.doctorName} {item.serviceName ? `· ${item.serviceName}` : ""} {item.roomName ? `· ${item.roomName}` : ""}
        </p>
      </div>
    </div>
  );

  if (typeof document === "undefined") return null;
  return createPortal(menu, document.body);
}
