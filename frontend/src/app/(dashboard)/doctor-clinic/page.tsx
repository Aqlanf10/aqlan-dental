"use client";

import { useState, useCallback, useMemo, useEffect, useRef } from "react";
import {
  Stethoscope, ClipboardCheck, ListChecks, GitBranch,
  Image, Pill, FlaskConical, CalendarClock, Send, RefreshCw,
  AlertCircle, CheckCircle,
  Loader2, FileText,
  Route, Save, X, ChevronLeft,
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
  StartVisitPanel,
  ExaminationPanel,
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
  | "procedures"
  | "treatmentPlan"
  | "orthoFollowUp"
  | "images"
  | "prescription"
  | "labOrder"
  | "followUp"
  | "handoff"
  | null;

/* ═══════════════════════════════════════════════════════════════════════════
   Action card definitions
   ═══════════════════════════════════════════════════════════════════════════ */
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
  const doctorId = user?.doctorId ?? user?.id ?? "";
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

  // ── Selected patient ──
  const [selectedPatient, setSelectedPatient] = useState<DoctorPatientItem | null>(null);
  const activePatientId = selectedPatient?.patientId ?? null;
  const { data: selectedSummary } = useDoctorPatientSummary(activePatientId);

  // ── Selected services for visit ──
  const [selectedServices, setSelectedServices] = useState<ServiceWithPrice[]>([]);

  // ── Search ──
  const [searchQuery, setSearchQuery] = useState("");
  const searchRef = useRef<HTMLInputElement>(null);

  // ── Sent patients tracking ──
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

  // ── Keyboard shortcuts ──
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        if (activePanel) setActivePanel(null);
        else setSelectedPatient(null);
        return;
      }
      if (e.ctrlKey && e.key === "r") { e.preventDefault(); refetch(); return; }
      if (e.ctrlKey && e.key === "f") { e.preventDefault(); searchRef.current?.focus(); return; }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [refetch, activePanel]);

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
    } catch { toast.error("فشل بدء الزيارة"); }
  }, [selectedPatient, startVisitMutation]);

  // ── Handoff to reception ──
  const handleHandoff = useCallback(async () => {
    if (!selectedPatient?.visitId) { toast.error("لا توجد زيارة نشطة"); return; }
    if (sentPatients.has(selectedPatient.appointmentId)) { toast.error("تم إرسال هذا المريض للاستقبال بالفعل"); return; }
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
      setActivePanel(null);
      setSentPatients(prev => new Set(prev).add(selectedPatient.appointmentId));
      setSelectedPatient(null);
      setSelectedServices([]);
      setClinicalNotes({ diagnosis: "", treatmentDone: "", instructions: "", nextVisitPlan: "", amountDue: 0, suggestedServiceId: "", followUpDate: "" });
    } catch { toast.error("فشل إرسال المريض للاستقبال"); }
  }, [selectedPatient, handoffMutation, clinicalNotes, sentPatients]);

  // ── Update clinical notes ──
  const updateClinicalNotes = useCallback((updates: Partial<typeof clinicalNotes>) => {
    setClinicalNotes(prev => ({ ...prev, ...updates }));
  }, []);

  // ── Save visit button ──
  const handleSaveVisit = useCallback(() => {
    if (!selectedPatient) { toast.error("اختر مريضاً أولاً"); return; }
    toast.success("تم حفظ بيانات الزيارة مؤقتاً");
  }, [selectedPatient]);

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
   Render — Microsoft Fluent-style Workspace
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

            {/* Save */}
            <button onClick={handleSaveVisit} className="flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-medium text-[#444] hover:bg-[#f0f0f0] transition-colors">
              <Save className="w-3.5 h-3.5" />
              <span className="hidden lg:inline">حفظ الزيارة</span>
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
          MAIN CONTENT AREA — Split: Patient bar + Workspace
          ═══════════════════════════════════════════════════════════════ */}
      <div className="flex-1 flex flex-col overflow-hidden">

        {/* ── PATIENT STRIP — horizontal scrollable cards ── */}
        <div className="flex-shrink-0 bg-white border-b border-[#e0e0e0] px-4 md:px-6 py-3">
          <div className="flex items-center gap-3 mb-2.5">
            <h2 className="text-xs font-bold text-[#1e3a8a]">
              {isAdmin ? "كل غرف العلاج" : "المرضى"}
            </h2>
            <span className="text-[10px] px-1.5 py-0.5 rounded-full font-bold bg-[#1e3a8a]/8 text-[#1e3a8a]">
              {filteredPatients.length}
            </span>

            {/* Admin filters */}
            {isAdmin && (
              <div className="flex items-center gap-2 mr-auto">
                {availableDoctors.length > 1 && (
                  <select value={filterDoctorId ?? ""} onChange={e => setFilterDoctorId(e.target.value || null)}
                    className="text-[11px] rounded-md border border-[#d1d5db] px-2 py-1 bg-white text-[#374151] outline-none focus:border-[#2563eb] transition-colors">
                    <option value="">كل الأطباء</option>
                    {availableDoctors.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
                  </select>
                )}
                {availableRooms.length > 1 && (
                  <select value={filterRoomId ?? ""} onChange={e => setFilterRoomId(e.target.value || null)}
                    className="text-[11px] rounded-md border border-[#d1d5db] px-2 py-1 bg-white text-[#374151] outline-none focus:border-[#2563eb] transition-colors">
                    <option value="">كل الغرف</option>
                    {availableRooms.map(r => <option key={r.id} value={r.id}>{r.name}</option>)}
                  </select>
                )}
              </div>
            )}

            {/* Search */}
            <input ref={searchRef} type="text" value={searchQuery} onChange={e => setSearchQuery(e.target.value)}
              placeholder="بحث..." className="text-[11px] rounded-md border border-[#d1d5db] px-2.5 py-1.5 outline-none w-32 focus:border-[#2563eb] transition-colors bg-white" />
          </div>

          {/* Patient cards row */}
          {isLoading && (
            <div className="flex items-center justify-center py-6">
              <Loader2 className="w-5 h-5 animate-spin text-[#2563eb]" />
            </div>
          )}

          {!isLoading && filteredPatients.length === 0 && (
            <div className="rounded-lg border border-dashed border-[#d1d5db] p-6 text-center">
              <p className="text-xs text-[#9ca3af] font-medium">لا يوجد مرضى اليوم</p>
            </div>
          )}

          {!isLoading && filteredPatients.length > 0 && (
            <div className="flex gap-2.5 overflow-x-auto pb-1" style={{ direction: "rtl" }}>
              {filteredPatients.map(p => {
                const isSelected = selectedPatient?.appointmentId === p.appointmentId;
                const isActive = ["InRoom", "InProgress"].includes(p.appointmentStatus);
                const isWaiting = p.queueStatus === "Waiting" || p.queueStatus === "Called";
                const sc = STATUS_COLORS[p.appointmentStatus] ?? STATUS_COLORS.Scheduled;
                const isSent = sentPatients.has(p.appointmentId);

                return (
                  <button key={p.appointmentId}
                    onClick={() => {
                      setSelectedPatient(p);
                      setSelectedServices([]);
                      setClinicalNotes({ diagnosis: "", treatmentDone: "", instructions: "", nextVisitPlan: "", amountDue: 0, suggestedServiceId: "", followUpDate: "" });
                    }}
                    className="flex-shrink-0 w-48 rounded-lg border p-3 text-right transition-all hover:shadow-md"
                    style={{
                      background: isSelected ? "#fff" : "#fafafa",
                      borderColor: isSelected ? "#2563eb" : "#e5e7eb",
                      borderWidth: isSelected ? "2px" : "1px",
                      boxShadow: isSelected ? "0 0 0 1px #2563eb, 0 2px 8px rgba(37,99,235,0.1)" : "none",
                      opacity: isSent ? 0.4 : 1,
                    }}
                  >
                    <div className="flex items-center gap-2 mb-2">
                      <div className="w-8 h-8 rounded-full flex items-center justify-center text-[10px] font-bold flex-shrink-0"
                        style={{ background: isSent ? "#9ca3af" : isActive ? "#16a34a" : isWaiting ? ORANGE : "#2563eb", color: "#fff" }}>
                        {getInitials(p.patientName)}
                      </div>
                      <div className="flex-1 min-w-0">
                        <p className="text-[11px] font-bold truncate" style={{ color: isSelected ? "#2563eb" : "#1a1a1a" }}>{p.patientName}</p>
                        <p className="text-[9px] truncate text-[#9ca3af]">
                          {p.patientNumber ? `#${p.patientNumber}` : ""}
                          {isAdmin && p.doctorName ? ` · ${p.doctorName}` : ""}
                        </p>
                      </div>
                    </div>
                    <div className="flex items-center justify-between">
                      <span className="text-[9px] text-[#6b7280]">{fmtTime(p.appointmentTime)}</span>
                      <span className="text-[9px] px-1.5 py-0.5 rounded font-semibold"
                        style={{ background: isSent ? "#f3f4f6" : sc.bg, color: isSent ? "#9ca3af" : sc.text }}>
                        {isSent ? "تم الإرسال" : APPT_STATUS_LABELS[p.appointmentStatus] ?? p.appointmentStatus}
                      </span>
                    </div>
                    {isAdmin && (
                      <p className="mt-1 text-[9px] text-[#6b7280] truncate">{p.roomName ?? "بدون غرفة"}</p>
                    )}
                    {isActive && p.inRoomSince && (
                      <p className="mt-0.5 text-[9px] font-semibold text-[#16a34a]">{fmtSessionDuration(p.inRoomSince)}</p>
                    )}
                  </button>
                );
              })}
            </div>
          )}
        </div>

        {/* ── WORKSPACE AREA — summary + action cards OR panel content ── */}
        <div className="flex-1 overflow-hidden">

          {/* When panel is open: show split view (patient summary left, panel right) */}
          {hasPanelOpen && selectedPatient ? (
            <div className="flex h-full">
              {/* Left: compact patient summary (40%) */}
              <div className="w-[40%] min-w-[320px] overflow-y-auto border-l border-[#e0e0e0] bg-white">
                <div className="p-4 space-y-4">
                  {/* Compact patient card */}
                  <div className="flex items-start gap-3">
                    <div className="w-11 h-11 rounded-lg flex items-center justify-center text-sm font-bold text-white flex-shrink-0" style={{ background: "linear-gradient(135deg, #1e3a8a, #2563eb)" }}>
                      {getInitials(selectedPatient.patientName)}
                    </div>
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-1.5 flex-wrap">
                        <h3 className="text-sm font-bold text-[#1a1a1a]">{selectedPatient.patientName}</h3>
                        {selectedPatient.patientNumber && <span className="text-[9px] font-semibold px-1.5 py-0.5 rounded bg-[#f3f4f6] text-[#6b7280]">#{selectedPatient.patientNumber}</span>}
                      </div>
                      <div className="flex items-center gap-2 mt-0.5 text-[10px] text-[#6b7280]">
                        <span className="px-1.5 py-0.5 rounded font-semibold"
                          style={{ background: STATUS_COLORS[selectedPatient.appointmentStatus]?.bg ?? "#f3f4f6", color: STATUS_COLORS[selectedPatient.appointmentStatus]?.text ?? "#6b7280" }}>
                          {APPT_STATUS_LABELS[selectedPatient.appointmentStatus] ?? selectedPatient.appointmentStatus}
                        </span>
                        <span>{selectedPatient.roomName ?? "بدون غرفة"}</span>
                        {isAdmin && selectedPatient.doctorName && <span>· {selectedPatient.doctorName}</span>}
                      </div>
                    </div>
                  </div>

                  {/* Medical alerts */}
                  {medicalAlerts.length > 0 && (
                    <div className="flex gap-1 flex-wrap">
                      {medicalAlerts.map((alert, i) => (
                        <span key={i} className="text-[9px] font-semibold px-2 py-0.5 rounded-full flex items-center gap-1"
                          style={{ background: alert.severity === "danger" ? "#fef2f2" : "#fffbeb", color: alert.severity === "danger" ? "#dc2626" : "#d97706" }}>
                          <AlertCircle className="w-2.5 h-2.5" />
                          {alert.type === "allergy" ? "حساسية" : alert.type === "bleeding" ? "نزيف" : alert.type === "pregnancy" ? "حمل" : alert.label ?? "تنبيه"}
                        </span>
                      ))}
                    </div>
                  )}

                  {/* Chief complaint */}
                  {selectedPatient.chiefComplaint && (
                    <div className="text-[11px] font-medium px-3 py-2 rounded-lg bg-[#fffbeb] text-[#92400e]">
                      الشكوى: {selectedPatient.chiefComplaint}
                    </div>
                  )}

                  {/* Info tiles */}
                  <div className="grid grid-cols-2 gap-2">
                    <div className="rounded-lg p-2.5 bg-[#f8fafc]">
                      <p className="text-[9px] text-[#9ca3af] mb-0.5">الخدمة</p>
                      <p className="text-[11px] font-bold text-[#1a1a1a]">{selectedPatient.serviceName ?? "—"}</p>
                    </div>
                    <div className="rounded-lg p-2.5 bg-[#f8fafc]">
                      <p className="text-[9px] text-[#9ca3af] mb-0.5">الغرفة</p>
                      <p className="text-[11px] font-bold text-[#1a1a1a]">{selectedPatient.roomName ?? "بدون غرفة"}</p>
                    </div>
                    <div className="rounded-lg p-2.5 bg-[#faf5ff]">
                      <p className="text-[9px] text-[#9ca3af] mb-0.5">الإجراءات</p>
                      <p className="text-[11px] font-bold text-[#7c3aed]">
                        {selectedServices.length} خدمة
                        {clinicalNotes.amountDue > 0 && <span className="text-[9px] mr-1">({fmtRial(clinicalNotes.amountDue)})</span>}
                      </p>
                    </div>
                    <div className="rounded-lg p-2.5 bg-[#f0fdf4]">
                      <p className="text-[9px] text-[#9ca3af] mb-0.5">المعالجون</p>
                      <p className="text-[11px] font-bold text-[#16a34a]">{sentPatients.size}</p>
                    </div>
                  </div>

                  {/* Action buttons — compact vertical list */}
                  <div className="space-y-1 pt-2">
                    <p className="text-[10px] font-bold text-[#9ca3af] mb-2">إجراءات سريعة</p>
                    {ACTION_CARDS.map(card => {
                      const isDisabled = !selectedPatient ||
                        (card.key !== "handoff" && !card.requiredStatus.includes(selectedPatient.appointmentStatus) && !card.requiredStatus.includes(selectedPatient.queueStatus ?? ""));
                      const isSentDisabled = card.key === "handoff" && isPatientSent;
                      const isCurrentPanel = activePanel === card.key;
                      const CardIcon = card.icon;

                      return (
                        <button key={card.key} onClick={() => handleAction(card.key)} disabled={isDisabled || isSentDisabled}
                          className="w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-right transition-all"
                          style={{
                            background: isCurrentPanel ? card.bgColor : "transparent",
                            opacity: isDisabled || isSentDisabled ? 0.35 : 1,
                            cursor: isDisabled || isSentDisabled ? "not-allowed" : "pointer",
                          }}>
                          <div className="w-7 h-7 rounded-md flex items-center justify-center flex-shrink-0"
                            style={{ background: (isDisabled || isSentDisabled) ? "#e5e7eb" : card.color + "15" }}>
                            <CardIcon className="w-3.5 h-3.5" style={{ color: (isDisabled || isSentDisabled) ? "#9ca3af" : card.color }} />
                          </div>
                          <div className="flex-1 min-w-0">
                            <p className="text-[11px] font-semibold" style={{ color: isCurrentPanel ? card.color : (isDisabled || isSentDisabled) ? "#9ca3af" : "#374151" }}>{card.label}</p>
                          </div>
                          <ChevronLeft className="w-3.5 h-3.5 text-[#d1d5db]" />
                        </button>
                      );
                    })}
                  </div>
                </div>
              </div>

              {/* Right: Panel content (60%) */}
              <div className="flex-1 bg-[#fafafa] overflow-y-auto">
                <div className="max-w-2xl mx-auto p-5">
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
                              <h2 className="text-sm font-bold text-[#1a1a1a]">{PANEL_TITLES[activePanel ?? ""]}</h2>
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

                  {/* Panel body — renders the correct panel component */}
                  <div className="bg-white rounded-xl border border-[#e5e7eb] shadow-sm">
                    {activePanel === "startVisit" && (
                      <StartVisitPanel patient={selectedPatient} isPending={startVisitMutation.isPending} onConfirm={handleStartVisit} onClose={() => setActivePanel(null)} />
                    )}
                    {activePanel === "examination" && (
                      <ExaminationPanel patient={selectedPatient} diagnosis={clinicalNotes.diagnosis} medicalAlerts={medicalAlerts} onClose={() => setActivePanel(null)}
                        onSave={(data) => {
                          const parts = [data.chiefComplaint ? `الشكوى: ${data.chiefComplaint}` : "", data.extraoral ? `فحص خارج الفم: ${data.extraoral}` : "", data.intraoral ? `فحص داخل الفم: ${data.intraoral}` : "", data.diagnosis ? `التشخيص: ${data.diagnosis}` : "", data.clinicalNotes ? `ملاحظات: ${data.clinicalNotes}` : ""].filter(Boolean).join(" | ");
                          updateClinicalNotes({ diagnosis: parts || data.diagnosis, treatmentDone: data.treatmentDone || clinicalNotes.treatmentDone });
                          setActivePanel(null);
                          toast.success("تم حفظ الفحص والتشخيص");
                        }}
                      />
                    )}
                    {activePanel === "procedures" && (
                      <PricedProceduresPanel patient={selectedPatient} services={services} currentAmountDue={clinicalNotes.amountDue} currentTreatmentDone={clinicalNotes.treatmentDone} onClose={() => setActivePanel(null)}
                        onSave={(data) => {
                          updateClinicalNotes({ treatmentDone: data.treatmentDone, amountDue: data.totalAmount, suggestedServiceId: data.suggestedServiceId });
                          setActivePanel(null);
                          toast.success("تم حفظ الإجراءات المسعّرة");
                        }}
                      />
                    )}
                    {activePanel === "treatmentPlan" && (
                      <TreatmentPlanPanel patient={selectedPatient} onClose={() => setActivePanel(null)}
                        onSave={(data) => { updateClinicalNotes({ nextVisitPlan: data.plan }); setActivePanel(null); toast.success("تم حفظ خطة العلاج"); }}
                      />
                    )}
                    {activePanel === "orthoFollowUp" && (
                      <OrthoFollowUpPanel patient={selectedPatient} onClose={() => setActivePanel(null)}
                        onSave={(data) => {
                          const parts = [data.notes, data.nextVisitPlan ? `خطة قادمة: ${data.nextVisitPlan}` : ""].filter(Boolean).join(" | ");
                          const existing = clinicalNotes.treatmentDone;
                          updateClinicalNotes({ treatmentDone: existing ? `${existing} | ${parts}` : parts, nextVisitPlan: data.nextVisitPlan || clinicalNotes.nextVisitPlan });
                          setActivePanel(null);
                          toast.success("تم حفظ متابعة التقويم");
                        }}
                      />
                    )}
                    {activePanel === "images" && (
                      <ImagesRadiographsPanel patient={selectedPatient} onClose={() => setActivePanel(null)} onSave={() => { setActivePanel(null); toast.success("تم حفظ الصور"); }} />
                    )}
                    {activePanel === "prescription" && (
                      <PrescriptionPanel patient={selectedPatient} onClose={() => setActivePanel(null)} onSave={() => { setActivePanel(null); toast.success("تم حفظ الوصفة"); }} />
                    )}
                    {activePanel === "labOrder" && (
                      <LabOrderPanel patient={selectedPatient} onClose={() => setActivePanel(null)} onSave={() => { setActivePanel(null); toast.success("تم حفظ طلب المعمل"); }} />
                    )}
                    {activePanel === "followUp" && (
                      <FollowUpSuggestPanel patient={selectedPatient} onClose={() => setActivePanel(null)} onSave={() => { setActivePanel(null); toast.success("تم حفظ موعد المتابعة"); }} />
                    )}
                    {activePanel === "handoff" && (
                      <HandoffPanel patient={selectedPatient} clinicalNotes={clinicalNotes} selectedServicesTotal={clinicalNotes.amountDue} onClose={() => setActivePanel(null)} onConfirm={handleHandoff} isPending={handoffMutation.isPending} />
                    )}
                  </div>
                </div>
              </div>
            </div>
          ) : (
            /* ── DEFAULT VIEW: No panel open — show summary + action grid ── */
            <div className="flex-1 overflow-y-auto">
              <div className="max-w-5xl mx-auto px-4 md:px-6 py-5 space-y-5">

                {/* Selected patient summary */}
                {selectedPatient ? (
                  <div className="rounded-xl border border-[#e5e7eb] bg-white p-5">
                    <div className="flex items-start gap-4">
                      <div className="w-12 h-12 rounded-xl flex items-center justify-center text-base font-bold text-white flex-shrink-0" style={{ background: "linear-gradient(135deg, #1e3a8a, #2563eb)" }}>
                        {getInitials(selectedPatient.patientName)}
                      </div>
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2 flex-wrap">
                          <h2 className="text-base font-bold text-[#1a1a1a]">{selectedPatient.patientName}</h2>
                          {selectedPatient.patientNumber && <span className="text-[9px] font-semibold px-2 py-0.5 rounded-full bg-[#f3f4f6] text-[#6b7280]">#{selectedPatient.patientNumber}</span>}
                          <span className="text-[9px] font-semibold px-2 py-0.5 rounded-full" style={{ background: STATUS_COLORS[selectedPatient.appointmentStatus]?.bg ?? "#f3f4f6", color: STATUS_COLORS[selectedPatient.appointmentStatus]?.text ?? "#6b7280" }}>
                            {APPT_STATUS_LABELS[selectedPatient.appointmentStatus] ?? selectedPatient.appointmentStatus}
                          </span>
                          {isPatientSent && <span className="text-[9px] font-semibold px-2 py-0.5 rounded-full flex items-center gap-0.5 bg-[#dcfce7] text-[#16a34a]"><CheckCircle className="w-2.5 h-2.5" /> تم الإرسال</span>}
                        </div>
                        <div className="flex items-center gap-3 mt-1 text-[11px] text-[#6b7280] flex-wrap">
                          <span>{selectedPatient.serviceName ?? "—"}</span>
                          <span className="text-[#d1d5db]">·</span>
                          <span>{selectedPatient.roomName ?? "بدون غرفة"}</span>
                          {isAdmin && selectedPatient.doctorName && <><span className="text-[#d1d5db]">·</span><span>{selectedPatient.doctorName}</span></>}
                          {selectedPatient.inRoomSince && <><span className="text-[#d1d5db]">·</span><span className="font-semibold text-[#16a34a]">{fmtSessionDuration(selectedPatient.inRoomSince)}</span></>}
                        </div>
                        {/* Medical alerts */}
                        {medicalAlerts.length > 0 && (
                          <div className="flex gap-1 mt-2 flex-wrap">
                            {medicalAlerts.map((alert, i) => (
                              <span key={i} className="text-[9px] font-semibold px-2 py-0.5 rounded-full flex items-center gap-1"
                                style={{ background: alert.severity === "danger" ? "#fef2f2" : "#fffbeb", color: alert.severity === "danger" ? "#dc2626" : "#d97706" }}>
                                <AlertCircle className="w-2.5 h-2.5" />
                                {alert.type === "allergy" ? "حساسية" : alert.type === "bleeding" ? "نزيف" : alert.type === "pregnancy" ? "حمل" : alert.label ?? "تنبيه"}
                              </span>
                            ))}
                          </div>
                        )}
                        {selectedPatient.chiefComplaint && (
                          <div className="mt-2 text-[11px] font-medium px-3 py-1.5 rounded-lg inline-block bg-[#fffbeb] text-[#92400e]">الشكوى: {selectedPatient.chiefComplaint}</div>
                        )}
                      </div>
                    </div>

                    {/* Quick info tiles */}
                    <div className="grid grid-cols-2 md:grid-cols-4 gap-2.5 mt-4">
                      <div className="rounded-lg p-3 bg-[#f8fafc]">
                        <p className="text-[9px] text-[#9ca3af] mb-0.5">الغرفة</p>
                        <p className="text-xs font-bold text-[#1a1a1a]">{selectedPatient.roomName ?? "بدون غرفة"}</p>
                      </div>
                      <div className="rounded-lg p-3 bg-[#f8fafc]">
                        <p className="text-[9px] text-[#9ca3af] mb-0.5">وقت الموعد</p>
                        <p className="text-xs font-bold text-[#1a1a1a]">{fmtTime(selectedPatient.appointmentTime)}</p>
                      </div>
                      <div className="rounded-lg p-3 bg-[#faf5ff]">
                        <p className="text-[9px] text-[#9ca3af] mb-0.5">الإجراءات</p>
                        <p className="text-xs font-bold text-[#7c3aed]">
                          {selectedServices.length} خدمة
                          {clinicalNotes.amountDue > 0 && <span className="text-[9px] mr-1">({fmtRial(clinicalNotes.amountDue)})</span>}
                        </p>
                      </div>
                      <div className="rounded-lg p-3 bg-[#f0fdf4]">
                        <p className="text-[9px] text-[#9ca3af] mb-0.5">المعالجون</p>
                        <p className="text-xs font-bold text-[#16a34a]">{sentPatients.size}</p>
                      </div>
                    </div>
                  </div>
                ) : (
                  /* No patient selected */
                  <div className="flex flex-col items-center justify-center py-20">
                    <div className="w-16 h-16 rounded-2xl flex items-center justify-center mb-3 bg-[#1e3a8a]/5">
                      <Stethoscope className="w-8 h-8 text-[#1e3a8a]/20" />
                    </div>
                    <p className="text-sm font-bold text-[#1a1a1a]">اختر مريضاً للبدء</p>
                    <p className="text-xs mt-1 text-[#9ca3af]">اختر مريضاً من القائمة أعلاه لعرض التفاصيل والإجراءات</p>
                  </div>
                )}

                {/* Action cards grid */}
                {selectedPatient && (
                  <div>
                    <h3 className="text-xs font-bold text-[#9ca3af] mb-3">إجراءات العلاج</h3>
                    <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-2.5">
                      {ACTION_CARDS.map(card => {
                        const isDisabled = !selectedPatient ||
                          (card.key !== "handoff" && !card.requiredStatus.includes(selectedPatient.appointmentStatus) && !card.requiredStatus.includes(selectedPatient.queueStatus ?? ""));
                        const isSentDisabled = card.key === "handoff" && isPatientSent;
                        const CardIcon = card.icon;

                        return (
                          <button key={card.key} onClick={() => handleAction(card.key)} disabled={isDisabled || isSentDisabled}
                            className="flex flex-col items-center gap-2 p-3.5 rounded-xl border transition-all text-center group"
                            style={{
                              background: isDisabled || isSentDisabled ? "#fafafa" : "#fff",
                              borderColor: isDisabled || isSentDisabled ? "#f3f4f6" : card.color + "25",
                              opacity: isDisabled || isSentDisabled ? 0.35 : 1,
                              cursor: isDisabled || isSentDisabled ? "not-allowed" : "pointer",
                              boxShadow: !isDisabled && !isSentDisabled ? "0 1px 3px rgba(0,0,0,0.04)" : "none",
                            }}>
                            <div className="w-10 h-10 rounded-lg flex items-center justify-center transition-transform group-hover:scale-105"
                              style={{ background: isDisabled || isSentDisabled ? "#e5e7eb" : card.color + "12" }}>
                              <CardIcon className="w-4.5 h-4.5" style={{ color: isDisabled || isSentDisabled ? "#9ca3af" : card.color }} />
                            </div>
                            <span className="text-[10px] font-bold leading-tight" style={{ color: isDisabled || isSentDisabled ? "#9ca3af" : "#374151" }}>{card.label}</span>
                          </button>
                        );
                      })}
                    </div>
                  </div>
                )}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
