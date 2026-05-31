"use client";

import { useEffect, useRef, useState, useCallback } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import {
  User, FileText, Stethoscope, Clock, Phone, MapPin, Pencil, Grid3x3,
  Calendar, Activity, Wallet, Pill, Plus, Scissors, Image as ImageIcon,
  MessageCircle, Archive, RotateCcw, ClipboardList, CreditCard,
  FileSignature, ScanLine, ArrowRightLeft, FolderOpen, FlaskConical,
  LayoutDashboard, KeyRound, Copy, Check, Mail,
  AlertTriangle, Shield, Receipt, Zap, ChevronRight,
  TrendingUp, Star, MoreHorizontal, Bell,
} from "lucide-react";
import type { LucideIcon } from "lucide-react";
import type { PatientProfile } from "@/types/patient";
import api from "@/lib/api";
import { cn, GENDER_LABELS, formatArabicDate, normalizePhone, formatPhoneForWhatsApp } from "@/lib/utils";
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

// ─── Ribbon Group Definitions ────────────────────────────────────────────────────

interface RibbonGroup {
  id: number;
  label: string;
  color: string;
  accent: string;
  tabs: { key: Tab; label: string; icon: LucideIcon }[];
}

const RIBBON_GROUPS: RibbonGroup[] = [
  {
    id: 0, label: "الملف", color: "#0d2137", accent: "#3d7ab5",
    tabs: [
      { key: "overview",  label: "نظرة عامة",    icon: LayoutDashboard },
      { key: "info",      label: "المعلومات",     icon: User },
      { key: "medical",   label: "التاريخ الطبي", icon: FileText },
      { key: "dental",    label: "التاريخ السني", icon: Stethoscope },
    ],
  },
  {
    id: 1, label: "سريري", color: "#14532d", accent: "#22c55e",
    tabs: [
      { key: "appointments",   label: "المواعيد",   icon: Calendar },
      { key: "visits",         label: "الزيارات",   icon: ClipboardList },
      { key: "treatment-plan", label: "خطة العلاج", icon: Star },
      { key: "orthodontics",   label: "التقويم",    icon: Activity },
      { key: "general",        label: "طب عام",     icon: Grid3x3 },
      { key: "surgery",        label: "الجراحة",    icon: Scissors },
    ],
  },
  {
    id: 2, label: "سجلات", color: "#4c1d95", accent: "#a855f7",
    tabs: [
      { key: "photos",        label: "الصور",      icon: ImageIcon },
      { key: "radiographs",   label: "الأشعة",     icon: ScanLine },
      { key: "prescriptions", label: "الوصفات",    icon: Pill },
      { key: "referrals",     label: "الإحالات",   icon: ArrowRightLeft },
      { key: "lab-orders",    label: "المختبر",    icon: FlaskConical },
      { key: "documents",     label: "المستندات",  icon: FolderOpen },
    ],
  },
  {
    id: 3, label: "مالي", color: "#78350f", accent: "#f59e0b",
    tabs: [
      { key: "finance",   label: "الحساب",     icon: Wallet },
      { key: "contracts", label: "العقود",     icon: FileSignature },
      { key: "payments",  label: "المدفوعات",  icon: CreditCard },
    ],
  },
  {
    id: 4, label: "تواصل", color: "#0c4a6e", accent: "#0ea5e9",
    tabs: [
      { key: "messages", label: "الرسائل",      icon: MessageCircle },
      { key: "timeline", label: "السجل الزمني", icon: Clock },
    ],
  },
  {
    id: 5, label: "البوابة", color: "#1f2937", accent: "#6b7280",
    tabs: [
      { key: "portal-access", label: "بوابة المريض", icon: KeyRound },
    ],
  },
];

const DOCTOR_HIDDEN_TABS: Tab[] = ["finance","contracts","payments","messages","portal-access"];

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
  const [activeGroup,  setActiveGroup]  = useState<number>(0);
  const [phoneCopied,  setPhoneCopied]  = useState(false);
  const [openAddVisitModal, setOpenAddVisitModal] = useState(false);
  const [confirmAction, setConfirmAction] = useState<{ type: "archive" | "restore"; id: string; name: string } | null>(null);
  const [moreMenuOpen,  setMoreMenuOpen]  = useState(false);
  const moreMenuRef = useRef<HTMLDivElement>(null);

  // ─── Handlers ───────────────────────────────────────────────────────────────

  const switchTab = useCallback((key: Tab) => {
    setActiveTab(key);
    const g = RIBBON_GROUPS.find(g => g.tabs.some(t => t.key === key));
    if (g) setActiveGroup(g.id);
  }, []);

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

  // ─── Data Fetching ──────────────────────────────────────────────────────────

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

  // ─── Tab Content ────────────────────────────────────────────────────────────

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

  // ─── States ─────────────────────────────────────────────────────────────────

  if (loading) return (
    <div className="h-full flex flex-col animate-pulse bg-slate-50" dir="rtl">
      <div className="h-[116px] bg-white border-b border-slate-200 flex items-center gap-4 px-6">
        <div className="w-14 h-14 rounded-2xl bg-slate-200 flex-shrink-0" />
        <div className="flex-1 space-y-2">
          <div className="h-5 bg-slate-200 rounded w-48" />
          <div className="h-3 bg-slate-100 rounded w-64" />
        </div>
      </div>
      <div className="h-10 bg-white border-b border-slate-200" />
      <div className="h-11 bg-slate-50 border-b border-slate-200" />
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

  const limited      = patient.isLimitedView === true;
  const patientName  = `${patient.firstName} ${patient.middleName ?? ""} ${patient.lastName}`.replace(/\s+/g," ").trim();
  const initials     = (patient.firstName[0] ?? "") + (patient.lastName[0] ?? "");
  const outstanding  = summary?.totalOutstanding ?? 0;
  const hasDebt      = outstanding > 0;

  const visibleGroups = limited
    ? RIBBON_GROUPS
        .map(g => ({ ...g, tabs: g.tabs.filter(t => !DOCTOR_HIDDEN_TABS.includes(t.key)) }))
        .filter(g => g.tabs.length > 0)
    : RIBBON_GROUPS;

  const activeGroupData = visibleGroups.find(g => g.id === activeGroup) ?? visibleGroups[0]!;

  // ─── Render ──────────────────────────────────────────────────────────────────

  return (
    <div className="h-full flex flex-col bg-[#f3f4f6] overflow-hidden" dir="rtl">

      {/* ══════════════════════════════════════════════════════
          LAYER 1 — Patient Banner
          ══════════════════════════════════════════════════════ */}
      <div className="flex-shrink-0 bg-white border-b border-slate-200" style={{ boxShadow: "0 1px 0 rgba(0,0,0,0.05)" }}>
        <div className="px-5 pt-3 pb-0">

          {/* Breadcrumb */}
          <div className="flex items-center gap-1.5 text-[11px] text-slate-400 mb-2.5">
            <Link href="/patients" className="hover:text-blue-600 transition-colors">المرضى</Link>
            <ChevronRight className="w-3 h-3 rotate-180" />
            <span className="text-slate-700 font-medium">{patientName}</span>
            <span className="mx-1.5 text-slate-200">|</span>
            <span className="font-mono text-slate-400">{patient.patientNumber}</span>
          </div>

          {/* Identity row */}
          <div className="flex items-start gap-4 pb-3">

            {/* Avatar with status dot */}
            <div className="relative flex-shrink-0">
              <div
                className="w-14 h-14 rounded-2xl flex items-center justify-center text-xl font-black select-none"
                style={{
                  background: patient.isActive
                    ? "linear-gradient(135deg,#3d7ab5 0%,#1e4d7a 100%)"
                    : "linear-gradient(135deg,#94a3b8 0%,#64748b 100%)",
                  color: "#fff",
                  boxShadow: "0 4px 14px rgba(61,122,181,.28)",
                }}
              >
                {initials}
              </div>
              <span
                className="absolute -bottom-1 -left-1 w-4 h-4 rounded-full border-2 border-white"
                style={{ background: patient.isActive ? "#22c55e" : "#94a3b8" }}
              />
            </div>

            {/* Name + details */}
            <div className="flex-1 min-w-0">
              <div className="flex flex-wrap items-center gap-2 mb-1">
                <h1 className="text-xl font-black text-slate-900 leading-tight">{patientName}</h1>

                <span className="text-[10px] font-bold px-2 py-0.5 rounded-full"
                  style={{ background: patient.isActive ? "#dcfce7" : "#f1f5f9", color: patient.isActive ? "#15803d" : "#64748b" }}>
                  {patient.isActive ? "● نشط" : "● مؤرشف"}
                </span>

                {patient.medicalHistory?.chronicDiseases && (
                  <span className="flex items-center gap-1 text-[10px] font-bold px-2 py-0.5 rounded-full bg-red-50 text-red-600 border border-red-100">
                    <Shield className="w-2.5 h-2.5" />تنبيه طبي
                  </span>
                )}

                {hasDebt && !limited && (
                  <span className="flex items-center gap-1 text-[10px] font-bold px-2 py-0.5 rounded-full bg-amber-50 text-amber-700 border border-amber-100">
                    <Receipt className="w-2.5 h-2.5" />{outstanding.toLocaleString()} ر.ي متبقي
                  </span>
                )}
              </div>

              <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-[12px] text-slate-500">
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
                    <a href={`tel:${normalizePhone(patient.phone)}`}
                      className="w-5 h-5 rounded flex items-center justify-center hover:bg-blue-50 transition" style={{ color: "#3d7ab5" }}>
                      <Phone className="w-3 h-3" />
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

            {/* Right-side actions */}
            <div className="flex items-center gap-2 flex-shrink-0">
              {!limited && patient.isActive && (
                <Link href={`/patients/${id}/edit`}
                  className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-lg border transition"
                  style={{ borderColor: "#d1d5db", color: "#374151", background: "#fff" }}
                  onMouseEnter={e => { (e.currentTarget as HTMLElement).style.background = "#f9fafb"; }}
                  onMouseLeave={e => { (e.currentTarget as HTMLElement).style.background = "#fff"; }}>
                  <Pencil className="w-3.5 h-3.5" />تعديل
                </Link>
              )}
              {user?.role === "Admin" && patient.isActive && (
                <button onClick={() => handleArchive(id, patientName)}
                  className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-lg text-white transition"
                  style={{ background: "#f59e0b" }}
                  onMouseEnter={e => { (e.currentTarget as HTMLElement).style.background = "#d97706"; }}
                  onMouseLeave={e => { (e.currentTarget as HTMLElement).style.background = "#f59e0b"; }}>
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
                  className="w-8 h-8 rounded-lg border flex items-center justify-center hover:bg-slate-50 transition"
                  style={{ borderColor: "#d1d5db", color: "#64748b" }}>
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

          {/* KPI Strip */}
          <div className="flex items-stretch border-t border-slate-100 -mx-5 px-5">
            {[
              { icon: Calendar,   label: "مواعيد",      value: summary?.totalAppointments    ?? "—", color: "#3d7ab5" },
              { icon: Check,      label: "مكتملة",      value: summary?.completedAppointments ?? "—", color: "#22c55e" },
              { icon: Activity,   label: "تقويم نشط",   value: summary?.activeOrthoCases     ?? "—", color: "#a855f7" },
              { icon: TrendingUp, label: "مدفوع",        value: summary?.totalPaid != null ? summary.totalPaid.toLocaleString() : "—", color: "#22c55e" },
              { icon: Wallet,     label: "متبقي",        value: summary?.totalOutstanding != null ? summary.totalOutstanding.toLocaleString() : "—", color: hasDebt ? "#f59e0b" : "#94a3b8" },
              { icon: Pill,       label: "وصفات",        value: summary?.prescriptionsCount   ?? "—", color: "#ef4444" },
            ].map(({ icon: Icon, label, value, color }, idx) => (
              <div key={label}
                className={cn("flex items-center gap-2 px-3 py-2 text-xs flex-1 justify-center select-none",
                  idx < 5 && "border-l border-slate-100")}
              >
                <Icon className="w-3.5 h-3.5 flex-shrink-0" style={{ color }} />
                <div className="text-center">
                  <div className="font-black text-slate-800 leading-none text-sm">{value}</div>
                  <div className="text-[10px] text-slate-400 mt-0.5">{label}</div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* ══════════════════════════════════════════════════════
          LAYER 2 — Microsoft Ribbon
          ══════════════════════════════════════════════════════ */}
      <div className="flex-shrink-0 bg-white border-b border-slate-200 select-none" dir="rtl">

        {/* Row A — Group Tabs (Office ribbon tab style) */}
        <div className="flex items-end gap-0 px-2 pt-1" style={{ borderBottom: "1px solid #e5e7eb" }}>
          {visibleGroups.map(group => {
            const isActive = group.id === activeGroup;
            return (
              <button key={group.id}
                onClick={() => {
                  setActiveGroup(group.id);
                  if (!group.tabs.find(t => t.key === activeTab)) setActiveTab(group.tabs[0]!.key);
                }}
                className="flex items-center gap-1.5 px-4 py-2 text-[12px] font-semibold rounded-t-lg transition-all"
                style={{
                  color:        isActive ? group.accent : "#64748b",
                  background:   isActive ? "#fff" : "transparent",
                  borderTop:    isActive ? `2px solid ${group.accent}` : "2px solid transparent",
                  borderLeft:   isActive ? "1px solid #e5e7eb" : "1px solid transparent",
                  borderRight:  isActive ? "1px solid #e5e7eb" : "1px solid transparent",
                  borderBottom: isActive ? "1px solid #fff" : "1px solid transparent",
                  marginBottom: isActive ? -1 : 0,
                  zIndex:       isActive ? 2 : 1,
                }}
              >
                {group.label}
              </button>
            );
          })}

          <div className="flex-1" />

          {/* Quick-action pills */}
          <div className="flex items-center gap-1.5 pb-1.5">
            <button
              onClick={() => { switchTab("visits"); setOpenAddVisitModal(true); }}
              className="flex items-center gap-1.5 px-3 py-1.5 text-[11px] font-bold rounded-lg text-white transition"
              style={{ background: "#22c55e" }}
              onMouseEnter={e => { (e.currentTarget as HTMLElement).style.background = "#16a34a"; }}
              onMouseLeave={e => { (e.currentTarget as HTMLElement).style.background = "#22c55e"; }}>
              <Zap className="w-3 h-3" />زيارة جديدة
            </button>
            <Link href={`/appointments/new?patientId=${id}&patientName=${encodeURIComponent(patientName)}`}
              className="flex items-center gap-1.5 px-3 py-1.5 text-[11px] font-bold rounded-lg text-white transition"
              style={{ background: "#3d7ab5" }}
              onMouseEnter={e => { (e.currentTarget as HTMLElement).style.background = "#2d5e8e"; }}
              onMouseLeave={e => { (e.currentTarget as HTMLElement).style.background = "#3d7ab5"; }}>
              <Plus className="w-3 h-3" />موعد
            </Link>
            {!limited && (
              <Link href={`/finance/contracts/new?patientId=${id}&patientName=${encodeURIComponent(patientName)}`}
                className="flex items-center gap-1.5 px-3 py-1.5 text-[11px] font-bold rounded-lg border transition"
                style={{ borderColor: "#d1d5db", color: "#374151", background: "#fff" }}
                onMouseEnter={e => { (e.currentTarget as HTMLElement).style.background = "#f9fafb"; }}
                onMouseLeave={e => { (e.currentTarget as HTMLElement).style.background = "#fff"; }}>
                <FileSignature className="w-3 h-3" />عقد
              </Link>
            )}
          </div>
        </div>

        {/* Row B — Ribbon Command Buttons */}
        <div className="flex items-center gap-0.5 px-3 py-1.5 min-h-[48px]" style={{ background: "#fafafa" }}>

          {activeGroupData.tabs.map(tab => {
            const isActive = activeTab === tab.key;
            return (
              <button key={tab.key}
                onClick={() => setActiveTab(tab.key)}
                className="flex flex-col items-center justify-center gap-0.5 px-3 py-1.5 rounded-lg transition-all min-w-[56px]"
                style={{
                  background: isActive ? `${activeGroupData.accent}15` : "transparent",
                  color:      isActive ? activeGroupData.accent : "#64748b",
                  border:     isActive ? `1px solid ${activeGroupData.accent}25` : "1px solid transparent",
                }}
                onMouseEnter={e => { if (!isActive) (e.currentTarget as HTMLElement).style.background = "#f1f5f9"; }}
                onMouseLeave={e => { if (!isActive) (e.currentTarget as HTMLElement).style.background = "transparent"; }}
              >
                <tab.icon className="w-4 h-4" />
                <span className="text-[10px] font-semibold whitespace-nowrap leading-tight">{tab.label}</span>
              </button>
            );
          })}

          {/* Group-specific extra actions */}
          {activeGroupData.id === 1 && (
            <>
              <div className="w-px h-8 bg-slate-200 mx-1.5" />
              <Link href={`/ortho/new?patientId=${id}`}
                className="flex flex-col items-center gap-0.5 px-3 py-1.5 rounded-lg text-slate-400 hover:bg-slate-100 hover:text-slate-600 transition min-w-[56px]">
                <Plus className="w-4 h-4" />
                <span className="text-[10px] font-semibold">تقويم</span>
              </Link>
            </>
          )}
          {activeGroupData.id === 2 && (
            <>
              <div className="w-px h-8 bg-slate-200 mx-1.5" />
              <Link href={`/prescriptions/new?patientId=${id}`}
                className="flex flex-col items-center gap-0.5 px-3 py-1.5 rounded-lg text-slate-400 hover:bg-slate-100 hover:text-slate-600 transition min-w-[56px]">
                <Plus className="w-4 h-4" />
                <span className="text-[10px] font-semibold">وصفة</span>
              </Link>
            </>
          )}
          {activeGroupData.id === 3 && !limited && (
            <>
              <div className="w-px h-8 bg-slate-200 mx-1.5" />
              <button onClick={() => setActiveTab("payments")}
                className="flex flex-col items-center gap-0.5 px-3 py-1.5 rounded-lg hover:bg-green-50 transition min-w-[56px]"
                style={{ color: "#22c55e" }}>
                <CreditCard className="w-4 h-4" />
                <span className="text-[10px] font-semibold">قبض</span>
              </button>
              <button onClick={() => setActiveTab("finance")}
                className="flex flex-col items-center gap-0.5 px-3 py-1.5 rounded-lg hover:bg-amber-50 transition min-w-[56px]"
                style={{ color: "#f59e0b" }}>
                <TrendingUp className="w-4 h-4" />
                <span className="text-[10px] font-semibold">كشف</span>
              </button>
            </>
          )}

          <div className="flex-1" />

          {/* Active group label accent */}
          <div className="flex items-center gap-1.5 px-3 py-1 rounded-lg text-[10px] font-bold"
            style={{ background: `${activeGroupData.accent}12`, color: activeGroupData.accent }}>
            {activeGroupData.label}
          </div>
        </div>
      </div>

      {/* ══════════════════════════════════════════════════════
          LAYER 3 — Content
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
