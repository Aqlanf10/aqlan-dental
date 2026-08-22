/**
 * StatusCardsBar — Sprint 2 daily-ops improvement.
 *
 * Replaces the old 8px navy text-status bar at the bottom of the daily-ops
 * page with a row of prominent, clickable status cards. Clicking a card
 * filters the view (switches the active sub-tab or module tab).
 *
 * Below the cards, a compact row preserves the SignalR indicator + finance
 * summary + room occupancy + date/time that used to live in the navy bar —
 * so no information is lost.
 *
 * RTL Arabic. Frontend-only.
 */

"use client";

import {
  Clock, Stethoscope, CreditCard, CheckCircle, UserX, Wallet,
} from "lucide-react";
import {
  type DayStats,
  type TabKey,
  fmtRial, fmtDate,
  type RoomOccupancy,
} from "../_lib/constants";

/** Module tab keys (mirrors `ModuleTab` from page.tsx — kept local to avoid a circular import). */
export type ModuleTabKey =
  | "appointments" | "journey" | "queue" | "rooms"
  | "checkout" | "booking" | "lab" | "report";

export interface StatusCardsBarProps {
  dayStats: DayStats;
  /** Count of items with checkoutStatus === "ReadyForCheckout" || nextAction === "Checkout". */
  readyForCheckoutCount: number;
  /** Currently-active sub-tab (used to highlight the matching card). */
  activeTab: TabKey;
  /** Currently-active module tab (used to highlight the checkout card). */
  activeModule: ModuleTabKey;
  /** Select a sub-tab (also implicitly routes to the "appointments" module). */
  onTabSelect: (tab: TabKey) => void;
  /** Select a module tab (e.g. "checkout"). */
  onModuleSelect?: (tab: ModuleTabKey) => void;
  /** SignalR connection state — shown in the compact row below the cards. */
  signalrConnected: boolean;
  /** Filter date (YYYY-MM-DD) — shown in the compact row. */
  filterDate: string;
  /** Room occupancy chips — shown in the compact row when non-empty. */
  roomOccupancy?: RoomOccupancy[];
}

interface CardDef {
  key: string;
  count: number;
  label: string;
  Icon: React.ComponentType<{ className?: string }>;
  accent: string;
  /** Sub-tab to switch to (also routes to the "appointments" module). */
  tab?: TabKey;
  /** Module tab to switch to. */
  module?: ModuleTabKey;
}

export default function StatusCardsBar({
  dayStats,
  readyForCheckoutCount,
  activeTab,
  activeModule,
  onTabSelect,
  onModuleSelect,
  signalrConnected,
  filterDate,
  roomOccupancy = [],
}: StatusCardsBarProps) {
  // Build the list of cards. The no-show card is only rendered when there is
  // at least one no-show today (per the task spec).
  const cards: CardDef[] = [
    {
      key: "queue",
      count: dayStats.waiting,
      label: "المنتظرون",
      Icon: Clock,
      accent: "#f59e2e",
      tab: "queue",
    },
    {
      key: "inClinic",
      count: dayStats.inClinic,
      label: "في الغرف",
      Icon: Stethoscope,
      accent: "#9333ea",
      tab: "inClinic",
    },
    {
      key: "checkout",
      count: readyForCheckoutCount,
      label: "جاهزون للتحصيل",
      Icon: CreditCard,
      accent: "#10b981",
      module: "checkout",
    },
    {
      key: "completed",
      count: dayStats.completed,
      label: "مكتملون",
      Icon: CheckCircle,
      accent: "#3b82f6",
      tab: "completed",
    },
  ];

  if (dayStats.noShow > 0) {
    cards.push({
      key: "noShow",
      count: dayStats.noShow,
      label: "لم يحضر",
      Icon: UserX,
      accent: "#ef4444",
      tab: "overdue",
    });
  }

  const handleClick = (card: CardDef) => {
    if (card.module && onModuleSelect) {
      onModuleSelect(card.module);
      return;
    }
    if (card.tab) {
      onTabSelect(card.tab);
    }
  };

  const isCardActive = (card: CardDef): boolean => {
    if (card.module) return activeModule === card.module;
    // Tab cards are only "active" when the appointments module is showing
    // AND the matching sub-tab is selected.
    return activeModule === "appointments" && activeTab === card.tab;
  };

  return (
    <div
      dir="rtl"
      className="flex-shrink-0 bg-white px-3 pt-3 pb-2 select-none"
      style={{ borderTop: "1px solid #e5e7eb" }}
    >
      {/* ── Status cards row ─────────────────────────────────────────── */}
      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
        {cards.map(card => {
          const { Icon, accent, count, label } = card;
          const active = isCardActive(card);
          return (
            <button
              key={card.key}
              type="button"
              onClick={() => handleClick(card)}
              dir="rtl"
              title={`تصفية: ${label}`}
              className="relative bg-white rounded-xl shadow-sm hover:shadow-md transition-all duration-150 text-start cursor-pointer focus:outline-none focus-visible:ring-2 focus-visible:ring-offset-1"
              style={{
                borderRight: `4px solid ${accent}`,
                borderTop: "1px solid #f1f5f9",
                borderLeft: "1px solid #f1f5f9",
                borderBottom: "1px solid #f1f5f9",
                boxShadow: active ? `0 0 0 2px ${accent}` : undefined,
              }}
            >
              <div className="flex items-center gap-2 px-3 py-2">
                <span
                  className="flex-shrink-0 w-9 h-9 rounded-lg flex items-center justify-center"
                  style={{ background: accent + "12" }}
                >
                  <Icon className="w-4 h-4" />
                  <span className="sr-only">{label}</span>
                </span>
                <div className="flex flex-col min-w-0 flex-1">
                  <span
                    className="text-2xl font-bold leading-none"
                    style={{ color: accent }}
                  >
                    {count}
                  </span>
                  <span className="text-[11px] text-gray-600 font-semibold mt-1 truncate">
                    {label}
                  </span>
                </div>
              </div>
            </button>
          );
        })}
      </div>

      {/* ── Compact row: SignalR + finance + rooms + date ──────────────
          Preserves the info that used to live in the old 8px navy bar so
          nothing is lost. Kept slim (single line, wraps on narrow screens). */}
      <div className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-[11px] font-medium text-gray-600">
        {/* SignalR */}
        <div className="flex items-center gap-1.5">
          <span
            className="w-2 h-2 rounded-full"
            style={{ background: signalrConnected ? "#16a34a" : "#ef4444" }}
          />
          <span>{signalrConnected ? "مباشر" : "غير متصل"}</span>
        </div>

        <span className="hidden sm:inline-block w-px h-3 bg-gray-200" />

        {/* Total appointments */}
        <span>
          <strong className="text-gray-900">{dayStats.totalAppointments}</strong>{" "}
          موعد
        </span>

        {/* Today's collections */}
        <span className="flex items-center gap-1">
          <Wallet className="w-3 h-3 text-emerald-600" />
          <strong className="text-gray-900">{fmtRial(dayStats.todayPayments)}</strong>
        </span>

        {/* Overdue amount (only when non-zero) */}
        {dayStats.overdueAmount > 0 && (
          <span className="flex items-center gap-1" style={{ color: "#dc2626" }}>
            <span aria-hidden>⚠</span>
            <strong>{fmtRial(dayStats.overdueAmount)}</strong>
            <span>متأخر</span>
          </span>
        )}

        {/* Room occupancy chips (compact, max 4) */}
        {roomOccupancy.length > 0 && (
          <>
            <span className="hidden sm:inline-block w-px h-3 bg-gray-200" />
            <div className="flex items-center gap-1.5 flex-wrap">
              <span aria-hidden>🏥</span>
              {roomOccupancy.slice(0, 4).map(room => (
                <span
                  key={room.roomId}
                  className="px-1.5 py-0.5 rounded text-[10px] font-semibold"
                  style={{
                    background: room.isOccupied ? "#f3e8ff" : "#dcfce7",
                    color: room.isOccupied ? "#7c3aed" : "#15803d",
                  }}
                  title={room.isOccupied ? `${room.roomName}: ${room.patientName ?? ""}` : `${room.roomName}: فارغة`}
                >
                  {room.roomName}:{room.isOccupied ? room.patientName ?? "مشغولة" : "فارغة"}
                </span>
              ))}
            </div>
          </>
        )}

        <div className="flex-1" />

        {/* Date + clock */}
        <span>{fmtDate(filterDate)}</span>
        <span>
          {new Date().toLocaleTimeString("ar-YE", { hour: "2-digit", minute: "2-digit" })}
        </span>

        <span className="hidden sm:inline-block w-px h-3 bg-gray-200" />
        <span className="hidden sm:inline" title="اختصارات لوحة المفاتيح">⌨ ؟</span>
      </div>
    </div>
  );
}
