"use client";
import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import {
  User, FileText, Stethoscope, Clock, Phone, MapPin, Pencil, Grid3x3,
  Calendar, Activity, Wallet, Pill, Plus, Scissors, Image as ImageIcon,
  MessageCircle, Archive, RotateCcw, ClipboardList, CreditCard,
  FileSignature, ScanLine, ArrowRightLeft, FolderOpen, FlaskConical,
  LayoutDashboard,
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
        return <VisitsTab />;
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
        return <DocumentsTab />;
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
    <div className="space-y-5 max-w-5xl">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Link href="/patients" className="hover:text-clinic-blue transition">المرضى</Link>
        <span>/</span>
        <span className="text-gray-900 font-medium">{patient.firstName} {patient.lastName}</span>
      </div>

      {/* Banner */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-4">
        {/* Top row */}
        <div className="flex items-start justify-between gap-4">
          <div className="flex items-start gap-4">
            <div className="w-14 h-14 blue-gradient rounded-2xl flex items-center justify-center text-white text-xl font-extrabold flex-shrink-0">
              {patient.firstName.charAt(0)}
            </div>
            <div>
              <div className="flex items-center gap-3 flex-wrap">
                <h1 className="text-xl font-extrabold text-gray-900">
                  {patient.firstName} {patient.middleName} {patient.lastName}
                </h1>
                <span className="font-mono text-xs bg-gray-100 px-2.5 py-1 rounded text-gray-600">
                  {patient.patientNumber}
                </span>
                <span className={cn(
                  "text-xs px-2 py-0.5 rounded-full font-medium",
                  patient.isActive
                    ? "bg-green-100 text-green-700"
                    : "bg-orange-100 text-orange-600"
                )}>
                  {patient.isActive ? "نشط" : "مؤرشف"}
                </span>
              </div>
              <div className="mt-2 flex flex-wrap items-center gap-4 text-sm text-gray-500">
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
              <p className="text-xs text-gray-400 mt-1">
                تسجيل: {formatArabicDate(patient.createdAt)}
              </p>
            </div>
          </div>
          {patient.isActive && (
            <Link
              href={`/patients/${id}/edit`}
              className="flex items-center gap-1.5 px-3 py-1.5 text-sm border border-gray-200 rounded-lg hover:bg-gray-50 transition text-gray-600 flex-shrink-0"
            >
              <Pencil className="w-3.5 h-3.5" />
              تعديل
            </Link>
          )}
          {user?.role === "Admin" && patient.isActive && (
            <button
              onClick={() => handleArchivePatient(id, patientName)}
              className="flex items-center gap-1.5 px-3 py-1.5 text-sm border border-orange-200 rounded-lg hover:bg-orange-50 transition text-orange-600 flex-shrink-0"
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
            className="flex items-center gap-1.5 px-3 py-1.5 text-sm border border-clinic-blue/30 rounded-lg hover:bg-clinic-blue/5 transition text-clinic-blue flex-shrink-0"
          >
            <MessageCircle className="w-3.5 h-3.5" />
            مراسلة
          </Link>
        </div>

        {/* Quick stats row */}
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3 pt-1 border-t border-gray-50">
          {[
            { icon: Calendar, label: "المواعيد",     value: summary?.totalAppointments   ?? "—", color: "text-blue-600",   bg: "bg-blue-50" },
            { icon: Calendar, label: "مكتملة",       value: summary?.completedAppointments ?? "—", color: "text-green-600",  bg: "bg-green-50" },
            { icon: Activity, label: "تقويم نشط",   value: summary?.activeOrthoCases    ?? "—", color: "text-purple-600", bg: "bg-purple-50" },
            { icon: Wallet,   label: "مدفوع",        value: summary ? `${summary.totalPaid.toLocaleString()}` : "—", color: "text-clinic-blue", bg: "bg-clinic-blue-50" },
            { icon: Wallet,   label: "متبقي",        value: summary ? `${summary.totalOutstanding.toLocaleString()}` : "—", color: "text-clinic-orange", bg: "bg-clinic-orange-50" },
            { icon: Pill,     label: "الوصفات",      value: summary?.prescriptionsCount  ?? "—", color: "text-rose-600",   bg: "bg-rose-50" },
          ].map(({ icon: Icon, label, value, color, bg }) => (
            <div key={label} className={cn("rounded-lg px-3 py-2 flex items-center gap-2", bg)}>
              <Icon className={cn("w-4 h-4 flex-shrink-0", color)} />
              <div className="min-w-0">
                <p className="text-xs text-gray-500 truncate">{label}</p>
                <p className={cn("text-sm font-bold truncate", color)}>{value}</p>
              </div>
            </div>
          ))}
        </div>

        {/* Action buttons */}
        <div className="flex flex-wrap gap-2 pt-1 border-t border-gray-50">
          <Link
            href={`/appointments/new?patientId=${id}&patientName=${encodeURIComponent(patientName)}`}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
          >
            <Plus className="w-3.5 h-3.5" />
            موعد جديد
          </Link>
          <Link
            href={`/prescriptions/new?patientId=${id}&patientName=${encodeURIComponent(patientName)}`}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg border border-gray-200 text-gray-600 hover:bg-gray-50 transition"
          >
            <Pill className="w-3.5 h-3.5" />
            وصفة طبية
          </Link>
          <Link
            href={`/finance/contracts/new?patientId=${id}&patientName=${encodeURIComponent(patientName)}`}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg border border-gray-200 text-gray-600 hover:bg-gray-50 transition"
          >
            <Wallet className="w-3.5 h-3.5" />
            عقد جديد
          </Link>
          <Link
            href={`/ortho/new?patientId=${id}&patientName=${encodeURIComponent(patientName)}`}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg border border-gray-200 text-gray-600 hover:bg-gray-50 transition"
          >
            <Activity className="w-3.5 h-3.5" />
            حالة تقويمية
          </Link>
          <Link
            href={`/surgery/new?patientId=${id}&patientName=${encodeURIComponent(patientName)}`}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg border border-gray-200 text-gray-600 hover:bg-gray-50 transition"
          >
            <Scissors className="w-3.5 h-3.5" />
            حالة جراحية
          </Link>
        </div>
      </div>

      {/* Tabs */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
        <div className="flex border-b border-gray-100 overflow-x-auto" dir="rtl">
          {TABS.map((tab, idx) => (
            <span key={tab.key} className="contents">
              {/* Group separator */}
              {idx > 0 && TABS[idx - 1].group !== tab.group && (
                <div className="w-px self-stretch bg-gray-200 my-2 flex-shrink-0" />
              )}
              <button
                onClick={() => setActiveTab(tab.key)}
                className={cn(
                  "flex items-center gap-1.5 px-4 py-3 text-sm font-medium whitespace-nowrap border-b-2 transition",
                  activeTab === tab.key
                    ? "border-clinic-blue text-clinic-blue"
                    : "border-transparent text-gray-500 hover:text-gray-900"
                )}
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
