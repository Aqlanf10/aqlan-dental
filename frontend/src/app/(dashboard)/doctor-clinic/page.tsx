"use client";

import { useState, useCallback, useMemo, useEffect, useRef } from "react";
import {
  Stethoscope, Play, ClipboardCheck, ListChecks, GitBranch,
  Image, Pill, FlaskConical, CalendarClock, Send, RefreshCw,
  Search, X, AlertCircle, CheckCircle, Clock,
  Activity, Loader2, FileText, User, Eye,
  CreditCard, ArrowRight, Route, DoorOpen, UserCheck,
  Wallet, Printer, MessageCircle, Zap, History, Bell,
} from "lucide-react";
import { useAuthStore } from "@/stores/authStore";
import { toast } from "@/stores/toastStore";
import { useSignalRClinicQueue } from "@/hooks/useSignalRClinicQueue";
import {
  NAVY, BLUE, ORANGE,
  fmtRial, fmtTime, getTodayStr,
  APPT_STATUS_LABELS, STATUS_COLORS,
  isDoctorRole, fmtSessionDuration,
} from "../daily-operations/_lib/constants";

import {
  useDoctorPatientsToday,
  useDoctorPatientSummary,
  useDoctorServices,
  useStartVisit,
  useHandoffToReception,
  type DoctorPatientItem,
  type ServiceWithPrice,
} from "./_lib/hooks";

import {
  StartVisitModal,
  PricedProceduresModal,
  ExaminationModal,
  TreatmentPlanModal,
  OrthoFollowUpModal,
  ImagesRadiographsModal,
  LabOrderModal,
  PrescriptionModal,
  HandoffConfirmModal,
  FollowUpSuggestModal,
} from "./_components/Modals";

/* ═══════════════════════════════════════════════════════════════════════════
   Animation styles
   ═══════════════════════════════════════════════════════════════════════════ */
const animationStyles = `
@keyframes fadeInUp {
  from { transform: translateY(8px); opacity: 0; }
  to { transform: translateY(0); opacity: 1; }
}
@keyframes pulseGlow {
  0%, 100% { box-shadow: 0 0 0 0 rgba(245,146,46,0.3); }
  50% { box-shadow: 0 0 12px 4px rgba(245,146,46,0.15); }
}
.animate-fade-in-up { animation: fadeInUp 0.25s ease-out; }
.animate-pulse-glow { animation: pulseGlow 2s ease-in-out infinite; }
`;

/* ═══════════════════════════════════════════════════════════════════════════
   Patient Detail Tabs
   ═══════════════════════════════════════════════════════════════════════════ */
type PatientTab = "journey" | "visit" | "ortho" | "history" | "payment" | "timeline";

const PATIENT_TABS: { key: PatientTab; label: string; icon: React.ElementType }[] = [
  { key: "journey",  label: "الرحلة",         icon: Route },
  { key: "visit",    label: "تسجيل الزيارة",   icon: Stethoscope },
  { key: "ortho",    label: "التقويم",         icon: Zap },
  { key: "history",  label: "التاريخ الطبي",   icon: History },
  { key: "payment",  label: "الدفع",           icon: CreditCard },
  { key: "timeline", label: "السجل",           icon: Clock },
];

/* ═══════════════════════════════════════════════════════════════════════════
   Flow Steps for Patient Journey
   ═══════════════════════════════════════════════════════════════════════════ */
const flowSteps = [
  { key: "Arrived",    label: "وصل",     icon: UserCheck },
  { key: "Waiting",    label: "انتظار",   icon: Clock },
  { key: "Called",     label: "نداء",     icon: Bell },
  { key: "InRoom",     label: "الغرفة",   icon: DoorOpen },
  { key: "InProgress", label: "الطبيب",   icon: Stethoscope },
  { key: "Checkout",   label: "الدفع",    icon: CreditCard },
  { key: "Completed",  label: "مكتمل",    icon: CheckCircle },
];

const STATUS_FLOW_MAP: Record<string, number> = {
  Arrived: 0, Waiting: 1, Called: 2, InRoom: 3, InProgress: 4, Checkout: 5, Completed: 6,
};

/* ═══════════════════════════════════════════════════════════════════════════
   Helper: format money
   ═══════════════════════════════════════════════════════════════════════════ */
function formatMoney(value: number) {
  return new Intl.NumberFormat("ar-YE").format(value || 0) + " ر.ي";
}

/* ═══════════════════════════════════════════════════════════════════════════
   Action card definitions (for quick actions in the visit tab)
   ═══════════════════════════════════════════════════════════════════════════ */
interface ActionCardDef {
  key: string;
  label: string;
  icon: React.ElementType;
  color: string;
  bgColor: string;
  requiredStatus: string[];
  description: string;
}

const ACTION_CARDS: ActionCardDef[] = [
  {
    key: "startVisit",
    label: "بدء الزيارة",
    icon: Play,
    color: "#16a34a",
    bgColor: "#f0fdf4",
    requiredStatus: ["InRoom", "Called"],
    description: "بدء جلسة العلاج مع المريض",
  },
  {
    key: "examination",
    label: "الفحص والتشخيص",
    icon: ClipboardCheck,
    color: "#3d7ab5",
    bgColor: "#f0f5fb",
    requiredStatus: ["InRoom", "InProgress"],
    description: "تسجيل الفحص السريري والتشخيص",
  },
  {
    key: "procedures",
    label: "الإجراءات المسعّرة",
    icon: ListChecks,
    color: "#9333ea",
    bgColor: "#faf5ff",
    requiredStatus: ["InRoom", "InProgress"],
    description: "اختيار الخدمات من كتالوج الأسعار",
  },
  {
    key: "treatmentPlan",
    label: "خطة العلاج",
    icon: FileText,
    color: "#2563eb",
    bgColor: "#eff6ff",
    requiredStatus: ["InRoom", "InProgress"],
    description: "إعداد أو تحديث خطة العلاج",
  },
  {
    key: "orthoFollowUp",
    label: "متابعة التقويم",
    icon: GitBranch,
    color: "#f5922e",
    bgColor: "#fff7ed",
    requiredStatus: ["InRoom", "InProgress"],
    description: "تسجيل متابعة جلسة التقويم",
  },
  {
    key: "images",
    label: "الأشعة والصور",
    icon: Image,
    color: "#64748b",
    bgColor: "#f8fafc",
    requiredStatus: ["InRoom", "InProgress"],
    description: "عرض أو رفع أشعة وصور",
  },
  {
    key: "prescription",
    label: "الوصفة والتعليمات",
    icon: Pill,
    color: "#dc2626",
    bgColor: "#fef2f2",
    requiredStatus: ["InRoom", "InProgress"],
    description: "كتابة وصفة طبية وتعليمات",
  },
  {
    key: "labOrder",
    label: "طلب مختبر / إحالة",
    icon: FlaskConical,
    color: "#0891b2",
    bgColor: "#ecfeff",
    requiredStatus: ["InRoom", "InProgress"],
    description: "إنشاء طلب مختبر أو إحالة داخلية",
  },
  {
    key: "followUp",
    label: "موعد متابعة",
    icon: CalendarClock,
    color: "#7c3aed",
    bgColor: "#f5f3ff",
    requiredStatus: ["InRoom", "InProgress"],
    description: "اقتراح موعد متابعة قادم",
  },
];

/* ═══════════════════════════════════════════════════════════════════════════
   MAIN PAGE — Doctor Clinic Workspace (Redesigned with Patient Tabs)
   ═══════════════════════════════════════════════════════════════════════════ */
export default function DoctorClinicPage() {
  const { user } = useAuthStore();
  const userRole = user?.role ?? "";
  const doctorId = user?.doctorId ?? user?.id ?? "";

  // ── SignalR ──
  const { isConnected: signalrConnected } = useSignalRClinicQueue();

  // ── Data ──
  const { data: patients = [], isLoading, refetch } = useDoctorPatientsToday(doctorId);
  const { data: services = [] } = useDoctorServices();
  const startVisitMutation = useStartVisit();
  const handoffMutation = useHandoffToReception();

  // ── Selected patient ──
  const [selectedPatient, setSelectedPatient] = useState<DoctorPatientItem | null>(null);
  const activePatientId = selectedPatient?.patientId ?? null;
  const { data: selectedSummary } = useDoctorPatientSummary(activePatientId);

  // ── Active patient detail tab ──
  const [activeTab, setActiveTab] = useState<PatientTab>("journey");

  // ── Selected services for visit ──
  const [selectedServices, setSelectedServices] = useState<ServiceWithPrice[]>([]);

  // ── Modal state ──
  const [startVisitModalOpen, setStartVisitModalOpen] = useState(false);
  const [examinationModalOpen, setExaminationModalOpen] = useState(false);
  const [proceduresModalOpen, setProceduresModalOpen] = useState(false);
  const [treatmentPlanModalOpen, setTreatmentPlanModalOpen] = useState(false);
  const [orthoFollowUpModalOpen, setOrthoFollowUpModalOpen] = useState(false);
  const [prescriptionModalOpen, setPrescriptionModalOpen] = useState(false);
  const [followUpModalOpen, setFollowUpModalOpen] = useState(false);
  const [imagesModalOpen, setImagesModalOpen] = useState(false);
  const [labOrderModalOpen, setLabOrderModalOpen] = useState(false);
  const [handoffModalOpen, setHandoffModalOpen] = useState(false);

  // ── Search ──
  const [searchQuery, setSearchQuery] = useState("");
  const searchRef = useRef<HTMLInputElement>(null);

  // ── Clinical notes (accumulated before handoff) ──
  const [clinicalNotes, setClinicalNotes] = useState({
    diagnosis: "",
    treatmentDone: "",
    instructions: "",
    nextVisitPlan: "",
    amountDue: 0,
    suggestedServiceId: "",
    followUpDate: "",
  });

  // ── Computed patient groups ──
  const inClinicPatients = useMemo(() =>
    patients.filter(p =>
      p.appointmentStatus === "InRoom" ||
      p.appointmentStatus === "InProgress" ||
      p.queueStatus === "InRoom" ||
      p.queueStatus === "InProgress"
    ), [patients]);

  const waitingPatients = useMemo(() =>
    patients.filter(p =>
      p.queueStatus === "Waiting" ||
      p.queueStatus === "Called" ||
      (p.appointmentStatus === "Waiting" && !p.queueStatus)
    ), [patients]);

  const scheduledPatients = useMemo(() =>
    patients.filter(p =>
      p.appointmentStatus === "Scheduled" ||
      p.appointmentStatus === "Confirmed" ||
      p.appointmentStatus === "Arrived"
    ), [patients]);

  const completedPatients = useMemo(() =>
    patients.filter(p =>
      p.appointmentStatus === "Completed" ||
      p.checkoutStatus === "ReadyForCheckout"
    ), [patients]);

  // ── Filtered patients for list ──
  const filteredPatients = useMemo(() => {
    if (!searchQuery.trim()) return patients;
    const q = searchQuery.trim().toLowerCase();
    return patients.filter(p =>
      p.patientName.toLowerCase().includes(q) ||
      (p.serviceName && p.serviceName.toLowerCase().includes(q))
    );
  }, [patients, searchQuery]);

  // ── Medical alerts from summary ──
  const medicalAlerts = selectedSummary?.medicalAlerts ?? [];

  // ── Is current patient in progress ──
  const isPatientActive = selectedPatient
    ? ["InRoom", "InProgress"].includes(selectedPatient.appointmentStatus) ||
      ["InRoom", "InProgress"].includes(selectedPatient.queueStatus ?? "")
    : false;

  // ── Selected services total ──
  const selectedServicesTotal = useMemo(() =>
    selectedServices.reduce((sum, s) => sum + (s.defaultPrice ?? 0), 0),
    [selectedServices]
  );

  // ── Keyboard shortcuts ──
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        setSelectedPatient(null);
        return;
      }
      if (e.ctrlKey && e.key === "r") {
        e.preventDefault();
        refetch();
        return;
      }
      if (e.ctrlKey && e.key === "f") {
        e.preventDefault();
        searchRef.current?.focus();
        return;
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [refetch]);

  // ── Action handler ──
  const handleAction = useCallback((cardKey: string) => {
    if (!selectedPatient) {
      toast.error("اختر مريضاً أولاً");
      return;
    }

    const card = ACTION_CARDS.find(c => c.key === cardKey);
    if (card && !card.requiredStatus.includes(selectedPatient.appointmentStatus) &&
        !card.requiredStatus.includes(selectedPatient.queueStatus ?? "")) {
      toast.error("هذا الإجراء غير متاح لحالة المريض الحالية");
      return;
    }

    switch (cardKey) {
      case "startVisit": setStartVisitModalOpen(true); break;
      case "examination": setExaminationModalOpen(true); break;
      case "procedures": setProceduresModalOpen(true); break;
      case "treatmentPlan": setTreatmentPlanModalOpen(true); break;
      case "orthoFollowUp": setOrthoFollowUpModalOpen(true); break;
      case "images": setImagesModalOpen(true); break;
      case "prescription": setPrescriptionModalOpen(true); break;
      case "labOrder": setLabOrderModalOpen(true); break;
      case "followUp": setFollowUpModalOpen(true); break;
    }
  }, [selectedPatient]);

  // ── Start visit handler ──
  const handleStartVisit = useCallback(async () => {
    if (!selectedPatient) return;
    try {
      await startVisitMutation.mutateAsync(selectedPatient.appointmentId);
      toast.success("تم بدء الزيارة بنجاح");
      setStartVisitModalOpen(false);
    } catch {
      toast.error("فشل بدء الزيارة");
    }
  }, [selectedPatient, startVisitMutation]);

  // ── Toggle service selection ──
  const handleToggleService = useCallback((service: ServiceWithPrice) => {
    setSelectedServices(prev => {
      const exists = prev.some(s => s.id === service.id);
      if (exists) return prev.filter(s => s.id !== service.id);
      return [...prev, service];
    });
  }, []);

  // ── Handoff to reception ──
  const handleHandoff = useCallback(async () => {
    if (!selectedPatient?.visitId) {
      toast.error("لا توجد زيارة نشطة");
      return;
    }
    try {
      await handoffMutation.mutateAsync({
        visitId: selectedPatient.visitId,
        body: {
          treatmentDone: clinicalNotes.treatmentDone || undefined,
          diagnosis: clinicalNotes.diagnosis || undefined,
          nextVisitPlan: clinicalNotes.nextVisitPlan || undefined,
          instructions: clinicalNotes.instructions || undefined,
          followUpDate: clinicalNotes.followUpDate || undefined,
          amountDue: clinicalNotes.amountDue || undefined,
          suggestedServiceId: clinicalNotes.suggestedServiceId || undefined,
        },
      });
      toast.success("تم إرسال المريض للاستقبال للتحصيل والخروج");
      setHandoffModalOpen(false);
      setSelectedPatient(null);
      setSelectedServices([]);
      setClinicalNotes({
        diagnosis: "", treatmentDone: "", instructions: "",
        nextVisitPlan: "", amountDue: 0, suggestedServiceId: "", followUpDate: "",
      });
    } catch {
      toast.error("فشل إرسال المريض للاستقبال");
    }
  }, [selectedPatient, handoffMutation, clinicalNotes]);

  // ── Update clinical notes from modals ──
  const updateClinicalNotes = useCallback((updates: Partial<typeof clinicalNotes>) => {
    setClinicalNotes(prev => ({ ...prev, ...updates }));
  }, []);

  // ── Patient initials ──
  const getInitials = (name: string) => {
    const parts = name.split(" ").filter(Boolean);
    return parts.length >= 2 ? parts[0][0] + parts[1][0] : parts[0]?.[0] ?? "؟";
  };

  // ── Doctor display name ──
  const doctorName = user?.doctorName ?? user?.username ?? "الطبيب";

  // ── Get flow index for journey tab ──
  const getFlowIndex = (status: string) => STATUS_FLOW_MAP[status] ?? -1;

  // ── Render ──
  return (
    <>
      <style dangerouslySetInnerHTML={{ __html: animationStyles }} />

      <div className="h-screen flex flex-col bg-[#f8fafc] overflow-hidden">
        {/* ═══════════════════════════════════════════════════════════════
            COMMAND BAR (52px)
            ═══════════════════════════════════════════════════════════════ */}
        <div className="h-[52px] flex-shrink-0 bg-white flex items-center px-4 gap-3"
          style={{ borderBottom: "1px solid #e5e7eb", boxShadow: "0 1px 3px rgba(0,0,0,0.04)" }}>

          {/* Icon + Title */}
          <div className="flex items-center gap-2.5 flex-shrink-0">
            <div className="w-9 h-9 rounded-xl flex items-center justify-center" style={{ background: `linear-gradient(135deg, ${NAVY}, ${BLUE})` }}>
              <Stethoscope className="w-4.5 h-4.5 text-white" />
            </div>
            <div className="leading-tight">
              <div className="text-sm font-extrabold" style={{ color: NAVY }}>عيادة الطبيب</div>
              <div className="text-[10px] font-medium" style={{ color: "#94a3b8" }}>د. {doctorName}</div>
            </div>
          </div>

          <div className="w-px h-7" style={{ background: "#e5e7eb" }} />

          {/* Search */}
          <div className="flex-1 max-w-sm relative">
            <Search className="w-4 h-4 absolute top-1/2 right-3 -translate-y-1/2" style={{ color: "#94a3b8" }} />
            <input
              ref={searchRef}
              type="text"
              value={searchQuery}
              onChange={e => setSearchQuery(e.target.value)}
              placeholder="بحث باسم المريض..."
              className="w-full text-sm rounded-full border-0 pl-4 pr-9 py-1.5 outline-none focus:ring-2 focus:ring-[#3d7ab5]/20"
              style={{ background: "#f5f7fa", color: NAVY }}
            />
          </div>

          {/* Status chips */}
          <div className="flex items-center gap-2 text-xs font-bold">
            <span className="px-2.5 py-1 rounded-full" style={{ background: "#f0fdf4", color: "#16a34a" }}>
              {inClinicPatients.length} عيادة
            </span>
            <span className="px-2.5 py-1 rounded-full" style={{ background: "#fff7ed", color: "#f5922e" }}>
              {waitingPatients.length} انتظار
            </span>
            <span className="px-2.5 py-1 rounded-full" style={{ background: "#f0f5fb", color: "#3d7ab5" }}>
              {scheduledPatients.length} مجدول
            </span>
            <span className="px-2.5 py-1 rounded-full" style={{ background: "#f5f5f5", color: "#64748b" }}>
              {completedPatients.length} مكتمل
            </span>
          </div>

          <div className="flex-1" />

          {/* SignalR */}
          <div className="flex items-center gap-1.5 text-xs font-medium" style={{ color: signalrConnected ? "#16a34a" : "#ef4444" }}>
            <span className="w-2 h-2 rounded-full" style={{ background: signalrConnected ? "#16a34a" : "#ef4444" }} />
            <span className="hidden sm:inline">{signalrConnected ? "مباشر" : "غير متصل"}</span>
          </div>

          {/* Refresh */}
          <button onClick={() => refetch()} className="w-9 h-9 rounded-lg flex items-center justify-center transition hover:bg-gray-100" title="تحديث (Ctrl+R)">
            <RefreshCw className="w-4 h-4" style={{ color: "#64748b" }} />
          </button>
        </div>

        {/* ═══════════════════════════════════════════════════════════════
            MAIN CONTENT — Three-panel layout
            ═══════════════════════════════════════════════════════════════ */}
        <div className="flex-1 flex overflow-hidden">

          {/* ── Left Panel: Patient List ── */}
          <div className="w-[280px] flex-shrink-0 flex flex-col bg-white border-l" style={{ borderColor: "#e5e7eb" }}>
            {/* Patient list header */}
            <div className="flex-shrink-0 px-4 py-3 flex items-center justify-between" style={{ borderBottom: "1px solid #f1f5f9" }}>
              <div className="flex items-center gap-2">
                <User className="w-4 h-4" style={{ color: NAVY }} />
                <span className="text-xs font-bold" style={{ color: NAVY }}>مرضاي اليوم</span>
                <span className="text-[10px] px-1.5 py-0.5 rounded-full font-bold" style={{ background: NAVY + "15", color: NAVY }}>{patients.length}</span>
              </div>
            </div>

            {/* Patient list */}
            <div className="flex-1 overflow-y-auto">
              {isLoading && (
                <div className="flex items-center justify-center p-8">
                  <Loader2 className="w-6 h-6 animate-spin" style={{ color: BLUE }} />
                </div>
              )}
              {!isLoading && filteredPatients.length === 0 && (
                <div className="p-6 text-center text-xs font-medium" style={{ color: "#94a3b8" }}>لا يوجد مرضى</div>
              )}
              {filteredPatients.map(p => {
                const isSelected = selectedPatient?.appointmentId === p.appointmentId;
                const isActive = ["InRoom", "InProgress"].includes(p.appointmentStatus);
                const isWaiting = p.queueStatus === "Waiting" || p.queueStatus === "Called";
                const statusColors = STATUS_COLORS[p.appointmentStatus] ?? STATUS_COLORS.Scheduled;

                return (
                  <button key={p.appointmentId}
                    onClick={() => {
                      setSelectedPatient(p);
                      setActiveTab("journey");
                      setSelectedServices([]);
                      setClinicalNotes({
                        diagnosis: "", treatmentDone: "", instructions: "",
                        nextVisitPlan: "", amountDue: 0, suggestedServiceId: "", followUpDate: "",
                      });
                    }}
                    className="w-full flex items-center gap-2 px-4 py-3 text-right border-b transition-all"
                    style={{
                      background: isSelected ? "#f0f5fb" : "#fff",
                      borderRight: isSelected ? `3px solid ${BLUE}` : "3px solid transparent",
                      borderBottomColor: "#f1f5f9",
                    }}>
                    {/* Avatar */}
                    <div className="w-9 h-9 rounded-full flex items-center justify-center text-xs font-bold flex-shrink-0"
                      style={{
                        background: isActive ? "#16a34a" : isWaiting ? ORANGE : BLUE,
                        color: "#fff",
                      }}>
                      {getInitials(p.patientName)}
                    </div>
                    {/* Info */}
                    <div className="flex-1 min-w-0">
                      <p className="text-xs font-bold truncate" style={{ color: isSelected ? BLUE : NAVY }}>{p.patientName}</p>
                      <p className="text-[10px]" style={{ color: "#94a3b8" }}>{fmtTime(p.appointmentTime)} · {p.serviceName ?? "—"}</p>
                    </div>
                    {/* Status */}
                    <div className="flex flex-col items-end gap-1">
                      <span className="text-[9px] px-1.5 py-0.5 rounded-full font-bold"
                        style={{ background: statusColors.bg, color: statusColors.text }}>
                        {APPT_STATUS_LABELS[p.appointmentStatus] ?? p.appointmentStatus}
                      </span>
                      {isActive && p.inRoomSince && (
                        <span className="text-[9px] font-bold" style={{ color: "#16a34a" }}>
                          {fmtSessionDuration(p.inRoomSince)}
                        </span>
                      )}
                      {p.checkoutStatus === "ReadyForCheckout" && (
                        <span className="text-[9px] font-bold px-1 py-0.5 rounded" style={{ background: "#fef3c7", color: "#d97706" }}>
                          جاهز للدفع
                        </span>
                      )}
                    </div>
                  </button>
                );
              })}
            </div>
          </div>

          {/* ── Right Panel: Patient Detail with Tabs ── */}
          <div className="flex-1 flex flex-col min-w-0 overflow-hidden">
            {selectedPatient ? (
              <>
                {/* Patient Header Bar */}
                <div className="flex-shrink-0 bg-white px-4 py-3 flex items-center gap-4" style={{ borderBottom: "1px solid #e5e7eb" }}>
                  {/* Avatar */}
                  <div className="w-12 h-12 rounded-2xl flex items-center justify-center text-base font-bold flex-shrink-0"
                    style={{ background: `linear-gradient(135deg, ${NAVY}, ${BLUE})`, color: "#fff" }}>
                    {getInitials(selectedPatient.patientName)}
                  </div>
                  {/* Info */}
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <h2 className="text-base font-extrabold" style={{ color: NAVY }}>{selectedPatient.patientName}</h2>
                      <span className="text-[10px] font-bold px-2 py-0.5 rounded-full"
                        style={{ background: STATUS_COLORS[selectedPatient.appointmentStatus]?.bg ?? "#f5f5f5", color: STATUS_COLORS[selectedPatient.appointmentStatus]?.text ?? "#64748b" }}>
                        {APPT_STATUS_LABELS[selectedPatient.appointmentStatus] ?? selectedPatient.appointmentStatus}
                      </span>
                      {selectedPatient.checkoutStatus === "ReadyForCheckout" && (
                        <span className="text-[10px] font-bold px-2 py-0.5 rounded-full" style={{ background: "#fef3c7", color: "#d97706" }}>جاهز للدفع</span>
                      )}
                    </div>
                    <div className="flex items-center gap-4 mt-1 text-xs font-medium" style={{ color: "#64748b" }}>
                      <span className="flex items-center gap-1"><Clock className="w-3 h-3" /> {fmtTime(selectedPatient.appointmentTime)}</span>
                      <span>{selectedPatient.serviceName ?? "—"}</span>
                      <span>غرفة: {selectedPatient.roomName ?? "—"}</span>
                      {selectedPatient.inRoomSince && (
                        <span className="font-bold" style={{ color: "#16a34a" }}>{fmtSessionDuration(selectedPatient.inRoomSince)}</span>
                      )}
                    </div>

                    {/* Medical Alerts */}
                    {medicalAlerts.length > 0 && (
                      <div className="flex gap-1.5 mt-2 flex-wrap">
                        {medicalAlerts.map((alert, i) => (
                          <span key={i} className="text-[10px] font-bold px-2 py-0.5 rounded-full flex items-center gap-1"
                            style={{ background: alert.severity === "danger" ? "#fef2f2" : "#fff7ed", color: alert.severity === "danger" ? "#dc2626" : "#d97706" }}>
                            <AlertCircle className="w-2.5 h-2.5" />
                            {alert.type === "allergy" ? "حساسية" : alert.type === "bleeding" ? "نزيف" : alert.type === "pregnancy" ? "حمل" : alert.label ?? "تنبيه"}
                          </span>
                        ))}
                      </div>
                    )}

                    {/* Chief Complaint */}
                    {selectedPatient.chiefComplaint && (
                      <div className="mt-2 text-xs font-medium px-3 py-1.5 rounded-lg inline-block" style={{ background: "#fff7ed", color: "#92400e" }}>
                        الشكوى: {selectedPatient.chiefComplaint}
                      </div>
                    )}
                  </div>

                  {/* Handoff button (when patient is active) */}
                  {isPatientActive && (
                    <button onClick={() => setHandoffModalOpen(true)}
                      className="flex items-center gap-2 px-5 py-2.5 rounded-xl text-xs font-extrabold text-white transition hover:opacity-90 animate-pulse-glow flex-shrink-0"
                      style={{ background: `linear-gradient(135deg, ${ORANGE}, #e67e22)` }}>
                      <Send className="w-4 h-4" />
                      إنهاء وإرسال للاستقبال
                    </button>
                  )}
                </div>

                {/* Patient Detail Tabs */}
                <div className="flex-shrink-0 bg-white flex items-center px-4 gap-0.5"
                  style={{ borderBottom: "2px solid #f1f5f9" }}>
                  {PATIENT_TABS.map(tab => {
                    const isActive = activeTab === tab.key;
                    const TabIcon = tab.icon;
                    return (
                      <button key={tab.key} onClick={() => setActiveTab(tab.key)}
                        className="flex items-center gap-1.5 px-3 py-2.5 rounded-t-lg text-xs font-bold relative transition-all"
                        style={{
                          color: isActive ? BLUE : "#64748b",
                          background: isActive ? BLUE + "08" : "transparent",
                        }}>
                        <TabIcon className="w-3.5 h-3.5" />
                        <span>{tab.label}</span>
                        {isActive && (
                          <span className="absolute bottom-[-2px] left-2 right-2 h-[2px] rounded-full" style={{ background: BLUE }} />
                        )}
                      </button>
                    );
                  })}
                </div>

                {/* Tab Content */}
                <div className="flex-1 overflow-y-auto p-4 bg-[#f8fafc]">
                  {/* ═══ JOURNEY TAB ═══ */}
                  {activeTab === "journey" && (
                    <div className="space-y-4 animate-fade-in-up">
                      {/* Flow steps */}
                      <div className="bg-white rounded-xl border p-4" style={{ borderColor: "#e5e7eb" }}>
                        <div className="flex items-center gap-2 mb-3">
                          <Route className="w-4 h-4" style={{ color: BLUE }} />
                          <span className="text-xs font-bold" style={{ color: NAVY }}>مسار المريض اليوم</span>
                        </div>
                        <div className="grid grid-cols-7 gap-2">
                          {flowSteps.map((step, i) => {
                            const StepIcon = step.icon;
                            const flowIndex = getFlowIndex(selectedPatient.appointmentStatus);
                            const active = i === flowIndex;
                            const done = i < flowIndex || selectedPatient.appointmentStatus === "Completed";
                            return (
                              <div key={step.key}
                                className="rounded-xl border p-3 text-center"
                                style={{
                                  background: active ? BLUE : done ? "#f0fdf4" : "#f9fafb",
                                  borderColor: active ? BLUE : done ? "#bbf7d0" : "#f1f5f9",
                                  color: active ? "#fff" : done ? "#16a34a" : "#94a3b8",
                                }}>
                                <StepIcon className="w-5 h-5 mx-auto mb-1" />
                                <p className="text-[10px] font-bold">{step.label}</p>
                              </div>
                            );
                          })}
                        </div>
                      </div>

                      {/* Quick info cards */}
                      <div className="grid grid-cols-3 gap-4">
                        {/* Financial quick view */}
                        <div className="bg-white rounded-xl border p-4" style={{ borderColor: "#e5e7eb" }}>
                          <div className="flex items-center gap-2 mb-3">
                            <Wallet className="w-4 h-4" style={{ color: ORANGE }} />
                            <span className="text-xs font-bold" style={{ color: NAVY }}>المالية السريعة</span>
                          </div>
                          <div className="space-y-2 text-sm">
                            <div className="flex justify-between">
                              <span className="text-gray-500">الإجمالي</span>
                              <b>{fmtRial(selectedSummary?.financeSummary?.totalTreatmentCost ?? 0)}</b>
                            </div>
                            <div className="flex justify-between">
                              <span className="text-gray-500">المدفوع</span>
                              <b className="text-green-700">{fmtRial(selectedSummary?.financeSummary?.totalPaid ?? 0)}</b>
                            </div>
                            <div className="flex justify-between">
                              <span className="text-gray-500">المتبقي</span>
                              <b className="text-red-600">{fmtRial(selectedSummary?.financeSummary?.outstandingBalance ?? 0)}</b>
                            </div>
                          </div>
                        </div>

                        {/* Communication */}
                        <div className="bg-white rounded-xl border p-4" style={{ borderColor: "#e5e7eb" }}>
                          <div className="flex items-center gap-2 mb-3">
                            <MessageCircle className="w-4 h-4" style={{ color: "#16a34a" }} />
                            <span className="text-xs font-bold" style={{ color: NAVY }}>التواصل</span>
                          </div>
                          <div className="space-y-2">
                            <button className="w-full flex items-center gap-2 px-3 py-2 rounded-lg text-xs font-bold bg-green-50 border border-green-100 text-green-700 hover:bg-green-100">
                              <MessageCircle className="w-3.5 h-3.5" /> واتساب
                            </button>
                            <button className="w-full flex items-center gap-2 px-3 py-2 rounded-lg text-xs font-bold bg-white border border-gray-200 hover:bg-gray-50" style={{ color: NAVY }}>
                              <Printer className="w-3.5 h-3.5" /> طباعة سند
                            </button>
                          </div>
                        </div>

                        {/* Status summary */}
                        <div className="bg-white rounded-xl border p-4" style={{ borderColor: "#e5e7eb" }}>
                          <div className="flex items-center gap-2 mb-3">
                            <FileText className="w-4 h-4" style={{ color: NAVY }} />
                            <span className="text-xs font-bold" style={{ color: NAVY }}>ملخص الحالة</span>
                          </div>
                          <div className="space-y-2 text-xs">
                            <div className="rounded-lg bg-gray-50 p-2.5">
                              <p className="text-[10px] text-gray-500 mb-0.5">الشكوى</p>
                              <p className="font-bold" style={{ color: NAVY }}>{selectedPatient.chiefComplaint ?? "—"}</p>
                            </div>
                            <div className="rounded-lg bg-gray-50 p-2.5">
                              <p className="text-[10px] text-gray-500 mb-0.5">الخدمة</p>
                              <p className="font-bold" style={{ color: NAVY }}>{selectedPatient.serviceName ?? "—"}</p>
                            </div>
                            <div className="rounded-lg bg-gray-50 p-2.5">
                              <p className="text-[10px] text-gray-500 mb-0.5">الغرفة</p>
                              <p className="font-bold" style={{ color: NAVY }}>{selectedPatient.roomName ?? "—"}</p>
                            </div>
                          </div>
                        </div>
                      </div>
                    </div>
                  )}

                  {/* ═══ VISIT TAB ═══ */}
                  {activeTab === "visit" && (
                    <div className="space-y-4 animate-fade-in-up">
                      <div className="grid grid-cols-2 gap-4">
                        {/* Service catalog selection */}
                        <div className="bg-white rounded-xl border overflow-hidden" style={{ borderColor: "#e5e7eb" }}>
                          <div className="flex items-center justify-between px-4 py-2.5 border-b border-gray-100">
                            <div className="flex items-center gap-1.5">
                              <Stethoscope className="w-4 h-4" style={{ color: BLUE }} />
                              <span className="text-xs font-bold" style={{ color: NAVY }}>اختر الإجراءات من الخدمات المسعّرة</span>
                            </div>
                          </div>
                          <div className="p-4 space-y-3">
                            <div className="grid grid-cols-2 gap-2 max-h-[320px] overflow-y-auto">
                              {services.map(service => {
                                const checked = selectedServices.some(s => s.id === service.id);
                                return (
                                  <button key={service.id}
                                    type="button"
                                    onClick={() => handleToggleService(service)}
                                    className="rounded-xl border p-3 text-right transition"
                                    style={{
                                      background: checked ? BLUE : "#fff",
                                      borderColor: checked ? BLUE : "#e5e7eb",
                                      color: checked ? "#fff" : NAVY,
                                    }}>
                                    <div className="flex items-start justify-between gap-2">
                                      <div>
                                        <p className="text-xs font-bold">{service.arabicName}</p>
                                        {service.requiresConsultationFee && (
                                          <p className="text-[10px] mt-0.5" style={{ color: checked ? "#bfdbfe" : "#94a3b8" }}>يتطلب رسوم معاينة</p>
                                        )}
                                      </div>
                                      {service.defaultPrice != null && (
                                        <b className="text-[10px] whitespace-nowrap">{formatMoney(service.defaultPrice)}</b>
                                      )}
                                    </div>
                                  </button>
                                );
                              })}
                            </div>

                            {/* Notes */}
                            <textarea className="w-full rounded-lg border border-gray-200 px-3 py-2 text-xs min-h-16" placeholder="ملاحظات الطبيب على الإجراءات" />
                          </div>
                        </div>

                        {/* Visit invoice preview */}
                        <div className="bg-white rounded-xl border overflow-hidden" style={{ borderColor: "#e5e7eb" }}>
                          <div className="flex items-center justify-between px-4 py-2.5 border-b border-gray-100">
                            <div className="flex items-center gap-1.5">
                              <Wallet className="w-4 h-4" style={{ color: ORANGE }} />
                              <span className="text-xs font-bold" style={{ color: NAVY }}>فاتورة الزيارة قبل الإرسال</span>
                            </div>
                          </div>
                          <div className="p-4 space-y-3">
                            {/* Selected services list */}
                            <div className="rounded-xl border border-gray-100 overflow-hidden">
                              {selectedServices.length === 0 ? (
                                <div className="p-5 text-center text-xs text-gray-400">اختر إجراءً واحداً أو أكثر من قائمة الخدمات</div>
                              ) : selectedServices.map(service => (
                                <div key={service.id} className="flex items-center justify-between border-b border-gray-100 px-3 py-2 text-xs last:border-b-0">
                                  <span className="font-bold" style={{ color: NAVY }}>{service.arabicName}</span>
                                  <span className="font-bold" style={{ color: "#64748b" }}>{service.defaultPrice != null ? formatMoney(service.defaultPrice) : "—"}</span>
                                </div>
                              ))}
                            </div>

                            {/* Total */}
                            <div className="rounded-xl bg-gray-50 p-3 text-sm flex items-center justify-between">
                              <span className="font-bold text-gray-600">إجمالي إجراءات اليوم</span>
                              <b style={{ color: NAVY }}>{formatMoney(selectedServicesTotal)}</b>
                            </div>

                            {/* Note about reception */}
                            <div className="rounded-xl bg-amber-50 border border-amber-100 p-3 text-xs text-amber-800 leading-6">
                              الطبيب لا يستلم المبلغ هنا؛ فقط يحدد الإجراءات المنفذة من الخدمات المسعّرة، ثم يرسل المريض للاستقبال للتحصيل والسند.
                            </div>

                            {/* Save & Send buttons */}
                            <button onClick={() => {
                              const treatmentDone = selectedServices.map(s => s.arabicName).join(" + ");
                              updateClinicalNotes({
                                treatmentDone: clinicalNotes.treatmentDone ? `${clinicalNotes.treatmentDone} + ${treatmentDone}` : treatmentDone,
                                amountDue: clinicalNotes.amountDue + selectedServicesTotal,
                              });
                              toast.success("تم حفظ إجراءات الزيارة");
                            }}
                              className="w-full flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl text-xs font-bold text-white"
                              style={{ background: BLUE }}>
                              <CheckCircle className="w-4 h-4" /> حفظ زيارة اليوم
                            </button>
                            <button onClick={() => setHandoffModalOpen(true)}
                              className="w-full flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl text-xs font-bold border"
                              style={{ background: "#f0fdf4", borderColor: "#bbf7d0", color: "#16a34a" }}>
                              <Send className="w-4 h-4" /> إنهاء وإرسال للاستقبال
                            </button>
                          </div>
                        </div>
                      </div>

                      {/* Clinical Quick Actions */}
                      <div className="bg-white rounded-xl border p-4" style={{ borderColor: "#e5e7eb" }}>
                        <div className="flex items-center gap-2 mb-3">
                          <Activity className="w-4 h-4" style={{ color: NAVY }} />
                          <span className="text-xs font-bold" style={{ color: NAVY }}>إجراءات سريرية سريعة</span>
                        </div>
                        <div className="grid grid-cols-3 sm:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-2">
                          {ACTION_CARDS.map(card => {
                            const isDisabled = !selectedPatient ||
                              (!card.requiredStatus.includes(selectedPatient.appointmentStatus) &&
                               !card.requiredStatus.includes(selectedPatient.queueStatus ?? ""));
                            return (
                              <button key={card.key}
                                onClick={() => handleAction(card.key)}
                                disabled={isDisabled}
                                className="flex flex-col items-center gap-1.5 p-3 rounded-xl border transition-all text-center"
                                style={{
                                  background: isDisabled ? "#f9fafb" : card.bgColor,
                                  borderColor: isDisabled ? "#f1f5f9" : card.color + "25",
                                  opacity: isDisabled ? 0.4 : 1,
                                  cursor: isDisabled ? "not-allowed" : "pointer",
                                }}>
                                <div className="w-8 h-8 rounded-lg flex items-center justify-center"
                                  style={{ background: isDisabled ? "#e5e7eb" : card.color + "18" }}>
                                  <card.icon className="w-4 h-4" style={{ color: isDisabled ? "#94a3b8" : card.color }} />
                                </div>
                                <div className="text-[10px] font-bold leading-tight" style={{ color: isDisabled ? "#94a3b8" : card.color }}>
                                  {card.label}
                                </div>
                              </button>
                            );
                          })}
                        </div>
                      </div>
                    </div>
                  )}

                  {/* ═══ ORTHO TAB ═══ */}
                  {activeTab === "ortho" && (
                    <div className="animate-fade-in-up">
                      <div className="bg-white rounded-xl border p-4" style={{ borderColor: "#e5e7eb" }}>
                        <div className="flex items-center gap-2 mb-4">
                          <Zap className="w-4 h-4" style={{ color: ORANGE }} />
                          <span className="text-xs font-bold" style={{ color: NAVY }}>ملف التقويم النشط</span>
                        </div>
                        {selectedSummary?.activeOrthoCase ? (
                          <div className="grid grid-cols-2 gap-3 text-xs">
                            <div className="rounded-lg bg-gray-50 p-3">
                              <p className="text-[10px] text-gray-500 mb-1">رقم الحالة</p>
                              <p className="font-bold" style={{ color: NAVY }}>{selectedSummary.activeOrthoCase.caseNumber ?? "—"}</p>
                            </div>
                            <div className="rounded-lg bg-gray-50 p-3">
                              <p className="text-[10px] text-gray-500 mb-1">المرحلة الحالية</p>
                              <p className="font-bold" style={{ color: NAVY }}>{selectedSummary.activeOrthoCase.currentStage ?? "—"}</p>
                            </div>
                            <div className="rounded-lg bg-gray-50 p-3">
                              <p className="text-[10px] text-gray-500 mb-1">نوع الجهاز</p>
                              <p className="font-bold" style={{ color: NAVY }}>{selectedSummary.activeOrthoCase.applianceType ?? "—"}</p>
                            </div>
                            <div className="rounded-lg bg-gray-50 p-3">
                              <p className="text-[10px] text-gray-500 mb-1">الخطة</p>
                              <p className="font-bold" style={{ color: NAVY }}>{selectedSummary.activeOrthoCase.currentStage ?? "—"}</p>
                            </div>
                          </div>
                        ) : (
                          <div className="text-center py-8 text-xs" style={{ color: "#94a3b8" }}>لا يوجد ملف تقويم نشط لهذا المريض</div>
                        )}
                      </div>
                    </div>
                  )}

                  {/* ═══ HISTORY TAB ═══ */}
                  {activeTab === "history" && (
                    <div className="animate-fade-in-up">
                      <div className="bg-white rounded-xl border p-4" style={{ borderColor: "#e5e7eb" }}>
                        <div className="flex items-center gap-2 mb-4">
                          <History className="w-4 h-4" style={{ color: NAVY }} />
                          <span className="text-xs font-bold" style={{ color: NAVY }}>التاريخ الطبي المختصر</span>
                        </div>
                        {selectedSummary?.medicalAlerts && selectedSummary.medicalAlerts.length > 0 ? (
                          <div className="grid grid-cols-2 gap-3 text-xs">
                            {selectedSummary.medicalAlerts.map((alert, i) => (
                              <div key={i} className="rounded-lg p-3"
                                style={{ background: alert.severity === "danger" ? "#fef2f2" : "#fff7ed" }}>
                                <p className="text-[10px] mb-1" style={{ color: alert.severity === "danger" ? "#dc2626" : "#d97706" }}>
                                  {alert.type === "allergy" ? "حساسية" : alert.type === "bleeding" ? "نزيف" : alert.type === "pregnancy" ? "حمل" : alert.label ?? "تنبيه"}
                                </p>
                                <p className="font-bold" style={{ color: NAVY }}>{alert.value ?? alert.label ?? "مسجل"}</p>
                              </div>
                            ))}
                          </div>
                        ) : (
                          <div className="grid grid-cols-2 gap-3 text-xs">
                            <div className="rounded-lg bg-gray-50 p-3">
                              <p className="text-[10px] text-gray-500 mb-1">أمراض مزمنة</p>
                              <p className="font-bold" style={{ color: NAVY }}>لا يوجد</p>
                            </div>
                            <div className="rounded-lg bg-gray-50 p-3">
                              <p className="text-[10px] text-gray-500 mb-1">حساسية أدوية</p>
                              <p className="font-bold" style={{ color: NAVY }}>لا يوجد</p>
                            </div>
                            <div className="rounded-lg bg-gray-50 p-3">
                              <p className="text-[10px] text-gray-500 mb-1">مشاكل نزف</p>
                              <p className="font-bold" style={{ color: NAVY }}>لا</p>
                            </div>
                            <div className="rounded-lg bg-gray-50 p-3">
                              <p className="text-[10px] text-gray-500 mb-1">ملاحظات أخرى</p>
                              <p className="font-bold" style={{ color: NAVY }}>—</p>
                            </div>
                          </div>
                        )}
                      </div>
                    </div>
                  )}

                  {/* ═══ PAYMENT TAB ═══ */}
                  {activeTab === "payment" && (
                    <div className="grid grid-cols-2 gap-4 animate-fade-in-up">
                      {/* Payment form */}
                      <div className="bg-white rounded-xl border p-4" style={{ borderColor: "#e5e7eb" }}>
                        <div className="flex items-center gap-2 mb-4">
                          <CreditCard className="w-4 h-4" style={{ color: "#22c55e" }} />
                          <span className="text-xs font-bold" style={{ color: NAVY }}>تسجيل دفعة</span>
                        </div>
                        <div className="space-y-3">
                          <input className="w-full rounded-lg border border-gray-200 px-3 py-2 text-xs" placeholder="المبلغ" type="number" dir="ltr" />
                          <select className="w-full rounded-lg border border-gray-200 px-3 py-2 text-xs">
                            <option>نقداً</option>
                            <option>تحويل بنكي</option>
                            <option>بطاقة</option>
                          </select>
                          <button className="w-full flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl text-xs font-bold text-white"
                            style={{ background: "#22c55e" }}>
                            <Wallet className="w-4 h-4" /> حفظ الدفعة
                          </button>
                          <div className="rounded-xl bg-amber-50 border border-amber-100 p-3 text-xs text-amber-800">
                            الطبيب لا يستلم المبلغ هنا؛ فقط يحدد الإجراءات ثم يرسل المريض للاستقبال للتحصيل والسند.
                          </div>
                        </div>
                      </div>

                      {/* Account summary */}
                      <div className="bg-white rounded-xl border p-4" style={{ borderColor: "#e5e7eb" }}>
                        <div className="flex items-center gap-2 mb-4">
                          <Wallet className="w-4 h-4" style={{ color: ORANGE }} />
                          <span className="text-xs font-bold" style={{ color: NAVY }}>ملخص الحساب</span>
                        </div>
                        <div className="space-y-3 text-sm">
                          <div className="flex justify-between">
                            <span className="text-gray-500">إجمالي الخطة</span>
                            <b>{fmtRial(selectedSummary?.financeSummary?.totalTreatmentCost ?? 0)}</b>
                          </div>
                          <div className="flex justify-between">
                            <span className="text-gray-500">المدفوع</span>
                            <b className="text-green-700">{fmtRial(selectedSummary?.financeSummary?.totalPaid ?? 0)}</b>
                          </div>
                          <div className="flex justify-between">
                            <span className="text-gray-500">المتبقي</span>
                            <b className="text-red-600">{fmtRial(selectedSummary?.financeSummary?.outstandingBalance ?? 0)}</b>
                          </div>
                          {(clinicalNotes.amountDue > 0 || selectedServicesTotal > 0) && (
                            <div className="flex justify-between pt-2 border-t border-gray-100">
                              <span className="text-gray-500 font-bold">إجراءات اليوم</span>
                              <b style={{ color: ORANGE }}>{formatMoney(clinicalNotes.amountDue || selectedServicesTotal)}</b>
                            </div>
                          )}
                        </div>
                      </div>
                    </div>
                  )}

                  {/* ═══ TIMELINE TAB ═══ */}
                  {activeTab === "timeline" && (
                    <div className="animate-fade-in-up">
                      <div className="bg-white rounded-xl border p-4" style={{ borderColor: "#e5e7eb" }}>
                        <div className="flex items-center gap-2 mb-4">
                          <Clock className="w-4 h-4" style={{ color: NAVY }} />
                          <span className="text-xs font-bold" style={{ color: NAVY }}>سجل الأحداث</span>
                        </div>
                        <div className="space-y-3">
                          {selectedPatient.appointmentStatus !== "Scheduled" && (
                            <div className="rounded-lg bg-blue-50 p-3 text-xs">
                              <b style={{ color: NAVY }}>تم تسجيل الحضور</b>
                              <p className="text-gray-500 mt-1">اليوم</p>
                            </div>
                          )}
                          {(selectedPatient.queueStatus === "Called" || selectedPatient.appointmentStatus === "InProgress" || selectedPatient.appointmentStatus === "InRoom") && (
                            <div className="rounded-lg bg-purple-50 p-3 text-xs">
                              <b style={{ color: NAVY }}>تم نداء المريض</b>
                              <p className="text-gray-500 mt-1">اليوم</p>
                            </div>
                          )}
                          {(clinicalNotes.treatmentDone || clinicalNotes.diagnosis) && (
                            <div className="rounded-lg bg-green-50 p-3 text-xs">
                              <b style={{ color: NAVY }}>تم حفظ زيارة — {clinicalNotes.treatmentDone || clinicalNotes.diagnosis}</b>
                              <p className="text-gray-500 mt-1">اليوم</p>
                            </div>
                          )}
                          {selectedPatient.appointmentStatus === "Completed" && (
                            <div className="rounded-lg bg-gray-50 p-3 text-xs">
                              <b style={{ color: NAVY }}>تم إكمال الزيارة</b>
                              <p className="text-gray-500 mt-1">اليوم</p>
                            </div>
                          )}
                          {selectedPatient.appointmentStatus === "Scheduled" && (
                            <div className="text-center py-8 text-xs" style={{ color: "#94a3b8" }}>لم تبدأ الزيارة بعد</div>
                          )}
                        </div>
                      </div>
                    </div>
                  )}
                </div>
              </>
            ) : (
              /* ── Empty state ── */
              <div className="flex-1 flex flex-col items-center justify-center p-8">
                <div className="w-20 h-20 rounded-2xl flex items-center justify-center mb-4" style={{ background: NAVY + "08" }}>
                  <Stethoscope className="w-10 h-10" style={{ color: NAVY + "30" }} />
                </div>
                <p className="text-base font-bold" style={{ color: NAVY }}>اختر مريضاً للبدء</p>
                <p className="text-sm mt-1" style={{ color: "#94a3b8" }}>اختر مريضاً من القائمة على اليمين لعرض التفاصيل والإجراءات</p>
                {isLoading && <Loader2 className="w-6 h-6 animate-spin mt-4" style={{ color: BLUE }} />}
              </div>
            )}
          </div>
        </div>

        {/* ═══════════════════════════════════════════════════════════════
            STATUS BAR (32px)
            ═══════════════════════════════════════════════════════════════ */}
        <div className="h-8 flex-shrink-0 flex items-center px-4 gap-4 text-[11px] font-medium text-white select-none"
          style={{ background: NAVY }}>
          <span>د. {doctorName}</span>
          <div className="w-px h-4" style={{ background: "rgba(255,255,255,0.2)" }} />
          <span>{patients.length} مريض اليوم</span>
          <span>{inClinicPatients.length} في العيادة</span>
          <span>{waitingPatients.length} انتظار</span>
          <span>{completedPatients.length} مكتمل</span>
          <div className="flex-1" />
          <span>{new Date().toLocaleDateString("ar-YE", { weekday: "long", year: "numeric", month: "long", day: "numeric" })}</span>
        </div>
      </div>

      {/* ═══════════════════════════════════════════════════════════════
          MODALS
          ═══════════════════════════════════════════════════════════════ */}
      <StartVisitModal
        open={startVisitModalOpen}
        onClose={() => setStartVisitModalOpen(false)}
        patient={selectedPatient}
        isPending={startVisitMutation.isPending}
        onConfirm={handleStartVisit}
      />

      <ExaminationModal
        open={examinationModalOpen}
        onClose={() => setExaminationModalOpen(false)}
        patient={selectedPatient}
        diagnosis={clinicalNotes.diagnosis}
        medicalAlerts={medicalAlerts}
        onSave={(data) => {
          const diagnosisParts = [
            data.chiefComplaint ? `الشكوى: ${data.chiefComplaint}` : "",
            data.extraoral ? `فحص خارج الفم: ${data.extraoral}` : "",
            data.intraoral ? `فحص داخل الفم: ${data.intraoral}` : "",
            data.diagnosis ? `التشخيص: ${data.diagnosis}` : "",
            data.clinicalNotes ? `ملاحظات: ${data.clinicalNotes}` : "",
          ].filter(Boolean).join(" | ");

          const treatmentParts = [data.treatmentDone].filter(Boolean).join(" + ");

          updateClinicalNotes({
            diagnosis: diagnosisParts || data.diagnosis,
            treatmentDone: treatmentParts || clinicalNotes.treatmentDone,
          });
          setExaminationModalOpen(false);
          toast.success("تم حفظ الفحص والتشخيص");
        }}
      />

      <PricedProceduresModal
        open={proceduresModalOpen}
        onClose={() => setProceduresModalOpen(false)}
        patient={selectedPatient}
        services={services}
        currentAmountDue={clinicalNotes.amountDue}
        currentTreatmentDone={clinicalNotes.treatmentDone}
        onSave={(data) => {
          updateClinicalNotes({
            treatmentDone: data.treatmentDone,
            amountDue: data.totalAmount,
            suggestedServiceId: data.suggestedServiceId,
          });
          setProceduresModalOpen(false);
          toast.success("تم حفظ الإجراءات المسعّرة");
        }}
      />

      <TreatmentPlanModal
        open={treatmentPlanModalOpen}
        onClose={() => setTreatmentPlanModalOpen(false)}
        patient={selectedPatient}
        onSave={(data) => {
          updateClinicalNotes({ nextVisitPlan: data.plan });
          setTreatmentPlanModalOpen(false);
          toast.success("تم حفظ خطة العلاج");
        }}
      />

      <ImagesRadiographsModal
        open={imagesModalOpen}
        onClose={() => setImagesModalOpen(false)}
        patient={selectedPatient}
        onSave={() => {
          setImagesModalOpen(false);
          toast.success("تم حفظ مرجع الأشعة");
        }}
      />

      <LabOrderModal
        open={labOrderModalOpen}
        onClose={() => setLabOrderModalOpen(false)}
        patient={selectedPatient}
        onSave={(data) => {
          const labParts = [
            data.labWorkType ? `طلب مختبر: ${data.labWorkType}` : "",
            data.shade ? `اللون: ${data.shade}` : "",
            data.deliveryDate ? `تسليم: ${data.deliveryDate}` : "",
            data.referralDepartment ? `إحالة: ${data.referralDepartment}` : "",
            data.labInstructions ? `تعليمات: ${data.labInstructions}` : "",
          ].filter(Boolean).join(" | ");

          if (labParts) {
            const existing = clinicalNotes.treatmentDone;
            updateClinicalNotes({
              treatmentDone: existing ? `${existing} + ${labParts}` : labParts,
            });
          }
          setLabOrderModalOpen(false);
          toast.success("تم حفظ طلب المختبر");
        }}
      />

      <OrthoFollowUpModal
        open={orthoFollowUpModalOpen}
        onClose={() => setOrthoFollowUpModalOpen(false)}
        patient={selectedPatient}
        onSave={(data) => {
          const orthoTreatment = data.notes || "متابعة تقويم";
          const orthoPlan = data.nextVisitPlan || data.nextOrthodonticPlan;
          const existing = clinicalNotes.treatmentDone;
          updateClinicalNotes({
            treatmentDone: existing ? `${existing} + متابعة تقويم` : orthoTreatment,
            nextVisitPlan: orthoPlan || clinicalNotes.nextVisitPlan,
          });
          setOrthoFollowUpModalOpen(false);
          toast.success("تم حفظ متابعة التقويم");
        }}
      />

      <PrescriptionModal
        open={prescriptionModalOpen}
        onClose={() => setPrescriptionModalOpen(false)}
        patient={selectedPatient}
        onSave={(data) => {
          const instructionParts = [
            data.prescriptionText ? `الوصفة: ${data.prescriptionText}` : "",
            data.instructions ? `التعليمات: ${data.instructions}` : "",
          ].filter(Boolean).join(" | ");
          updateClinicalNotes({ instructions: instructionParts || data.instructions });
          setPrescriptionModalOpen(false);
          toast.success("تم حفظ الوصفة والتعليمات");
        }}
      />

      <FollowUpSuggestModal
        open={followUpModalOpen}
        onClose={() => setFollowUpModalOpen(false)}
        patient={selectedPatient}
        services={services}
        onSave={(data) => {
          updateClinicalNotes({
            followUpDate: data.followUpDate,
            suggestedServiceId: data.serviceId,
          });
          setFollowUpModalOpen(false);
          toast.success("تم حفظ اقتراح موعد المتابعة");
        }}
      />

      <HandoffConfirmModal
        open={handoffModalOpen}
        onClose={() => setHandoffModalOpen(false)}
        patient={selectedPatient}
        clinicalNotes={clinicalNotes}
        isPending={handoffMutation.isPending}
        onConfirm={handleHandoff}
      />
    </>
  );
}
