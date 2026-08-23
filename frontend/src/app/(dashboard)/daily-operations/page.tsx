"use client";
import { printScreen } from "@/lib/printUtils";
import { getDailyOperationsModuleTab, type DailyOperationsModuleTab } from "@/lib/dailyOperationsRoute";

import { useState, useCallback, useMemo, useEffect, useRef } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import {
  Calendar, ClipboardList, CreditCard, Clock, CheckCircle,
  Stethoscope, AlertTriangle, Search, RefreshCw,
  Globe,
  Wallet, UserPlus, Keyboard, Bell, BellOff,
  Printer, Download, Activity, Megaphone, Building2,
  X,
  ClipboardCheck, LogIn, BellRing, CalendarPlus,
  Monitor, MoreHorizontal,
} from "lucide-react";
import { useHasPermission, PERMISSION_KEYS } from "@/hooks/usePermissions";
import { useAuthStore } from "@/stores/authStore";
import { toast } from "@/stores/toastStore";
import { extractErrorMessage } from "@/lib/errors";
import { useSignalRClinicQueue } from "@/hooks/useSignalRClinicQueue";
import api from "@/lib/api";
import { NewLabOrderModal } from "@/components/lab/NewLabOrderModal";
import { useT, useTf } from "@/i18n/LocaleProvider";

import {
  NAVY, BLUE, ORANGE,
  TABS,
  overdueOverrideNote,
  getTodayStr, fmtRial,
  computeDayStats, filterByTab,
  computeRoomOccupancy, getNextPatient,
  isDoctorRole,
  type TodayJourneyItem, type TabKey, type UndoAction,
} from "./_lib/constants";

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
  useStartVisit,
  useUpdateAppointmentStatus,
  useCreatePayment,
  useCreateDraftInvoice,
  useCheckout,
  useValidateFinancialClosure,
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
// Sprint 11B: extracted inline components
import { RoomSelectDialog } from "./_components/RoomSelectDialog";
import { DailyOperationsJourneyWorkspace } from "./_components/JourneyWorkspace";
import { PatientDetailPanel } from "./_components/PatientDetailPanel";
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
  FutureAppointmentOverrideDialog,
  type FutureAppointmentOverrideOperation,
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

function journeyBusinessDateErrorCode(error: unknown): string | undefined {
  if (!error || typeof error !== "object" || !("response" in error)) return undefined;
  return (error as { response?: { data?: { code?: string } } }).response?.data?.code;
}

function isFutureAppointmentBlock(error: unknown): boolean {
  return journeyBusinessDateErrorCode(error) === "future_appointment";
}



/* ═══════════════════════════════════════════════════════════════════════════
   Module tabs (top level navigation within daily operations)
   ═══════════════════════════════════════════════════════════════════════════ */
type ModuleTab = DailyOperationsModuleTab;

const MODULE_TABS: { key: ModuleTab; label: string; icon: React.ElementType; color: string }[] = [
  { key: "appointments", label: "dailyOps.module.appointments", icon: Calendar,      color: BLUE },
  { key: "journey",      label: "dailyOps.module.journey",      icon: Activity,      color: NAVY },
  { key: "queue",        label: "dailyOps.module.queue",        icon: ClipboardList, color: ORANGE },
  { key: "rooms",        label: "dailyOps.module.rooms",        icon: Building2,     color: NAVY },
  { key: "checkout",     label: "dailyOps.module.checkout",     icon: CreditCard,    color: "#22c55e" },
  { key: "booking",      label: "dailyOps.module.booking",      icon: Globe,         color: BLUE },
  { key: "lab",          label: "dailyOps.module.lab",          icon: Activity,      color: "#8b5cf6" },
  { key: "report",       label: "dailyOps.module.report",       icon: Wallet,        color: "#16a34a" },
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
   MAIN PAGE — Microsoft Fluent Design
   ═══════════════════════════════════════════════════════════════════════════ */
export default function DailyOperationsPage() {
  const t = useT();
  const tf = useTf();
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
  const [activeModule, setActiveModule] = useState<ModuleTab>(
    () => getDailyOperationsModuleTab(searchParams.get("tab")) ?? "appointments",
  );

  // ── SignalR real-time updates ──
  // The embedded queue tab owns a dedicated queue connection, so the page-level
  // connection pauses there to avoid duplicate event processing and duplicate sounds.
  const { isConnected: signalrConnected } = useSignalRClinicQueue({
    enabled: activeModule !== "queue",
    playSoundOnPatientCalled: activeModule !== "queue",
  });

  // ── Data ──
  const { data: items = [], isLoading: itemsLoading, isError: itemsError, refetch: refetchItems } = useTodayJourneyItems({
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
  const clinicName = clinicSettings?.clinicName ?? t("dailyOps.clinicFallback");

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
  const startVisitMutation = useStartVisit();
  const updateStatusMutation = useUpdateAppointmentStatus();
  const createPaymentMutation = useCreatePayment();
  const createDraftInvoiceMutation = useCreateDraftInvoice();
  const checkoutMutation = useCheckout();
  const validateFinancialClosureMutation = useValidateFinancialClosure();
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
  const [futureOverrideOpen, setFutureOverrideOpen] = useState(false);
  const [pendingFutureOverride, setPendingFutureOverride] = useState<{
    type: FutureAppointmentOverrideOperation;
    item: TodayJourneyItem;
    body?: { notes?: string; roomId?: string };
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
      toast.error(t("dailyOps.toast.selectPatient"));
      return;
    }
    const latestPaymentId = selectedSummary?.financeSummary?.latestPayment?.id;
    if (!latestPaymentId) {
      toast.error(t("dailyOps.toast.noPaymentToDownload"));
      return;
    }
    try {
      const { downloadPdfFromApi } = await import("@/lib/pdfDownload");
      const receiptNum = selectedSummary?.financeSummary?.latestPayment?.receiptNumber ?? latestPaymentId;
      await downloadPdfFromApi(`/api/payments/${latestPaymentId}/pdf`, `receipt-${receiptNum}.pdf`);
      toast.success(t("dailyOps.toast.receiptDownloaded"));
    } catch (err) {
      const reason = err instanceof Error ? err.message : t("common.genericError");
      toast.error(tf("dailyOps.toast.receiptDownloadFailed", { reason }));
    }
  }, [getActiveItem, selectedSummary, t, tf]);

  // ── Print receipt PDF for active item — prints the PDF itself, not the system page ──
  const handlePrintReceipt = useCallback(async () => {
    const item = getActiveItem();
    if (!item) {
      toast.error(t("dailyOps.toast.selectPatient"));
      return;
    }
    const latestPaymentId = selectedSummary?.financeSummary?.latestPayment?.id;
    if (!latestPaymentId) {
      toast.error(t("dailyOps.toast.noPaymentToPrint"));
      return;
    }
    try {
      const { printPdfFromApi } = await import("@/lib/pdfDownload");
      const receiptNum = selectedSummary?.financeSummary?.latestPayment?.receiptNumber ?? latestPaymentId;
      await printPdfFromApi(`/api/payments/${latestPaymentId}/pdf`, `receipt-${receiptNum}.pdf`);
    } catch (err) {
      const reason = err instanceof Error ? err.message : t("common.genericError");
      toast.error(tf("dailyOps.toast.receiptPrintFailed", { reason }));
    }
  }, [getActiveItem, selectedSummary, t, tf]);

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
  const openFutureOverrideIfAllowed = useCallback((
    error: unknown,
    type: FutureAppointmentOverrideOperation,
    item: TodayJourneyItem,
    body?: { notes?: string; roomId?: string },
  ) => {
    if (!isFutureAppointmentBlock(error)) return false;
    if (userRole !== "Admin") return false;

    setPendingFutureOverride({ type, item, body });
    setFutureOverrideOpen(true);
    return true;
  }, [userRole]);

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

      const notesSuffix = overrideManager ? overdueOverrideNote(overrideManager) : "";
      const actionBody = notesSuffix ? { notes: notesSuffix } : {};

      if (actionType === "Intake") {
        if (!item.appointmentId) { toast.error(t("dailyOps.toast.intakeNoAppointment")); return; }
        intakeMutation.mutate(
          { appointmentId: item.appointmentId, body: actionBody },
          {
            onSuccess: () => toast.success(t("dailyOps.toast.intakeDone")),
            onError: (err) => {
              if (openFutureOverrideIfAllowed(err, "Intake", item, actionBody)) return;
              toast.error(extractErrorMessage(err, t("dailyOps.toast.intakeFailed")));
            },
          },
        );
      } else {
        if (!item.appointmentId) { toast.error(t("dailyOps.toast.queueNoAppointment")); return; }
        sendToQueueMutation.mutate(
          { appointmentId: item.appointmentId, body: actionBody },
          {
            onSuccess: () => toast.success(t("dailyOps.toast.queueDone")),
            onError: (err) => {
              if (openFutureOverrideIfAllowed(err, "SendToQueue", item, actionBody)) return;
              toast.error(extractErrorMessage(err, t("dailyOps.toast.queueFailed")));
            },
          },
        );
      }
    } catch (err) {
      toast.error(extractErrorMessage(err, t("dailyOps.toast.actionFailed")));
    }
  }, [intakeMutation, sendToQueueMutation, openFutureOverrideIfAllowed, t]);

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
        { onSuccess: () => toast.success(t("dailyOps.toast.recallDone")), onError: (err) => toast.error(extractErrorMessage(err, t("dailyOps.toast.recallFailed"))) }
      );
    } else {
      callPatientMutation.mutate(
        { queueItemId: pendingCallItem.queueItemId, roomName },
        { onSuccess: () => toast.success(t("dailyOps.toast.callDone")), onError: (err) => toast.error(extractErrorMessage(err, t("dailyOps.toast.callFailed"))) }
      );
    }
    setRoomSelectOpen(false);
    setPendingCallItem(null);
  }, [pendingCallItem, callActionType, callPatientMutation, recallPatientMutation, t]);

  const handleEnterRoom = useCallback((item: TodayJourneyItem) => {
    if (!item.queueItemId) return;
    enterRoomMutation.mutate(
      item.queueItemId,
      { onSuccess: () => toast.success(t("dailyOps.toast.enterRoomDone")), onError: (err) => toast.error(extractErrorMessage(err, t("dailyOps.toast.enterRoomFailed"))) },
    );
  }, [enterRoomMutation, t]);

  const handleStartVisit = useCallback((item: TodayJourneyItem) => {
    if (!item.appointmentId) {
      toast.error(t("dailyOps.toast.startVisitNoAppointment"));
      return;
    }
    startVisitMutation.mutate(
      { appointmentId: item.appointmentId },
      {
        onSuccess: () => toast.success(t("dailyOps.toast.startVisitDone")),
        onError: (err) => {
          if (openFutureOverrideIfAllowed(err, "StartVisit", item)) return;
          toast.error(extractErrorMessage(err, t("dailyOps.toast.startVisitFailed")));
        },
      },
    );
  }, [startVisitMutation, openFutureOverrideIfAllowed, t]);

  const handleFutureOverrideConfirm = useCallback((reason: string) => {
    const appointmentId = pendingFutureOverride?.item.appointmentId;
    if (!pendingFutureOverride || !appointmentId) return;

    const { type, item, body } = pendingFutureOverride;
    const overrideBody = {
      ...body,
      overrideFutureAppointment: true,
      overrideReason: reason,
    };
    const callbacks = {
      onSuccess: () => {
        setFutureOverrideOpen(false);
        setPendingFutureOverride(null);
        toast.success(t("dailyOps.toast.overrideDone"));
      },
      onError: (error: unknown) =>
        toast.error(extractErrorMessage(error, t("dailyOps.toast.overrideFailed"))),
    };

    if (type === "Intake") {
      intakeMutation.mutate(
        { appointmentId, body: overrideBody },
        callbacks,
      );
    } else if (type === "SendToQueue") {
      sendToQueueMutation.mutate(
        { appointmentId, body: overrideBody },
        callbacks,
      );
    } else {
      startVisitMutation.mutate(
        { appointmentId, body: overrideBody },
        callbacks,
      );
    }
  }, [pendingFutureOverride, intakeMutation, sendToQueueMutation, startVisitMutation, t]);

  const handleQuickPayment = useCallback((item: TodayJourneyItem) => {
    if (!activeCashierSession) {
      toast.error(t("dailyOps.toast.shiftRequired"));
      return;
    }
    setSelectedItem(item);
    setPaymentModalOpen(true);
  }, [activeCashierSession, t]);

  const handleCompleteVisit = useCallback((item: TodayJourneyItem) => {
    setSelectedItem(item);
    setCompleteVisitModalOpen(true);
  }, []);

  const handleLeftWithoutCompletion = useCallback((item: TodayJourneyItem) => {
    if (!item.visitId) {
      toast.error(t("dailyOps.toast.lwcNoVisit"));
      return;
    }
    setSelectedItem(item);
    setLeftWithoutReason("");
    setLeftWithoutModalOpen(true);
  }, [t]);

  const handleCreateDraftInvoice = useCallback(async (item: TodayJourneyItem) => {
    if (!item.visitId) {
      toast.error(t("dailyOps.toast.invoiceNoVisit"));
      return;
    }

    try {
      const result = await createDraftInvoiceMutation.mutateAsync(item.visitId) as {
        isExisting?: boolean;
        IsExisting?: boolean;
      };
      toast.success(
        result?.isExisting || result?.IsExisting
          ? t("dailyOps.toast.invoiceExists")
          : t("dailyOps.toast.invoiceCreated")
      );
    } catch (err) {
      toast.error(extractErrorMessage(err, t("dailyOps.toast.invoiceFailed")));
    }
  }, [createDraftInvoiceMutation, t]);

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

  // Legacy routes and cross-module links use ?tab=<module>. Resolve only the
  // explicit allow-list so `/clinic-queue` and checkout shortcuts land on the
  // intended workspace without accepting arbitrary query values.
  useEffect(() => {
    const requestedModule = getDailyOperationsModuleTab(searchParams.get("tab"));
    if (requestedModule) setActiveModule(requestedModule);
  }, [searchParams]);

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
        if (!undoAction.appointmentId) { toast.error(t("dailyOps.toast.undoNoAppointment")); return; }
        await updateStatusMutation.mutateAsync({
          appointmentId: undoAction.appointmentId,
          status: undoAction.previousStatus,
        });
        toast.success(t("dailyOps.toast.undoDone"));
      } else if (undoAction.type === "CancelQueue" && undoAction.queueItemId) {
        if (!undoAction.appointmentId) { toast.error(t("dailyOps.toast.undoNoAppointment")); return; }
        await sendToQueueMutation.mutateAsync({ appointmentId: undoAction.appointmentId });
        toast.success(t("dailyOps.toast.undoQueueDone"));
      }
    } catch (err) {
      toast.error(extractErrorMessage(err, t("dailyOps.toast.undoFailed")));
    }
    setUndoAction(null);
  }, [undoAction, updateStatusMutation, sendToQueueMutation, t]);

  const handleConfirmAction = useCallback(async () => {
    if (!selectedItem) return;
    try {
      if (confirmDialogType === "NoShow") {
        if (!selectedItem.appointmentId) { toast.error(t("dailyOps.toast.noShowNoAppointment")); return; }
        const prevStatus = selectedItem.appointmentStatus;
        await updateStatusMutation.mutateAsync({ appointmentId: selectedItem.appointmentId, status: "NoShow" });
        toast.success(t("dailyOps.toast.noShowDone"));
        pushUndoAction({
          id: crypto.randomUUID(),
          type: "NoShow",
          appointmentId: selectedItem.appointmentId,
          previousStatus: prevStatus,
          patientName: selectedItem.patientName,
          timestamp: Date.now(),
        });
      } else if (confirmDialogType === "Cancel") {
        if (!selectedItem.appointmentId) { toast.error(t("dailyOps.toast.cancelNoAppointment")); return; }
        const prevStatus = selectedItem.appointmentStatus;
        await updateStatusMutation.mutateAsync({ appointmentId: selectedItem.appointmentId, status: "Cancelled" });
        toast.success(t("dailyOps.toast.cancelDone"));
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
        toast.success(t("dailyOps.toast.queueCancelDone"));
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
        toast.success(t("dailyOps.toast.visitClosed"));
      }
    } catch (err) {
      toast.error(extractErrorMessage(err, t("dailyOps.toast.actionFailedGeneric")));
    }
    setConfirmDialogOpen(false);
  }, [selectedItem, confirmDialogType, updateStatusMutation, cancelQueueMutation, completeVisitMutation, pushUndoAction, t]);

  const handlePaymentConfirm = useCallback(async (amount: number, method: string, desc: string, notes: string, referenceNumber?: string, currency?: string, accountCurrency?: string, exchangeRateToAccountCurrency?: number) => {
    if (!selectedItem) return;
    try {
      const result = await createPaymentMutation.mutateAsync({
        patientId: selectedItem.patientId,
        amount,
        paymentMethod: method,
        currency: currency ?? "YER",
        accountCurrency: accountCurrency ?? "YER",
        exchangeRateToAccountCurrency,
        exchangeRateSource: exchangeRateToAccountCurrency ? "manual" : undefined,
        serviceDescription: desc || undefined,
        doctorId: selectedItem.doctorId,
        notes: notes || undefined,
        referenceNumber: referenceNumber || undefined,
      });
      toast.success(t("dailyOps.toast.paymentDone"));
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
    } catch (err) {
      toast.error(extractErrorMessage(err, t("dailyOps.toast.paymentFailed")));
    }
  }, [selectedItem, createPaymentMutation, t]);

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
        toast.success(t("dailyOps.toast.handoffDone"));
        setCompleteVisitModalOpen(false);
        return;
      }

      if (selectedItem.checkoutStatus === "ReadyForCheckout" || selectedItem.nextAction === "Checkout") {
        // Checkout should only mark the appointment as completed.
        // Payment creation is handled separately via the payment flow,
        // not auto-created during checkout with a hardcoded method.
        if (!selectedItem.appointmentId) { toast.error(t("dailyOps.toast.checkoutNoAppointment")); return; }
        const validation = await validateFinancialClosureMutation.mutateAsync({
          patientId: selectedItem.patientId,
          visitId: selectedItem.visitId,
        });
        if (!validation.canClose) {
          toast.error(validation.reason || t("dailyOps.toast.checkoutBlocked"));
          return;
        }
        await checkoutMutation.mutateAsync({
          appointmentId: selectedItem.appointmentId,
          body: {
            notes: data.notes || undefined,
            nextAppointmentDate: data.needsFollowUp ? data.nextDate : undefined,
          },
        });
        toast.success(t("dailyOps.toast.checkoutDone"));
        setCompleteVisitModalOpen(false);
        return;
      }

      toast.error(t("dailyOps.toast.checkoutNoVisit"));
    } catch (err) {
      toast.error(extractErrorMessage(err, t("dailyOps.toast.visitCloseFailed")));
    }
  }, [selectedItem, handoffMutation, validateFinancialClosureMutation, checkoutMutation, t]);

  const handleCheckoutConfirm = useCallback(async (data: {
    paymentAmount: number; paymentMethod: string; notes: string;
    nextDate?: string; nextServiceId?: string;
  }) => {
    if (!selectedItem) return;
    if (!selectedItem.appointmentId) { toast.error(t("dailyOps.toast.checkoutNoAppointment")); return; }
    try {
      // Checkout only marks the appointment as completed.
      // Payment creation is handled separately via the payment flow,
      // not auto-created during checkout.
      const validation = await validateFinancialClosureMutation.mutateAsync({
        patientId: selectedItem.patientId,
        visitId: selectedItem.visitId,
      });
      if (!validation.canClose) {
        toast.error(validation.reason || t("dailyOps.toast.checkoutBlocked"));
        return;
      }
      await checkoutMutation.mutateAsync({
        appointmentId: selectedItem.appointmentId,
        body: {
          notes: data.notes || undefined,
          nextAppointmentDate: data.nextDate || undefined,
        },
      });
      toast.success(t("dailyOps.toast.visitClosedOk"));
      setCompleteVisitModalOpen(false);
    } catch (err) {
      toast.error(extractErrorMessage(err, t("dailyOps.toast.visitCloseFailed")));
    }
  }, [selectedItem, validateFinancialClosureMutation, checkoutMutation, t]);

  const handleLeftWithoutCompletionConfirm = useCallback(async () => {
    if (!selectedItem?.visitId) return;

    const reason = leftWithoutReason.trim();
    if (!reason) {
      toast.error(t("dailyOps.lwc.reasonRequired"));
      return;
    }

    try {
      await leftWithoutCompletionMutation.mutateAsync({
        visitId: selectedItem.visitId,
        reason,
        status: "LeftWithoutCompletion",
      });
      toast.success(t("dailyOps.toast.lwcDone"));
      setLeftWithoutModalOpen(false);
      setLeftWithoutReason("");
    } catch (err) {
      toast.error(extractErrorMessage(err, t("dailyOps.toast.lwcFailed")));
    }
  }, [selectedItem, leftWithoutReason, leftWithoutCompletionMutation, t]);

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
      toast.success(t("dailyOps.toast.bookingDone"));
      setBookAppointmentModalOpen(false);
    } catch (err) {
      toast.error(extractErrorMessage(err, t("dailyOps.toast.bookingFailed")));
    }
  }, [selectedItem, createAppointmentMutation, t]);

  const handleWalkInConfirm = useCallback(async (data: {
    patientName: string; patientPhone: string; doctorId: string;
    serviceId: string; branchId: string; notes: string;
  }) => {
    try {
      await walkInMutation.mutateAsync(data);
      toast.success(t("dailyOps.toast.walkInDone"));
      setWalkInModalOpen(false);
    } catch (err) {
      toast.error(extractErrorMessage(err, t("dailyOps.toast.walkInFailed")));
    }
  }, [walkInMutation, t]);

  const handleBulkSms = useCallback(async () => {
    try {
      const result = await bulkSmsMutation.mutateAsync({
        appointmentIds: tomorrowItems.map(i => i.appointmentId).filter((id): id is string => !!id),
      });
      toast.success(
        tf("dailyOps.toast.remindersSent", { sent: result.succeeded })
        + (result.failed > 0 ? tf("dailyOps.toast.remindersFailedSuffix", { failed: result.failed }) : ""),
      );
      setBulkSmsModalOpen(false);
    } catch (err) {
      toast.error(extractErrorMessage(err, t("dailyOps.toast.remindersFailed")));
    }
  }, [bulkSmsMutation, tomorrowItems, t, tf]);

  // ── Direct payment for unbooked patient ──
  const handleDirectPaymentConfirm = useCallback(async (data: {
    patientId: string; patientName: string;
    amount: number; paymentMethod: string;
    currency?: string; accountCurrency?: string; exchangeRateToAccountCurrency?: number;
    serviceDescription: string; notes: string;
    referenceNumber?: string;
  }) => {
    try {
      const result = await createPaymentMutation.mutateAsync({
        patientId: data.patientId,
        amount: data.amount,
        paymentMethod: data.paymentMethod,
        currency: data.currency ?? "YER",
        accountCurrency: data.accountCurrency ?? "YER",
        exchangeRateToAccountCurrency: data.exchangeRateToAccountCurrency,
        exchangeRateSource: data.exchangeRateToAccountCurrency ? "manual" : undefined,
        serviceDescription: data.serviceDescription || undefined,
        notes: data.notes || undefined,
        referenceNumber: data.referenceNumber || undefined,
      });
      toast.success(tf("dailyOps.toast.directPaymentDone", { amount: fmtRial(data.amount), patient: data.patientName }));
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
    } catch (err) {
      toast.error(extractErrorMessage(err, t("dailyOps.toast.paymentFailed")));
    }
  }, [createPaymentMutation, t, tf]);

  // ── Available status filters ──
  const statusFilters = [
    { value: "", label: t("dailyOps.status.all") },
    { value: "Scheduled", label: t("dailyOps.status.scheduled") },
    { value: "Confirmed", label: t("dailyOps.status.confirmed") },
    { value: "Arrived", label: t("dailyOps.status.arrived") },
    { value: "Waiting", label: t("dailyOps.status.waiting") },
    { value: "InRoom", label: t("dailyOps.status.inRoom") },
    { value: "InProgress", label: t("dailyOps.status.inProgress") },
    { value: "Completed", label: t("dailyOps.status.completed") },
    { value: "NoShow", label: t("dailyOps.status.noShow") },
    { value: "Cancelled", label: t("dailyOps.status.cancelled") },
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
              <div className="text-sm font-extrabold" style={{ color: NAVY }}>{t("dailyOps.title")}</div>
              <div className="text-[10px] font-medium" style={{ color: "#94a3b8" }}>{t("dailyOps.subtitle")}</div>
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
              placeholder={t("dailyOps.searchPlaceholder")}
              className="w-full text-sm rounded-full border-0 pe-16 ps-9 py-1.5 outline-none focus:ring-2 focus:ring-[#3d7ab5]/20"
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
              <option value="">👨‍⚕️ {t("dailyOps.filter.allDoctors")}</option>
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
              title={t("dailyOps.action.clinicDisplay")}>
              <Monitor className="w-3.5 h-3.5" />
              <span className="hidden md:inline">{t("dailyOps.action.clinicDisplay")}</span>
            </button>
          )}

          {/* مريض مشي (Walk-in) — Orange solid */}
          {canCreateWalkIn && (
          <button onClick={() => setWalkInModalOpen(true)}
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-bold text-white transition hover:opacity-90 flex-shrink-0"
            style={{ background: ORANGE }}
            title={t("dailyOps.action.walkInShortcut")}>
            <UserPlus className="w-3.5 h-3.5" />
            <span className="hidden md:inline">{t("dailyOps.action.walkIn")}</span>
          </button>
          )}

          {/* تحصيل/دفع (Collect Payment) — Green solid with cashier session indicator */}
          {canCollectPayment && (
            <button onClick={() => {
              if (!activeCashierSession) {
                toast.error(t("dailyOps.toast.shiftRequired"));
                return;
              }
              setDirectPaymentModalOpen(true);
            }}
              className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-bold text-white transition hover:opacity-90 flex-shrink-0 relative"
              style={{ background: activeCashierSession ? "#22c55e" : "#94a3b8" }}
              title={activeCashierSession ? t("dailyOps.action.collectPayment") : t("dailyOps.action.needsOpenShift")}>
              {/* Cashier session status dot */}
              <span className="absolute -top-0.5 -right-0.5 w-2.5 h-2.5 rounded-full border-2 border-white"
                style={{ background: activeCashierSession ? "#16a34a" : "#ef4444" }} />
              <CreditCard className="w-3.5 h-3.5" />
              <span className="hidden md:inline">{t("dailyOps.action.collectPayment")}</span>
            </button>
          )}

          {/* ═══ المزيد (More) dropdown ═══ */}
          <div ref={moreMenuRef} className="relative flex-shrink-0">
            <button onClick={() => setMoreMenuOpen(!moreMenuOpen)}
              className="w-9 h-9 rounded-lg flex items-center justify-center transition hover:bg-gray-100"
              title={t("dailyOps.action.more")}>
              <MoreHorizontal className="w-4 h-4" style={{ color: "#64748b" }} />
            </button>

            {moreMenuOpen && (
              <div className="absolute top-full end-0 mt-1 bg-white rounded-xl shadow-lg border py-1.5 min-w-[200px] z-50"
                style={{ borderColor: "#e5e7eb" }}>

                {/* تسجيل وصول (CheckIn) */}
                {canCheckIn && (
                  <button onClick={() => {
                    setMoreMenuOpen(false);
                    const item = getActiveItem();
                    if (!item) { toast.error(t("dailyOps.toast.selectPatient")); return; }
                    handleIntake(item);
                  }}
                    className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold transition hover:bg-green-50"
                    style={{ color: "#16a34a" }}>
                    <ClipboardCheck className="w-4 h-4" />
                    <span>{t("dailyOps.action.checkIn")}</span>
                  </button>
                )}

                {/* مريض جديد (New Patient) */}
                <button onClick={() => { setMoreMenuOpen(false); router.push("/patients/new"); }}
                  className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold transition hover:bg-[#1a3a5c08]"
                  style={{ color: NAVY }}>
                  <UserPlus className="w-4 h-4" />
                  <span>{t("dailyOps.action.newPatient")}</span>
                </button>

                {/* دخول مباشر (Walk-In) */}
                {canCreateWalkIn && (
                  <button onClick={() => { setMoreMenuOpen(false); setWalkInModalOpen(true); }}
                    className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold transition hover:bg-orange-50"
                    style={{ color: ORANGE }}>
                    <LogIn className="w-4 h-4" />
                    <span>{t("dailyOps.action.directEntry")}</span>
                  </button>
                )}

                {/* تحصيل دفعة (Collect Payment) */}
                {canCollectPayment && (
                  <button onClick={() => {
                    setMoreMenuOpen(false);
                    const item = getActiveItem();
                    if (!item) { toast.error(t("dailyOps.toast.selectPatient")); return; }
                    handleQuickPayment(item);
                  }}
                    className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold transition hover:bg-emerald-50"
                    style={{ color: "#059669" }}>
                    <CreditCard className="w-4 h-4" />
                    <span>{t("dailyOps.action.collect")}</span>
                  </button>
                )}

                {/* نداء المريض (Call Patient) */}
                {canCallPatient && (
                  <button onClick={() => {
                    setMoreMenuOpen(false);
                    const item = getActiveItem();
                    if (!item) { toast.error(t("dailyOps.toast.selectPatient")); return; }
                    handleCallPatient(item);
                  }}
                    className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold transition hover:bg-amber-50"
                    style={{ color: "#d97706" }}>
                    <Bell className="w-4 h-4" />
                    <span>{t("dailyOps.action.callPatient")}</span>
                  </button>
                )}

                {/* إعادة النداء (Recall) */}
                {canRecallPatient && (
                  <button onClick={() => {
                    setMoreMenuOpen(false);
                    const item = getActiveItem();
                    if (!item) { toast.error(t("dailyOps.toast.selectPatient")); return; }
                    setPendingCallItem(item);
                    setCallActionType("recall");
                    setRoomSelectOpen(true);
                  }}
                    className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold transition hover:bg-yellow-50"
                    style={{ color: "#ca8a04" }}>
                    <BellRing className="w-4 h-4" />
                    <span>{t("dailyOps.action.recall")}</span>
                  </button>
                )}

                {/* موعد قادم (Next Appointment) */}
                <button onClick={() => {
                  setMoreMenuOpen(false);
                  const item = getActiveItem();
                  if (!item) { toast.error(t("dailyOps.toast.selectPatient")); return; }
                  handleBookAppointment(item);
                }}
                  className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold transition hover:bg-blue-50"
                  style={{ color: BLUE }}>
                  <CalendarPlus className="w-4 h-4" />
                  <span>{t("dailyOps.action.nextAppointment")}</span>
                </button>

                {/* تحميل سند PDF (Download Receipt PDF) */}
                <button onClick={() => { setMoreMenuOpen(false); handleDownloadReceipt(); }}
                  className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold transition hover:bg-green-50"
                  style={{ color: "#16a34a" }}>
                  <Download className="w-4 h-4" />
                  <span>{t("dailyOps.action.downloadReceipt")}</span>
                </button>

                {/* طباعة سند مباشرة (Print Receipt — prints PDF only, not the system page) */}
                <button onClick={() => { setMoreMenuOpen(false); handlePrintReceipt(); }}
                  className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold transition hover:bg-purple-50"
                  style={{ color: "#7c3aed" }}>
                  <Printer className="w-4 h-4" />
                  <span>{t("dailyOps.action.printReceipt")}</span>
                </button>

                <div className="my-1 border-t" style={{ borderColor: "#f1f5f9" }} />

                {/* SMS Reminders */}
                {!isDoctor && (
                  <button onClick={() => { setMoreMenuOpen(false); refetchTomorrow(); setBulkSmsModalOpen(true); }}
                    className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold transition hover:bg-[#3d7ab508]"
                    style={{ color: BLUE }}>
                    <Megaphone className="w-4 h-4" />
                    <span>{t("dailyOps.action.sendReminders")}</span>
                  </button>
                )}

                {/* Print */}
                <button onClick={() => { setMoreMenuOpen(false); printScreen(); }}
                  className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold transition hover:bg-gray-50"
                  style={{ color: "#64748b" }}>
                  <Printer className="w-4 h-4" />
                  <span>{t("dailyOps.action.printDaySheet")}</span>
                </button>

                {/* Refresh */}
                <button onClick={() => { setMoreMenuOpen(false); refetchItems(); }}
                  className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold transition hover:bg-gray-50"
                  style={{ color: "#64748b" }}>
                  <RefreshCw className="w-4 h-4" />
                  <span>{t("dailyOps.action.refresh")}</span>
                </button>

                {/* Sound toggle */}
                <button onClick={() => { setMoreMenuOpen(false); setSoundEnabled(!soundEnabled); }}
                  className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold transition hover:bg-gray-50"
                  style={{ color: soundEnabled ? "#64748b" : "#94a3b8" }}>
                  {soundEnabled ? <Bell className="w-4 h-4" /> : <BellOff className="w-4 h-4" />}
                  <span>{t(soundEnabled ? "dailyOps.action.soundOff" : "dailyOps.action.soundOn")}</span>
                </button>

                {/* Shortcuts help */}
                <button onClick={() => { setMoreMenuOpen(false); setShortcutsHelpOpen(true); }}
                  className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold transition hover:bg-gray-50"
                  style={{ color: "#64748b" }}>
                  <Keyboard className="w-4 h-4" />
                  <span>{t("dailyOps.action.shortcuts")}</span>
                </button>
              </div>
            )}
          </div>

          {/* SignalR indicator */}
          <div className="flex items-center gap-1 w-6 h-6 rounded-full justify-center flex-shrink-0" title={t(signalrConnected ? "dailyOps.live.connected" : "dailyOps.live.disconnected")}>
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
                <span>{t(tab.label)}</span>
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

            {/* Honest failure state: without this, a 500 from patient-journey/today
                renders as "لا توجد مواعيد" and reception believes the day is empty. */}
            {itemsError && (
              <div className="flex-shrink-0 mx-3 mt-3 px-4 py-3 rounded-xl border flex items-center justify-between gap-3 flex-wrap"
                style={{ background: "#fef2f2", borderColor: "#fecaca" }}>
                <div className="flex items-center gap-2 text-xs font-bold" style={{ color: "#b91c1c" }}>
                  <AlertTriangle className="w-4 h-4 flex-shrink-0" />
                  {t("dailyOps.error.todayLoad")}
                </div>
                <button onClick={() => refetchItems()}
                  className="px-3 py-1.5 rounded-lg text-xs font-bold text-white flex-shrink-0 transition hover:opacity-90"
                  style={{ background: "#b91c1c" }}>
                  {t("dailyOps.error.retry")}
                </button>
              </div>
            )}

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
                        <span>{t(tab.label)}</span>
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
                    <div className="flex items-center gap-1.5 ms-auto px-2.5 py-1 rounded-lg text-xs font-bold"
                      style={{ background: "linear-gradient(135deg, #f0f7ff, #faf5ff)", border: "1px solid #e0e7ff", color: NAVY }}>
                      <Activity className="w-3 h-3" style={{ color: BLUE }} />
                      <span>{tf("dailyOps.next.patient", { name: nextPatient.patientName })}</span>
                      {nextPatient.nextAction === "CallPatient" && (
                        <button onClick={() => handleCallPatient(nextPatient)}
                          className="px-2 py-0.5 rounded text-[10px] font-bold text-white" style={{ background: ORANGE }}>
                          {t("dailyOps.next.call")}
                        </button>
                      )}
                      {nextPatient.nextAction === "EnterRoom" && (
                        <button onClick={() => handleEnterRoom(nextPatient)}
                          className="px-2 py-0.5 rounded text-[10px] font-bold text-white" style={{ background: "#9333ea" }}>
                          {t("dailyOps.next.enter")}
                        </button>
                      )}
                      {nextPatient.nextAction === "StartVisit" && (
                        <button onClick={() => handleStartVisit(nextPatient)}
                          className="px-2 py-0.5 rounded text-[10px] font-bold text-white" style={{ background: "#dc2626" }}>
                          {t("dailyOps.next.start")}
                        </button>
                      )}
                      {(nextPatient.nextAction === "Intake" || nextPatient.nextAction === "SendToQueue") && (
                        <button onClick={() => handleIntake(nextPatient)}
                          className="px-2 py-0.5 rounded text-[10px] font-bold text-white" style={{ background: "#16a34a" }}>
                          {t("dailyOps.next.checkIn")}
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
                    onStartVisit={handleStartVisit}
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
                      <h3 className="text-xs font-bold" style={{ color: NAVY }}>{t("dailyOps.checkout.heading")}</h3>
                      <p className="text-[10px] text-gray-400 font-medium">{t("dailyOps.checkout.subheading")}</p>
                    </div>
                  </div>
                  {tabCounts.payments > 0 && (
                    <span className="text-[11px] font-bold px-2 py-0.5 rounded-full bg-emerald-50 text-emerald-600 border border-emerald-200">
                      {tf("dailyOps.checkout.pendingCount", { count: tabCounts.payments })}
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
                    onStartVisit={handleStartVisit}
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
                <h3 className="font-extrabold text-[15px]" style={{ color: NAVY }}>{t("dailyOps.lwc.title")}</h3>
                <p className="text-[11px] text-gray-500 mt-0.5">{selectedItem?.patientName}</p>
              </div>
              <button type="button" onClick={() => setLeftWithoutModalOpen(false)} className="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-gray-100 transition">
                <X className="w-4 h-4 text-gray-400" />
              </button>
            </div>

            <div className="p-5 space-y-4">
              <div className="rounded-xl border border-red-100 bg-red-50 px-3 py-2 text-xs leading-6 text-red-700">
                {t("dailyOps.lwc.notice")}
              </div>
              <div>
                <label className="block text-xs font-bold mb-1.5" style={{ color: NAVY }}>{t("dailyOps.lwc.reasonLabel")}</label>
                <textarea
                  value={leftWithoutReason}
                  onChange={(event) => setLeftWithoutReason(event.target.value)}
                  rows={4}
                  className="w-full rounded-xl border border-gray-200 px-3 py-2 text-sm outline-none focus:border-[#3d7ab5] focus:ring-2 focus:ring-[#3d7ab5]/15"
                  placeholder={t("dailyOps.lwc.reasonPlaceholder")}
                />
              </div>
              <div className="flex justify-end gap-2">
                <button
                  type="button"
                  onClick={() => setLeftWithoutModalOpen(false)}
                  className="rounded-lg border border-gray-200 px-4 py-2 text-xs font-bold text-gray-600 hover:bg-gray-50"
                >
                  {t("dailyOps.lwc.back")}
                </button>
                <button
                  type="button"
                  disabled={leftWithoutCompletionMutation.isPending}
                  onClick={handleLeftWithoutCompletionConfirm}
                  className="rounded-lg px-4 py-2 text-xs font-bold text-white disabled:opacity-60"
                  style={{ background: "#dc2626" }}
                >
                  {t(leftWithoutCompletionMutation.isPending ? "dailyOps.lwc.saving" : "dailyOps.lwc.submit")}
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
            toast.success(t("dailyOps.toast.roomChanged"));
            setChangeRoomModalOpen(false);
          } catch (err) {
            toast.error(extractErrorMessage(err, t("dailyOps.toast.roomChangeFailed")));
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

      <FutureAppointmentOverrideDialog
        open={futureOverrideOpen}
        onClose={() => {
          if (intakeMutation.isPending || sendToQueueMutation.isPending || startVisitMutation.isPending) return;
          setFutureOverrideOpen(false);
          setPendingFutureOverride(null);
        }}
        patientName={pendingFutureOverride?.item.patientName ?? ""}
        appointmentDate={pendingFutureOverride?.item.appointmentDate ?? undefined}
        operation={pendingFutureOverride?.type ?? "Intake"}
        isPending={intakeMutation.isPending || sendToQueueMutation.isPending || startVisitMutation.isPending}
        onConfirm={handleFutureOverrideConfirm}
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
          // Doctor room assignment: pre-select the calling doctor's standing room
          defaultRoomName={doctors.find(d => d.id === pendingCallItem?.doctorId)?.defaultRoomName}
          onConfirm={handleRoomConfirm}
          onCancel={() => { setRoomSelectOpen(false); setPendingCallItem(null); }}
          loading={callPatientMutation.isPending || recallPatientMutation.isPending}
        />
      )}
    </>
  );
}


