"use client";

import { useState, useCallback, useMemo, useEffect, useRef } from "react";
import { useRouter } from "next/navigation";
import {
  Calendar, ClipboardList, CreditCard, Clock, CheckCircle,
  Stethoscope, AlertTriangle, Search, RefreshCw,
  Globe,
  Wallet, UserPlus, Keyboard, Bell, BellOff,
  Printer, Activity, Megaphone, Building2,
  X, Phone, MessageCircle, Monitor,
} from "lucide-react";
import { useAuthStore } from "@/stores/authStore";
import { toast } from "@/stores/toastStore";
import { useSignalRClinicQueue } from "@/hooks/useSignalRClinicQueue";
import api from "@/lib/api";

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
  useEnterRoom,
  useUpdateAppointmentStatus,
  useCreatePayment,
  useCreateDraftInvoice,
  useCheckout,
  useHandoff,
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
import AppointmentsView from "./_modules/AppointmentsView";
import RoomsView from "./_modules/RoomsView";
import FinanceView from "./_modules/FinanceView";
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
type ModuleTab = "appointments" | "queue" | "rooms" | "checkout" | "booking" | "lab" | "report";

const MODULE_TABS: { key: ModuleTab; label: string; icon: React.ElementType; color: string }[] = [
  { key: "appointments", label: "وصول اليوم",         icon: Calendar,      color: BLUE },
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
   MAIN PAGE — Microsoft Fluent Design
   ═══════════════════════════════════════════════════════════════════════════ */
export default function DailyOperationsPage() {
  const router = useRouter();
  const { user } = useAuthStore();
  const userRole = user?.role ?? "";
  const isDoctor = isDoctorRole(userRole);
  const canProcessCheckout = userRole === "Admin" || userRole === "Reception";

  // ── SignalR real-time updates ──
  const { isConnected: signalrConnected } = useSignalRClinicQueue();

  // ── Filters ──
  const [filterDate, setFilterDate] = useState(getTodayStr());
  const [filterDoctor, setFilterDoctor] = useState("");
  const [filterStatus, setFilterStatus] = useState("");
  const [searchQuery, setSearchQuery] = useState("");
  const [activeTab, setActiveTab] = useState<TabKey>("appointments");
  const [activeModule, setActiveModule] = useState<ModuleTab>("appointments");

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
  const enterRoomMutation = useEnterRoom();
  const updateStatusMutation = useUpdateAppointmentStatus();
  const createPaymentMutation = useCreatePayment();
  const createDraftInvoiceMutation = useCreateDraftInvoice();
  const checkoutMutation = useCheckout();
  const handoffMutation = useHandoff();
  const cancelQueueMutation = useCancelQueue();
  const changeRoomMutation = useChangeRoom();
  const createAppointmentMutation = useCreateAppointment();
  const completeVisitMutation = useCompleteVisit();
  const walkInMutation = useWalkInPatient();

  // ── Modal state ──
  const [selectedItem, setSelectedItem] = useState<TodayJourneyItem | null>(null);
  const [paymentModalOpen, setPaymentModalOpen] = useState(false);
  const [completeVisitModalOpen, setCompleteVisitModalOpen] = useState(false);
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

  // ── Computed ──
  const dayStats = useMemo(() => computeDayStats(items, financeSummary), [items, financeSummary]);

  // ── Room occupancy ──
  const roomOccupancy = useMemo(() => computeRoomOccupancy(rooms, items), [rooms, items]);

  // ── Next patient to call ──
  const nextPatient = useMemo(() => getNextPatient(items), [items]);

  const filteredItems = useMemo(() => {
    let result = filterByTab(items, activeTab);
    if (searchQuery.trim()) {
      const q = searchQuery.trim().toLowerCase();
      result = result.filter(i =>
        i.patientName.toLowerCase().includes(q) ||
        (i.patientPhone && i.patientPhone.includes(q)) ||
        i.doctorName.toLowerCase().includes(q) ||
        (i.serviceName && i.serviceName.toLowerCase().includes(q))
      );
    }
    return result;
  }, [items, activeTab, searchQuery]);

  // ── Tab counts ──
  const tabCounts = useMemo(() => ({
    appointments: items.length,
    queue: items.filter(i => i.queueStatus === "Waiting" || i.queueStatus === "Called" || (i.appointmentStatus === "Waiting" && !i.queueStatus)).length,
    inClinic: items.filter(i => i.appointmentStatus === "InRoom" || i.appointmentStatus === "InProgress" || i.queueStatus === "InRoom" || i.queueStatus === "InProgress").length,
    completed: items.filter(i => i.appointmentStatus === "Completed").length,
    payments: items.filter(i => i.checkoutStatus === "ReadyForCheckout" || i.nextAction === "Checkout" || i.appointmentStatus === "Completed").length,
    overdue: items.filter(i => i.appointmentStatus === "NoShow" || i.appointmentStatus === "Cancelled").length,
  }), [items]);

  // ── Search input ref for keyboard shortcut ──
  const searchInputRef = useRef<HTMLInputElement>(null);

  // ── Keyboard shortcuts ──
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      const target = e.target as HTMLElement;
      const isInput = target.tagName === "INPUT" || target.tagName === "TEXTAREA" || target.tagName === "SELECT";

      if (e.key === "Escape") {
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
    walkInModalOpen, sidePanelOpen, shortcutsHelpOpen, ctxMenu, refetchItems,
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
        intakeMutation.mutate(
          { appointmentId: item.appointmentId, body: notesSuffix ? { notes: notesSuffix } : {} },
          { onSuccess: () => toast.success("تم تسجيل وصول المريض"), onError: () => toast.error("فشل تسجيل الوصول") }
        );
      } else {
        sendToQueueMutation.mutate(
          { appointmentId: item.appointmentId, body: notesSuffix ? { notes: notesSuffix } : {} },
          { onSuccess: () => toast.success("تمت إضافة المريض للانتظار"), onError: () => toast.error("فشل إضافة المريض للانتظار") }
        );
      }
    } catch (error) {
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
    callPatientMutation.mutate(
      item.queueItemId,
      { onSuccess: () => toast.success("تم نداء المريض"), onError: () => toast.error("فشل نداء المريض") },
    );
  }, [callPatientMutation]);

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

  const pushUndoAction = useCallback((action: UndoAction) => {
    if (undoAction) setUndoAction(null);
    setUndoAction(action);
  }, [undoAction]);

  const handleUndo = useCallback(async () => {
    if (!undoAction) return;
    try {
      if (undoAction.type === "NoShow" || undoAction.type === "Cancel") {
        await updateStatusMutation.mutateAsync({
          appointmentId: undoAction.appointmentId,
          status: undoAction.previousStatus,
        });
        toast.success("تم التراجع عن الإجراء");
      } else if (undoAction.type === "CancelQueue" && undoAction.queueItemId) {
        await sendToQueueMutation.mutateAsync({ appointmentId: undoAction.appointmentId });
        toast.success("تم التراجع — أعيد المريض للطابور");
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
        toast.success("تم إلغاء المريض من الطابور");
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

  const handlePaymentConfirm = useCallback(async (amount: number, method: string, desc: string, notes: string) => {
    if (!selectedItem) return;
    try {
      const result = await createPaymentMutation.mutateAsync({
        patientId: selectedItem.patientId,
        amount,
        paymentMethod: method,
        serviceDescription: desc || undefined,
        doctorId: selectedItem.doctorId,
        notes: notes || undefined,
      });
      toast.success("تم تسجيل الدفعة بنجاح");
      setPaymentModalOpen(false);

      if (result?.id) {
        try {
          const { data } = await import("@/lib/api").then(m => m.default.get(`/api/payments/${result.id}/pdf`, {
            responseType: "blob",
          }));
          const url = window.URL.createObjectURL(new Blob([data], { type: "application/pdf" }));
          const link = document.createElement("a");
          link.href = url;
          link.download = `receipt-${result.receiptNumber || result.id}.pdf`;
          link.click();
          window.URL.revokeObjectURL(url);
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
      toast.success("تم تسجيل المريض المشي وإضافته للطابور");
      setWalkInModalOpen(false);
    } catch {
      toast.error("فشل تسجيل المريض المشي");
    }
  }, [walkInMutation]);

  const handleBulkSms = useCallback(async () => {
    try {
      const result = await bulkSmsMutation.mutateAsync({
        appointmentIds: tomorrowItems.map(i => i.appointmentId),
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
  }) => {
    try {
      const result = await createPaymentMutation.mutateAsync({
        patientId: data.patientId,
        amount: data.amount,
        paymentMethod: data.paymentMethod,
        serviceDescription: data.serviceDescription || undefined,
        notes: data.notes || undefined,
      });
      toast.success(`تم تسجيل دفعة ${fmtRial(data.amount)} للمريض ${data.patientName} بنجاح`);
      setDirectPaymentModalOpen(false);

      // Try to download PDF receipt
      if (result?.id) {
        try {
          const { data: pdfData } = await import("@/lib/api").then(m => m.default.get(`/api/payments/${result.id}/pdf`, {
            responseType: "blob",
          }));
          const url = window.URL.createObjectURL(new Blob([pdfData], { type: "application/pdf" }));
          const link = document.createElement("a");
          link.href = url;
          link.download = `receipt-${result.receiptNumber || result.id}.pdf`;
          link.click();
          window.URL.revokeObjectURL(url);
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
    { value: "Cancelled", label: "ملغى" },
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
            COMMAND BAR (52px)
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
          <div className="flex-1 max-w-md relative">
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
          <div className="flex items-center gap-1 px-2 py-1 rounded-lg text-xs font-semibold"
            style={{ background: "#f5f7fa", color: NAVY }}>
            <Calendar className="w-3.5 h-3.5" />
            <input type="date" value={filterDate} onChange={e => setFilterDate(e.target.value)}
              className="bg-transparent text-xs font-semibold outline-none border-0 w-[110px]"
              style={{ color: NAVY }} />
          </div>

          {/* Doctor filter */}
          {!isDoctor && (
            <select value={filterDoctor} onChange={e => setFilterDoctor(e.target.value)}
              className="text-xs font-semibold rounded-lg px-2 py-1.5 outline-none border-0 focus:ring-2 focus:ring-[#3d7ab5]/20"
              style={{ background: "#f5f7fa", color: NAVY, maxWidth: 120 }}>
              <option value="">👨‍⚕️ كل الأطباء</option>
              {doctors.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          )}

          {/* Status filter */}
          <select value={filterStatus} onChange={e => setFilterStatus(e.target.value)}
            className="text-xs font-semibold rounded-lg px-2 py-1.5 outline-none border-0 focus:ring-2 focus:ring-[#3d7ab5]/20"
            style={{ background: "#f5f7fa", color: NAVY, maxWidth: 110 }}>
            {statusFilters.map(s => <option key={s.value} value={s.value}>📊 {s.label}</option>)}
          </select>

          {/* Spacer */}
          <div className="flex-1" />

          {/* Right Actions */}
          {/* Walk-in (PRIMARY - Orange with text) */}
          <button onClick={() => setWalkInModalOpen(true)}
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-bold text-white transition hover:opacity-90"
            style={{ background: ORANGE }}
            title="مريض مشي (Ctrl+N)">
            <UserPlus className="w-3.5 h-3.5" />
            <span className="hidden sm:inline">مريض مشي</span>
          </button>

          {/* Direct Payment for unbooked patient (Green with text) */}
          {!isDoctor && (
            <button onClick={() => {
              if (!activeCashierSession) {
                toast.error("يجب فتح وردية (صندوق الكاشير) أولاً قبل تسجيل أي مدفوعات. اذهب إلى المالية > الصندوق لفتح وردية.");
                return;
              }
              setDirectPaymentModalOpen(true);
            }}
              className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-bold text-white transition hover:opacity-90"
              style={{ background: activeCashierSession ? "#22c55e" : "#94a3b8" }}
              title={activeCashierSession ? "دفع لمريض بدون موعد" : "يجب فتح وردية أولاً"}>
              <CreditCard className="w-3.5 h-3.5" />
              <span className="hidden sm:inline">دفع لمريض</span>
            </button>
          )}

          {/* SMS Reminders (blue outline) */}
          {!isDoctor && (
            <button onClick={() => { refetchTomorrow(); setBulkSmsModalOpen(true); }}
              className="w-9 h-9 rounded-lg flex items-center justify-center transition hover:bg-[#3d7ab508]"
              style={{ border: "1px solid #3d7ab530" }}
              title="إرسال تذكيرات لمواعيد الغد">
              <Megaphone className="w-4 h-4" style={{ color: BLUE }} />
            </button>
          )}

          {/* Print */}
          <button onClick={() => window.print()}
            className="w-9 h-9 rounded-lg flex items-center justify-center transition hover:bg-gray-100"
            title="طباعة جدول اليوم">
            <Printer className="w-4 h-4" style={{ color: "#64748b" }} />
          </button>

          {/* Refresh */}
          <button onClick={() => refetchItems()}
            className="w-9 h-9 rounded-lg flex items-center justify-center transition hover:bg-gray-100"
            title="تحديث (Ctrl+R)">
            <RefreshCw className="w-4 h-4" style={{ color: "#64748b" }} />
          </button>

          {/* Sound toggle */}
          <button onClick={() => setSoundEnabled(!soundEnabled)}
            className="w-9 h-9 rounded-lg flex items-center justify-center transition hover:bg-gray-100"
            title={soundEnabled ? "إيقاف صوت التنبيه" : "تشغيل صوت التنبيه"}>
            {soundEnabled ? <Bell className="w-4 h-4" style={{ color: "#64748b" }} /> : <BellOff className="w-4 h-4" style={{ color: "#94a3b8" }} />}
          </button>

          {/* Shortcuts help */}
          <button onClick={() => setShortcutsHelpOpen(true)}
            className="w-9 h-9 rounded-lg flex items-center justify-center transition hover:bg-gray-100"
            title="اختصارات لوحة المفاتيح">
            <Keyboard className="w-4 h-4" style={{ color: "#64748b" }} />
          </button>

          {/* SignalR indicator */}
          <div className="flex items-center gap-1 w-9 h-9 rounded-lg justify-center" title={signalrConnected ? "متصل مباشر" : "غير متصل"}>
            <span className="w-2.5 h-2.5 rounded-full" style={{ background: signalrConnected ? "#16a34a" : "#ef4444" }} />
          </div>
        </div>

        {/* ═══════════ Module Tabs Row (42px) — Microsoft Fluent Tabs ═══════════ */}
        <div className="h-[42px] flex-shrink-0 bg-white flex items-center px-3 gap-0.5"
          style={{ borderBottom: "2px solid #f1f5f9" }}>
          {MODULE_TABS.map(tab => {
            const isActive = activeModule === tab.key;
            const TabIcon = tab.icon;
            return (
              <button key={tab.key}
                onClick={() => {
                  setActiveModule(tab.key);
                }}
                className="flex items-center gap-1.5 px-3 py-1.5 rounded-t-lg text-xs font-bold relative transition-all"
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
                    items={items.filter(i => i.checkoutStatus === "ReadyForCheckout" || i.nextAction === "Checkout" || i.appointmentStatus === "Completed")}
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
            STATUS BAR (32px)
            ═══════════════════════════════════════════════════════════════════ */}
        <div className="h-8 flex-shrink-0 flex items-center px-3 gap-4 text-[11px] font-medium text-white select-none"
          style={{ background: NAVY }}>
          {/* SignalR */}
          <div className="flex items-center gap-1.5">
            <span className="w-2 h-2 rounded-full" style={{ background: signalrConnected ? "#4ade80" : "#f87171" }} />
            <span>{signalrConnected ? "مباشر" : "غير متصل"}</span>
          </div>

          <div className="w-px h-4" style={{ background: "rgba(255,255,255,0.2)" }} />

          {/* Stats */}
          <span>{dayStats.totalAppointments} موعد</span>
          <span>{dayStats.waiting} انتظار</span>
          <span>{dayStats.inClinic} عيادة</span>
          <span>{dayStats.completed} مكتمل</span>
          {dayStats.noShow > 0 && <span>{dayStats.noShow} لم يحضر</span>}

          <div className="w-px h-4" style={{ background: "rgba(255,255,255,0.2)" }} />

          {/* Finance */}
          <span>💰 {fmtRial(dayStats.todayPayments)}</span>
          {dayStats.overdueAmount > 0 && <span>⚠ {fmtRial(dayStats.overdueAmount)} متأخر</span>}

          <div className="w-px h-4" style={{ background: "rgba(255,255,255,0.2)" }} />

          {/* Rooms */}
          {roomOccupancy.length > 0 && (
            <div className="flex items-center gap-2">
              <span>🏥</span>
              {roomOccupancy.slice(0, 4).map(room => (
                <span key={room.roomId} className="px-1.5 py-0.5 rounded text-[10px]"
                  style={{ background: room.isOccupied ? "rgba(147,51,234,0.3)" : "rgba(34,197,94,0.3)" }}>
                  {room.roomName}:{room.isOccupied ? room.patientName : "فارغة"}
                </span>
              ))}
            </div>
          )}

          <div className="flex-1" />

          {/* Date + Time */}
          <span>{fmtDate(filterDate)}</span>
          <span>{new Date().toLocaleTimeString("ar-YE", { hour: "2-digit", minute: "2-digit" })}</span>

          <div className="w-px h-4" style={{ background: "rgba(255,255,255,0.2)" }} />

          <span>⌨ ?</span>
        </div>
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

      <BookAppointmentModal
        open={bookAppointmentModalOpen}
        onClose={() => setBookAppointmentModalOpen(false)}
        item={selectedItem}
        doctors={doctors}
        services={services}
        isPending={createAppointmentMutation.isPending}
        onConfirm={handleBookConfirm}
      />

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
        onBookAppointment={handleBookAppointment}
        onWhatsApp={handleWhatsApp}
        onNoShow={handleNoShow}
        onCancel={handleCancel}
        onViewPatient={handleViewPatient}
        onOpenSidePanel={handleOpenSidePanel}
      />
    </>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Patient Detail Panel — Inline component for the right panel
   ═══════════════════════════════════════════════════════════════════════════ */
function PatientDetailPanel({
  item, summary, waitTime, onClose,
  onQuickPayment, onBookAppointment, onWhatsApp, onViewPatient,
  medicalAlerts, finance, activeContract, activeOrtho,
}: {
  item: TodayJourneyItem;
  summary: DailyJourneySummary | null;
  waitTime?: { estimatedMinutes: number; patientsAhead: number } | null;
  onClose: () => void;
  onQuickPayment: (item: TodayJourneyItem) => void;
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
            <div className="text-[11px] font-bold mb-1.5" style={{ color: NAVY }}>💰 الملخص المالي</div>
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
