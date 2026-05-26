"use client";

import { useState, useCallback, useMemo, useEffect, useRef } from "react";
import {
  Stethoscope, ClipboardCheck, ListChecks, GitBranch,
  Image, Pill, FlaskConical, CalendarClock, Send, RefreshCw,
  AlertCircle, CheckCircle,
  Loader2, FileText,
  Route, Save,
} from "lucide-react";
import { useAuthStore } from "@/stores/authStore";
import { toast } from "@/stores/toastStore";
import { useSignalRClinicQueue } from "@/hooks/useSignalRClinicQueue";
import {
  fmtRial, fmtTime,
  APPT_STATUS_LABELS, STATUS_COLORS,
  fmtSessionDuration, ORANGE,
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
@keyframes slideInRight {
  from { transform: translateX(12px); opacity: 0; }
  to { transform: translateX(0); opacity: 1; }
}
.animate-fade-in-up { animation: fadeInUp 0.25s ease-out; }
.animate-pulse-glow { animation: pulseGlow 2s ease-in-out infinite; }
.animate-slide-in-right { animation: slideInRight 0.2s ease-out; }
`;

/* ═══════════════════════════════════════════════════════════════════════════
   Action card definitions (3-column grid)
   ═══════════════════════════════════════════════════════════════════════════ */
interface ActionCardDef {
  key: string;
  label: string;
  icon: React.ElementType;
  color: string;
  bgColor: string;
  requiredStatus: string[];
}

const ACTION_CARDS: ActionCardDef[] = [
  {
    key: "examination",
    label: "الفحص والتشخيص",
    icon: FileText,
    color: "#2563eb",
    bgColor: "#eff6ff",
    requiredStatus: ["InRoom", "InProgress"],
  },
  {
    key: "procedures",
    label: "الإجراءات المسعّرة",
    icon: ListChecks,
    color: "#9333ea",
    bgColor: "#faf5ff",
    requiredStatus: ["InRoom", "InProgress"],
  },
  {
    key: "treatmentPlan",
    label: "خطة العلاج",
    icon: Route,
    color: "#2563eb",
    bgColor: "#eff6ff",
    requiredStatus: ["InRoom", "InProgress"],
  },
  {
    key: "orthoFollowUp",
    label: "متابعة التقويم",
    icon: GitBranch,
    color: "#f5922e",
    bgColor: "#fff7ed",
    requiredStatus: ["InRoom", "InProgress"],
  },
  {
    key: "images",
    label: "الصور والأشعة",
    icon: Image,
    color: "#64748b",
    bgColor: "#f8fafc",
    requiredStatus: ["InRoom", "InProgress"],
  },
  {
    key: "prescription",
    label: "الوصفة والتعليمات",
    icon: Pill,
    color: "#dc2626",
    bgColor: "#fef2f2",
    requiredStatus: ["InRoom", "InProgress"],
  },
  {
    key: "labOrder",
    label: "طلب معمل / إحالة",
    icon: FlaskConical,
    color: "#0891b2",
    bgColor: "#ecfeff",
    requiredStatus: ["InRoom", "InProgress"],
  },
  {
    key: "followUp",
    label: "موعد المتابعة",
    icon: CalendarClock,
    color: "#7c3aed",
    bgColor: "#f5f3ff",
    requiredStatus: ["InRoom", "InProgress"],
  },
  {
    key: "startVisit",
    label: "تسجيل الفحص",
    icon: ClipboardCheck,
    color: "#16a34a",
    bgColor: "#f0fdf4",
    requiredStatus: ["InRoom", "Called"],
  },
  {
    key: "finishHandoff",
    label: "إنهاء وتسليم للاستقبال",
    icon: Send,
    color: "#b91c1c",
    bgColor: "#fef2f2",
    requiredStatus: ["InRoom", "InProgress"],
  },
];

/* ═══════════════════════════════════════════════════════════════════════════
   MAIN PAGE — Doctor Clinic Workspace (Calm, clean, minimal)
   ═══════════════════════════════════════════════════════════════════════════ */
export default function DoctorClinicPage() {
  const { user } = useAuthStore();
  const doctorId = user?.doctorId ?? user?.id ?? "";
  const doctorName = user?.doctorName ?? user?.username ?? "الطبيب";

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

  // ── Sent patients tracking (to prevent re-sending) ──
  const [sentPatients, setSentPatients] = useState<Set<string>>(new Set());

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
  // eslint-disable-next-line @typescript-eslint/no-unused-vars -- kept for future use
  const inClinicPatients = useMemo(() =>
    patients.filter(p =>
      p.appointmentStatus === "InRoom" ||
      p.appointmentStatus === "InProgress" ||
      p.queueStatus === "InRoom" ||
      p.queueStatus === "InProgress"
    ), [patients]);

  // eslint-disable-next-line @typescript-eslint/no-unused-vars -- kept for future use
  const waitingPatients = useMemo(() =>
    patients.filter(p =>
      p.queueStatus === "Waiting" ||
      p.queueStatus === "Called" ||
      (p.appointmentStatus === "Waiting" && !p.queueStatus)
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

  // ── Is current patient in progress (kept for action gating) ──
  // eslint-disable-next-line @typescript-eslint/no-unused-vars -- kept for action gating logic
  const isPatientActive = selectedPatient
    ? ["InRoom", "InProgress"].includes(selectedPatient.appointmentStatus) ||
      ["InRoom", "InProgress"].includes(selectedPatient.queueStatus ?? "")
    : false;

  // ── Is current patient sent to reception ──
  const isPatientSent = selectedPatient
    ? sentPatients.has(selectedPatient.appointmentId)
    : false;

  // ── Selected services total (kept for state management) ──
  // eslint-disable-next-line @typescript-eslint/no-unused-vars -- kept for state management
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

    if (cardKey === "finishHandoff") {
      if (isPatientSent) {
        toast.error("تم إرسال هذا المريض للاستقبال بالفعل");
        return;
      }
      setHandoffModalOpen(true);
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
  }, [selectedPatient, isPatientSent]);

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

  // ── Toggle service selection (kept for state management) ──
  // eslint-disable-next-line @typescript-eslint/no-unused-vars -- kept for state management
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
    if (sentPatients.has(selectedPatient.appointmentId)) {
      toast.error("تم إرسال هذا المريض للاستقبال بالفعل");
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
      setSentPatients(prev => new Set(prev).add(selectedPatient.appointmentId));
      setSelectedPatient(null);
      setSelectedServices([]);
      setClinicalNotes({
        diagnosis: "", treatmentDone: "", instructions: "",
        nextVisitPlan: "", amountDue: 0, suggestedServiceId: "", followUpDate: "",
      });
    } catch {
      toast.error("فشل إرسال المريض للاستقبال");
    }
  }, [selectedPatient, handoffMutation, clinicalNotes, sentPatients]);

  // ── Update clinical notes from modals ──
  const updateClinicalNotes = useCallback((updates: Partial<typeof clinicalNotes>) => {
    setClinicalNotes(prev => ({ ...prev, ...updates }));
  }, []);

  // ── Save visit button handler ──
  const handleSaveVisit = useCallback(() => {
    if (!selectedPatient) {
      toast.error("اختر مريضاً أولاً");
      return;
    }
    toast.success("تم حفظ بيانات الزيارة مؤقتاً");
  }, [selectedPatient]);

  // ── Patient initials ──
  const getInitials = (name: string) => {
    const parts = name.split(" ").filter(Boolean);
    return parts.length >= 2 ? parts[0][0] + parts[1][0] : parts[0]?.[0] ?? "؟";
  };

  /* ═══════════════════════════════════════════════════════════════════════════
   Render
   ═══════════════════════════════════════════════════════════════════════════ */
  return (
    <>
      <style dangerouslySetInnerHTML={{ __html: animationStyles }} />

      <div
        className="h-screen flex flex-col overflow-hidden"
        style={{ background: "#f0f2f5" }}
        dir="rtl"
      >
        {/* ═══════════════════════════════════════════════════════════════
            TOP BAR — 56px, Dark Blue (#1e3a8a)
            ═══════════════════════════════════════════════════════════════ */}
        <header
          className="h-14 flex-shrink-0 flex items-center px-4 md:px-6 gap-3"
          style={{ background: "#1e3a8a" }}
        >
          {/* Stethoscope + Title */}
          <div className="flex items-center gap-3 flex-shrink-0">
            <div
              className="w-9 h-9 rounded-xl flex items-center justify-center"
              style={{ background: "rgba(255,255,255,0.12)" }}
            >
              <Stethoscope className="w-4.5 h-4.5 text-white" />
            </div>
            <div className="leading-tight hidden sm:block">
              <h1 className="text-sm font-extrabold text-white tracking-wide">
                عيادة الطبيب
              </h1>
              <div
                className="text-[10px] font-medium"
                style={{ color: "rgba(255,255,255,0.6)" }}
              >
                الصفحة الرئيسية هادئة، وكل زر يفتح شاشة عمل كاملة
              </div>
            </div>
          </div>

          <div className="flex-1" />

          {/* Right side: buttons */}
          <div className="flex items-center gap-2 flex-shrink-0">
            {/* Refresh button */}
            <button
              onClick={() => refetch()}
              className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-bold text-white transition hover:opacity-90"
              style={{ background: "rgba(255,255,255,0.1)" }}
              title="تحديث (Ctrl+R)"
            >
              <RefreshCw className="w-3.5 h-3.5" />
              <span className="hidden md:inline">تحديث</span>
            </button>

            {/* Save visit button */}
            <button
              onClick={handleSaveVisit}
              className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-bold text-white transition hover:opacity-90"
              style={{ background: "rgba(255,255,255,0.1)" }}
            >
              <Save className="w-3.5 h-3.5" />
              <span className="hidden md:inline">حفظ الزيارة</span>
            </button>

            {/* Finish handoff button */}
            <button
              onClick={() => {
                if (!selectedPatient) {
                  toast.error("اختر مريضاً أولاً");
                  return;
                }
                if (isPatientSent) {
                  toast.error("تم إرسال هذا المريض للاستقبال بالفعل");
                  return;
                }
                setHandoffModalOpen(true);
              }}
              className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-bold text-white transition hover:opacity-90 animate-pulse-glow"
              style={{ background: "#f5922e" }}
            >
              <Send className="w-3.5 h-3.5" />
              <span className="hidden lg:inline">إنهاء وإرسال للاستقبال</span>
            </button>

            {/* Divider */}
            <div
              className="w-px h-7 mx-1 hidden md:block"
              style={{ background: "rgba(255,255,255,0.15)" }}
            />

            {/* SignalR indicator */}
            <div
              className="hidden md:flex items-center gap-1.5 text-[10px] font-bold"
              style={{ color: signalrConnected ? "#4ade80" : "#fca5a5" }}
            >
              <span
                className="w-2 h-2 rounded-full"
                style={{ background: signalrConnected ? "#4ade80" : "#fca5a5" }}
              />
              <span>{signalrConnected ? "متصل" : "غير متصل"}</span>
            </div>

            {/* Doctor avatar */}
            <div
              className="w-8 h-8 rounded-full flex items-center justify-center text-[11px] font-bold text-white flex-shrink-0"
              style={{ background: "linear-gradient(135deg, #3b82f6, #1d4ed8)" }}
            >
              {getInitials(doctorName)}
            </div>
          </div>
        </header>

        {/* ═══════════════════════════════════════════════════════════════
            MAIN CONTENT — Scrollable
            ═══════════════════════════════════════════════════════════════ */}
        <div className="flex-1 overflow-y-auto">
          <div className="max-w-6xl mx-auto px-4 md:px-6 py-5 space-y-5">

            {/* ── PATIENT SELECTOR — horizontal scrollable ── */}
            <div>
              <div className="flex items-center justify-between mb-3">
                <div className="flex items-center gap-2">
                  <h2 className="text-sm font-extrabold" style={{ color: "#1e3a8a" }}>
                    مرضاي اليوم
                  </h2>
                  <span
                    className="text-[10px] px-1.5 py-0.5 rounded-full font-bold"
                    style={{ background: "#1e3a8a12", color: "#1e3a8a" }}
                  >
                    {patients.length}
                  </span>
                </div>
                <div className="relative">
                  <input
                    ref={searchRef}
                    type="text"
                    value={searchQuery}
                    onChange={e => setSearchQuery(e.target.value)}
                    placeholder="بحث..."
                    className="text-xs rounded-lg border px-3 py-1.5 pl-8 outline-none w-36 transition-colors"
                    style={{
                      borderColor: "#e5e7eb",
                      background: "#fff",
                      color: "#1e3a8a",
                    }}
                  />
                </div>
              </div>

              {isLoading && (
                <div className="flex items-center justify-center py-8">
                  <Loader2 className="w-6 h-6 animate-spin" style={{ color: "#2563eb" }} />
                </div>
              )}

              {!isLoading && filteredPatients.length === 0 && (
                <div
                  className="rounded-xl border-2 border-dashed p-8 text-center"
                  style={{ borderColor: "#e5e7eb" }}
                >
                  <Stethoscope className="w-10 h-10 mx-auto mb-2" style={{ color: "#cbd5e1" }} />
                  <p className="text-sm font-bold" style={{ color: "#94a3b8" }}>
                    لا يوجد مرضى اليوم
                  </p>
                </div>
              )}

              {!isLoading && filteredPatients.length > 0 && (
                <div className="flex gap-3 overflow-x-auto pb-2" style={{ direction: "rtl" }}>
                  {filteredPatients.map(p => {
                    const isSelected = selectedPatient?.appointmentId === p.appointmentId;
                    const isActive = ["InRoom", "InProgress"].includes(p.appointmentStatus);
                    const isWaiting = p.queueStatus === "Waiting" || p.queueStatus === "Called";
                    const statusColors = STATUS_COLORS[p.appointmentStatus] ?? STATUS_COLORS.Scheduled;
                    const isSent = sentPatients.has(p.appointmentId);

                    return (
                      <button
                        key={p.appointmentId}
                        onClick={() => {
                          setSelectedPatient(p);
                          setSelectedServices([]);
                          setClinicalNotes({
                            diagnosis: "", treatmentDone: "", instructions: "",
                            nextVisitPlan: "", amountDue: 0, suggestedServiceId: "", followUpDate: "",
                          });
                        }}
                        className="flex-shrink-0 w-52 rounded-xl border p-3.5 text-right transition-all"
                        style={{
                          background: isSelected ? "#fff" : "#fff",
                          borderColor: isSelected ? "#2563eb" : isSent ? "#e5e7eb" : "#e5e7eb",
                          borderWidth: isSelected ? "2px" : "1px",
                          boxShadow: isSelected ? "0 2px 12px rgba(37,99,235,0.12)" : "none",
                          opacity: isSent ? 0.45 : 1,
                        }}
                      >
                        <div className="flex items-center gap-2.5 mb-2.5">
                          <div
                            className="w-9 h-9 rounded-full flex items-center justify-center text-[11px] font-bold flex-shrink-0"
                            style={{
                              background: isSent
                                ? "#94a3b8"
                                : isActive
                                ? "#16a34a"
                                : isWaiting
                                ? ORANGE
                                : "#2563eb",
                              color: "#fff",
                            }}
                          >
                            {getInitials(p.patientName)}
                          </div>
                          <div className="flex-1 min-w-0">
                            <p
                              className="text-xs font-bold truncate"
                              style={{ color: isSelected ? "#2563eb" : "#1e3a8a" }}
                            >
                              {p.patientName}
                            </p>
                            <p className="text-[10px] truncate" style={{ color: "#94a3b8" }}>
                              {p.patientNumber ? `#${p.patientNumber}` : ""}
                            </p>
                          </div>
                        </div>
                        <div className="flex items-center justify-between">
                          <span className="text-[10px] font-medium" style={{ color: "#64748b" }}>
                            {fmtTime(p.appointmentTime)}
                          </span>
                          <span
                            className="text-[9px] px-1.5 py-0.5 rounded-full font-bold"
                            style={{
                              background: isSent ? "#f1f5f9" : statusColors.bg,
                              color: isSent ? "#64748b" : statusColors.text,
                            }}
                          >
                            {isSent
                              ? "تم الإرسال"
                              : APPT_STATUS_LABELS[p.appointmentStatus] ?? p.appointmentStatus}
                          </span>
                        </div>
                        {isActive && p.inRoomSince && (
                          <div className="mt-1.5 text-[9px] font-bold" style={{ color: "#16a34a" }}>
                            {fmtSessionDuration(p.inRoomSince)}
                          </div>
                        )}
                      </button>
                    );
                  })}
                </div>
              )}
            </div>

            {/* ── SELECTED PATIENT SUMMARY CARD ── */}
            {selectedPatient && (
              <div className="animate-fade-in-up rounded-2xl border bg-white p-5" style={{ borderColor: "#e5e7eb" }}>
                <div className="flex items-start gap-4">
                  {/* Avatar */}
                  <div
                    className="w-14 h-14 rounded-2xl flex items-center justify-center text-lg font-bold flex-shrink-0"
                    style={{ background: "linear-gradient(135deg, #1e3a8a, #2563eb)", color: "#fff" }}
                  >
                    {getInitials(selectedPatient.patientName)}
                  </div>

                  {/* Info */}
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 flex-wrap">
                      <h2 className="text-base font-extrabold" style={{ color: "#1e3a8a" }}>
                        {selectedPatient.patientName}
                      </h2>
                      {selectedPatient.patientNumber && (
                        <span
                          className="text-[10px] font-bold px-2 py-0.5 rounded-full"
                          style={{ background: "#f1f5f9", color: "#64748b" }}
                        >
                          #{selectedPatient.patientNumber}
                        </span>
                      )}
                      <span
                        className="text-[10px] font-bold px-2 py-0.5 rounded-full"
                        style={{
                          background: STATUS_COLORS[selectedPatient.appointmentStatus]?.bg ?? "#f5f5f5",
                          color: STATUS_COLORS[selectedPatient.appointmentStatus]?.text ?? "#64748b",
                        }}
                      >
                        {APPT_STATUS_LABELS[selectedPatient.appointmentStatus] ?? selectedPatient.appointmentStatus}
                      </span>
                      {isPatientSent && (
                        <span
                          className="text-[10px] font-bold px-2 py-0.5 rounded-full flex items-center gap-0.5"
                          style={{ background: "#dcfce7", color: "#16a34a" }}
                        >
                          <CheckCircle className="w-2.5 h-2.5" /> تم الإرسال
                        </span>
                      )}
                    </div>

                    <div className="flex items-center gap-4 mt-1.5 text-xs font-medium flex-wrap" style={{ color: "#64748b" }}>
                      <span>{selectedPatient.serviceName ?? "—"}</span>
                      <span>·</span>
                      <span>{selectedPatient.roomName ?? "—"}</span>
                      {selectedPatient.inRoomSince && (
                        <>
                          <span>·</span>
                          <span className="font-bold" style={{ color: "#16a34a" }}>
                            {fmtSessionDuration(selectedPatient.inRoomSince)}
                          </span>
                        </>
                      )}
                    </div>

                    {/* Medical alerts */}
                    {medicalAlerts.length > 0 && (
                      <div className="flex gap-1.5 mt-2 flex-wrap">
                        {medicalAlerts.map((alert, i) => (
                          <span
                            key={i}
                            className="text-[10px] font-bold px-2 py-0.5 rounded-full flex items-center gap-1"
                            style={{
                              background: alert.severity === "danger" ? "#fef2f2" : "#fff7ed",
                              color: alert.severity === "danger" ? "#dc2626" : "#d97706",
                            }}
                          >
                            <AlertCircle className="w-2.5 h-2.5" />
                            {alert.type === "allergy" ? "حساسية" : alert.type === "bleeding" ? "نزيف" : alert.type === "pregnancy" ? "حمل" : alert.label ?? "تنبيه"}
                          </span>
                        ))}
                      </div>
                    )}

                    {/* Chief complaint */}
                    {selectedPatient.chiefComplaint && (
                      <div
                        className="mt-2 text-xs font-medium px-3 py-1.5 rounded-lg inline-block"
                        style={{ background: "#fff7ed", color: "#92400e" }}
                      >
                        الشكوى: {selectedPatient.chiefComplaint}
                      </div>
                    )}
                  </div>
                </div>

                {/* Quick info tiles — 2x2 grid */}
                <div className="grid grid-cols-2 md:grid-cols-4 gap-3 mt-4">
                  {/* الغرفة */}
                  <div
                    className="rounded-xl p-3"
                    style={{ background: "#f8fafc" }}
                  >
                    <p className="text-[10px] font-medium mb-0.5" style={{ color: "#94a3b8" }}>الغرفة</p>
                    <p className="text-sm font-bold" style={{ color: "#1e3a8a" }}>
                      {selectedPatient.roomName ?? "—"}
                    </p>
                  </div>
                  {/* وقت الموعد */}
                  <div
                    className="rounded-xl p-3"
                    style={{ background: "#f8fafc" }}
                  >
                    <p className="text-[10px] font-medium mb-0.5" style={{ color: "#94a3b8" }}>وقت الموعد</p>
                    <p className="text-sm font-bold" style={{ color: "#1e3a8a" }}>
                      {fmtTime(selectedPatient.appointmentTime)}
                    </p>
                  </div>
                  {/* إجراءات مختارة */}
                  <div
                    className="rounded-xl p-3"
                    style={{ background: "#faf5ff" }}
                  >
                    <p className="text-[10px] font-medium mb-0.5" style={{ color: "#9333ea" }}>إجراءات مختارة</p>
                    <p className="text-sm font-bold" style={{ color: "#9333ea" }}>
                      {selectedServices.length} إجراء
                      {clinicalNotes.amountDue > 0 && (
                        <span className="text-[10px] mr-1.5">({fmtRial(clinicalNotes.amountDue)})</span>
                      )}
                    </p>
                  </div>
                  {/* إجمالي اليوم */}
                  <div
                    className="rounded-xl p-3"
                    style={{ background: "#f0fdf4" }}
                  >
                    <p className="text-[10px] font-medium mb-0.5" style={{ color: "#16a34a" }}>إجمالي اليوم</p>
                    <p className="text-sm font-bold" style={{ color: "#16a34a" }}>
                      {fmtRial(selectedSummary?.financeSummary?.totalTreatmentCost ?? 0)}
                    </p>
                  </div>
                </div>
              </div>
            )}

            {/* ── MAIN DOCTOR ACTION GRID — 3-column ── */}
            {selectedPatient ? (
              <div className="animate-slide-in-right">
                <h3 className="text-sm font-extrabold mb-3" style={{ color: "#1e3a8a" }}>
                  إجراءات العلاج
                </h3>
                <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-3">
                  {ACTION_CARDS.map(card => {
                    const isDisabled =
                      !selectedPatient ||
                      (card.key !== "finishHandoff" &&
                        !card.requiredStatus.includes(selectedPatient.appointmentStatus) &&
                        !card.requiredStatus.includes(selectedPatient.queueStatus ?? ""));
                    const isSentDisabled = card.key === "finishHandoff" && isPatientSent;

                    const CardIcon = card.icon;

                    return (
                      <button
                        key={card.key}
                        onClick={() => handleAction(card.key)}
                        disabled={isDisabled || isSentDisabled}
                        className="flex flex-col items-center gap-2.5 p-4 rounded-xl border transition-all text-center group"
                        style={{
                          background: isDisabled || isSentDisabled ? "#f9fafb" : card.bgColor,
                          borderColor: isDisabled || isSentDisabled ? "#f1f5f9" : card.color + "20",
                          opacity: isDisabled || isSentDisabled ? 0.35 : 1,
                          cursor: isDisabled || isSentDisabled ? "not-allowed" : "pointer",
                        }}
                      >
                        <div
                          className="w-11 h-11 rounded-xl flex items-center justify-center transition-transform group-hover:scale-110"
                          style={{
                            background: isDisabled || isSentDisabled ? "#e5e7eb" : card.color + "15",
                          }}
                        >
                          <CardIcon
                            className="w-5 h-5"
                            style={{
                              color: isDisabled || isSentDisabled ? "#94a3b8" : card.color,
                            }}
                          />
                        </div>
                        <span
                          className="text-[11px] font-bold leading-tight"
                          style={{
                            color: isDisabled || isSentDisabled ? "#94a3b8" : card.color,
                          }}
                        >
                          {card.label}
                        </span>
                      </button>
                    );
                  })}
                </div>
              </div>
            ) : (
              <div className="flex flex-col items-center justify-center py-16">
                <div
                  className="w-20 h-20 rounded-2xl flex items-center justify-center mb-4"
                  style={{ background: "#1e3a8a08" }}
                >
                  <Stethoscope className="w-10 h-10" style={{ color: "#1e3a8a30" }} />
                </div>
                <p className="text-base font-bold" style={{ color: "#1e3a8a" }}>
                  اختر مريضاً للبدء
                </p>
                <p className="text-sm mt-1" style={{ color: "#94a3b8" }}>
                  اختر مريضاً من القائمة أعلاه لعرض التفاصيل والإجراءات
                </p>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* ═══════════════════════════════════════════════════════════════
          MODALS — All wiring preserved from original
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

      <OrthoFollowUpModal
        open={orthoFollowUpModalOpen}
        onClose={() => setOrthoFollowUpModalOpen(false)}
        patient={selectedPatient}
        onSave={(data) => {
          const orthoParts = [
            data.notes,
            data.nextVisitPlan ? `خطة قادمة: ${data.nextVisitPlan}` : "",
          ].filter(Boolean).join(" | ");
          const existing = clinicalNotes.treatmentDone;
          updateClinicalNotes({
            treatmentDone: existing ? `${existing} | ${orthoParts}` : orthoParts,
            nextVisitPlan: data.nextVisitPlan || clinicalNotes.nextVisitPlan,
          });
          setOrthoFollowUpModalOpen(false);
          toast.success("تم حفظ متابعة التقويم");
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
            updateClinicalNotes({ treatmentDone: existing ? `${existing} + ${labParts}` : labParts });
          }
          setLabOrderModalOpen(false);
          toast.success("تم حفظ طلب المعمل / الإحالة");
        }}
      />

      <PrescriptionModal
        open={prescriptionModalOpen}
        onClose={() => setPrescriptionModalOpen(false)}
        patient={selectedPatient}
        onSave={(data) => {
          const rxParts = [
            data.prescriptionText ? `الوصفة: ${data.prescriptionText}` : "",
            data.instructions ? `التعليمات: ${data.instructions}` : "",
          ].filter(Boolean).join(" | ");
          if (rxParts) {
            updateClinicalNotes({
              instructions: clinicalNotes.instructions ? `${clinicalNotes.instructions} | ${rxParts}` : rxParts,
            });
          }
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
            followUpDate: data.followUpDate || clinicalNotes.followUpDate,
            suggestedServiceId: data.serviceId || clinicalNotes.suggestedServiceId,
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
