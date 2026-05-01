"use client";
import { useEffect, useState, useCallback } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import {
  User, FileText, Stethoscope, Clock, Phone, MapPin, Pencil, Grid3x3,
  Calendar, Activity, Wallet, Pill, Plus, Scissors, Image as ImageIcon,
  Trash2, ExternalLink, Archive, RotateCcw, MessageSquare, FlaskConical,
  FolderOpen, FileCheck, Users, TrendingUp, AlertCircle, ChevronRight,
  BadgeCheck, Banknote, Receipt, Send,
} from "lucide-react";
import type { PatientProfile } from "@/types/patient";
import api from "@/lib/api";
import { cn, GENDER_LABELS, formatArabicDate, APPOINTMENT_STATUS_LABELS } from "@/lib/utils";
import { toast } from "@/stores/toastStore";
import { DentalChart } from "@/components/dental/DentalChart";
import { TreatmentHistory } from "@/components/dental/TreatmentHistory";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";
import { useAuthStore } from "@/stores/authStore";

// ─── Types ────────────────────────────────────────────────────────────────────
interface PatientSummary {
  totalAppointments: number;
  completedAppointments: number;
  activeOrthoCases: number;
  totalPaid: number;
  totalOutstanding: number;
  prescriptionsCount: number;
}
interface TimelineEvent { type: string; id: string; date: string; title: string; description: string; status?: string; }
interface AppointmentItem { id: string; appointmentDate: string; startTime: string; endTime: string; appointmentType: string; doctorName: string; status: string; notes?: string; }
interface ContractItem { id: string; specialty?: string; totalAmount: number; paidAmount: number; remainingAmount: number; status: string; startDate?: string; installmentsCount: number; }
interface PaymentItem { id: string; amount: number; paymentDate: string; paymentMethod?: string; serviceDescription?: string; receiptNumber?: string; specialty?: string; doctorName?: string; }
interface PrescriptionItem { id: string; createdAt: string; doctorName?: string; medications: { name: string; dosage?: string; instructions?: string }[]; notes?: string; }
interface ReferralItem { id: string; referralDate: string; referredToSpecialty: string; referredByDoctorName?: string; reason?: string; status: string; }
interface LabOrderItem { id: string; labName?: string; orderType: string; status: string; orderDate: string; dueDate?: string; notes?: string; doctorName?: string; }
interface OrthoCase { id: string; caseNumber: string; applianceType?: string; status: string; stagePercentage: number; doctorName?: string; startDate?: string; }
interface SurgeryCase { id: string; caseNumber: string; surgeryType: string; status: string; doctorName?: string; createdAt: string; }
interface GeneralTreatment { id: string; treatmentType: string; toothNumber?: string; materialUsed?: string; cost?: number; doctorName?: string; createdAt: string; }
interface ClinicalPhotoItem { id: string; category: string; photoType?: string; fileUrl: string; thumbnailUrl?: string; stage?: string; notes?: string; photoDate: string; }
interface RadiographItem { id: string; xrayType: string; fileUrl: string; toothRelated?: string; notes?: string; xrayDate: string; doctorName?: string; }

// ─── Constants ────────────────────────────────────────────────────────────────
const STATUS_COLORS: Record<string, string> = {
  Scheduled: "bg-blue-100 text-blue-700",
  Confirmed: "bg-teal-100 text-teal-700",
  Arrived: "bg-yellow-100 text-yellow-700",
  InProgress: "bg-purple-100 text-purple-700",
  Completed: "bg-green-100 text-green-700",
  Cancelled: "bg-gray-100 text-gray-500",
  NoShow: "bg-red-100 text-red-700",
};
const CONTRACT_STATUS: Record<string, string> = { active: "نشط", completed: "مكتمل", cancelled: "ملغى" };
const REFERRAL_STATUS: Record<string, string> = { pending: "معلقة", accepted: "مقبولة", completed: "مكتملة", cancelled: "ملغاة" };
const LAB_STATUS: Record<string, string> = { pending: "معلق", in_lab: "في المختبر", ready: "جاهز", delivered: "تسليم" };
const SURGERY_STATUS: Record<string, string> = { scheduled: "مجدولة", in_progress: "جارية", completed: "مكتملة", cancelled: "ملغاة" };
const ORTHO_STATUS: Record<string, string> = { active: "نشطة", completed: "مكتملة", cancelled: "ملغاة" };
const PAYMENT_METHODS: Record<string, string> = { cash: "نقدي", card: "بطاقة", bank_transfer: "تحويل", other: "أخرى" };

type Tab = "overview" | "info" | "medical" | "dental" | "appointments" | "visits" |
  "finance" | "contracts" | "payments" | "messages" | "ortho" | "general" |
  "surgery" | "photos" | "xrays" | "prescriptions" | "referrals" | "documents" | "lab" | "timeline";

const TABS: { key: Tab; label: string; icon: React.ElementType }[] = [
  { key: "overview",      label: "ملخص",              icon: User },
  { key: "info",          label: "البيانات",           icon: FileText },
  { key: "medical",       label: "التاريخ الطبي",     icon: AlertCircle },
  { key: "dental",        label: "التاريخ السني",     icon: Stethoscope },
  { key: "appointments",  label: "المواعيد",           icon: Calendar },
  { key: "visits",        label: "الجلسات",            icon: BadgeCheck },
  { key: "finance",       label: "المالية",            icon: Wallet },
  { key: "contracts",     label: "العقود",             icon: FileCheck },
  { key: "payments",      label: "الدفعات",            icon: Banknote },
  { key: "messages",      label: "الرسائل",            icon: MessageSquare },
  { key: "ortho",         label: "التقويم",            icon: Activity },
  { key: "general",       label: "طب الأسنان",         icon: Grid3x3 },
  { key: "surgery",       label: "الجراحة",            icon: Scissors },
  { key: "photos",        label: "الصور",              icon: ImageIcon },
  { key: "xrays",         label: "الأشعة",             icon: TrendingUp },
  { key: "prescriptions", label: "الوصفات",            icon: Pill },
  { key: "referrals",     label: "الإحالات",           icon: Send },
  { key: "documents",     label: "الوثائق",            icon: FolderOpen },
  { key: "lab",           label: "المختبر",            icon: FlaskConical },
  { key: "timeline",      label: "السجل الزمني",       icon: Clock },
];

// ─── Helpers ──────────────────────────────────────────────────────────────────
function EmptyState({ icon: Icon, title, subtitle, action }: {
  icon: React.ElementType; title: string; subtitle?: string;
  action?: { label: string; href?: string; onClick?: () => void };
}) {
  return (
    <div className="flex flex-col items-center justify-center py-14 text-center">
      <div className="w-14 h-14 rounded-2xl bg-gray-100 flex items-center justify-center mb-3">
        <Icon className="w-7 h-7 text-gray-300" />
      </div>
      <p className="font-semibold text-gray-500 text-sm">{title}</p>
      {subtitle && <p className="text-xs text-gray-400 mt-1 max-w-xs">{subtitle}</p>}
      {action && (
        action.href ? (
          <Link href={action.href}
            className="mt-4 inline-flex items-center gap-1.5 px-4 py-2 text-xs font-medium bg-clinic-teal text-white rounded-lg hover:opacity-90 transition"
          >
            <Plus className="w-3.5 h-3.5" />
            {action.label}
          </Link>
        ) : (
          <button onClick={action.onClick}
            className="mt-4 inline-flex items-center gap-1.5 px-4 py-2 text-xs font-medium bg-clinic-teal text-white rounded-lg hover:opacity-90 transition"
          >
            <Plus className="w-3.5 h-3.5" />
            {action.label}
          </button>
        )
      )}
    </div>
  );
}

function Skeleton({ rows = 4 }: { rows?: number }) {
  return (
    <div className="space-y-3 animate-pulse">
      {Array.from({ length: rows }).map((_, i) => (
        <div key={i} className="h-14 bg-gray-100 rounded-lg" />
      ))}
    </div>
  );
}

function StatusBadge({ label, className }: { label: string; className: string }) {
  return <span className={cn("text-xs px-2 py-0.5 rounded-full font-medium", className)}>{label}</span>;
}

// ─── Overview Tab ─────────────────────────────────────────────────────────────
function OverviewTab({ patient, summary }: { patient: PatientProfile; summary: PatientSummary | null }) {
  const stats = [
    { label: "المواعيد الكلية",    value: summary?.totalAppointments ?? "—",    icon: Calendar,   color: "text-blue-600",   bg: "bg-blue-50" },
    { label: "مواعيد مكتملة",     value: summary?.completedAppointments ?? "—", icon: BadgeCheck,  color: "text-green-600",  bg: "bg-green-50" },
    { label: "حالات تقويم نشطة",  value: summary?.activeOrthoCases ?? "—",     icon: Activity,   color: "text-purple-600", bg: "bg-purple-50" },
    { label: "إجمالي المدفوع",    value: summary ? `${summary.totalPaid.toLocaleString()} ر.ي` : "—", icon: Banknote, color: "text-teal-600", bg: "bg-teal-50" },
    { label: "المبلغ المتبقي",    value: summary ? `${summary.totalOutstanding.toLocaleString()} ر.ي` : "—", icon: Receipt, color: "text-orange-600", bg: "bg-orange-50" },
    { label: "الوصفات الطبية",    value: summary?.prescriptionsCount ?? "—",    icon: Pill,       color: "text-rose-600",   bg: "bg-rose-50" },
  ];

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
        {stats.map(({ label, value, icon: Icon, color, bg }) => (
          <div key={label} className={cn("rounded-xl p-4 flex items-center gap-3", bg)}>
            <div className={cn("w-9 h-9 rounded-lg flex items-center justify-center bg-white/60", color)}>
              <Icon className="w-4 h-4" />
            </div>
            <div>
              <p className="text-xs text-gray-500">{label}</p>
              <p className={cn("text-base font-bold", color)}>{value}</p>
            </div>
          </div>
        ))}
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div className="border border-gray-100 rounded-xl p-4">
          <p className="text-xs font-semibold text-gray-400 uppercase tracking-wide mb-3">بيانات التواصل</p>
          <div className="space-y-2.5">
            {patient.phone && (
              <div className="flex items-center gap-2 text-sm">
                <Phone className="w-3.5 h-3.5 text-gray-400 flex-shrink-0" />
                <span className="font-mono text-gray-700" dir="ltr">{patient.phone}</span>
              </div>
            )}
            {patient.address && (
              <div className="flex items-center gap-2 text-sm">
                <MapPin className="w-3.5 h-3.5 text-gray-400 flex-shrink-0" />
                <span className="text-gray-700">{patient.address}</span>
              </div>
            )}
            {patient.primaryDoctorName && (
              <div className="flex items-center gap-2 text-sm">
                <Users className="w-3.5 h-3.5 text-gray-400 flex-shrink-0" />
                <span className="text-gray-700">{patient.primaryDoctorName}</span>
              </div>
            )}
          </div>
        </div>

        <div className="border border-gray-100 rounded-xl p-4">
          <p className="text-xs font-semibold text-gray-400 uppercase tracking-wide mb-3">الحالة الصحية</p>
          <div className="space-y-2.5 text-sm">
            {patient.medicalHistory?.chronicDiseases && (
              <p><span className="text-gray-400">أمراض مزمنة: </span><span className="text-gray-700">{patient.medicalHistory.chronicDiseases}</span></p>
            )}
            {patient.medicalHistory?.drugAllergies && (
              <p><span className="text-gray-400">حساسية: </span><span className="text-red-600 font-medium">{patient.medicalHistory.drugAllergies}</span></p>
            )}
            {patient.dentalHistory?.chiefComplaint && (
              <p><span className="text-gray-400">الشكوى: </span><span className="text-gray-700">{patient.dentalHistory.chiefComplaint}</span></p>
            )}
            {!patient.medicalHistory?.chronicDiseases && !patient.medicalHistory?.drugAllergies && !patient.dentalHistory?.chiefComplaint && (
              <p className="text-gray-400 text-xs">لا توجد تنبيهات صحية مسجّلة</p>
            )}
          </div>
        </div>
      </div>

      <div className="flex flex-wrap gap-2">
        {[
          { label: "إضافة موعد", href: `/appointments/new?patientId=${patient.id}`, icon: Calendar },
          { label: "وصفة طبية", href: `/prescriptions/new?patientId=${patient.id}`, icon: Pill },
          { label: "إحالة", href: `/referrals/new?patientId=${patient.id}`, icon: Send },
          { label: "طلب مختبر", href: `/lab-orders/new?patientId=${patient.id}`, icon: FlaskConical },
        ].map(({ label, href, icon: Icon }) => (
          <Link key={label} href={href}
            className="inline-flex items-center gap-1.5 px-3 py-2 text-xs font-medium border border-gray-200 rounded-lg hover:bg-gray-50 hover:border-clinic-teal/40 transition text-gray-600"
          >
            <Icon className="w-3.5 h-3.5" />
            {label}
          </Link>
        ))}
      </div>
    </div>
  );
}

// ─── Appointments Tab ─────────────────────────────────────────────────────────
function AppointmentsTab({ patientId }: { patientId: string }) {
  const [items, setItems] = useState<AppointmentItem[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<AppointmentItem[]>(`/api/appointments/patient/${patientId}`)
      .then(r => setItems(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [patientId]);

  if (loading) return <Skeleton />;
  if (!items.length) return (
    <EmptyState icon={Calendar} title="لا توجد مواعيد بعد"
      action={{ label: "موعد جديد", href: `/appointments/new?patientId=${patientId}` }} />
  );

  return (
    <div className="space-y-2">
      {items.map(a => (
        <div key={a.id} className="flex items-center justify-between gap-3 p-3 rounded-lg border border-gray-100 hover:bg-gray-50 transition">
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2 flex-wrap">
              <span className="font-semibold text-sm text-gray-900">{a.appointmentType}</span>
              <StatusBadge
                label={APPOINTMENT_STATUS_LABELS[a.status] ?? a.status}
                className={STATUS_COLORS[a.status] ?? "bg-gray-100 text-gray-600"}
              />
            </div>
            <p className="text-xs text-gray-500 mt-0.5">
              {formatArabicDate(a.appointmentDate)} · {a.startTime}–{a.endTime} · {a.doctorName}
            </p>
            {a.notes && <p className="text-xs text-gray-400 mt-0.5 truncate">{a.notes}</p>}
          </div>
        </div>
      ))}
    </div>
  );
}

// ─── Visits / General Treatments Tab ─────────────────────────────────────────
function VisitsTab({ patientId }: { patientId: string }) {
  const [items, setItems] = useState<GeneralTreatment[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<GeneralTreatment[]>(`/api/general-treatments/${patientId}`)
      .then(r => setItems(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [patientId]);

  if (loading) return <Skeleton />;
  if (!items.length) return (
    <EmptyState icon={BadgeCheck} title="لا توجد جلسات علاجية مسجّلة"
      subtitle="جلسات طب الأسنان العام تظهر هنا بعد تسجيلها من قسم طب الأسنان." />
  );

  return (
    <div className="space-y-2">
      {items.map(t => (
        <div key={t.id} className="flex items-start justify-between gap-3 p-3 rounded-lg border border-gray-100 hover:bg-gray-50 transition">
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2">
              <span className="font-semibold text-sm text-gray-900">{t.treatmentType}</span>
              {t.toothNumber && <span className="text-xs bg-teal-50 text-teal-700 px-1.5 py-0.5 rounded">سن {t.toothNumber}</span>}
            </div>
            <p className="text-xs text-gray-500 mt-0.5">
              {t.doctorName ?? "—"} · {formatArabicDate(t.createdAt)}
              {t.materialUsed && ` · ${t.materialUsed}`}
            </p>
          </div>
          {t.cost != null && t.cost > 0 && (
            <span className="text-sm font-bold text-teal-700 flex-shrink-0">{t.cost.toLocaleString()} ر.ي</span>
          )}
        </div>
      ))}
    </div>
  );
}

// ─── Finance Overview Tab ─────────────────────────────────────────────────────
function FinanceTab({ patientId, summary }: { patientId: string; summary: PatientSummary | null }) {
  const [contracts, setContracts] = useState<ContractItem[]>([]);
  const [payments, setPayments] = useState<PaymentItem[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.allSettled([
      api.get<{ data: ContractItem[] }>(`/api/contracts?patientId=${patientId}&pageSize=5`),
      api.get<{ data: PaymentItem[] }>(`/api/payments?patientId=${patientId}&pageSize=5`),
    ]).then(([c, p]) => {
      if (c.status === "fulfilled") setContracts(c.value.data.data ?? []);
      if (p.status === "fulfilled") setPayments(p.value.data.data ?? []);
    }).finally(() => setLoading(false));
  }, [patientId]);

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-2 gap-3">
        {[
          { label: "إجمالي المدفوع", value: summary?.totalPaid ?? 0, color: "text-green-600", bg: "bg-green-50" },
          { label: "المبلغ المتبقي",  value: summary?.totalOutstanding ?? 0, color: "text-orange-600", bg: "bg-orange-50" },
        ].map(({ label, value, color, bg }) => (
          <div key={label} className={cn("rounded-xl p-4 text-center", bg)}>
            <p className="text-xs text-gray-500 mb-1">{label}</p>
            <p className={cn("text-xl font-bold", color)}>{value.toLocaleString()} <span className="text-sm font-normal">ر.ي</span></p>
          </div>
        ))}
      </div>

      {loading ? <Skeleton rows={3} /> : (
        <>
          <div>
            <div className="flex items-center justify-between mb-2">
              <p className="text-sm font-semibold text-gray-700">آخر العقود</p>
              <Link href={`/finance/contracts?patientId=${patientId}`} className="text-xs text-clinic-teal hover:underline">عرض الكل</Link>
            </div>
            {contracts.length === 0 ? (
              <p className="text-xs text-gray-400 py-3">لا توجد عقود</p>
            ) : contracts.map(c => (
              <div key={c.id} className="flex items-center justify-between py-2 border-b border-gray-50 last:border-0">
                <div>
                  <p className="text-sm text-gray-800">{c.specialty ?? "عقد علاج"}</p>
                  <p className="text-xs text-gray-400">{c.startDate ? formatArabicDate(c.startDate) : "—"}</p>
                </div>
                <div className="text-right">
                  <p className="text-sm font-bold text-gray-900">{c.totalAmount.toLocaleString()} ر.ي</p>
                  <StatusBadge label={CONTRACT_STATUS[c.status] ?? c.status} className={c.status === "active" ? "bg-blue-50 text-blue-700" : "bg-gray-100 text-gray-500"} />
                </div>
              </div>
            ))}
          </div>

          <div>
            <div className="flex items-center justify-between mb-2">
              <p className="text-sm font-semibold text-gray-700">آخر الدفعات</p>
              <Link href={`/finance/payments?patientId=${patientId}`} className="text-xs text-clinic-teal hover:underline">عرض الكل</Link>
            </div>
            {payments.length === 0 ? (
              <p className="text-xs text-gray-400 py-3">لا توجد دفعات</p>
            ) : payments.map(p => (
              <div key={p.id} className="flex items-center justify-between py-2 border-b border-gray-50 last:border-0">
                <div>
                  <p className="text-sm text-gray-800">{p.serviceDescription ?? "دفعة"}</p>
                  <p className="text-xs text-gray-400">{formatArabicDate(p.paymentDate)} · {PAYMENT_METHODS[p.paymentMethod ?? ""] ?? p.paymentMethod ?? "نقدي"}</p>
                </div>
                <p className="text-sm font-bold text-green-600">{p.amount.toLocaleString()} ر.ي</p>
              </div>
            ))}
          </div>
        </>
      )}
    </div>
  );
}

// ─── Contracts Tab ────────────────────────────────────────────────────────────
function ContractsTab({ patientId }: { patientId: string }) {
  const [items, setItems] = useState<ContractItem[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<{ data: ContractItem[] }>(`/api/contracts?patientId=${patientId}&pageSize=50`)
      .then(r => setItems(r.data.data ?? []))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [patientId]);

  if (loading) return <Skeleton />;
  if (!items.length) return (
    <EmptyState icon={FileCheck} title="لا توجد عقود مسجّلة"
      action={{ label: "عقد جديد", href: `/finance/contracts/new?patientId=${patientId}` }} />
  );

  return (
    <div className="space-y-3">
      {items.map(c => (
        <Link key={c.id} href={`/finance/contracts/${c.id}`}
          className="block p-4 border border-gray-100 rounded-xl hover:border-clinic-teal/30 hover:bg-teal-50/20 transition"
        >
          <div className="flex items-center justify-between mb-2">
            <p className="font-semibold text-gray-900">{c.specialty ?? "عقد علاج"}</p>
            <StatusBadge label={CONTRACT_STATUS[c.status] ?? c.status}
              className={c.status === "active" ? "bg-blue-50 text-blue-700" : c.status === "completed" ? "bg-green-50 text-green-700" : "bg-gray-100 text-gray-500"} />
          </div>
          <div className="flex items-center justify-between text-sm text-gray-600">
            <span>الإجمالي: <strong>{c.totalAmount.toLocaleString()} ر.ي</strong></span>
            <span>المدفوع: <strong className="text-green-600">{c.paidAmount.toLocaleString()} ر.ي</strong></span>
            <span>المتبقي: <strong className="text-orange-600">{c.remainingAmount.toLocaleString()} ر.ي</strong></span>
          </div>
          <div className="mt-2 h-1.5 bg-gray-100 rounded-full overflow-hidden">
            <div className="h-full bg-clinic-teal rounded-full transition-all"
              style={{ width: `${Math.min(100, (c.paidAmount / c.totalAmount) * 100)}%` }} />
          </div>
          <p className="text-xs text-gray-400 mt-1.5">{c.startDate ? formatArabicDate(c.startDate) : "—"} · {c.installmentsCount} قسط</p>
        </Link>
      ))}
    </div>
  );
}

// ─── Payments Tab ─────────────────────────────────────────────────────────────
function PaymentsTab({ patientId }: { patientId: string }) {
  const [items, setItems] = useState<PaymentItem[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<{ data: PaymentItem[] }>(`/api/payments?patientId=${patientId}&pageSize=100`)
      .then(r => setItems(r.data.data ?? []))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [patientId]);

  if (loading) return <Skeleton />;
  if (!items.length) return (
    <EmptyState icon={Banknote} title="لا توجد دفعات مسجّلة"
      action={{ label: "تسجيل دفعة", href: `/finance/payments/new?patientId=${patientId}` }} />
  );

  const total = items.reduce((s, p) => s + p.amount, 0);
  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between p-3 bg-teal-50 rounded-lg border border-teal-100">
        <span className="text-sm text-gray-600">إجمالي المدفوع</span>
        <span className="font-bold text-teal-700">{total.toLocaleString()} ر.ي</span>
      </div>
      {items.map(p => (
        <div key={p.id} className="flex items-center justify-between p-3 rounded-lg border border-gray-100 hover:bg-gray-50 transition">
          <div>
            <p className="text-sm font-medium text-gray-900">{p.serviceDescription ?? "دفعة"}</p>
            <p className="text-xs text-gray-400">
              {formatArabicDate(p.paymentDate)} · {PAYMENT_METHODS[p.paymentMethod ?? ""] ?? "نقدي"}
              {p.receiptNumber && ` · ${p.receiptNumber}`}
            </p>
          </div>
          <p className="text-sm font-bold text-green-600">{p.amount.toLocaleString()} ر.ي</p>
        </div>
      ))}
    </div>
  );
}

// ─── Messages Tab ─────────────────────────────────────────────────────────────
function MessagesTab({ patientId }: { patientId: string }) {
  return (
    <div className="flex flex-col items-center py-10 gap-4 text-center">
      <div className="w-14 h-14 rounded-2xl bg-teal-50 flex items-center justify-center">
        <MessageSquare className="w-7 h-7 text-clinic-teal" />
      </div>
      <div>
        <p className="font-semibold text-gray-800">المحادثة الداخلية</p>
        <p className="text-xs text-gray-400 mt-1 max-w-xs">
          المراسلة الداخلية تتيح للفريق التنسيق حول المريض دون مغادرة النظام.
        </p>
      </div>
      <Link
        href={`/messages?patientId=${patientId}`}
        className="inline-flex items-center gap-2 px-5 py-2.5 bg-clinic-teal text-white text-sm font-medium rounded-lg hover:opacity-90 transition"
      >
        <MessageSquare className="w-4 h-4" />
        فتح المحادثة
      </Link>
    </div>
  );
}

// ─── Orthodontics Tab ─────────────────────────────────────────────────────────
function OrthoTab({ patientId }: { patientId: string }) {
  const [items, setItems] = useState<OrthoCase[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<OrthoCase[]>(`/api/ortho-cases?patientId=${patientId}&pageSize=20`)
      .then(r => setItems(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [patientId]);

  if (loading) return <Skeleton rows={2} />;
  if (!items.length) return (
    <EmptyState icon={Activity} title="لا توجد حالات تقويمية"
      action={{ label: "حالة تقويمية جديدة", href: `/ortho/new?patientId=${patientId}` }} />
  );

  return (
    <div className="space-y-3">
      {items.map(c => (
        <Link key={c.id} href={`/ortho/${c.id}`}
          className="block p-4 border border-gray-100 rounded-xl hover:border-clinic-teal/30 hover:bg-teal-50/20 transition"
        >
          <div className="flex items-center justify-between mb-2">
            <div className="flex items-center gap-2">
              <span className="font-mono text-xs bg-teal-50 text-teal-700 px-2 py-0.5 rounded">{c.caseNumber}</span>
              {c.applianceType && <span className="text-sm text-gray-700">{c.applianceType}</span>}
            </div>
            <StatusBadge label={ORTHO_STATUS[c.status] ?? c.status}
              className={c.status === "active" ? "bg-teal-50 text-teal-700" : c.status === "completed" ? "bg-green-50 text-green-700" : "bg-gray-100 text-gray-500"} />
          </div>
          <div className="flex items-center gap-3">
            <div className="flex-1 h-2 bg-gray-100 rounded-full overflow-hidden">
              <div className="h-full bg-clinic-teal rounded-full" style={{ width: `${c.stagePercentage}%` }} />
            </div>
            <span className="text-xs text-gray-500 flex-shrink-0">{c.stagePercentage}%</span>
          </div>
          {c.doctorName && <p className="text-xs text-gray-400 mt-1.5">{c.doctorName}{c.startDate ? ` · ${formatArabicDate(c.startDate)}` : ""}</p>}
        </Link>
      ))}
    </div>
  );
}

// ─── General Dentistry Tab ────────────────────────────────────────────────────
function GeneralTab({ patientId }: { patientId: string }) {
  return (
    <div className="space-y-8">
      <DentalChart patientId={patientId} />
      <div className="border-t border-gray-100 pt-6">
        <TreatmentHistory patientId={patientId} />
      </div>
    </div>
  );
}

// ─── Surgery Tab ──────────────────────────────────────────────────────────────
function SurgeryTab({ patientId }: { patientId: string }) {
  const [items, setItems] = useState<SurgeryCase[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<{ data: SurgeryCase[] }>(`/api/surgery-cases?patientId=${patientId}&pageSize=20`)
      .then(r => setItems(r.data.data ?? []))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [patientId]);

  if (loading) return <Skeleton rows={2} />;
  if (!items.length) return (
    <EmptyState icon={Scissors} title="لا توجد حالات جراحية"
      action={{ label: "حالة جراحية جديدة", href: `/surgery/new?patientId=${patientId}` }} />
  );

  return (
    <div className="space-y-3">
      {items.map(c => (
        <Link key={c.id} href={`/surgery/${c.id}`}
          className="flex items-center justify-between p-4 border border-gray-100 rounded-xl hover:border-red-100 hover:bg-red-50/20 transition"
        >
          <div>
            <div className="flex items-center gap-2">
              <span className="font-mono text-xs bg-red-50 text-red-700 px-2 py-0.5 rounded">{c.caseNumber}</span>
              <span className="text-sm font-medium text-gray-900">{c.surgeryType}</span>
            </div>
            <p className="text-xs text-gray-400 mt-1">{c.doctorName ?? "—"} · {formatArabicDate(c.createdAt)}</p>
          </div>
          <div className="flex items-center gap-2">
            <StatusBadge label={SURGERY_STATUS[c.status] ?? c.status}
              className={c.status === "completed" ? "bg-green-50 text-green-700" : c.status === "in_progress" ? "bg-blue-50 text-blue-700" : "bg-gray-100 text-gray-500"} />
            <ChevronRight className="w-4 h-4 text-gray-300" />
          </div>
        </Link>
      ))}
    </div>
  );
}

// ─── Photos Tab ───────────────────────────────────────────────────────────────
function PhotosTab({ patientId }: { patientId: string }) {
  const [photos, setPhotos] = useState<ClinicalPhotoItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [photoFile, setPhotoFile] = useState<File | null>(null);
  const [photoCategory, setPhotoCategory] = useState("intraoral");
  const [photoNotes, setPhotoNotes] = useState("");
  const [photoPreview, setPhotoPreview] = useState<string | null>(null);
  const [adding, setAdding] = useState(false);

  const fetchPhotos = useCallback(() => {
    setLoading(true);
    api.get<ClinicalPhotoItem[]>(`/api/clinical-photos/${patientId}`)
      .then(r => setPhotos(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [patientId]);

  useEffect(() => { fetchPhotos(); }, [fetchPhotos]);

  const uploadFile = async (file: File) => {
    const token = sessionStorage.getItem("accessToken") ?? "";
    const formData = new FormData();
    formData.append("file", file);
    const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL ?? ""}/api/uploads`, {
      method: "POST", headers: { Authorization: `Bearer ${token}` }, body: formData,
    });
    if (!res.ok) throw new Error("فشل رفع الملف");
    return ((await res.json()) as { url: string }).url;
  };

  const handleAdd = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!photoFile) return;
    setAdding(true);
    try {
      const fileUrl = await uploadFile(photoFile);
      await api.post("/api/clinical-photos", { patientId, fileUrl, category: photoCategory, notes: photoNotes || undefined });
      setPhotoFile(null); setPhotoPreview(null); setPhotoNotes("");
      fetchPhotos();
      toast.success("تم رفع الصورة");
    } catch { toast.error("فشل رفع الصورة"); } finally { setAdding(false); }
  };

  const CATEGORY_LABELS: Record<string, string> = { intraoral: "داخل الفم", extraoral: "خارج الفم" };

  return (
    <div className="space-y-6">
      <form onSubmit={handleAdd} className="bg-gray-50 rounded-xl p-4 border border-gray-100 space-y-3">
        <p className="text-xs font-semibold text-gray-500">إضافة صورة سريرية</p>
        <div className="flex gap-3 flex-wrap">
          <label className={cn("flex items-center gap-2 flex-1 min-w-40 border-2 border-dashed rounded-lg px-3 py-2.5 cursor-pointer transition text-sm",
            photoFile ? "border-clinic-teal bg-teal-50" : "border-gray-200 hover:border-gray-300")}>
            <input type="file" accept="image/*" className="sr-only"
              onChange={e => {
                const f = e.target.files?.[0] ?? null;
                setPhotoFile(f);
                if (f?.type.startsWith("image/")) {
                  const r = new FileReader(); r.onload = ev => setPhotoPreview(ev.target?.result as string); r.readAsDataURL(f);
                } else setPhotoPreview(null);
              }} />
            {photoPreview
              // eslint-disable-next-line @next/next/no-img-element
              ? <img src={photoPreview} alt="" className="w-8 h-8 rounded object-cover" />
              : <ImageIcon className="w-4 h-4 text-gray-400" />}
            <span className="text-gray-600 truncate">{photoFile ? photoFile.name : "اختر صورة"}</span>
          </label>
          <select value={photoCategory} onChange={e => setPhotoCategory(e.target.value)}
            className="border border-gray-200 rounded-lg px-3 py-2 text-sm bg-white">
            {Object.entries(CATEGORY_LABELS).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </select>
          <input value={photoNotes} onChange={e => setPhotoNotes(e.target.value)} placeholder="ملاحظات"
            className="flex-1 min-w-32 border border-gray-200 rounded-lg px-3 py-2 text-sm" />
          <button type="submit" disabled={!photoFile || adding}
            className="px-4 py-2 bg-clinic-teal text-white text-sm rounded-lg disabled:opacity-50 hover:opacity-90 transition">
            {adding ? "جاري الرفع..." : "رفع"}
          </button>
        </div>
      </form>

      {loading ? <Skeleton rows={2} /> : photos.length === 0 ? (
        <EmptyState icon={ImageIcon} title="لا توجد صور سريرية بعد" />
      ) : (
        <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
          {photos.map(p => (
            <div key={p.id} className="group relative rounded-xl overflow-hidden border border-gray-100">
              <a href={p.fileUrl} target="_blank" rel="noreferrer">
                {/* eslint-disable-next-line @next/next/no-img-element */}
                <img src={p.thumbnailUrl ?? p.fileUrl} alt="" className="w-full h-32 object-cover" />
              </a>
              <div className="p-2">
                <p className="text-xs text-gray-600">{CATEGORY_LABELS[p.category] ?? p.category}</p>
                <p className="text-xs text-gray-400">{formatArabicDate(p.photoDate)}</p>
              </div>
              <button onClick={() => api.delete(`/api/clinical-photos/${p.id}`).then(fetchPhotos).catch(() => {})}
                className="absolute top-2 left-2 w-6 h-6 bg-red-500 text-white rounded-full hidden group-hover:flex items-center justify-center">
                <Trash2 className="w-3 h-3" />
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

// ─── Radiographs Tab ──────────────────────────────────────────────────────────
function XraysTab({ patientId }: { patientId: string }) {
  const [xrays, setXrays] = useState<RadiographItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [xrayFile, setXrayFile] = useState<File | null>(null);
  const [xrayType, setXrayType] = useState("OPG");
  const [xrayNotes, setXrayNotes] = useState("");
  const [adding, setAdding] = useState(false);

  const fetchXrays = useCallback(() => {
    setLoading(true);
    api.get<RadiographItem[]>(`/api/radiographs/${patientId}`)
      .then(r => setXrays(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [patientId]);

  useEffect(() => { fetchXrays(); }, [fetchXrays]);

  const uploadFile = async (file: File) => {
    const token = sessionStorage.getItem("accessToken") ?? "";
    const formData = new FormData();
    formData.append("file", file);
    const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL ?? ""}/api/uploads`, {
      method: "POST", headers: { Authorization: `Bearer ${token}` }, body: formData,
    });
    if (!res.ok) throw new Error("فشل رفع الملف");
    return ((await res.json()) as { url: string }).url;
  };

  const handleAdd = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!xrayFile) return;
    setAdding(true);
    try {
      const fileUrl = await uploadFile(xrayFile);
      await api.post("/api/radiographs", { patientId, fileUrl, xrayType, notes: xrayNotes || undefined });
      setXrayFile(null); setXrayNotes("");
      fetchXrays();
      toast.success("تم رفع الأشعة");
    } catch { toast.error("فشل رفع الأشعة"); } finally { setAdding(false); }
  };

  const XRAY_TYPES = ["OPG", "lateral_ceph", "bitewing", "periapical", "PA_ceph", "CBCT"];

  return (
    <div className="space-y-6">
      <form onSubmit={handleAdd} className="bg-gray-50 rounded-xl p-4 border border-gray-100 space-y-3">
        <p className="text-xs font-semibold text-gray-500">إضافة أشعة</p>
        <div className="flex gap-3 flex-wrap">
          <label className={cn("flex items-center gap-2 flex-1 min-w-40 border-2 border-dashed rounded-lg px-3 py-2.5 cursor-pointer transition text-sm",
            xrayFile ? "border-clinic-teal bg-teal-50" : "border-gray-200 hover:border-gray-300")}>
            <input type="file" accept="image/*,.pdf,.dcm" className="sr-only" onChange={e => setXrayFile(e.target.files?.[0] ?? null)} />
            <TrendingUp className="w-4 h-4 text-gray-400" />
            <span className="text-gray-600 truncate">{xrayFile ? xrayFile.name : "اختر ملف الأشعة"}</span>
          </label>
          <select value={xrayType} onChange={e => setXrayType(e.target.value)}
            className="border border-gray-200 rounded-lg px-3 py-2 text-sm bg-white">
            {XRAY_TYPES.map(t => <option key={t} value={t}>{t}</option>)}
          </select>
          <input value={xrayNotes} onChange={e => setXrayNotes(e.target.value)} placeholder="ملاحظات"
            className="flex-1 min-w-32 border border-gray-200 rounded-lg px-3 py-2 text-sm" />
          <button type="submit" disabled={!xrayFile || adding}
            className="px-4 py-2 bg-clinic-teal text-white text-sm rounded-lg disabled:opacity-50 hover:opacity-90 transition">
            {adding ? "جاري الرفع..." : "رفع"}
          </button>
        </div>
      </form>

      {loading ? <Skeleton rows={2} /> : xrays.length === 0 ? (
        <EmptyState icon={TrendingUp} title="لا توجد أشعة مسجّلة بعد" />
      ) : (
        <div className="space-y-2">
          {xrays.map(x => (
            <div key={x.id} className="flex items-center justify-between p-3 rounded-lg border border-gray-100 hover:bg-gray-50 transition">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 bg-gray-100 rounded-lg flex items-center justify-center flex-shrink-0">
                  <TrendingUp className="w-4 h-4 text-gray-400" />
                </div>
                <div>
                  <p className="text-sm font-medium text-gray-900">{x.xrayType}</p>
                  <p className="text-xs text-gray-400">{formatArabicDate(x.xrayDate)}{x.doctorName ? ` · ${x.doctorName}` : ""}</p>
                </div>
              </div>
              <div className="flex items-center gap-2">
                <a href={x.fileUrl} target="_blank" rel="noreferrer"
                  className="text-xs text-clinic-teal hover:underline flex items-center gap-1">
                  <ExternalLink className="w-3 h-3" /> عرض
                </a>
                <button onClick={() => api.delete(`/api/radiographs/${x.id}`).then(fetchXrays).catch(() => {})}
                  className="p-1 text-gray-300 hover:text-red-500 transition">
                  <Trash2 className="w-3.5 h-3.5" />
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

// ─── Prescriptions Tab ────────────────────────────────────────────────────────
function PrescriptionsTab({ patientId }: { patientId: string }) {
  const [items, setItems] = useState<PrescriptionItem[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<{ data: PrescriptionItem[] }>(`/api/prescriptions?patientId=${patientId}&pageSize=50`)
      .then(r => setItems(r.data.data ?? []))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [patientId]);

  if (loading) return <Skeleton />;
  if (!items.length) return (
    <EmptyState icon={Pill} title="لا توجد وصفات طبية"
      action={{ label: "وصفة جديدة", href: `/prescriptions/new?patientId=${patientId}` }} />
  );

  return (
    <div className="space-y-3">
      {items.map(p => (
        <Link key={p.id} href={`/prescriptions/${p.id}`}
          className="block p-4 border border-gray-100 rounded-xl hover:bg-gray-50 transition">
          <div className="flex items-center justify-between mb-2">
            <p className="text-xs text-gray-400">{formatArabicDate(p.createdAt)}{p.doctorName ? ` · ${p.doctorName}` : ""}</p>
            <ExternalLink className="w-3.5 h-3.5 text-gray-300" />
          </div>
          <div className="space-y-1">
            {p.medications?.slice(0, 3).map((m, i) => (
              <p key={i} className="text-sm text-gray-800">
                <span className="font-medium">{m.name}</span>
                {m.dosage && <span className="text-gray-500"> · {m.dosage}</span>}
              </p>
            ))}
            {(p.medications?.length ?? 0) > 3 && (
              <p className="text-xs text-gray-400">+{p.medications.length - 3} أدوية أخرى</p>
            )}
          </div>
          {p.notes && <p className="text-xs text-gray-400 mt-2 border-t border-gray-50 pt-2">{p.notes}</p>}
        </Link>
      ))}
    </div>
  );
}

// ─── Referrals Tab ────────────────────────────────────────────────────────────
function ReferralsTab({ patientId }: { patientId: string }) {
  const [items, setItems] = useState<ReferralItem[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<{ data: ReferralItem[] }>(`/api/referrals?patientId=${patientId}&pageSize=50`)
      .then(r => setItems(r.data.data ?? []))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [patientId]);

  if (loading) return <Skeleton />;
  if (!items.length) return (
    <EmptyState icon={Send} title="لا توجد إحالات"
      action={{ label: "إحالة جديدة", href: `/referrals/new?patientId=${patientId}` }} />
  );

  return (
    <div className="space-y-2">
      {items.map(r => (
        <div key={r.id} className="flex items-center justify-between p-3 rounded-lg border border-gray-100 hover:bg-gray-50 transition">
          <div>
            <div className="flex items-center gap-2">
              <span className="text-sm font-medium text-gray-900">{r.referredToSpecialty}</span>
              <StatusBadge label={REFERRAL_STATUS[r.status] ?? r.status}
                className={r.status === "completed" ? "bg-green-50 text-green-700" : r.status === "accepted" ? "bg-blue-50 text-blue-700" : "bg-gray-100 text-gray-500"} />
            </div>
            <p className="text-xs text-gray-400 mt-0.5">
              {formatArabicDate(r.referralDate)}{r.referredByDoctorName ? ` · ${r.referredByDoctorName}` : ""}
            </p>
            {r.reason && <p className="text-xs text-gray-500 mt-0.5">{r.reason}</p>}
          </div>
        </div>
      ))}
    </div>
  );
}

// ─── Documents Tab (empty — no API yet) ──────────────────────────────────────
function DocumentsTab() {
  return (
    <EmptyState
      icon={FolderOpen}
      title="وحدة الوثائق قيد التطوير"
      subtitle="ستتيح هذه الوحدة رفع وإدارة الوثائق والموافقات والنماذج الخاصة بالمريض."
    />
  );
}

// ─── Lab Orders Tab ───────────────────────────────────────────────────────────
function LabOrdersTab({ patientId }: { patientId: string }) {
  const [items, setItems] = useState<LabOrderItem[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<{ data: LabOrderItem[] }>(`/api/lab-orders?patientId=${patientId}&pageSize=50`)
      .then(r => setItems(r.data.data ?? []))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [patientId]);

  if (loading) return <Skeleton />;
  if (!items.length) return (
    <EmptyState icon={FlaskConical} title="لا توجد طلبات مختبر"
      action={{ label: "طلب مختبر جديد", href: `/lab-orders/new?patientId=${patientId}` }} />
  );

  return (
    <div className="space-y-2">
      {items.map(l => (
        <div key={l.id} className="flex items-center justify-between p-3 rounded-lg border border-gray-100 hover:bg-gray-50 transition">
          <div>
            <div className="flex items-center gap-2">
              <span className="text-sm font-medium text-gray-900">{l.orderType}</span>
              <StatusBadge label={LAB_STATUS[l.status] ?? l.status}
                className={l.status === "delivered" ? "bg-green-50 text-green-700" : l.status === "ready" ? "bg-teal-50 text-teal-700" : "bg-gray-100 text-gray-500"} />
            </div>
            <p className="text-xs text-gray-400 mt-0.5">
              {l.labName ?? "—"} · {formatArabicDate(l.orderDate)}
              {l.dueDate ? ` · موعد التسليم: ${formatArabicDate(l.dueDate)}` : ""}
            </p>
          </div>
        </div>
      ))}
    </div>
  );
}

// ─── Timeline Tab ─────────────────────────────────────────────────────────────
function TimelineTab({ patientId }: { patientId: string }) {
  const [events, setEvents] = useState<TimelineEvent[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<TimelineEvent[]>(`/api/patients/${patientId}/timeline`)
      .then(r => setEvents(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [patientId]);

  if (loading) return <Skeleton />;
  if (!events.length) return (
    <EmptyState icon={Clock} title="لا يوجد سجل زمني بعد"
      subtitle="سيظهر هنا تاريخ المواعيد والجلسات والمدفوعات مرتّباً زمنياً." />
  );

  return (
    <div className="relative">
      <div className="absolute right-[19px] top-0 bottom-0 w-0.5 bg-gray-100" />
      <div className="space-y-4">
        {events.map(ev => (
          <div key={ev.id} className="flex gap-4 relative">
            <div className="w-10 h-10 rounded-full bg-white border-2 border-clinic-teal flex items-center justify-center flex-shrink-0 z-10">
              <Clock className="w-4 h-4 text-clinic-teal" />
            </div>
            <div className="flex-1 bg-gray-50 rounded-lg p-3 border border-gray-100">
              <div className="flex items-center justify-between gap-2 flex-wrap">
                <span className="font-semibold text-sm text-gray-900">{ev.title}</span>
                {ev.status && (
                  <StatusBadge
                    label={APPOINTMENT_STATUS_LABELS[ev.status] ?? ev.status}
                    className={STATUS_COLORS[ev.status] ?? "bg-gray-100 text-gray-600"}
                  />
                )}
              </div>
              <p className="text-xs text-gray-500 mt-0.5">{ev.description}</p>
              <p className="text-xs text-gray-400 mt-1">{formatArabicDate(ev.date)}</p>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

// ─── Main Patient Profile Page ────────────────────────────────────────────────
export default function PatientProfilePage() {
  const { id } = useParams<{ id: string }>();
  const { user } = useAuthStore();
  const isAdmin = user?.role?.toLowerCase() === "admin";

  const [patient, setPatient] = useState<PatientProfile | null>(null);
  const [summary, setSummary]  = useState<PatientSummary | null>(null);
  const [loading, setLoading]  = useState(true);
  const [activeTab, setActiveTab] = useState<Tab>("overview");
  const [confirmAction, setConfirmAction] = useState<"archive" | "restore" | null>(null);

  const fetchPatient = useCallback(() => {
    api.get<PatientProfile>(`/api/patients/${id}`)
      .then(r => setPatient(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [id]);

  useEffect(() => {
    fetchPatient();
    api.get<PatientSummary>(`/api/patients/${id}/summary`)
      .then(r => setSummary(r.data))
      .catch(() => {});
  }, [id, fetchPatient]);

  const handleArchive = async () => {
    try {
      await api.put(`/api/patients/${id}/archive`);
      toast.success("تم أرشفة المريض");
      fetchPatient();
    } catch { toast.error("فشل أرشفة المريض"); }
    setConfirmAction(null);
  };

  const handleRestore = async () => {
    try {
      await api.put(`/api/patients/${id}/restore`);
      toast.success("تم استعادة المريض");
      fetchPatient();
    } catch { toast.error("فشل استعادة المريض"); }
    setConfirmAction(null);
  };

  if (loading) {
    return (
      <div className="space-y-4 animate-pulse max-w-5xl">
        <div className="h-32 bg-gray-100 rounded-xl" />
        <div className="h-10 bg-gray-100 rounded-xl" />
        <div className="h-64 bg-gray-100 rounded-xl" />
      </div>
    );
  }

  if (!patient) {
    return <div className="text-center py-20 text-gray-400">المريض غير موجود</div>;
  }

  return (
    <div className="space-y-4 max-w-5xl" dir="rtl">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Link href="/patients" className="hover:text-clinic-teal transition">المرضى</Link>
        <span>/</span>
        <span className="text-gray-900 font-medium">{patient.firstName} {patient.lastName}</span>
      </div>

      {/* Banner */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
        <div className="flex items-start justify-between gap-4 flex-wrap">
          <div className="flex items-start gap-4">
            <div className="w-14 h-14 clinic-gradient rounded-2xl flex items-center justify-center text-white text-xl font-extrabold flex-shrink-0">
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
                <StatusBadge
                  label={patient.isActive ? "نشط" : "مؤرشف"}
                  className={patient.isActive ? "bg-green-100 text-green-700" : "bg-red-100 text-red-600"}
                />
              </div>
              <div className="mt-2 flex flex-wrap items-center gap-4 text-sm text-gray-500">
                {patient.gender && (
                  <span className="flex items-center gap-1">
                    <User className="w-3.5 h-3.5" />
                    {GENDER_LABELS[patient.gender]}
                    {patient.age ? ` · ${patient.age} سنة` : ""}
                  </span>
                )}
                {patient.phone && (
                  <span className="flex items-center gap-1" dir="ltr">
                    <Phone className="w-3.5 h-3.5" />{patient.phone}
                  </span>
                )}
                {patient.primaryDoctorName && (
                  <span className="flex items-center gap-1">
                    <Users className="w-3.5 h-3.5" />{patient.primaryDoctorName}
                  </span>
                )}
              </div>
            </div>
          </div>

          {/* Action buttons */}
          <div className="flex items-center gap-2 flex-shrink-0 flex-wrap">
            {patient.isActive && (
              <Link href={`/patients/${id}/edit`}
                className="flex items-center gap-1.5 px-3 py-1.5 text-sm border border-gray-200 rounded-lg hover:bg-gray-50 transition text-gray-600">
                <Pencil className="w-3.5 h-3.5" /> تعديل
              </Link>
            )}
            <Link href={`/messages?patientId=${id}`}
              className="flex items-center gap-1.5 px-3 py-1.5 text-sm border border-clinic-teal/30 rounded-lg hover:bg-clinic-teal/5 transition text-clinic-teal">
              <MessageSquare className="w-3.5 h-3.5" /> رسالة
            </Link>
            {isAdmin && patient.isActive && (
              <button onClick={() => setConfirmAction("archive")}
                className="flex items-center gap-1.5 px-3 py-1.5 text-sm border border-red-200 rounded-lg hover:bg-red-50 transition text-red-600">
                <Archive className="w-3.5 h-3.5" /> أرشفة
              </button>
            )}
            {isAdmin && !patient.isActive && (
              <button onClick={() => setConfirmAction("restore")}
                className="flex items-center gap-1.5 px-3 py-1.5 text-sm border border-green-200 rounded-lg hover:bg-green-50 transition text-green-700">
                <RotateCcw className="w-3.5 h-3.5" /> استعادة
              </button>
            )}
          </div>
        </div>

        {/* Stats row */}
        <div className="mt-4 pt-4 border-t border-gray-50 grid grid-cols-3 sm:grid-cols-6 gap-2">
          {[
            { label: "المواعيد",    value: summary?.totalAppointments ?? "—",    color: "text-blue-600" },
            { label: "مكتملة",     value: summary?.completedAppointments ?? "—", color: "text-green-600" },
            { label: "التقويم",    value: summary?.activeOrthoCases ?? "—",     color: "text-purple-600" },
            { label: "مدفوع",      value: summary ? `${summary.totalPaid.toLocaleString()}` : "—", color: "text-teal-600" },
            { label: "متبقي",      value: summary ? `${summary.totalOutstanding.toLocaleString()}` : "—", color: "text-orange-600" },
            { label: "الوصفات",    value: summary?.prescriptionsCount ?? "—",    color: "text-rose-600" },
          ].map(({ label, value, color }) => (
            <div key={label} className="text-center">
              <p className={cn("text-base font-bold", color)}>{value}</p>
              <p className="text-xs text-gray-400">{label}</p>
            </div>
          ))}
        </div>
      </div>

      {/* Tabs card */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
        {/* Tab bar — horizontally scrollable */}
        <div className="border-b border-gray-100 overflow-x-auto">
          <div className="flex min-w-max">
            {TABS.map(({ key, label, icon: Icon }) => (
              <button
                key={key}
                onClick={() => setActiveTab(key)}
                className={cn(
                  "flex items-center gap-1.5 px-3 py-3 text-xs font-medium border-b-2 whitespace-nowrap transition",
                  activeTab === key
                    ? "border-clinic-teal text-clinic-teal"
                    : "border-transparent text-gray-500 hover:text-gray-900 hover:bg-gray-50"
                )}
              >
                <Icon className="w-3.5 h-3.5 flex-shrink-0" />
                {label}
              </button>
            ))}
          </div>
        </div>

        {/* Tab content */}
        <div className="p-5">
          {activeTab === "overview" && <OverviewTab patient={patient} summary={summary} />}

          {activeTab === "info" && (
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {([
                ["الاسم الكامل",    `${patient.firstName} ${patient.middleName ?? ""} ${patient.lastName}`.trim()],
                ["رقم المريض",      patient.patientNumber],
                ["الجنس",           GENDER_LABELS[patient.gender ?? ""] ?? "—"],
                ["العمر",           patient.age ? `${patient.age} سنة` : "—"],
                ["تاريخ الميلاد",   patient.dateOfBirth ?? "—"],
                ["الهاتف",          patient.phone ?? "—"],
                ["واتساب",          patient.whatsApp ?? "—"],
                ["العنوان",         patient.address ?? "—"],
                ["المهنة",          patient.occupation ?? "—"],
                ["مصدر الإحالة",   patient.referralSource ?? "—"],
                ["الطبيب المعالج",  patient.primaryDoctorName ?? "—"],
                ["الفرع",           patient.branchName ?? "—"],
              ] as [string, string][]).map(([label, value]) => (
                <div key={label} className="border-b border-gray-50 pb-3 last:border-0">
                  <p className="text-xs text-gray-400 mb-0.5">{label}</p>
                  <p className="text-sm font-medium text-gray-900">{value}</p>
                </div>
              ))}
            </div>
          )}

          {activeTab === "medical" && (
            <div className="space-y-3">
              {!patient.medicalHistory ? (
                <EmptyState icon={AlertCircle} title="لا يوجد تاريخ طبي مسجّل" />
              ) : (
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  {([
                    ["الأمراض المزمنة",    patient.medicalHistory.chronicDiseases],
                    ["الأدوية الحالية",    patient.medicalHistory.currentMedications],
                    ["حساسية الأدوية",     patient.medicalHistory.drugAllergies],
                    ["اضطرابات النزيف",    patient.medicalHistory.bleedingDisorders ? "نعم" : "لا"],
                    ["الحمل",             patient.medicalHistory.isPregnant === "yes" ? "نعم" : patient.medicalHistory.isPregnant === "no" ? "لا" : "لا ينطبق"],
                    ["مشاكل TMJ",         patient.medicalHistory.tmjProblems ? "نعم" : "لا"],
                    ["العمليات السابقة",   patient.medicalHistory.previousSurgeries],
                    ["ملاحظات",           patient.medicalHistory.notes],
                  ] as [string, string | undefined][]).map(([label, value]) => (
                    <div key={label} className="border-b border-gray-50 pb-3">
                      <p className="text-xs text-gray-400 mb-0.5">{label}</p>
                      <p className="text-sm font-medium text-gray-900">{value ?? "—"}</p>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}

          {activeTab === "dental" && (
            <div className="space-y-3">
              {!patient.dentalHistory ? (
                <EmptyState icon={Stethoscope} title="لا يوجد تاريخ سني مسجّل" />
              ) : (
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  {([
                    ["الشكوى الرئيسية",   patient.dentalHistory.chiefComplaint],
                    ["العلاجات السابقة",   patient.dentalHistory.previousTreatments],
                    ["التنفس الفموي",      patient.dentalHistory.mouthBreathing ? "نعم" : "لا"],
                    ["صرير الأسنان",      patient.dentalHistory.bruxism ? "نعم" : "لا"],
                    ["مص الإبهام",        patient.dentalHistory.thumbSucking ? "نعم" : "لا"],
                    ["وضع اللسان الخاطئ", patient.dentalHistory.tongueThrusing ? "نعم" : "لا"],
                    ["ملاحظات",           patient.dentalHistory.notes],
                  ] as [string, string | undefined][]).map(([label, value]) => (
                    <div key={label} className="border-b border-gray-50 pb-3">
                      <p className="text-xs text-gray-400 mb-0.5">{label}</p>
                      <p className="text-sm font-medium text-gray-900">{value ?? "—"}</p>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}

          {activeTab === "appointments"  && <AppointmentsTab  patientId={id} />}
          {activeTab === "visits"        && <VisitsTab        patientId={id} />}
          {activeTab === "finance"       && <FinanceTab       patientId={id} summary={summary} />}
          {activeTab === "contracts"     && <ContractsTab     patientId={id} />}
          {activeTab === "payments"      && <PaymentsTab      patientId={id} />}
          {activeTab === "messages"      && <MessagesTab      patientId={id} />}
          {activeTab === "ortho"         && <OrthoTab         patientId={id} />}
          {activeTab === "general"       && <GeneralTab       patientId={id} />}
          {activeTab === "surgery"       && <SurgeryTab       patientId={id} />}
          {activeTab === "photos"        && <PhotosTab        patientId={id} />}
          {activeTab === "xrays"         && <XraysTab         patientId={id} />}
          {activeTab === "prescriptions" && <PrescriptionsTab patientId={id} />}
          {activeTab === "referrals"     && <ReferralsTab     patientId={id} />}
          {activeTab === "documents"     && <DocumentsTab />}
          {activeTab === "lab"           && <LabOrdersTab     patientId={id} />}
          {activeTab === "timeline"      && <TimelineTab      patientId={id} />}
        </div>
      </div>

      {/* Archive/Restore confirmation */}
      <ConfirmDialog
        open={confirmAction !== null}
        title={confirmAction === "archive" ? "أرشفة المريض" : "استعادة المريض"}
        message={
          confirmAction === "archive"
            ? `هل تريد أرشفة المريض "${patient.firstName} ${patient.lastName}"؟ لن يظهر في القوائم العادية لكن يمكن استعادته لاحقاً.`
            : `هل تريد استعادة المريض "${patient.firstName} ${patient.lastName}" وإعادة تفعيله؟`
        }
        confirmLabel={confirmAction === "archive" ? "أرشفة" : "استعادة"}
        variant={confirmAction === "archive" ? "danger" : "warning"}
        onConfirm={confirmAction === "archive" ? handleArchive : handleRestore}
        onCancel={() => setConfirmAction(null)}
      />
    </div>
  );
}
