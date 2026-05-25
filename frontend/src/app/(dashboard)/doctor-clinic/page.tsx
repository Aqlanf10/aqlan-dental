"use client";

import { useState, useCallback, useMemo, useEffect, useRef } from "react";
import {
  Stethoscope, Play, ClipboardCheck, ListChecks, GitBranch,
  Image, Pill, FlaskConical, CalendarClock, Send, RefreshCw,
  Search, X, ChevronLeft, AlertCircle, CheckCircle, Clock,
  Activity, Loader2, FileText, User, Settings2, Eye,
  CreditCard, ArrowRight,
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
   Action card definitions
   ═══════════════════════════════════════════════════════════════════════════ */
interface ActionCardDef {
  key: string;
  label: string;
  icon: React.ElementType;
  color: string;
  bgColor: string;
  requiredStatus: string[]; // appointmentStatus that enables this action
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
   MAIN PAGE — Doctor Clinic Workspace
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

    // Check if action is enabled for current patient status
    const card = ACTION_CARDS.find(c => c.key === cardKey);
    if (card && !card.requiredStatus.includes(selectedPatient.appointmentStatus) &&
        !card.requiredStatus.includes(selectedPatient.queueStatus ?? "")) {
      toast.error("هذا الإجراء غير متاح لحالة المريض الحالية");
      return;
    }

    switch (cardKey) {
      case "startVisit":
        setStartVisitModalOpen(true);
        break;
      case "examination":
        setExaminationModalOpen(true);
        break;
      case "procedures":
        setProceduresModalOpen(true);
        break;
      case "treatmentPlan":
        setTreatmentPlanModalOpen(true);
        break;
      case "orthoFollowUp":
        setOrthoFollowUpModalOpen(true);
        break;
      case "images":
        setImagesModalOpen(true);
        break;
      case "prescription":
        setPrescriptionModalOpen(true);
        break;
      case "labOrder":
        setLabOrderModalOpen(true);
        break;
      case "followUp":
        setFollowUpModalOpen(true);
        break;
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
            MAIN CONTENT — Two-column layout
            ═══════════════════════════════════════════════════════════════ */}
        <div className="flex-1 flex overflow-hidden">

          {/* ── Left: Patient List + Action Cards ── */}
          <div className="flex-1 flex flex-col min-w-0 overflow-y-auto">

            {/* Patient selector row (horizontal scrollable) */}
            <div className="flex-shrink-0 bg-white px-4 py-3" style={{ borderBottom: "1px solid #f1f5f9" }}>
              <div className="flex items-center gap-2 mb-2">
                <User className="w-4 h-4" style={{ color: NAVY }} />
                <span className="text-xs font-bold" style={{ color: NAVY }}>مرضاي اليوم</span>
                <span className="text-[10px] px-1.5 py-0.5 rounded-full font-bold" style={{ background: NAVY + "15", color: NAVY }}>{patients.length}</span>
              </div>
              <div className="flex gap-2 overflow-x-auto pb-1">
                {filteredPatients.length === 0 && !isLoading && (
                  <div className="text-xs font-medium py-3 text-center w-full" style={{ color: "#94a3b8" }}>لا يوجد مرضى</div>
                )}
                {filteredPatients.map(p => {
                  const isSelected = selectedPatient?.appointmentId === p.appointmentId;
                  const isActive = ["InRoom", "InProgress"].includes(p.appointmentStatus);
                  const isWaiting = p.queueStatus === "Waiting" || p.queueStatus === "Called";
                  const isCompleted = p.appointmentStatus === "Completed" || p.checkoutStatus === "ReadyForCheckout";
                  const statusColors = STATUS_COLORS[p.appointmentStatus] ?? STATUS_COLORS.Scheduled;

                  return (
                    <button key={p.appointmentId}
                      onClick={() => {
                        setSelectedPatient(p);
                        // Reset clinical notes when switching patients
                        setClinicalNotes({
                          diagnosis: "", treatmentDone: "", instructions: "",
                          nextVisitPlan: "", amountDue: 0, suggestedServiceId: "", followUpDate: "",
                        });
                      }}
                      className="flex-shrink-0 flex flex-col items-center gap-1.5 px-3 py-2.5 rounded-xl transition-all border-2 min-w-[90px]"
                      style={{
                        background: isSelected ? NAVY + "08" : "#fff",
                        borderColor: isSelected ? NAVY : "#e5e7eb",
                        boxShadow: isSelected ? `0 2px 8px ${NAVY}15` : "none",
                      }}>
                      {/* Avatar */}
                      <div className="w-10 h-10 rounded-full flex items-center justify-center text-sm font-bold"
                        style={{
                          background: isActive ? "#16a34a" : isWaiting ? ORANGE : isCompleted ? "#94a3b8" : BLUE,
                          color: "#fff",
                        }}>
                        {getInitials(p.patientName)}
                      </div>
                      {/* Name */}
                      <div className="text-[11px] font-bold text-center leading-tight max-w-[80px] truncate" style={{ color: NAVY }}>
                        {p.patientName}
                      </div>
                      {/* Status badge */}
                      <div className="text-[9px] font-bold px-1.5 py-0.5 rounded-full"
                        style={{ background: statusColors.bg, color: statusColors.text }}>
                        {APPT_STATUS_LABELS[p.appointmentStatus] ?? p.appointmentStatus}
                      </div>
                      {/* Time */}
                      <div className="text-[10px] font-medium" style={{ color: "#94a3b8" }}>
                        {fmtTime(p.appointmentTime)}
                      </div>
                      {/* Session duration for active patients */}
                      {isActive && p.inRoomSince && (
                        <div className="text-[9px] font-bold px-1 py-0.5 rounded" style={{ background: "#f0fdf4", color: "#16a34a" }}>
                          {fmtSessionDuration(p.inRoomSince)}
                        </div>
                      )}
                      {/* ReadyForCheckout indicator */}
                      {p.checkoutStatus === "ReadyForCheckout" && (
                        <div className="text-[9px] font-bold px-1 py-0.5 rounded" style={{ background: "#fef3c7", color: "#d97706" }}>
                          جاهز للدفع
                        </div>
                      )}
                    </button>
                  );
                })}
              </div>
            </div>

            {/* ── Selected Patient Summary + Action Cards ── */}
            {selectedPatient ? (
              <div className="flex-1 p-4 space-y-4 overflow-y-auto">
                {/* Patient Summary Card */}
                <div className="bg-white rounded-2xl p-4 border" style={{ borderColor: "#e5e7eb" }}>
                  <div className="flex items-start gap-4">
                    {/* Avatar */}
                    <div className="w-14 h-14 rounded-2xl flex items-center justify-center text-lg font-bold flex-shrink-0"
                      style={{
                        background: `linear-gradient(135deg, ${NAVY}, ${BLUE})`,
                        color: "#fff",
                      }}>
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
                          <span className="text-[10px] font-bold px-2 py-0.5 rounded-full" style={{ background: "#fef3c7", color: "#d97706" }}>
                            جاهز للدفع
                          </span>
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
                        <div className="mt-2 text-xs font-medium px-3 py-1.5 rounded-lg" style={{ background: "#fff7ed", color: "#92400e" }}>
                          الشكوى: {selectedPatient.chiefComplaint}
                        </div>
                      )}
                    </div>
                  </div>
                </div>

                {/* Clinical Notes Summary (if any) */}
                {(clinicalNotes.diagnosis || clinicalNotes.treatmentDone || clinicalNotes.amountDue > 0) && (
                  <div className="bg-white rounded-2xl p-4 border" style={{ borderColor: "#e5e7eb" }}>
                    <div className="flex items-center gap-2 mb-2">
                      <FileText className="w-4 h-4" style={{ color: BLUE }} />
                      <span className="text-xs font-bold" style={{ color: NAVY }}>ملاحظات سريرية مسجّلة</span>
                    </div>
                    <div className="grid grid-cols-2 gap-2 text-xs">
                      {clinicalNotes.diagnosis && (
                        <div className="px-2.5 py-1.5 rounded-lg" style={{ background: "#f0f5fb" }}>
                          <span className="font-bold" style={{ color: BLUE }}>التشخيص:</span>{" "}
                          <span style={{ color: NAVY }}>{clinicalNotes.diagnosis}</span>
                        </div>
                      )}
                      {clinicalNotes.treatmentDone && (
                        <div className="px-2.5 py-1.5 rounded-lg" style={{ background: "#faf5ff" }}>
                          <span className="font-bold" style={{ color: "#9333ea" }}>العلاج:</span>{" "}
                          <span style={{ color: NAVY }}>{clinicalNotes.treatmentDone}</span>
                        </div>
                      )}
                      {clinicalNotes.amountDue > 0 && (
                        <div className="px-2.5 py-1.5 rounded-lg" style={{ background: "#fff7ed" }}>
                          <span className="font-bold" style={{ color: ORANGE }}>المبلغ:</span>{" "}
                          <span style={{ color: NAVY }}>{fmtRial(clinicalNotes.amountDue)}</span>
                        </div>
                      )}
                      {clinicalNotes.instructions && (
                        <div className="px-2.5 py-1.5 rounded-lg" style={{ background: "#f0fdf4" }}>
                          <span className="font-bold" style={{ color: "#16a34a" }}>التعليمات:</span>{" "}
                          <span style={{ color: NAVY }}>{clinicalNotes.instructions}</span>
                        </div>
                      )}
                    </div>
                  </div>
                )}

                {/* ── Action Cards Grid ── */}
                <div>
                  <div className="flex items-center gap-2 mb-3">
                    <Settings2 className="w-4 h-4" style={{ color: NAVY }} />
                    <span className="text-xs font-bold" style={{ color: NAVY }}>إجراءات سريرية</span>
                  </div>
                  <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-3">
                    {ACTION_CARDS.map(card => {
                      const isDisabled = !selectedPatient ||
                        (!card.requiredStatus.includes(selectedPatient.appointmentStatus) &&
                         !card.requiredStatus.includes(selectedPatient.queueStatus ?? ""));
                      return (
                        <button key={card.key}
                          onClick={() => handleAction(card.key)}
                          disabled={isDisabled}
                          className="flex flex-col items-center gap-2 p-4 rounded-xl border transition-all text-center"
                          style={{
                            background: isDisabled ? "#f9fafb" : card.bgColor,
                            borderColor: isDisabled ? "#f1f5f9" : card.color + "25",
                            opacity: isDisabled ? 0.4 : 1,
                            cursor: isDisabled ? "not-allowed" : "pointer",
                          }}>
                          <div className="w-10 h-10 rounded-xl flex items-center justify-center"
                            style={{ background: isDisabled ? "#e5e7eb" : card.color + "18" }}>
                            <card.icon className="w-5 h-5" style={{ color: isDisabled ? "#94a3b8" : card.color }} />
                          </div>
                          <div className="text-[11px] font-bold leading-tight" style={{ color: isDisabled ? "#94a3b8" : card.color }}>
                            {card.label}
                          </div>
                          <div className="text-[9px] font-medium leading-tight" style={{ color: "#94a3b8" }}>
                            {card.description}
                          </div>
                        </button>
                      );
                    })}
                  </div>
                </div>

                {/* ── Finish & Send to Reception ── */}
                {isPatientActive && (
                  <div className="mt-4">
                    <button
                      onClick={() => setHandoffModalOpen(true)}
                      className="w-full py-3.5 rounded-2xl text-sm font-extrabold text-white flex items-center justify-center gap-2 transition hover:opacity-90 animate-pulse-glow"
                      style={{ background: `linear-gradient(135deg, ${ORANGE}, #e67e22)` }}>
                      <Send className="w-5 h-5" />
                      إنهاء وإرسال للاستقبال
                      <ArrowRight className="w-4 h-4" />
                    </button>
                    <p className="text-[10px] text-center mt-1.5 font-medium" style={{ color: "#94a3b8" }}>
                      سيتم إرسال المريض للاستقبال لإتمام التحصيل والخروج
                    </p>
                  </div>
                )}
              </div>
            ) : (
              /* ── Empty state ── */
              <div className="flex-1 flex flex-col items-center justify-center p-8">
                <div className="w-20 h-20 rounded-2xl flex items-center justify-center mb-4" style={{ background: NAVY + "08" }}>
                  <Stethoscope className="w-10 h-10" style={{ color: NAVY + "30" }} />
                </div>
                <p className="text-base font-bold" style={{ color: NAVY }}>اختر مريضاً للبدء</p>
                <p className="text-sm mt-1" style={{ color: "#94a3b8" }}>اختر مريضاً من القائمة أعلاه لعرض الإجراءات السريرية</p>
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
          // Merge examination data into clinical notes for handoff
          const diagnosisParts = [
            data.chiefComplaint ? `الشكوى: ${data.chiefComplaint}` : "",
            data.extraoral ? `فحص خارج الفم: ${data.extraoral}` : "",
            data.intraoral ? `فحص داخل الفم: ${data.intraoral}` : "",
            data.diagnosis ? `التشخيص: ${data.diagnosis}` : "",
            data.clinicalNotes ? `ملاحظات: ${data.clinicalNotes}` : "",
          ].filter(Boolean).join(" | ");

          const treatmentParts = [
            data.treatmentDone,
          ].filter(Boolean).join(" + ");

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
          // Save lab order info into treatmentDone
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
          // Merge ortho data into clinical notes
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
          // Merge prescription + instructions
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
          // Just save the suggestion — do NOT create appointment
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
