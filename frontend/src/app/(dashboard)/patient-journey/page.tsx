"use client";

import { useEffect, useState, useCallback, useMemo } from "react";
import Link from "next/link";
import {
  Users, UserCheck, Clock, DoorOpen, CreditCard, CheckCircle2,
  Save, RefreshCw, Stethoscope, ArrowRight, Phone, CalendarDays,
  Filter, FileText, AlertTriangle, ExternalLink, Megaphone,
  PlayCircle, ShieldAlert, Wallet, CircleDot,
  Wrench, HeartPulse, Route, Search, Printer,
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
} from "@/hooks/usePatientJourney";
import { QUEUE_STATUS_ARABIC } from "@/types/journey";
import type {
  DailyJourneySummary,
  MedicalAlert,
  DailyJourneyRecentVisit,
  TimelineEvent,
  DailyJourneyOrtho,
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
  medicalAlerts?: MedicalAlert[];
  outstandingBalance?: number;
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

// ─── Handoff Nodes ────────────────────────────────────────────────────────────

const HANDOFF_NODES = [
  { key: "Reception", label: "الاستقبال" },
  { key: "Waiting", label: "الانتظار" },
  { key: "DoctorRoom", label: "غرفة الطبيب" },
  { key: "Treatment", label: "العلاج" },
  { key: "Checkout", label: "الحساب" },
] as const;

function getHandoffActiveNode(journeyStep: string): string {
  if (["Scheduled", "Arrived"].includes(journeyStep)) return "Reception";
  if (journeyStep === "Waiting") return "Waiting";
  if (["Called", "InRoom"].includes(journeyStep)) return "DoctorRoom";
  if (journeyStep === "InProgress") return "Treatment";
  if (["Handoff", "Checkout", "Completed"].includes(journeyStep)) return "Checkout";
  return "Reception";
}

const DEPARTMENT_CHIPS = [
  { key: "ortho", label: "تقويم" },
  { key: "general", label: "طب الأسنان العام" },
  { key: "surgery", label: "جراحة الفم" },
  { key: "implant", label: "زراعة" },
  { key: "prosthetics", label: "تركيبات" },
  { key: "endo", label: "علاج العصب" },
] as const;

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

function calcWaitingDuration(arrivedAt?: string): string | null {
  if (!arrivedAt) return null;
  try {
    const arrived = new Date(arrivedAt);
    const now = new Date();
    const diffMs = now.getTime() - arrived.getTime();
    if (diffMs < 0) return null;
    const mins = Math.floor(diffMs / 60000);
    if (mins < 60) return `${mins} دقيقة`;
    const hrs = Math.floor(mins / 60);
    const rem = mins % 60;
    return `${hrs} س ${rem} د`;
  } catch {
    return null;
  }
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

// ─── Section Header Component ─────────────────────────────────────────────────

function SectionHeader({ icon: Icon, title, color = "#3d7ab5" }: { icon: React.ElementType; title: string; color?: string }) {
  return (
    <div className="flex items-center gap-2 mb-3">
      <div
        className="w-7 h-7 rounded-full flex items-center justify-center flex-shrink-0"
        style={{ backgroundColor: color + "18" }}
      >
        <Icon className="w-3.5 h-3.5" style={{ color }} />
      </div>
      <span className="text-sm font-bold text-[#1a3a5c]">{title}</span>
    </div>
  );
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

  // Search
  const [searchText, setSearchText] = useState("");

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
  }, [selectedPatientId]);

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

  // ─── Filtered Items (Search) ───────────────────────────────────────────────

  const filteredItems = useMemo(() => {
    if (!searchText.trim()) return items;
    const q = searchText.trim().toLowerCase();
    return items.filter(
      (i) =>
        i.patientName.toLowerCase().includes(q) ||
        (i.patientPhone && i.patientPhone.includes(q)) ||
        i.patientId.toLowerCase().includes(q)
    );
  }, [items, searchText]);

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

        {/* Search */}
        <div className="flex-shrink-0">
          <div className="relative">
            <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
            <input
              type="text"
              value={searchText}
              onChange={(e) => setSearchText(e.target.value)}
              placeholder="بحث بالاسم أو الهاتف أو الرقم..."
              className="w-full pr-9 pl-3 py-2 text-xs rounded-xl border border-gray-200 bg-white focus:outline-none focus:ring-2 focus:ring-[#3d7ab5] focus:border-[#3d7ab5]"
            />
          </div>
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
        {!loading && filteredItems.length > 0 && (
          <div className="flex-1 overflow-y-auto space-y-1.5 min-h-0 pr-1">
            {filteredItems.map((item) => {
              const isSelected = item.patientId === selectedPatientId;
              return (
                <button
                  key={item.appointmentId}
                  onClick={() => setSelectedPatientId(item.patientId)}
                  className={cn(
                    "w-full text-start rounded-xl border p-3 transition-all hover:shadow-sm",
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
                        {item.medicalAlerts && item.medicalAlerts.length > 0 && (
                          <span title="تنبيهات طبية" className="text-amber-500 text-xs">⚠️</span>
                        )}
                        {item.outstandingBalance != null && item.outstandingBalance > 0 && (
                          <span title="رصيد مستحق" className="text-xs">💰</span>
                        )}
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
                        <span className="text-[11px] text-gray-600">{item.doctorName}</span>
                        <span className="text-gray-300">|</span>
                        <span className="text-[11px] text-gray-500 font-mono" dir="ltr">
                          {item.appointmentTime}
                        </span>
                        {item.roomName && (
                          <>
                            <span className="text-gray-300">|</span>
                            <span className="text-[11px] text-gray-500">{item.roomName}</span>
                          </>
                        )}
                      </div>
                      {item.serviceName && (
                        <span className="inline-block mt-1 text-[9px] px-1.5 py-0.5 rounded-full bg-[#e6f1fb] text-[#185fa5] font-medium">
                          {item.serviceName}
                        </span>
                      )}
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
        {!loading && filteredItems.length === 0 && (
          <div className="text-center py-12 flex-1 flex flex-col items-center justify-center">
            <Users className="w-10 h-10 text-gray-300 mx-auto mb-2" />
            <p className="text-gray-500 font-medium text-sm">
              {searchText ? "لا توجد نتائج مطابقة للبحث" : "لا توجد رحلات مرضى لهذا اليوم"}
            </p>
            <p className="text-xs text-gray-400 mt-1">
              {searchText ? "جرب كلمة بحث مختلفة" : "اختر تاريخًا آخر أو أضف مواعيد"}
            </p>
          </div>
        )}
      </div>

      {/* ═══ RIGHT PANEL (~60%) ═══ */}
      <div className="w-full lg:w-[60%] flex flex-col bg-white rounded-2xl border border-gray-200 overflow-hidden">
        {!selectedPatientId && !loading && (
          <div className="flex-1 flex flex-col items-center justify-center text-center p-8">
            <Route className="w-14 h-14 text-gray-200 mb-3" />
            <p className="text-gray-500 font-bold">اختر مريضًا من القائمة</p>
            <p className="text-xs text-gray-400 mt-1">سيتم عرض تفاصيل رحلة المريض هنا</p>
          </div>
        )}

        {selectedPatientId && summaryLoading && (
          <div className="flex-1 flex flex-col items-center justify-center p-8">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-[#3d7ab5] mb-3" />
            <p className="text-sm text-gray-500">جارٍ تحميل بيانات المريض...</p>
          </div>
        )}

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
            <PatientHeaderCard summary={summary} isDoctor={isDoctor} selectedItem={selectedItem} />

            {/* ─── 2. Today's Appointment ─── */}
            <TodaysAppointmentCard summary={summary} />

            {/* ─── 3. Queue/Room Status + Actions ─── */}
            <QueueRoomStatusCard
              summary={summary}
              selectedItem={selectedItem}
              isDoctor={isDoctor}
              canEditJourney={canEditJourney}
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
              intakeMutation={intakeMutation}
              sendToQueueMutation={sendToQueueMutation}
              startVisitMutation={startVisitMutation}
              onSimpleAction={handleSimpleAction}
              onIntakeSubmit={handleIntakeSubmit}
            />

            {/* ─── 4. Medical Safety Alerts ─── */}
            {!isAccountant && (
              <MedicalAlertsCard alerts={summary.medicalAlerts} />
            )}

            {/* ─── 5. Clinical Visit Card ─── */}
            {!isAccountant && canViewVisits && (
              <ClinicalVisitCard summary={summary} isReception={isReception} isAccountant={isAccountant} />
            )}

            {/* ─── 6. Department Handoff ─── */}
            <DepartmentHandoffCard summary={summary} />

            {/* ─── 7. Finance/Checkout ─── */}
            {(canViewFinance || canViewCheckout) && !isDoctor && (
              <FinanceCheckoutCard
                summary={summary}
                isAccountant={isAccountant}
                canViewFinance={canViewFinance}
                canViewInvoices={canViewInvoices}
              />
            )}

            {/* ─── 8. Active Ortho Case ─── */}
            {canViewVisits && !isAccountant && (
              <ActiveOrthoCard summary={summary} />
            )}

            {/* ─── 9. Timeline ─── */}
            {canViewJourney && (
              <TimelineCard events={summary.timeline} />
            )}

            {/* ─── 6b. Handoff Form (Doctor) ─── */}
            {canEditJourney && summary.nextAction === "Handoff" && isDoctor && (
              <HandoffFormCard
                handoffForm={handoffForm}
                setHandoffForm={setHandoffForm}
                handoffMutation={handoffMutation}
                onSubmit={handleHandoffSubmit}
              />
            )}

            {/* ─── 7b. Checkout Form ─── */}
            {canEditJourney && summary.nextAction === "Checkout" && (
              <CheckoutFormCard
                selectedItem={selectedItem}
                services={services}
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
                checkoutMutation={checkoutMutation}
                draftInvoiceMutation={draftInvoiceMutation}
                onCheckoutSubmit={handleCheckoutSubmit}
                onCreateDraftInvoice={handleCreateDraftInvoice}
              />
            )}

            {/* ─── 10. Quick Actions ─── */}
            <QuickActionsCard
              patientId={selectedPatientId}
              activeOrthoCase={summary.activeOrthoCase}
              canViewFinance={canViewFinance}
              canViewVisits={canViewVisits}
            />
          </div>
        )}
      </div>
    </div>
  );
}

// ═══════════════════════════════════════════════════════════════════════════════
// SUB-COMPONENTS
// ═══════════════════════════════════════════════════════════════════════════════

// ─── 1. Patient Header Card ──────────────────────────────────────────────────

function PatientHeaderCard({ summary, isDoctor, selectedItem }: { summary: DailyJourneySummary; isDoctor: boolean; selectedItem: JourneyItem | null }) {
  const { patient, journeyStep } = summary;
  const currentStepIdx = getStepIndex(journeyStep);

  return (
    <div className="bg-[#1a3a5c] rounded-xl p-5 text-white hover:shadow-md transition-shadow">
      <div className="flex items-start gap-4">
        <div className="w-14 h-14 rounded-full bg-[#3d7ab5] flex items-center justify-center text-xl font-bold flex-shrink-0 shadow-lg">
          {getInitials(patient.fullName)}
        </div>
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 flex-wrap">
            <h2 className="text-lg font-bold truncate">{patient.fullName}</h2>
            <span className="text-[10px] font-mono text-white/60">#{patient.patientNumber}</span>
            <span
              className="text-[10px] px-2.5 py-0.5 rounded-full font-bold mr-auto"
              style={{
                backgroundColor: journeyStep === "InProgress" ? "#f5922e" : journeyStep === "Completed" ? "#22c55e" : "rgba(255,255,255,0.15)",
                color: "white"
              }}
            >
              {STATUS_LABELS[journeyStep] ?? journeyStep}
            </span>
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
          {selectedItem && (
            <div className="flex flex-wrap gap-3 mt-1.5">
              <span className="text-[11px] text-white/70 flex items-center gap-1">
                <Stethoscope className="w-3 h-3" />
                {selectedItem.doctorName}
              </span>
              {selectedItem.roomName && (
                <span className="text-[11px] text-white/70 flex items-center gap-1">
                  <DoorOpen className="w-3 h-3" />
                  {selectedItem.roomName}
                </span>
              )}
            </div>
          )}
        </div>
      </div>

      {/* Enhanced Journey Step Progress Bar */}
      <div className="mt-5">
        <div className="flex items-center gap-0 overflow-x-auto pb-1">
          {JOURNEY_STEPS.map((step, idx) => {
            const status = getStepStatus(step.key, journeyStep);
            return (
              <div key={step.key} className="flex items-center">
                <div className="flex flex-col items-center gap-1 min-w-[56px]">
                  <div
                    className={cn(
                      "w-8 h-8 rounded-full flex items-center justify-center text-[11px] font-bold border-2 transition-all",
                      status === "done" && "bg-[#3d7ab5] text-white border-[#3d7ab5]",
                      status === "current" && "bg-white text-[#f5922e] border-[#f5922e] shadow-[0_0_12px_rgba(245,146,46,0.5)]",
                      status === "pending" && "bg-white/10 text-white/40 border-white/20"
                    )}
                  >
                    {status === "done" ? <CheckCircle2 className="w-4 h-4" /> : idx + 1}
                  </div>
                  <span
                    className={cn(
                      "text-[9px] text-center leading-tight",
                      status === "current" ? "text-[#f5922e] font-bold" : "text-white/50"
                    )}
                  >
                    {step.label}
                  </span>
                </div>
                {idx < JOURNEY_STEPS.length - 1 && (
                  <div
                    className={cn(
                      "flex-1 h-[3px] min-w-[10px] rounded-full",
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
  const apt = summary.todayAppointment;

  if (!apt) {
    return (
      <div className="bg-white rounded-xl border border-gray-200 p-4 hover:shadow-sm transition-shadow">
        <SectionHeader icon={CalendarDays} title="موعد اليوم" />
        <div className="flex flex-col items-center justify-center py-6 text-center">
          <CalendarDays className="w-10 h-10 text-gray-200 mb-2" />
          <p className="text-sm text-gray-500 font-medium">لا يوجد موعد مسجل لهذا اليوم</p>
        </div>
      </div>
    );
  }

  return (
    <div className="bg-white rounded-xl border border-gray-200 p-4 hover:shadow-sm transition-shadow">
      <SectionHeader icon={CalendarDays} title="موعد اليوم" />
      <div className="flex items-center gap-2 mb-3">
        <span className={cn(
          "text-[10px] px-2.5 py-1 rounded-full font-medium",
          STATUS_COLORS[apt.status] ?? "bg-gray-50 text-gray-700"
        )}>
          {STATUS_LABELS[apt.status] ?? apt.status}
        </span>
      </div>
      <div className="grid grid-cols-2 gap-x-4 gap-y-2.5 text-[12px]">
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
      {apt.notes && (
        <div className="mt-3 pt-2 border-t border-gray-100">
          <span className="text-[10px] text-gray-500 font-semibold">ملاحظات الموعد</span>
          <p className="text-[11px] text-gray-700 mt-0.5">{apt.notes}</p>
        </div>
      )}
    </div>
  );
}

// ─── 3. Queue/Room Status + Actions ──────────────────────────────────────────

function QueueRoomStatusCard({
  summary, selectedItem, isDoctor, canEditJourney,
  services, rooms,
  intakeService, setIntakeService, intakeComplaint, setIntakeComplaint,
  intakeRoom, setIntakeRoom, intakeConsultFee, setIntakeConsultFee,
  intakeConsultAmount, setIntakeConsultAmount, intakeNotes, setIntakeNotes,
  intakeMutation, sendToQueueMutation, startVisitMutation,
  onSimpleAction, onIntakeSubmit,
}: {
  summary: DailyJourneySummary;
  selectedItem: JourneyItem | null;
  isDoctor: boolean;
  canEditJourney: boolean;
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
  intakeMutation: { isPending: boolean };
  sendToQueueMutation: { isPending: boolean };
  startVisitMutation: { isPending: boolean };
  onSimpleAction: (item: JourneyItem) => void;
  onIntakeSubmit: (e: React.FormEvent) => void;
}) {
  const q = summary.queueStatus;
  const nextAction = summary.nextAction;
  const waitingDuration = q ? calcWaitingDuration(summary.todayAppointment?.arrivedAt) : null;

  return (
    <div className="bg-white rounded-xl border border-gray-200 p-4 hover:shadow-sm transition-shadow">
      <SectionHeader icon={Clock} title="حالة الطابور والإجراءات" color="#3d7ab5" />

      {!q ? (
        <div className="flex flex-col items-center justify-center py-6 text-center">
          <Clock className="w-10 h-10 text-gray-200 mb-2" />
          <p className="text-sm text-gray-500 font-medium">لم يدخل الطابور بعد</p>
        </div>
      ) : (
        <div className="bg-blue-50/60 rounded-lg p-3 mb-3">
          <div className="flex items-center gap-2 mb-2">
            <span className={cn(
              "text-[10px] px-2 py-0.5 rounded-full font-medium",
              q.status === "Waiting" ? "bg-orange-100 text-orange-700" :
              q.status === "Called" ? "bg-purple-100 text-purple-700" :
              q.status === "InRoom" ? "bg-cyan-100 text-cyan-700" :
              q.status === "InProgress" ? "bg-emerald-100 text-emerald-700" :
              "bg-gray-100 text-gray-700"
            )}>
              {QUEUE_STATUS_ARABIC[q.status] ?? q.status}
            </span>
            {waitingDuration && (
              <span className="text-[10px] text-gray-500 flex items-center gap-1">
                <Clock className="w-3 h-3" />
                مدة الانتظار: {waitingDuration}
              </span>
            )}
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
      )}

      {canEditJourney && nextAction && nextAction !== "None" && selectedItem && (
        <div className="space-y-3">
          <div className="flex items-center gap-2">
            <CircleDot className="w-5 h-5 text-[#f5922e]" />
            <span className="text-xs font-bold text-[#1a3a5c]">الإجراء التالي</span>
            <span className={cn(
              "text-[10px] px-2.5 py-1 rounded-full font-bold text-white mr-auto",
              ACTION_COLORS[nextAction] ?? "bg-gray-400"
            )}>
              {ACTION_LABELS[nextAction] ?? nextAction}
            </span>
          </div>

          {nextAction === "Intake" && (
            <form onSubmit={onIntakeSubmit} className="space-y-3 bg-[#f0f6fc] rounded-xl border border-[#3d7ab5]/20 p-3">
              <span className="text-xs font-bold text-[#2d5e8e] flex items-center gap-1.5">
                <UserCheck className="w-4 h-4" />
                تسجيل الوصول وإدخال الطابور
              </span>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                <div>
                  <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">الخدمة المطلوبة</label>
                  <select value={intakeService} onChange={(e) => { setIntakeService(e.target.value); const svc = services.find((s) => s.id === e.target.value); if (svc) { setIntakeConsultFee(svc.requiresConsultationFee); setIntakeConsultAmount(svc.defaultPrice); } }} className={inputCls}>
                    <option value="">— اختر خدمة —</option>
                    {services.map((s) => (<option key={s.id} value={s.id}>{s.arabicName}</option>))}
                  </select>
                </div>
                <div>
                  <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">الشكوى الرئيسية</label>
                  <input value={intakeComplaint} onChange={(e) => setIntakeComplaint(e.target.value)} className={inputCls} placeholder="مثل: ألم في الضرس" />
                </div>
                <div>
                  <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">الغرفة</label>
                  <select value={intakeRoom} onChange={(e) => setIntakeRoom(e.target.value)} className={inputCls}>
                    <option value="">— اختر غرفة —</option>
                    {rooms.map((r) => (<option key={r.id} value={r.id}>{r.arabicName}</option>))}
                  </select>
                </div>
                <div className="flex items-end gap-2">
                  <label className="flex items-center gap-1.5 text-xs text-gray-700 flex-1">
                    <input type="checkbox" checked={intakeConsultFee} onChange={(e) => setIntakeConsultFee(e.target.checked)} className="rounded border-gray-300" />
                    رسوم معاينة
                  </label>
                  {intakeConsultFee && (
                    <input type="number" value={intakeConsultAmount} onChange={(e) => setIntakeConsultAmount(parseInt(e.target.value) || 0)} className={cn(inputCls, "w-28")} dir="ltr" min={0} />
                  )}
                </div>
              </div>
              <div>
                <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">ملاحظات</label>
                <textarea value={intakeNotes} onChange={(e) => setIntakeNotes(e.target.value)} className={cn(inputCls, "h-16 resize-none")} placeholder="ملاحظات إضافية" />
              </div>
              <div className="flex gap-2">
                <button type="submit" disabled={intakeMutation.isPending || sendToQueueMutation.isPending} className="flex items-center gap-1.5 px-4 py-2 text-xs font-bold rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] transition disabled:opacity-50">
                  <Save className="w-3.5 h-3.5" />
                  {(intakeMutation.isPending || sendToQueueMutation.isPending) ? "جارٍ الحفظ..." : "تسجيل الوصول وإدخال الطابور"}
                </button>
              </div>
            </form>
          )}

          {(nextAction === "SendToQueue" || nextAction === "CallPatient" || nextAction === "EnterRoom" || nextAction === "StartVisit") && (
            <div className="flex gap-2">
              <button
                onClick={() => onSimpleAction(selectedItem)}
                disabled={nextAction === "SendToQueue" ? sendToQueueMutation.isPending : nextAction === "StartVisit" ? startVisitMutation.isPending : false}
                className={cn("flex items-center gap-1.5 px-5 py-2.5 text-sm font-bold rounded-lg text-white transition disabled:opacity-50", ACTION_COLORS[nextAction] ?? "bg-gray-400")}
              >
                {nextAction === "SendToQueue" && <ArrowRight className="w-4 h-4" />}
                {nextAction === "CallPatient" && <Megaphone className="w-4 h-4" />}
                {nextAction === "EnterRoom" && <DoorOpen className="w-4 h-4" />}
                {nextAction === "StartVisit" && <PlayCircle className="w-4 h-4" />}
                {(nextAction === "SendToQueue" && sendToQueueMutation.isPending) || (nextAction === "StartVisit" && startVisitMutation.isPending) ? "جارٍ التنفيذ..." : ACTION_LABELS[nextAction] ?? nextAction}
              </button>
            </div>
          )}

          {nextAction === "InProgress" && (
            <div className="flex items-center gap-2 bg-emerald-50 border border-emerald-200 rounded-lg px-3 py-2.5">
              <Stethoscope className="w-5 h-5 text-emerald-600" />
              <span className="text-sm font-semibold text-emerald-800">المريض عند الطبيب حاليًا</span>
            </div>
          )}

          {nextAction === "Handoff" && isDoctor && (
            <div className="bg-teal-50 border border-teal-200 rounded-lg px-3 py-2.5 text-sm text-teal-800 font-medium flex items-center gap-2">
              <ArrowRight className="w-4 h-4" />
              املأ نموذج التسليم أدناه
            </div>
          )}

          {nextAction === "Checkout" && (
            <div className="bg-green-50 border border-green-200 rounded-lg px-3 py-2.5 text-sm text-green-800 font-medium flex items-center gap-2">
              <CreditCard className="w-4 h-4" />
              املأ نموذج إنهاء الحساب أدناه
            </div>
          )}
        </div>
      )}
    </div>
  );
}

// ─── 4. Medical Safety Alerts ────────────────────────────────────────────────

function MedicalAlertsCard({ alerts }: { alerts: MedicalAlert[] }) {
  if (!alerts || alerts.length === 0) {
    return (
      <div className="bg-green-50 rounded-xl border border-green-200 p-4 hover:shadow-sm transition-shadow">
        <div className="flex flex-col items-center justify-center py-4 text-center">
          <CheckCircle2 className="w-8 h-8 text-green-500 mb-2" />
          <p className="text-sm text-green-700 font-bold">✓ لا توجد تنبيهات طبية</p>
          <p className="text-[10px] text-green-600 mt-0.5">المريض لا يعاني من حساسيات أو أمراض مسجلة</p>
        </div>
      </div>
    );
  }

  return (
    <div className="rounded-xl border border-red-200 bg-[#fcebeb]/50 p-4 hover:shadow-sm transition-shadow">
      <SectionHeader icon={ShieldAlert} title="تنبيهات طبية" color="#a32d2d" />
      <div className="flex flex-wrap gap-1.5">
        {alerts.map((alert, i) => {
          const style = SEVERITY_STYLES[alert.severity] ?? SEVERITY_STYLES.info;
          return (
            <span key={i} className={cn("inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-[10px] font-bold border", style.bg, style.border, style.text)}>
              <AlertTriangle className="w-3 h-3" />
              {alert.label}{alert.value ? `: ${alert.value}` : ""}
            </span>
          );
        })}
      </div>
    </div>
  );
}

// ─── 5. Clinical Visit Card ──────────────────────────────────────────────────

function ClinicalVisitCard({ summary, isReception, isAccountant }: { summary: DailyJourneySummary; isReception: boolean; isAccountant: boolean }) {
  const visit = summary.todayVisit;
  const hideClinical = isReception || isAccountant;

  if (!visit) {
    return (
      <div className="bg-white rounded-xl border border-gray-200 p-4 hover:shadow-sm transition-shadow">
        <SectionHeader icon={Stethoscope} title="الزيارة السريرية" />
        <div className="flex flex-col items-center justify-center py-6 text-center">
          <Stethoscope className="w-10 h-10 text-gray-200 mb-2" />
          <p className="text-sm text-gray-500 font-medium">لم تبدأ الزيارة بعد</p>
          <p className="text-[10px] text-gray-400 mt-0.5">ستظهر التفاصيل السريرية بعد بدء الزيارة</p>
        </div>
      </div>
    );
  }

  return (
    <div className="bg-white rounded-xl border border-gray-200 p-4 hover:shadow-sm transition-shadow">
      <SectionHeader icon={Stethoscope} title="الزيارة السريرية" />
      {visit.checkoutStatus && (
        <div className="mb-2">
          <span className={cn("text-[10px] px-2 py-0.5 rounded-full font-medium", visit.checkoutStatus === "Completed" ? "bg-green-50 text-green-700" : visit.checkoutStatus === "ReadyForCheckout" ? "bg-amber-50 text-amber-700" : "bg-gray-50 text-gray-700")}>
            {visit.checkoutStatus === "Completed" ? "مكتمل" : visit.checkoutStatus === "ReadyForCheckout" ? "جاهز للحساب" : visit.checkoutStatus}
          </span>
        </div>
      )}
      <div className="space-y-2.5 text-[12px]">
        {visit.chiefComplaint && (
          <div className="bg-amber-50/50 rounded-lg p-2.5">
            <span className="text-[10px] text-amber-700 font-semibold">الشكوى الرئيسية</span>
            <p className="text-gray-800 font-medium">{visit.chiefComplaint}</p>
          </div>
        )}
        {!hideClinical && visit.diagnosis && (
          <div className="bg-blue-50/50 rounded-lg p-2.5">
            <span className="text-[10px] text-[#185fa5] font-semibold">التشخيص</span>
            <p className="text-gray-800 font-medium">{visit.diagnosis}</p>
          </div>
        )}
        {!hideClinical && visit.treatmentDone && (
          <div className="bg-emerald-50/50 rounded-lg p-2.5">
            <span className="text-[10px] text-emerald-700 font-semibold">ما تم عمله</span>
            <p className="text-gray-800 font-medium">{visit.treatmentDone}</p>
          </div>
        )}
        {!hideClinical && visit.instructions && (
          <div className="bg-purple-50/50 rounded-lg p-2.5">
            <span className="text-[10px] text-purple-700 font-semibold">تعليمات للمريض</span>
            <p className="text-gray-800 font-medium">{visit.instructions}</p>
          </div>
        )}
        {!hideClinical && visit.nextVisitPlan && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">خطة الزيارة القادمة</span>
            <p className="text-gray-800 font-medium">{visit.nextVisitPlan}</p>
          </div>
        )}
        {visit.cost != null && (
          <div className="bg-gray-50 rounded-lg p-2.5">
            <span className="text-[10px] text-gray-600 font-semibold">التكلفة</span>
            <p className="text-gray-800 font-bold text-sm">{fmtRial(visit.cost)}</p>
          </div>
        )}
      </div>
    </div>
  );
}

// ─── 6. Department Handoff Card ──────────────────────────────────────────────

function DepartmentHandoffCard({ summary }: { summary: DailyJourneySummary }) {
  const activeNode = getHandoffActiveNode(summary.journeyStep);

  return (
    <div className="bg-white rounded-xl border border-gray-200 p-4 hover:shadow-sm transition-shadow">
      <SectionHeader icon={Route} title="مسار الأقسام" color="#f5922e" />
      <div className="flex items-center justify-between gap-0 mb-4 overflow-x-auto py-2">
        {HANDOFF_NODES.map((node, idx) => {
          const isActive = node.key === activeNode;
          const isDone = HANDOFF_NODES.findIndex((n) => n.key === activeNode) > idx;
          return (
            <div key={node.key} className="flex items-center flex-1 min-w-0">
              <div className="flex flex-col items-center gap-1 flex-1">
                <div className={cn(
                  "w-9 h-9 rounded-full flex items-center justify-center text-[10px] font-bold border-2 transition-all",
                  isDone && "bg-[#3d7ab5] text-white border-[#3d7ab5]",
                  isActive && "bg-white text-[#f5922e] border-[#f5922e] shadow-[0_0_12px_rgba(245,146,46,0.4)]",
                  !isDone && !isActive && "bg-gray-50 text-gray-400 border-gray-200"
                )}>
                  {isDone ? <CheckCircle2 className="w-4 h-4" /> : idx + 1}
                </div>
                <span className={cn("text-[9px] text-center leading-tight font-medium whitespace-nowrap", isActive ? "text-[#f5922e] font-bold" : isDone ? "text-[#3d7ab5]" : "text-gray-400")}>
                  {node.label}
                </span>
              </div>
              {idx < HANDOFF_NODES.length - 1 && (
                <div className={cn("flex-1 h-[3px] min-w-[8px] rounded-full mx-1", isDone ? "bg-[#3d7ab5]" : "bg-gray-200")} />
              )}
            </div>
          );
        })}
      </div>
      <div className="flex flex-wrap gap-1.5">
        {DEPARTMENT_CHIPS.map((chip) => {
          const isOrtho = chip.key === "ortho";
          const hasOrthoCase = isOrtho && summary.activeOrthoCase;
          return (
            <Link key={chip.key} href={hasOrthoCase ? `/ortho/${summary.activeOrthoCase!.id}` : "#"} onClick={(e) => { if (!hasOrthoCase) e.preventDefault(); }}
              className={cn("text-[10px] px-2.5 py-1 rounded-full font-medium border transition-colors", hasOrthoCase ? "bg-[#3d7ab5]/10 text-[#3d7ab5] border-[#3d7ab5]/30 hover:bg-[#3d7ab5]/20 cursor-pointer" : "bg-gray-50 text-gray-500 border-gray-200")}>
              {chip.label}
              {hasOrthoCase && <ExternalLink className="w-2.5 h-2.5 inline mr-0.5" />}
            </Link>
          );
        })}
      </div>
    </div>
  );
}

// ─── 6b. Handoff Form Card (Doctor) ──────────────────────────────────────────

function HandoffFormCard({ handoffForm, setHandoffForm, handoffMutation, onSubmit }: {
  handoffForm: { treatmentDone: string; diagnosis: string; nextVisitPlan: string; instructions: string; amountDue: number; notes: string };
  setHandoffForm: (v: { treatmentDone: string; diagnosis: string; nextVisitPlan: string; instructions: string; amountDue: number; notes: string }) => void;
  handoffMutation: { isPending: boolean };
  onSubmit: (e: React.FormEvent) => void;
}) {
  return (
    <div className="bg-white rounded-xl border-2 border-teal-200 p-4 hover:shadow-sm transition-shadow">
      <SectionHeader icon={ArrowRight} title="نموذج التسليم" color="#0d9488" />
      <form onSubmit={onSubmit} className="space-y-3">
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
          <div>
            <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">ما تم عمله</label>
            <textarea value={handoffForm.treatmentDone} onChange={(e) => setHandoffForm({ ...handoffForm, treatmentDone: e.target.value })} className={cn(inputCls, "h-16 resize-none")} />
          </div>
          <div>
            <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">التشخيص</label>
            <input value={handoffForm.diagnosis} onChange={(e) => setHandoffForm({ ...handoffForm, diagnosis: e.target.value })} className={inputCls} />
          </div>
          <div>
            <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">تعليمات للمريض</label>
            <textarea value={handoffForm.instructions} onChange={(e) => setHandoffForm({ ...handoffForm, instructions: e.target.value })} className={cn(inputCls, "h-16 resize-none")} />
          </div>
          <div>
            <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">خطة الزيارة القادمة</label>
            <input value={handoffForm.nextVisitPlan} onChange={(e) => setHandoffForm({ ...handoffForm, nextVisitPlan: e.target.value })} className={inputCls} />
          </div>
        </div>
        <div>
          <label className="block text-[10px] font-bold text-gray-600 mb-0.5">المبلغ المطلوب</label>
          <input type="number" value={handoffForm.amountDue} onChange={(e) => setHandoffForm({ ...handoffForm, amountDue: parseInt(e.target.value) || 0 })} className={inputCls} dir="ltr" min={0} />
        </div>
        <div>
          <label className="block text-[10px] font-bold text-gray-600 mb-0.5">ملاحظات</label>
          <textarea value={handoffForm.notes} onChange={(e) => setHandoffForm({ ...handoffForm, notes: e.target.value })} className={cn(inputCls, "h-14 resize-none")} placeholder="ملاحظات إضافية" />
        </div>
        <div className="flex gap-2">
          <button type="submit" disabled={handoffMutation.isPending} className="flex items-center gap-1.5 px-4 py-2 text-xs font-bold rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] transition disabled:opacity-50">
            <Save className="w-3.5 h-3.5" />
            {handoffMutation.isPending ? "جارٍ التسليم..." : "تسليم للاستقبال"}
          </button>
        </div>
      </form>
    </div>
  );
}

// ─── 7b. Checkout Form Card ──────────────────────────────────────────────────

function CheckoutFormCard({ selectedItem, services, checkoutAmount, setCheckoutAmount, checkoutPayment, setCheckoutPayment, checkoutNextDate, setCheckoutNextDate, checkoutNextService, setCheckoutNextService, checkoutNotes, setCheckoutNotes, checkoutSuccess, draftInvoiceResult, checkoutMutation, draftInvoiceMutation, onCheckoutSubmit, onCreateDraftInvoice }: {
  selectedItem: JourneyItem | null;
  services: ServiceOption[];
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
  checkoutMutation: { isPending: boolean };
  draftInvoiceMutation: { isPending: boolean };
  onCheckoutSubmit: (e: React.FormEvent) => void;
  onCreateDraftInvoice: () => void;
}) {
  return (
    <div className="bg-white rounded-xl border-2 border-green-200 p-4 hover:shadow-sm transition-shadow">
      <SectionHeader icon={CreditCard} title="نموذج إنهاء الحساب" color="#16a34a" />
      {checkoutSuccess ? (
        <div className="space-y-3">
          <div className="flex items-center gap-2 bg-green-50 border border-green-200 rounded-lg px-3 py-2.5">
            <CheckCircle2 className="w-5 h-5 text-green-600 flex-shrink-0" />
            <span className="text-sm font-medium text-green-800">تم إنهاء حساب الزيارة بنجاح</span>
          </div>
          {draftInvoiceResult && (
            <div className="border border-green-200 bg-green-50 rounded-lg p-2.5 space-y-1">
              <p className="text-xs font-medium text-green-800">الفاتورة: <span className="font-mono">{draftInvoiceResult.invoiceNumber}</span></p>
              <Link href={`/finance/invoices/${draftInvoiceResult.invoiceId}`} className="text-xs text-[#3d7ab5] hover:underline flex items-center gap-1">
                <FileText className="w-3 h-3" /> عرض الفاتورة
              </Link>
            </div>
          )}
        </div>
      ) : (
        <form onSubmit={onCheckoutSubmit} className="space-y-3">
          {selectedItem?.checkoutStatus === "ReadyForCheckout" && !draftInvoiceResult && (
            <div className="border border-[#3d7ab5]/20 bg-[#f7fafd] rounded-lg p-2.5 flex items-center justify-between">
              <div className="flex items-center gap-1.5 text-xs font-medium text-[#1a3a5c]">
                <FileText className="w-3.5 h-3.5 text-[#3d7ab5]" /> إنشاء فاتورة مسودة
              </div>
              <button type="button" disabled={draftInvoiceMutation.isPending} onClick={onCreateDraftInvoice} className="flex items-center gap-1 px-2.5 py-1 text-[10px] rounded-lg bg-[#3d7ab5] text-white hover:opacity-90 disabled:opacity-50 transition">
                <FileText className="w-3 h-3" /> {draftInvoiceMutation.isPending ? "جارٍ..." : "إنشاء فاتورة"}
              </button>
            </div>
          )}
          {draftInvoiceResult && (
            <div className="border border-green-200 bg-green-50 rounded-lg p-2.5 space-y-1">
              <p className="text-xs font-medium text-green-800">تم إنشاء الفاتورة: <span className="font-mono">{draftInvoiceResult.invoiceNumber}</span></p>
              <Link href={`/finance/invoices/${draftInvoiceResult.invoiceId}`} className="text-[10px] text-[#3d7ab5] hover:underline flex items-center gap-1">
                <FileText className="w-3 h-3" /> عرض الفاتورة
              </Link>
            </div>
          )}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
            <div>
              <label className="block text-[10px] font-bold text-gray-600 mb-0.5">المبلغ المطلوب (مرجعي)</label>
              <input type="number" value={checkoutAmount} onChange={(e) => setCheckoutAmount(parseInt(e.target.value) || 0)} className={inputCls} dir="ltr" min={0} />
            </div>
            <div>
              <label className="block text-[10px] font-bold text-gray-600 mb-0.5">طريقة الدفع</label>
              <select value={checkoutPayment} onChange={(e) => setCheckoutPayment(e.target.value)} className={inputCls}>
                {PAYMENT_METHODS.map((m) => (<option key={m.value} value={m.value}>{m.label}</option>))}
              </select>
            </div>
            <div>
              <label className="block text-[10px] font-bold text-gray-600 mb-0.5">حجز موعد قادم</label>
              <input type="date" value={checkoutNextDate} onChange={(e) => setCheckoutNextDate(e.target.value)} className={inputCls} dir="ltr" />
            </div>
            <div>
              <label className="block text-[10px] font-bold text-gray-600 mb-0.5">الخدمة القادمة</label>
              <select value={checkoutNextService} onChange={(e) => setCheckoutNextService(e.target.value)} className={inputCls}>
                <option value="">— اختر خدمة —</option>
                {services.map((s) => (<option key={s.id} value={s.id}>{s.arabicName}</option>))}
              </select>
            </div>
          </div>
          <div>
            <label className="block text-[10px] font-bold text-gray-600 mb-0.5">ملاحظات</label>
            <textarea value={checkoutNotes} onChange={(e) => setCheckoutNotes(e.target.value)} className={cn(inputCls, "h-14 resize-none")} placeholder="ملاحظات إضافية" />
          </div>
          <div className="flex gap-2">
            <button type="submit" disabled={checkoutMutation.isPending} className="flex items-center gap-1.5 px-4 py-2 text-xs font-bold rounded-lg bg-green-600 text-white hover:bg-green-700 transition disabled:opacity-50">
              <CreditCard className="w-3.5 h-3.5" /> {checkoutMutation.isPending ? "جارٍ الإنهاء..." : "إنهاء الحساب"}
            </button>
          </div>
        </form>
      )}
    </div>
  );
}

// ─── 7. Finance/Checkout Card ────────────────────────────────────────────────

function FinanceCheckoutCard({ summary, isAccountant, canViewFinance, canViewInvoices }: {
  summary: DailyJourneySummary; isAccountant: boolean; canViewFinance: boolean; canViewInvoices: boolean;
}) {
  const fin = summary.financeSummary;

  if (!fin) {
    return (
      <div className="bg-white rounded-xl border border-gray-200 p-4 hover:shadow-sm transition-shadow">
        <SectionHeader icon={Wallet} title="الملف المالي" />
        <div className="flex flex-col items-center justify-center py-6 text-center">
          <Wallet className="w-10 h-10 text-gray-200 mb-2" />
          <p className="text-sm text-gray-500 font-medium">لا توجد بيانات مالية</p>
        </div>
      </div>
    );
  }

  const showFullFinance = canViewFinance;

  return (
    <div className="bg-white rounded-xl border border-gray-200 p-4 hover:shadow-sm transition-shadow">
      <SectionHeader icon={Wallet} title="الملف المالي" />
      <div className="flex items-center gap-2 mb-3">
        <span className={cn("text-[10px] px-2.5 py-1 rounded-full font-medium",
          fin.financialStatus === "paid_full" ? "bg-green-50 text-green-700" :
          fin.financialStatus === "overdue" ? "bg-red-50 text-red-700" :
          fin.financialStatus === "has_balance" ? "bg-amber-50 text-amber-700" :
          "bg-gray-50 text-gray-600"
        )}>
          {fin.financialStatus === "paid_full" ? "مسدد" : fin.financialStatus === "overdue" ? "متأخر" : fin.financialStatus === "has_balance" ? "رصيد مستحق" : "بدون خطة"}
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
        <div className="mt-2 text-[11px] text-red-600 font-medium">فواتير غير مدفوعة: {summary.unpaidInvoicesCount}</div>
      )}
      {summary.activeContract && (isAccountant || canViewFinance) && (
        <div className="mt-3 border-t border-gray-100 pt-3">
          <div className="flex items-center gap-1.5 mb-2">
            <FileText className="w-3.5 h-3.5 text-[#3d7ab5]" />
            <span className="text-[11px] font-bold text-[#1a3a5c]">عقد نشط</span>
          </div>
          <div className="grid grid-cols-2 gap-2 text-[11px]">
            <div><span className="text-[9px] text-gray-500">الإجمالي</span><p className="text-gray-800 font-medium">{fmtRial(summary.activeContract.totalAmount)}</p></div>
            <div><span className="text-[9px] text-gray-500">المدفوع</span><p className="text-green-700 font-medium">{fmtRial(summary.activeContract.paidAmount)}</p></div>
            <div><span className="text-[9px] text-gray-500">المتبقي</span><p className="text-amber-700 font-medium">{fmtRial(summary.activeContract.remainingAmount)}</p></div>
            <div><span className="text-[9px] text-gray-500">الأقساط</span><p className="text-gray-800 font-medium">{summary.activeContract.installmentsCount}</p></div>
          </div>
        </div>
      )}
    </div>
  );
}

// ─── 8. Active Ortho Case ────────────────────────────────────────────────────

function ActiveOrthoCard({ summary }: { summary: DailyJourneySummary }) {
  const ortho = summary.activeOrthoCase;

  if (!ortho) {
    return (
      <div className="bg-white rounded-xl border border-gray-200 p-4 hover:shadow-sm transition-shadow">
        <SectionHeader icon={Wrench} title="ملف التقويم" />
        <div className="flex flex-col items-center justify-center py-6 text-center">
          <Wrench className="w-10 h-10 text-gray-200 mb-2" />
          <p className="text-sm text-gray-500 font-medium">لا توجد حالة تقويم نشطة</p>
        </div>
      </div>
    );
  }

  return (
    <div className="bg-white rounded-xl border border-gray-200 p-4 hover:shadow-sm transition-shadow">
      <SectionHeader icon={Wrench} title="ملف التقويم النشط" />
      <div className="flex items-center gap-2 mb-3">
        <Link href={`/ortho/${ortho.id}`} className="mr-auto text-[10px] text-[#3d7ab5] hover:underline flex items-center gap-0.5">
          التفاصيل <ExternalLink className="w-3 h-3" />
        </Link>
      </div>
      <div className="grid grid-cols-2 gap-2 text-[12px]">
        {ortho.caseNumber && (<div><span className="text-[10px] text-gray-500 font-semibold">رقم القضية</span><p className="text-gray-800 font-medium font-mono">{ortho.caseNumber}</p></div>)}
        {ortho.applianceType && (<div><span className="text-[10px] text-gray-500 font-semibold">نوع الجهاز</span><p className="text-gray-800 font-medium">{ortho.applianceType}</p></div>)}
        {ortho.currentStage && (<div><span className="text-[10px] text-gray-500 font-semibold">المرحلة الحالية</span><p className="text-gray-800 font-medium">{ortho.currentStage}</p></div>)}
        {ortho.totalFee != null && (<div><span className="text-[10px] text-gray-500 font-semibold">الرسوم الإجمالية</span><p className="text-gray-800 font-bold">{fmtRial(ortho.totalFee)}</p></div>)}
      </div>
      {ortho.stagePercentage != null && (
        <div className="mt-2">
          <div className="flex items-center justify-between text-[10px] text-gray-600 mb-1">
            <span>نسبة الإنجاز</span>
            <span className="font-bold text-[#3d7ab5]">{ortho.stagePercentage}%</span>
          </div>
          <div className="w-full bg-gray-100 rounded-full h-1.5">
            <div className="bg-[#3d7ab5] rounded-full h-1.5 transition-all" style={{ width: `${Math.min(ortho.stagePercentage, 100)}%` }} />
          </div>
        </div>
      )}
    </div>
  );
}

// ─── 9. Timeline ─────────────────────────────────────────────────────────────

function TimelineCard({ events }: { events: TimelineEvent[] }) {
  if (!events || events.length === 0) {
    return (
      <div className="bg-white rounded-xl border border-gray-200 p-4 hover:shadow-sm transition-shadow">
        <SectionHeader icon={Clock} title="السجل الزمني" />
        <div className="flex flex-col items-center justify-center py-6 text-center">
          <Clock className="w-10 h-10 text-gray-200 mb-2" />
          <p className="text-sm text-gray-500 font-medium">لا توجد أحداث مسجلة اليوم</p>
        </div>
      </div>
    );
  }

  return (
    <div className="bg-white rounded-xl border border-gray-200 p-4 hover:shadow-sm transition-shadow">
      <SectionHeader icon={Clock} title="السجل الزمني" />
      <div className="relative space-y-0">
        {events.map((event, i) => {
          const dotColor = TIMELINE_DOT_COLORS[event.type] ?? TIMELINE_DOT_COLORS.default;
          return (
            <div key={i} className="flex gap-3 pb-3 last:pb-0">
              <div className="flex flex-col items-center flex-shrink-0">
                <div className={cn("w-2.5 h-2.5 rounded-full mt-1", dotColor)} />
                {i < events.length - 1 && (<div className="w-0.5 flex-1 bg-gray-200 mt-0.5" />)}
              </div>
              <div className="flex-1 min-w-0 -mt-0.5">
                <div className="flex items-center gap-2">
                  <span className="text-[11px] font-semibold text-gray-800 truncate">{event.title}</span>
                  {event.status && (<span className="text-[9px] px-1.5 py-0.5 rounded-full bg-gray-100 text-gray-600 font-medium">{event.status}</span>)}
                </div>
                {event.sub && (<p className="text-[10px] text-gray-500 truncate">{event.sub}</p>)}
                <span className="text-[9px] text-gray-400">{fmtDate(event.date)}</span>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

// ─── 10. Quick Actions ───────────────────────────────────────────────────────

function QuickActionsCard({ patientId, activeOrthoCase, canViewFinance, canViewVisits }: {
  patientId: string | null; activeOrthoCase: DailyJourneyOrtho | null; canViewFinance: boolean; canViewVisits: boolean;
}) {
  const actions: Array<{ label: string; href?: string; onClick?: () => void; icon: React.ElementType }> = [
    { label: "فتح ملف المريض", href: `/patients/${patientId}`, icon: Users },
    { label: "صفحة الرحلة الكاملة", href: `/patient-journey/${patientId}`, icon: Route },
    { label: "موعد جديد", href: "/appointments", icon: CalendarDays },
    ...(canViewFinance ? [{ label: "فاتورة/دفعة", href: "/payments", icon: CreditCard }] : []),
    ...(activeOrthoCase && canViewVisits ? [{ label: "ملف التقويم", href: `/ortho/${activeOrthoCase.id}`, icon: Wrench }] : []),
    { label: "طباعة ملخص الزيارة", onClick: () => { window.print(); }, icon: Printer },
  ];

  return (
    <div className="bg-white rounded-xl border border-gray-200 p-4 hover:shadow-sm transition-shadow">
      <SectionHeader icon={CircleDot} title="إجراءات سريعة" color="#f5922e" />
      <div className="grid grid-cols-2 sm:grid-cols-3 gap-2">
        {actions.map((action, i) => {
          if (action.href) {
            return (
              <Link key={i} href={action.href}
                className="flex items-center gap-2 px-3 py-2.5 rounded-lg border border-gray-200 bg-gray-50 text-[11px] font-medium text-[#1a3a5c] hover:bg-[#f0f6fc] hover:border-[#3d7ab5]/30 transition-colors"
              >
                <action.icon className="w-4 h-4 text-[#3d7ab5]" />
                <span>{action.label}</span>
              </Link>
            );
          }
          return (
            <button key={i} type="button" onClick={action.onClick}
              className="flex items-center gap-2 px-3 py-2.5 rounded-lg border border-gray-200 bg-gray-50 text-[11px] font-medium text-[#1a3a5c] hover:bg-[#f0f6fc] hover:border-[#3d7ab5]/30 transition-colors"
            >
              <action.icon className="w-4 h-4 text-[#3d7ab5]" />
              <span>{action.label}</span>
            </button>
          );
        })}
      </div>
    </div>
  );
}
