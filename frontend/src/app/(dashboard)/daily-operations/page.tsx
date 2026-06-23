"use client";
import { printScreen } from "@/lib/printUtils";

import { useState, useCallback, useMemo, useEffect, useRef } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import {
  Calendar, ClipboardList, CreditCard, Clock, CheckCircle,
  Stethoscope, AlertTriangle, Search, RefreshCw,
  Globe,
  Wallet, UserPlus, Keyboard, Bell, BellOff,
  Printer, Download, Activity, Megaphone, Building2,
  X, Phone, MessageCircle,
  ClipboardCheck, LogIn, BellRing, CalendarPlus,
  Monitor, MoreHorizontal,
} from "lucide-react";
import { useHasPermission, PERMISSION_KEYS } from "@/hooks/usePermissions";
import { useAuthStore } from "@/stores/authStore";
import { toast } from "@/stores/toastStore";
import { useSignalRClinicQueue } from "@/hooks/useSignalRClinicQueue";
import api from "@/lib/api";
import { NewLabOrderModal } from "@/components/lab/NewLabOrderModal";

import {
  NAVY, BLUE, ORANGE,
  TABS,
  fmtDate, getTodayStr, fmtRial,
  computeDayStats, filterByTab,
  computeRoomOccupancy, getNextPatient,
  isDoctorRole,
  APPT_STATUS_LABELS, STATUS_COLORS, ACTION_LABELS,
  type TodayJourneyItem, type TabKey, type UndoAction,
} from "./_lib/constants";
import type { DailyJourneySummary } from "@/types/journey";

import {
  useTodayJourneyItems,
  usePatientSummary,
  useDoctors,
  useBranches,
  useRooms,
  useServices,
  useClinicSettings,
  useDashboardStats,
  useFinanceSummary,
  useIntake,
  useSendToQueue,
  useCallPatient,
  useRecallPatient,
  useEnterRoom,
  useUpdateAppointmentStatus,
  useCreatePayment,
  useCreateDraftInvoice,
  useCheckout,
  useHandoff,
  useMarkLeftWithoutCompletion,
  useCancelQueue,
  useChangeRoom,
  useCreateAppointment,
  useCompleteVisit,
  useWalkInPatient,
  useQueueWaitTime,
  useTomorrowAppointments,
  useSendBulkSmsReminders,
  useActiveCashierSession,
} from "./_lib/hooks";

import AppointmentsTable from "./_components/AppointmentsTable";
import JourneyContextMenu from "./_components/JourneyContextMenu";
import type { ContextMenuPosition } from "./_components/JourneyContextMenu";
import StatusCardsBar, { type ModuleTabKey } from "./_components/StatusCardsBar";
import {
  QuickPaymentModal,
  CompleteVisitModal,
  BookAppointmentModal,
  ConfirmDialog,
  WhatsAppMenu,
  ChangeRoomModal,
  WalkInModal,
  UndoToast,
  KeyboardShortcutsHelp,
  BulkSmsModal,
  DirectPaymentModal,
  OverrideDialog,
} from "./_components/Modals";

// ── Embedded module views ──
import BookingRequestsView from "./_modules/BookingRequestsView";
import ClinicQueueView from "./_modules/ClinicQueueView";
import RoomsView from "./_modules/RoomsView";
import LabView from "./_modules/LabView";
import ReportView from "./_modules/ReportView";


/* ═══════════════════════════════════════════════════════════════════════════
   Inline styles for animations
   ═══════════════════════════════════════════════════════════════════════════ */
const animationStyles = `
@keyframes slideInRight {
  from { transform: translateX(100%); opacity: 0; }
  to { transform: translateX(0); opacity: 1; }
}
@keyframes fadeInUp {
  from { transform: translateY(8px); opacity: 0; }
  to { transform: translateY(0); opacity: 1; }
}
@keyframes panelSlideIn {
  from { transform: translateX(-100%); }
  to { transform: translateX(0); }
}
.animate-slide-in-right { animation: slideInRight 0.25s ease-out; }
.animate-fade-in-up { animation: fadeInUp 0.2s ease-out; }
.animate-panel-slide { animation: panelSlideIn 0.25s ease-out; }
`;



/* ═══════════════════════════════════════════════════════════════════════════
   Module tabs (top level navigation within daily operations)
   ═══════════════════════════════════════════════════════════════════════════ */
type ModuleTab = "appointments" | "journey" | "queue" | "rooms" | "checkout" | "booking" | "lab" | "report";

const MODULE_TABS: { key: ModuleTab; label: string; icon: React.ElementType; color: string }[] = [
  { key: "appointments", label: "وصول اليوم",         icon: Calendar,      color: BLUE },
  { key: "journey",      label: "رحلة المرضى",       icon: Activity,      color: NAVY },
  { key: "queue",        label: "قائمة الانتظار",      icon: ClipboardList, color: ORANGE },
  { key: "rooms",        label: "الغرف والعيادات",      icon: Building2,     color: NAVY },
  { key: "checkout",     label: "جاهز للمحاسبة",      icon: CreditCard,    color: "#22c55e" },
  { key: "booking",      label: "طلبات الحجز",        icon: Globe,         color: BLUE },
  { key: "lab",          label: "المعمل",            icon: Activity,      color: "#8b5cf6" },
  { key: "report",       label: "تقرير اليوم والعمولات", icon: Wallet,        color: "#16a34a" },
];

/* ═══════════════════════════════════════════════════════════════════════════
   Sub-tab icon mapping (for رحلة المريض view)
   ═══════════════════════════════════════════════════════════════════════════ */
const TAB_ICONS: Record<string, React.ElementType> = {
  appointments: Calendar,
  queue: Clock,
  inClinic: Stethoscope,
  completed: CheckCircle,
  payments: CreditCard,
  overdue: AlertTriangle,
};

/* ═══════════════════════════════════════════════════════════════════════════
   RoomSelectDialog — Room selector for call/recall patient
   ═══════════════════════════════════════════════════════════════════════════ */
function RoomSelectDialog({ rooms, onConfirm, onCancel, loading }: {
  rooms: { id: string; arabicName: string }[];
  onConfirm: (roomName: string) => void;
  onCancel: () => void;
  loading: boolean;
}) {
  const [selectedRoom, setSelectedRoom] = useState(rooms[0]?.arabicName || "غرفة 1");
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40" onClick={onCancel}>
      <div className="bg-white rounded-xl shadow-xl p-5 min-w-[300px]" onClick={e => e.stopPropagation()}>
        <h3 className="text-sm font-bold mb-3" style={{ color: NAVY }}>اختر الغرفة</h3>
        <div className="grid grid-cols-3 gap-2 mb-4">
          {rooms.map(r => (
            <button key={r.id} onClick={() => setSelectedRoom(r.arabicName)}
              className="px-3 py-2 rounded-lg text-xs font-bold transition"
              style={{ background: selectedRoom === r.arabicName ? BLUE + "20" : "#f5f7fa", color: selectedRoom === r.arabicName ? BLUE : "#64748b", border: selectedRoom === r.arabicName ? `2px solid ${BLUE}` : "2px solid transparent" }}>
              {r.arabicName}
            </button>
          ))}
        </div>
        <div className="flex gap-2">
          <button onClick={() => onConfirm(selectedRoom)} disabled={loading}
            className="flex-1 py-2 rounded-lg text-xs font-bold text-white transition hover:opacity-90 disabled:opacity-50" style={{ background: BLUE }}>
            {loading ? "جاري النداء..." : "تأكيد"}
          </button>
          <button onClick={onCancel} className="flex-1 py-2 rounded-lg text-xs font-bold transition" style={{ background: "#f5f7fa", color: "#64748b" }}>
            إلغاء
          </button>
        </div>
      </div>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   MAIN PAGE — Microsoft Fluent Design
   ═══════════════════════════════════════════════════════════════════════════ */
export default function DailyOperationsPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { user } = useAuthStore();
  const userRole = user?.role ?? "";
  const isDoctor = isDoctorRole(userRole);
  const canProcessCheckout = userRole === "Admin" || userRole === "Reception";

  // ── Permission checks ──
  const canCheckIn = useHasPermission(PERMISSION_KEYS.DAILY_OPS_CHECK_IN);
  const canCreateWalkIn = useHasPermission(PERMISSION_KEYS.DAILY_OPS_CREATE_WALK_IN);
  const canCallPatient = useHasPermission(PERMISSION_KEYS.DAILY_OPS_CALL_PATIENT);
  const canRecallPatient = useHasPermission(PERMISSION_KEYS.DAILY_OPS_RECALL_PATIENT);
  const canCollectPayment = useHasPermission(PERMISSION_KEYS.DAILY_OPS_COLLECT_PAYMENT);
  const canViewClinicDisplay = useHasPermission(PERMISSION_KEYS.CLINIC_DISPLAY_VIEW);

  // ── Filters ──
  const [filterDate, setFilterDate] = useState(getTodayStr());
  const [filterDoctor, setFilterDoctor] = useState("");
  const [filterStatus, setFilterStatus] = useState("");
  const [searchQuery, setSearchQuery] = useState("");
  const [activeTab, setActiveTab] = useState<TabKey>("appointments");
  const [activeModule, setActiveModule] = useState<ModuleTab>("appointments");

  // ── SignalR real-time updates ──
  // The embedded queue tab owns a dedicated queue connection, so the page-level
  // connection pauses there to avoid duplicate event processing and duplicate sounds.
  const { isConnected: signalrConnected } = useSignalRClinicQueue({
    enabled: activeModule !== "queue",
    playSoundOnPatientCalled: activeModule !== "queue",
  });

  // ── Data ──
  const { data: items = [], isLoading: itemsLoading, refetch: refetchItems } = useTodayJourneyItems({
    date: filterDate,
    status: filterStatus || undefined,
    doctorId: filterDoctor || undefined,
  });

  useDashboardStats();
  const { data: doctors = [] } = useDoctors();
  const { data: branches = [] } = useBranches();
  const { data: rooms = [] } = useRooms();
  const { data: services = [] } = useServices();
  const { data: clinicSettings } = useClinicSettings();
  const { data: financeSummary } = useFinanceSummary();
  const clinicName = clinicSettings?.clinicName ?? "مركز الدكتور عقلان";

  // ── Queue wait time ──
  const { data: queueWaitTime } = useQueueWaitTime();

  // ── Tomorrow's appointments for bulk SMS ──
  const { data: tomorrowItems = [], refetch: refetchTomorrow } = useTomorrowAppointments();
  const bulkSmsMutation = useSendBulkSmsReminders();

  // ── Mutations ──
  const intakeMutation = useIntake();
  const sendToQueueMutation = useSendToQueue();
  const callPatientMutation = useCallPatient();
  const recallPatientMutation = useRecallPatient();
  const enterRoomMutation = useEnterRoom();
  const updateStatusMutation = useUpdateAppointmentStatus();
  const createPaymentMutation = useCreatePayment();
  const createDraftInvoiceMutation = useCreateDraftInvoice();
  const checkoutMutation = useCheckout();
  const handoffMutation = useHandoff();
  const leftWithoutCompletionMutation = useMarkLeftWithoutCompletion();
  const cancelQueueMutation = useCancelQueue();
  const changeRoomMutation = useChangeRoom();
  const createAppointmentMutation = useCreateAppointment();
  const completeVisitMutation = useCompleteVisit();
  const walkInMutation = useWalkInPatient();

  // ── Modal state ──
  const [selectedItem, setSelectedItem] = useState<TodayJourneyItem | null>(null);
  const [paymentModalOpen, setPaymentModalOpen] = useState(false);
  const [completeVisitModalOpen, setCompleteVisitModalOpen] = useState(false);
  const [leftWithoutModalOpen, setLeftWithoutModalOpen] = useState(false);
  const [leftWithoutReason, setLeftWithoutReason] = useState("");
  const [bookAppointmentModalOpen, setBookAppointmentModalOpen] = useState(false);
  const [overrideOpen, setOverrideOpen] = useState(false);
  const [pendingOverrideAction, setPendingOverrideAction] = useState<{
    type: "Intake" | "SendToQueue";
    item: TodayJourneyItem;
    overdueAmount: number;
  } | null>(null);
  const [confirmDialogOpen, setConfirmDialogOpen] = useState(false);
  const [confirmDialogType, setConfirmDialogType] = useState<"Cancel" | "NoShow" | "CancelQueue" | "ChangeRoom" | "Complete">("Cancel");
  const [whatsAppMenuOpen, setWhatsAppMenuOpen] = useState(false);
  const [changeRoomModalOpen, setChangeRoomModalOpen] = useState(false);

  // ── Walk-in modal ──
  const [walkInModalOpen, setWalkInModalOpen] = useState(false);

  // ── Undo state ──
  const [undoAction, setUndoAction] = useState<UndoAction | null>(null);

  // ── Side panel ──
  const [sidePanelOpen, setSidePanelOpen] = useState(false);
  const [sidePanelItem, setSidePanelItem] = useState<TodayJourneyItem | null>(null);

  // ── Keyboard shortcuts help ──
  const [shortcutsHelpOpen, setShortcutsHelpOpen] = useState(false);

  // ── Bulk SMS modal ──
  const [bulkSmsModalOpen, setBulkSmsModalOpen] = useState(false);

  // ── Direct payment modal (for unbooked patients) ──
  const [directPaymentModalOpen, setDirectPaymentModalOpen] = useState(false);

  // ── Lab Order Modal ──
  const [labOrderModalOpen, setLabOrderModalOpen] = useState(false);

  // ── More menu dropdown ──
  const [moreMenuOpen, setMoreMenuOpen] = useState(false);
  const moreMenuRef = useRef<HTMLDivElement>(null);

  // ── Room selection for call/recall ──
  const [roomSelectOpen, setRoomSelectOpen] = useState(false);
  const [pendingCallItem, setPendingCallItem] = useState<TodayJourneyItem | null>(null);
  const [callActionType, setCallActionType] = useState<"call" | "recall">("call");

  // ── Active cashier session check ──
  const { data: activeCashierSession } = useActiveCashierSession();

  // ── Sound toggle ──
  const [soundEnabled, setSoundEnabled] = useState(true);

  // ── Context menu state (right-click on journey items) ──
  const [ctxMenu, setCtxMenu] = useState<{ item: TodayJourneyItem; position: ContextMenuPosition } | null>(null);

  const handleItemContextMenu = useCallback((e: React.MouseEvent, item: TodayJourneyItem) => {
    setCtxMenu({ item, position: { x: e.clientX, y: e.clientY } });
  }, []);

  // ── Selected patient summary (for side panel + modals) ──
  const activePatientId = sidePanelItem?.patientId ?? selectedItem?.patientId ?? null;
  const { data: selectedSummary } = usePatientSummary(activePatientId);

  // ── Active item helper (returns whichever is selected) ──
  const getActiveItem = useCallback((): TodayJourneyItem | null => {
    return sidePanelItem ?? selectedItem ?? null;
  }, [sidePanelItem, selectedItem]);

  // ── Download receipt PDF for active item ──
  const handleDownloadReceipt = useCallback(async () => {
    const item = getActiveItem();
    if (!item) {
      toast.error("يرجى اختيار مريض أولاً");
      return;
    }
    const latestPaymentId = selectedSummary?.financeSummary?.latestPayment?.id;
    if (!latestPaymentId) {
      toast.error("لا توجد دفعة حديثة لتحميل سند");
      return;
    }
    try {
      const { downloadPdfFromApi } = await import("@/lib/pdfDownload");
      const receiptNum = selectedSummary?.financeSummary?.latestPayment?.receiptNumber ?? latestPaymentId;
      await downloadPdfFromApi(`/api/payments/${latestPaymentId}/pdf`, `receipt-${receiptNum}.pdf`);
      toast.success("تم تحميل سند القبض");
    } catch (err) {
      const reason = err instanceof Error ? err.message : "خطأ";
      toast.error(`فشل تحميل سند الدفع: ${reason}`);
    }
  }, [getActiveItem, selectedSummary]);

  // ── Print receipt PDF for active item — prints the PDF itself, not the system page ──
  const handlePrintReceipt = useCallback(async () => {
    const item = getActiveItem();
    if (!item) {
      toast.error("يرجى اختيار مريض أولاً");
      return;
    }
    const latestPaymentId = selectedSummary?.financeSummary?.latestPayment?.id;
    if (!latestPaymentId) {
      toast.error("لا توجد دفعة حديثة لطباعة سند");
      return;
    }
    try {
      const { printPdfFromApi } = await import("@/lib/pdfDownload");
      const receiptNum = selectedSummary?.financeSummary?.latestPayment?.receiptNumber ?? latestPaymentId;
      await printPdfFromApi(`/api/payments/${latestPaymentId}/pdf`, `receipt-${receiptNum}.pdf`);
    } catch (err) {
      const reason = err instanceof Error ? err.message : "خطأ";
      toast.error(`فشل طباعة سند الدفع: ${reason}`);
    }
  }, [getActiveItem, selectedSummary]);

  // ── Computed ──
  const dayStats = useMemo(() => computeDayStats(items, financeSummary), [items, financeSummary]);

  // ── Room occupancy ──
  const roomOccupancy = useMemo(() => computeRoomOccupancy(rooms, items), [rooms, items]);

  // ── Next patient to call ──
  const nextPatient = useMemo(() => getNextPatient(items), [items]);

  const matchesSearch = useCallback((item: TodayJourneyItem, query: string) => (
    item.patientName.toLowerCase().includes(query) ||
    (item.patientPhone && item.patientPhone.includes(query)) ||
    item.doctorName.toLowerCase().includes(query) ||
    (item.serviceName && item.serviceName.toLowerCase().includes(query))
  ), []);

  const filteredItems = useMemo(() => {
    let result = filterByTab(items, activeTab);
    if (searchQuery.trim()) {
      const q = searchQuery.trim().toLowerCase();
      result = result.filter(i => matchesSearch(i, q));
    }
    return result;
  }, [items, activeTab, searchQuery, matchesSearch]);

  const journeyItems = useMemo(() => {
    if (!searchQuery.trim()) return items;
    const q = searchQuery.trim().toLowerCase();
    return items.filter(i => matchesSearch(i, q));
  }, [items, searchQuery, matchesSearch]);

  // ── Tab counts ──
  const tabCounts = useMemo(() => ({
    appointments: items.length,
    queue: items.filter(i => i.queueStatus === "Waiting" || i.queueStatus === "Called" || (i.appointmentStatus === "Waiting" && !i.queueStatus)).length,
    inClinic: items.filter(i => i.appointmentStatus === "InRoom" || i.appointmentStatus === "InProgress" || i.queueStatus === "InRoom" || i.queueStatus === "InProgress").length,
    completed: items.filter(i => i.appointmentStatus === "Completed").length,
    payments: items.filter(i => i.checkoutStatus === "ReadyForCheckout" || i.nextAction === "Checkout").length,
    overdue: items.filter(i => i.appointmentStatus === "NoShow" || i.appointmentStatus === "Cancelled").length,
  }), [items]);

  // ── Search input ref for keyboard shortcut ──
  const searchInputRef = useRef<HTMLInputElement>(null);

  // ── Click outside handler for more menu ──
  useEffect(() => {
    if (!moreMenuOpen) return;
    const handleClick = (e: MouseEvent) => {
      if (moreMenuRef.current && !moreMenuRef.current.contains(e.target as Node)) {
        setMoreMenuOpen(false);
      }
    };
    document.addEventListener("mousedown", handleClick);
    return () => document.removeEventListener("mousedown", handleClick);
  }, [moreMenuOpen]);

  // ── Keyboard shortcuts ──
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      const target = e.target as HTMLElement;
      const isInput = target.tagName === "INPUT" || target.tagName === "TEXTAREA" || target.tagName === "SELECT";

      if (e.key === "Escape") {
        if (moreMenuOpen) { setMoreMenuOpen(false); return; }
        if (roomSelectOpen) { setRoomSelectOpen(false); setPendingCallItem(null); return; }
        if (ctxMenu) { setCtxMenu(null); return; }
        if (paymentModalOpen) { setPaymentModalOpen(false); return; }
        if (completeVisitModalOpen) { setCompleteVisitModalOpen(false); return; }
        if (bookAppointmentModalOpen) { setBookAppointmentModalOpen(false); return; }
        if (overrideOpen) { setOverrideOpen(false); return; }
        if (confirmDialogOpen) { setConfirmDialogOpen(false); return; }
        if (whatsAppMenuOpen) { setWhatsAppMenuOpen(false); return; }
        if (changeRoomModalOpen) { setChangeRoomModalOpen(false); return; }
        if (walkInModalOpen) { setWalkInModalOpen(false); return; }
        if (sidePanelOpen) { setSidePanelOpen(false); return; }
        if (shortcutsHelpOpen) { setShortcutsHelpOpen(false); return; }
      }

      if (e.ctrlKey && e.key === "r") {
        e.preventDefault();
        refetchItems();
        return;
      }

      if (e.ctrlKey && e.key === "f") {
        e.preventDefault();
        searchInputRef.current?.focus();
        return;
      }

      if (e.ctrlKey && e.key === "n") {
        e.preventDefault();
        setWalkInModalOpen(true);
        return;
      }

      if (!isInput && !e.ctrlKey && !e.altKey && !e.metaKey) {
        const tabKeys: TabKey[] = ["appointments", "queue", "inClinic", "completed", "payments", "overdue"];
        const num = parseInt(e.key, 10);
        if (num >= 1 && num <= 6) {
          setActiveTab(tabKeys[num - 1]);
          return;
        }
        if (e.key === "?") {
          setShortcutsHelpOpen(true);
          return;
        }
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [
    paymentModalOpen, completeVisitModalOpen, bookAppointmentModalOpen, overrideOpen,
    confirmDialogOpen, whatsAppMenuOpen, changeRoomModalOpen,
    walkInModalOpen, sidePanelOpen, shortcutsHelpOpen, ctxMenu, moreMenuOpen, roomSelectOpen, refetchItems,
  ]);

  // ── Action handlers ──
  const triggerIntakeOrQueue = useCallback(async (item: TodayJourneyItem, actionType: "Intake" | "SendToQueue", overrideManager?: string) => {
    try {
      if (!overrideManager) {
        // Fetch patient summary to check overdue amount
        const { data: summaryData } = await api.get(`/api/patient-journey/${item.patientId}/daily-summary`);
        const overdue = summaryData?.financeSummary?.overdueAmount ?? 0;
        if (overdue > 0) {
          setPendingOverrideAction({ type: actionType, item, overdueAmount: overdue });
          setOverrideOpen(true);
          return;
        }
      }

      const notesSuffix = overrideManager ? `[تجاوز متأخرات: بواسطة المدير ${overrideManager}]` : "";

      if (actionType === "Intake") {
        if (!item.appointmentId) { toast.error("لا يمكن تسجيل الوصول بدون موعد"); return; }
        intakeMutation.mutate(
          { appointmentId: item.appointmentId, body: notesSuffix ? { notes: notesSuffix } : {} },
          { onSuccess: () => toast.success("تم تسجيل وصول المريض"), onError: () => toast.error("فشل تسجيل الوصول") }
        );
      } else {
        if (!item.appointmentId) { toast.error("لا يمكن إضافة المريض للانتظار بدون موعد"); return; }
        sendToQueueMutation.mutate(
          { appointmentId: item.appointmentId, body: notesSuffix ? { notes: notesSuffix } : {} },
          { onSuccess: () => toast.success("تمت إضافة المريض للانتظار"), onError: () => toast.error("فشل إضافة المريض للانتظار") }
        );
      }
    } catch {
      toast.error("فشل إتمام العملية");
    }
  }, [intakeMutation, sendToQueueMutation]);

  const handleOverrideConfirm = useCallback((managerUsername: string) => {
    if (!pendingOverrideAction) return;
    const { item, type } = pendingOverrideAction;
    setOverrideOpen(false);
    setPendingOverrideAction(null);
    triggerIntakeOrQueue(item, type, managerUsername);
  }, [pendingOverrideAction, triggerIntakeOrQueue]);

  const handleIntake = useCallback((item: TodayJourneyItem) => {
    triggerIntakeOrQueue(item, "Intake");
  }, [triggerIntakeOrQueue]);

  const handleSendToQueue = useCallback((item: TodayJourneyItem) => {
    triggerIntakeOrQueue(item, "SendToQueue");
  }, [triggerIntakeOrQueue]);

  const handleCallPatient = useCallback((item: TodayJourneyItem) => {
    if (!item.queueItemId) return;
    setPendingCallItem(item);
    setCallActionType("call");
    setRoomSelectOpen(true);
  }, []);

  const handleRoomConfirm = useCallback((roomName: string) => {
    if (!pendingCallItem?.queueItemId) return;
    if (callActionType === "recall") {
      recallPatientMutation.mutate(
        { queueItemId: pendingCallItem.queueItemId, roomName },
        { onSuccess: () => toast.success("تم إعادة النداء"), onError: () => toast.error("فشل إعادة النداء") }
      );
    } else {
      callPatientMutation.mutate(
        { queueItemId: pendingCallItem.queueItemId, roomName },
        { onSuccess: () => toast.success("تم نداء المريض"), onError: () => toast.error("فشل نداء المريض") }
      );
    }
    setRoomSelectOpen(false);
    setPendingCallItem(null);
  }, [pendingCallItem, callActionType, callPatientMutation, recallPatientMutation]);

  const handleEnterRoom = useCallback((item: TodayJourneyItem) => {
    if (!item.queueItemId) return;
    enterRoomMutation.mutate(
      item.queueItemId,
      { onSuccess: () => toast.success("تم دخول الغرفة"), onError: () => toast.error("فشل دخول الغرفة") },
    );
  }, [enterRoomMutation]);

  const handleQuickPayment = useCallback((item: TodayJourneyItem) => {
    if (!activeCashierSession) {
      toast.error("يجب فتح وردية (صندوق الكاشير) أولاً قبل تسجيل أي مدفوعات. اذهب إلى المالية > الصندوق لفتح وردية.");
      return;
    }
    setSelectedItem(item);
    setPaymentModalOpen(true);
  }, [activeCashierSession]);

  const handleCompleteVisit = useCallback((item: TodayJourneyItem) => {
    setSelectedItem(item);
    setCompleteVisitModalOpen(true);
  }, []);

  const handleLeftWithoutCompletion = useCallback((item: TodayJourneyItem) => {
    if (!item.visitId) {
      toast.error("لا توجد زيارة مفتوحة لتسجيل خروج بدون إكمال");
      return;
    }
    setSelectedItem(item);
    setLeftWithoutReason("");
    setLeftWithoutModalOpen(true);
  }, []);

  const handleCreateDraftInvoice = useCallback(async (item: TodayJourneyItem) => {
    if (!item.visitId) {
      toast.error("لا توجد زيارة مرتبطة لإنشاء الفاتورة");
      return;
    }

    try {
      const result = await createDraftInvoiceMutation.mutateAsync(item.visitId) as {
        isExisting?: boolean;
        IsExisting?: boolean;
      };
      toast.success(
        result?.isExisting || result?.IsExisting
          ? "توجد فاتورة مسودة مسبقاً لهذه الزيارة"
          : "تم إنشاء فاتورة مسودة للتحصيل"
      );
    } catch {
      toast.error("فشل إنشاء فاتورة التحصيل");
    }
  }, [createDraftInvoiceMutation]);

  const handleBookAppointment = useCallback((item: TodayJourneyItem) => {
    setSelectedItem(item);
    setBookAppointmentModalOpen(true);
  }, []);

  const handleCreateLabOrder = useCallback((item: TodayJourneyItem) => {
    setSelectedItem(item);
    setLabOrderModalOpen(true);
  }, []);

  const handleWhatsApp = useCallback((item: TodayJourneyItem) => {
    setSelectedItem(item);
    setWhatsAppMenuOpen(true);
  }, []);

  const handleNoShow = useCallback((item: TodayJourneyItem) => {
    setSelectedItem(item);
    setConfirmDialogType("NoShow");
    setConfirmDialogOpen(true);
  }, []);

  const handleCancel = useCallback((item: TodayJourneyItem) => {
    setSelectedItem(item);
    setConfirmDialogType("Cancel");
    setConfirmDialogOpen(true);
  }, []);

  const handleViewPatient = useCallback((item: TodayJourneyItem) => {
    router.push(`/patients/${item.patientId}`);
  }, [router]);

  const handleOpenSidePanel = useCallback((item: TodayJourneyItem) => {
    setSidePanelItem(item);
    setSidePanelOpen(true);
  }, []);

  useEffect(() => {
    const appointmentId = searchParams.get("appointmentId");
    const patientId = searchParams.get("patientId");
    if (!appointmentId && !patientId) return;

    setActiveModule("journey");
    const match = items.find((item) =>
      (appointmentId && item.appointmentId === appointmentId) ||
      (patientId && item.patientId === patientId)
    );
    if (match) handleOpenSidePanel(match);
  }, [searchParams, items, handleOpenSidePanel]);

  const pushUndoAction = useCallback((action: UndoAction) => {
    if (undoAction) setUndoAction(null);
    setUndoAction(action);
  }, [undoAction]);

  const handleUndo = useCallback(async () => {
    if (!undoAction) return;
    try {
      if (undoAction.type === "NoShow" || undoAction.type === "Cancel") {
        if (!undoAction.appointmentId) { toast.error("لا يمكن التراجع بدون موعد"); return; }
        await updateStatusMutation.mutateAsync({
          appointmentId: undoAction.appointmentId,
          status: undoAction.previousStatus,
        });
        toast.success("تم التراجع عن الإجراء");
      } else if (undoAction.type === "CancelQueue" && undoAction.queueItemId) {
        if (!undoAction.appointmentId) { toast.error("لا يمكن التراجع بدون موعد"); return; }
        await sendToQueueMutation.mutateAsync({ appointmentId: undoAction.appointmentId });
        toast.success("تم التراجع — أعيد المريض لقائمة الانتظار");
      }
    } catch {
      toast.error("فشل التراجع");
    }
    setUndoAction(null);
  }, [undoAction, updateStatusMutation, sendToQueueMutation]);

  const handleConfirmAction = useCallback(async () => {
    if (!selectedItem) return;
    try {
      if (confirmDialogType === "NoShow") {
        if (!selectedItem.appointmentId) { toast.error("لا يمكن تسجيل عدم الحضور بدون موعد"); return; }
        const prevStatus = selectedItem.appointmentStatus;
        await updateStatusMutation.mutateAsync({ appointmentId: selectedItem.appointmentId, status: "NoShow" });
        toast.success("تم تسجيل عدم الحضور");
        pushUndoAction({
          id: crypto.randomUUID(),
          type: "NoShow",
          appointmentId: selectedItem.appointmentId,
          previousStatus: prevStatus,
          patientName: selectedItem.patientName,
          timestamp: Date.now(),
        });
      } else if (confirmDialogType === "Cancel") {
        if (!selectedItem.appointmentId) { toast.error("لا يمكن إلغاء الموعد بدون موعد"); return; }
        const prevStatus = selectedItem.appointmentStatus;
        await updateStatusMutation.mutateAsync({ appointmentId: selectedItem.appointmentId, status: "Cancelled" });
        toast.success("تم إلغاء الموعد");
        pushUndoAction({
          id: crypto.randomUUID(),
          type: "Cancel",
          appointmentId: selectedItem.appointmentId,
          previousStatus: prevStatus,
          patientName: selectedItem.patientName,
          timestamp: Date.now(),
        });
      } else if (confirmDialogType === "CancelQueue") {
        if (!selectedItem.queueItemId) return;
        await cancelQueueMutation.mutateAsync(selectedItem.queueItemId);
        toast.success("تم إلغاء المريض من قائمة الانتظار");
        pushUndoAction({
          id: crypto.randomUUID(),
          type: "CancelQueue",
          appointmentId: selectedItem.appointmentId,
          previousStatus: selectedItem.appointmentStatus,
          queueItemId: selectedItem.queueItemId,
          patientName: selectedItem.patientName,
          timestamp: Date.now(),
        });
      } else if (confirmDialogType === "Complete") {
        if (!selectedItem.queueItemId) return;
        await completeVisitMutation.mutateAsync({ queueItemId: selectedItem.queueItemId });
        toast.success("تم إنهاء الزيارة");
      }
    } catch {
      toast.error("فشل تنفيذ الإجراء");
    }
    setConfirmDialogOpen(false);
  }, [selectedItem, confirmDialogType, updateStatusMutation, cancelQueueMutation, completeVisitMutation, pushUndoAction]);

  const handlePaymentConfirm = useCallback(async (amount: number, method: string, desc: string, notes: string, referenceNumber?: string) => {
    if (!selectedItem) return;
    try {
      const result = await createPaymentMutation.mutateAsync({
        patientId: selectedItem.patientId,
        amount,
        paymentMethod: method,
        serviceDescription: desc || undefined,
        doctorId: selectedItem.doctorId,
        notes: notes || undefined,
        referenceNumber: referenceNumber || undefined,
      });
      toast.success("تم تسجيل الدفعة بنجاح");
      setPaymentModalOpen(false);

      if (result?.id) {
        try {
          const { downloadPdfFromApi } = await import("@/lib/pdfDownload");
          const filename = `receipt-${result.receiptNumber || result.id}.pdf`;
          await downloadPdfFromApi(`/api/payments/${result.id}/pdf`, filename);
        } catch {
          // PDF download failed, user can still download manually
        }
      }
    } catch (err: unknown) {
      // Extract the actual error message from backend response
      let errorMsg = "فشل تسجيل الدفعة";
      if (err && typeof err === "object" && "response" in err) {
        const resp = (err as { response?: { data?: { message?: string } } }).response;
        if (resp?.data?.message) errorMsg = resp.data.message;
      }
      toast.error(errorMsg);
    }
  }, [selectedItem, createPaymentMutation]);

  const handleCompleteVisitConfirm = useCallback(async (data: {
    serviceDesc: string; amountDue: number; isPaid: boolean;
    needsFollowUp: boolean; nextDate: string; notes: string;
    diagnosis: string; instructions: string;
    proposedProcedure?: string;
  }) => {
    if (!selectedItem) return;
    try {
      if (
        selectedItem.checkoutStatus !== "ReadyForCheckout" &&
        selectedItem.nextAction !== "Checkout" &&
        selectedItem.visitId
      ) {
        await handoffMutation.mutateAsync({
          visitId: selectedItem.visitId,
          body: {
            treatmentDone: data.serviceDesc || undefined,
            diagnosis: data.diagnosis || undefined,
            instructions: data.instructions || undefined,
            amountDue: data.amountDue || undefined,
            notes: data.notes || undefined,
            followUpDate: data.needsFollowUp ? data.nextDate : undefined,
            proposedProcedure: data.proposedProcedure || undefined,
          },
        });
        toast.success("تم إرسال المريض للاستقبال للتحصيل والخروج");
        setCompleteVisitModalOpen(false);
        return;
      }

      if (selectedItem.checkoutStatus === "ReadyForCheckout" || selectedItem.nextAction === "Checkout") {
        // Checkout should only mark the appointment as completed.
        // Payment creation is handled separately via the payment flow,
        // not auto-created during checkout with a hardcoded method.
        if (!selectedItem.appointmentId) { toast.error("لا يمكن إكمال الخروج بدون موعد"); return; }
        await checkoutMutation.mutateAsync({
          appointmentId: selectedItem.appointmentId,
          body: {
            notes: data.notes || undefined,
            nextAppointmentDate: data.needsFollowUp ? data.nextDate : undefined,
          },
        });
        toast.success("تم إكمال الخروج بنجاح");
        setCompleteVisitModalOpen(false);
        return;
      }

      toast.error("لا يمكن إكمال الإجراء دون زيارة جاهزة للتحصيل");
    } catch {
      toast.error("فشل إنهاء الزيارة");
    }
  }, [selectedItem, handoffMutation, checkoutMutation]);

  const handleCheckoutConfirm = useCallback(async (data: {
    paymentAmount: number; paymentMethod: string; notes: string;
    nextDate?: string; nextServiceId?: string;
  }) => {
    if (!selectedItem) return;
    if (!selectedItem.appointmentId) { toast.error("لا يمكن إكمال الخروج بدون موعد"); return; }
    try {
      // Checkout only marks the appointment as completed.
      // Payment creation is handled separately via the payment flow,
      // not auto-created during checkout.
      await checkoutMutation.mutateAsync({
        appointmentId: selectedItem.appointmentId,
        body: {
          notes: data.notes || undefined,
          nextAppointmentDate: data.nextDate || undefined,
        },
      });
      toast.success("تم إنهاء الزيارة بنجاح");
      setCompleteVisitModalOpen(false);
    } catch {
      toast.error("فشل إنهاء الزيارة");
    }
  }, [selectedItem, checkoutMutation]);

  const handleLeftWithoutCompletionConfirm = useCallback(async () => {
    if (!selectedItem?.visitId) return;

    const reason = leftWithoutReason.trim();
    if (!reason) {
      toast.error("يرجى كتابة سبب الخروج بدون إكمال");
      return;
    }

    try {
      await leftWithoutCompletionMutation.mutateAsync({
        visitId: selectedItem.visitId,
        reason,
        status: "LeftWithoutCompletion",
      });
      toast.success("تم تسجيل خروج المريض بدون إكمال");
      setLeftWithoutModalOpen(false);
      setLeftWithoutReason("");
    } catch (err: unknown) {
      let errorMsg = "فشل تسجيل خروج المريض بدون إكمال";
      if (err && typeof err === "object" && "response" in err) {
        const resp = (err as { response?: { data?: { message?: string } } }).response;
        if (resp?.data?.message) errorMsg = resp.data.message;
      }
      toast.error(errorMsg);
    }
  }, [selectedItem, leftWithoutReason, leftWithoutCompletionMutation]);

  const handleBookConfirm = useCallback(async (data: {
    doctorId: string; date: string; startTime: string; endTime: string;
    serviceId: string; type: string; notes: string;
  }) => {
    if (!selectedItem) return;
    try {
      await createAppointmentMutation.mutateAsync({
        patientId: selectedItem.patientId,
        doctorId: data.doctorId,
        appointmentDate: data.date,
        startTime: data.startTime,
        endTime: data.endTime,
        serviceId: data.serviceId || undefined,
        appointmentType: data.type,
        notes: data.notes || undefined,
      });
      toast.success("تم حجز الموعد بنجاح");
      setBookAppointmentModalOpen(false);
    } catch {
      toast.error("فشل حجز الموعد");
    }
  }, [selectedItem, createAppointmentMutation]);

  const handleWalkInConfirm = useCallback(async (data: {
    patientName: string; patientPhone: string; doctorId: string;
    serviceId: string; branchId: string; notes: string;
  }) => {
    try {
      await walkInMutation.mutateAsync(data);
      toast.success("تم تسجيل المريض المشي وإضافته لقائمة الانتظار");
      setWalkInModalOpen(false);
    } catch {
      toast.error("فشل تسجيل المريض المشي");
    }
  }, [walkInMutation]);

  const handleBulkSms = useCallback(async () => {
    try {
      const result = await bulkSmsMutation.mutateAsync({
        appointmentIds: tomorrowItems.map(i => i.appointmentId).filter((id): id is string => !!id),
      });
      toast.success(`تم إرسال ${result.succeeded} تذكير${result.failed > 0 ? ` (فشل ${result.failed})` : ""}`);
      setBulkSmsModalOpen(false);
    } catch {
      toast.error("فشل إرسال التذكيرات");
    }
  }, [bulkSmsMutation, tomorrowItems]);

  // ── Direct payment for unbooked patient ──
  const handleDirectPaymentConfirm = useCallback(async (data: {
    patientId: string; patientName: string;
    amount: number; paymentMethod: string;
    serviceDescription: string; notes: string;
    referenceNumber?: string;
  }) => {
    try {
      const result = await createPaymentMutation.mutateAsync({
        patientId: data.patientId,
        amount: data.amount,
        paymentMethod: data.paymentMethod,
        serviceDescription: data.serviceDescription || undefined,
        notes: data.notes || undefined,
        referenceNumber: data.referenceNumber || undefined,
      });
      toast.success(`تم تسجيل دفعة ${fmtRial(data.amount)} للمريض ${data.patientName} بنجاح`);
      setDirectPaymentModalOpen(false);

      // Try to download PDF receipt
      if (result?.id) {
        try {
          const { downloadPdfFromApi } = await import("@/lib/pdfDownload");
          const filename = `receipt-${result.receiptNumber || result.id}.pdf`;
          await downloadPdfFromApi(`/api/payments/${result.id}/pdf`, filename);
        } catch {
          // PDF download failed, user can still download manually
        }
      }
    } catch (err: unknown) {
      // Extract the actual error message from backend response
      let errorMsg = "فشل تسجيل الدفعة";
      if (err && typeof err === "object" && "response" in err) {
        const resp = (err as { response?: { data?: { message?: string } } }).response;
        if (resp?.data?.message) errorMsg = resp.data.message;
      }
      toast.error(errorMsg);
    }
  }, [createPaymentMutation]);

  // ── Available status filters ──
  const statusFilters = [
    { value: "", label: "الكل" },
    { value: "Scheduled", label: "مجدول" },
    { value: "Confirmed", label: "مؤكد" },
    { value: "Arrived", label: "وصل" },
    { value: "Waiting", label: "في الانتظار" },
    { value: "InRoom", label: "داخل الغرفة" },
    { value: "InProgress", label: "جاري العلاج" },
    { value: "Completed", label: "مكتمل" },
    { value: "NoShow", label: "لم يحضر" },
    { value: "Cancelled", label: "ملغي" },
  ];

  // ── Side panel data ──
  const panelFinance = selectedSummary?.financeSummary;
  const panelMedicalAlerts = selectedSummary?.medicalAlerts ?? [];
  const panelActiveContract = selectedSummary?.activeContract;
  const panelActiveOrtho = selectedSummary?.activeOrthoCase;

  // ── Render ──
  return (
    <>
      <style dangerouslySetInnerHTML={{ __html: animationStyles }} />

      <div className="h-screen flex flex-col bg-[#f8fafc] overflow-hidden">
        {/* ═══════════════════════════════════════════════════════════════════
            COMMAND BAR (52px) — Redesigned: Primary visible + More dropdown
            ═══════════════════════════════════════════════════════════════════ */}
        <div className="h-[52px] flex-shrink-0 bg-white flex items-center px-3 gap-2"
          style={{ borderBottom: "1px solid #e5e7eb", boxShadow: "0 1px 3px rgba(0,0,0,0.04)" }}>

          {/* Left: Icon + Title */}
          <div className="flex items-center gap-2 flex-shrink-0">
            <div className="w-8 h-8 rounded-lg flex items-center justify-center" style={{ background: NAVY }}>
              <Activity className="w-4 h-4 text-white" />
            </div>
            <div className="leading-tight">
              <div className="text-sm font-extrabold" style={{ color: NAVY }}>التشغيل اليومي</div>
              <div className="text-[10px] font-medium" style={{ color: "#94a3b8" }}>لوحة التحكم الموحدة</div>
            </div>
          </div>

          {/* Divider */}
          <div className="w-px h-7 mx-1" style={{ background: "#e5e7eb" }} />

          {/* Center: Search */}
          <div className="flex-1 max-w-md min-w-0 relative">
            <Search className="w-4 h-4 absolute top-1/2 right-3 -translate-y-1/2" style={{ color: "#94a3b8" }} />
            <input
              ref={searchInputRef}
              type="text"
              value={searchQuery}
              onChange={e => setSearchQuery(e.target.value)}
              placeholder="بحث باسم المريض أو الهاتف..."
              className="w-full text-sm rounded-full border-0 pl-16 pr-9 py-1.5 outline-none focus:ring-2 focus:ring-[#3d7ab5]/20"
              style={{ background: "#f5f7fa", color: NAVY }}
            />
            <span className="absolute left-3 top-1/2 -translate-y-1/2 text-[10px] font-medium px-1.5 py-0.5 rounded"
              style={{ background: "#e5e7eb", color: "#94a3b8" }}>
              Ctrl+F
            </span>
          </div>

          {/* Inline Filters (compact, no labels) */}
          {/* Date picker */}
          <div className="flex items-center gap-1 px-2 py-1 rounded-lg text-xs font-semibold flex-shrink-0"
            style={{ background: "#f5f7fa", color: NAVY }}>
            <Calendar className="w-3.5 h-3.5" />
            <input type="date" value={filterDate} onChange={e => setFilterDate(e.target.value)}
              className="bg-transparent text-xs font-semibold outline-none border-0 w-[110px]"
              style={{ color: NAVY }} />
          </div>

          {/* Doctor filter */}
          {!isDoctor && (
            <select value={filterDoctor} onChange={e => setFilterDoctor(e.target.value)}
              className="text-xs font-semibold rounded-lg px-2 py-1.5 outline-none border-0 focus:ring-2 focus:ring-[#3d7ab5]/20 flex-shrink-0"
              style={{ background: "#f5f7fa", color: NAVY, maxWidth: 120 }}>
              <option value="">👨‍⚕️ كل الأطباء</option>
              {doctors.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          )}

          {/* Status filter */}
          <select value={filterStatus} onChange={e => setFilterStatus(e.target.value)}
            className="text-xs font-semibold rounded-lg px-2 py-1.5 outline-none border-0 focus:ring-2 focus:ring-[#3d7ab5]/20 flex-shrink-0"
            style={{ background: "#f5f7fa", color: NAVY, maxWidth: 110 }}>
            {statusFilters.map(s => <option key={s.value} value={s.value}>📊 {s.label}</option>)}
          </select>

          {/* Divider */}
          <div className="w-px h-7 mx-1" style={{ background: "#e5e7eb" }} />

          {/* ═══ PRIMARY ACTIONS (always visible) ═══ */}

          {/* شاشة النداء (Clinic Display) — navy outline */}
          {canViewClinicDisplay && (
            <button onClick={() => window.open("/clinic-display", "_blank")}
              className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-bold transition hover:opacity-90 flex-shrink-0"
              style={{ color: NAVY, border: `1.5px solid ${NAVY}40`, background: `${NAVY}08` }}
              title="شاشة النداء">
              <Monitor className="w-3.5 h-3.5" />
              <span className="hidden md:inline">شاشة النداء</span>
            </button>
          )}

          {/* مريض مشي (Walk-in) — Orange solid */}
          {canCreateWalkIn && (
          <button onClick={() => setWalkInModalOpen(true)}
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-bold text-white transition hover:opacity-90 flex-shrink-0"
            style={{ background: ORANGE }}
            title="مريض مشي (Ctrl+N)">
            <UserPlus className="w-3.5 h-3.5" />
            <span className="hidden md:inline">مريض مشي</span>
          </button>
          )}

          {/* تحصيل/دفع (Collect Payment) — Green solid with cashier session indicator */}
          {canCollectPayment && (
            <button onClick={() => {
              if (!activeCashierSession) {
                toast.error("يجب فتح وردية (صندوق الكاشير) أولاً قبل تسجيل أي مدفوعات. اذهب إلى المالية > الصندوق لفتح وردية.");
                return;
              }
              setDirectPaymentModalOpen(true);
            }}
              className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-bold text-white transition hover:opacity-90 flex-shrink-0 relative"
              style={{ background: activeCashierSession ? "#22c55e" : "#94a3b8" }}
              title={activeCashierSession ? "تحصيل/دفع" : "يجب فتح وردية أولاً"}>
              {/* Cashier session status dot */}
              <span className="absolute -top-0.5 -right-0.5 w-2.5 h-2.5 rounded-full border-2 border-white"
                style={{ background: activeCashierSession ? "#16a34a" : "#ef4444" }} />
              <CreditCard className="w-3.5 h-3.5" />
              <span className="hidden md:inline">تحصيل/دفع</span>
            </button>
          )}

          {/* ═══ المزيد (More) dropdown ═══ */}
          <div ref={moreMenuRef} className="relative flex-shrink-0">
            <button onClick={() => setMoreMenuOpen(!moreMenuOpen)}
              className="w-9 h-9 rounded-lg flex items-center justify-center transition hover:bg-gray-100"
              title="المزيد">
              <MoreHorizontal className="w-4 h-4" style={{ color: "#64748b" }} />
            </button>

            {moreMenuOpen && (
              <div className="absolute top-full left-0 mt-1 bg-white rounded-xl shadow-lg border py-1.5 min-w-[200px] z-50"
                style={{ borderColor: "#e5e7eb" }}>

                {/* تسجيل وصول (CheckIn) */}
                {canCheckIn && (
                  <button onClick={() => {
                    setMoreMenuOpen(false);
                    const item = getActiveItem();
                    if (!item) { toast.error("يرجى اختيار مريض أولاً"); return; }
                    handleIntake(item);
                  }}
                    className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold transition hover:bg-green-50"
                    style={{ color: "#16a34a" }}>
                    <ClipboardCheck className="w-4 h-4" />
                    <span>تسجيل وصول</span>
                  </button>
                )}

                {/* مريض جديد (New Patient) */}
                <button onClick={() => { setMoreMenuOpen(false); router.push("/patients/new"); }}
                  className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold transition hover:bg-[#1a3a5c08]"
                  style={{ color: NAVY }}>
                  <UserPlus className="w-4 h-4" />
                  <span>مريض جديد</span>
                </button>

                {/* دخول مباشر (Walk-In) */}
                {canCreateWalkIn && (
                  <button onClick={() => { setMoreMenuOpen(false); setWalkInModalOpen(true); }}
                    className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold transition hover:bg-orange-50"
                    style={{ color: ORANGE }}>
                    <LogIn className="w-4 h-4" />
                    <span>دخول مباشر</span>
                  </button>
                )}

                {/* تحصيل دفعة (Collect Payment) */}
                {canCollectPayment && (
                  <button onClick={() => {
                    setMoreMenuOpen(false);
                    const item = getActiveItem();
                    if (!item) { toast.error("يرجى اختيار مريض أولاً"); return; }
                    handleQuickPayment(item);
                  }}
                    className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold transition hover:bg-emerald-50"
                    style={{ color: "#059669" }}>
                    <CreditCard className="w-4 h-4" />
                    <span>تحصيل دفعة</span>
                  </button>
                )}

                {/* نداء المريض (Call Patient) */}
                {canCallPatient && (
                  <button onClick={() => {
                    setMoreMenuOpen(false);
                    const item = getActiveItem();
                    if (!item) { toast.error("يرجى اختيار مريض أولاً"); return; }
                    handleCallPatient(item);
                  }}
                    className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold transition hover:bg-amber-50"
                    style={{ color: "#d97706" }}>
                    <Bell className="w-4 h-4" />
                    <span>نداء المريض</span>
                  </button>
                )}

                {/* إعادة النداء (Recall) */}
                {canRecallPatient && (
                  <button onClick={() => {
                    setMoreMenuOpen(false);
                    const item = getActiveItem();
                    if (!item) { toast.error("يرجى اختيار مريض أولاً"); return; }
                    setPendingCallItem(item);
                    setCallActionType("recall");
                    setRoomSelectOpen(true);
                  }}
                    className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold transition hover:bg-yellow-50"
                    style={{ color: "#ca8a04" }}>
                    <BellRing className="w-4 h-4" />
                    <span>إعادة النداء</span>
                  </button>
                )}

                {/* موعد قادم (Next Appointment) */}
                <button onClick={() => {
                  setMoreMenuOpen(false);
                  const item = getActiveItem();
                  if (!item) { toast.error("يرجى اختيار مريض أولاً"); return; }
                  handleBookAppointment(item);
                }}
                  className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold transition hover:bg-blue-50"
                  style={{ color: BLUE }}>
                  <CalendarPlus className="w-4 h-4" />
                  <span>موعد قادم</span>
                </button>

                {/* تحميل سند PDF (Download Receipt PDF) */}
                <button onClick={() => { setMoreMenuOpen(false); handleDownloadReceipt(); }}
                  className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold transition hover:bg-green-50"
                  style={{ color: "#16a34a" }}>
                  <Download className="w-4 h-4" />
                  <span>تحميل سند PDF</span>
                </button>

                {/* طباعة سند مباشرة (Print Receipt — prints PDF only, not the system page) */}
                <button onClick={() => { setMoreMenuOpen(false); handlePrintReceipt(); }}
                  className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold transition hover:bg-purple-50"
                  style={{ color: "#7c3aed" }}>
                  <Printer className="w-4 h-4" />
                  <span>طباعة سند مباشرة</span>
                </button>

                <div className="my-1 border-t" style={{ borderColor: "#f1f5f9" }} />

                {/* SMS Reminders */}
                {!isDoctor && (
                  <button onClick={() => { setMoreMenuOpen(false); refetchTomorrow(); setBulkSmsModalOpen(true); }}
                    className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold transition hover:bg-[#3d7ab508]"
                    style={{ color: BLUE }}>
                    <Megaphone className="w-4 h-4" />
                    <span>إرسال تذكيرات</span>
                  </button>
                )}

                {/* Print */}
                <button onClick={() => { setMoreMenuOpen(false); printScreen(); }}
                  className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold transition hover:bg-gray-50"
                  style={{ color: "#64748b" }}>
                  <Printer className="w-4 h-4" />
                  <span>طباعة جدول اليوم</span>
                </button>

                {/* Refresh */}
                <button onClick={() => { setMoreMenuOpen(false); refetchItems(); }}
                  className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold transition hover:bg-gray-50"
                  style={{ color: "#64748b" }}>
                  <RefreshCw className="w-4 h-4" />
                  <span>تحديث (Ctrl+R)</span>
                </button>

                {/* Sound toggle */}
                <button onClick={() => { setMoreMenuOpen(false); setSoundEnabled(!soundEnabled); }}
                  className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold transition hover:bg-gray-50"
                  style={{ color: soundEnabled ? "#64748b" : "#94a3b8" }}>
                  {soundEnabled ? <Bell className="w-4 h-4" /> : <BellOff className="w-4 h-4" />}
                  <span>{soundEnabled ? "إيقاف صوت التنبيه" : "تشغيل صوت التنبيه"}</span>
                </button>

                {/* Shortcuts help */}
                <button onClick={() => { setMoreMenuOpen(false); setShortcutsHelpOpen(true); }}
                  className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold transition hover:bg-gray-50"
                  style={{ color: "#64748b" }}>
                  <Keyboard className="w-4 h-4" />
                  <span>اختصارات لوحة المفاتيح</span>
                </button>
              </div>
            )}
          </div>

          {/* SignalR indicator */}
          <div className="flex items-center gap-1 w-6 h-6 rounded-full justify-center flex-shrink-0" title={signalrConnected ? "متصل مباشر" : "غير متصل"}>
            <span className="w-2.5 h-2.5 rounded-full" style={{ background: signalrConnected ? "#16a34a" : "#ef4444" }} />
          </div>
        </div>

        {/* ═══════════ Module Tabs Row (42px) — Microsoft Fluent Tabs ═══════════ */}
        <div className="h-[42px] flex-shrink-0 bg-white flex items-center px-3 gap-0.5 overflow-x-auto"
          style={{ borderBottom: "2px solid #f1f5f9", scrollbarWidth: "thin" }}>
          {MODULE_TABS.map(tab => {
            const isActive = activeModule === tab.key;
            const TabIcon = tab.icon;
            return (
              <button key={tab.key}
                onClick={() => {
                  setActiveModule(tab.key);
                }}
                className="flex items-center gap-1.5 px-3 py-1.5 rounded-t-lg text-xs font-bold relative transition-all flex-shrink-0"
                style={{
                  color: isActive ? tab.color : "#64748b",
                  background: isActive ? tab.color + "08" : "transparent",
                }}>
                <TabIcon className="w-3.5 h-3.5" />
                <span>{tab.label}</span>
                {tab.key === "appointments" && items.length > 0 && (
                  <span className="text-[9px] font-extrabold px-1.5 py-0.5 rounded-full min-w-[18px] text-center"
                    style={{ background: isActive ? tab.color : tab.color + "20", color: isActive ? "#fff" : tab.color }}>
                    {items.length}
                  </span>
                )}
                {tab.key === "journey" && items.length > 0 && (
                  <span className="text-[9px] font-extrabold px-1.5 py-0.5 rounded-full min-w-[18px] text-center"
                    style={{ background: isActive ? tab.color : tab.color + "20", color: isActive ? "#fff" : tab.color }}>
                    {items.length}
                  </span>
                )}
                {tab.key === "queue" && tabCounts.queue > 0 && (
                  <span className="text-[9px] font-extrabold px-1.5 py-0.5 rounded-full min-w-[18px] text-center"
                    style={{ background: isActive ? tab.color : tab.color + "20", color: isActive ? "#fff" : tab.color }}>
                    {tabCounts.queue}
                  </span>
                )}
                {tab.key === "checkout" && tabCounts.payments > 0 && (
                  <span className="text-[9px] font-extrabold px-1.5 py-0.5 rounded-full min-w-[18px] text-center"
                    style={{ background: isActive ? tab.color : tab.color + "20", color: isActive ? "#fff" : tab.color }}>
                    {tabCounts.payments}
                  </span>
                )}
                {/* Active indicator */}
                {isActive && (
                  <span className="absolute bottom-[-2px] left-2 right-2 h-[2px] rounded-full" style={{ background: tab.color }} />
                )}
              </button>
            );
          })}
        </div>

        {/* ═══════════════════════════════════════════════════════════════════
            MAIN CONTENT AREA (flex-1) — Unified Layout
            ═══════════════════════════════════════════════════════════════════ */}
        <div className="flex-1 flex overflow-hidden">

          {/* ── Left Area: Active Tab View (75%) ── */}
          <div className="flex-1 flex flex-col min-w-0 bg-[#f8fafc]">

            {/* Tab: وصول اليوم (appointments) */}
            {activeModule === "appointments" && (
              <div className="flex-1 flex flex-col min-w-0">
                {/* Tab Pills */}
                <div className="flex-shrink-0 bg-white px-3 py-2 flex items-center gap-1.5 overflow-x-auto"
                  style={{ borderBottom: "1px solid #f1f5f9" }}>
                  {TABS.filter(t => t.key !== "payments" && t.key !== "queue").map(tab => {
                    const count = tabCounts[tab.key];
                    const isActive = activeTab === tab.key;
                    const TabIcon = TAB_ICONS[tab.key];
                    return (
                      <button key={tab.key} onClick={() => setActiveTab(tab.key)}
                        className="flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-bold whitespace-nowrap transition-all"
                        style={{
                          background: isActive ? tab.color : "#f5f7fa",
                          color: isActive ? "#fff" : "#64748b",
                          boxShadow: isActive ? `0 1px 3px ${tab.color}30` : "none",
                        }}>
                        {TabIcon && <TabIcon className="w-3.5 h-3.5" />}
                        <span>{tab.label}</span>
                        {count > 0 && (
                          <span className="text-[10px] font-extrabold px-1.5 py-0.5 rounded-full min-w-[18px] text-center"
                            style={{
                              background: isActive ? "rgba(255,255,255,0.25)" : tab.color + "15",
                              color: isActive ? "#fff" : tab.color,
                            }}>
                            {count}
                          </span>
                        )}
                      </button>
                    );
                  })}

                  {/* Next patient chip (inline) */}
                  {nextPatient && (
                    <div className="flex items-center gap-1.5 mr-auto px-2.5 py-1 rounded-lg text-xs font-bold"
                      style={{ background: "linear-gradient(135deg, #f0f7ff, #faf5ff)", border: "1px solid #e0e7ff", color: NAVY }}>
                      <Activity className="w-3 h-3" style={{ color: BLUE }} />
                      <span>التالي: {nextPatient.patientName}</span>
                      {nextPatient.nextAction === "CallPatient" && (
                        <button onClick={() => handleCallPatient(nextPatient)}
                          className="px-2 py-0.5 rounded text-[10px] font-bold text-white" style={{ background: ORANGE }}>
                          نداء
                        </button>
                      )}
                      {nextPatient.nextAction === "EnterRoom" && (
                        <button onClick={() => handleEnterRoom(nextPatient)}
                          className="px-2 py-0.5 rounded text-[10px] font-bold text-white" style={{ background: "#9333ea" }}>
                          دخول
                        </button>
                      )}
                      {(nextPatient.nextAction === "Intake" || nextPatient.nextAction === "SendToQueue") && (
                        <button onClick={() => handleIntake(nextPatient)}
                          className="px-2 py-0.5 rounded text-[10px] font-bold text-white" style={{ background: "#16a34a" }}>
                          تسجيل
                        </button>
                      )}
                    </div>
                  )}
                </div>

                {/* Data Grid Area */}
                <div className="flex-1 overflow-auto bg-white">
                  <AppointmentsTable
                    items={filteredItems}
                    loading={itemsLoading}
                    isDoctor={isDoctor}
                    canProcessCheckout={canProcessCheckout}
                    isReception={userRole === "Reception"}
                    isAccountant={userRole === "Accountant"}
                    queueWaitTime={queueWaitTime}
                    onIntake={handleIntake}
                    onSendToQueue={handleSendToQueue}
                    onCallPatient={handleCallPatient}
                    onEnterRoom={handleEnterRoom}
                    onQuickPayment={handleQuickPayment}
                    onCreateDraftInvoice={handleCreateDraftInvoice}
                    createDraftInvoicePending={createDraftInvoiceMutation.isPending}
                    onBookAppointment={handleBookAppointment}
                    onWhatsApp={handleWhatsApp}
                    onNoShow={handleNoShow}
                    onCancel={handleCancel}
                    onViewPatient={handleViewPatient}
                    onCompleteVisit={handleCompleteVisit}
                    onOpenSidePanel={handleOpenSidePanel}
                    selectedPatientId={sidePanelOpen ? sidePanelItem?.patientId : undefined}
                    onContextMenu={handleItemContextMenu}
                  />
                </div>
              </div>
            )}

            {/* Tab: رحلة المرضى — يفتح الواجهة الأصلية المعتمدة */}
            {activeModule === "journey" && (
              <div className="flex-1 flex min-w-0 overflow-hidden bg-[#f4f8fc]">
                <DailyOperationsJourneyWorkspace
                  items={journeyItems}
                  loading={itemsLoading}
                  selectedItem={sidePanelItem}
                  summary={selectedSummary ?? null}
                  waitTime={queueWaitTime}
                  dayStats={dayStats}
                  filterDate={filterDate}
                  onRefresh={refetchItems}
                  onSelect={(item) => {
                    setSidePanelItem(item);
                    setSidePanelOpen(false);
                  }}
                  onClearSelected={() => setSidePanelItem(null)}
                  onContextMenu={handleItemContextMenu}
                  onQuickPayment={handleQuickPayment}
                  onCreateLabOrder={handleCreateLabOrder}
                  onBookAppointment={handleBookAppointment}
                  onWhatsApp={handleWhatsApp}
                  onViewPatient={handleViewPatient}
                  medicalAlerts={panelMedicalAlerts}
                  finance={panelFinance}
                  activeContract={panelActiveContract}
                  activeOrtho={panelActiveOrtho}
                />
              </div>
            )}

            {/* Tab: قائمة الانتظار (queue) */}
            {activeModule === "queue" && (
              <div className="flex-1 flex flex-col min-w-0 bg-[#f8fafc]">
                <ClinicQueueView searchQuery={searchQuery} onContextMenu={handleItemContextMenu} onOpenSidePanel={handleOpenSidePanel} />
              </div>
            )}

            {/* Tab: الغرف والعيادات (rooms) */}
            {activeModule === "rooms" && (
              <div className="flex-1 flex flex-col min-w-0 bg-[#f8fafc]">
                <RoomsView />
              </div>
            )}

            {/* Tab: جاهز للمحاسبة (checkout) */}
            {activeModule === "checkout" && (
              <div className="flex-1 flex flex-col min-w-0">
                {/* Heading banner for checkout list */}
                <div className="bg-white p-3 border-b flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <CreditCard className="w-5 h-5 text-emerald-500" />
                    <div>
                      <h3 className="text-xs font-bold" style={{ color: NAVY }}>المرضى الجاهزون للمحاسبة والتحصيل</h3>
                      <p className="text-[10px] text-gray-400 font-medium">قائمة الحالات التي تم تسليمها من الأطباء بانتظار سداد الفاتورة والخروج</p>
                    </div>
                  </div>
                  {tabCounts.payments > 0 && (
                    <span className="text-[11px] font-bold px-2 py-0.5 rounded-full bg-emerald-50 text-emerald-600 border border-emerald-200">
                      {tabCounts.payments} حالات معلقة
                    </span>
                  )}
                </div>

                <div className="flex-1 overflow-auto bg-white">
                  <AppointmentsTable
                    items={items.filter(i => i.checkoutStatus === "ReadyForCheckout" || i.nextAction === "Checkout" || (i.appointmentStatus === "Completed" && i.checkoutStatus !== "CheckedOut"))}
                    loading={itemsLoading}
                    isDoctor={isDoctor}
                    canProcessCheckout={canProcessCheckout}
                    isReception={userRole === "Reception"}
                    isAccountant={userRole === "Accountant"}
                    queueWaitTime={queueWaitTime}
                    onIntake={handleIntake}
                    onSendToQueue={handleSendToQueue}
                    onCallPatient={handleCallPatient}
                    onEnterRoom={handleEnterRoom}
                    onQuickPayment={handleQuickPayment}
                    onCreateDraftInvoice={handleCreateDraftInvoice}
                    createDraftInvoicePending={createDraftInvoiceMutation.isPending}
                    onBookAppointment={handleBookAppointment}
                    onWhatsApp={handleWhatsApp}
                    onNoShow={handleNoShow}
                    onCancel={handleCancel}
                    onViewPatient={handleViewPatient}
                    onCompleteVisit={handleCompleteVisit}
                    onOpenSidePanel={handleOpenSidePanel}
                    selectedPatientId={sidePanelOpen ? sidePanelItem?.patientId : undefined}
                    onContextMenu={handleItemContextMenu}
                  />
                </div>
              </div>
            )}

            {/* Tab: طلبات الحجز (booking) */}
            {activeModule === "booking" && (
              <div className="flex-1 flex flex-col min-w-0 bg-[#f8fafc]">
                <BookingRequestsView searchQuery={searchQuery} />
              </div>
            )}

            {/* Tab: المعمل (lab) */}
            {activeModule === "lab" && (
              <LabView />
            )}

            {/* Tab: تقرير اليوم (report) */}
            {activeModule === "report" && (
              <ReportView />
            )}

          </div>

          {/* Sticky Right Patient Detail Panel (25%) */}
          {sidePanelOpen && sidePanelItem && (
            <>
              {/* Desktop: inline panel */}
              <div className="hidden lg:flex w-[380px] flex-shrink-0 flex-col bg-white animate-panel-slide overflow-hidden"
                style={{ borderRight: "1px solid #e5e7eb" }}>
                <PatientDetailPanel
                  item={sidePanelItem}
                  summary={selectedSummary ?? null}
                  waitTime={queueWaitTime}
                  onClose={() => setSidePanelOpen(false)}
                  onQuickPayment={handleQuickPayment}
                  onCreateLabOrder={handleCreateLabOrder}
                  onBookAppointment={handleBookAppointment}
                  onWhatsApp={handleWhatsApp}
                  onViewPatient={handleViewPatient}
                  medicalAlerts={panelMedicalAlerts}
                  finance={panelFinance}
                  activeContract={panelActiveContract}
                  activeOrtho={panelActiveOrtho}
                />
              </div>

              {/* Mobile: full-screen overlay */}
              <div className="lg:hidden fixed inset-0 z-50 flex">
                <div className="flex-1 bg-black/30" onClick={() => setSidePanelOpen(false)} />
                <div className="w-full max-w-md bg-white overflow-y-auto animate-slide-in-right">
                  <PatientDetailPanel
                    item={sidePanelItem}
                    summary={selectedSummary ?? null}
                    waitTime={queueWaitTime}
                    onClose={() => setSidePanelOpen(false)}
                    onQuickPayment={handleQuickPayment}
                    onCreateLabOrder={handleCreateLabOrder}
                    onBookAppointment={handleBookAppointment}
                    onWhatsApp={handleWhatsApp}
                    onViewPatient={handleViewPatient}
                    medicalAlerts={panelMedicalAlerts}
                    finance={panelFinance}
                    activeContract={panelActiveContract}
                    activeOrtho={panelActiveOrtho}
                  />
                </div>
              </div>
            </>
          )}
        </div>

        {/* ═══════════════════════════════════════════════════════════════════
            STATUS CARDS BAR — replaces the old 8px navy text-status bar.
            5 prominent clickable cards + a compact row below (SignalR,
            finance, rooms, date). Clicking a card filters the view.
            ═══════════════════════════════════════════════════════════════════ */}
        <StatusCardsBar
          dayStats={dayStats}
          readyForCheckoutCount={tabCounts.payments}
          activeTab={activeTab}
          activeModule={activeModule as ModuleTabKey}
          onTabSelect={(tab) => {
            // Switching a sub-tab also routes the user back to the
            // appointments module so the filter is actually visible.
            if (activeModule !== "appointments") setActiveModule("appointments");
            setActiveTab(tab);
          }}
          onModuleSelect={(mod) => setActiveModule(mod)}
          signalrConnected={signalrConnected}
          filterDate={filterDate}
          roomOccupancy={roomOccupancy}
        />
      </div>

      {/* ═══════════════════════════════════════════════════════════════════
          MODALS (exactly as before)
          ═══════════════════════════════════════════════════════════════════ */}
      <QuickPaymentModal
        open={paymentModalOpen}
        onClose={() => setPaymentModalOpen(false)}
        item={selectedItem}
        summary={selectedSummary ?? null}
        isPending={createPaymentMutation.isPending}
        onConfirm={handlePaymentConfirm}
      />

      <CompleteVisitModal
        open={completeVisitModalOpen}
        onClose={() => setCompleteVisitModalOpen(false)}
        item={selectedItem}
        summary={selectedSummary ?? null}
        isPending={checkoutMutation.isPending || handoffMutation.isPending}
        onConfirm={handleCompleteVisitConfirm}
        onCheckout={handleCheckoutConfirm}
        onQuickPayment={handleQuickPayment}
        onCreateDraftInvoice={handleCreateDraftInvoice}
        createDraftInvoicePending={createDraftInvoiceMutation.isPending}
      />

      {leftWithoutModalOpen && (
        <div className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4" onClick={() => setLeftWithoutModalOpen(false)}>
          <div className="w-full max-w-md rounded-2xl bg-white shadow-xl" onClick={(event) => event.stopPropagation()}>
            <div className="flex items-center gap-3 border-b border-[#e8f0f9] px-5 py-4">
              <div className="w-9 h-9 rounded-lg flex items-center justify-center bg-red-50">
                <AlertTriangle className="w-4.5 h-4.5 text-red-600" />
              </div>
              <div className="flex-1">
                <h3 className="font-extrabold text-[15px]" style={{ color: NAVY }}>خرج بدون إكمال</h3>
                <p className="text-[11px] text-gray-500 mt-0.5">{selectedItem?.patientName}</p>
              </div>
              <button type="button" onClick={() => setLeftWithoutModalOpen(false)} className="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-gray-100 transition">
                <X className="w-4 h-4 text-gray-400" />
              </button>
            </div>

            <div className="p-5 space-y-4">
              <div className="rounded-xl border border-red-100 bg-red-50 px-3 py-2 text-xs leading-6 text-red-700">
                هذه الحالة لا تُسجل دفعاً ولا تلغي بيانات المعمل. ستغلق مسار الزيارة تشغيلياً وتظهر في تقرير نهاية اليوم.
              </div>
              <div>
                <label className="block text-xs font-bold mb-1.5" style={{ color: NAVY }}>سبب الخروج بدون إكمال *</label>
                <textarea
                  value={leftWithoutReason}
                  onChange={(event) => setLeftWithoutReason(event.target.value)}
                  rows={4}
                  className="w-full rounded-xl border border-gray-200 px-3 py-2 text-sm outline-none focus:border-[#3d7ab5] focus:ring-2 focus:ring-[#3d7ab5]/15"
                  placeholder="مثال: المريض غادر قبل استكمال الإجراء / رفض الانتظار / ظرف طارئ"
                />
              </div>
              <div className="flex justify-end gap-2">
                <button
                  type="button"
                  onClick={() => setLeftWithoutModalOpen(false)}
                  className="rounded-lg border border-gray-200 px-4 py-2 text-xs font-bold text-gray-600 hover:bg-gray-50"
                >
                  تراجع
                </button>
                <button
                  type="button"
                  disabled={leftWithoutCompletionMutation.isPending}
                  onClick={handleLeftWithoutCompletionConfirm}
                  className="rounded-lg px-4 py-2 text-xs font-bold text-white disabled:opacity-60"
                  style={{ background: "#dc2626" }}
                >
                  {leftWithoutCompletionMutation.isPending ? "جار التسجيل..." : "تسجيل الحالة"}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      <BookAppointmentModal
        open={bookAppointmentModalOpen}
        onClose={() => setBookAppointmentModalOpen(false)}
        item={selectedItem}
        doctors={doctors}
        services={services}
        isPending={createAppointmentMutation.isPending}
        onConfirm={handleBookConfirm}
      />

      {/* Lab Order Modal */}
      {labOrderModalOpen && (
        <NewLabOrderModal
          onClose={() => setLabOrderModalOpen(false)}
          initialPatient={selectedItem ? { id: selectedItem.patientId, displayName: selectedItem.patientName } : undefined}
        />
      )}

      <ConfirmDialog
        open={confirmDialogOpen}
        onClose={() => setConfirmDialogOpen(false)}
        type={confirmDialogType}
        patientName={selectedItem?.patientName ?? ""}
        isPending={updateStatusMutation.isPending || cancelQueueMutation.isPending || completeVisitMutation.isPending}
        onConfirm={handleConfirmAction}
      />

      <WhatsAppMenu
        open={whatsAppMenuOpen}
        onClose={() => setWhatsAppMenuOpen(false)}
        item={selectedItem}
        summary={selectedSummary ?? null}
        clinicName={clinicName}
      />

      <ChangeRoomModal
        open={changeRoomModalOpen}
        onClose={() => setChangeRoomModalOpen(false)}
        rooms={rooms}
        isPending={changeRoomMutation.isPending}
        onConfirm={async (roomName: string) => {
          if (!selectedItem?.queueItemId) return;
          try {
            await changeRoomMutation.mutateAsync({ queueItemId: selectedItem.queueItemId, roomName });
            toast.success("تم تغيير الغرفة");
            setChangeRoomModalOpen(false);
          } catch {
            toast.error("فشل تغيير الغرفة");
          }
        }}
      />

      <WalkInModal
        open={walkInModalOpen}
        onClose={() => setWalkInModalOpen(false)}
        doctors={doctors}
        services={services}
        branches={branches}
        isPending={walkInMutation.isPending}
        onConfirm={handleWalkInConfirm}
      />

      <OverrideDialog
        open={overrideOpen}
        onClose={() => setOverrideOpen(false)}
        patientName={pendingOverrideAction?.item?.patientName ?? ""}
        overdueAmount={pendingOverrideAction?.overdueAmount ?? 0}
        onConfirm={handleOverrideConfirm}
      />

      {undoAction && (
        <UndoToast
          action={undoAction}
          onUndo={handleUndo}
          onDismiss={() => setUndoAction(null)}
        />
      )}

      <KeyboardShortcutsHelp
        open={shortcutsHelpOpen}
        onClose={() => setShortcutsHelpOpen(false)}
      />

      <BulkSmsModal
        open={bulkSmsModalOpen}
        onClose={() => setBulkSmsModalOpen(false)}
        tomorrowItems={tomorrowItems}
        isPending={bulkSmsMutation.isPending}
        onConfirm={handleBulkSms}
      />

      {/* Direct Payment Modal (for unbooked patients) */}
      <DirectPaymentModal
        open={directPaymentModalOpen}
        onClose={() => setDirectPaymentModalOpen(false)}
        isPending={createPaymentMutation.isPending}
        onConfirm={handleDirectPaymentConfirm}
      />

      {/* ── Right-click Context Menu for Journey Items ── */}
      <JourneyContextMenu
        item={ctxMenu?.item ?? null}
        position={ctxMenu?.position ?? null}
        isDoctor={isDoctor}
        canProcessCheckout={canProcessCheckout}
        onClose={() => setCtxMenu(null)}
        onIntake={handleIntake}
        onSendToQueue={handleSendToQueue}
        onCallPatient={handleCallPatient}
        onEnterRoom={handleEnterRoom}
        onQuickPayment={handleQuickPayment}
        onCreateDraftInvoice={handleCreateDraftInvoice}
        onCompleteVisit={handleCompleteVisit}
        onCreateLabOrder={handleCreateLabOrder}
        onLeftWithoutCompletion={handleLeftWithoutCompletion}
        onBookAppointment={handleBookAppointment}
        onWhatsApp={handleWhatsApp}
        onNoShow={handleNoShow}
        onCancel={handleCancel}
        onViewPatient={handleViewPatient}
        onOpenSidePanel={handleOpenSidePanel}
      />

      {/* Room Selection Dialog for Call/Recall */}
      {roomSelectOpen && (
        <RoomSelectDialog
          rooms={rooms}
          onConfirm={handleRoomConfirm}
          onCancel={() => { setRoomSelectOpen(false); setPendingCallItem(null); }}
          loading={callPatientMutation.isPending || recallPatientMutation.isPending}
        />
      )}
    </>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Patient Detail Panel — Inline component for the right panel
   ═══════════════════════════════════════════════════════════════════════════ */
function DailyOperationsJourneyWorkspace({
  items,
  loading,
  selectedItem,
  summary,
  waitTime,
  dayStats,
  filterDate,
  onRefresh,
  onSelect,
  onClearSelected,
  onContextMenu,
  onQuickPayment,
  onCreateLabOrder,
  onBookAppointment,
  onWhatsApp,
  onViewPatient,
  medicalAlerts,
  finance,
  activeContract,
  activeOrtho,
}: {
  items: TodayJourneyItem[];
  loading: boolean;
  selectedItem: TodayJourneyItem | null;
  summary: DailyJourneySummary | null;
  waitTime?: { estimatedMinutes: number; patientsAhead: number } | null;
  dayStats: ReturnType<typeof computeDayStats>;
  filterDate: string;
  onRefresh: () => void;
  onSelect: (item: TodayJourneyItem) => void;
  onClearSelected: () => void;
  onContextMenu: (e: React.MouseEvent, item: TodayJourneyItem) => void;
  onQuickPayment: (item: TodayJourneyItem) => void;
  onCreateLabOrder: (item: TodayJourneyItem) => void;
  onBookAppointment: (item: TodayJourneyItem) => void;
  onWhatsApp: (item: TodayJourneyItem) => void;
  onViewPatient: (item: TodayJourneyItem) => void;
  medicalAlerts: DailyJourneySummary["medicalAlerts"];
  finance: DailyJourneySummary["financeSummary"] | undefined;
  activeContract: DailyJourneySummary["activeContract"] | undefined;
  activeOrtho: DailyJourneySummary["activeOrthoCase"] | undefined;
}) {
  return (
    <div className="flex-1 min-w-0 flex overflow-hidden">
      <aside className="w-[360px] max-w-[42vw] flex-shrink-0 bg-white border-l border-[#e5edf5] flex flex-col">
        <div className="p-3 border-b border-[#edf2f7] flex items-center justify-between gap-2">
          <div>
            <h3 className="text-[13px] font-extrabold" style={{ color: NAVY }}>رحلة المرضى اليومية</h3>
            <p className="text-[10px] text-gray-500 mt-0.5">من الوصول إلى الحساب والخروج داخل التشغيل اليومي</p>
          </div>
          <button
            type="button"
            onClick={onRefresh}
            className="h-8 px-2.5 rounded-lg border border-[#dbe6f1] bg-white text-[10px] font-bold text-[#48627d] flex items-center gap-1 hover:bg-[#f7fbff]"
          >
            <RefreshCw className="w-3.5 h-3.5" />
            تحديث
          </button>
        </div>

        <div className="grid grid-cols-3 gap-2 p-3 border-b border-[#edf2f7]">
          <div className="rounded-xl bg-blue-50 px-2 py-2 text-center">
            <p className="text-[15px] font-extrabold text-[#1a3a5c]">{items.length}</p>
            <p className="text-[9px] font-bold text-[#6b7f96]">رحلة</p>
          </div>
          <div className="rounded-xl bg-amber-50 px-2 py-2 text-center">
            <p className="text-[15px] font-extrabold text-[#b45309]">{dayStats.waiting}</p>
            <p className="text-[9px] font-bold text-[#92400e]">انتظار</p>
          </div>
          <div className="rounded-xl bg-emerald-50 px-2 py-2 text-center">
            <p className="text-[15px] font-extrabold text-[#047857]">{dayStats.completed}</p>
            <p className="text-[9px] font-bold text-[#047857]">مكتمل</p>
          </div>
        </div>

        <div className="flex-1 overflow-auto">
          {loading ? (
            <div className="h-full min-h-[220px] flex items-center justify-center text-xs font-bold text-gray-400">
              جار تحميل رحلة المرضى...
            </div>
          ) : items.length === 0 ? (
            <div className="h-full min-h-[220px] flex flex-col items-center justify-center text-center px-6">
              <Activity className="w-10 h-10 text-gray-200 mb-3" />
              <p className="text-sm font-bold text-gray-500">لا توجد رحلات مطابقة</p>
              <p className="text-[11px] text-gray-400 mt-1">غيّر البحث أو الفلاتر لعرض المرضى</p>
            </div>
          ) : (
            items.map((item) => {
              const isSelected = selectedItem?.patientId === item.patientId;
              const statusColor = STATUS_COLORS[item.appointmentStatus] ?? STATUS_COLORS.Scheduled;
              const initials = item.patientName.split(" ").filter(Boolean).slice(0, 2).map(part => part[0]).join("") || "P";
              return (
                <button
                  key={`${item.appointmentId ?? item.visitId ?? item.patientId}-journey`}
                  type="button"
                  onClick={() => onSelect(item)}
                  onContextMenu={(event) => onContextMenu(event, item)}
                  className="w-full px-3 py-3 text-right border-b border-[#f0f3f7] hover:bg-[#f8fbff] transition"
                  style={{
                    background: isSelected ? "#eef6ff" : "#fff",
                    borderRight: isSelected ? `3px solid ${BLUE}` : "3px solid transparent",
                  }}
                >
                  <div className="flex items-center gap-2.5">
                    <div className="w-9 h-9 rounded-xl flex items-center justify-center text-[11px] font-extrabold text-white flex-shrink-0" style={{ background: statusColor.text }}>
                      {initials}
                    </div>
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-1.5">
                        <p className="text-[12px] font-extrabold truncate" style={{ color: NAVY }}>{item.patientName}</p>
                        <span className="text-[9px] px-1.5 py-0.5 rounded-full font-bold flex-shrink-0" style={{ background: statusColor.bg, color: statusColor.text }}>
                          {APPT_STATUS_LABELS[item.appointmentStatus] ?? item.appointmentStatus}
                        </span>
                      </div>
                      <p className="text-[10px] text-gray-500 truncate mt-0.5">
                        {item.doctorName} · {item.serviceName ?? "بدون خدمة"} · {item.appointmentTime ?? fmtDate(filterDate)}
                      </p>
                      <p className="text-[10px] font-semibold mt-1" style={{ color: BLUE }}>
                        التالي: {ACTION_LABELS[item.nextAction] ?? item.nextAction}
                      </p>
                    </div>
                  </div>
                </button>
              );
            })
          )}
        </div>
      </aside>

      <section className="flex-1 min-w-0 overflow-auto bg-white">
        {selectedItem ? (
          <PatientDetailPanel
            item={selectedItem}
            summary={summary}
            waitTime={waitTime}
            onClose={onClearSelected}
            onQuickPayment={onQuickPayment}
            onCreateLabOrder={onCreateLabOrder}
            onBookAppointment={onBookAppointment}
            onWhatsApp={onWhatsApp}
            onViewPatient={onViewPatient}
            medicalAlerts={medicalAlerts}
            finance={finance}
            activeContract={activeContract}
            activeOrtho={activeOrtho}
          />
        ) : (
          <div className="h-full min-h-[420px] flex flex-col items-center justify-center text-center px-8">
            <div className="w-16 h-16 rounded-2xl flex items-center justify-center mb-4" style={{ background: `${NAVY}10` }}>
              <Activity className="w-8 h-8" style={{ color: NAVY }} />
            </div>
            <h2 className="text-lg font-extrabold mb-2" style={{ color: NAVY }}>اختر مريضاً من القائمة</h2>
            <p className="text-sm text-gray-500 max-w-md leading-7">
              ستظهر هنا تفاصيل رحلة المريض، حالته الحالية، التنبيهات الطبية، الرصيد، والإجراءات السريعة للتشغيل اليومي.
            </p>
          </div>
        )}
      </section>
    </div>
  );
}

function PatientDetailPanel({
  item, summary, waitTime, onClose,
  onQuickPayment, onCreateLabOrder, onBookAppointment, onWhatsApp, onViewPatient,
  medicalAlerts, finance, activeContract, activeOrtho,
}: {
  item: TodayJourneyItem;
  summary: DailyJourneySummary | null;
  waitTime?: { estimatedMinutes: number; patientsAhead: number } | null;
  onClose: () => void;
  onQuickPayment: (item: TodayJourneyItem) => void;
  onCreateLabOrder: (item: TodayJourneyItem) => void;
  onBookAppointment: (item: TodayJourneyItem) => void;
  onWhatsApp: (item: TodayJourneyItem) => void;
  onViewPatient: (item: TodayJourneyItem) => void;
  medicalAlerts: DailyJourneySummary["medicalAlerts"];
  finance: DailyJourneySummary["financeSummary"] | undefined;
  activeContract: DailyJourneySummary["activeContract"] | undefined;
  activeOrtho: DailyJourneySummary["activeOrthoCase"] | undefined;
}) {
  const getInitials = (name: string) => {
    const parts = name.split(" ").filter(Boolean);
    if (parts.length >= 2) return parts[0][0] + parts[1][0];
    return parts[0]?.[0] ?? "?";
  };

  return (
    <div className="flex flex-col h-full">
      {/* Header */}
      <div className="flex-shrink-0 px-4 py-3 flex items-start gap-3"
        style={{ borderBottom: "1px solid #e5e7eb" }}>
        <button onClick={onClose}
          className="w-7 h-7 rounded-lg flex items-center justify-center hover:bg-gray-100 transition flex-shrink-0 mt-0.5">
          <X className="w-4 h-4" style={{ color: "#64748b" }} />
        </button>
        <div className="flex items-center gap-3 flex-1 min-w-0">
          {/* Avatar */}
          <div className="w-10 h-10 rounded-full flex items-center justify-center flex-shrink-0 text-sm font-bold text-white"
            style={{ background: NAVY }}>
            {getInitials(item.patientName)}
          </div>
          <div className="min-w-0">
            <div className="text-sm font-extrabold truncate" style={{ color: NAVY }}>{item.patientName}</div>
            {item.patientPhone && (
              <div className="text-[11px] font-medium" style={{ color: "#64748b" }}>
                <Phone className="w-3 h-3 inline ml-1" />
                {item.patientPhone}
              </div>
            )}
            <div className="text-[11px]" style={{ color: "#94a3b8" }}>
              {item.doctorName} — {item.serviceName ?? "—"}
            </div>
          </div>
        </div>
      </div>

      {/* Scrollable content */}
      <div className="flex-1 overflow-y-auto p-4 space-y-4">
        {/* Medical Alerts */}
        {medicalAlerts && medicalAlerts.length > 0 && (
          <div>
            <div className="text-[11px] font-bold mb-1.5 flex items-center gap-1" style={{ color: "#ef4444" }}>
              <AlertTriangle className="w-3 h-3" /> تنبيهات طبية
            </div>
            <div className="space-y-1">
              {medicalAlerts.map((alert, i) => (
                <div key={i} className="px-2.5 py-1.5 rounded-lg text-[11px] font-medium"
                  style={{
                    background: alert.severity === "danger" ? "#fef2f2" : alert.severity === "warning" ? "#fff7ed" : "#f0f5fb",
                    color: alert.severity === "danger" ? "#dc2626" : alert.severity === "warning" ? "#d97706" : NAVY,
                    border: `1px solid ${alert.severity === "danger" ? "#fecaca" : alert.severity === "warning" ? "#fde8d0" : "#dce8f5"}`,
                  }}>
                  {alert.label ?? alert.type ?? "تنبيه"}
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Quick Status */}
        <div className="flex items-center gap-2 flex-wrap">
          <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-bold"
            style={{
              background: STATUS_COLORS[item.appointmentStatus]?.bg ?? "#f5f5f5",
              color: STATUS_COLORS[item.appointmentStatus]?.text ?? "#6b7280",
              border: `1px solid ${STATUS_COLORS[item.appointmentStatus]?.border ?? "#e5e7eb"}`,
            }}>
            {APPT_STATUS_LABELS[item.appointmentStatus] ?? item.appointmentStatus}
          </span>
          {item.nextAction && item.nextAction !== "None" && (
            <span className="inline-flex items-center px-2 py-0.5 rounded text-[10px] font-bold"
              style={{ background: ORANGE + "12", color: ORANGE }}>
              {ACTION_LABELS[item.nextAction] ?? item.nextAction}
            </span>
          )}
          {waitTime && waitTime.estimatedMinutes > 0 && (
            <span className="text-[10px] font-bold" style={{ color: ORANGE }}>
              ~{waitTime.estimatedMinutes}د انتظار
            </span>
          )}
        </div>

        {/* Financial Summary */}
        {finance && (
          <div>
            <div className="text-[11px] font-bold mb-1.5 flex items-center justify-between" style={{ color: NAVY }}>
              <span>💰 الملخص المالي</span>
              {finance.latestPayment?.id && (
                <button onClick={async () => {
                  try {
                    const { downloadPdfFromApi } = await import("@/lib/pdfDownload");
                    const filename = `receipt-${finance.latestPayment!.receiptNumber || finance.latestPayment!.id}.pdf`;
                    await downloadPdfFromApi(`/api/payments/${finance.latestPayment!.id}/pdf`, filename);
                  } catch (err) {
                    const reason = err instanceof Error ? err.message : "خطأ";
                    toast.error(`فشل تحميل السند: ${reason}`);
                  }
                }}
                  className="flex items-center gap-1 px-2 py-0.5 rounded text-[9px] font-bold transition hover:bg-purple-100"
                  style={{ color: "#7c3aed", background: "#7c3aed10", border: "1px solid #7c3aed30" }}
                  title="طباعة سند آخر دفعة">
                  <Printer className="w-3 h-3" />
                  طباعة سند
                </button>
              )}
            </div>
            <div className="grid grid-cols-3 gap-1.5">
              <div className="p-2 rounded-lg text-center" style={{ background: "#fff7ed" }}>
                <div className="text-[9px] font-medium" style={{ color: ORANGE }}>المستحق</div>
                <div className="text-xs font-bold" style={{ color: NAVY }}>{fmtRial(finance.outstandingBalance)}</div>
              </div>
              <div className="p-2 rounded-lg text-center" style={{ background: finance.overdueAmount > 0 ? "#fef2f2" : "#f0fdf4" }}>
                <div className="text-[9px] font-medium" style={{ color: finance.overdueAmount > 0 ? "#ef4444" : "#16a34a" }}>متأخرات</div>
                <div className="text-xs font-bold" style={{ color: NAVY }}>{fmtRial(finance.overdueAmount)}</div>
              </div>
              <div className="p-2 rounded-lg text-center" style={{ background: "#f0fdf4" }}>
                <div className="text-[9px] font-medium" style={{ color: "#16a34a" }}>المدفوع</div>
                <div className="text-xs font-bold" style={{ color: NAVY }}>{fmtRial(finance.totalPaid ?? 0)}</div>
              </div>
            </div>
          </div>
        )}

        {/* Active Contract / Ortho */}
        {(activeContract || activeOrtho) && (
          <div>
            <div className="text-[11px] font-bold mb-1.5" style={{ color: NAVY }}>
              {activeOrtho ? "🦷 تقويم نشط" : "📋 عقد نشط"}
            </div>
            {activeContract && (
              <div className="p-2.5 rounded-lg" style={{ background: "#f0f5fb", border: "1px solid #dce8f5" }}>
                <div className="text-[11px] font-bold" style={{ color: NAVY }}>
                  {activeContract.specialty ?? "عقد علاج"}
                </div>
                <div className="mt-1.5">
                  <div className="flex items-center justify-between text-[10px] mb-0.5">
                    <span style={{ color: "#64748b" }}>التقدم</span>
                    <span className="font-bold" style={{ color: BLUE }}>
                      {fmtRial(activeContract.paidAmount)} / {fmtRial(activeContract.totalAmount)}
                    </span>
                  </div>
                  <div className="h-1.5 rounded-full overflow-hidden" style={{ background: "#e2e8f0" }}>
                    <div className="h-full rounded-full transition-all" style={{
                      width: `${activeContract.totalAmount ? (activeContract.paidAmount / activeContract.totalAmount) * 100 : 0}%`,
                      background: BLUE,
                    }} />
                  </div>
                </div>
              </div>
            )}
            {activeOrtho && !activeContract && (
              <div className="p-2.5 rounded-lg" style={{ background: "#faf5ff", border: "1px solid #e9d5ff" }}>
                <div className="text-[11px] font-bold" style={{ color: "#9333ea" }}>
                  حالة تقويم نشطة
                </div>
              </div>
            )}
          </div>
        )}

        {/* Recent Visits */}
        {summary?.recentVisits && summary.recentVisits.length > 0 && (
          <div>
            <div className="text-[11px] font-bold mb-1.5" style={{ color: NAVY }}>📅 زيارات سابقة</div>
            <div className="space-y-1">
              {summary.recentVisits.slice(0, 3).map((visit, i) => (
                <div key={i} className="px-2.5 py-1.5 rounded-lg text-[11px] flex items-center justify-between"
                  style={{ background: "#f8fafc", border: "1px solid #f1f5f9" }}>
                  <span className="font-medium" style={{ color: NAVY }}>
                    {visit.treatmentDone ?? visit.chiefComplaint ?? "زيارة"}
                  </span>
                  <span style={{ color: "#94a3b8" }}>{visit.visitDate ? fmtDate(visit.visitDate) : "—"}</span>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>

      {/* Quick Actions (sticky bottom) */}
      <div className="flex-shrink-0 p-3 flex items-center gap-1.5 border-t"
        style={{ borderColor: "#e5e7eb", background: "#fafbfc" }}>
        <button onClick={() => onQuickPayment(item)}
          className="flex items-center gap-1 px-2.5 py-1.5 rounded-lg text-[11px] font-bold transition hover:opacity-80"
          style={{ background: "#22c55e15", color: "#22c55e", border: "1px solid #22c55e30" }}>
          <CreditCard className="w-3 h-3" />
          دفع
        </button>
        <button onClick={() => onBookAppointment(item)}
          className="flex items-center gap-1 px-2.5 py-1.5 rounded-lg text-[11px] font-bold transition hover:opacity-80"
          style={{ background: BLUE + "12", color: BLUE, border: `1px solid ${BLUE}30` }}>
          <Calendar className="w-3 h-3" />
          حجز
        </button>
        <button onClick={() => onCreateLabOrder(item)}
          className="flex items-center gap-1 px-2.5 py-1.5 rounded-lg text-[11px] font-bold transition hover:opacity-80"
          style={{ background: "#0891b214", color: "#0891b2", border: "1px solid #0891b230" }}>
          <ClipboardList className="w-3 h-3" />
          طلب معمل
        </button>
        {item.patientPhone && (
          <button onClick={() => onWhatsApp(item)}
            className="flex items-center gap-1 px-2.5 py-1.5 rounded-lg text-[11px] font-bold transition hover:opacity-80"
            style={{ background: "#25D36612", color: "#25D366", border: "1px solid #25D36630" }}>
            <MessageCircle className="w-3 h-3" />
            واتساب
          </button>
        )}
        <div className="flex-1" />
        <button onClick={() => onViewPatient(item)}
          className="flex items-center gap-1 px-2.5 py-1.5 rounded-lg text-[11px] font-bold transition hover:opacity-80"
          style={{ background: NAVY + "0d", color: NAVY, border: `1px solid ${NAVY}20` }}>
          فتح الملف
        </button>
      </div>
    </div>
  );
}
