"use client";

import { useState, useCallback, useMemo, useEffect, useRef } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  Calendar, ClipboardList, CreditCard, Clock, CheckCircle,
  Stethoscope, AlertTriangle, Search, RefreshCw, ArrowLeft,
  UserCheck, Globe, Plus, CalendarClock, Route,
  Wallet, UserPlus, Keyboard, Bell, BellOff,
  Printer, Users, Activity, ArrowRight, Megaphone, Building2,
} from "lucide-react";
import { useAuthStore } from "@/stores/authStore";
import { hasPermission, PERMISSION_KEYS } from "@/hooks/usePermissions";
import { toast } from "@/stores/toastStore";
import { WorkflowNav, WORKFLOW_LINKS } from "@/components/shared/WorkflowNav";
import { useSignalRClinicQueue } from "@/hooks/useSignalRClinicQueue";

import {
  NAVY, BLUE, ORANGE,
  TABS,
  fmtDate, getTodayStr, fmtRial, fmtTime,
  computeDayStats, filterByTab,
  computeRoomOccupancy, computeDoctorWorkload, getNextPatient,
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
  useEnterRoom,
  useUpdateAppointmentStatus,
  useCreatePayment,
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
} from "./_lib/hooks";

import AppointmentsTable from "./_components/AppointmentsTable";
import {
  QuickPaymentModal,
  CompleteVisitModal,
  BookAppointmentModal,
  ConfirmDialog,
  WhatsAppMenu,
  ChangeRoomModal,
  WalkInModal,
  UndoToast,
  PatientSidePanel,
  KeyboardShortcutsHelp,
  BulkSmsModal,
} from "./_components/Modals";

/* ═══════════════════════════════════════════════════════════════════════════
   Quick Link items for sidebar-style navigation at bottom
   ═══════════════════════════════════════════════════════════════════════════ */
const QUICK_LINKS = [
  { href: "/booking-requests",    label: "طلبات الحجز",   icon: Globe,         color: BLUE,   perm: PERMISSION_KEYS.BOOKING_REQUESTS_VIEW },
  { href: "/appointments/new",    label: "موعد جديد",     icon: CalendarClock, color: "#a855f7", perm: PERMISSION_KEYS.APPOINTMENTS_CREATE },
  { href: "/clinic-queue",        label: "الطابور",       icon: ClipboardList, color: ORANGE, perm: PERMISSION_KEYS.CLINIC_QUEUE_VIEW },
  { href: "/patient-journey",     label: "رحلة المريض",   icon: Route,         color: "#22c55e", perm: PERMISSION_KEYS.PATIENT_JOURNEY_VIEW },
  { href: "/finance/payments",    label: "المدفوعات",     icon: CreditCard,    color: "#22c55e", perm: PERMISSION_KEYS.PAYMENTS_VIEW },
  { href: "/patients/new",        label: "مريض جديد",     icon: Plus,          color: BLUE,   perm: PERMISSION_KEYS.PATIENTS_CREATE },
];

/* ═══════════════════════════════════════════════════════════════════════════
   Summary Card
   ═══════════════════════════════════════════════════════════════════════════ */
function SummaryCard({
  icon: Icon, label, value, color, subValue,
}: {
  icon: React.ElementType; label: string; value: number | string;
  color: string; subValue?: string;
}) {
  return (
    <div className="rounded-xl p-3.5 flex items-center gap-3 transition-shadow hover:shadow-md"
      style={{ background: color + "08", border: `1.5px solid ${color}20` }}>
      <div className="w-10 h-10 rounded-lg flex items-center justify-center flex-shrink-0"
        style={{ background: color + "15" }}>
        <Icon className="w-5 h-5" style={{ color }} />
      </div>
      <div>
        <div className="text-xl font-extrabold leading-tight" style={{ color }}>{value}</div>
        <div className="text-[11px] font-medium" style={{ color: "#64748b" }}>{label}</div>
        {subValue && <div className="text-[10px] font-semibold mt-0.5" style={{ color: "#94a3b8" }}>{subValue}</div>}
      </div>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   MAIN PAGE
   ═══════════════════════════════════════════════════════════════════════════ */
export default function DailyOperationsPage() {
  const router = useRouter();
  const { user } = useAuthStore();
  const userRole = user?.role ?? "";
  const isDoctor = isDoctorRole(userRole);

  // ── Improvement 1: SignalR real-time updates ──
  const { isConnected: signalrConnected } = useSignalRClinicQueue();

  // ── Filters ──
  const [filterDate, setFilterDate] = useState(getTodayStr());
  const [filterDoctor, setFilterDoctor] = useState("");
  const [filterBranch, setFilterBranch] = useState("");
  const [filterStatus, setFilterStatus] = useState("");
  const [searchQuery, setSearchQuery] = useState("");
  const [activeTab, setActiveTab] = useState<TabKey>("appointments");

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

  // ── Improvement 2: Queue wait time ──
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
  const [confirmDialogOpen, setConfirmDialogOpen] = useState(false);
  const [confirmDialogType, setConfirmDialogType] = useState<"Cancel" | "NoShow" | "CancelQueue" | "ChangeRoom" | "Complete">("Cancel");
  const [whatsAppMenuOpen, setWhatsAppMenuOpen] = useState(false);
  const [changeRoomModalOpen, setChangeRoomModalOpen] = useState(false);

  // ── Improvement 4: Walk-in modal ──
  const [walkInModalOpen, setWalkInModalOpen] = useState(false);

  // ── Improvement 5: Undo state ──
  const [undoAction, setUndoAction] = useState<UndoAction | null>(null);

  // ── Improvement 6: Side panel ──
  const [sidePanelOpen, setSidePanelOpen] = useState(false);
  const [sidePanelItem, setSidePanelItem] = useState<TodayJourneyItem | null>(null);

  // ── Improvement 7: Keyboard shortcuts help ──
  const [shortcutsHelpOpen, setShortcutsHelpOpen] = useState(false);

  // ── Bulk SMS modal ──
  const [bulkSmsModalOpen, setBulkSmsModalOpen] = useState(false);

  // ── Sound toggle ──
  const [soundEnabled, setSoundEnabled] = useState(true);

  // ── Selected patient summary (for side panel + modals) ──
  const activePatientId = sidePanelItem?.patientId ?? selectedItem?.patientId ?? null;
  const { data: selectedSummary } = usePatientSummary(activePatientId);

  // ── Computed ──
  const dayStats = useMemo(() => computeDayStats(items, financeSummary), [items, financeSummary]);

  // ── Room occupancy ──
  const roomOccupancy = useMemo(() => computeRoomOccupancy(rooms, items), [rooms, items]);

  // ── Doctor workload ──
  const doctorWorkload = useMemo(() => computeDoctorWorkload(items), [items]);

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

  // ── Improvement 7: Keyboard shortcuts ──
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // Ignore when typing in inputs
      const target = e.target as HTMLElement;
      const isInput = target.tagName === "INPUT" || target.tagName === "TEXTAREA" || target.tagName === "SELECT";

      // Escape: close any open modal
      if (e.key === "Escape") {
        if (paymentModalOpen) { setPaymentModalOpen(false); return; }
        if (completeVisitModalOpen) { setCompleteVisitModalOpen(false); return; }
        if (bookAppointmentModalOpen) { setBookAppointmentModalOpen(false); return; }
        if (confirmDialogOpen) { setConfirmDialogOpen(false); return; }
        if (whatsAppMenuOpen) { setWhatsAppMenuOpen(false); return; }
        if (changeRoomModalOpen) { setChangeRoomModalOpen(false); return; }
        if (walkInModalOpen) { setWalkInModalOpen(false); return; }
        if (sidePanelOpen) { setSidePanelOpen(false); return; }
        if (shortcutsHelpOpen) { setShortcutsHelpOpen(false); return; }
      }

      // Ctrl+R: refresh (prevent browser default)
      if (e.ctrlKey && e.key === "r") {
        e.preventDefault();
        refetchItems();
        return;
      }

      // Ctrl+F: focus search
      if (e.ctrlKey && e.key === "f") {
        e.preventDefault();
        searchInputRef.current?.focus();
        return;
      }

      // Ctrl+N: walk-in patient
      if (e.ctrlKey && e.key === "n") {
        e.preventDefault();
        setWalkInModalOpen(true);
        return;
      }

      // Number keys 1-6: switch tabs (only when not in input)
      if (!isInput && !e.ctrlKey && !e.altKey && !e.metaKey) {
        const tabKeys: TabKey[] = ["appointments", "queue", "inClinic", "completed", "payments", "overdue"];
        const num = parseInt(e.key, 10);
        if (num >= 1 && num <= 6) {
          setActiveTab(tabKeys[num - 1]);
          return;
        }
        // ? key: show shortcuts help
        if (e.key === "?") {
          setShortcutsHelpOpen(true);
          return;
        }
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [
    paymentModalOpen, completeVisitModalOpen, bookAppointmentModalOpen,
    confirmDialogOpen, whatsAppMenuOpen, changeRoomModalOpen,
    walkInModalOpen, sidePanelOpen, shortcutsHelpOpen, refetchItems,
  ]);

  // ── Action handlers ──
  const handleIntake = useCallback((item: TodayJourneyItem) => {
    intakeMutation.mutate(
      { appointmentId: item.appointmentId, body: {} },
      { onSuccess: () => toast.success("تم تسجيل وصول المريض"), onError: () => toast.error("فشل تسجيل الوصول") },
    );
  }, [intakeMutation]);

  const handleSendToQueue = useCallback((item: TodayJourneyItem) => {
    sendToQueueMutation.mutate(
      { appointmentId: item.appointmentId },
      { onSuccess: () => toast.success("تمت إضافة المريض للطابور"), onError: () => toast.error("فشل الإضافة للطابور") },
    );
  }, [sendToQueueMutation]);

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
    setSelectedItem(item);
    setPaymentModalOpen(true);
  }, []);

  const handleCompleteVisit = useCallback((item: TodayJourneyItem) => {
    setSelectedItem(item);
    setCompleteVisitModalOpen(true);
  }, []);

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

  // ── Improvement 6: Side panel trigger ──
  const handleOpenSidePanel = useCallback((item: TodayJourneyItem) => {
    setSidePanelItem(item);
    setSidePanelOpen(true);
  }, []);

  // ── Improvement 5: Undo helper ──
  const pushUndoAction = useCallback((action: UndoAction) => {
    // Clear any previous undo
    if (undoAction) setUndoAction(null);
    setUndoAction(action);
  }, [undoAction]);

  const handleUndo = useCallback(async () => {
    if (!undoAction) return;
    try {
      // Revert the action by restoring previous status
      if (undoAction.type === "NoShow" || undoAction.type === "Cancel") {
        await updateStatusMutation.mutateAsync({
          appointmentId: undoAction.appointmentId,
          status: undoAction.previousStatus,
        });
        toast.success("تم التراجع عن الإجراء");
      } else if (undoAction.type === "CancelQueue" && undoAction.queueItemId) {
        // Re-add to queue by sending to queue again
        await sendToQueueMutation.mutateAsync({ appointmentId: undoAction.appointmentId });
        toast.success("تم التراجع — أعيد المريض للطابور");
      }
    } catch {
      toast.error("فشل التراجع");
    }
    setUndoAction(null);
  }, [undoAction, updateStatusMutation, sendToQueueMutation]);

  // ── Confirm action (with Undo) ──
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

  // ── Payment confirm (with PDF receipt) ──
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

      // Auto-download PDF receipt
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
    } catch {
      toast.error("فشل تسجيل الدفعة");
    }
  }, [selectedItem, createPaymentMutation]);

  // ── Complete visit confirm ──
  const handleCompleteVisitConfirm = useCallback(async (data: {
    serviceDesc: string; amountDue: number; isPaid: boolean;
    needsFollowUp: boolean; nextDate: string; notes: string;
    diagnosis: string; instructions: string;
  }) => {
    if (!selectedItem) return;
    try {
      if (selectedItem.visitId) {
        await handoffMutation.mutateAsync({
          visitId: selectedItem.visitId,
          body: {
            treatmentDone: data.serviceDesc || undefined,
            diagnosis: data.diagnosis || undefined,
            instructions: data.instructions || undefined,
            amountDue: data.amountDue || undefined,
            notes: data.notes || undefined,
            followUpDate: data.needsFollowUp ? data.nextDate : undefined,
          },
        });
      }
      await checkoutMutation.mutateAsync({
        appointmentId: selectedItem.appointmentId,
        body: {
          paymentAmount: data.isPaid ? data.amountDue : 0,
          paymentMethod: "Cash",
          notes: data.notes || undefined,
          nextAppointmentDate: data.needsFollowUp ? data.nextDate : undefined,
        },
      });
      toast.success("تم إنهاء الزيارة بنجاح");
      setCompleteVisitModalOpen(false);
    } catch {
      toast.error("فشل إنهاء الزيارة");
    }
  }, [selectedItem, handoffMutation, checkoutMutation]);

  // ── Checkout shortcut ──
  const handleCheckoutConfirm = useCallback(async (data: {
    paymentAmount: number; paymentMethod: string; notes: string;
    nextDate?: string; nextServiceId?: string;
  }) => {
    if (!selectedItem) return;
    try {
      await checkoutMutation.mutateAsync({
        appointmentId: selectedItem.appointmentId,
        body: data,
      });
      toast.success("تم إنهاء الزيارة بنجاح");
      setCompleteVisitModalOpen(false);
    } catch {
      toast.error("فشل إنهاء الزيارة");
    }
  }, [selectedItem, checkoutMutation]);

  // ── Book appointment confirm ──
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

  // ── Walk-in confirm ──
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

  // ── Bulk SMS reminders ──
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

  // ── Visible quick links ──
  const visibleQuickLinks = useMemo(() =>
    QUICK_LINKS.filter(l => hasPermission(user, l.perm)),
  [user]);

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

  // ── Render ──
  return (
    <div className="space-y-4 page-content">
      {/* ═══ Header ═══ */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div>
          <h1 className="text-2xl font-extrabold" style={{ color: NAVY }}>التشغيل اليومي</h1>
          <p className="text-sm mt-1" style={{ color: "#64748b" }}>
            رحلة الاستقبال اليومية — من الموعد حتى الدفع والمتابعة
          </p>
        </div>
        <div className="flex items-center gap-2">
          {/* Last updated */}
          <span className="text-[10px] font-medium" style={{ color: "#94a3b8" }}>
            آخر تحديث: {new Date().toLocaleTimeString("ar-YE", { hour: "2-digit", minute: "2-digit" })}
          </span>
          {/* SignalR indicator */}
          <div className="flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg text-[11px] font-bold"
            style={{ background: signalrConnected ? "#f0fdf4" : "#fef2f2", color: signalrConnected ? "#16a34a" : "#ef4444",
              border: `1px solid ${signalrConnected ? "#bbf7d0" : "#fecaca"}` }}>
            <span className="w-2 h-2 rounded-full" style={{ background: signalrConnected ? "#16a34a" : "#ef4444" }} />
            {signalrConnected ? "مباشر" : "غير متصل"}
          </div>
          {/* Sound toggle */}
          <button onClick={() => setSoundEnabled(!soundEnabled)}
            className="w-9 h-9 rounded-lg flex items-center justify-center transition hover:bg-gray-100"
            style={{ border: "1px solid #e2e8f0" }}
            title={soundEnabled ? "إيقاف صوت التنبيه" : "تشغيل صوت التنبيه"}>
            {soundEnabled ? <Bell className="w-4 h-4" style={{ color: NAVY }} /> : <BellOff className="w-4 h-4" style={{ color: "#94a3b8" }} />}
          </button>
          {/* Shortcuts help */}
          <button onClick={() => setShortcutsHelpOpen(true)}
            className="w-9 h-9 rounded-lg flex items-center justify-center transition hover:bg-gray-100"
            style={{ border: "1px solid #e2e8f0" }}
            title="اختصارات لوحة المفاتيح">
            <Keyboard className="w-4 h-4" style={{ color: NAVY }} />
          </button>
          <button onClick={() => refetchItems()}
            className="w-9 h-9 rounded-lg flex items-center justify-center transition hover:bg-gray-100"
            style={{ border: "1px solid #e2e8f0" }}
            title="تحديث (Ctrl+R)">
            <RefreshCw className="w-4 h-4" style={{ color: NAVY }} />
          </button>
          {/* Walk-in button */}
          <button onClick={() => setWalkInModalOpen(true)}
            className="flex items-center gap-1.5 px-3.5 py-2 rounded-lg text-sm font-bold text-white transition"
            style={{ background: ORANGE }}
            title="مريض مشي (Ctrl+N)">
            <UserPlus className="w-4 h-4" />
            مريض مشي
          </button>
          {/* Bulk SMS reminders */}
          {!isDoctor && (
            <button onClick={() => { refetchTomorrow(); setBulkSmsModalOpen(true); }}
              className="flex items-center gap-1.5 px-3.5 py-2 rounded-lg text-sm font-bold transition"
              style={{ background: "#3d7ab50d", color: "#3d7ab5", border: "1px solid #3d7ab520" }}
              title="إرسال تذكيرات لمواعيد الغد">
              <Megaphone className="w-4 h-4" />
              تذكيرات الغد
            </button>
          )}
          {/* Print schedule */}
          <button onClick={() => window.print()}
            className="flex items-center gap-1.5 px-3.5 py-2 rounded-lg text-sm font-semibold transition"
            style={{ background: NAVY + "0d", color: NAVY, border: `1px solid ${NAVY}20` }}
            title="طباعة جدول اليوم">
            <Printer className="w-4 h-4" />
            طباعة
          </button>
          <Link href="/"
            className="flex items-center gap-1.5 px-3.5 py-2 rounded-lg text-sm font-semibold transition"
            style={{ background: NAVY + "0d", color: NAVY, border: `1px solid ${NAVY}20` }}
            onMouseEnter={e => (e.currentTarget.style.background = NAVY + "1a")}
            onMouseLeave={e => (e.currentTarget.style.background = NAVY + "0d")}>
            <ArrowLeft className="w-4 h-4" />
            لوحة التحكم
          </Link>
        </div>
      </div>

      {/* ═══ Workflow Nav ═══ */}
      <WorkflowNav
        links={[
          WORKFLOW_LINKS.bookingRequests(),
          WORKFLOW_LINKS.appointments(),
          WORKFLOW_LINKS.newAppointment(),
          WORKFLOW_LINKS.clinicQueue(),
          WORKFLOW_LINKS.patientJourney(),
          WORKFLOW_LINKS.checkout(),
          WORKFLOW_LINKS.payments(),
        ]}
        currentPage="/daily-operations"
      />

      {/* ═══ Top Bar: Date + Filters ═══ */}
      <div className="bg-white rounded-xl border p-4 flex flex-wrap items-center gap-3"
        style={{ borderColor: "#e8f0f9" }}>
        {/* Date */}
        <div className="flex items-center gap-2">
          <Calendar className="w-4 h-4" style={{ color: NAVY }} />
          <input type="date" value={filterDate} onChange={e => setFilterDate(e.target.value)}
            className="text-sm font-semibold rounded-lg border px-2.5 py-1.5 outline-none focus:border-[#3d7ab5]"
            style={{ borderColor: "#e2e8f0", color: NAVY }} />
        </div>

        {/* Branch filter */}
        {!isDoctor && branches.length > 1 && (
          <select value={filterBranch} onChange={e => setFilterBranch(e.target.value)}
            className="text-sm rounded-lg border px-2.5 py-1.5 outline-none focus:border-[#3d7ab5]"
            style={{ borderColor: "#e2e8f0", color: NAVY }}>
            <option value="">كل الفروع</option>
            {branches.map(b => <option key={b.id} value={b.id}>{b.name}</option>)}
          </select>
        )}

        {/* Doctor filter */}
        {!isDoctor && (
          <select value={filterDoctor} onChange={e => setFilterDoctor(e.target.value)}
            className="text-sm rounded-lg border px-2.5 py-1.5 outline-none focus:border-[#3d7ab5]"
            style={{ borderColor: "#e2e8f0", color: NAVY }}>
            <option value="">كل الأطباء</option>
            {doctors.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
          </select>
        )}

        {/* Status filter */}
        <select value={filterStatus} onChange={e => setFilterStatus(e.target.value)}
          className="text-sm rounded-lg border px-2.5 py-1.5 outline-none focus:border-[#3d7ab5]"
          style={{ borderColor: "#e2e8f0", color: NAVY }}>
          {statusFilters.map(s => <option key={s.value} value={s.value}>{s.label}</option>)}
        </select>

        {/* Search */}
        <div className="flex-1 min-w-[200px] relative">
          <Search className="w-4 h-4 absolute top-1/2 right-3 -translate-y-1/2" style={{ color: "#94a3b8" }} />
          <input
            ref={searchInputRef}
            type="text"
            value={searchQuery}
            onChange={e => setSearchQuery(e.target.value)}
            placeholder="بحث باسم المريض / رقم الهاتف / الطبيب... (Ctrl+F)"
            className="w-full text-sm rounded-lg border pl-3 pr-9 py-1.5 outline-none focus:border-[#3d7ab5]"
            style={{ borderColor: "#e2e8f0" }}
          />
        </div>

        {/* Date display */}
        <div className="text-sm font-bold" style={{ color: NAVY }}>
          {fmtDate(filterDate)}
        </div>
      </div>

      {/* ═══ Summary Cards ═══ */}
      <div className="grid grid-cols-2 sm:grid-cols-5 lg:grid-cols-10 gap-2.5">
        <SummaryCard icon={Calendar} label="مواعيد اليوم" value={dayStats.totalAppointments} color={ORANGE} />
        <SummaryCard icon={UserCheck} label="حضروا" value={dayStats.arrived} color="#16a34a" />
        <SummaryCard icon={Clock} label="في الانتظار" value={dayStats.waiting} color="#d97706" />
        <SummaryCard icon={Stethoscope} label="داخل العيادة" value={dayStats.inClinic} color="#9333ea" />
        <SummaryCard icon={CheckCircle} label="مكتمل" value={dayStats.completed} color="#22c55e" />
        <SummaryCard icon={AlertTriangle} label="لم يحضر" value={dayStats.noShow} color="#ef4444" />
        <SummaryCard icon={CreditCard} label="مدفوعات اليوم"
          value={fmtRial(dayStats.todayPayments)}
          color="#22c55e" />
        <SummaryCard icon={Wallet} label="المستحقات المتأخرة"
          value={fmtRial(dayStats.overdueAmount)}
          color="#ef4444"
          subValue={financeSummary?.overdueAmount ? "يحتاج متابعة" : undefined} />
        <SummaryCard icon={AlertTriangle} label="معدل عدم الحضور"
          value={`${dayStats.noShowRate}%`}
          color={dayStats.noShowRate > 20 ? "#ef4444" : "#f5922e"}
          subValue={dayStats.noShowRate > 20 ? "يحتاج متابعة" : undefined} />
        <SummaryCard icon={Clock} label="مواعيد متأخرة"
          value={dayStats.overdueAppointments}
          color={dayStats.overdueAppointments > 0 ? "#ef4444" : "#94a3b8"}
          subValue={dayStats.overdueAppointments > 0 ? "لم يحضروا بعد" : undefined} />
      </div>

      {/* ═══ Next Patient Card (Doctor view) ═══ */}
      {nextPatient && (
        <div className="rounded-xl border p-4" style={{ borderColor: "#e8f0f9", background: "linear-gradient(135deg, #1a3a5c08, #3d7ab508)" }}>
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="w-11 h-11 rounded-xl flex items-center justify-center" style={{ background: NAVY }}>
                <Activity className="w-5 h-5 text-white" />
              </div>
              <div>
                <div className="text-xs font-bold" style={{ color: "#64748b" }}>المريض القادم</div>
                <div className="text-lg font-extrabold" style={{ color: NAVY }}>{nextPatient.patientName}</div>
                <div className="text-xs" style={{ color: "#94a3b8" }}>
                  {nextPatient.doctorName} — {nextPatient.serviceName ?? "—"} — {fmtTime(nextPatient.appointmentTime)}
                </div>
              </div>
            </div>
            <div className="flex items-center gap-2">
              {nextPatient.hasMedicalAlerts && (
                <span className="px-2 py-1 rounded-lg text-xs font-bold" style={{ background: "#fef2f2", color: "#dc2626" }}>
                  ⚠ تنبيه طبي
                </span>
              )}
              {nextPatient.nextAction === "CallPatient" && (
                <button onClick={() => handleCallPatient(nextPatient)}
                  className="px-4 py-2 rounded-xl text-sm font-bold text-white flex items-center gap-1.5"
                  style={{ background: ORANGE }}>
                  نداء المريض <ArrowRight className="w-4 h-4" />
                </button>
              )}
              {nextPatient.nextAction === "EnterRoom" && (
                <button onClick={() => handleEnterRoom(nextPatient)}
                  className="px-4 py-2 rounded-xl text-sm font-bold text-white flex items-center gap-1.5"
                  style={{ background: "#9333ea" }}>
                  دخول الغرفة <ArrowRight className="w-4 h-4" />
                </button>
              )}
              {(nextPatient.nextAction === "Intake" || nextPatient.nextAction === "SendToQueue") && (
                <button onClick={() => handleIntake(nextPatient)}
                  className="px-4 py-2 rounded-xl text-sm font-bold text-white flex items-center gap-1.5"
                  style={{ background: "#16a34a" }}>
                  تسجيل حضور <ArrowRight className="w-4 h-4" />
                </button>
              )}
            </div>
          </div>
        </div>
      )}

      {/* ═══ Room Status & Doctor Workload ═══ */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-3">
        {/* Room Status */}
        {roomOccupancy.length > 0 && (
          <div className="bg-white rounded-xl border p-4" style={{ borderColor: "#e8f0f9" }}>
            <h3 className="text-xs font-bold mb-3 flex items-center gap-1.5" style={{ color: NAVY }}>
              <Building2 className="w-3.5 h-3.5" /> حالة الغرف
            </h3>
            <div className="grid grid-cols-2 sm:grid-cols-3 gap-2">
              {roomOccupancy.map(room => (
                <div key={room.roomId}
                  className="px-3 py-2.5 rounded-lg text-center"
                  style={{
                    background: room.isOccupied ? "#faf5ff" : "#f0fdf4",
                    border: `1px solid ${room.isOccupied ? "#e9d5ff" : "#bbf7d0"}`,
                  }}>
                  <div className="text-[11px] font-bold" style={{ color: room.isOccupied ? "#9333ea" : "#16a34a" }}>
                    {room.roomName}
                  </div>
                  {room.isOccupied ? (
                    <div className="text-[9px] mt-0.5" style={{ color: "#64748b" }}>
                      {room.patientName}
                    </div>
                  ) : (
                    <div className="text-[9px] mt-0.5" style={{ color: "#94a3b8" }}>فارغة</div>
                  )}
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Doctor Workload */}
        {doctorWorkload.length > 0 && !isDoctor && (
          <div className="bg-white rounded-xl border p-4" style={{ borderColor: "#e8f0f9" }}>
            <h3 className="text-xs font-bold mb-3 flex items-center gap-1.5" style={{ color: NAVY }}>
              <Users className="w-3.5 h-3.5" /> عبء الأطباء اليوم
            </h3>
            <div className="space-y-2">
              {doctorWorkload.map(dw => (
                <div key={dw.doctorId} className="flex items-center gap-2">
                  <div className="text-xs font-bold flex-shrink-0 w-24 truncate" style={{ color: NAVY }}>{dw.doctorName}</div>
                  <div className="flex-1 h-5 rounded-full overflow-hidden" style={{ background: "#f1f5f9" }}>
                    <div className="h-full rounded-full flex items-center justify-end px-2 transition-all"
                      style={{
                        width: `${Math.min(100, (dw.totalPatients / Math.max(dayStats.totalAppointments, 1)) * 100)}%`,
                        background: dw.inClinic > 0 ? "#9333ea" : dw.waiting > 0 ? ORANGE : "#16a34a",
                      }}>
                      <span className="text-[9px] font-extrabold text-white">{dw.totalPatients}</span>
                    </div>
                  </div>
                  <div className="flex gap-1.5 text-[9px] font-bold flex-shrink-0">
                    <span style={{ color: "#16a34a" }}>✓{dw.completed}</span>
                    <span style={{ color: "#9333ea" }}>🏥{dw.inClinic}</span>
                    <span style={{ color: ORANGE }}>⏳{dw.waiting}</span>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>

      {/* ═══ Tabs ═══ */}
      <div className="bg-white rounded-xl border overflow-hidden" style={{ borderColor: "#e8f0f9" }}>
        {/* Tab headers */}
        <div className="flex overflow-x-auto border-b" style={{ borderColor: "#e8f0f9" }}>
          {TABS.map(tab => {
            const count = tabCounts[tab.key];
            const isActive = activeTab === tab.key;
            return (
              <button key={tab.key} onClick={() => setActiveTab(tab.key)}
                className="flex items-center gap-1.5 px-4 py-3 text-sm font-bold whitespace-nowrap transition-colors relative"
                style={{
                  color: isActive ? tab.color : "#64748b",
                  borderBottom: isActive ? `2.5px solid ${tab.color}` : "2.5px solid transparent",
                }}>
                <span>{tab.label}</span>
                {count > 0 && (
                  <span className="text-[10px] font-extrabold px-1.5 py-0.5 rounded-full"
                    style={{ background: tab.color + "15", color: tab.color }}>
                    {count}
                  </span>
                )}
                {/* Shortcut hint */}
                <span className="text-[9px] font-mono opacity-40">{tab.shortcut}</span>
              </button>
            );
          })}
        </div>

        {/* Tab content */}
        <div className="p-4">
          <AppointmentsTable
            items={filteredItems}
            loading={itemsLoading}
            isDoctor={isDoctor}
            queueWaitTime={queueWaitTime}
            onIntake={handleIntake}
            onSendToQueue={handleSendToQueue}
            onCallPatient={handleCallPatient}
            onEnterRoom={handleEnterRoom}
            onQuickPayment={handleQuickPayment}
            onBookAppointment={handleBookAppointment}
            onWhatsApp={handleWhatsApp}
            onNoShow={handleNoShow}
            onCancel={handleCancel}
            onViewPatient={handleViewPatient}
            onCompleteVisit={handleCompleteVisit}
            onOpenSidePanel={handleOpenSidePanel}
          />
        </div>
      </div>

      {/* ═══ Quick Links Bar ═══ */}
      {visibleQuickLinks.length > 0 && (
        <div className="bg-white rounded-xl border p-4" style={{ borderColor: "#e8f0f9" }}>
          <h3 className="text-xs font-bold mb-3" style={{ color: "#64748b" }}>روابط سريعة</h3>
          <div className="flex flex-wrap gap-2">
            {visibleQuickLinks.map(link => (
              <Link key={link.href} href={link.href}
                className="flex items-center gap-1.5 px-3 py-2 rounded-lg text-xs font-bold transition"
                style={{ background: link.color + "0d", color: link.color, border: `1px solid ${link.color}20` }}
                onMouseEnter={e => { e.currentTarget.style.background = link.color + "18"; }}
                onMouseLeave={e => { e.currentTarget.style.background = link.color + "0d"; }}>
                <link.icon className="w-3.5 h-3.5" />
                {link.label}
              </Link>
            ))}
          </div>
        </div>
      )}

      {/* ═══ Modals ═══ */}
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
        onConfirm={async (roomId: string) => {
          if (!selectedItem?.queueItemId) return;
          try {
            await changeRoomMutation.mutateAsync({ queueItemId: selectedItem.queueItemId, roomId });
            toast.success("تم تغيير الغرفة");
            setChangeRoomModalOpen(false);
          } catch {
            toast.error("فشل تغيير الغرفة");
          }
        }}
      />

      {/* Improvement 4: Walk-in Modal */}
      <WalkInModal
        open={walkInModalOpen}
        onClose={() => setWalkInModalOpen(false)}
        doctors={doctors}
        services={services}
        branches={branches}
        isPending={walkInMutation.isPending}
        onConfirm={handleWalkInConfirm}
      />

      {/* Improvement 5: Undo Toast */}
      {undoAction && (
        <UndoToast
          action={undoAction}
          onUndo={handleUndo}
          onDismiss={() => setUndoAction(null)}
        />
      )}

      {/* Improvement 6: Patient Side Panel */}
      <PatientSidePanel
        open={sidePanelOpen}
        onClose={() => setSidePanelOpen(false)}
        item={sidePanelItem}
        summary={selectedSummary ?? null}
        waitTime={queueWaitTime}
      />

      {/* Improvement 7: Keyboard Shortcuts Help */}
      <KeyboardShortcutsHelp
        open={shortcutsHelpOpen}
        onClose={() => setShortcutsHelpOpen(false)}
      />

      {/* Bulk SMS Modal */}
      <BulkSmsModal
        open={bulkSmsModalOpen}
        onClose={() => setBulkSmsModalOpen(false)}
        tomorrowItems={tomorrowItems}
        isPending={bulkSmsMutation.isPending}
        onConfirm={handleBulkSms}
      />
    </div>
  );
}
