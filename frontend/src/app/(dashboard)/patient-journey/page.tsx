"use client";

import { useEffect, useState, useCallback, useMemo } from "react";
import Link from "next/link";
import {
  Users, UserCheck, Clock, DoorOpen, CreditCard, CheckCircle2,
  Save, RefreshCw, Stethoscope, ArrowRight, Phone, CalendarDays,
  Filter, FileText, AlertTriangle, ExternalLink, Megaphone,
  PlayCircle, ShieldAlert, Wallet, CircleDot,
  Wrench, HeartPulse, Route,
  X, MessageSquare, Printer, Download, Pill, Send, Ban, UserX,
  ArrowRightLeft, Timer, Plus, Trash2, MessageCircle,
} from "lucide-react";
import api from "@/lib/api";
import { cn } from "@/lib/utils";
import { useAuthStore } from "@/stores/authStore";
import { toast } from "@/stores/toastStore";
import { WorkflowNav, WORKFLOW_LINKS } from "@/components/shared/WorkflowNav";
import { useHasPermission, PERMISSION_KEYS } from "@/hooks/usePermissions";
import {
  useDailyJourneySummary,
  useJourneyIntake,
  useJourneySendToQueue,
  useJourneyStartVisit,
  useJourneyHandoff,
  useJourneyCheckout,
  useJourneyCreateDraftInvoice,
  useJourneyCancelQueue,
  useJourneyChangeRoom,
  useJourneyUpdateAppointmentStatus,
  useJourneyCreatePayment,
  useJourneyIssueInvoice,
  useJourneyUpdateVisit,
  useJourneySendSms,
  useJourneySendAppointmentReminder,
  useJourneyCreatePrescription,
  useJourneyCreateAppointment,
  useQueueEstimatedWait,
} from "@/hooks/usePatientJourney";
import { QUEUE_STATUS_ARABIC } from "@/types/journey";
import type {
  DailyJourneySummary,
  MedicalAlert,
  DailyJourneyRecentVisit,
  TimelineEvent,
} from "@/types/journey";

// ─── Types ────────────────────────────────────────────────────────────────────

interface JourneyItem {
  appointmentId: string;
  patientId: string;
  patientName: string;
  patientPhone?: string;
  appointmentTime: string;
  appointmentStatus: string;
  doctorId: string;
  doctorName: string;
  serviceId?: string;
  serviceName?: string;
  roomName?: string;
  queueItemId?: string;
  queueStatus?: string;
  visitId?: string;
  visitStatus?: string;
  consultationFeeRequired: boolean;
  consultationFeePaid: boolean;
  checkoutStatus?: string;
  nextAction: string;
}

interface ServiceOption {
  id: string;
  arabicName: string;
  code: string;
  defaultPrice: number;
  requiresConsultationFee: boolean;
}

interface RoomOption {
  id: string;
  arabicName: string;
  code: string;
  roomType: string;
}

// ─── Journey Steps (8 steps as specified) ────────────────────────────────────

const JOURNEY_STEPS = [
  { key: "Scheduled", label: "مجدول" },
  { key: "Arrived", label: "وصل" },
  { key: "Waiting", label: "في الانتظار" },
  { key: "Called", label: "تم النداء" },
  { key: "InRoom", label: "داخل الغرفة" },
  { key: "InProgress", label: "قيد المعالجة" },
  { key: "Handoff", label: "تسليم" },
  { key: "Checkout", label: "إنهاء الحساب" },
] as const;

const STEP_ORDER_MAP: Record<string, number> = {
  Scheduled: 0,
  Arrived: 1,
  Waiting: 2,
  Called: 3,
  InRoom: 4,
  InProgress: 5,
  Handoff: 6,
  Checkout: 7,
  Completed: 8,
};

function getStepIndex(status: string): number {
  return STEP_ORDER_MAP[status] ?? -1;
}

function getStepStatus(stepKey: string, currentStep: string): "done" | "current" | "pending" {
  const currentIdx = getStepIndex(currentStep);
  const stepIdx = getStepIndex(stepKey);
  if (currentIdx < 0 || stepIdx < 0) return "pending";
  if (stepIdx < currentIdx) return "done";
  if (stepIdx === currentIdx) return "current";
  return "pending";
}

// ─── Constants ────────────────────────────────────────────────────────────────

const STATUS_LABELS: Record<string, string> = {
  Scheduled: "مجدول",
  Confirmed: "مؤكد",
  Arrived: "وصل",
  Waiting: "في الانتظار",
  Called: "تم النداء",
  InRoom: "داخل الغرفة",
  InProgress: "قيد المعالجة",
  Completed: "مكتمل",
  Cancelled: "ملغي",
  NoShow: "لم يحضر",
  Handoff: "تسليم",
  Checkout: "إنهاء الحساب",
};

const STATUS_COLORS: Record<string, string> = {
  Scheduled: "bg-blue-50 text-blue-700",
  Confirmed: "bg-indigo-50 text-indigo-700",
  Arrived: "bg-amber-50 text-amber-700",
  Waiting: "bg-orange-50 text-orange-700",
  Called: "bg-purple-50 text-purple-700",
  InRoom: "bg-cyan-50 text-cyan-700",
  InProgress: "bg-emerald-50 text-emerald-700",
  Completed: "bg-green-50 text-green-700",
  Cancelled: "bg-gray-100 text-gray-500",
  NoShow: "bg-red-50 text-red-700",
  Handoff: "bg-teal-50 text-teal-700",
  Checkout: "bg-lime-50 text-lime-700",
};

const ACTION_LABELS: Record<string, string> = {
  Intake: "تسجيل الوصول",
  SendToQueue: "إدخال للطابور",
  CallPatient: "نداء المريض",
  EnterRoom: "إدخال الغرفة",
  StartVisit: "بدء الزيارة",
  InProgress: "عند الطبيب",
  Handoff: "تسليم للاستقبال",
  Checkout: "إنهاء الحساب",
  None: "—",
};

const ACTION_COLORS: Record<string, string> = {
  Intake: "bg-amber-500 hover:bg-amber-600",
  SendToQueue: "bg-blue-500 hover:bg-blue-600",
  CallPatient: "bg-purple-500 hover:bg-purple-600",
  EnterRoom: "bg-cyan-500 hover:bg-cyan-600",
  StartVisit: "bg-emerald-500 hover:bg-emerald-600",
  InProgress: "bg-gray-400",
  Handoff: "bg-teal-500 hover:bg-teal-600",
  Checkout: "bg-green-600 hover:bg-green-700",
  None: "bg-gray-300",
};

const PAYMENT_METHODS = [
  { value: "cash", label: "نقدي" },
  { value: "transfer", label: "تحويل" },
  { value: "card", label: "بطاقة" },
];

const SEVERITY_STYLES: Record<string, { bg: string; border: string; text: string }> = {
  danger: { bg: "bg-[#fcebeb]", border: "border-[#f09595]/50", text: "text-[#a32d2d]" },
  warning: { bg: "bg-[#faeeda]", border: "border-[#fac775]/50", text: "text-[#633806]" },
  info: { bg: "bg-[#e6f1fb]", border: "border-[#85b7eb]/50", text: "text-[#185fa5]" },
};

const TIMELINE_DOT_COLORS: Record<string, string> = {
  appointment: "bg-[#3d7ab5]",
  visit: "bg-[#3d7ab5]",
  payment: "bg-[#fac775]",
  invoice: "bg-[#185fa5]",
  document: "bg-[#d3d1c7]",
  ortho: "bg-[#3d7ab5]",
  message: "bg-[#185fa5]",
  default: "bg-[#d3d1c7]",
};

const inputCls =
  "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-[#3d7ab5] focus:border-[#3d7ab5]";

// ─── Helpers ──────────────────────────────────────────────────────────────────

function fmtRial(amount: number): string {
  return amount.toLocaleString("ar-SA") + " ر.ي";
}

function fmtDate(dateStr: string): string {
  try {
    return new Intl.DateTimeFormat("ar-YE", {
      year: "numeric", month: "long", day: "numeric",
    }).format(new Date(dateStr));
  } catch {
    return dateStr;
  }
}

function fmtTime(timeStr: string): string {
  const [h, m] = timeStr.split(":");
  const hour = parseInt(h);
  const period = hour >= 12 ? "م" : "ص";
  const h12 = hour === 0 ? 12 : hour > 12 ? hour - 12 : hour;
  return `${h12}:${m} ${period}`;
}

function getInitials(name: string): string {
  return name
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((w) => w[0])
    .join("");
}

// ─── Role Helpers ─────────────────────────────────────────────────────────────

function isDoctorRole(role: string): boolean {
  return role === "Orthodontist" || role === "GeneralDentist" || role === "OralSurgeon";
}

function isAccountantRole(role: string): boolean {
  return role === "Accountant";
}

function isReceptionRole(role: string): boolean {
  return role === "Receptionist";
}

// ─── Main Page ────────────────────────────────────────────────────────────────

export default function PatientJourneyPage() {
  const { user } = useAuthStore();
  const userRole = user?.role ?? "";
  const isDoctor = isDoctorRole(userRole);
  const isAccountant = isAccountantRole(userRole);
  const isReception = isReceptionRole(userRole);

  // Permissions
  const canViewJourney = useHasPermission(PERMISSION_KEYS.PATIENT_JOURNEY_VIEW);
  const canEditJourney = useHasPermission(PERMISSION_KEYS.PATIENT_JOURNEY_EDIT);
  const canViewVisits = useHasPermission(PERMISSION_KEYS.VISITS_VIEW);
  const canViewFinance = useHasPermission(PERMISSION_KEYS.PAYMENTS_VIEW);
  const canViewCheckout = useHasPermission(PERMISSION_KEYS.CHECKOUT_VIEW);
  const canViewInvoices = useHasPermission(PERMISSION_KEYS.INVOICES_VIEW);

  // ─── Left Panel State ─────────────────────────────────────────────────────
  const [items, setItems] = useState<JourneyItem[]>([]);
  const [services, setServices] = useState<ServiceOption[]>([]);
  const [rooms, setRooms] = useState<RoomOption[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  // Filters
  const [filterDate, setFilterDate] = useState(
    new Date().toISOString().split("T")[0]
  );
  const [filterStatus, setFilterStatus] = useState("");
  const [filterService, setFilterService] = useState("");
  const [filterRoom, setFilterRoom] = useState("");

  // Selected patient
  const [selectedPatientId, setSelectedPatientId] = useState<string | null>(null);

  // ─── Right Panel: Daily Summary ────────────────────────────────────────────
  const { data: summary, isLoading: summaryLoading, error: summaryError, refetch: refetchSummary } =
    useDailyJourneySummary(selectedPatientId);

  // ─── Action Mutations ──────────────────────────────────────────────────────
  const intakeMutation = useJourneyIntake();
  const sendToQueueMutation = useJourneySendToQueue();
  const startVisitMutation = useJourneyStartVisit();
  const handoffMutation = useJourneyHandoff();
  const checkoutMutation = useJourneyCheckout();
  const draftInvoiceMutation = useJourneyCreateDraftInvoice();

  // ─── New Action Mutations ──────────────────────────────────────────────────
  const cancelQueueMutation = useJourneyCancelQueue();
  const changeRoomMutation = useJourneyChangeRoom();
  const updateAptStatusMutation = useJourneyUpdateAppointmentStatus();
  const createPaymentMutation = useJourneyCreatePayment();
  const issueInvoiceMutation = useJourneyIssueInvoice();
  const updateVisitMutation = useJourneyUpdateVisit();
  const sendSmsMutation = useJourneySendSms();
  const sendReminderMutation = useJourneySendAppointmentReminder();
  const createPrescriptionMutation = useJourneyCreatePrescription();
  const createAppointmentMutation = useJourneyCreateAppointment();

  // ─── Intake Form State ─────────────────────────────────────────────────────
  const [intakeService, setIntakeService] = useState("");
  const [intakeComplaint, setIntakeComplaint] = useState("");
  const [intakeRoom, setIntakeRoom] = useState("");
  const [intakeConsultFee, setIntakeConsultFee] = useState(false);
  const [intakeConsultAmount, setIntakeConsultAmount] = useState(0);
  const [intakeNotes, setIntakeNotes] = useState("");

  // ─── Handoff Form State ────────────────────────────────────────────────────
  const [handoffForm, setHandoffForm] = useState({
    treatmentDone: "",
    diagnosis: "",
    nextVisitPlan: "",
    instructions: "",
    amountDue: 0,
    notes: "",
  });

  // ─── Checkout Form State ───────────────────────────────────────────────────
  const [checkoutAmount, setCheckoutAmount] = useState(0);
  const [checkoutPayment, setCheckoutPayment] = useState("cash");
  const [checkoutNextDate, setCheckoutNextDate] = useState("");
  const [checkoutNextService, setCheckoutNextService] = useState("");
  const [checkoutNotes, setCheckoutNotes] = useState("");
  const [checkoutSuccess, setCheckoutSuccess] = useState(false);
  const [draftInvoiceResult, setDraftInvoiceResult] = useState<{ invoiceId: string; invoiceNumber: string } | null>(null);

  // ─── Cancel Appointment/NoShow Dialog ──────────────────────────────────────
  const [cancelDialogOpen, setCancelDialogOpen] = useState(false);
  const [cancelDialogType, setCancelDialogType] = useState<"Cancelled" | "NoShow" | "CancelQueue" | "ChangeRoom">("Cancelled");
  const [pendingChangeRoomId, setPendingChangeRoomId] = useState("");
  const [pendingChangeRoomName, setPendingChangeRoomName] = useState("");

  // ─── Record Payment Modal ──────────────────────────────────────────────────
  const [recordPaymentOpen, setRecordPaymentOpen] = useState(false);
  const [paymentAmount, setPaymentAmount] = useState(0);
  const [paymentMethod, setPaymentMethod] = useState("cash");
  const [paymentServiceDesc, setPaymentServiceDesc] = useState("");
  const [paymentNotes, setPaymentNotes] = useState("");

  // ─── Send SMS Modal ────────────────────────────────────────────────────────
  const [smsModalOpen, setSmsModalOpen] = useState(false);
  const [smsTo, setSmsTo] = useState("");
  const [smsMessage, setSmsMessage] = useState("");

  // ─── Prescription Modal ────────────────────────────────────────────────────
  const [prescriptionModalOpen, setPrescriptionModalOpen] = useState(false);
  const [prescDiagnosis, setPrescDiagnosis] = useState("");
  const [prescNotes, setPrescNotes] = useState("");
  const [prescItems, setPrescItems] = useState<Array<{
    medicationName: string; dosage: string; frequency: string; duration: string; notes: string;
  }>>([{ medicationName: "", dosage: "", frequency: "", duration: "", notes: "" }]);

  // ─── Edit Visit Modal ──────────────────────────────────────────────────────
  const [editVisitModalOpen, setEditVisitModalOpen] = useState(false);
  const [editVisitForm, setEditVisitForm] = useState({
    chiefComplaint: "", clinicalNotes: "", treatmentDone: "", diagnosis: "", instructions: "", nextVisitPlan: "", cost: 0,
  });

  // ─── Book Appointment Modal ────────────────────────────────────────────────
  const [bookAppointmentModalOpen, setBookAppointmentModalOpen] = useState(false);
  const [bookAptDate, setBookAptDate] = useState("");
  const [bookAptStartTime, setBookAptStartTime] = useState("");
  const [bookAptEndTime, setBookAptEndTime] = useState("");
  const [bookAptDoctorId, setBookAptDoctorId] = useState("");
  const [bookAptServiceId, setBookAptServiceId] = useState("");
  const [bookAptType, setBookAptType] = useState("FollowUp");
  const [bookAptNotes, setBookAptNotes] = useState("");

  // ─── Change Room Inline ────────────────────────────────────────────────────
  const [changeRoomSelectedRoom, setChangeRoomSelectedRoom] = useState("");

  // ─── PDF Download Loading ────────────────────────────────────────────────────
  const [pdfDownloading, setPdfDownloading] = useState(false);

  // ─── Data Loading ──────────────────────────────────────────────────────────

  const loadJourney = useCallback(() => {
    setLoading(true);
    setError("");
    const params = new URLSearchParams();
    if (filterDate) params.set("date", filterDate);
    if (filterStatus) params.set("status", filterStatus);
    if (filterService) params.set("serviceId", filterService);
    if (filterRoom) params.set("roomId", filterRoom);
    api
      .get<JourneyItem[]>(`/api/patient-journey/today?${params.toString()}`)
      .then((r) => {
        setItems(r.data);
        // Auto-select first patient if none selected
        if (r.data.length > 0 && !selectedPatientId) {
          setSelectedPatientId(r.data[0].patientId);
        }
      })
      .catch((err) => {
        const msg = err?.response?.data?.message ?? "فشل تحميل البيانات";
        setError(msg);
      })
      .finally(() => setLoading(false));
  }, [filterDate, filterStatus, filterService, filterRoom, selectedPatientId]);

  useEffect(loadJourney, [loadJourney]);

  useEffect(() => {
    api
      .get<ServiceOption[]>("/api/settings/services/active")
      .then((r) => setServices(r.data))
      .catch(() => {});
    api
      .get<RoomOption[]>("/api/settings/rooms/active")
      .then((r) => setRooms(r.data))
      .catch(() => {});
  }, []);

  // Reset form states when patient changes
  useEffect(() => {
    setCheckoutSuccess(false);
    setDraftInvoiceResult(null);
    setIntakeService("");
    setIntakeComplaint("");
    setIntakeRoom("");
    setIntakeConsultFee(false);
    setIntakeConsultAmount(0);
    setIntakeNotes("");
    setHandoffForm({ treatmentDone: "", diagnosis: "", nextVisitPlan: "", instructions: "", amountDue: 0, notes: "" });
    setCheckoutAmount(0);
    setCheckoutPayment("cash");
    setCheckoutNextDate("");
    setCheckoutNextService("");
    setCheckoutNotes("");
    // Reset new modal states
    setCancelDialogOpen(false);
    setPendingChangeRoomId("");
    setPendingChangeRoomName("");
    setPdfDownloading(false);
    setRecordPaymentOpen(false);
    setPaymentAmount(0);
    setPaymentMethod("cash");
    setPaymentServiceDesc("");
    setPaymentNotes("");
    setSmsModalOpen(false);
    setSmsTo("");
    setSmsMessage("");
    setPrescriptionModalOpen(false);
    setPrescDiagnosis("");
    setPrescNotes("");
    setPrescItems([{ medicationName: "", dosage: "", frequency: "", duration: "", notes: "" }]);
    setEditVisitModalOpen(false);
    setEditVisitForm({ chiefComplaint: "", clinicalNotes: "", treatmentDone: "", diagnosis: "", instructions: "", nextVisitPlan: "", cost: 0 });
    setBookAppointmentModalOpen(false);
    setBookAptDate("");
    setBookAptStartTime("");
    setBookAptEndTime("");
    setBookAptDoctorId("");
    setBookAptServiceId("");
    setBookAptType("FollowUp");
    setBookAptNotes("");
    setChangeRoomSelectedRoom("");
  }, [selectedPatientId]);

  // ─── Auto-fill Checkout Amount ──────────────────────────────────────────────
  useEffect(() => {
    if (summary?.todayVisit?.amountDueReference && checkoutAmount === 0) {
      setCheckoutAmount(summary.todayVisit.amountDueReference);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [summary?.todayVisit?.amountDueReference]);

  // ─── Summary Cards ─────────────────────────────────────────────────────────

  const todayCount = items.length;
  const arrivedCount = items.filter((i) =>
    ["Arrived", "Waiting", "Called", "InRoom"].includes(i.appointmentStatus)
  ).length;
  const waitingCount = items.filter(
    (i) => i.appointmentStatus === "Waiting" || i.appointmentStatus === "Called"
  ).length;
  const inRoomCount = items.filter(
    (i) => i.appointmentStatus === "InRoom" || i.appointmentStatus === "InProgress"
  ).length;
  const checkoutCount = items.filter(
    (i) => i.checkoutStatus === "ReadyForCheckout"
  ).length;
  const completedCount = items.filter(
    (i) => i.appointmentStatus === "Completed"
  ).length;

  const summaryCards = [
    { label: "مواعيد اليوم", count: todayCount, icon: CalendarDays, color: "bg-blue-500" },
    { label: "وصلوا", count: arrivedCount, icon: UserCheck, color: "bg-amber-500" },
    { label: "في الانتظار", count: waitingCount, icon: Clock, color: "bg-orange-500" },
    { label: "داخل الغرفة", count: inRoomCount, icon: DoorOpen, color: "bg-cyan-500" },
    { label: "جاهز للحساب", count: checkoutCount, icon: CreditCard, color: "bg-green-600" },
    { label: "مكتمل", count: completedCount, icon: CheckCircle2, color: "bg-emerald-500" },
  ];

  // ─── Selected JourneyItem ──────────────────────────────────────────────────

  const selectedItem = useMemo(
    () => items.find((i) => i.patientId === selectedPatientId) ?? null,
    [items, selectedPatientId]
  );

  // ─── Queue Estimated Wait ──────────────────────────────────────────────────
  const queueItemId = selectedItem?.queueItemId || summary?.queueStatus?.id || null;
  const { data: estimatedWait } = useQueueEstimatedWait(queueItemId);

  // ─── Action Handlers ──────────────────────────────────────────────────────

  const handleSimpleAction = useCallback(async (item: JourneyItem) => {
    try {
      if (item.nextAction === "SendToQueue") {
        await sendToQueueMutation.mutateAsync({ appointmentId: item.appointmentId });
        toast.success("تم إضافة المريض للطابور");
      } else if (item.nextAction === "CallPatient") {
        await api.post(`/api/clinic-queue/${item.queueItemId}/call`, {});
        toast.success("تم نداء المريض");
      } else if (item.nextAction === "EnterRoom") {
        await api.post(`/api/clinic-queue/${item.queueItemId}/enter-room`);
        toast.success("تم تسجيل دخول الغرفة");
      } else if (item.nextAction === "StartVisit") {
        await startVisitMutation.mutateAsync(item.appointmentId);
        toast.success("تم بدء الزيارة");
      }
      loadJourney();
      refetchSummary();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "حدث خطأ";
      toast.error(msg);
    }
  }, [sendToQueueMutation, startVisitMutation, loadJourney, refetchSummary]);

  const handleIntakeSubmit = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedItem) return;
    try {
      await intakeMutation.mutateAsync({
        appointmentId: selectedItem.appointmentId,
        body: {
          serviceId: intakeService || undefined,
          chiefComplaint: intakeComplaint || undefined,
          roomId: intakeRoom || undefined,
          requiresConsultationFee: intakeConsultFee || undefined,
          consultationFeeAmount: intakeConsultFee ? intakeConsultAmount : undefined,
          notes: intakeNotes || undefined,
        },
      });
      // Also send to queue
      await sendToQueueMutation.mutateAsync({
        appointmentId: selectedItem.appointmentId,
        body: { roomId: intakeRoom || undefined },
      });
      toast.success("تم تسجيل الوصول وإدخال الطابور");
      loadJourney();
      refetchSummary();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "حدث خطأ";
      toast.error(msg);
    }
  }, [selectedItem, intakeService, intakeComplaint, intakeRoom, intakeConsultFee, intakeConsultAmount, intakeNotes, intakeMutation, sendToQueueMutation, loadJourney, refetchSummary]);

  const handleHandoffSubmit = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    if (!summary?.todayVisit?.id) return;
    try {
      await handoffMutation.mutateAsync({
        visitId: summary.todayVisit.id,
        body: {
          treatmentDone: handoffForm.treatmentDone || undefined,
          diagnosis: handoffForm.diagnosis || undefined,
          nextVisitPlan: handoffForm.nextVisitPlan || undefined,
          instructions: handoffForm.instructions || undefined,
          amountDue: handoffForm.amountDue || undefined,
          notes: handoffForm.notes || undefined,
        },
      });
      toast.success("تم تسليم المريض للاستقبال");
      loadJourney();
      refetchSummary();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "حدث خطأ";
      toast.error(msg);
    }
  }, [summary?.todayVisit?.id, handoffForm, handoffMutation, loadJourney, refetchSummary]);

  const handleCheckoutSubmit = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedItem) return;
    try {
      await checkoutMutation.mutateAsync({
        appointmentId: selectedItem.appointmentId,
        body: {
          paymentAmount: checkoutAmount > 0 ? checkoutAmount : undefined,
          paymentMethod: checkoutPayment,
          nextAppointmentDate: checkoutNextDate || undefined,
          nextServiceId: checkoutNextService || undefined,
          notes: checkoutNotes || undefined,
        },
      });
      setCheckoutSuccess(true);
      toast.success("تم إنهاء الحساب بنجاح");
      loadJourney();
      refetchSummary();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "حدث خطأ";
      toast.error(msg);
    }
  }, [selectedItem, checkoutAmount, checkoutPayment, checkoutNextDate, checkoutNextService, checkoutNotes, checkoutMutation, loadJourney, refetchSummary]);

  const handleCreateDraftInvoice = useCallback(async () => {
    if (!summary?.todayVisit?.id) {
      toast.error("لا يوجد زيارة مرتبطة لإنشاء فاتورة");
      return;
    }
    try {
      const res = await draftInvoiceMutation.mutateAsync(summary.todayVisit.id);
      setDraftInvoiceResult({ invoiceId: res.id, invoiceNumber: res.invoiceNumber });
      toast.success(`تم إنشاء الفاتورة ${res.invoiceNumber}`);
    } catch {
      toast.error("فشل إنشاء الفاتورة المسودة");
    }
  }, [summary?.todayVisit?.id, draftInvoiceMutation]);

  // ─── Confirm Action (Cancel/NoShow/CancelQueue/ChangeRoom) ────────────────
  const handleConfirmAction = useCallback(async () => {
    try {
      if (cancelDialogType === "Cancelled" || cancelDialogType === "NoShow") {
        if (!summary?.todayAppointment?.id) return;
        await updateAptStatusMutation.mutateAsync({
          appointmentId: summary.todayAppointment.id,
          status: cancelDialogType,
        });
        toast.success(cancelDialogType === "Cancelled" ? "تم إلغاء الموعد" : "تم تسجيل عدم الحضور");
      } else if (cancelDialogType === "CancelQueue") {
        if (!queueItemId) return;
        await cancelQueueMutation.mutateAsync(queueItemId);
        toast.success("تم إلغاء المريض من الطابور");
      } else if (cancelDialogType === "ChangeRoom") {
        if (!queueItemId || !pendingChangeRoomId) return;
        await changeRoomMutation.mutateAsync({ queueItemId, roomId: pendingChangeRoomId });
        toast.success("تم تغيير الغرفة");
        setChangeRoomSelectedRoom("");
      }
      setCancelDialogOpen(false);
      setPendingChangeRoomId("");
      loadJourney();
      refetchSummary();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "حدث خطأ";
      toast.error(msg);
    }
  }, [cancelDialogType, summary?.todayAppointment?.id, queueItemId, pendingChangeRoomId, updateAptStatusMutation, cancelQueueMutation, changeRoomMutation, loadJourney, refetchSummary]);

  // ─── Request Cancel from Queue (shows confirmation) ────────────────────────
  const handleRequestCancelQueue = useCallback(() => {
    setCancelDialogType("CancelQueue");
    setCancelDialogOpen(true);
  }, []);

  // ─── Request Change Room (shows confirmation) ───────────────────────────────
  const handleRequestChangeRoom = useCallback((roomId: string) => {
    const targetRoom = rooms.find((r) => r.id === roomId);
    setPendingChangeRoomId(roomId);
    setChangeRoomSelectedRoom(roomId);
    setCancelDialogType("ChangeRoom");
    setCancelDialogOpen(true);
    // Store target room name for dialog display
    setPendingChangeRoomName(targetRoom?.arabicName ?? "");
  }, [rooms]);

  // ─── Record Payment ────────────────────────────────────────────────────────
  const handleRecordPayment = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedPatientId || paymentAmount <= 0) {
      toast.error("يرجى إدخال مبلغ صحيح");
      return;
    }
    try {
      await createPaymentMutation.mutateAsync({
        patientId: selectedPatientId,
        amount: paymentAmount,
        paymentMethod: paymentMethod || undefined,
        serviceDescription: paymentServiceDesc || undefined,
        notes: paymentNotes || undefined,
        doctorId: selectedItem?.doctorId || undefined,
      });
      toast.success("تم تسجيل الدفعة بنجاح");
      setRecordPaymentOpen(false);
      setPaymentAmount(0);
      setPaymentServiceDesc("");
      setPaymentNotes("");
      loadJourney();
      refetchSummary();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "حدث خطأ";
      toast.error(msg);
    }
  }, [selectedPatientId, paymentAmount, paymentMethod, paymentServiceDesc, paymentNotes, selectedItem?.doctorId, createPaymentMutation, loadJourney, refetchSummary]);

  // ─── Issue Invoice ─────────────────────────────────────────────────────────
  const handleIssueInvoice = useCallback(async () => {
    if (!draftInvoiceResult?.invoiceId) return;
    try {
      await issueInvoiceMutation.mutateAsync(draftInvoiceResult.invoiceId);
      toast.success("تم إصدار الفاتورة");
      refetchSummary();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "حدث خطأ";
      toast.error(msg);
    }
  }, [draftInvoiceResult?.invoiceId, issueInvoiceMutation, refetchSummary]);

  // ─── Download PDF ──────────────────────────────────────────────────────────
  const handleDownloadPdf = useCallback(async (url: string, filename: string) => {
    setPdfDownloading(true);
    try {
      const res = await api.get(url, { responseType: "blob" });
      const blob = new Blob([res.data as BlobPart], { type: "application/pdf" });
      const linkUrl = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = linkUrl;
      a.download = filename;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(linkUrl);
      toast.success("تم تحميل الملف بنجاح");
    } catch {
      toast.error("فشل تحميل الملف");
    } finally {
      setPdfDownloading(false);
    }
  }, []);

  // ─── Send SMS ──────────────────────────────────────────────────────────────
  const handleSendSms = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    if (!smsTo || !smsMessage) {
      toast.error("يرجى إدخال رقم الهاتف والرسالة");
      return;
    }
    try {
      await sendSmsMutation.mutateAsync({
        to: smsTo,
        message: smsMessage,
        patientId: selectedPatientId || undefined,
      });
      toast.success("تم إرسال الرسالة");
      setSmsModalOpen(false);
      setSmsMessage("");
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "حدث خطأ";
      toast.error(msg);
    }
  }, [smsTo, smsMessage, selectedPatientId, sendSmsMutation]);

  // ─── Send Appointment Reminder ─────────────────────────────────────────────
  const handleSendReminder = useCallback(async () => {
    if (!summary?.todayAppointment?.id) return;
    try {
      await sendReminderMutation.mutateAsync(summary.todayAppointment.id);
      toast.success("تم إرسال التذكير");
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "حدث خطأ";
      toast.error(msg);
    }
  }, [summary?.todayAppointment?.id, sendReminderMutation]);

  // ─── WhatsApp Reminder ─────────────────────────────────────────────────────
  const handleOpenWhatsApp = useCallback(() => {
    const phone = summary?.patient.phone;
    if (!phone) {
      toast.error("لا يوجد رقم هاتف للمريض");
      return;
    }
    // Ensure country code: if starts with 0, replace with 967 (Yemen)
    let cleanPhone = phone.replace(/[^0-9+]/g, "");
    if (cleanPhone.startsWith("0")) {
      cleanPhone = "967" + cleanPhone.substring(1);
    } else if (!cleanPhone.startsWith("+") && !cleanPhone.startsWith("967") && cleanPhone.length <= 9) {
      cleanPhone = "967" + cleanPhone;
    }
    const aptTime = summary?.todayAppointment?.startTime
      ? fmtTime(summary.todayAppointment.startTime)
      : "";
    const aptDate = summary?.todayAppointment?.appointmentDate
      ? fmtDate(summary.todayAppointment.appointmentDate)
      : "";
    const doctorName = summary?.todayAppointment?.doctorName ?? "الطبيب";
    const msg = `تذكير: لديكم موعد يوم ${aptDate} الساعة ${aptTime} مع ${doctorName}`;
    const encoded = encodeURIComponent(msg);
    window.open(`https://wa.me/${cleanPhone}?text=${encoded}`, "_blank");
  }, [summary?.patient.phone, summary?.todayAppointment?.startTime, summary?.todayAppointment?.appointmentDate, summary?.todayAppointment?.doctorName]);

  // ─── Create Prescription ───────────────────────────────────────────────────
  const handleCreatePrescription = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedPatientId) return;
    const validItems = prescItems.filter((item) => item.medicationName.trim());
    if (validItems.length === 0) {
      toast.error("يرجى إضافة دواء واحد على الأقل");
      return;
    }
    try {
      await createPrescriptionMutation.mutateAsync({
        patientId: selectedPatientId,
        doctorId: selectedItem?.doctorId || undefined,
        diagnosis: prescDiagnosis || undefined,
        notes: prescNotes || undefined,
        items: validItems.map((item) => ({
          medicationName: item.medicationName,
          dosage: item.dosage || undefined,
          frequency: item.frequency || undefined,
          duration: item.duration || undefined,
          notes: item.notes || undefined,
        })),
      });
      toast.success("تم إنشاء الوصفة الطبية");
      setPrescriptionModalOpen(false);
      setPrescDiagnosis("");
      setPrescNotes("");
      setPrescItems([{ medicationName: "", dosage: "", frequency: "", duration: "", notes: "" }]);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "حدث خطأ";
      toast.error(msg);
    }
  }, [selectedPatientId, prescDiagnosis, prescNotes, prescItems, selectedItem?.doctorId, createPrescriptionMutation]);

  // ─── Update Visit ──────────────────────────────────────────────────────────
  const handleUpdateVisit = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    if (!summary?.todayVisit?.id) return;
    try {
      await updateVisitMutation.mutateAsync({
        visitId: summary.todayVisit.id,
        body: {
          chiefComplaint: editVisitForm.chiefComplaint || undefined,
          clinicalNotes: editVisitForm.clinicalNotes || undefined,
          treatmentDone: editVisitForm.treatmentDone || undefined,
          diagnosis: editVisitForm.diagnosis || undefined,
          instructions: editVisitForm.instructions || undefined,
          nextVisitPlan: editVisitForm.nextVisitPlan || undefined,
          cost: editVisitForm.cost || undefined,
        },
      });
      toast.success("تم تحديث ملاحظات الزيارة");
      setEditVisitModalOpen(false);
      refetchSummary();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "حدث خطأ";
      toast.error(msg);
    }
  }, [summary?.todayVisit?.id, editVisitForm, updateVisitMutation, refetchSummary]);

  // ─── Book Next Appointment ─────────────────────────────────────────────────
  const handleBookAppointment = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedPatientId || !bookAptDoctorId || !bookAptDate || !bookAptStartTime) {
      toast.error("يرجى ملء جميع الحقول المطلوبة");
      return;
    }
    try {
      await createAppointmentMutation.mutateAsync({
        patientId: selectedPatientId,
        doctorId: bookAptDoctorId,
        appointmentDate: bookAptDate,
        startTime: bookAptStartTime,
        endTime: bookAptEndTime || undefined,
        serviceId: bookAptServiceId || undefined,
        appointmentType: bookAptType || undefined,
        notes: bookAptNotes || undefined,
      });
      toast.success("تم حجز الموعد بنجاح");
      setBookAppointmentModalOpen(false);
      setBookAptDate("");
      setBookAptStartTime("");
      setBookAptEndTime("");
      setBookAptServiceId("");
      setBookAptNotes("");
      loadJourney();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "حدث خطأ";
      toast.error(msg);
    }
  }, [selectedPatientId, bookAptDoctorId, bookAptDate, bookAptStartTime, bookAptEndTime, bookAptServiceId, bookAptType, bookAptNotes, createAppointmentMutation, loadJourney]);

  // ─── Render ───────────────────────────────────────────────────────────────

  return (
    <div className="flex flex-col lg:flex-row gap-4 h-[calc(100vh-5rem)]">
      {/* ═══ LEFT PANEL (~40%) ═══ */}
      <div className="w-full lg:w-[40%] flex flex-col gap-3 overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between flex-shrink-0">
          <div>
            <h1 className="text-xl font-extrabold text-[#1a3a5c]">مركز القيادة</h1>
            <p className="text-xs text-gray-500 mt-0.5">
              رحلة المرضى — من الوصول حتى إنهاء الحساب
            </p>
          </div>
          <button
            onClick={() => { loadJourney(); refetchSummary(); }}
            className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
          >
            <RefreshCw className="w-3.5 h-3.5" />
            تحديث
          </button>
        </div>

        {/* Workflow Navigation */}
        <WorkflowNav
          links={[
            WORKFLOW_LINKS.backToDailyOps(),
            WORKFLOW_LINKS.clinicQueue(),
            WORKFLOW_LINKS.payments(),
            WORKFLOW_LINKS.invoices(),
          ]}
          currentPage="/patient-journey"
        />

        {/* Summary Cards */}
        <div className="grid grid-cols-3 md:grid-cols-6 gap-2 flex-shrink-0">
          {summaryCards.map((card) => (
            <div
              key={card.label}
              className="rounded-xl border border-gray-200 bg-white p-2.5 flex items-center gap-2"
            >
              <div
                className={cn(
                  "w-8 h-8 rounded-lg flex items-center justify-center flex-shrink-0",
                  card.color
                )}
              >
                <card.icon className="w-4 h-4 text-white" />
              </div>
              <div>
                <p className="text-lg font-extrabold text-gray-900 leading-tight">{card.count}</p>
                <p className="text-[9px] text-gray-500 whitespace-nowrap leading-tight">{card.label}</p>
              </div>
            </div>
          ))}
        </div>

        {/* Filters */}
        <div className="flex flex-wrap items-end gap-2 bg-gray-50 rounded-xl border border-gray-200 p-3 flex-shrink-0">
          <div className="flex items-center gap-1 text-xs font-semibold text-gray-700">
            <Filter className="w-3.5 h-3.5" />
            تصفية
          </div>
          <div>
            <label className="block text-[10px] font-medium text-gray-600 mb-0.5">التاريخ</label>
            <input
              type="date"
              value={filterDate}
              onChange={(e) => setFilterDate(e.target.value)}
              className="w-full px-2 py-1 text-xs rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-1 focus:ring-[#3d7ab5]"
              dir="ltr"
            />
          </div>
          <div>
            <label className="block text-[10px] font-medium text-gray-600 mb-0.5">الحالة</label>
            <select
              value={filterStatus}
              onChange={(e) => setFilterStatus(e.target.value)}
              className="w-full px-2 py-1 text-xs rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-1 focus:ring-[#3d7ab5]"
            >
              <option value="">الكل</option>
              {Object.entries(STATUS_LABELS).map(([k, v]) => (
                <option key={k} value={k}>{v}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-[10px] font-medium text-gray-600 mb-0.5">الخدمة</label>
            <select
              value={filterService}
              onChange={(e) => setFilterService(e.target.value)}
              className="w-full px-2 py-1 text-xs rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-1 focus:ring-[#3d7ab5]"
            >
              <option value="">الكل</option>
              {services.map((s) => (
                <option key={s.id} value={s.id}>{s.arabicName}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-[10px] font-medium text-gray-600 mb-0.5">الغرفة</label>
            <select
              value={filterRoom}
              onChange={(e) => setFilterRoom(e.target.value)}
              className="w-full px-2 py-1 text-xs rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-1 focus:ring-[#3d7ab5]"
            >
              <option value="">الكل</option>
              {rooms.map((r) => (
                <option key={r.id} value={r.id}>{r.arabicName}</option>
              ))}
            </select>
          </div>
        </div>

        {/* Error */}
        {error && (
          <div className="text-xs text-red-600 bg-red-50 px-3 py-2 rounded-lg flex-shrink-0">{error}</div>
        )}

        {/* Loading */}
        {loading && (
          <div className="animate-pulse space-y-2 flex-1">
            {Array.from({ length: 6 }).map((_, i) => (
              <div key={i} className="h-16 bg-gray-100 rounded-lg" />
            ))}
          </div>
        )}

        {/* Patient Card List */}
        {!loading && items.length > 0 && (
          <div className="flex-1 overflow-y-auto space-y-1.5 min-h-0 pr-1">
            {items.map((item) => {
              const isSelected = item.patientId === selectedPatientId;
              return (
                <button
                  key={item.appointmentId}
                  onClick={() => setSelectedPatientId(item.patientId)}
                  className={cn(
                    "w-full text-start rounded-xl border p-3 transition-all",
                    isSelected
                      ? "border-[#3d7ab5] bg-[#f0f6fc] ring-1 ring-[#3d7ab5]/20"
                      : "border-gray-200 bg-white hover:bg-gray-50 hover:border-gray-300"
                  )}
                >
                  <div className="flex items-start justify-between gap-2">
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-1.5">
                        <span className={cn(
                          "text-sm font-bold truncate",
                          isSelected ? "text-[#1a3a5c]" : "text-gray-900"
                        )}>
                          {item.patientName}
                        </span>
                        <Link
                          href={`/patient-journey/${item.patientId}`}
                          onClick={(e) => e.stopPropagation()}
                          className="text-[#3d7ab5] hover:text-[#2d5e8e] flex-shrink-0"
                          title="عرض صفحة المريض الكاملة"
                        >
                          <ExternalLink className="w-3 h-3" />
                        </Link>
                      </div>
                      <div className="flex items-center gap-2 mt-0.5">
                        <span className="text-[11px] text-gray-500 font-mono" dir="ltr">
                          {item.appointmentTime}
                        </span>
                        <span className="text-gray-300">|</span>
                        <span className="text-[11px] text-gray-600">{item.doctorName}</span>
                      </div>
                    </div>
                    <div className="flex flex-col items-end gap-1 flex-shrink-0">
                      <span
                        className={cn(
                          "text-[10px] px-2 py-0.5 rounded-full font-medium",
                          STATUS_COLORS[item.appointmentStatus] ?? "bg-gray-50 text-gray-700"
                        )}
                      >
                        {STATUS_LABELS[item.appointmentStatus] ?? item.appointmentStatus}
                      </span>
                      {item.nextAction && item.nextAction !== "None" && item.nextAction !== "InProgress" && (
                        <span
                          className={cn(
                            "text-[9px] px-1.5 py-0.5 rounded-full font-medium text-white",
                            ACTION_COLORS[item.nextAction] ?? "bg-gray-300"
                          )}
                        >
                          {ACTION_LABELS[item.nextAction] ?? item.nextAction}
                        </span>
                      )}
                      {item.nextAction === "InProgress" && (
                        <span className="text-[9px] px-1.5 py-0.5 rounded-full font-medium bg-emerald-100 text-emerald-700 flex items-center gap-0.5">
                          <Stethoscope className="w-2.5 h-2.5" />
                          عند الطبيب
                        </span>
                      )}
                    </div>
                  </div>
                </button>
              );
            })}
          </div>
        )}

        {/* Empty State for List */}
        {!loading && items.length === 0 && (
          <div className="text-center py-12 flex-1 flex flex-col items-center justify-center">
            <Users className="w-10 h-10 text-gray-300 mx-auto mb-2" />
            <p className="text-gray-500 font-medium text-sm">لا توجد رحلات مرضى لهذا اليوم</p>
            <p className="text-xs text-gray-400 mt-1">اختر تاريخًا آخر أو أضف مواعيد</p>
          </div>
        )}
      </div>

      {/* ═══ RIGHT PANEL (~60%) ═══ */}
      <div className="w-full lg:w-[60%] flex flex-col bg-white rounded-2xl border border-gray-200 overflow-hidden">
        {/* No patient selected or list empty */}
        {!selectedPatientId && !loading && (
          <div className="flex-1 flex flex-col items-center justify-center text-center p-8">
            <Route className="w-14 h-14 text-gray-200 mb-3" />
            <p className="text-gray-500 font-bold">اختر مريضًا من القائمة</p>
            <p className="text-xs text-gray-400 mt-1">سيتم عرض تفاصيل رحلة المريض هنا</p>
          </div>
        )}

        {/* Loading Summary */}
        {selectedPatientId && summaryLoading && (
          <div className="flex-1 flex flex-col items-center justify-center p-8">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-[#3d7ab5] mb-3" />
            <p className="text-sm text-gray-500">جارٍ تحميل بيانات المريض...</p>
          </div>
        )}

        {/* Error loading summary */}
        {selectedPatientId && summaryError && !summaryLoading && (
          <div className="flex-1 flex flex-col items-center justify-center p-8">
            <AlertTriangle className="w-10 h-10 text-[#ba7517] mb-3" />
            <p className="text-sm font-bold text-[#1a3a5c]">فشل تحميل البيانات</p>
            <p className="text-xs text-gray-500 mt-1">
              {(summaryError as Error)?.message ?? "ليس لديك صلاحية أو حدث خطأ"}
            </p>
            <button
              onClick={() => refetchSummary()}
              className="mt-3 px-3 py-1.5 text-xs font-semibold rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] transition"
            >
              إعادة المحاولة
            </button>
          </div>
        )}

        {/* ═══ Patient Detail Content ═══ */}
        {selectedPatientId && summary && !summaryLoading && (
          <div className="flex-1 overflow-y-auto p-4 space-y-4">
            {/* ─── 1. Patient Header Card ─── */}
            <PatientHeaderCard summary={summary} isDoctor={isDoctor} />

            {/* ─── 2. Today's Appointment ─── */}
            {summary.todayAppointment && (
              <div>
                <TodaysAppointmentCard summary={summary} />
                {/* Phase 1a: Cancel / NoShow Buttons */}
                {canEditJourney && ["Scheduled", "Confirmed"].includes(summary.todayAppointment.status) && (
                  <div className="flex gap-2 mt-2">
                    <button
                      onClick={() => { setCancelDialogType("Cancelled"); setCancelDialogOpen(true); }}
                      className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-bold rounded-lg bg-red-50 text-red-700 border border-red-200 hover:bg-red-100 transition"
                    >
                      <Ban className="w-3 h-3" />
                      إلغاء الموعد
                    </button>
                    <button
                      onClick={() => { setCancelDialogType("NoShow"); setCancelDialogOpen(true); }}
                      className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-bold rounded-lg bg-orange-50 text-orange-700 border border-orange-200 hover:bg-orange-100 transition"
                    >
                      <UserX className="w-3 h-3" />
                      لم يحضر
                    </button>
                  </div>
                )}
              </div>
            )}

            {/* ─── 3. Queue Status (Enhanced) ─── */}
            {summary.queueStatus && (
              <div>
                <QueueStatusCard summary={summary} />
                {/* Phase 1b: Cancel from Queue */}
                {canEditJourney && ["Waiting", "Called"].includes(summary.queueStatus.status) && (
                  <div className="flex flex-wrap items-center gap-2 mt-2">
                    <button
                      onClick={handleRequestCancelQueue}
                      className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-bold rounded-lg bg-red-50 text-red-700 border border-red-200 hover:bg-red-100 transition"
                    >
                      <Ban className="w-3 h-3" />
                      إلغاء من الطابور
                    </button>
                    {/* Phase 1c: Change Room */}
                    <div className="flex items-center gap-1.5">
                      <ArrowRightLeft className="w-3 h-3 text-gray-500" />
                      <select
                        value={changeRoomSelectedRoom}
                        onChange={(e) => {
                          if (e.target.value) handleRequestChangeRoom(e.target.value);
                        }}
                        disabled={changeRoomMutation.isPending}
                        className="px-2 py-1.5 text-[11px] rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-1 focus:ring-[#3d7ab5] disabled:opacity-50"
                      >
                        <option value="">تغيير الغرفة</option>
                        {rooms.map((r) => (
                          <option key={r.id} value={r.id}>{r.arabicName}</option>
                        ))}
                      </select>
                    </div>
                  </div>
                )}
                {/* Phase 1d: Estimated Wait Time */}
                {summary.queueStatus.status === "Waiting" && estimatedWait && (
                  <div className="mt-2 flex items-center gap-2 text-[11px] text-[#1a3a5c] bg-blue-50 border border-blue-100 rounded-lg px-3 py-1.5">
                    <Timer className="w-3.5 h-3.5 text-[#3d7ab5]" />
                    <span>الوقت المتوقع: <strong>{estimatedWait.estimatedMinutes} دقيقة</strong></span>
                    <span className="text-gray-300">|</span>
                    <span>الترتيب: <strong>{estimatedWait.position}</strong></span>
                  </div>
                )}
              </div>
            )}

            {/* ─── 4. Medical Safety Alerts ─── */}
            {summary.medicalAlerts && summary.medicalAlerts.length > 0 && !isAccountant && (
              <MedicalAlertsCard alerts={summary.medicalAlerts} />
            )}

            {/* ─── 5. Journey Actions Panel ─── */}
            {canEditJourney && summary.nextAction && summary.nextAction !== "None" && selectedItem && (
              <JourneyActionsPanel
                summary={summary}
                selectedItem={selectedItem}
                isDoctor={isDoctor}
                services={services}
                rooms={rooms}
                intakeService={intakeService}
                setIntakeService={setIntakeService}
                intakeComplaint={intakeComplaint}
                setIntakeComplaint={setIntakeComplaint}
                intakeRoom={intakeRoom}
                setIntakeRoom={setIntakeRoom}
                intakeConsultFee={intakeConsultFee}
                setIntakeConsultFee={setIntakeConsultFee}
                intakeConsultAmount={intakeConsultAmount}
                setIntakeConsultAmount={setIntakeConsultAmount}
                intakeNotes={intakeNotes}
                setIntakeNotes={setIntakeNotes}
                handoffForm={handoffForm}
                setHandoffForm={setHandoffForm}
                checkoutAmount={checkoutAmount}
                setCheckoutAmount={setCheckoutAmount}
                checkoutPayment={checkoutPayment}
                setCheckoutPayment={setCheckoutPayment}
                checkoutNextDate={checkoutNextDate}
                setCheckoutNextDate={setCheckoutNextDate}
                checkoutNextService={checkoutNextService}
                setCheckoutNextService={setCheckoutNextService}
                checkoutNotes={checkoutNotes}
                setCheckoutNotes={setCheckoutNotes}
                checkoutSuccess={checkoutSuccess}
                draftInvoiceResult={draftInvoiceResult}
                intakeMutation={intakeMutation}
                sendToQueueMutation={sendToQueueMutation}
                startVisitMutation={startVisitMutation}
                handoffMutation={handoffMutation}
                checkoutMutation={checkoutMutation}
                draftInvoiceMutation={draftInvoiceMutation}
                onSimpleAction={handleSimpleAction}
                onIntakeSubmit={handleIntakeSubmit}
                onHandoffSubmit={handleHandoffSubmit}
                onCheckoutSubmit={handleCheckoutSubmit}
                onCreateDraftInvoice={handleCreateDraftInvoice}
                onIssueInvoice={handleIssueInvoice}
                issueInvoiceMutation={issueInvoiceMutation}
                onDownloadPdf={handleDownloadPdf}
              />
            )}

            {/* ─── 6. Today's Visit ─── */}
            {summary.todayVisit && canViewVisits && (
              <div>
                <TodaysVisitCard summary={summary} isReception={isReception} isAccountant={isAccountant} />
                {/* Phase 4b: Edit Visit Notes - Doctors only */}
                {canEditJourney && isDoctor && summary.todayVisit.id && (
                  <button
                    onClick={() => {
                      setEditVisitForm({
                        chiefComplaint: summary.todayVisit?.chiefComplaint ?? "",
                        clinicalNotes: summary.todayVisit?.clinicalNotes ?? "",
                        treatmentDone: summary.todayVisit?.treatmentDone ?? "",
                        diagnosis: summary.todayVisit?.diagnosis ?? "",
                        instructions: summary.todayVisit?.instructions ?? "",
                        nextVisitPlan: summary.todayVisit?.nextVisitPlan ?? "",
                        cost: summary.todayVisit?.cost ?? 0,
                      });
                      setEditVisitModalOpen(true);
                    }}
                    className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-bold rounded-lg bg-[#3d7ab5]/10 text-[#3d7ab5] border border-[#3d7ab5]/20 hover:bg-[#3d7ab5]/20 transition mt-2"
                  >
                    <Stethoscope className="w-3 h-3" />
                    تعديل ملاحظات الزيارة
                  </button>
                )}
              </div>
            )}

            {/* ─── 7. Finance Summary ─── */}
            {(canViewFinance || canViewCheckout) && !isDoctor && summary.financeSummary && (
              <div>
                <FinanceSummaryCard summary={summary} isAccountant={isAccountant} canViewFinance={canViewFinance} canViewInvoices={canViewInvoices} />
                {/* Phase 2a: Record Payment Button */}
                {canEditJourney && (canViewFinance || canViewCheckout) && (
                  <button
                    onClick={() => {
                      setPaymentAmount(summary.financeSummary?.outstandingBalance ?? 0);
                      setRecordPaymentOpen(true);
                    }}
                    className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-bold rounded-lg bg-green-50 text-green-700 border border-green-200 hover:bg-green-100 transition mt-2"
                  >
                    <CreditCard className="w-3 h-3" />
                    تسجيل دفعة
                  </button>
                )}
                {/* Phase 2c: Download Payment Receipt PDF */}
                {summary.financeSummary?.latestPayment && canViewFinance && (
                  <button
                    onClick={() => handleDownloadPdf(`/api/payments/${summary.financeSummary!.latestPayment!.id}/pdf`, `receipt-${summary.financeSummary!.latestPayment!.receiptNumber ?? summary.financeSummary!.latestPayment!.id}.pdf`)}
                    disabled={pdfDownloading}
                    className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-bold rounded-lg bg-gray-50 text-gray-600 border border-gray-200 hover:bg-gray-100 transition mt-1 mr-2 disabled:opacity-50"
                  >
                    {pdfDownloading ? <RefreshCw className="w-3 h-3 animate-spin" /> : <Download className="w-3 h-3" />}
                    {pdfDownloading ? "جارٍ التحميل..." : "تحميل إيصال الدفعة"}
                  </button>
                )}
              </div>
            )}

            {/* ─── 8. Active Ortho Case ─── */}
            {summary.activeOrthoCase && canViewVisits && !isAccountant && (
              <ActiveOrthoCard summary={summary} />
            )}

            {/* ─── 9. Recent Visits ─── */}
            {summary.recentVisits && summary.recentVisits.length > 0 && canViewVisits && !isAccountant && (
              <RecentVisitsCard visits={summary.recentVisits.slice(0, 3)} isReception={isReception} isAccountant={isAccountant} />
            )}

            {/* ─── 10. Timeline ─── */}
            {summary.timeline && summary.timeline.length > 0 && canViewJourney && (
              <TimelineCard events={summary.timeline.slice(0, 5)} />
            )}

            {/* ─── 11. Quick Actions Bar ─── */}
            {canEditJourney && (
              <div className="rounded-xl border border-[#3d7ab5]/20 bg-[#f0f6fc] p-3">
                <div className="flex items-center gap-2 mb-2">
                  <CircleDot className="w-4 h-4 text-[#f5922e]" />
                  <span className="text-sm font-bold text-[#1a3a5c]">إجراءات سريعة</span>
                </div>
                <div className="flex flex-wrap gap-2">
                  {/* SMS */}
                  <button
                    onClick={() => {
                      setSmsTo(summary.patient.phone ?? "");
                      setSmsModalOpen(true);
                    }}
                    className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-bold rounded-lg bg-white text-[#3d7ab5] border border-[#3d7ab5]/20 hover:bg-[#3d7ab5]/10 transition"
                  >
                    <MessageSquare className="w-3 h-3" />
                    إرسال رسالة
                  </button>
                  {/* Appointment Reminder */}
                  {summary.todayAppointment && (
                    <button
                      onClick={handleSendReminder}
                      disabled={sendReminderMutation.isPending}
                      className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-bold rounded-lg bg-white text-amber-700 border border-amber-200 hover:bg-amber-50 transition disabled:opacity-50"
                    >
                      <Megaphone className="w-3 h-3" />
                      {sendReminderMutation.isPending ? "جارٍ..." : "تذكير بالموعد"}
                    </button>
                  )}
                  {/* WhatsApp */}
                  <button
                    onClick={handleOpenWhatsApp}
                    className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-bold rounded-lg bg-white text-green-700 border border-green-200 hover:bg-green-50 transition"
                  >
                    <MessageCircle className="w-3 h-3" />
                    واتساب
                  </button>
                  {/* Prescription - Doctors only */}
                  {isDoctor && (
                    <button
                      onClick={() => setPrescriptionModalOpen(true)}
                      className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-bold rounded-lg bg-white text-purple-700 border border-purple-200 hover:bg-purple-50 transition"
                    >
                      <Pill className="w-3 h-3" />
                      وصفة طبية
                    </button>
                  )}
                  {/* Edit Visit - Doctors only */}
                  {isDoctor && summary.todayVisit?.id && (
                    <button
                      onClick={() => {
                        setEditVisitForm({
                          chiefComplaint: summary.todayVisit?.chiefComplaint ?? "",
                          clinicalNotes: summary.todayVisit?.clinicalNotes ?? "",
                          treatmentDone: summary.todayVisit?.treatmentDone ?? "",
                          diagnosis: summary.todayVisit?.diagnosis ?? "",
                          instructions: summary.todayVisit?.instructions ?? "",
                          nextVisitPlan: summary.todayVisit?.nextVisitPlan ?? "",
                          cost: summary.todayVisit?.cost ?? 0,
                        });
                        setEditVisitModalOpen(true);
                      }}
                      className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-bold rounded-lg bg-white text-[#3d7ab5] border border-[#3d7ab5]/20 hover:bg-[#3d7ab5]/10 transition"
                    >
                      <Stethoscope className="w-3 h-3" />
                      تعديل الزيارة
                    </button>
                  )}
                  {/* Book Appointment */}
                  <button
                    onClick={() => {
                      setBookAptDoctorId(selectedItem?.doctorId ?? "");
                      setBookAptServiceId(selectedItem?.serviceId ?? "");
                      setBookAppointmentModalOpen(true);
                    }}
                    className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-bold rounded-lg bg-white text-[#1a3a5c] border border-gray-200 hover:bg-gray-50 transition"
                  >
                    <CalendarDays className="w-3 h-3" />
                    حجز موعد
                  </button>
                  {/* Print */}
                  <button
                    onClick={() => window.print()}
                    className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-bold rounded-lg bg-white text-gray-600 border border-gray-200 hover:bg-gray-50 transition"
                  >
                    <Printer className="w-3 h-3" />
                    طباعة
                  </button>
                </div>
              </div>
            )}
          </div>
        )}
      </div>

      {/* ═══ MODAL OVERLAYS ═══ */}

      {/* ─── Unified Confirmation Dialog ─── */}
      {cancelDialogOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md mx-4">
            <div className="flex items-center justify-between p-4 border-b">
              <h3 className="text-sm font-bold text-[#1a3a5c]">
                {cancelDialogType === "Cancelled" ? "تأكيد إلغاء الموعد"
                  : cancelDialogType === "NoShow" ? "تأكيد تسجيل عدم الحضور"
                  : cancelDialogType === "CancelQueue" ? "تأكيد الإلغاء من الطابور"
                  : "تأكيد تغيير الغرفة"}
              </h3>
              <button onClick={() => { setCancelDialogOpen(false); setChangeRoomSelectedRoom(""); }} className="p-1 rounded-lg hover:bg-gray-100 transition">
                <X className="w-4 h-4 text-gray-500" />
              </button>
            </div>
            <div className="p-4 space-y-2">
              {/* Patient name display */}
              <div className="flex items-center gap-2 bg-gray-50 rounded-lg px-3 py-2">
                <span className="text-[11px] text-gray-500">المريض:</span>
                <span className="text-sm font-bold text-[#1a3a5c]">{summary?.patient.fullName}</span>
              </div>
              <p className="text-sm text-gray-700">
                {cancelDialogType === "Cancelled"
                  ? "هل أنت متأكد من إلغاء هذا الموعد؟ لا يمكن التراجع عن هذا الإجراء."
                  : cancelDialogType === "NoShow"
                  ? "هل أنت متأكد من تسجيل عدم الحضور؟ سيتم تغيير حالة الموعد."
                  : cancelDialogType === "CancelQueue"
                  ? "هل أنت متأكد من إلغاء المريض من الطابور؟ سيتم إخراجه من قائمة الانتظار."
                  : `هل أنت متأكد من نقل المريض إلى غرفة "${pendingChangeRoomName}"؟`}
              </p>
            </div>
            <div className="flex gap-2 justify-end p-4 border-t">
              <button
                onClick={() => { setCancelDialogOpen(false); setChangeRoomSelectedRoom(""); }}
                className="px-4 py-2 text-xs font-bold rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
              >
                إلغاء
              </button>
              <button
                onClick={handleConfirmAction}
                disabled={updateAptStatusMutation.isPending || cancelQueueMutation.isPending || changeRoomMutation.isPending}
                className={cn(
                  "px-4 py-2 text-xs font-bold rounded-lg text-white transition disabled:opacity-50",
                  cancelDialogType === "Cancelled" || cancelDialogType === "CancelQueue" ? "bg-red-600 hover:bg-red-700"
                  : cancelDialogType === "NoShow" ? "bg-orange-600 hover:bg-orange-700"
                  : "bg-[#3d7ab5] hover:bg-[#2d5e8e]"
                )}
              >
                {(updateAptStatusMutation.isPending || cancelQueueMutation.isPending || changeRoomMutation.isPending) ? "جارٍ..." : "تأكيد"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ─── Record Payment Modal ─── */}
      {recordPaymentOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg mx-4 max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between p-4 border-b">
              <h3 className="text-sm font-bold text-[#1a3a5c]">تسجيل دفعة</h3>
              <button onClick={() => setRecordPaymentOpen(false)} className="p-1 rounded-lg hover:bg-gray-100 transition">
                <X className="w-4 h-4 text-gray-500" />
              </button>
            </div>
            <form onSubmit={handleRecordPayment} className="p-4 space-y-3">
              {/* Outstanding balance reference */}
              {summary?.financeSummary && summary.financeSummary.outstandingBalance > 0 && (
                <div className="flex items-center justify-between bg-amber-50 border border-amber-200 rounded-lg px-3 py-2">
                  <span className="text-[10px] text-amber-700 font-semibold">الرصيد المستحق</span>
                  <span className="text-sm font-bold text-amber-800">{fmtRial(summary.financeSummary.outstandingBalance)}</span>
                </div>
              )}
              <div>
                <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">المبلغ</label>
                <input
                  type="number"
                  value={paymentAmount}
                  onChange={(e) => setPaymentAmount(parseInt(e.target.value) || 0)}
                  className={inputCls}
                  dir="ltr"
                  min={1}
                  required
                />
              </div>
              <div>
                <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">طريقة الدفع</label>
                <select value={paymentMethod} onChange={(e) => setPaymentMethod(e.target.value)} className={inputCls}>
                  {PAYMENT_METHODS.map((m) => (
                    <option key={m.value} value={m.value}>{m.label}</option>
                  ))}
                </select>
              </div>
              <div>
                <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">وصف الخدمة</label>
                <input
                  value={paymentServiceDesc}
                  onChange={(e) => setPaymentServiceDesc(e.target.value)}
                  className={inputCls}
                  placeholder="مثل: رسوم معاينة"
                />
              </div>
              <div>
                <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">ملاحظات</label>
                <textarea
                  value={paymentNotes}
                  onChange={(e) => setPaymentNotes(e.target.value)}
                  className={cn(inputCls, "h-16 resize-none")}
                />
              </div>
              <div className="flex gap-2 justify-end pt-2 border-t">
                <button
                  type="button"
                  onClick={() => setRecordPaymentOpen(false)}
                  className="px-4 py-2 text-xs font-bold rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
                >
                  إلغاء
                </button>
                <button
                  type="submit"
                  disabled={createPaymentMutation.isPending}
                  className="px-4 py-2 text-xs font-bold rounded-lg bg-green-600 text-white hover:bg-green-700 transition disabled:opacity-50"
                >
                  {createPaymentMutation.isPending ? "جارٍ..." : "تسجيل الدفعة"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ─── Send SMS Modal ─── */}
      {smsModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg mx-4 max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between p-4 border-b">
              <h3 className="text-sm font-bold text-[#1a3a5c]">إرسال رسالة SMS</h3>
              <button onClick={() => setSmsModalOpen(false)} className="p-1 rounded-lg hover:bg-gray-100 transition">
                <X className="w-4 h-4 text-gray-500" />
              </button>
            </div>
            <form onSubmit={handleSendSms} className="p-4 space-y-3">
              <div>
                <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">رقم الهاتف</label>
                <input
                  value={smsTo}
                  onChange={(e) => setSmsTo(e.target.value)}
                  className={inputCls}
                  dir="ltr"
                  required
                />
              </div>
              <div>
                <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">الرسالة</label>
                <textarea
                  value={smsMessage}
                  onChange={(e) => setSmsMessage(e.target.value)}
                  className={cn(inputCls, "h-28 resize-none")}
                  placeholder="اكتب الرسالة هنا..."
                  required
                />
              </div>
              <div className="flex gap-2 justify-end pt-2 border-t">
                <button
                  type="button"
                  onClick={() => setSmsModalOpen(false)}
                  className="px-4 py-2 text-xs font-bold rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
                >
                  إلغاء
                </button>
                <button
                  type="submit"
                  disabled={sendSmsMutation.isPending}
                  className="flex items-center gap-1 px-4 py-2 text-xs font-bold rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] transition disabled:opacity-50"
                >
                  <Send className="w-3 h-3" />
                  {sendSmsMutation.isPending ? "جارٍ الإرسال..." : "إرسال"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ─── Quick Prescription Modal ─── */}
      {prescriptionModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-2xl mx-4 max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between p-4 border-b">
              <h3 className="text-sm font-bold text-[#1a3a5c]">وصفة طبية</h3>
              <button onClick={() => setPrescriptionModalOpen(false)} className="p-1 rounded-lg hover:bg-gray-100 transition">
                <X className="w-4 h-4 text-gray-500" />
              </button>
            </div>
            <form onSubmit={handleCreatePrescription} className="p-4 space-y-3">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                <div>
                  <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">التشخيص</label>
                  <input
                    value={prescDiagnosis}
                    onChange={(e) => setPrescDiagnosis(e.target.value)}
                    className={inputCls}
                  />
                </div>
                <div>
                  <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">ملاحظات</label>
                  <input
                    value={prescNotes}
                    onChange={(e) => setPrescNotes(e.target.value)}
                    className={inputCls}
                  />
                </div>
              </div>

              {/* Medication Items */}
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <span className="text-xs font-bold text-[#1a3a5c]">الأدوية</span>
                  <button
                    type="button"
                    onClick={() => setPrescItems([...prescItems, { medicationName: "", dosage: "", frequency: "", duration: "", notes: "" }])}
                    className="flex items-center gap-1 px-2 py-1 text-[10px] rounded-lg bg-[#3d7ab5]/10 text-[#3d7ab5] hover:bg-[#3d7ab5]/20 transition"
                  >
                    <Plus className="w-3 h-3" />
                    إضافة دواء
                  </button>
                </div>
                {prescItems.map((item, idx) => (
                  <div key={idx} className="border border-gray-200 rounded-lg p-2.5 space-y-2 bg-gray-50/50">
                    <div className="flex items-center justify-between">
                      <span className="text-[10px] font-bold text-gray-500">دواء {idx + 1}</span>
                      {prescItems.length > 1 && (
                        <button
                          type="button"
                          onClick={() => setPrescItems(prescItems.filter((_, i) => i !== idx))}
                          className="p-1 rounded hover:bg-red-50 transition"
                        >
                          <Trash2 className="w-3 h-3 text-red-500" />
                        </button>
                      )}
                    </div>
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                      <div>
                        <label className="block text-[9px] font-medium text-gray-500">اسم الدواء *</label>
                        <input
                          value={item.medicationName}
                          onChange={(e) => {
                            const updated = [...prescItems];
                            updated[idx] = { ...updated[idx], medicationName: e.target.value };
                            setPrescItems(updated);
                          }}
                          className={inputCls}
                          required
                        />
                      </div>
                      <div>
                        <label className="block text-[9px] font-medium text-gray-500">الجرعة</label>
                        <input
                          value={item.dosage}
                          onChange={(e) => {
                            const updated = [...prescItems];
                            updated[idx] = { ...updated[idx], dosage: e.target.value };
                            setPrescItems(updated);
                          }}
                          className={inputCls}
                          placeholder="مثل: 500mg"
                        />
                      </div>
                      <div>
                        <label className="block text-[9px] font-medium text-gray-500">التكرار</label>
                        <input
                          value={item.frequency}
                          onChange={(e) => {
                            const updated = [...prescItems];
                            updated[idx] = { ...updated[idx], frequency: e.target.value };
                            setPrescItems(updated);
                          }}
                          className={inputCls}
                          placeholder="مثل: مرتين يومياً"
                        />
                      </div>
                      <div>
                        <label className="block text-[9px] font-medium text-gray-500">المدة</label>
                        <input
                          value={item.duration}
                          onChange={(e) => {
                            const updated = [...prescItems];
                            updated[idx] = { ...updated[idx], duration: e.target.value };
                            setPrescItems(updated);
                          }}
                          className={inputCls}
                          placeholder="مثل: 7 أيام"
                        />
                      </div>
                      <div className="sm:col-span-2">
                        <label className="block text-[9px] font-medium text-gray-500">ملاحظات</label>
                        <input
                          value={item.notes}
                          onChange={(e) => {
                            const updated = [...prescItems];
                            updated[idx] = { ...updated[idx], notes: e.target.value };
                            setPrescItems(updated);
                          }}
                          className={inputCls}
                        />
                      </div>
                    </div>
                  </div>
                ))}
              </div>

              <div className="flex gap-2 justify-end pt-2 border-t">
                <button
                  type="button"
                  onClick={() => setPrescriptionModalOpen(false)}
                  className="px-4 py-2 text-xs font-bold rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
                >
                  إلغاء
                </button>
                <button
                  type="submit"
                  disabled={createPrescriptionMutation.isPending}
                  className="flex items-center gap-1 px-4 py-2 text-xs font-bold rounded-lg bg-purple-600 text-white hover:bg-purple-700 transition disabled:opacity-50"
                >
                  <Pill className="w-3 h-3" />
                  {createPrescriptionMutation.isPending ? "جارٍ..." : "إنشاء وصفة"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ─── Edit Visit Notes Modal ─── */}
      {editVisitModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-2xl mx-4 max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between p-4 border-b">
              <h3 className="text-sm font-bold text-[#1a3a5c]">تعديل ملاحظات الزيارة</h3>
              <button onClick={() => setEditVisitModalOpen(false)} className="p-1 rounded-lg hover:bg-gray-100 transition">
                <X className="w-4 h-4 text-gray-500" />
              </button>
            </div>
            <form onSubmit={handleUpdateVisit} className="p-4 space-y-3">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                <div>
                  <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">الشكوى الرئيسية</label>
                  <textarea
                    value={editVisitForm.chiefComplaint}
                    onChange={(e) => setEditVisitForm({ ...editVisitForm, chiefComplaint: e.target.value })}
                    className={cn(inputCls, "h-16 resize-none")}
                  />
                </div>
                <div>
                  <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">ملاحظات سريرية</label>
                  <textarea
                    value={editVisitForm.clinicalNotes}
                    onChange={(e) => setEditVisitForm({ ...editVisitForm, clinicalNotes: e.target.value })}
                    className={cn(inputCls, "h-16 resize-none")}
                  />
                </div>
                <div>
                  <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">ما تم عمله</label>
                  <textarea
                    value={editVisitForm.treatmentDone}
                    onChange={(e) => setEditVisitForm({ ...editVisitForm, treatmentDone: e.target.value })}
                    className={cn(inputCls, "h-16 resize-none")}
                  />
                </div>
                <div>
                  <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">التشخيص</label>
                  <input
                    value={editVisitForm.diagnosis}
                    onChange={(e) => setEditVisitForm({ ...editVisitForm, diagnosis: e.target.value })}
                    className={inputCls}
                  />
                </div>
                <div>
                  <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">تعليمات للمريض</label>
                  <textarea
                    value={editVisitForm.instructions}
                    onChange={(e) => setEditVisitForm({ ...editVisitForm, instructions: e.target.value })}
                    className={cn(inputCls, "h-16 resize-none")}
                  />
                </div>
                <div>
                  <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">خطة الزيارة القادمة</label>
                  <textarea
                    value={editVisitForm.nextVisitPlan}
                    onChange={(e) => setEditVisitForm({ ...editVisitForm, nextVisitPlan: e.target.value })}
                    className={cn(inputCls, "h-16 resize-none")}
                  />
                </div>
                <div>
                  <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">التكلفة</label>
                  <input
                    type="number"
                    value={editVisitForm.cost}
                    onChange={(e) => setEditVisitForm({ ...editVisitForm, cost: parseInt(e.target.value) || 0 })}
                    className={inputCls}
                    dir="ltr"
                    min={0}
                  />
                </div>
              </div>
              <div className="flex gap-2 justify-end pt-2 border-t">
                <button
                  type="button"
                  onClick={() => setEditVisitModalOpen(false)}
                  className="px-4 py-2 text-xs font-bold rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
                >
                  إلغاء
                </button>
                <button
                  type="submit"
                  disabled={updateVisitMutation.isPending}
                  className="flex items-center gap-1 px-4 py-2 text-xs font-bold rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] transition disabled:opacity-50"
                >
                  <Save className="w-3 h-3" />
                  {updateVisitMutation.isPending ? "جارٍ الحفظ..." : "حفظ التعديلات"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ─── Book Next Appointment Modal ─── */}
      {bookAppointmentModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg mx-4 max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between p-4 border-b">
              <h3 className="text-sm font-bold text-[#1a3a5c]">حجز موعد قادم</h3>
              <button onClick={() => setBookAppointmentModalOpen(false)} className="p-1 rounded-lg hover:bg-gray-100 transition">
                <X className="w-4 h-4 text-gray-500" />
              </button>
            </div>
            <form onSubmit={handleBookAppointment} className="p-4 space-y-3">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                <div>
                  <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">التاريخ *</label>
                  <input
                    type="date"
                    value={bookAptDate}
                    onChange={(e) => setBookAptDate(e.target.value)}
                    className={inputCls}
                    dir="ltr"
                    required
                  />
                </div>
                <div>
                  <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">وقت البداية *</label>
                  <input
                    type="time"
                    value={bookAptStartTime}
                    onChange={(e) => setBookAptStartTime(e.target.value)}
                    className={inputCls}
                    dir="ltr"
                    required
                  />
                </div>
                <div>
                  <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">وقت النهاية</label>
                  <input
                    type="time"
                    value={bookAptEndTime}
                    onChange={(e) => setBookAptEndTime(e.target.value)}
                    className={inputCls}
                    dir="ltr"
                  />
                </div>
                <div>
                  <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">الطبيب</label>
                  <div className="flex items-center gap-2">
                    <input
                      value={selectedItem?.doctorName ?? ""}
                      className={cn(inputCls, "bg-gray-50")}
                      readOnly
                    />
                    <input type="hidden" value={bookAptDoctorId} />
                  </div>
                </div>
                <div>
                  <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">الخدمة</label>
                  <select
                    value={bookAptServiceId}
                    onChange={(e) => setBookAptServiceId(e.target.value)}
                    className={inputCls}
                  >
                    <option value="">— اختر خدمة —</option>
                    {services.map((s) => (
                      <option key={s.id} value={s.id}>{s.arabicName}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">نوع الموعد</label>
                  <select
                    value={bookAptType}
                    onChange={(e) => setBookAptType(e.target.value)}
                    className={inputCls}
                  >
                    <option value="FollowUp">متابعة</option>
                    <option value="Consultation">معاينة</option>
                    <option value="Procedure">إجراء</option>
                    <option value="Emergency">طوارئ</option>
                  </select>
                </div>
              </div>
              <div>
                <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">ملاحظات</label>
                <textarea
                  value={bookAptNotes}
                  onChange={(e) => setBookAptNotes(e.target.value)}
                  className={cn(inputCls, "h-16 resize-none")}
                />
              </div>
              <div className="flex gap-2 justify-end pt-2 border-t">
                <button
                  type="button"
                  onClick={() => setBookAppointmentModalOpen(false)}
                  className="px-4 py-2 text-xs font-bold rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
                >
                  إلغاء
                </button>
                <button
                  type="submit"
                  disabled={createAppointmentMutation.isPending}
                  className="flex items-center gap-1 px-4 py-2 text-xs font-bold rounded-lg bg-[#1a3a5c] text-white hover:bg-[#2d5e8e] transition disabled:opacity-50"
                >
                  <CalendarDays className="w-3 h-3" />
                  {createAppointmentMutation.isPending ? "جارٍ الحجز..." : "حجز الموعد"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}

// ═══════════════════════════════════════════════════════════════════════════════
// SUB-COMPONENTS
// ═══════════════════════════════════════════════════════════════════════════════

// ─── 1. Patient Header Card ──────────────────────────────────────────────────

function PatientHeaderCard({ summary, isDoctor }: { summary: DailyJourneySummary; isDoctor: boolean }) {
  const { patient, journeyStep } = summary;
  const currentStepIdx = getStepIndex(journeyStep);

  return (
    <div className="bg-[#1a3a5c] rounded-xl p-4 text-white">
      <div className="flex items-start gap-3">
        <div className="w-12 h-12 rounded-full bg-[#3d7ab5] flex items-center justify-center text-lg font-bold flex-shrink-0">
          {getInitials(patient.fullName)}
        </div>
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <h2 className="text-[15px] font-bold truncate">{patient.fullName}</h2>
            <span className="text-[10px] font-mono text-white/60">#{patient.patientNumber}</span>
          </div>
          <div className="flex flex-wrap gap-3 mt-1.5">
            {patient.age != null && (
              <span className="text-[11px] text-white/80">{patient.age} سنة</span>
            )}
            {patient.gender && (
              <span className="text-[11px] text-white/80">
                {patient.gender === "Male" ? "ذكر" : patient.gender === "Female" ? "أنثى" : patient.gender}
              </span>
            )}
            {patient.phone && !isDoctor && (
              <span className="text-[11px] text-[#9fe1cb] flex items-center gap-1" dir="ltr">
                <Phone className="w-3 h-3" />
                {patient.phone}
              </span>
            )}
          </div>
        </div>
      </div>

      {/* Journey Step Progress Bar */}
      <div className="mt-4">
        <div className="flex items-center gap-0 overflow-x-auto pb-1">
          {JOURNEY_STEPS.map((step, idx) => {
            const status = getStepStatus(step.key, journeyStep);
            return (
              <div key={step.key} className="flex items-center">
                <div className="flex flex-col items-center gap-0.5 min-w-[52px]">
                  <div
                    className={cn(
                      "w-6 h-6 rounded-full flex items-center justify-center text-[10px] font-bold border-2 transition-all",
                      status === "done" && "bg-[#3d7ab5] text-white border-[#3d7ab5]",
                      status === "current" && "bg-white text-[#f5922e] border-[#f5922e] shadow-[0_0_8px_rgba(245,146,46,0.4)]",
                      status === "pending" && "bg-white/10 text-white/40 border-white/20"
                    )}
                  >
                    {status === "done" ? <CheckCircle2 className="w-3.5 h-3.5" /> : idx + 1}
                  </div>
                  <span
                    className={cn(
                      "text-[8px] text-center leading-tight",
                      status === "current" ? "text-[#f5922e] font-bold" : "text-white/50"
                    )}
                  >
                    {step.label}
                  </span>
                </div>
                {idx < JOURNEY_STEPS.length - 1 && (
                  <div
                    className={cn(
                      "flex-1 h-0.5 min-w-[8px]",
                      getStepIndex(step.key) < currentStepIdx ? "bg-[#3d7ab5]" : "bg-white/20"
                    )}
                  />
                )}
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}

// ─── 2. Today's Appointment ──────────────────────────────────────────────────

function TodaysAppointmentCard({ summary }: { summary: DailyJourneySummary }) {
  const apt = summary.todayAppointment!;

  return (
    <div className="bg-white rounded-xl border border-gray-200 p-4">
      <div className="flex items-center gap-2 mb-3">
        <CalendarDays className="w-4 h-4 text-[#3d7ab5]" />
        <span className="text-sm font-bold text-[#1a3a5c]">موعد اليوم</span>
        <span className={cn(
          "text-[10px] px-2 py-0.5 rounded-full font-medium mr-auto",
          STATUS_COLORS[apt.status] ?? "bg-gray-50 text-gray-700"
        )}>
          {STATUS_LABELS[apt.status] ?? apt.status}
        </span>
      </div>
      <div className="grid grid-cols-2 gap-x-4 gap-y-2 text-[12px]">
        <div>
          <span className="text-[10px] text-gray-500 font-semibold">الوقت</span>
          <p className="text-gray-800 font-medium" dir="ltr">
            {fmtTime(apt.startTime)}{apt.endTime ? ` — ${fmtTime(apt.endTime)}` : ""}
          </p>
        </div>
        <div>
          <span className="text-[10px] text-gray-500 font-semibold">الطبيب</span>
          <p className="text-gray-800 font-medium">{apt.doctorName}</p>
        </div>
        {apt.roomName && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">الغرفة</span>
            <p className="text-gray-800 font-medium">{apt.roomName}</p>
          </div>
        )}
        {apt.appointmentType && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">نوع الزيارة</span>
            <p className="text-gray-800 font-medium">{apt.appointmentType}</p>
          </div>
        )}
        {apt.specialty && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">التخصص</span>
            <p className="text-gray-800 font-medium">{apt.specialty}</p>
          </div>
        )}
        {apt.arrivedAt && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">وقت الوصول</span>
            <p className="text-gray-800 font-medium">{fmtTime(apt.arrivedAt)}</p>
          </div>
        )}
        {apt.calledAt && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">وقت النداء</span>
            <p className="text-gray-800 font-medium">{fmtTime(apt.calledAt)}</p>
          </div>
        )}
        {apt.inRoomAt && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">وقت دخول الغرفة</span>
            <p className="text-gray-800 font-medium">{fmtTime(apt.inRoomAt)}</p>
          </div>
        )}
      </div>
    </div>
  );
}

// ─── 3. Queue Status ─────────────────────────────────────────────────────────

function QueueStatusCard({ summary }: { summary: DailyJourneySummary }) {
  const q = summary.queueStatus!;

  return (
    <div className="bg-blue-50 rounded-xl border border-blue-200 p-3">
      <div className="flex items-center gap-2 mb-2">
        <Clock className="w-4 h-4 text-[#3d7ab5]" />
        <span className="text-sm font-bold text-[#1a3a5c]">حالة الطابور</span>
        <span className={cn(
          "text-[10px] px-2 py-0.5 rounded-full font-medium mr-auto",
          q.status === "Waiting" ? "bg-orange-100 text-orange-700" :
          q.status === "Called" ? "bg-purple-100 text-purple-700" :
          q.status === "InRoom" ? "bg-cyan-100 text-cyan-700" :
          q.status === "InProgress" ? "bg-emerald-100 text-emerald-700" :
          "bg-gray-100 text-gray-700"
        )}>
          {QUEUE_STATUS_ARABIC[q.status] ?? q.status}
        </span>
      </div>
      <div className="flex gap-4 text-[12px]">
        {q.roomName && (
          <div>
            <span className="text-[10px] text-gray-500">الغرفة</span>
            <p className="text-gray-800 font-medium">{q.roomName}</p>
          </div>
        )}
        {q.calledAt && (
          <div>
            <span className="text-[10px] text-gray-500">وقت النداء</span>
            <p className="text-gray-800 font-medium">{fmtTime(q.calledAt)}</p>
          </div>
        )}
        {q.inRoomAt && (
          <div>
            <span className="text-[10px] text-gray-500">دخول الغرفة</span>
            <p className="text-gray-800 font-medium">{fmtTime(q.inRoomAt)}</p>
          </div>
        )}
      </div>
    </div>
  );
}

// ─── 4. Medical Safety Alerts ────────────────────────────────────────────────

function MedicalAlertsCard({ alerts }: { alerts: MedicalAlert[] }) {
  return (
    <div className="rounded-xl border border-red-200 bg-[#fcebeb]/50 p-3">
      <div className="flex items-center gap-2 mb-2">
        <ShieldAlert className="w-4 h-4 text-red-600" />
        <span className="text-sm font-bold text-red-800">تنبيهات طبية</span>
      </div>
      <div className="flex flex-wrap gap-1.5">
        {alerts.map((alert, i) => {
          const style = SEVERITY_STYLES[alert.severity] ?? SEVERITY_STYLES.info;
          return (
            <span
              key={i}
              className={cn(
                "inline-flex items-center gap-1 px-2 py-1 rounded-full text-[10px] font-bold border",
                style.bg, style.border, style.text
              )}
            >
              <AlertTriangle className="w-3 h-3" />
              {alert.label}{alert.value ? `: ${alert.value}` : ""}
            </span>
          );
        })}
      </div>
    </div>
  );
}

// ─── 5. Journey Actions Panel ────────────────────────────────────────────────

interface JourneyActionsPanelProps {
  summary: DailyJourneySummary;
  selectedItem: JourneyItem;

  services: ServiceOption[];
  rooms: RoomOption[];
  intakeService: string;
  setIntakeService: (v: string) => void;
  intakeComplaint: string;
  setIntakeComplaint: (v: string) => void;
  intakeRoom: string;
  setIntakeRoom: (v: string) => void;
  intakeConsultFee: boolean;
  setIntakeConsultFee: (v: boolean) => void;
  intakeConsultAmount: number;
  setIntakeConsultAmount: (v: number) => void;
  intakeNotes: string;
  setIntakeNotes: (v: string) => void;
  handoffForm: { treatmentDone: string; diagnosis: string; nextVisitPlan: string; instructions: string; amountDue: number; notes: string };
  setHandoffForm: (v: { treatmentDone: string; diagnosis: string; nextVisitPlan: string; instructions: string; amountDue: number; notes: string }) => void;
  checkoutAmount: number;
  setCheckoutAmount: (v: number) => void;
  checkoutPayment: string;
  setCheckoutPayment: (v: string) => void;
  checkoutNextDate: string;
  setCheckoutNextDate: (v: string) => void;
  checkoutNextService: string;
  setCheckoutNextService: (v: string) => void;
  checkoutNotes: string;
  setCheckoutNotes: (v: string) => void;
  checkoutSuccess: boolean;
  draftInvoiceResult: { invoiceId: string; invoiceNumber: string } | null;
  intakeMutation: { isPending: boolean };
  sendToQueueMutation: { isPending: boolean };
  startVisitMutation: { isPending: boolean };
  handoffMutation: { isPending: boolean };
  checkoutMutation: { isPending: boolean };
  draftInvoiceMutation: { isPending: boolean };
  onSimpleAction: (item: JourneyItem) => void;
  onIntakeSubmit: (e: React.FormEvent) => void;
  onHandoffSubmit: (e: React.FormEvent) => void;
  onCheckoutSubmit: (e: React.FormEvent) => void;
  isDoctor: boolean;
  onCreateDraftInvoice: () => void;
  onIssueInvoice: () => void;
  issueInvoiceMutation: { isPending: boolean };
  onDownloadPdf: (url: string, filename: string) => void;
}

function JourneyActionsPanel({
  summary, selectedItem, isDoctor, services, rooms,
  intakeService, setIntakeService, intakeComplaint, setIntakeComplaint,
  intakeRoom, setIntakeRoom, intakeConsultFee, setIntakeConsultFee,
  intakeConsultAmount, setIntakeConsultAmount, intakeNotes, setIntakeNotes,
  handoffForm, setHandoffForm,
  checkoutAmount, setCheckoutAmount, checkoutPayment, setCheckoutPayment,
  checkoutNextDate, setCheckoutNextDate, checkoutNextService, setCheckoutNextService,
  checkoutNotes, setCheckoutNotes, checkoutSuccess, draftInvoiceResult,
  intakeMutation, sendToQueueMutation, startVisitMutation,
  handoffMutation, checkoutMutation, draftInvoiceMutation,
  onSimpleAction, onIntakeSubmit, onHandoffSubmit, onCheckoutSubmit, onCreateDraftInvoice,
  onIssueInvoice, issueInvoiceMutation, onDownloadPdf,
}: JourneyActionsPanelProps) {
  const { nextAction } = summary;
  const actionLabel = ACTION_LABELS[nextAction] ?? nextAction;
  const actionColor = ACTION_COLORS[nextAction] ?? "bg-gray-400";

  return (
    <div className="rounded-xl border-2 border-[#3d7ab5]/30 bg-[#f0f6fc] p-4 space-y-3">
      {/* Next Action Banner */}
      <div className="flex items-center gap-2">
        <CircleDot className="w-5 h-5 text-[#f5922e]" />
        <span className="text-sm font-bold text-[#1a3a5c]">الإجراء التالي</span>
        <span className={cn(
          "text-[11px] px-3 py-1 rounded-full font-bold text-white mr-auto",
          actionColor
        )}>
          {actionLabel}
        </span>
      </div>

      {/* ── Intake Form (Inline) ── */}
      {nextAction === "Intake" && (
        <form onSubmit={onIntakeSubmit} className="space-y-3 bg-white rounded-xl border border-[#9fe1cb] p-3">
          <span className="text-xs font-bold text-[#2d5e8e] flex items-center gap-1.5">
            <UserCheck className="w-4 h-4" />
            تسجيل الوصول وإدخال الطابور
          </span>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
            <div>
              <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">الخدمة المطلوبة</label>
              <select
                value={intakeService}
                onChange={(e) => {
                  setIntakeService(e.target.value);
                  const svc = services.find((s) => s.id === e.target.value);
                  if (svc) {
                    setIntakeConsultFee(svc.requiresConsultationFee);
                    setIntakeConsultAmount(svc.defaultPrice);
                  }
                }}
                className={inputCls}
              >
                <option value="">— اختر خدمة —</option>
                {services.map((s) => (
                  <option key={s.id} value={s.id}>{s.arabicName}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">الشكوى الرئيسية</label>
              <input
                value={intakeComplaint}
                onChange={(e) => setIntakeComplaint(e.target.value)}
                className={inputCls}
                placeholder="مثل: ألم في الضرس"
              />
            </div>
            <div>
              <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">الغرفة</label>
              <select
                value={intakeRoom}
                onChange={(e) => setIntakeRoom(e.target.value)}
                className={inputCls}
              >
                <option value="">— اختر غرفة —</option>
                {rooms.map((r) => (
                  <option key={r.id} value={r.id}>{r.arabicName}</option>
                ))}
              </select>
            </div>
            <div className="flex items-end gap-2">
              <label className="flex items-center gap-1.5 text-xs text-gray-700 flex-1">
                <input
                  type="checkbox"
                  checked={intakeConsultFee}
                  onChange={(e) => setIntakeConsultFee(e.target.checked)}
                  className="rounded border-gray-300"
                />
                رسوم معاينة
              </label>
              {intakeConsultFee && (
                <input
                  type="number"
                  value={intakeConsultAmount}
                  onChange={(e) => setIntakeConsultAmount(parseInt(e.target.value) || 0)}
                  className={cn(inputCls, "w-28")}
                  dir="ltr"
                  min={0}
                />
              )}
            </div>
          </div>
          <div>
            <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">ملاحظات</label>
            <textarea
              value={intakeNotes}
              onChange={(e) => setIntakeNotes(e.target.value)}
              className={cn(inputCls, "h-16 resize-none")}
              placeholder="ملاحظات إضافية"
            />
          </div>
          <div className="flex gap-2">
            <button
              type="submit"
              disabled={intakeMutation.isPending || sendToQueueMutation.isPending}
              className="flex items-center gap-1.5 px-4 py-2 text-xs font-bold rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] transition disabled:opacity-50"
            >
              <Save className="w-3.5 h-3.5" />
              {(intakeMutation.isPending || sendToQueueMutation.isPending) ? "جارٍ الحفظ..." : "تسجيل الوصول وإدخال الطابور"}
            </button>
          </div>
        </form>
      )}

      {/* ── Simple Action Buttons ── */}
      {(nextAction === "SendToQueue" || nextAction === "CallPatient" || nextAction === "EnterRoom" || nextAction === "StartVisit") && (
        <div className="flex gap-2">
          <button
            onClick={() => onSimpleAction(selectedItem)}
            disabled={nextAction === "SendToQueue" ? sendToQueueMutation.isPending :
                      nextAction === "StartVisit" ? startVisitMutation.isPending : false}
            className={cn(
              "flex items-center gap-1.5 px-5 py-2.5 text-sm font-bold rounded-lg text-white transition disabled:opacity-50",
              actionColor
            )}
          >
            {nextAction === "SendToQueue" && <ArrowRight className="w-4 h-4" />}
            {nextAction === "CallPatient" && <Megaphone className="w-4 h-4" />}
            {nextAction === "EnterRoom" && <DoorOpen className="w-4 h-4" />}
            {nextAction === "StartVisit" && <PlayCircle className="w-4 h-4" />}
            {(nextAction === "SendToQueue" && sendToQueueMutation.isPending) ||
             (nextAction === "StartVisit" && startVisitMutation.isPending)
              ? "جارٍ التنفيذ..."
              : actionLabel}
          </button>
        </div>
      )}

      {/* ── Handoff Form (Doctor Inline) ── */}
      {nextAction === "Handoff" && isDoctor && (
        <form onSubmit={onHandoffSubmit} className="space-y-3 bg-white rounded-xl border border-[#9fe1cb] p-3">
          <span className="text-xs font-bold text-[#2d5e8e] flex items-center gap-1.5">
            <ArrowRight className="w-4 h-4" />
            تسليم المريض للاستقبال
          </span>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
            <div>
              <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">ما تم عمله</label>
              <textarea
                value={handoffForm.treatmentDone}
                onChange={(e) => setHandoffForm({ ...handoffForm, treatmentDone: e.target.value })}
                className={cn(inputCls, "h-16 resize-none")}
              />
            </div>
            <div>
              <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">التشخيص</label>
              <input
                value={handoffForm.diagnosis}
                onChange={(e) => setHandoffForm({ ...handoffForm, diagnosis: e.target.value })}
                className={inputCls}
              />
            </div>
            <div>
              <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">تعليمات للمريض</label>
              <textarea
                value={handoffForm.instructions}
                onChange={(e) => setHandoffForm({ ...handoffForm, instructions: e.target.value })}
                className={cn(inputCls, "h-16 resize-none")}
              />
            </div>
            <div>
              <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">المبلغ المطلوب</label>
              <input
                type="number"
                value={handoffForm.amountDue}
                onChange={(e) => setHandoffForm({ ...handoffForm, amountDue: parseInt(e.target.value) || 0 })}
                className={inputCls}
                dir="ltr"
                min={0}
              />
            </div>
          </div>
          <div className="flex gap-2">
            <button
              type="submit"
              disabled={handoffMutation.isPending}
              className="flex items-center gap-1.5 px-4 py-2 text-xs font-bold rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] transition disabled:opacity-50"
            >
              <Save className="w-3.5 h-3.5" />
              {handoffMutation.isPending ? "جارٍ التسليم..." : "تسليم للاستقبال"}
            </button>
          </div>
        </form>
      )}

      {/* ── Checkout Form (Inline) ── */}
      {nextAction === "Checkout" && (
        <div className="space-y-3 bg-white rounded-xl border border-green-200 p-3">
          {checkoutSuccess ? (
            <div className="space-y-3">
              <div className="flex items-center gap-2 bg-green-50 border border-green-200 rounded-lg px-3 py-2.5">
                <CheckCircle2 className="w-5 h-5 text-green-600 flex-shrink-0" />
                <span className="text-sm font-medium text-green-800">تم إنهاء حساب الزيارة بنجاح</span>
              </div>
              {draftInvoiceResult && (
                <div className="border border-green-200 bg-green-50 rounded-lg p-2.5 space-y-2">
                  <p className="text-xs font-medium text-green-800">
                    الفاتورة: <span className="font-mono">{draftInvoiceResult.invoiceNumber}</span>
                  </p>
                  <div className="flex flex-wrap gap-2">
                    <Link
                      href={`/finance/invoices/${draftInvoiceResult.invoiceId}`}
                      className="text-xs text-[#3d7ab5] hover:underline flex items-center gap-1"
                    >
                      <FileText className="w-3 h-3" />
                      عرض الفاتورة
                    </Link>
                    {/* Phase 2b: Issue Invoice */}
                    <button
                      onClick={onIssueInvoice}
                      disabled={issueInvoiceMutation.isPending}
                      className="flex items-center gap-1 px-2 py-1 text-[10px] rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] disabled:opacity-50 transition"
                    >
                      <FileText className="w-3 h-3" />
                      {issueInvoiceMutation.isPending ? "جارٍ..." : "إصدار فاتورة"}
                    </button>
                    {/* Phase 2c: Download Invoice PDF */}
                    <button
                      onClick={() => onDownloadPdf(`/api/invoices/${draftInvoiceResult.invoiceId}/pdf`, `invoice-${draftInvoiceResult.invoiceNumber}.pdf`)}
                      className="flex items-center gap-1 px-2 py-1 text-[10px] rounded-lg bg-gray-100 text-gray-700 hover:bg-gray-200 transition"
                    >
                      <Download className="w-3 h-3" />
                      تحميل PDF
                    </button>
                  </div>
                </div>
              )}
            </div>
          ) : (
            <form onSubmit={onCheckoutSubmit} className="space-y-3">
              <span className="text-xs font-bold text-green-700 flex items-center gap-1.5">
                <CreditCard className="w-4 h-4" />
                إنهاء الحساب
              </span>

              {/* Draft Invoice */}
              {selectedItem.checkoutStatus === "ReadyForCheckout" && !draftInvoiceResult && (
                <div className="border border-[#3d7ab5]/20 bg-[#f7fafd] rounded-lg p-2.5 flex items-center justify-between">
                  <div className="flex items-center gap-1.5 text-xs font-medium text-[#1a3a5c]">
                    <FileText className="w-3.5 h-3.5 text-[#3d7ab5]" />
                    إنشاء فاتورة مسودة
                  </div>
                  <button
                    type="button"
                    disabled={draftInvoiceMutation.isPending}
                    onClick={onCreateDraftInvoice}
                    className="flex items-center gap-1 px-2.5 py-1 text-[10px] rounded-lg bg-[#3d7ab5] text-white hover:opacity-90 disabled:opacity-50 transition"
                  >
                    <FileText className="w-3 h-3" />
                    {draftInvoiceMutation.isPending ? "جارٍ..." : "إنشاء فاتورة"}
                  </button>
                </div>
              )}
              {draftInvoiceResult && (
                <div className="border border-green-200 bg-green-50 rounded-lg p-2.5 space-y-1">
                  <p className="text-xs font-medium text-green-800">
                    تم إنشاء الفاتورة: <span className="font-mono">{draftInvoiceResult.invoiceNumber}</span>
                  </p>
                  <Link
                    href={`/finance/invoices/${draftInvoiceResult.invoiceId}`}
                    className="text-[10px] text-[#3d7ab5] hover:underline flex items-center gap-1"
                  >
                    <FileText className="w-3 h-3" />
                    عرض الفاتورة
                  </Link>
                </div>
              )}

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                <div>
                  <label className="block text-[10px] font-bold text-gray-600 mb-0.5">المبلغ المطلوب</label>
                  {summary?.todayVisit?.amountDueReference != null && (
                    <span className="text-[9px] text-[#3d7ab5] font-medium">المبلغ المرجعي: {fmtRial(summary.todayVisit.amountDueReference)}</span>
                  )}
                  <input
                    type="number"
                    value={checkoutAmount}
                    onChange={(e) => setCheckoutAmount(parseInt(e.target.value) || 0)}
                    className={inputCls}
                    dir="ltr"
                    min={0}
                  />
                </div>
                <div>
                  <label className="block text-[10px] font-bold text-gray-600 mb-0.5">طريقة الدفع</label>
                  <select
                    value={checkoutPayment}
                    onChange={(e) => setCheckoutPayment(e.target.value)}
                    className={inputCls}
                  >
                    {PAYMENT_METHODS.map((m) => (
                      <option key={m.value} value={m.value}>{m.label}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="block text-[10px] font-bold text-gray-600 mb-0.5">حجز موعد قادم</label>
                  <input
                    type="date"
                    value={checkoutNextDate}
                    onChange={(e) => setCheckoutNextDate(e.target.value)}
                    className={inputCls}
                    dir="ltr"
                  />
                </div>
                <div>
                  <label className="block text-[10px] font-bold text-gray-600 mb-0.5">الخدمة القادمة</label>
                  <select
                    value={checkoutNextService}
                    onChange={(e) => setCheckoutNextService(e.target.value)}
                    className={inputCls}
                  >
                    <option value="">— اختر خدمة —</option>
                    {services.map((s) => (
                      <option key={s.id} value={s.id}>{s.arabicName}</option>
                    ))}
                  </select>
                </div>
              </div>
              <div>
                <label className="block text-[10px] font-bold text-gray-600 mb-0.5">ملاحظات</label>
                <textarea
                  value={checkoutNotes}
                  onChange={(e) => setCheckoutNotes(e.target.value)}
                  className={cn(inputCls, "h-14 resize-none")}
                  placeholder="ملاحظات إضافية"
                />
              </div>
              <div className="flex gap-2">
                <button
                  type="submit"
                  disabled={checkoutMutation.isPending}
                  className="flex items-center gap-1.5 px-4 py-2 text-xs font-bold rounded-lg bg-green-600 text-white hover:bg-green-700 transition disabled:opacity-50"
                >
                  <CreditCard className="w-3.5 h-3.5" />
                  {checkoutMutation.isPending ? "جارٍ الإنهاء..." : "إنهاء الحساب"}
                </button>
              </div>
            </form>
          )}
        </div>
      )}

      {/* ── InProgress indicator ── */}
      {nextAction === "InProgress" && (
        <div className="flex items-center gap-2 bg-emerald-50 border border-emerald-200 rounded-lg px-3 py-2.5">
          <Stethoscope className="w-5 h-5 text-emerald-600" />
          <span className="text-sm font-semibold text-emerald-800">المريض عند الطبيب حاليًا</span>
        </div>
      )}
    </div>
  );
}

// ─── 6. Today's Visit ────────────────────────────────────────────────────────

function TodaysVisitCard({
  summary, isReception, isAccountant,
}: {
  summary: DailyJourneySummary;

  isReception: boolean;
  isAccountant: boolean;
}) {
  const visit = summary.todayVisit!;
  const hideClinical = isReception || isAccountant;

  return (
    <div className="bg-white rounded-xl border border-gray-200 p-4">
      <div className="flex items-center gap-2 mb-3">
        <Stethoscope className="w-4 h-4 text-[#3d7ab5]" />
        <span className="text-sm font-bold text-[#1a3a5c]">زيارة اليوم</span>
        {visit.checkoutStatus && (
          <span className={cn(
            "text-[10px] px-2 py-0.5 rounded-full font-medium mr-auto",
            visit.checkoutStatus === "Completed" ? "bg-green-50 text-green-700" :
            visit.checkoutStatus === "ReadyForCheckout" ? "bg-amber-50 text-amber-700" :
            "bg-gray-50 text-gray-700"
          )}>
            {visit.checkoutStatus === "Completed" ? "مكتمل" :
             visit.checkoutStatus === "ReadyForCheckout" ? "جاهز للحساب" :
             visit.checkoutStatus}
          </span>
        )}
      </div>
      <div className="space-y-2 text-[12px]">
        {visit.chiefComplaint && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">الشكوى الرئيسية</span>
            <p className="text-gray-800 font-medium">{visit.chiefComplaint}</p>
          </div>
        )}
        {!hideClinical && visit.treatmentDone && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">ما تم عمله</span>
            <p className="text-gray-800 font-medium">{visit.treatmentDone}</p>
          </div>
        )}
        {!hideClinical && visit.diagnosis && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">التشخيص</span>
            <p className="text-gray-800 font-medium">{visit.diagnosis}</p>
          </div>
        )}
        {!hideClinical && visit.instructions && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">تعليمات للمريض</span>
            <p className="text-gray-800 font-medium">{visit.instructions}</p>
          </div>
        )}
        {visit.cost != null && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">التكلفة</span>
            <p className="text-gray-800 font-bold">{fmtRial(visit.cost)}</p>
          </div>
        )}
      </div>
    </div>
  );
}

// ─── 7. Finance Summary ──────────────────────────────────────────────────────

function FinanceSummaryCard({
  summary, isAccountant, canViewFinance, canViewInvoices,
}: {
  summary: DailyJourneySummary;
  isAccountant: boolean;
  canViewFinance: boolean;
  canViewInvoices: boolean;
}) {
  const fin = summary.financeSummary!;
  const showFullFinance = canViewFinance;

  return (
    <div className="bg-white rounded-xl border border-gray-200 p-4">
      <div className="flex items-center gap-2 mb-3">
        <Wallet className="w-4 h-4 text-[#3d7ab5]" />
        <span className="text-sm font-bold text-[#1a3a5c]">الملف المالي</span>
        <span className={cn(
          "text-[10px] px-2 py-0.5 rounded-full font-medium mr-auto",
          fin.financialStatus === "paid_full" ? "bg-green-50 text-green-700" :
          fin.financialStatus === "overdue" ? "bg-red-50 text-red-700" :
          fin.financialStatus === "has_balance" ? "bg-amber-50 text-amber-700" :
          "bg-gray-50 text-gray-600"
        )}>
          {fin.financialStatus === "paid_full" ? "مسدد" :
           fin.financialStatus === "overdue" ? "متأخر" :
           fin.financialStatus === "has_balance" ? "رصيد مستحق" :
           "بدون خطة"}
        </span>
      </div>
      <div className="grid grid-cols-2 gap-3 text-[12px]">
        <div className="bg-amber-50 rounded-lg p-2.5">
          <span className="text-[10px] text-amber-700 font-semibold">المستحق</span>
          <p className="text-amber-800 font-bold text-sm">{fmtRial(fin.outstandingBalance)}</p>
        </div>
        <div className="bg-red-50 rounded-lg p-2.5">
          <span className="text-[10px] text-red-700 font-semibold">متأخر</span>
          <p className="text-red-800 font-bold text-sm">{fmtRial(fin.overdueAmount)}</p>
        </div>
        {showFullFinance && fin.totalTreatmentCost != null && (
          <div className="bg-gray-50 rounded-lg p-2.5">
            <span className="text-[10px] text-gray-600 font-semibold">إجمالي التكلفة</span>
            <p className="text-gray-800 font-bold text-sm">{fmtRial(fin.totalTreatmentCost)}</p>
          </div>
        )}
        {showFullFinance && fin.totalPaid != null && (
          <div className="bg-green-50 rounded-lg p-2.5">
            <span className="text-[10px] text-green-700 font-semibold">المدفوع</span>
            <p className="text-green-800 font-bold text-sm">{fmtRial(fin.totalPaid)}</p>
          </div>
        )}
      </div>
      {fin.latestPayment && (
        <div className="mt-2 text-[11px] text-gray-600 border-t border-gray-100 pt-2">
          <span className="font-semibold">آخر دفعة:</span>{" "}
          {fmtRial(fin.latestPayment.amount)} — {fmtDate(fin.latestPayment.paymentDate)}
          {fin.latestPayment.paymentMethod && ` (${PAYMENT_METHODS.find(m => m.value === fin.latestPayment!.paymentMethod)?.label ?? fin.latestPayment.paymentMethod})`}
        </div>
      )}
      {summary.unpaidInvoicesCount > 0 && canViewInvoices && (
        <div className="mt-2 text-[11px] text-red-600 font-medium">
          فواتير غير مدفوعة: {summary.unpaidInvoicesCount}
        </div>
      )}
      {/* Active Contract — Admin/Accountant only */}
      {summary.activeContract && (isAccountant || canViewFinance) && (
        <div className="mt-3 border-t border-gray-100 pt-3">
          <div className="flex items-center gap-1.5 mb-2">
            <FileText className="w-3.5 h-3.5 text-[#3d7ab5]" />
            <span className="text-[11px] font-bold text-[#1a3a5c]">عقد نشط</span>
          </div>
          <div className="grid grid-cols-2 gap-2 text-[11px]">
            <div>
              <span className="text-[9px] text-gray-500">الإجمالي</span>
              <p className="text-gray-800 font-medium">{fmtRial(summary.activeContract.totalAmount)}</p>
            </div>
            <div>
              <span className="text-[9px] text-gray-500">المدفوع</span>
              <p className="text-green-700 font-medium">{fmtRial(summary.activeContract.paidAmount)}</p>
            </div>
            <div>
              <span className="text-[9px] text-gray-500">المتبقي</span>
              <p className="text-amber-700 font-medium">{fmtRial(summary.activeContract.remainingAmount)}</p>
            </div>
            <div>
              <span className="text-[9px] text-gray-500">الأقساط</span>
              <p className="text-gray-800 font-medium">{summary.activeContract.installmentsCount}</p>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

// ─── 8. Active Ortho Case ────────────────────────────────────────────────────

function ActiveOrthoCard({ summary }: { summary: DailyJourneySummary }) {
  const ortho = summary.activeOrthoCase!;

  return (
    <div className="bg-white rounded-xl border border-gray-200 p-4">
      <div className="flex items-center gap-2 mb-3">
        <Wrench className="w-4 h-4 text-[#3d7ab5]" />
        <span className="text-sm font-bold text-[#1a3a5c]">ملف التقويم النشط</span>
        <Link
          href={`/ortho/${ortho.id}`}
          className="mr-auto text-[10px] text-[#3d7ab5] hover:underline flex items-center gap-0.5"
        >
          التفاصيل <ExternalLink className="w-3 h-3" />
        </Link>
      </div>
      <div className="grid grid-cols-2 gap-2 text-[12px]">
        {ortho.caseNumber && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">رقم القضية</span>
            <p className="text-gray-800 font-medium font-mono">{ortho.caseNumber}</p>
          </div>
        )}
        {ortho.applianceType && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">نوع الجهاز</span>
            <p className="text-gray-800 font-medium">{ortho.applianceType}</p>
          </div>
        )}
        {ortho.currentStage && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">المرحلة الحالية</span>
            <p className="text-gray-800 font-medium">{ortho.currentStage}</p>
          </div>
        )}
        {ortho.totalFee != null && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">الرسوم الإجمالية</span>
            <p className="text-gray-800 font-bold">{fmtRial(ortho.totalFee)}</p>
          </div>
        )}
      </div>
      {ortho.stagePercentage != null && (
        <div className="mt-2">
          <div className="flex items-center justify-between text-[10px] text-gray-600 mb-1">
            <span>نسبة الإنجاز</span>
            <span className="font-bold text-[#3d7ab5]">{ortho.stagePercentage}%</span>
          </div>
          <div className="w-full bg-gray-100 rounded-full h-1.5">
            <div
              className="bg-[#3d7ab5] rounded-full h-1.5 transition-all"
              style={{ width: `${Math.min(ortho.stagePercentage, 100)}%` }}
            />
          </div>
        </div>
      )}
    </div>
  );
}

// ─── 9. Recent Visits ────────────────────────────────────────────────────────

function RecentVisitsCard({
  visits, isReception, isAccountant,
}: {
  visits: DailyJourneyRecentVisit[];
  isReception: boolean;
  isAccountant: boolean;
}) {
  const hideDiagnosis = isReception || isAccountant;

  return (
    <div className="bg-white rounded-xl border border-gray-200 p-4">
      <div className="flex items-center gap-2 mb-3">
        <HeartPulse className="w-4 h-4 text-[#3d7ab5]" />
        <span className="text-sm font-bold text-[#1a3a5c]">زيارات سابقة</span>
      </div>
      <div className="space-y-2">
        {visits.map((visit) => (
          <div
            key={visit.id}
            className="border border-gray-100 rounded-lg p-2.5 hover:bg-gray-50 transition"
          >
            <div className="flex items-start justify-between gap-2">
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2">
                  <span className="text-[11px] text-gray-500">{fmtDate(visit.visitDate)}</span>
                  {visit.visitType && (
                    <span className="text-[9px] px-1.5 py-0.5 rounded-full bg-gray-100 text-gray-600 font-medium">{visit.visitType}</span>
                  )}
                </div>
                {visit.chiefComplaint && (
                  <p className="text-[11px] text-gray-800 mt-0.5 truncate">{visit.chiefComplaint}</p>
                )}
                {!hideDiagnosis && visit.diagnosis && (
                  <p className="text-[10px] text-gray-500 truncate">التشخيص: {visit.diagnosis}</p>
                )}
              </div>
              {visit.cost != null && (
                <span className="text-[11px] font-bold text-gray-700 flex-shrink-0">{fmtRial(visit.cost)}</span>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

// ─── 10. Timeline ─────────────────────────────────────────────────────────────

function TimelineCard({ events }: { events: TimelineEvent[] }) {
  return (
    <div className="bg-white rounded-xl border border-gray-200 p-4">
      <div className="flex items-center gap-2 mb-3">
        <Clock className="w-4 h-4 text-[#3d7ab5]" />
        <span className="text-sm font-bold text-[#1a3a5c]">السجل الزمني</span>
      </div>
      <div className="relative space-y-0">
        {events.map((event, i) => {
          const dotColor = TIMELINE_DOT_COLORS[event.type] ?? TIMELINE_DOT_COLORS.default;
          return (
            <div key={i} className="flex gap-3 pb-3 last:pb-0">
              {/* Timeline line + dot */}
              <div className="flex flex-col items-center flex-shrink-0">
                <div className={cn("w-2.5 h-2.5 rounded-full mt-1", dotColor)} />
                {i < events.length - 1 && (
                  <div className="w-0.5 flex-1 bg-gray-200 mt-0.5" />
                )}
              </div>
              {/* Content */}
              <div className="flex-1 min-w-0 -mt-0.5">
                <div className="flex items-center gap-2">
                  <span className="text-[11px] font-semibold text-gray-800 truncate">{event.title}</span>
                  {event.status && (
                    <span className="text-[9px] px-1.5 py-0.5 rounded-full bg-gray-100 text-gray-600 font-medium">
                      {event.status}
                    </span>
                  )}
                </div>
                {event.sub && (
                  <p className="text-[10px] text-gray-500 truncate">{event.sub}</p>
                )}
                <span className="text-[9px] text-gray-400">{fmtDate(event.date)}</span>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
