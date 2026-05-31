"use client";

import React, { useEffect, useRef, useState, useCallback } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import {
  User, Stethoscope, Phone, MapPin, Pencil,
  Activity, Wallet, Pill, Scissors, Image as ImageIcon,
  MessageCircle, Archive, RotateCcw, ClipboardList,
  FileSignature, ScanLine, FolderOpen, FlaskConical,
  KeyRound, Copy, Check, Mail,
  AlertTriangle, Shield, Receipt, ChevronRight,
  MoreHorizontal, Bell,
  ChevronDown, ChevronUp, Eye, Coins,
  Stethoscope as TreatmentIcon, ArrowLeftRight, Printer, CalendarPlus,
} from "lucide-react";
import type { LucideIcon } from "lucide-react";
import type { PatientProfile } from "@/types/patient";
import api from "@/lib/api";
import { cn, GENDER_LABELS, formatArabicDate, formatPhoneForWhatsApp } from "@/lib/utils";
import { toast } from "@/stores/toastStore";
import { useAuthStore } from "@/stores/authStore";

// Tab components
import { OverviewTab }         from "@/components/patient/tabs/OverviewTab";
import { BasicInfoTab }        from "@/components/patient/tabs/BasicInfoTab";
import { MedicalHistoryTab }   from "@/components/patient/tabs/MedicalHistoryTab";
import { DentalHistoryTab }    from "@/components/patient/tabs/DentalHistoryTab";
import { AppointmentsTab }     from "@/components/patient/tabs/AppointmentsTab";
import { VisitsTab }           from "@/components/patient/tabs/VisitsTab";
import { FinanceTab }          from "@/components/patient/tabs/FinanceTab";
import { ContractsTab }        from "@/components/patient/tabs/ContractsTab";
import { PaymentsTab }         from "@/components/patient/tabs/PaymentsTab";
import { MessagesTab }         from "@/components/patient/tabs/MessagesTab";
import { OrthodonticsTab }     from "@/components/patient/tabs/OrthodonticsTab";
import { GeneralDentistryTab } from "@/components/patient/tabs/GeneralDentistryTab";
import { SurgeryTab }          from "@/components/patient/tabs/SurgeryTab";
import { PhotosTab }           from "@/components/patient/tabs/PhotosTab";
import { RadiographsTab }      from "@/components/patient/tabs/RadiographsTab";
import { PrescriptionsTab }    from "@/components/patient/tabs/PrescriptionsTab";
import { ReferralsTab }        from "@/components/patient/tabs/ReferralsTab";
import { DocumentsTab }        from "@/components/patient/tabs/DocumentsTab";
import { LabOrdersTab }        from "@/components/patient/tabs/LabOrdersTab";
import { TimelineTab }         from "@/components/patient/tabs/TimelineTab";
import { PortalAccessTab }     from "@/components/patient/tabs/PortalAccessTab";
import { TreatmentPlanTab }    from "@/components/patient/tabs/TreatmentPlanTab";

// ─── Types ──────────────────────────────────────────────────────────────────────

interface PatientSummary {
  totalAppointments: number;
  completedAppointments: number;
  activeOrthoCases: number;
  totalPaid: number | null;
  totalOutstanding: number | null;
  prescriptionsCount: number;
}

interface OrthoCase   { id: string; caseNumber: string; applianceType?: string; status: string; stagePercentage: number; doctorName?: string; }
interface SurgeryCase { id: string; caseNumber: string; surgeryType: string;   status: string; doctorName?: string; }

type Tab =
  | "overview" | "info" | "medical" | "dental"
  | "appointments" | "visits" | "orthodontics" | "general" | "surgery" | "treatment-plan"
  | "photos" | "radiographs" | "prescriptions" | "referrals" | "documents" | "lab-orders"
  | "finance" | "contracts" | "payments"
  | "messages" | "timeline"
  | "portal-access";

// ─── Pivot Tab Definitions (my original design) ────────────────────────────────

interface PivotTabDef {
  key: Tab;
  label: string;
  group?: string;
}

const ALL_PIVOT_TABS: PivotTabDef[] = [
  { key: "overview",  label: "نظرة عامة" },
  { key: "info",      label: "المعلومات الأساسية", group: "عام" },
  { key: "medical",   label: "التاريخ الطبي",      group: "عام" },
  { key: "dental",    label: "التاريخ السني",      group: "عام" },
  { key: "appointments",   label: "المواعيد",   group: "سريري" },
  { key: "visits",         label: "الزيارات",   group: "سريري" },
  { key: "treatment-plan", label: "خطة العلاج", group: "سريري" },
  { key: "orthodontics",   label: "التقويم",    group: "سريري" },
  { key: "general",        label: "طب الأسنان العام", group: "سريري" },
  { key: "surgery",        label: "الجراحة",    group: "سريري" },
  { key: "photos",        label: "الصور",     group: "سجلات" },
  { key: "radiographs",   label: "الأشعة",    group: "سجلات" },
  { key: "prescriptions", label: "الوصفات",   group: "سجلات" },
  { key: "referrals",     label: "الإحالات",  group: "سجلات" },
  { key: "lab-orders",    label: "طلبات المختبر", group: "سجلات" },
  { key: "documents",     label: "المستندات", group: "سجلات" },
  { key: "finance",   label: "المالية",   group: "مالي" },
  { key: "contracts", label: "العقود",    group: "مالي" },
  { key: "payments",  label: "المدفوعات", group: "مالي" },
  { key: "messages", label: "الرسائل",     group: "تواصل" },
  { key: "timeline", label: "السجل الزمني", group: "تواصل" },
  { key: "portal-access", label: "بوابة المريض", group: "بوابة" },
];

const DOCTOR_HIDDEN_TABS: Tab[] = ["finance","contracts","payments","messages","portal-access"];

// ─── Ribbon Group Definitions (my original design) ──────────────────────────────

interface RibbonItem {
  icon: LucideIcon;
  label: string;
  color: string;
  bgColor: string;
  action: () => void;
}

interface RibbonGroup {
  label: string;
  items: RibbonItem[];
}

// ─── Page Component ──────────────────────────────────────────────────────────────

export default function PatientProfilePage() {
  const { id }  = useParams<{ id: string }>();
  const { user } = useAuthStore();

  const [patient,      setPatient]      = useState<PatientProfile | null>(null);
  const [summary,      setSummary]      = useState<PatientSummary | null>(null);
  const [orthoCases,   setOrthoCases]   = useState<OrthoCase[]>([]);
  const [surgeryCases, setSurgeryCases] = useState<SurgeryCase[]>([]);
  const [loading,      setLoading]      = useState(true);
  const [error,        setError]        = useState<string | null>(null);
  const [activeTab,    setActiveTab]    = useState<Tab>("overview");
  const [phoneCopied,  setPhoneCopied]  = useState(false);
  const [openAddVisitModal, setOpenAddVisitModal] = useState(false);
  const [confirmAction, setConfirmAction] = useState<{ type: "archive" | "restore"; id: string; name: string } | null>(null);
  const [moreMenuOpen,  setMoreMenuOpen]  = useState(false);
  const [isRibbonCollapsed, setIsRibbonCollapsed] = useState(false);
  const [isBannerHovered, setIsBannerHovered] = useState(false);
  const [ribbonActiveAction, setRibbonActiveAction] = useState<string | null>(null);
  const [hoveredTab, setHoveredTab] = useState<string | null>(null);

  const moreMenuRef = useRef<HTMLDivElement>(null);

  // ─── Tab switch ───────────────────────────────────────────────────────────
  const switchTab = useCallback((key: Tab) => {
    setActiveTab(key);
  }, []);

  // ─── Ribbon action groups (built after patient data is available) ─────────
  const patientName = patient
    ? `${patient.firstName} ${patient.middleName ?? ""} ${patient.lastName}`.replace(/\s+/g, " ").trim()
    : "";

  const limited = patient?.isLimitedView === true;

  const ribbonGroups: RibbonGroup[] = [
    {
      label: "إجراءات العيادة",
      items: [
        { icon: TreatmentIcon, label: "جلسة علاج", color: "text-emerald-600", bgColor: "hover:bg-emerald-50",
          action: () => { switchTab("visits"); setOpenAddVisitModal(true); } },
        { icon: ClipboardList, label: "خطة علاج", color: "text-sky-600", bgColor: "hover:bg-sky-50",
          action: () => switchTab("treatment-plan") },
        { icon: CalendarPlus, label: "موعد جديد", color: "text-violet-600", bgColor: "hover:bg-violet-50",
          action: () => { /* handled via Link */ } },
        { icon: Pill, label: "وصفة طبية", color: "text-pink-600", bgColor: "hover:bg-pink-50",
          action: () => { /* handled via Link */ } },
      ],
    },
    {
      label: "المالية V3",
      items: [
        { icon: Receipt, label: "قبض دفعة", color: "text-emerald-600", bgColor: "hover:bg-emerald-50",
          action: () => switchTab("payments") },
        { icon: ArrowLeftRight, label: "مرتجع", color: "text-red-500", bgColor: "hover:bg-red-50",
          action: () => switchTab("finance") },
        { icon: FileSignature, label: "عقد جديد", color: "text-amber-600", bgColor: "hover:bg-amber-50",
          action: () => { /* handled via Link */ } },
        { icon: Wallet, label: "كشف حساب", color: "text-sky-600", bgColor: "hover:bg-sky-50",
          action: () => switchTab("finance") },
      ],
    },
    {
      label: "الحالات السريرية",
      items: [
        { icon: Activity, label: "حالة تقويم", color: "text-violet-600", bgColor: "hover:bg-violet-50",
          action: () => { /* handled via Link */ } },
        { icon: Scissors, label: "حالة جراحة", color: "text-rose-600", bgColor: "hover:bg-rose-50",
          action: () => { /* handled via Link */ } },
        { icon: Eye, label: "طب أسنان عام", color: "text-sky-600", bgColor: "hover:bg-sky-50",
          action: () => switchTab("general") },
      ],
    },
    {
      label: "المرفقات والسجلات",
      items: [
        { icon: ImageIcon, label: "صور", color: "text-teal-600", bgColor: "hover:bg-teal-50",
          action: () => switchTab("photos") },
        { icon: ScanLine, label: "أشعة", color: "text-indigo-600", bgColor: "hover:bg-indigo-50",
          action: () => switchTab("radiographs") },
        { icon: FolderOpen, label: "مستندات", color: "text-amber-600", bgColor: "hover:bg-amber-50",
          action: () => switchTab("documents") },
        { icon: FlaskConical, label: "مختبر", color: "text-cyan-600", bgColor: "hover:bg-cyan-50",
          action: () => switchTab("lab-orders") },
      ],
    },
    {
      label: "طباعة وتواصل",
      items: [
        { icon: Printer, label: "طباعة", color: "text-slate-600", bgColor: "hover:bg-slate-100",
          action: () => { /* handled via Link */ } },
        { icon: MessageCircle, label: "رسالة", color: "text-green-600", bgColor: "hover:bg-green-50",
          action: () => switchTab("messages") },
        { icon: KeyRound, label: "بوابة المريض", color: "text-orange-600", bgColor: "hover:bg-orange-50",
          action: () => switchTab("portal-access") },
      ],
    },
  ];

  // Filter ribbon groups for limited view
  const visibleRibbonGroups = limited
    ? ribbonGroups.filter(g => g.label !== "المالية V3")
    : ribbonGroups;

  // ─── Handlers ───────────────────────────────────────────────────────────

  const handleArchive = (pid: string, name: string) => setConfirmAction({ type: "archive",  id: pid, name });
  const handleRestore = (pid: string, name: string) => setConfirmAction({ type: "restore", id: pid, name });

  const executeConfirm = async () => {
    if (!confirmAction) return;
    try {
      if (confirmAction.type === "archive") {
        await api.put(`/api/patients/${confirmAction.id}/archive`);
        toast.success(`تم أرشفة المريض ${confirmAction.name}`);
      } else {
        await api.put(`/api/patients/${confirmAction.id}/restore`);
        toast.success(`تم استعادة المريض ${confirmAction.name}`);
      }
      const { data } = await api.get<PatientProfile>(`/api/patients/${id}`);
      setPatient(data);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? "حدث خطأ");
    } finally {
      setConfirmAction(null);
    }
  };

  const handleRibbonAction = (label: string, action: () => void) => {
    setRibbonActiveAction(label);
    action();
    setTimeout(() => setRibbonActiveAction(null), 600);
  };

  // ─── Data Fetching ──────────────────────────────────────────────────────

  useEffect(() => {
    setError(null);
    api.get<PatientProfile>(`/api/patients/${id}`)
      .then(r  => setPatient(r.data))
      .catch(err => {
        const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
        setError(msg ?? "فشل تحميل بيانات المريض");
      })
      .finally(() => setLoading(false));

    api.get<PatientSummary>(`/api/patients/${id}/summary`)
      .then(r => setSummary(r.data)).catch(() => {});
    api.get<OrthoCase[]>(`/api/ortho-cases?patientId=${id}&pageSize=10`)
      .then(r => setOrthoCases(r.data)).catch(() => {});
    api.get<{ data: SurgeryCase[] }>(`/api/surgery-cases?patientId=${id}&pageSize=10`)
      .then(r => setSurgeryCases(r.data.data ?? [])).catch(() => {});
  }, [id]);

  useEffect(() => {
    const close = (e: MouseEvent) => {
      if (moreMenuRef.current && !moreMenuRef.current.contains(e.target as Node))
        setMoreMenuOpen(false);
    };
    document.addEventListener("mousedown", close);
    return () => document.removeEventListener("mousedown", close);
  }, []);

  // ─── Tab Content ────────────────────────────────────────────────────────

  const renderTabContent = () => {
    if (!patient) return null;
    const pn = `${patient.firstName} ${patient.lastName}`;
    switch (activeTab) {
      case "overview":       return <OverviewTab patientId={id} summary={summary} patient={patient} onAddVisit={() => { switchTab("visits"); setOpenAddVisitModal(true); }} />;
      case "info":           return <BasicInfoTab patient={patient} orthoCases={orthoCases} surgeryCases={surgeryCases} />;
      case "medical":        return <MedicalHistoryTab patientId={id} />;
      case "dental":         return <DentalHistoryTab patientId={id} />;
      case "appointments":   return <AppointmentsTab patientId={id} patientName={pn} />;
      case "visits":         return <VisitsTab patientId={id} openAddModal={openAddVisitModal} onModalOpened={() => setOpenAddVisitModal(false)} />;
      case "treatment-plan": return <TreatmentPlanTab patientId={id} />;
      case "orthodontics":   return <OrthodonticsTab patientId={id} />;
      case "general":        return <GeneralDentistryTab patientId={id} />;
      case "surgery":        return <SurgeryTab patientId={id} />;
      case "photos":         return <PhotosTab patientId={id} />;
      case "radiographs":    return <RadiographsTab patientId={id} />;
      case "prescriptions":  return <PrescriptionsTab patientId={id} />;
      case "referrals":      return <ReferralsTab patientId={id} />;
      case "lab-orders":     return <LabOrdersTab patientId={id} />;
      case "documents":      return <DocumentsTab patientId={id} />;
      case "finance":        return <FinanceTab patientId={id} />;
      case "contracts":      return <ContractsTab patientId={id} />;
      case "payments":       return <PaymentsTab patientId={id} />;
      case "messages":       return <MessagesTab patientId={id} />;
      case "timeline":       return <TimelineTab patientId={id} />;
      case "portal-access":  return <PortalAccessTab patientId={id} patientNumber={patient.patientNumber ?? ''} />;
      default:               return null;
    }
  };

  // ─── Pivot tab grouping ─────────────────────────────────────────────────

  const visibleTabs = limited
    ? ALL_PIVOT_TABS.filter(t => !DOCTOR_HIDDEN_TABS.includes(t.key))
    : ALL_PIVOT_TABS;

  const pivotGroups: { label: string | null; tabs: PivotTabDef[] }[] = [];
  let currentGroup: { label: string | null; tabs: PivotTabDef[] } = { label: null, tabs: [] };
  visibleTabs.forEach(tab => {
    if (tab.group && tab.group !== currentGroup.label) {
      if (currentGroup.tabs.length > 0) pivotGroups.push(currentGroup);
      currentGroup = { label: tab.group, tabs: [tab] };
    } else {
      currentGroup.tabs.push(tab);
    }
  });
  if (currentGroup.tabs.length > 0) pivotGroups.push(currentGroup);

  // ─── Loading / Error States ─────────────────────────────────────────────

  if (loading) return (
    <div className="h-full flex flex-col animate-pulse bg-slate-50" dir="rtl">
      <div className="h-1 bg-gradient-to-l from-sky-500 via-blue-600 to-indigo-600" />
      <div className="h-[100px] bg-white border-b border-slate-200 flex items-center gap-4 px-6">
        <div className="w-14 h-14 rounded-2xl bg-slate-200 flex-shrink-0" />
        <div className="flex-1 space-y-2">
          <div className="h-5 bg-slate-200 rounded w-48" />
          <div className="h-3 bg-slate-100 rounded w-64" />
        </div>
      </div>
      <div className="h-10 bg-white border-b border-slate-200" />
      <div className="h-11 bg-white border-b border-slate-200" />
      <div className="flex-1 p-6 space-y-4">
        <div className="h-32 bg-white rounded-xl border border-slate-100" />
        <div className="h-24 bg-white rounded-xl border border-slate-100" />
      </div>
    </div>
  );

  if (error || !patient) return (
    <div className="h-full flex items-center justify-center" dir="rtl">
      <div className="text-center space-y-3">
        <div className="w-16 h-16 bg-red-50 rounded-full flex items-center justify-center mx-auto">
          <AlertTriangle className="w-8 h-8 text-red-400" />
        </div>
        <p className="text-slate-600 font-medium">{error ?? "المريض غير موجود"}</p>
        <Link href="/patients" className="text-sm text-blue-600 hover:underline">← العودة للمرضى</Link>
      </div>
    </div>
  );

  const initials    = (patient.firstName[0] ?? "") + (patient.lastName[0] ?? "");
  const outstanding = summary?.totalOutstanding ?? 0;
  const hasDebt     = outstanding > 0;

  // ─── Render ──────────────────────────────────────────────────────────────

  return (
    <div className="h-full flex flex-col bg-slate-50 overflow-hidden" dir="rtl">

      {/* ══════════════════════════════════════════════════════
          LAYER 1 — Patient Banner (my original design)
          ══════════════════════════════════════════════════════ */}
      <div
        className="flex-shrink-0 bg-white border-b border-slate-200 transition-all duration-300"
        onMouseEnter={() => setIsBannerHovered(true)}
        onMouseLeave={() => setIsBannerHovered(false)}
      >
        {/* Top accent line - Microsoft Fluent style */}
        <div className="h-1 bg-gradient-to-l from-sky-500 via-blue-600 to-indigo-600" />

        <div className="px-6 py-4 flex flex-col md:flex-row justify-between items-start gap-4">
          {/* Right side - Alerts & Balance */}
          <div className="flex items-center gap-3 order-2 md:order-1">
            {/* Financial Balance Card */}
            {!limited && (
              <div
                className="group relative bg-gradient-to-br from-orange-50 to-amber-50 border border-orange-200 rounded-xl p-3 text-center min-w-[140px] shadow-sm hover:shadow-md transition-all duration-300 cursor-pointer hover:scale-[1.02]"
                onClick={() => switchTab("finance")}
              >
                <span className="block text-[10px] text-orange-600 font-bold uppercase mb-1 tracking-wider">
                  <Coins className="inline w-3 h-3 ml-1" /> الرصيد المالي
                </span>
                <span className="text-lg font-black text-orange-700" dir="ltr">
                  {outstanding.toLocaleString()} ر.ي
                </span>
                <div className="absolute -bottom-1 left-1/2 -translate-x-1/2 w-3 h-3 bg-orange-200 rotate-45 border-b border-r border-orange-300" />
              </div>
            )}

            {/* Medical Alerts Card */}
            {patient.medicalHistory?.chronicDiseases && (
              <div className="bg-gradient-to-br from-red-50 to-rose-50 border border-red-100 p-2.5 rounded-xl min-w-[160px] hover:shadow-md transition-all duration-300 cursor-pointer hover:scale-[1.02]"
                onClick={() => switchTab("medical")}>
                <span className="block text-[10px] text-red-500 font-bold mb-1.5 tracking-wider">
                  <AlertTriangle className="inline w-3 h-3 ml-1" /> تنبيهات طبية
                </span>
                <div className="flex flex-wrap gap-1">
                  <span className="bg-red-500 text-white text-[11px] font-bold px-2 py-0.5 rounded-md">
                    <Shield className="inline w-2.5 h-2.5 ml-0.5" />تنبيه طبي
                  </span>
                </div>
              </div>
            )}
          </div>

          {/* Left side - Patient Info */}
          <div className="flex items-center gap-5 mr-auto order-1 md:order-2">
            <div className="text-right">
              {/* Breadcrumb */}
              <div className="flex items-center gap-1.5 text-[11px] text-slate-400 mb-1.5">
                <Link href="/patients" className="hover:text-blue-600 transition-colors">المرضى</Link>
                <ChevronRight className="w-3 h-3 rotate-180" />
                <span className="text-slate-700 font-medium">{patientName}</span>
                <span className="mx-1 text-slate-200">|</span>
                <span className="font-mono text-slate-400">{patient.patientNumber}</span>
              </div>

              {/* Name + Status */}
              <div className="flex items-center justify-end gap-2 mb-1">
                {patient.isActive && (
                  <span className="bg-emerald-500 text-white text-[10px] font-bold px-2 py-0.5 rounded-full shadow-sm">
                    نشط
                  </span>
                )}
                {!patient.isActive && (
                  <span className="bg-slate-400 text-white text-[10px] font-bold px-2 py-0.5 rounded-full shadow-sm">
                    مؤرشف
                  </span>
                )}
                {hasDebt && !limited && (
                  <span className="flex items-center gap-1 text-[10px] font-bold px-2 py-0.5 rounded-full bg-amber-50 text-amber-700 border border-amber-100">
                    <Receipt className="w-2.5 h-2.5" />{outstanding.toLocaleString()} ر.ي متبقي
                  </span>
                )}
                <h1 className="text-2xl font-black text-slate-800 tracking-tight">{patientName}</h1>
              </div>

              {/* Meta info */}
              <div className="text-xs text-slate-500 font-bold flex items-center gap-2 mt-1.5 justify-end flex-wrap">
                {patient.gender && (
                  <span className="flex items-center gap-1">
                    <User className="w-3 h-3" />
                    {GENDER_LABELS[patient.gender]}{patient.age ? ` · ${patient.age} سنة` : ""}
                  </span>
                )}
                {patient.phone && (
                  <span className="flex items-center gap-1 font-mono" dir="ltr">
                    <Phone className="w-3 h-3" />{patient.phone}
                    <a href={`https://wa.me/${formatPhoneForWhatsApp(patient.phone)}`} target="_blank" rel="noopener noreferrer"
                      className="w-5 h-5 rounded flex items-center justify-center hover:bg-green-50 transition" style={{ color: "#22c55e" }}>
                      <MessageCircle className="w-3 h-3" />
                    </a>
                    <button type="button" onClick={async () => {
                        await navigator.clipboard.writeText(patient.phone ?? "");
                        setPhoneCopied(true); toast.success("تم نسخ الرقم");
                        setTimeout(() => setPhoneCopied(false), 2000);
                      }}
                      className="w-5 h-5 rounded flex items-center justify-center hover:bg-slate-100 transition" style={{ color: "#64748b" }}>
                      {phoneCopied ? <Check className="w-3 h-3 text-green-500" /> : <Copy className="w-3 h-3" />}
                    </button>
                  </span>
                )}
                {patient.email && (
                  <span className="flex items-center gap-1 font-mono" dir="ltr">
                    <Mail className="w-3 h-3" />{patient.email}
                  </span>
                )}
                {patient.primaryDoctorName && (
                  <span className="flex items-center gap-1">
                    <Stethoscope className="w-3 h-3" />{patient.primaryDoctorName}
                  </span>
                )}
                {patient.address && (
                  <span className="flex items-center gap-1">
                    <MapPin className="w-3 h-3" />{patient.address}
                  </span>
                )}
                <span className="text-[11px] text-slate-400">منذ {formatArabicDate(patient.createdAt)}</span>
              </div>
            </div>

            {/* Avatar */}
            <div className="relative flex-shrink-0">
              <div
                className="w-14 h-14 bg-gradient-to-br from-sky-500 to-indigo-600 rounded-2xl flex items-center justify-center font-black text-white text-lg shadow-lg shadow-sky-200/50"
              >
                {initials}
              </div>
              <span
                className="absolute -bottom-0.5 -left-0.5 w-4 h-4 border-2 border-white rounded-full"
                style={{ background: patient.isActive ? "#22c55e" : "#94a3b8" }}
              />
            </div>

            {/* Right-side actions */}
            <div className="flex items-center gap-2 flex-shrink-0">
              {!limited && patient.isActive && (
                <Link href={`/patients/${id}/edit`}
                  className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-lg border border-slate-200 text-slate-700 hover:bg-slate-50 transition">
                  <Pencil className="w-3.5 h-3.5" />تعديل
                </Link>
              )}
              {user?.role === "Admin" && patient.isActive && (
                <button onClick={() => handleArchive(id, patientName)}
                  className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-lg bg-amber-500 hover:bg-amber-600 text-white transition">
                  <Archive className="w-3.5 h-3.5" />أرشفة
                </button>
              )}
              {user?.role === "Admin" && !patient.isActive && (
                <button onClick={() => handleRestore(id, patientName)}
                  className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-lg border border-green-200 text-green-600 hover:bg-green-50 transition">
                  <RotateCcw className="w-3.5 h-3.5" />استعادة
                </button>
              )}
              <div className="relative" ref={moreMenuRef}>
                <button onClick={() => setMoreMenuOpen(!moreMenuOpen)}
                  className="w-8 h-8 rounded-lg border border-slate-200 flex items-center justify-center hover:bg-slate-50 transition text-slate-500">
                  <MoreHorizontal className="w-4 h-4" />
                </button>
                {moreMenuOpen && (
                  <div className="absolute top-full left-0 mt-1 z-30 py-1 min-w-[160px] rounded-xl bg-white shadow-xl border border-slate-200">
                    {[
                      { icon: Scissors,      label: "حالة جراحية", href: `/surgery/new?patientId=${id}&patientName=${encodeURIComponent(patientName)}` },
                      { icon: MessageCircle, label: "راسل المريض", href: `/messages?patientId=${id}` },
                      { icon: Bell,          label: "إرسال تذكير",  href: "#" },
                    ].map(({ icon: Icon, label, href }) => (
                      <Link key={label} href={href} onClick={() => setMoreMenuOpen(false)}
                        className="flex items-center gap-2.5 px-3 py-2 text-xs font-medium text-slate-700 hover:bg-slate-50 transition">
                        <Icon className="w-3.5 h-3.5 text-slate-400" />{label}
                      </Link>
                    ))}
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>

        {/* Quick Stats Bar - appears on hover */}
        <div className={`overflow-hidden transition-all duration-500 ${isBannerHovered ? 'max-h-16 opacity-100' : 'max-h-0 opacity-0'}`}>
          <div className="px-6 pb-3 grid grid-cols-6 gap-2">
            {[
              { label: 'المواعيد', value: summary?.totalAppointments ?? '—', bgClass: 'bg-sky-50 border-sky-100', textClass: 'text-sky-700' },
              { label: 'المكتملة', value: summary?.completedAppointments ?? '—', bgClass: 'bg-emerald-50 border-emerald-100', textClass: 'text-emerald-700' },
              { label: 'التقويم النشط', value: summary?.activeOrthoCases ?? '—', bgClass: 'bg-violet-50 border-violet-100', textClass: 'text-violet-700' },
              { label: 'المدفوع', value: summary?.totalPaid != null ? summary.totalPaid.toLocaleString() : '—', bgClass: 'bg-green-50 border-green-100', textClass: 'text-green-700' },
              { label: 'المستحق', value: summary?.totalOutstanding != null ? summary.totalOutstanding.toLocaleString() : '—', bgClass: 'bg-orange-50 border-orange-100', textClass: 'text-orange-700' },
              { label: 'الوصفات', value: summary?.prescriptionsCount ?? '—', bgClass: 'bg-blue-50 border-blue-100', textClass: 'text-blue-700' },
            ].map((stat, i) => (
              <div key={i} className={`${stat.bgClass} rounded-lg px-3 py-1.5 text-center border`}>
                <div className={`${stat.textClass} text-sm font-black`}>{stat.value}</div>
                <div className="text-[9px] text-slate-500 font-bold">{stat.label}</div>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* ══════════════════════════════════════════════════════
          LAYER 2 — Ribbon Command Bar (my original design)
          ══════════════════════════════════════════════════════ */}
      <div className="flex-shrink-0 bg-white border-b border-slate-200 shadow-sm">
        {/* Ribbon Header */}
        <div className="flex items-center justify-between px-4 py-1 bg-slate-50 border-b border-slate-100">
          <div className="flex items-center gap-2">
            <div className="w-1.5 h-1.5 rounded-full bg-sky-500" />
            <span className="text-[10px] font-bold text-slate-400 uppercase tracking-widest">شريط الأوامر</span>
          </div>
          <button
            onClick={() => setIsRibbonCollapsed(!isRibbonCollapsed)}
            className="text-slate-400 hover:text-slate-600 transition-colors p-1 rounded hover:bg-slate-200"
          >
            {isRibbonCollapsed ? <ChevronDown className="w-3.5 h-3.5" /> : <ChevronUp className="w-3.5 h-3.5" />}
          </button>
        </div>

        {/* Ribbon Content */}
        <div className={`overflow-hidden transition-all duration-400 ${isRibbonCollapsed ? 'max-h-0' : 'max-h-[200px]'}`}>
          <div className="p-2 overflow-x-auto flex gap-0.5">
            {visibleRibbonGroups.map((group, gi) => (
              <React.Fragment key={gi}>
                <div className="relative border-l border-slate-200 pr-1 pl-2 pt-5 pb-1 min-w-fit">
                  {/* Group Label */}
                  <span className="absolute top-1 right-2 text-[9px] text-slate-400 font-bold tracking-wider">
                    {group.label}
                  </span>
                  {/* Group Buttons */}
                  <div className="flex gap-0.5">
                    {group.items.map((item, ii) => {
                      const Icon = item.icon;
                      const isActive = ribbonActiveAction === item.label;

                      // Special items that need Link navigation
                      if (item.label === "موعد جديد") {
                        return (
                          <Link
                            key={ii}
                            href={`/appointments/new?patientId=${id}&patientName=${encodeURIComponent(patientName)}`}
                            onClick={() => setRibbonActiveAction(item.label)}
                            className={`
                              flex flex-col items-center w-[68px] p-1.5 rounded-lg transition-all duration-200
                              ${item.bgColor}
                              ${isActive ? 'ring-2 ring-sky-400 ring-offset-1 scale-95 bg-sky-50' : 'hover:scale-105'}
                              active:scale-95
                            `}
                          >
                            <div className={`w-8 h-8 rounded-lg flex items-center justify-center bg-white shadow-sm border border-slate-100 mb-0.5 ${isActive ? 'animate-bounce' : ''}`}>
                              <Icon className={`w-4 h-4 ${item.color}`} />
                            </div>
                            <span className="text-[9px] font-bold text-slate-600 whitespace-nowrap">{item.label}</span>
                          </Link>
                        );
                      }

                      if (item.label === "وصفة طبية") {
                        return (
                          <Link
                            key={ii}
                            href={`/prescriptions/new?patientId=${id}`}
                            onClick={() => setRibbonActiveAction(item.label)}
                            className={`
                              flex flex-col items-center w-[68px] p-1.5 rounded-lg transition-all duration-200
                              ${item.bgColor}
                              ${isActive ? 'ring-2 ring-sky-400 ring-offset-1 scale-95 bg-sky-50' : 'hover:scale-105'}
                              active:scale-95
                            `}
                          >
                            <div className={`w-8 h-8 rounded-lg flex items-center justify-center bg-white shadow-sm border border-slate-100 mb-0.5 ${isActive ? 'animate-bounce' : ''}`}>
                              <Icon className={`w-4 h-4 ${item.color}`} />
                            </div>
                            <span className="text-[9px] font-bold text-slate-600 whitespace-nowrap">{item.label}</span>
                          </Link>
                        );
                      }

                      if (item.label === "عقد جديد" && !limited) {
                        return (
                          <Link
                            key={ii}
                            href={`/finance/contracts/new?patientId=${id}&patientName=${encodeURIComponent(patientName)}`}
                            onClick={() => setRibbonActiveAction(item.label)}
                            className={`
                              flex flex-col items-center w-[68px] p-1.5 rounded-lg transition-all duration-200
                              ${item.bgColor}
                              ${isActive ? 'ring-2 ring-sky-400 ring-offset-1 scale-95 bg-sky-50' : 'hover:scale-105'}
                              active:scale-95
                            `}
                          >
                            <div className={`w-8 h-8 rounded-lg flex items-center justify-center bg-white shadow-sm border border-slate-100 mb-0.5 ${isActive ? 'animate-bounce' : ''}`}>
                              <Icon className={`w-4 h-4 ${item.color}`} />
                            </div>
                            <span className="text-[9px] font-bold text-slate-600 whitespace-nowrap">{item.label}</span>
                          </Link>
                        );
                      }

                      if (item.label === "حالة تقويم") {
                        return (
                          <Link
                            key={ii}
                            href={`/ortho/new?patientId=${id}`}
                            onClick={() => setRibbonActiveAction(item.label)}
                            className={`
                              flex flex-col items-center w-[68px] p-1.5 rounded-lg transition-all duration-200
                              ${item.bgColor}
                              ${isActive ? 'ring-2 ring-sky-400 ring-offset-1 scale-95 bg-sky-50' : 'hover:scale-105'}
                              active:scale-95
                            `}
                          >
                            <div className={`w-8 h-8 rounded-lg flex items-center justify-center bg-white shadow-sm border border-slate-100 mb-0.5 ${isActive ? 'animate-bounce' : ''}`}>
                              <Icon className={`w-4 h-4 ${item.color}`} />
                            </div>
                            <span className="text-[9px] font-bold text-slate-600 whitespace-nowrap">{item.label}</span>
                          </Link>
                        );
                      }

                      if (item.label === "حالة جراحة") {
                        return (
                          <Link
                            key={ii}
                            href={`/surgery/new?patientId=${id}&patientName=${encodeURIComponent(patientName)}`}
                            onClick={() => setRibbonActiveAction(item.label)}
                            className={`
                              flex flex-col items-center w-[68px] p-1.5 rounded-lg transition-all duration-200
                              ${item.bgColor}
                              ${isActive ? 'ring-2 ring-sky-400 ring-offset-1 scale-95 bg-sky-50' : 'hover:scale-105'}
                              active:scale-95
                            `}
                          >
                            <div className={`w-8 h-8 rounded-lg flex items-center justify-center bg-white shadow-sm border border-slate-100 mb-0.5 ${isActive ? 'animate-bounce' : ''}`}>
                              <Icon className={`w-4 h-4 ${item.color}`} />
                            </div>
                            <span className="text-[9px] font-bold text-slate-600 whitespace-nowrap">{item.label}</span>
                          </Link>
                        );
                      }

                      if (item.label === "طباعة") {
                        return (
                          <Link
                            key={ii}
                            href={`/patients/${id}/print/summary`}
                            onClick={() => setRibbonActiveAction(item.label)}
                            className={`
                              flex flex-col items-center w-[68px] p-1.5 rounded-lg transition-all duration-200
                              ${item.bgColor}
                              ${isActive ? 'ring-2 ring-sky-400 ring-offset-1 scale-95 bg-sky-50' : 'hover:scale-105'}
                              active:scale-95
                            `}
                          >
                            <div className={`w-8 h-8 rounded-lg flex items-center justify-center bg-white shadow-sm border border-slate-100 mb-0.5 ${isActive ? 'animate-bounce' : ''}`}>
                              <Icon className={`w-4 h-4 ${item.color}`} />
                            </div>
                            <span className="text-[9px] font-bold text-slate-600 whitespace-nowrap">{item.label}</span>
                          </Link>
                        );
                      }

                      // Skip contract button in limited view
                      if (item.label === "عقد جديد" && limited) {
                        return null;
                      }

                      // Default: button with tab-switch action
                      return (
                        <button
                          key={ii}
                          onClick={() => handleRibbonAction(item.label, item.action)}
                          className={`
                            flex flex-col items-center w-[68px] p-1.5 rounded-lg transition-all duration-200
                            ${item.bgColor}
                            ${isActive ? 'ring-2 ring-sky-400 ring-offset-1 scale-95 bg-sky-50' : 'hover:scale-105'}
                            active:scale-95
                          `}
                        >
                          <div className={`w-8 h-8 rounded-lg flex items-center justify-center bg-white shadow-sm border border-slate-100 mb-0.5 ${isActive ? 'animate-bounce' : ''}`}>
                            <Icon className={`w-4 h-4 ${item.color}`} />
                          </div>
                          <span className="text-[9px] font-bold text-slate-600 whitespace-nowrap">{item.label}</span>
                        </button>
                      );
                    })}
                  </div>
                </div>
                {/* Separator */}
                {gi < visibleRibbonGroups.length - 1 && (
                  <div className="w-px bg-slate-200 self-stretch my-2" />
                )}
              </React.Fragment>
            ))}
          </div>
        </div>
      </div>

      {/* ══════════════════════════════════════════════════════
          LAYER 3 — Pivot Tabs (my original design)
          ══════════════════════════════════════════════════════ */}
      <div className="flex-shrink-0 bg-white border-b border-slate-200">
        <div className="px-4 overflow-x-auto">
          <div className="flex items-end gap-0 min-w-fit">
            {pivotGroups.map((group, gi) => (
              <React.Fragment key={gi}>
                {group.label && gi > 0 && (
                  <div className="w-px h-6 bg-slate-200 self-center mx-1" />
                )}
                {group.label && (
                  <div className="flex items-center px-2 pb-1">
                    <span className="text-[9px] font-bold text-slate-300 uppercase tracking-wider">
                      {group.label}
                    </span>
                  </div>
                )}
                {group.tabs.map(tab => {
                  const isActive = activeTab === tab.key;
                  const isHovered = hoveredTab === tab.key;
                  return (
                    <button
                      key={tab.key}
                      onClick={() => switchTab(tab.key)}
                      onMouseEnter={() => setHoveredTab(tab.key)}
                      onMouseLeave={() => setHoveredTab(null)}
                      className={`
                        relative px-4 py-2.5 text-sm font-bold whitespace-nowrap transition-all duration-200
                        ${isActive
                          ? 'text-sky-600'
                          : isHovered
                            ? 'text-slate-700 bg-slate-50'
                            : 'text-slate-400 hover:text-slate-600'
                        }
                      `}
                    >
                      {tab.label}
                      {/* Active indicator - Microsoft Fluent style bottom bar */}
                      {isActive && (
                        <div className="absolute bottom-0 left-2 right-2 h-[3px] bg-sky-500 rounded-t-full" />
                      )}
                      {/* Hover indicator */}
                      {isHovered && !isActive && (
                        <div className="absolute bottom-0 left-2 right-2 h-[2px] bg-slate-200 rounded-t-full" />
                      )}
                    </button>
                  );
                })}
              </React.Fragment>
            ))}
          </div>
        </div>
      </div>

      {/* ══════════════════════════════════════════════════════
          LAYER 4 — Content
          ══════════════════════════════════════════════════════ */}
      <div className="flex-1 overflow-auto">
        {limited && (
          <div className="mx-4 mt-3 flex items-center gap-2 px-4 py-2.5 rounded-xl text-xs font-medium"
            style={{ background: "#fef9c3", border: "1px solid #fde047", color: "#854d0e" }}>
            <AlertTriangle className="w-4 h-4 flex-shrink-0" />
            عرض سريري محدود — بيانات الاتصال والمالية محجوبة
          </div>
        )}
        <div className="p-4 sm:p-6">{renderTabContent()}</div>
      </div>

      {/* ══════════════════════════════════════════════════════
          Confirm Dialog
          ══════════════════════════════════════════════════════ */}
      {confirmAction && (
        <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl w-full max-w-sm shadow-2xl p-6 space-y-4">
            <div className="text-center">
              <div className={cn("w-14 h-14 rounded-full flex items-center justify-center mx-auto mb-3",
                confirmAction.type === "archive" ? "bg-amber-50" : "bg-green-50")}>
                {confirmAction.type === "archive"
                  ? <Archive className="w-7 h-7 text-amber-500" />
                  : <RotateCcw className="w-7 h-7 text-green-500" />}
              </div>
              <h3 className="text-lg font-bold text-slate-900">
                {confirmAction.type === "archive" ? "أرشفة المريض" : "استعادة المريض"}
              </h3>
              <p className="text-sm text-slate-500 mt-2">
                {confirmAction.type === "archive"
                  ? `هل تريد أرشفة "${confirmAction.name}"؟`
                  : `هل تريد استعادة "${confirmAction.name}"؟`}
              </p>
            </div>
            <div className="flex gap-3">
              <button onClick={() => setConfirmAction(null)}
                className="flex-1 py-2.5 text-sm font-medium rounded-xl border border-slate-200 text-slate-700 hover:bg-slate-50 transition">
                إلغاء
              </button>
              <button onClick={executeConfirm}
                className={cn("flex-1 py-2.5 text-sm font-medium rounded-xl text-white transition",
                  confirmAction.type === "archive" ? "bg-amber-500 hover:bg-amber-600" : "bg-green-500 hover:bg-green-600")}>
                {confirmAction.type === "archive" ? "أرشفة" : "استعادة"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
