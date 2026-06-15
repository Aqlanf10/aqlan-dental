"use client";

import { useState, useCallback, useMemo, useEffect, useRef } from "react";
import {
  Stethoscope, ClipboardCheck, ListChecks, GitBranch,
  Image, Pill, FlaskConical, CalendarClock, Send, RefreshCw,
  CheckCircle,
  Loader2, FileText,
  Route, Save, X,
} from "lucide-react";
import { useAuthStore } from "@/stores/authStore";
import { toast } from "@/stores/toastStore";
import { useSignalRClinicQueue } from "@/hooks/useSignalRClinicQueue";
import {
  fmtTime,
  APPT_STATUS_LABELS, STATUS_COLORS,
  fmtSessionDuration, ORANGE,
} from "../daily-operations/_lib/constants";

import {
  useDoctorPatientsToday,
  useDoctorPatientSummary,
  useDoctorServices,
  useStartVisit,
  useHandoffToReception,
  useUpdateVisit,
  useDoctorCreatePrescription,
  useDoctorCreateLabOrder,
  useDoctorCreateFollowUpAppointment,
  useDoctorUpdateToothCondition,
  useDoctorCreateGeneralTreatment,
  useCreateDraftInvoice,
  type DoctorPatientItem,
  type ServiceWithPrice,
} from "./_lib/hooks";

import {
  StartVisitPanel,
  ExaminationPanel,
  GeneralDentistryPanel,
  PricedProceduresPanel,
  TreatmentPlanPanel,
  OrthoFollowUpPanel,
  ImagesRadiographsPanel,
  LabOrderPanel,
  PrescriptionPanel,
  HandoffPanel,
  FollowUpSuggestPanel,
} from "./_components/Modals";

/* ═══════════════════════════════════════════════════════════════════════════
   Panel type — single source of truth for which panel is open
   ═══════════════════════════════════════════════════════════════════════════ */
type PanelType =
  | "startVisit"
  | "examination"
  | "generalDentistry"
  | "procedures"
  | "treatmentPlan"
  | "orthoFollowUp"
  | "images"
  | "prescription"
  | "labOrder"
  | "followUp"
  | "handoff"
  | null;

const EMPTY_CLINICAL_NOTES = {
  chiefComplaint: "",
  diagnosis: "",
  treatmentDone: "",
  instructions: "",
  nextVisitPlan: "",
  clinicalNotes: "",
  extraoralExamination: "",
  intraoralExamination: "",
  amountDue: 0,
  suggestedServiceId: "",
  additionalServicesText: "",
  followUpDate: "",
};

function buildPrescriptionDrugs(prescriptionText: string, instructions: string) {
  const lines = prescriptionText
    .split(/\r?\n/)
    .map(line => line.trim())
    .filter(Boolean);

  const source = lines.length > 0 ? lines : [instructions.trim()];
  return source.filter(Boolean).map(line => ({
    name: line,
    dose: "حسب الوصفة",
    frequency: "حسب تعليمات الطبيب",
    duration: "حسب الخطة العلاجية",
    notes: instructions.trim() || undefined,
  }));
}

/* ═══════════════════════════════════════════════════════════════════════════
   Action card definitions
   ═══════════════════════════════════════════════════════════════════════════ */
function appendClinicalText(existing: string, addition: string) {
  const cleanExisting = existing.trim();
  const cleanAddition = addition.trim();
  if (!cleanAddition) return cleanExisting;
  if (!cleanExisting) return cleanAddition;
  return `${cleanExisting}\n${cleanAddition}`;
}

interface ActionCardDef {
  key: PanelType;
  label: string;
  description: string;
  icon: React.ElementType;
  color: string;
  bgColor: string;
  requiredStatus: string[];
}

const ACTION_CARDS: ActionCardDef[] = [
  {
    key: "examination",
    label: "الفحص والتشخيص",
    description: "فحص سريري وتشخيص",
    icon: FileText,
    color: "#2563eb",
    bgColor: "#eff6ff",
    requiredStatus: ["InRoom", "InProgress"],
  },
  {
    key: "generalDentistry",
    label: "طب الأسنان العام",
    description: "توثيق السن والإجراء",
    icon: Stethoscope,
    color: "#0f766e",
    bgColor: "#f0fdfa",
    requiredStatus: ["InRoom", "InProgress"],
  },
  {
    key: "procedures",
    label: "الإجراءات المسعّرة",
    description: "اختيار خدمات مسعّرة",
    icon: ListChecks,
    color: "#9333ea",
    bgColor: "#faf5ff",
    requiredStatus: ["InRoom", "InProgress"],
  },
  {
    key: "treatmentPlan",
    label: "خطة العلاج",
    description: "خطة العلاج والأهداف",
    icon: Route,
    color: "#2563eb",
    bgColor: "#eff6ff",
    requiredStatus: ["InRoom", "InProgress"],
  },
  {
    key: "orthoFollowUp",
    label: "متابعة التقويم",
    description: "متابعة حالة التقويم",
    icon: GitBranch,
    color: "#f5922e",
    bgColor: "#fff7ed",
    requiredStatus: ["InRoom", "InProgress"],
  },
  {
    key: "images",
    label: "الصور والأشعة",
    description: "صور وأشعة سينية",
    icon: Image,
    color: "#64748b",
    bgColor: "#f8fafc",
    requiredStatus: ["InRoom", "InProgress"],
  },
  {
    key: "prescription",
    label: "الوصفة والتعليمات",
    description: "وصفة طبية وتعليمات",
    icon: Pill,
    color: "#dc2626",
    bgColor: "#fef2f2",
    requiredStatus: ["InRoom", "InProgress"],
  },
  {
    key: "labOrder",
    label: "طلب معمل / إحالة",
    description: "طلب مخبري أو إحالة",
    icon: FlaskConical,
    color: "#0891b2",
    bgColor: "#ecfeff",
    requiredStatus: ["InRoom", "InProgress"],
  },
  {
    key: "followUp",
    label: "موعد المتابعة",
    description: "تحديد موعد متابعة",
    icon: CalendarClock,
    color: "#7c3aed",
    bgColor: "#f5f3ff",
    requiredStatus: ["InRoom", "InProgress"],
  },
  {
    key: "startVisit",
    label: "تسجيل الفحص",
    description: "بدء تسجيل الزيارة",
    icon: ClipboardCheck,
    color: "#16a34a",
    bgColor: "#f0fdf4",
    requiredStatus: ["InRoom", "Called"],
  },
  {
    key: "handoff",
    label: "إنهاء وتسليم للاستقبال",
    description: "تسليم المريض للاستقبال",
    icon: Send,
    color: "#b91c1c",
    bgColor: "#fef2f2",
    requiredStatus: ["InRoom", "InProgress"],
  },
];

/* ═══════════════════════════════════════════════════════════════════════════
   MAIN PAGE — Microsoft Fluent-style Doctor Clinic Workspace
   ═══════════════════════════════════════════════════════════════════════════ */
export default function DoctorClinicPage() {
  const { user } = useAuthStore();
  const isAdmin = user?.role === "Admin";
  // FIX: Only use user.doctorId — do NOT fall back to user.id (auth user ID)
  // because Doctors table IDs differ from Users table IDs.
  // If doctorId is missing, the page shows an error message instead of an empty list.
  const doctorId = user?.doctorId ?? "";
  const doctorName = user?.doctorName ?? user?.username ?? "الطبيب";

  // ── SignalR ──
  const { isConnected: signalrConnected } = useSignalRClinicQueue();

  // ── Active panel state (single source of truth — no route navigation) ──
  const [activePanel, setActivePanel] = useState<PanelType>(null);

  // ── Admin filter state ──
  const [filterDoctorId, setFilterDoctorId] = useState<string | null>(null);
  const [filterRoomId, setFilterRoomId] = useState<string | null>(null);

  // ── Data ──
  const { data: patients = [], isLoading, refetch } = useDoctorPatientsToday(
    isAdmin ? { includeAll: true } : { doctorId }
  );
  const { data: services = [] } = useDoctorServices();
  const startVisitMutation = useStartVisit();
  const handoffMutation = useHandoffToReception();
  const updateVisitMutation = useUpdateVisit();
  const createPrescriptionMutation = useDoctorCreatePrescription();
  const createLabOrderMutation = useDoctorCreateLabOrder();
  const createFollowUpMutation = useDoctorCreateFollowUpAppointment();
  const updateToothMutation = useDoctorUpdateToothCondition();
  const createGeneralTreatmentMutation = useDoctorCreateGeneralTreatment();
  const createDraftInvoiceMutation = useCreateDraftInvoice();

  // ── Selected patient ──
  const [selectedPatient, setSelectedPatient] = useState<DoctorPatientItem | null>(null);
  const activePatientId = selectedPatient?.patientId ?? null;
  const { data: selectedSummary } = useDoctorPatientSummary(activePatientId);

  // ── Selected services for visit ──
  const [, setSelectedServices] = useState<ServiceWithPrice[]>([]);

  // ── Search ──
  const [searchQuery, setSearchQuery] = useState("");
  const searchRef = useRef<HTMLInputElement>(null);

  // ── Sent patients tracking ──
  const [sentPatients, setSentPatients] = useState<Set<string>>(new Set());

  // ── Clinical notes (accumulated before handoff) ──
  const [clinicalNotes, setClinicalNotes] = useState(EMPTY_CLINICAL_NOTES);
  const [hasUnsavedClinicalNotes, setHasUnsavedClinicalNotes] = useState(false);

  const resetClinicalWorkspace = useCallback(() => {
    setSelectedServices([]);
    setClinicalNotes(EMPTY_CLINICAL_NOTES);
    setHasUnsavedClinicalNotes(false);
  }, []);

  const confirmDiscardUnsavedClinicalNotes = useCallback(() => {
    if (!hasUnsavedClinicalNotes) return true;
    return window.confirm("توجد بيانات زيارة غير محفوظة. هل تريد تجاهلها والانتقال؟");
  }, [hasUnsavedClinicalNotes]);

  const clearSelectedPatient = useCallback(() => {
    if (!confirmDiscardUnsavedClinicalNotes()) return;
    setActivePanel(null);
    setSelectedPatient(null);
    resetClinicalWorkspace();
  }, [confirmDiscardUnsavedClinicalNotes, resetClinicalWorkspace]);

  const selectPatient = useCallback((patient: DoctorPatientItem) => {
    if (selectedPatient?.appointmentId === patient.appointmentId) {
      setSelectedPatient(patient);
      return;
    }
    if (!confirmDiscardUnsavedClinicalNotes()) return;
    setActivePanel(null);
    setSelectedPatient(patient);
    resetClinicalWorkspace();
  }, [selectedPatient?.appointmentId, confirmDiscardUnsavedClinicalNotes, resetClinicalWorkspace]);

  // ── Admin filter dropdowns ──
  const availableDoctors = useMemo(() => {
    if (!isAdmin) return [];
    const map = new Map<string, string>();
    for (const p of patients) {
      if (p.doctorId && p.doctorName) map.set(p.doctorId, p.doctorName);
    }
    return Array.from(map, ([id, name]) => ({ id, name }));
  }, [patients, isAdmin]);

  const availableRooms = useMemo(() => {
    if (!isAdmin) return [];
    const map = new Map<string, string>();
    for (const p of patients) {
      if (p.roomId && p.roomName) map.set(p.roomId, p.roomName);
    }
    return Array.from(map, ([id, name]) => ({ id, name }));
  }, [patients, isAdmin]);

  // ── Filtered patients ──
  const filteredPatients = useMemo(() => {
    let result = patients;
    if (isAdmin && filterDoctorId) result = result.filter(p => p.doctorId === filterDoctorId);
    if (isAdmin && filterRoomId) result = result.filter(p => p.roomId === filterRoomId);
    if (searchQuery.trim()) {
      const q = searchQuery.trim().toLowerCase();
      result = result.filter(p =>
        p.patientName.toLowerCase().includes(q) ||
        (p.serviceName && p.serviceName.toLowerCase().includes(q)) ||
        (isAdmin && p.doctorName && p.doctorName.toLowerCase().includes(q))
      );
    }
    return result;
  }, [patients, searchQuery, isAdmin, filterDoctorId, filterRoomId]);

  // ── Derived state ──
  const medicalAlerts = selectedSummary?.medicalAlerts ?? [];
  const isPatientSent = selectedPatient ? sentPatients.has(selectedPatient.appointmentId) : false;
  // Phase 1 ortho integration: highlight ortho follow-up when the patient has an active case
  const activeOrthoCase = selectedSummary?.activeOrthoCase ?? null;
  const hasActiveOrthoCase = !!activeOrthoCase && String(activeOrthoCase.status).toLowerCase() === "active";

  // Sync selectedPatient state with the latest patients array updates
  useEffect(() => {
    if (selectedPatient) {
      const updated = patients.find(p => p.appointmentId === selectedPatient.appointmentId);
      if (updated) {
        if (
          updated.visitId !== selectedPatient.visitId ||
          updated.appointmentStatus !== selectedPatient.appointmentStatus ||
          updated.queueStatus !== selectedPatient.queueStatus
        ) {
          setSelectedPatient(updated);
        }
      }
    }
  }, [patients, selectedPatient]);

  // ── Keyboard shortcuts ──
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        if (activePanel) setActivePanel(null);
        else clearSelectedPatient();
        return;
      }
      if (e.ctrlKey && e.key === "r") { e.preventDefault(); refetch(); return; }
      if (e.ctrlKey && e.key === "f") { e.preventDefault(); searchRef.current?.focus(); return; }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [refetch, activePanel, clearSelectedPatient]);

  // ── Action handler — opens in-page panel, never navigates ──
  const handleAction = useCallback((cardKey: PanelType) => {
    if (!selectedPatient) { toast.error("اختر مريضاً أولاً"); return; }
    if (cardKey === "handoff") {
      if (isPatientSent) { toast.error("تم إرسال هذا المريض للاستقبال بالفعل"); return; }
      setActivePanel("handoff");
      return;
    }
    const card = ACTION_CARDS.find(c => c.key === cardKey);
    if (card && !card.requiredStatus.includes(selectedPatient.appointmentStatus) &&
        !card.requiredStatus.includes(selectedPatient.queueStatus ?? "")) {
      toast.error("هذا الإجراء غير متاح لحالة المريض الحالية");
      return;
    }
    setActivePanel(cardKey);
  }, [selectedPatient, isPatientSent]);

  // ── Start visit handler ──
  const handleStartVisit = useCallback(async () => {
    if (!selectedPatient) return;
    try {
      await startVisitMutation.mutateAsync(selectedPatient.appointmentId);
      toast.success("تم بدء الزيارة بنجاح");
      setActivePanel(null);
    } catch { toast.error("فشل بدء الزيارة — حاول مرة أخرى"); }
  }, [selectedPatient, startVisitMutation]);

  // ── Handoff to reception (F1+F3+F4+F6: structured data, append notes, draft invoice) ──
  const handleHandoff = useCallback(async () => {
    if (!selectedPatient?.visitId) { toast.error("لا توجد زيارة نشطة"); return; }
    if (sentPatients.has(selectedPatient.appointmentId)) { toast.error("تم إرسال هذا المريض للاستقبال بالفعل"); return; }
    try {
      await handoffMutation.mutateAsync({
        visitId: selectedPatient.visitId,
        body: {
          chiefComplaint: clinicalNotes.chiefComplaint || undefined,
          treatmentDone: clinicalNotes.treatmentDone || undefined,
          diagnosis: clinicalNotes.diagnosis || undefined,
          nextVisitPlan: clinicalNotes.nextVisitPlan || undefined,
          instructions: clinicalNotes.instructions || undefined,
          extraoralExamination: clinicalNotes.extraoralExamination || undefined,
          intraoralExamination: clinicalNotes.intraoralExamination || undefined,
          followUpDate: clinicalNotes.followUpDate || undefined,
          amountDue: clinicalNotes.amountDue !== 0 ? clinicalNotes.amountDue || undefined : 0,
          suggestedServiceId: clinicalNotes.suggestedServiceId || undefined,
          additionalServicesText: clinicalNotes.additionalServicesText || undefined,
          notes: clinicalNotes.clinicalNotes || undefined,
        },
      });
      // F4: Create draft invoice when amountDue > 0 and suggestedServiceId exists
      if (clinicalNotes.amountDue > 0 && clinicalNotes.suggestedServiceId && selectedPatient.visitId) {
        try {
          await createDraftInvoiceMutation.mutateAsync(selectedPatient.visitId);
          toast.success("تم إنشاء فاتورة مبدئية وإرسال المريض للاستقبال");
        } catch {
          // Draft invoice failure is non-blocking — handoff already succeeded
          toast.error("تم إرسال المريض للاستقبال لكن فشل إنشاء الفاتورة المبدئية");
        }
      } else {
        toast.success("تم إرسال المريض للاستقبال للتحصيل والخروج");
      }
      setActivePanel(null);
      setSentPatients(prev => new Set(prev).add(selectedPatient.appointmentId));
      setSelectedPatient(null);
      resetClinicalWorkspace();
    } catch { toast.error("فشل إرسال المريض للاستقبال"); }
  }, [selectedPatient, handoffMutation, clinicalNotes, sentPatients, resetClinicalWorkspace, createDraftInvoiceMutation]);

  // ── Update clinical notes ──
  const updateClinicalNotes = useCallback((updates: Partial<typeof clinicalNotes>) => {
    setClinicalNotes(prev => ({ ...prev, ...updates }));
    setHasUnsavedClinicalNotes(true);
  }, []);

  // ── Save visit button (F3: save structured fields to individual Visit fields) ──
  const handleSaveVisit = useCallback(async () => {
    if (!selectedPatient) { toast.error("اختر مريضاً أولاً"); return; }

    // FIX: Auto-start visit if not yet started — simplifies the doctor's workflow
    // so they can fill data first and save directly without needing to click "تسجيل الفحص" first.
    if (!selectedPatient.visitId) {
      try {
        await startVisitMutation.mutateAsync(selectedPatient.appointmentId);
        toast.success("تم بدء الزيارة وحفظ البيانات تلقائياً");
        // After starting visit, the patient data refreshes with visitId —
        // save will continue in the next render cycle via hasUnsavedClinicalNotes.
        setHasUnsavedClinicalNotes(true);
        return;
      } catch {
        toast.error("فشل بدء الزيارة — حاول مرة أخرى");
        return;
      }
    }

    try {
      await updateVisitMutation.mutateAsync({
        visitId: selectedPatient.visitId,
        patientId: selectedPatient.patientId,
        body: {
          chiefComplaint: clinicalNotes.chiefComplaint || undefined,
          diagnosis: clinicalNotes.diagnosis || undefined,
          treatmentDone: clinicalNotes.treatmentDone || undefined,
          instructions: clinicalNotes.instructions || undefined,
          nextVisitPlan: clinicalNotes.nextVisitPlan || undefined,
          cost: clinicalNotes.amountDue > 0 ? clinicalNotes.amountDue : undefined,
          nextVisitDate: clinicalNotes.followUpDate || undefined,
          // S1: Prevent data loss — send these on interim save too
          serviceId: clinicalNotes.suggestedServiceId || undefined,
          extraoralExamination: clinicalNotes.extraoralExamination || undefined,
          intraoralExamination: clinicalNotes.intraoralExamination || undefined,
          additionalServicesText: clinicalNotes.additionalServicesText || undefined,
          handoffNotes: clinicalNotes.clinicalNotes || undefined,
        },
      });
      setHasUnsavedClinicalNotes(false);
      toast.success("تم حفظ بيانات الزيارة في ملف المريض بنجاح");
    } catch {
      toast.error("فشل حفظ بيانات الزيارة — حاول مرة أخرى");
    }
  }, [selectedPatient, updateVisitMutation, clinicalNotes, startVisitMutation]);

  // ── Patient initials ──
  const getInitials = (name: string) => {
    const parts = name.split(" ").filter(Boolean);
    return parts.length >= 2 ? parts[0][0] + parts[1][0] : parts[0]?.[0] ?? "؟";
  };

  // ── Whether panel is open ──
  const hasPanelOpen = activePanel !== null;

  // ── Panel title map ──
  const PANEL_TITLES: Record<string, string> = {
    startVisit: "تسجيل الفحص",
    examination: "الفحص والتشخيص",
    procedures: "الإجراءات المسعّرة",
    treatmentPlan: "خطة العلاج",
    orthoFollowUp: "متابعة التقويم",
    images: "الصور والأشعة",
    prescription: "الوصفة والتعليمات",
    labOrder: "طلب معمل / إحالة",
    followUp: "موعد المتابعة",
    handoff: "إنهاء وتسليم للاستقبال",
  };

  /* ═══════════════════════════════════════════════════════════════════════════
   Render — Vertical Sidebar + Workspace Layout
   ═══════════════════════════════════════════════════════════════════════════ */
  return (
    <div className="h-screen flex flex-col overflow-hidden bg-[#f5f5f5]" dir="rtl">

      {/* ═══════════════════════════════════════════════════════════════
          COMMAND BAR — White, clean, Microsoft-style
          ═══════════════════════════════════════════════════════════════ */}
      <header className="flex-shrink-0 bg-white border-b border-[#e0e0e0] px-4 md:px-6 py-2.5">
        <div className="flex items-center gap-3">
          {/* Title area */}
          <div className="flex items-center gap-3 flex-shrink-0">
            <div className="w-9 h-9 rounded-lg flex items-center justify-center" style={{ background: "#1e3a8a" }}>
              <Stethoscope className="w-4.5 h-4.5 text-white" />
            </div>
            <div className="leading-tight">
              <h1 className="text-sm font-bold text-[#1a1a1a]">عيادة الطبيب</h1>
              <p className="text-[11px] text-[#666]">
                {isAdmin ? "كل غرف العلاج" : "مرضاي اليوم"}
              </p>
            </div>
          </div>

          <div className="flex-1" />

          {/* Command bar actions */}
          <div className="flex items-center gap-1.5">
            {/* Refresh */}
            <button onClick={() => refetch()} className="flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-medium text-[#444] hover:bg-[#f0f0f0] transition-colors" title="تحديث (Ctrl+R)">
              <RefreshCw className="w-3.5 h-3.5" />
              <span className="hidden lg:inline">تحديث</span>
            </button>

            {/* Save — enabled even without visitId (auto-starts visit) */}
            <button
              onClick={handleSaveVisit}
              disabled={updateVisitMutation.isPending || startVisitMutation.isPending || !selectedPatient}
              className="flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-medium text-[#444] hover:bg-[#f0f0f0] disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              <Save className="w-3.5 h-3.5" />
              <span className="hidden lg:inline">{updateVisitMutation.isPending ? "جار الحفظ..." : "حفظ في ملف المريض"}</span>
            </button>

            {/* Handoff */}
            <button
              onClick={() => handleAction("handoff")}
              className="flex items-center gap-1.5 px-4 py-1.5 rounded-md text-xs font-bold text-white transition-colors"
              style={{ background: "#f5922e" }}
            >
              <Send className="w-3.5 h-3.5" />
              <span className="hidden lg:inline">إنهاء وإرسال</span>
            </button>

            {/* Divider */}
            <div className="w-px h-6 mx-1 hidden md:block bg-[#e0e0e0]" />

            {/* SignalR */}
            <div className="hidden md:flex items-center gap-1.5 text-[10px] font-medium" style={{ color: signalrConnected ? "#16a34a" : "#dc2626" }}>
              <span className="w-2 h-2 rounded-full" style={{ background: signalrConnected ? "#16a34a" : "#dc2626" }} />
              <span>{signalrConnected ? "متصل" : "غير متصل"}</span>
            </div>

            {/* Avatar */}
            <div className="w-8 h-8 rounded-full flex items-center justify-center text-[11px] font-bold text-white flex-shrink-0" style={{ background: "linear-gradient(135deg, #1e3a8a, #2563eb)" }}>
              {getInitials(doctorName)}
            </div>
          </div>
        </div>
      </header>

      {/* ═══════════════════════════════════════════════════════════════
          MAIN CONTENT AREA — Horizontal split: Workspace + Patient Sidebar
          ═══════════════════════════════════════════════════════════════ */}
      <div className="flex-1 flex overflow-hidden">

        {/* ═══════════════════════════════════════════════════════════════
            WORKSPACE (flex-1, scrollable)
            ═══════════════════════════════════════════════════════════════ */}
        <div className="flex-1 flex flex-col overflow-hidden bg-[#f5f5f5]">

          {/* ── ACTION BUTTONS BAR — compact, sticky at top of workspace ── */}
          <div className="flex-shrink-0 bg-white border-b border-[#e0e0e0] px-3 py-1.5">
            <div className="flex items-center gap-1.5 overflow-x-auto">
              {ACTION_CARDS.map(card => {
                const isDisabled = !selectedPatient ||
                  (card.key !== "handoff" && !card.requiredStatus.includes(selectedPatient.appointmentStatus) && !card.requiredStatus.includes(selectedPatient.queueStatus ?? ""));
                const isSentDisabled = card.key === "handoff" && isPatientSent;
                const isCurrentPanel = activePanel === card.key;
                const isOrthoHighlighted = card.key === "orthoFollowUp" && hasActiveOrthoCase && !isDisabled;
                const CardIcon = card.icon;

                return (
                  <button
                    key={card.key}
                    onClick={() => handleAction(card.key)}
                    disabled={isDisabled || isSentDisabled}
                    className="flex items-center gap-1 px-2 py-1 rounded text-[9px] font-semibold flex-shrink-0 border transition-all whitespace-nowrap"
                    style={{
                      background: isCurrentPanel ? card.bgColor : isOrthoHighlighted ? "#f5f3ff" : "#fff",
                      borderColor: isCurrentPanel ? card.color : isOrthoHighlighted ? "#7c3aed" : "#e5e7eb",
                      color: isCurrentPanel ? card.color : (isDisabled || isSentDisabled ? "#9ca3af" : "#374151"),
                      opacity: (isDisabled || isSentDisabled) ? 0.35 : 1,
                      borderWidth: isCurrentPanel || isOrthoHighlighted ? "1.5px" : "1px",
                      boxShadow: isOrthoHighlighted ? "0 0 0 2px rgba(124, 58, 237, 0.15)" : undefined,
                    }}
                  >
                    <CardIcon className="w-3 h-3" />
                    <span>{card.label}</span>
                    {isOrthoHighlighted && (
                      <span className="text-[8px] font-bold px-1 py-px rounded-full" style={{ background: "#ede9fe", color: "#7c3aed" }}>
                        حالة نشطة
                      </span>
                    )}
                  </button>
                );
              })}
            </div>
          </div>

          {/* ── CLINICAL VISIT DATA SUMMARY — always visible when visit is active ── */}
          {selectedPatient?.visitId && (
            <div className="flex-shrink-0 bg-white border-b border-[#e5e7eb] px-4 py-2.5">
              <div className="flex items-center justify-between mb-2">
                <div className="flex items-center gap-2">
                  <FileText className="w-3.5 h-3.5 text-[#2563eb]" />
                  <h3 className="text-[11px] font-bold text-[#1a1a1a]">بيانات الزيارة السريرية</h3>
                  {hasUnsavedClinicalNotes && (
                    <span className="text-[9px] px-1.5 py-0.5 rounded-full bg-amber-100 text-amber-700 font-semibold">غير محفوظ</span>
                  )}
                </div>
                <button
                  onClick={handleSaveVisit}
                  disabled={updateVisitMutation.isPending || startVisitMutation.isPending}
                  className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-[10px] font-bold text-white bg-[#16a34a] hover:bg-[#15803d] disabled:opacity-50 transition-colors"
                >
                  <Save className="w-3 h-3" />
                  {(updateVisitMutation.isPending || startVisitMutation.isPending) ? "جار الحفظ..." : "حفظ في ملف المريض"}
                </button>
              </div>
              <div className="grid grid-cols-2 md:grid-cols-3 gap-x-4 gap-y-1.5">
                {[
                  { label: "الشكوى الرئيسية", value: clinicalNotes.chiefComplaint },
                  { label: "فحص خارج الفم", value: clinicalNotes.extraoralExamination },
                  { label: "فحص داخل الفم", value: clinicalNotes.intraoralExamination },
                  { label: "التشخيص", value: clinicalNotes.diagnosis },
                  { label: "الإجراء المنفذ", value: clinicalNotes.treatmentDone },
                  { label: "ملاحظات سريرية", value: clinicalNotes.clinicalNotes },
                  { label: "التعليمات", value: clinicalNotes.instructions },
                  { label: "خطة الزيارة القادمة", value: clinicalNotes.nextVisitPlan },
                  { label: "المبلغ المستحق", value: clinicalNotes.amountDue > 0 ? `${clinicalNotes.amountDue} ر.س` : "" },
                ].map(field => (
                  <div key={field.label} className="min-w-0">
                    <span className="text-[9px] font-semibold text-[#9ca3af]">{field.label}</span>
                    <p className="text-[10px] text-[#1a1a1a] truncate" title={field.value || "—"}>{field.value || "—"}</p>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* ── PANEL CONTENT AREA — fills remaining space ── */}
          <div className="flex-1 overflow-y-auto">
            {hasPanelOpen && selectedPatient ? (
              /* ── Panel open: show panel content ── */
              <div className="max-w-3xl mx-auto p-5">
                {/* Panel header */}
                <div className="flex items-center justify-between mb-4">
                  <div className="flex items-center gap-2">
                    {(() => {
                      const card = ACTION_CARDS.find(c => c.key === activePanel);
                      const CardIcon = card?.icon ?? FileText;
                      return (
                        <>
                          <div className="w-8 h-8 rounded-lg flex items-center justify-center" style={{ background: (card?.color ?? "#2563eb") + "15" }}>
                            <CardIcon className="w-4 h-4" style={{ color: card?.color ?? "#2563eb" }} />
                          </div>
                          <div>
                            <h2 className="text-sm font-bold text-[#1a1a1a]">{PANEL_TITLES[activePanel ?? ""] ?? card?.label ?? ""}</h2>
                            <p className="text-[10px] text-[#9ca3af]">{selectedPatient.patientName}</p>
                          </div>
                        </>
                      );
                    })()}
                  </div>
                  <button onClick={() => setActivePanel(null)} className="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-[#e5e7eb] transition-colors text-[#6b7280]">
                    <X className="w-4 h-4" />
                  </button>
                </div>

                {/* Panel body */}
                <div className="bg-white rounded-xl border border-[#e5e7eb] shadow-sm">
                  {activePanel === "startVisit" && (
                    <StartVisitPanel patient={selectedPatient} isPending={startVisitMutation.isPending} onConfirm={handleStartVisit} onClose={() => setActivePanel(null)} />
                  )}
                  {activePanel === "examination" && (
                    <ExaminationPanel patient={selectedPatient} diagnosis={clinicalNotes.diagnosis} medicalAlerts={medicalAlerts} onClose={() => setActivePanel(null)}
                      onSave={(data) => {
                        // F3: Map structured clinical fields to individual Visit fields
                        updateClinicalNotes({
                          chiefComplaint: data.chiefComplaint || clinicalNotes.chiefComplaint,
                          diagnosis: data.diagnosis || clinicalNotes.diagnosis,
                          treatmentDone: data.treatmentDone || clinicalNotes.treatmentDone,
                          clinicalNotes: data.clinicalNotes || clinicalNotes.clinicalNotes,
                          extraoralExamination: data.extraoral || clinicalNotes.extraoralExamination,
                          intraoralExamination: data.intraoral || clinicalNotes.intraoralExamination,
                        });
                        setActivePanel(null);
                        toast.success("تم حفظ الفحص والتشخيص بنجاح");
                      }}
                    />
                  )}
                  {activePanel === "generalDentistry" && (
                    <GeneralDentistryPanel
                      patient={selectedPatient}
                      services={services}
                      isPending={updateToothMutation.isPending || createGeneralTreatmentMutation.isPending}
                      onClose={() => setActivePanel(null)}
                      onSave={async (data) => {
                        if (!selectedPatient) return;

                        await updateToothMutation.mutateAsync({
                          patientId: selectedPatient.patientId,
                          body: {
                            toothNumber: data.toothNumber,
                            condition: data.condition,
                            surfacesAffected: data.surfacesAffected || null,
                            treatmentDone: data.treatmentSummary,
                            notes: data.chartNotes || null,
                          },
                        });

                        await createGeneralTreatmentMutation.mutateAsync({
                          patientId: selectedPatient.patientId,
                          visitId: selectedPatient.visitId || undefined,
                          doctorId: selectedPatient.doctorId || undefined,
                          toothNumber: data.toothNumber,
                          treatmentType: data.procedureType,
                          materialUsed: data.materialUsed || undefined,
                          anesthesiaType: data.anesthesiaType || undefined,
                          cost: data.cost > 0 ? data.cost : undefined,
                          notes: data.chartNotes || data.treatmentSummary,
                        });

                        updateClinicalNotes({
                          diagnosis: appendClinicalText(clinicalNotes.diagnosis, `[طب الأسنان العام] السن ${data.toothNumber}: ${data.diagnosis}`),
                          treatmentDone: appendClinicalText(clinicalNotes.treatmentDone, data.treatmentSummary),
                          instructions: data.instructions ? appendClinicalText(clinicalNotes.instructions, data.instructions) : clinicalNotes.instructions,
                          nextVisitPlan: data.nextVisitPlan || clinicalNotes.nextVisitPlan,
                          amountDue: clinicalNotes.amountDue + (data.cost || 0),
                          suggestedServiceId: data.serviceId || clinicalNotes.suggestedServiceId,
                        });
                        setActivePanel(null);
                        toast.success("تم حفظ طب الأسنان العام وربطه بملف المريض");
                      }}
                    />
                  )}
                  {activePanel === "procedures" && (
                    <PricedProceduresPanel patient={selectedPatient} services={services} currentAmountDue={clinicalNotes.amountDue} currentTreatmentDone={clinicalNotes.treatmentDone} onClose={() => setActivePanel(null)}
                      onSave={(data) => {
                        // F6: First service in suggestedServiceId, additional services text in additionalServicesText
                        updateClinicalNotes({
                          treatmentDone: data.treatmentDone,
                          amountDue: data.totalAmount,
                          suggestedServiceId: data.suggestedServiceId,
                          additionalServicesText: data.additionalServicesText || clinicalNotes.additionalServicesText,
                        });
                        setActivePanel(null);
                        toast.success("تم حفظ الإجراءات المسعّرة بنجاح");
                      }}
                    />
                  )}
                  {activePanel === "treatmentPlan" && (
                    <TreatmentPlanPanel patient={selectedPatient} onClose={() => setActivePanel(null)}
                      onSave={(data) => { updateClinicalNotes({ nextVisitPlan: data.plan }); setActivePanel(null); toast.success("تم حفظ خطة العلاج بنجاح"); }}
                    />
                  )}
                  {activePanel === "orthoFollowUp" && (
                    <OrthoFollowUpPanel patient={selectedPatient} activeOrthoCase={activeOrthoCase} onClose={() => setActivePanel(null)}
                      onSave={(data) => {
                        const parts = [data.notes, data.nextVisitPlan ? `خطة قادمة: ${data.nextVisitPlan}` : ""].filter(Boolean).join(" | ");
                        const existing = clinicalNotes.treatmentDone;
                        updateClinicalNotes({ treatmentDone: existing ? `${existing} | ${parts}` : parts, nextVisitPlan: data.nextVisitPlan || clinicalNotes.nextVisitPlan });
                        setActivePanel(null);
                        toast.success("تم حفظ متابعة التقويم بنجاح");
                      }}
                    />
                  )}
                  {activePanel === "images" && (
                    <ImagesRadiographsPanel patient={selectedPatient} onClose={() => setActivePanel(null)} onSave={() => setActivePanel(null)} />
                  )}
                  {activePanel === "prescription" && (
                    <PrescriptionPanel patient={selectedPatient} onClose={() => setActivePanel(null)} isPending={createPrescriptionMutation.isPending}
                      onSave={async (data) => {
                        if (!selectedPatient) return;
                        const drugs = buildPrescriptionDrugs(data.prescriptionText, data.instructions);
                        await createPrescriptionMutation.mutateAsync({
                          patientId: selectedPatient.patientId,
                          doctorId: selectedPatient.doctorId || undefined,
                          visitId: selectedPatient.visitId || undefined,
                          diagnosis: clinicalNotes.diagnosis || undefined,
                          notes: data.instructions || undefined,
                          drugs,
                        });
                        updateClinicalNotes({
                          instructions: data.instructions || clinicalNotes.instructions,
                          treatmentDone: clinicalNotes.treatmentDone ? `${clinicalNotes.treatmentDone} | وصفة طبية` : "وصفة طبية",
                        });
                        setActivePanel(null);
                        toast.success("تم إنشاء الوصفة الطبية وربطها بالزيارة بنجاح");
                      }}
                    />
                  )}
                  {activePanel === "labOrder" && (
                    <LabOrderPanel patient={selectedPatient} onClose={() => setActivePanel(null)} isPending={createLabOrderMutation.isPending}
                      onSave={async (data) => {
                        if (!selectedPatient) return;
                        const applianceType = data.labWorkType || data.referralDepartment || "طلب معمل";
                        await createLabOrderMutation.mutateAsync({
                          patientId: selectedPatient.patientId,
                          applianceType,
                          expectedDate: data.deliveryDate || undefined,
                          instructions: data.labInstructions || undefined,
                          doctorId: selectedPatient.doctorId || undefined,
                          shade: data.shade || undefined,
                          restorationType: data.labWorkType || undefined,
                          visitId: selectedPatient.visitId || undefined,
                        });
                        updateClinicalNotes({
                          treatmentDone: clinicalNotes.treatmentDone ? `${clinicalNotes.treatmentDone} | طلب معمل: ${applianceType}` : `طلب معمل: ${applianceType}`,
                        });
                        setActivePanel(null);
                        toast.success("تم إنشاء طلب المعمل وربطه بالزيارة بنجاح");
                      }}
                    />
                  )}
                  {activePanel === "followUp" && (
                    <FollowUpSuggestPanel patient={selectedPatient} services={services} onClose={() => setActivePanel(null)} isPending={createFollowUpMutation.isPending}
                      onSave={async (data) => {
                        if (!selectedPatient?.doctorId) {
                          toast.error("لا يوجد طبيب مرتبط لإنشاء موعد المتابعة");
                          return;
                        }
                        await createFollowUpMutation.mutateAsync({
                          patientId: selectedPatient.patientId,
                          doctorId: selectedPatient.doctorId,
                          appointmentDate: data.followUpDate,
                          startTime: data.followUpTime || "09:00",
                          serviceId: data.serviceId || undefined,
                          notes: data.reason || "موعد متابعة من عيادة الطبيب",
                        });
                        updateClinicalNotes({
                          followUpDate: data.followUpDate,
                          nextVisitPlan: data.reason || clinicalNotes.nextVisitPlan,
                        });
                        setActivePanel(null);
                        toast.success("تم إنشاء موعد المتابعة بنجاح");
                      }}
                    />
                  )}
                  {activePanel === "handoff" && (
                    <HandoffPanel patient={selectedPatient} clinicalNotes={clinicalNotes} selectedServicesTotal={clinicalNotes.amountDue} onClose={() => setActivePanel(null)} onConfirm={handleHandoff} isPending={handoffMutation.isPending} />
                  )}
                </div>
              </div>
            ) : (
              /* ── Empty state — no patient selected or no panel open ── */
              <div className="flex flex-col items-center justify-center h-full min-h-[300px]">
                <div className="w-14 h-14 rounded-2xl flex items-center justify-center mb-3 bg-[#1e3a8a]/5">
                  <Stethoscope className="w-7 h-7 text-[#1e3a8a]/20" />
                </div>
                <p className="text-sm font-bold text-[#1a1a1a]">
                  {selectedPatient ? selectedPatient.patientName : "اختر مريضاً من القائمة الجانبية"}
                </p>
                <p className="text-xs mt-1 text-[#9ca3af]">اضغط على أحد الأزرار أعلاه لفتح الإجراء المطلوب</p>
              </div>
            )}
          </div>
        </div>

        {/* ═══════════════════════════════════════════════════════════════
            PATIENT SIDEBAR (280px fixed) — in RTL, appears on the RIGHT
            ═══════════════════════════════════════════════════════════════ */}
        <aside className="w-[280px] flex-shrink-0 bg-white border-r border-[#e0e0e0] flex flex-col overflow-hidden">
          {/* Sidebar header: title + search + filters */}
          <div className="p-3 border-b border-[#e0e0e0]">
            <div className="flex items-center justify-between mb-2">
              <h2 className="text-xs font-bold text-[#1e3a8a]">المرضى</h2>
              <span className="text-[10px] px-1.5 py-0.5 rounded-full font-bold bg-[#1e3a8a]/8 text-[#1e3a8a]">
                {filteredPatients.length}
              </span>
            </div>
            <input
              ref={searchRef}
              type="text"
              value={searchQuery}
              onChange={e => setSearchQuery(e.target.value)}
              placeholder="بحث عن مريض..."
              className="w-full text-[11px] rounded-md border border-[#d1d5db] px-2.5 py-1.5 outline-none focus:border-[#2563eb] transition-colors bg-white mb-2"
            />
            {/* Admin filters */}
            {isAdmin && availableDoctors.length > 1 && (
              <select
                value={filterDoctorId ?? ""}
                onChange={e => setFilterDoctorId(e.target.value || null)}
                className="w-full text-[11px] rounded-md border border-[#d1d5db] px-2 py-1 bg-white text-[#374151] outline-none focus:border-[#2563eb] transition-colors mb-1.5"
              >
                <option value="">كل الأطباء</option>
                {availableDoctors.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
              </select>
            )}
            {isAdmin && availableRooms.length > 1 && (
              <select
                value={filterRoomId ?? ""}
                onChange={e => setFilterRoomId(e.target.value || null)}
                className="w-full text-[11px] rounded-md border border-[#d1d5db] px-2 py-1 bg-white text-[#374151] outline-none focus:border-[#2563eb] transition-colors"
              >
                <option value="">كل الغرف</option>
                {availableRooms.map(r => <option key={r.id} value={r.id}>{r.name}</option>)}
              </select>
            )}
          </div>

          {/* Patient list — vertical scrollable */}
          <div className="flex-1 overflow-y-auto">
            {isLoading && (
              <div className="flex items-center justify-center py-8">
                <Loader2 className="w-5 h-5 animate-spin text-[#2563eb]" />
              </div>
            )}

            {!isLoading && filteredPatients.length === 0 && (
              <div className="p-4 text-center">
                <p className="text-[11px] text-[#9ca3af] font-medium">لا يوجد مرضى</p>
              </div>
            )}

            {!isLoading && filteredPatients.length > 0 && filteredPatients.map(p => {
              const isSelected = selectedPatient?.appointmentId === p.appointmentId;
              const isActive = ["InRoom", "InProgress"].includes(p.appointmentStatus);
              const isWaiting = p.queueStatus === "Waiting" || p.queueStatus === "Called";
              const sc = STATUS_COLORS[p.appointmentStatus] ?? STATUS_COLORS.Scheduled;
              const isSent = sentPatients.has(p.appointmentId);
              const isPatientSentForBadge = isSent;

              return (
                <button
                  key={p.appointmentId}
                  onClick={() => selectPatient(p)}
                  className="w-full px-3 py-2.5 text-right transition-all hover:bg-[#f8fafc]"
                  style={{
                    background: isSelected ? "#eff6ff" : "transparent",
                    borderRight: isSelected ? "3px solid #2563eb" : "3px solid transparent",
                    borderBottom: "1px solid #f3f4f6",
                    opacity: isSent ? 0.4 : 1,
                  }}
                >
                  <div className="flex items-center gap-2.5">
                    {/* Avatar */}
                    <div
                      className="w-9 h-9 rounded-full flex items-center justify-center text-[10px] font-bold flex-shrink-0"
                      style={{ background: isSent ? "#9ca3af" : isActive ? "#16a34a" : isWaiting ? ORANGE : "#2563eb", color: "#fff" }}
                    >
                      {getInitials(p.patientName)}
                    </div>
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-1.5">
                        <p className="text-[11px] font-bold truncate" style={{ color: isSelected ? "#2563eb" : "#1a1a1a" }}>{p.patientName}</p>
                        <span
                          className="text-[9px] font-semibold px-1.5 py-0.5 rounded flex-shrink-0"
                          style={{ background: sc.bg, color: sc.text }}
                        >
                          {APPT_STATUS_LABELS[p.appointmentStatus] ?? p.appointmentStatus}
                        </span>
                      </div>
                      <p className="text-[9px] text-[#9ca3af] truncate mt-0.5">
                        {p.serviceName ?? "—"} · {fmtTime(p.appointmentTime)}
                      </p>
                      {isAdmin && p.doctorName && (
                        <p className="text-[9px] text-[#6b7280] truncate">{p.doctorName} · {p.roomName ?? "—"}</p>
                      )}
                      {isActive && p.inRoomSince && (
                        <p className="text-[9px] font-semibold text-[#16a34a]">{fmtSessionDuration(p.inRoomSince)}</p>
                      )}
                      {p.hasActiveOrthoCase && (
                        <div className="mt-1 rounded border border-violet-100 bg-violet-50 px-1.5 py-1 text-[9px] text-violet-800">
                          <span className="font-bold">{p.orthoCaseNumber ?? "تقويم نشط"}</span>
                          {p.orthoCurrentStage && <span> · {p.orthoCurrentStage}</span>}
                          {p.orthoNextAppointmentDate && (
                            <span className="block text-violet-600">التالي: {p.orthoNextAppointmentDate}</span>
                          )}
                        </div>
                      )}
                    </div>
                  </div>
                  {isPatientSentForBadge && (
                    <span className="text-[9px] font-semibold text-[#16a34a] mt-1 flex items-center gap-0.5">
                      <CheckCircle className="w-2.5 h-2.5" /> تم الإرسال
                    </span>
                  )}
                </button>
              );
            })}
          </div>
        </aside>
      </div>
    </div>
  );
}
