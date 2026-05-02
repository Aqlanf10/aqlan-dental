"use client";
import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import {
  User, FileText, Stethoscope, Clock, Phone, MapPin, Pencil, Grid3x3,
  Calendar, Activity, Wallet, Pill, Plus, Scissors, Image as ImageIcon,
  MessageCircle, Archive, RotateCcw, ClipboardList, CreditCard,
  FileSignature, ScanLine, ArrowRightLeft, FolderOpen, FlaskConical,
  LayoutDashboard, KeyRound,
} from "lucide-react";
import type { LucideIcon } from "lucide-react";
import type { PatientProfile } from "@/types/patient";
import api from "@/lib/api";
import { cn, GENDER_LABELS, formatArabicDate } from "@/lib/utils";
import { toast } from "@/stores/toastStore";
import { useAuthStore } from "@/stores/authStore";

// Tab components
import { OverviewTab } from "@/components/patient/tabs/OverviewTab";
import { BasicInfoTab } from "@/components/patient/tabs/BasicInfoTab";
import { MedicalHistoryTab } from "@/components/patient/tabs/MedicalHistoryTab";
import { DentalHistoryTab } from "@/components/patient/tabs/DentalHistoryTab";
import { AppointmentsTab } from "@/components/patient/tabs/AppointmentsTab";
import { VisitsTab } from "@/components/patient/tabs/VisitsTab";
import { FinanceTab } from "@/components/patient/tabs/FinanceTab";
import { ContractsTab } from "@/components/patient/tabs/ContractsTab";
import { PaymentsTab } from "@/components/patient/tabs/PaymentsTab";
import { MessagesTab } from "@/components/patient/tabs/MessagesTab";
import { OrthodonticsTab } from "@/components/patient/tabs/OrthodonticsTab";
import { GeneralDentistryTab } from "@/components/patient/tabs/GeneralDentistryTab";
import { SurgeryTab } from "@/components/patient/tabs/SurgeryTab";
import { PhotosTab } from "@/components/patient/tabs/PhotosTab";
import { RadiographsTab } from "@/components/patient/tabs/RadiographsTab";
import { PrescriptionsTab } from "@/components/patient/tabs/PrescriptionsTab";
import { ReferralsTab } from "@/components/patient/tabs/ReferralsTab";
import { DocumentsTab } from "@/components/patient/tabs/DocumentsTab";
import { LabOrdersTab } from "@/components/patient/tabs/LabOrdersTab";
import { TimelineTab } from "@/components/patient/tabs/TimelineTab";

// ─── Types ──────────────────────────────────────────────────────────────────────

interface PatientSummary {
  totalAppointments: number;
  completedAppointments: number;
  activeOrthoCases: number;
  totalPaid: number;
  totalOutstanding: number;
  prescriptionsCount: number;
}

interface OrthoCase { id: string; caseNumber: string; applianceType?: string; status: string; stagePercentage: number; doctorName?: string; }
interface SurgeryCase { id: string; caseNumber: string; surgeryType: string; status: string; doctorName?: string; }

type Tab = "overview" | "info" | "medical" | "dental" | "appointments" | "visits" |
  "finance" | "contracts" | "payments" | "messages" | "orthodontics" | "general" |
  "surgery" | "photos" | "radiographs" | "prescriptions" | "referrals" |
  "documents" | "lab-orders" | "timeline";

interface TabDef {
  key: Tab;
  label: string;
  icon: LucideIcon;
  group: number;
}

// ─── Tab Definitions ────────────────────────────────────────────────────────────

const TABS: TabDef[] = [
  // Group 0 — General
  { key: "overview",      label: "نظرة عامة",          icon: LayoutDashboard,  group: 0 },
  { key: "info",          label: "المعلومات الأساسية",  icon: User,             group: 0 },
  { key: "medical",       label: "التاريخ الطبي",       icon: FileText,         group: 0 },
  { key: "dental",        label: "التاريخ السني",       icon: Stethoscope,      group: 0 },
  // Group 1 — Clinical
  { key: "appointments",  label: "المواعيد",            icon: Calendar,         group: 1 },
  { key: "visits",        label: "الزيارات",            icon: ClipboardList,    group: 1 },
  { key: "orthodontics",  label: "التقويم",             icon: Activity,         group: 1 },
  { key: "general",       label: "طب الأسنان العام",    icon: Grid3x3,          group: 1 },
  { key: "surgery",       label: "الجراحة",             icon: Scissors,         group: 1 },
  // Group 2 — Clinical Records
  { key: "photos",        label: "الصور",               icon: ImageIcon,        group: 2 },
  { key: "radiographs",   label: "الأشعة",              icon: ScanLine,         group: 2 },
  { key: "prescriptions", label: "الوصفات",             icon: Pill,             group: 2 },
  { key: "referrals",     label: "الإحالات",            icon: ArrowRightLeft,   group: 2 },
  { key: "lab-orders",    label: "طلبات المختبر",       icon: FlaskConical,     group: 2 },
  { key: "documents",     label: "المستندات",           icon: FolderOpen,       group: 2 },
  // Group 3 — Financial
  { key: "finance",       label: "المالية",             icon: Wallet,           group: 3 },
  { key: "contracts",     label: "العقود",              icon: FileSignature,    group: 3 },
  { key: "payments",      label: "المدفوعات",           icon: CreditCard,       group: 3 },
  // Group 4 — Communication & History
  { key: "messages",      label: "الرسائل",             icon: MessageCircle,    group: 4 },
  { key: "timeline",      label: "السجل الزمني",        icon: Clock,            group: 4 },
];

// ─── Page Component ─────────────────────────────────────────────────────────────

export default function PatientProfilePage() {
  const { id } = useParams<{ id: string }>();
  const [patient, setPatient] = useState<PatientProfile | null>(null);
  const [summary, setSummary] = useState<PatientSummary | null>(null);
  const [orthoCases, setOrthoCases] = useState<OrthoCase[]>([]);
  const [surgeryCases, setSurgeryCases] = useState<SurgeryCase[]>([]);
  const [portalCreds, setPortalCreds] = useState<{ username: string; password: string } | null>(null);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<Tab>("overview");
  const { user } = useAuthStore();
  const [confirmAction, setConfirmAction] = useState<{ type: "archive" | "restore"; id: string; name: string } | null>(null);

  // ─── Actions ────────────────────────────────────────────────────────────────

  const handleArchivePatient = (patientId: string, name: string) => {
    setConfirmAction({ type: "archive", id: patientId, name });
  };

  const handleRestorePatient = (patientId: string, name: string) => {
    setConfirmAction({ type: "restore", id: patientId, name });
  };

  const executeConfirmAction = async () => {
    if (!confirmAction) return;
    try {
      if (confirmAction.type === "archive") {
        await api.delete(`/api/patients/${confirmAction.id}`);
        toast.success(`تم أرشفة المريض ${confirmAction.name} بنجاح`);
      } else {
        await api.post(`/api/patients/${confirmAction.id}/restore`);
        toast.success(`تم استعادة المريض ${confirmAction.name} بنجاح`);
      }
      const { data: updated } = await api.get<PatientProfile>(`/api/patients/${id}`);
      setPatient(updated);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? "حدث خطأ أثناء العملية");
    } finally {
      setConfirmAction(null);
    }
  };

  // ─── Data Fetching ──────────────────────────────────────────────────────────

  useEffect(() => {
    api.get<PatientProfile>(`/api/patients/${id}`)
      .then((r) => setPatient(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
    api.get<PatientSummary>(`/api/patients/${id}/summary`)
      .then((r) => setSummary(r.data))
      .catch(() => {});
    api.get<OrthoCase[]>(`/api/ortho-cases?patientId=${id}&pageSize=10`)
      .then((r) => setOrthoCases(r.data))
      .catch(() => {});
    api.get<{ data: SurgeryCase[] }>(`/api/surgery-cases?patientId=${id}&pageSize=10`)
      .then((r) => setSurgeryCases(r.data.data ?? []))
      .catch(() => {});
    api.get<{ username: string; password: string }>(`/api/patients/${id}/portal-credentials`)
      .then((r) => setPortalCreds(r.data))
      .catch(() => {});
  }, [id]);

  // ─── Loading & Not Found ────────────────────────────────────────────────────

  if (loading) {
    return (
      <div className="space-y-4 animate-pulse">
        <div className="h-28 bg-gray-100 rounded-xl" />
        <div className="h-64 bg-gray-100 rounded-xl" />
      </div>
    );
  }

  if (!patient) {
    return (
      <div className="text-center py-20 text-gray-400">
        المريض غير موجود
      </div>
    );
  }

  const patientName = `${patient.firstName} ${patient.lastName}`;

  // ─── Render Tab Content ─────────────────────────────────────────────────────

  const renderTabContent = () => {
    switch (activeTab) {
      case "overview":
        return <OverviewTab patientId={id} summary={summary} />;
      case "info":
        return <BasicInfoTab patient={patient} orthoCases={orthoCases} surgeryCases={surgeryCases} />;
      case "medical":
        return <MedicalHistoryTab patientId={id} initialData={patient.medicalHistory} />;
      case "dental":
        return <DentalHistoryTab patientId={id} initialData={patient.dentalHistory} />;
      case "appointments":
        return <AppointmentsTab patientId={id} patientName={patientName} />;
      case "visits":
        return <VisitsTab patientId={id} />;
      case "finance":
        return <FinanceTab patientId={id} totalPaid={summary?.totalPaid ?? 0} totalOutstanding={summary?.totalOutstanding ?? 0} />;
      case "contracts":
        return <ContractsTab patientId={id} />;
      case "payments":
        return <PaymentsTab patientId={id} />;
      case "messages":
        return <MessagesTab patientId={id} />;
      case "orthodontics":
        return <OrthodonticsTab patientId={id} />;
      case "general":
        return <GeneralDentistryTab patientId={id} />;
      case "surgery":
        return <SurgeryTab patientId={id} />;
      case "photos":
        return <PhotosTab patientId={id} />;
      case "radiographs":
        return <RadiographsTab patientId={id} />;
      case "prescriptions":
        return <PrescriptionsTab patientId={id} />;
      case "referrals":
        return <ReferralsTab patientId={id} />;
      case "documents":
        return <DocumentsTab patientId={id} />;
      case "lab-orders":
        return <LabOrdersTab patientId={id} />;
      case "timeline":
        return <TimelineTab patientId={id} />;
      default:
        return null;
    }
  };

  // ─── Main Render ────────────────────────────────────────────────────────────

  return (
    <div className="space-y-5 max-w-5xl page-content">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm" style={{ color: "#64748b" }}>
        <Link href="/patients" className="transition" style={{ color: "#64748b" }} onMouseEnter={(e: React.MouseEvent<HTMLAnchorElement>) => (e.currentTarget.style.color = "#3d7ab5")} onMouseLeave={(e: React.MouseEvent<HTMLAnchorElement>) => (e.currentTarget.style.color = "#64748b")}>المرضى</Link>
        <span>/</span>
        <span className="font-medium" style={{ color: "#0d2137" }}>{patient.firstName} {patient.lastName}</span>
      </div>

      {/* Banner — matches ZIP card style */}
      <div className="p-5 space-y-4" style={{ background: "#fff", borderRadius: 12, boxShadow: "0 1px 3px rgba(13,33,55,0.06), 0 1px 10px rgba(13,33,55,0.04)", border: "1px solid #e8f0f9" }}>
        {/* Top row */}
        <div className="flex items-start justify-between gap-4">
          <div className="flex items-start gap-4">
            <div className="w-16 h-16 rounded-2xl flex items-center justify-center text-xl font-extrabold flex-shrink-0" style={{ background: "#3d7ab518", color: "#3d7ab5" }}>
              {patient.firstName.charAt(0)}
            </div>
            <div>
              <div className="flex items-center gap-3 flex-wrap">
                <h1 className="text-xl font-extrabold text-gray-900">
                  {patient.firstName} {patient.middleName} {patient.lastName}
                </h1>
                <span className="font-mono text-xs px-2.5 py-1 rounded" style={{ background: "#3d7ab518", color: "#3d7ab5" }}>
                  {patient.patientNumber}
                </span>
                <span
                  className="text-xs px-2.5 py-0.5 rounded-full font-semibold"
                  style={{
                    color: patient.isActive ? "#22c55e" : "#94a3b8",
                    background: patient.isActive ? "#22c55e18" : "#94a3b818",
                  }}
                >
                  {patient.isActive ? "نشط" : "مؤرشف"}
                </span>
              </div>
              <div className="mt-2 flex flex-wrap items-center gap-4 text-sm" style={{ color: "#94a3b8" }}>
                {patient.gender && (
                  <span className="flex items-center gap-1">
                    <User className="w-3.5 h-3.5" />
                    {GENDER_LABELS[patient.gender]} {patient.age ? `· ${patient.age} سنة` : ""}
                  </span>
                )}
                {patient.phone && (
                  <span className="flex items-center gap-1 font-mono" dir="ltr">
                    <Phone className="w-3.5 h-3.5" />
                    {patient.phone}
                  </span>
                )}
                {patient.address && (
                  <span className="flex items-center gap-1">
                    <MapPin className="w-3.5 h-3.5" />
                    {patient.address}
                  </span>
                )}
                {patient.primaryDoctorName && (
                  <span className="flex items-center gap-1">
                    <Stethoscope className="w-3.5 h-3.5" />
                    {patient.primaryDoctorName}
                  </span>
                )}
              </div>
              <p className="text-xs mt-1" style={{ color: "#94a3b8" }}>
                تسجيل: {formatArabicDate(patient.createdAt)}
              </p>
              {portalCreds && (
                <div className="mt-2 flex items-center gap-2 text-xs" style={{ background: "#f5922e18", borderRadius: 8, padding: "6px 10px", border: "1px solid #f5922e30" }}>
                  <KeyRound className="w-3.5 h-3.5" style={{ color: "#f5922e" }} />
                  <span style={{ color: "#f5922e" }}>بوابة المريض:</span>
                  <span className="font-mono" style={{ color: "#0d2137" }}>اسم المستخدم: <strong>{portalCreds.username}</strong></span>
                  <span style={{ color: "#94a3b8" }}>|</span>
                  <span className="font-mono" style={{ color: "#0d2137" }}>كلمة المرور: <strong>{portalCreds.password}</strong></span>
                </div>
              )}
            </div>
          </div>
          {patient.isActive && (
            <Link
              href={`/patients/${id}/edit`}
              className="flex items-center gap-1.5 px-3 py-1.5 text-sm font-semibold rounded-lg transition flex-shrink-0"
              style={{ border: "1.5px solid #3d7ab5", color: "#3d7ab5", background: "#fff" }}
              onMouseEnter={(e: React.MouseEvent<HTMLAnchorElement>) => (e.currentTarget.style.background = "#eef3f9")}
              onMouseLeave={(e: React.MouseEvent<HTMLAnchorElement>) => (e.currentTarget.style.background = "#fff")}
            >
              <Pencil className="w-3.5 h-3.5" />
              تعديل
            </Link>
          )}
          {user?.role === "Admin" && patient.isActive && (
            <button
              onClick={() => handleArchivePatient(id, patientName)}
              className="flex items-center gap-1.5 px-3 py-1.5 text-sm font-semibold rounded-lg transition flex-shrink-0"
              style={{ background: "#f5922e", color: "#fff" }}
              onMouseEnter={(e) => (e.currentTarget.style.background = "#e07d1e")}
              onMouseLeave={(e) => (e.currentTarget.style.background = "#f5922e")}
            >
              <Archive className="w-3.5 h-3.5" />
              أرشفة
            </button>
          )}
          {user?.role === "Admin" && !patient.isActive && (
            <button
              onClick={() => handleRestorePatient(id, patientName)}
              className="flex items-center gap-1.5 px-3 py-1.5 text-sm border border-green-200 rounded-lg hover:bg-green-50 transition text-green-600 flex-shrink-0"
            >
              <RotateCcw className="w-3.5 h-3.5" />
              استعادة
            </button>
          )}
          <Link
            href={`/messages?patientId=${id}`}
            className="flex items-center gap-1.5 px-3 py-1.5 text-sm font-semibold rounded-lg transition flex-shrink-0"
            style={{ background: "#3d7ab5", color: "#fff" }}
            onMouseEnter={(e: React.MouseEvent<HTMLAnchorElement>) => (e.currentTarget.style.background = "#2d5e8e")}
            onMouseLeave={(e: React.MouseEvent<HTMLAnchorElement>) => (e.currentTarget.style.background = "#3d7ab5")}
          >
            <MessageCircle className="w-3.5 h-3.5" />
            مراسلة
          </Link>
        </div>

        {/* Quick stats row */}
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3 pt-1" style={{ borderTop: "1px solid #f1f5f9" }}>
          {[
            { icon: Calendar, label: "المواعيد",     value: summary?.totalAppointments   ?? "—", color: "#3d7ab5", bg: "#3d7ab518" },
            { icon: Calendar, label: "مكتملة",       value: summary?.completedAppointments ?? "—", color: "#22c55e",  bg: "#22c55e18" },
            { icon: Activity, label: "تقويم نشط",   value: summary?.activeOrthoCases    ?? "—", color: "#a855f7", bg: "#a855f718" },
            { icon: Wallet,   label: "مدفوع",        value: summary ? `${summary.totalPaid.toLocaleString()}` : "—", color: "#3d7ab5", bg: "#3d7ab518" },
            { icon: Wallet,   label: "متبقي",        value: summary ? `${summary.totalOutstanding.toLocaleString()}` : "—", color: "#f5922e", bg: "#f5922e18" },
            { icon: Pill,     label: "الوصفات",      value: summary?.prescriptionsCount  ?? "—", color: "#ef4444",   bg: "#ef444418" },
          ].map(({ icon: Icon, label, value, color, bg }) => (
            <div key={label} className="rounded-lg px-3 py-2 flex items-center gap-2" style={{ background: bg }}>
              <Icon className="w-4 h-4 flex-shrink-0" style={{ color }} />
              <div className="min-w-0">
                <p className="text-xs truncate" style={{ color: "#94a3b8" }}>{label}</p>
                <p className="text-sm font-bold truncate" style={{ color }}>{value}</p>
              </div>
            </div>
          ))}
        </div>

        {/* Action buttons */}
        <div className="flex flex-wrap gap-2 pt-1" style={{ borderTop: "1px solid #f1f5f9" }}>
          <Link
            href={`/appointments/new?patientId=${id}&patientName=${encodeURIComponent(patientName)}`}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-lg text-white transition"
            style={{ background: "#3d7ab5" }}
            onMouseEnter={(e: React.MouseEvent<HTMLAnchorElement>) => (e.currentTarget.style.background = "#2d5e8e")}
            onMouseLeave={(e: React.MouseEvent<HTMLAnchorElement>) => (e.currentTarget.style.background = "#3d7ab5")}
          >
            <Plus className="w-3.5 h-3.5" />
            موعد جديد
          </Link>
          <Link
            href={`/prescriptions/new?patientId=${id}&patientName=${encodeURIComponent(patientName)}`}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-lg transition"
            style={{ border: "1.5px solid #3d7ab5", color: "#3d7ab5", background: "#fff" }}
            onMouseEnter={(e: React.MouseEvent<HTMLAnchorElement>) => (e.currentTarget.style.background = "#eef3f9")}
            onMouseLeave={(e: React.MouseEvent<HTMLAnchorElement>) => (e.currentTarget.style.background = "#fff")}
          >
            <Pill className="w-3.5 h-3.5" />
            وصفة طبية
          </Link>
          <Link
            href={`/finance/contracts/new?patientId=${id}&patientName=${encodeURIComponent(patientName)}`}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-lg transition"
            style={{ border: "1.5px solid #3d7ab5", color: "#3d7ab5", background: "#fff" }}
            onMouseEnter={(e: React.MouseEvent<HTMLAnchorElement>) => (e.currentTarget.style.background = "#eef3f9")}
            onMouseLeave={(e: React.MouseEvent<HTMLAnchorElement>) => (e.currentTarget.style.background = "#fff")}
          >
            <Wallet className="w-3.5 h-3.5" />
            عقد جديد
          </Link>
          <Link
            href={`/ortho/new?patientId=${id}&patientName=${encodeURIComponent(patientName)}`}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-lg transition"
            style={{ border: "1.5px solid #dce8f5", color: "#64748b", background: "#fff" }}
            onMouseEnter={(e: React.MouseEvent<HTMLAnchorElement>) => (e.currentTarget.style.background = "#eef3f9")}
            onMouseLeave={(e: React.MouseEvent<HTMLAnchorElement>) => (e.currentTarget.style.background = "#fff")}
          >
            <Activity className="w-3.5 h-3.5" />
            حالة تقويمية
          </Link>
          <Link
            href={`/surgery/new?patientId=${id}&patientName=${encodeURIComponent(patientName)}`}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-lg transition"
            style={{ border: "1.5px solid #dce8f5", color: "#64748b", background: "#fff" }}
            onMouseEnter={(e: React.MouseEvent<HTMLAnchorElement>) => (e.currentTarget.style.background = "#eef3f9")}
            onMouseLeave={(e: React.MouseEvent<HTMLAnchorElement>) => (e.currentTarget.style.background = "#fff")}
          >
            <Scissors className="w-3.5 h-3.5" />
            حالة جراحية
          </Link>
        </div>
      </div>

      {/* Tabs — matches ZIP tab style */}
      <div className="overflow-hidden" style={{ background: "#fff", borderRadius: 12, boxShadow: "0 1px 3px rgba(13,33,55,0.06), 0 1px 10px rgba(13,33,55,0.04)", border: "1px solid #e8f0f9" }}>
        <div className="flex overflow-x-auto" dir="rtl" style={{ borderBottom: "2px solid #e8f0f9" }}>
          {TABS.map((tab, idx) => (
            <span key={tab.key} className="contents">
              {/* Group separator */}
              {idx > 0 && TABS[idx - 1].group !== tab.group && (
                <div className="w-px self-stretch my-2 flex-shrink-0" style={{ background: "#e8f0f9" }} />
              )}
              <button
                onClick={() => setActiveTab(tab.key)}
                className="flex items-center gap-1.5 px-4 py-2.5 text-[13px] font-medium whitespace-nowrap transition"
                style={{
                  borderBottom: activeTab === tab.key ? "2px solid #3d7ab5" : "2px solid transparent",
                  color: activeTab === tab.key ? "#3d7ab5" : "#64748b",
                  fontWeight: activeTab === tab.key ? 700 : 500,
                  marginBottom: -2,
                }}
              >
                <tab.icon className="w-3.5 h-3.5" />
                {tab.label}
              </button>
            </span>
          ))}
        </div>

        <div className="p-5">
          {renderTabContent()}
        </div>
      </div>

      {/* Confirmation Dialog */}
      {confirmAction && (
        <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl w-full max-w-sm shadow-2xl p-6 space-y-4">
            <div className="text-center">
              <div className={cn(
                "w-14 h-14 rounded-full flex items-center justify-center mx-auto mb-3",
                confirmAction.type === "archive" ? "bg-orange-100" : "bg-green-100"
              )}>
                {confirmAction.type === "archive"
                  ? <Archive className="w-7 h-7 text-orange-600" />
                  : <RotateCcw className="w-7 h-7 text-green-600" />
                }
              </div>
              <h3 className="text-lg font-bold text-gray-900">
                {confirmAction.type === "archive" ? "أرشفة المريض" : "استعادة المريض"}
              </h3>
              <p className="text-sm text-gray-500 mt-2">
                {confirmAction.type === "archive"
                  ? `هل أنت متأكد من أرشفة المريض "${confirmAction.name}"؟ لن يظهر في قائمة المرضى النشطين، ويمكن استعادته لاحقًا.`
                  : `هل أنت متأكد من استعادة المريض "${confirmAction.name}"؟ سيظهر مجدداً في قائمة المرضى النشطين.`
                }
              </p>
            </div>
            <div className="flex gap-3">
              <button
                onClick={() => setConfirmAction(null)}
                className="flex-1 py-2.5 text-sm font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
              >
                إلغاء
              </button>
              <button
                onClick={executeConfirmAction}
                className={cn(
                  "flex-1 py-2.5 text-sm font-medium rounded-lg text-white transition",
                  confirmAction.type === "archive"
                    ? "bg-orange-500 hover:bg-orange-600"
                    : "bg-green-500 hover:bg-green-600"
                )}
              >
                {confirmAction.type === "archive" ? "أرشفة" : "استعادة"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
