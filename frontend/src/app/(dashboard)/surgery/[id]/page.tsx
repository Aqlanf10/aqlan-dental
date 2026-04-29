"use client";
import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import {
  Scissors, User, ArrowRight, CheckCircle, Clock, XCircle,
  PlayCircle, ClipboardList, FileText, Save, AlertCircle,
  Stethoscope, Building2, Plus, X, ShieldCheck,
} from "lucide-react";
import api from "@/lib/api";
import { cn, formatArabicDate } from "@/lib/utils";
import type { OperativeReport, HospitalReferral } from "@/types/surgery";

interface SurgeryCase {
  id: string;
  caseNumber: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  doctorName?: string;
  doctorColor?: string;
  surgeryType: string;
  teethInvolved?: string;
  status: string;
  createdAt: string;
}

interface PreopReport {
  surgeryDate?: string;
  surgeryLocation?: string;
  anesthesiaType?: string;
  consentSigned?: boolean;
  doctorId?: string;
  doctorName?: string;
}

interface PostopRecord {
  instructions?: string;
}

interface Doctor {
  id: string;
  name: string;
}

const STATUS_LABELS: Record<string, string> = {
  scheduled:   "مجدولة",
  in_progress: "جارية",
  completed:   "مكتملة",
  cancelled:   "ملغاة",
  postponed:   "مؤجلة",
};

const STATUS_COLORS: Record<string, string> = {
  scheduled:   "bg-blue-50 text-blue-700",
  in_progress: "bg-yellow-50 text-yellow-700",
  completed:   "bg-green-50 text-green-700",
  cancelled:   "bg-gray-100 text-gray-500",
  postponed:   "bg-orange-50 text-orange-700",
};

const NEXT_STATUSES: Record<
  string,
  { status: string; label: string; icon: typeof PlayCircle; color: string }[]
> = {
  scheduled: [
    { status: "in_progress", label: "بدء الجراحة",    icon: PlayCircle,  color: "bg-yellow-500 hover:bg-yellow-600 text-white" },
    { status: "postponed",   label: "تأجيل",           icon: Clock,       color: "bg-orange-500 hover:bg-orange-600 text-white" },
    { status: "cancelled",   label: "إلغاء",            icon: XCircle,     color: "bg-gray-500 hover:bg-gray-600 text-white" },
  ],
  in_progress: [
    { status: "completed",   label: "إكمال الجراحة",   icon: CheckCircle, color: "bg-green-600 hover:bg-green-700 text-white" },
    { status: "cancelled",   label: "إلغاء",             icon: XCircle,    color: "bg-gray-500 hover:bg-gray-600 text-white" },
  ],
  postponed: [
    { status: "scheduled",   label: "إعادة الجدولة",   icon: Clock,       color: "bg-blue-500 hover:bg-blue-600 text-white" },
    { status: "cancelled",   label: "إلغاء",             icon: XCircle,    color: "bg-gray-500 hover:bg-gray-600 text-white" },
  ],
};

const OPERATIVE_TECHNIQUES = [
  { value: "extraction", label: "قلع" },
  { value: "impaction", label: "ضرس منغرس" },
  { value: "cyst", label: "كيسة" },
  { value: "implant", label: "زراعة" },
  { value: "fracture", label: "كسر" },
  { value: "bone_graft", label: "ترقيع عظمي" },
  { value: "other", label: "أخرى" },
];

const OPERATIVE_OUTCOMES = [
  { value: "successful", label: "ناجحة" },
  { value: "complicated", label: "معقدة" },
  { value: "partial", label: "جزئية" },
];

const REFERRAL_STATUS_LABELS: Record<string, string> = {
  pending: "قيد الانتظار",
  accepted: "مقبول",
  completed: "مكتمل",
};

const REFERRAL_STATUS_COLORS: Record<string, string> = {
  pending: "bg-yellow-50 text-yellow-700",
  accepted: "bg-blue-50 text-blue-700",
  completed: "bg-green-50 text-green-700",
};

const inputCls =
  "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal";

type Tab = "info" | "preop" | "operative" | "postop" | "referrals";

export default function SurgeryDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [surgeryCase, setSurgeryCase] = useState<SurgeryCase | null>(null);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<Tab>("info");
  const [doctors, setDoctors] = useState<Doctor[]>([]);

  // preop state
  const [preop, setPreop] = useState<PreopReport>({});
  const [savingPreop, setSavingPreop] = useState(false);
  const [preopSaved, setPreopSaved] = useState(false);

  // postop state
  const [postop, setPostop] = useState<PostopRecord>({});
  const [savingPostop, setSavingPostop] = useState(false);
  const [postopSaved, setPostopSaved] = useState(false);

  // operative report state
  const [operative, setOperative] = useState<OperativeReport>({});
  const [savingOperative, setSavingOperative] = useState(false);
  const [operativeSaved, setOperativeSaved] = useState(false);
  const [approving, setApproving] = useState(false);

  // hospital referrals state
  const [referrals, setReferrals] = useState<HospitalReferral[]>([]);
  const [loadingReferrals, setLoadingReferrals] = useState(false);
  const [showReferralForm, setShowReferralForm] = useState(false);
  const [referralHospital, setReferralHospital] = useState("");
  const [referralReason, setReferralReason] = useState("");
  const [referralDate, setReferralDate] = useState(new Date().toISOString().split("T")[0]);
  const [referralNotes, setReferralNotes] = useState("");
  const [savingReferral, setSavingReferral] = useState(false);

  useEffect(() => {
    Promise.all([
      api.get<SurgeryCase>(`/api/surgery-cases/${id}`),
      api.get<Doctor[]>("/api/doctors"),
      api.get<PreopReport | null>(`/api/surgery-cases/${id}/preop`),
      api.get<PostopRecord | null>(`/api/surgery-cases/${id}/postop`),
      api.get<OperativeReport | null>(`/api/surgery-cases/${id}/operative-report`),
      api.get<HospitalReferral[]>(`/api/surgery-cases/${id}/hospital-referrals`),
    ])
      .then(([caseRes, doctorsRes, preopRes, postopRes, operativeRes, referralsRes]) => {
        setSurgeryCase(caseRes.data);
        setDoctors(doctorsRes.data);
        if (preopRes.data) setPreop(preopRes.data);
        if (postopRes.data) setPostop(postopRes.data);
        if (operativeRes.data) setOperative(operativeRes.data);
        setReferrals(referralsRes.data);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [id]);

  const loadReferrals = () => {
    setLoadingReferrals(true);
    api.get<HospitalReferral[]>(`/api/surgery-cases/${id}/hospital-referrals`)
      .then((r) => setReferrals(r.data))
      .catch(() => {})
      .finally(() => setLoadingReferrals(false));
  };

  const updateStatus = async (status: string) => {
    try {
      await api.put(`/api/surgery-cases/${id}/status`, { status });
      setSurgeryCase((prev) => (prev ? { ...prev, status } : prev));
    } catch {}
  };

  const savePreop = async () => {
    setSavingPreop(true);
    setPreopSaved(false);
    try {
      await api.put(`/api/surgery-cases/${id}/preop`, preop);
      setPreopSaved(true);
      setTimeout(() => setPreopSaved(false), 3000);
    } catch {} finally {
      setSavingPreop(false);
    }
  };

  const savePostop = async () => {
    setSavingPostop(true);
    setPostopSaved(false);
    try {
      await api.put(`/api/surgery-cases/${id}/postop`, postop);
      setPostopSaved(true);
      setTimeout(() => setPostopSaved(false), 3000);
    } catch {} finally {
      setSavingPostop(false);
    }
  };

  const saveOperative = async () => {
    setSavingOperative(true);
    setOperativeSaved(false);
    try {
      const { data } = await api.put<OperativeReport>(`/api/surgery-cases/${id}/operative-report`, operative);
      setOperative(data);
      setOperativeSaved(true);
      setTimeout(() => setOperativeSaved(false), 3000);
    } catch {} finally {
      setSavingOperative(false);
    }
  };

  const approveOperative = async () => {
    if (!operative.id) return;
    setApproving(true);
    try {
      const { data } = await api.put<OperativeReport>(
        `/api/surgery-cases/${id}/operative-report/approve`
      );
      setOperative(data);
    } catch {} finally {
      setApproving(false);
    }
  };

  const handleCreateReferral = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!referralHospital || !referralReason) return;
    setSavingReferral(true);
    try {
      await api.post(`/api/surgery-cases/${id}/hospital-referrals`, {
        surgeryCaseId: id,
        hospitalName: referralHospital,
        reason: referralReason,
        referralDate: referralDate,
        notes: referralNotes || undefined,
      });
      setReferralHospital("");
      setReferralReason("");
      setReferralNotes("");
      setShowReferralForm(false);
      loadReferrals();
    } catch {} finally {
      setSavingReferral(false);
    }
  };

  if (loading) {
    return (
      <div className="space-y-4 max-w-4xl animate-pulse">
        <div className="h-5 w-48 bg-gray-100 rounded" />
        <div className="h-32 bg-gray-100 rounded-xl" />
        <div className="h-10 bg-gray-100 rounded-xl" />
        <div className="h-64 bg-gray-100 rounded-xl" />
      </div>
    );
  }

  if (!surgeryCase) {
    return (
      <div className="flex flex-col items-center justify-center py-24 text-gray-400">
        <AlertCircle className="w-12 h-12 mb-3 opacity-40" />
        <p className="text-sm">لم يتم العثور على الحالة الجراحية</p>
        <Link href="/surgery" className="mt-4 text-sm text-clinic-teal hover:underline">
          العودة إلى قائمة الجراحة
        </Link>
      </div>
    );
  }

  const nextActions = NEXT_STATUSES[surgeryCase.status] ?? [];

  const TABS: { id: Tab; label: string; icon: typeof ClipboardList }[] = [
    { id: "info",       label: "معلومات الحالة",    icon: ClipboardList },
    { id: "preop",      label: "ما قبل الجراحة",    icon: FileText },
    { id: "operative",  label: "التقرير الجراحي",   icon: Stethoscope },
    { id: "postop",     label: "ما بعد الجراحة",    icon: CheckCircle },
    { id: "referrals",  label: "إحالات المستشفى",   icon: Building2 },
  ];

  return (
    <div className="space-y-5 max-w-4xl">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Link href="/surgery" className="hover:text-clinic-teal transition">
          الجراحة
        </Link>
        <span>/</span>
        <span className="text-gray-900 font-medium font-mono">{surgeryCase.caseNumber}</span>
      </div>

      {/* Back + Title */}
      <div className="flex items-center gap-3">
        <Link
          href="/surgery"
          className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 transition text-gray-500"
        >
          <ArrowRight className="w-4 h-4" />
        </Link>
        <Scissors className="w-5 h-5 text-clinic-teal" />
        <h1 className="text-2xl font-extrabold text-gray-900">{surgeryCase.surgeryType}</h1>
      </div>

      {/* Banner */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-4">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <User className="w-4 h-4 text-gray-400" />
              <Link
                href={`/patients/${surgeryCase.patientId}`}
                className="text-base font-semibold text-gray-900 hover:text-clinic-teal transition"
              >
                {surgeryCase.patientName}
              </Link>
              <span className="text-xs text-gray-400 font-mono">{surgeryCase.patientNumber}</span>
            </div>
            <div className="flex items-center gap-2 flex-wrap">
              <span className="font-mono text-xs font-semibold text-clinic-teal">{surgeryCase.caseNumber}</span>
              <span className="text-gray-300">·</span>
              <span className="text-sm text-gray-600">{surgeryCase.surgeryType}</span>
              {surgeryCase.teethInvolved && (
                <>
                  <span className="text-gray-300">·</span>
                  <span className="text-xs font-mono text-gray-500">{surgeryCase.teethInvolved}</span>
                </>
              )}
            </div>
            {surgeryCase.doctorName && (
              <div className="flex items-center gap-1.5 text-sm text-gray-600">
                <div
                  className="w-2 h-2 rounded-full"
                  style={{ backgroundColor: surgeryCase.doctorColor ?? "#0E7490" }}
                />
                {surgeryCase.doctorName}
              </div>
            )}
          </div>

          <div className="flex flex-col items-end gap-2">
            <span
              className={cn(
                "text-sm px-3 py-1 rounded-full font-medium",
                STATUS_COLORS[surgeryCase.status] ?? "bg-gray-100 text-gray-600"
              )}
            >
              {STATUS_LABELS[surgeryCase.status] ?? surgeryCase.status}
            </span>
            <p className="text-xs text-gray-400">{formatArabicDate(surgeryCase.createdAt)}</p>
          </div>
        </div>

        {/* Status action buttons */}
        {nextActions.length > 0 && (
          <div className="flex items-center gap-2 flex-wrap pt-1 border-t border-gray-100">
            {nextActions.map(({ status, label, icon: Icon, color }) => (
              <button
                key={status}
                onClick={() => updateStatus(status)}
                className={cn(
                  "flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg transition",
                  color
                )}
              >
                <Icon className="w-3.5 h-3.5" />
                {label}
              </button>
            ))}
          </div>
        )}
      </div>

      {/* Tabs */}
      <div className="flex items-center gap-1 border-b border-gray-200 overflow-x-auto">
        {TABS.map(({ id: tabId, label, icon: Icon }) => (
          <button
            key={tabId}
            onClick={() => setActiveTab(tabId)}
            className={cn(
              "flex items-center gap-1.5 px-4 py-2.5 text-sm font-medium border-b-2 transition -mb-px whitespace-nowrap",
              activeTab === tabId
                ? "border-clinic-teal text-clinic-teal"
                : "border-transparent text-gray-500 hover:text-gray-700"
            )}
          >
            <Icon className="w-4 h-4" />
            {label}
          </button>
        ))}
      </div>

      {/* Tab: Info */}
      {activeTab === "info" && (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
          <h2 className="text-sm font-semibold text-gray-700 mb-4">معلومات الحالة</h2>
          <dl className="grid grid-cols-1 sm:grid-cols-2 gap-x-8 gap-y-4 text-sm">
            <div>
              <dt className="text-xs text-gray-400 mb-0.5">رقم الحالة</dt>
              <dd className="font-mono font-semibold text-clinic-teal">{surgeryCase.caseNumber}</dd>
            </div>
            <div>
              <dt className="text-xs text-gray-400 mb-0.5">نوع الجراحة</dt>
              <dd className="text-gray-800">{surgeryCase.surgeryType}</dd>
            </div>
            <div>
              <dt className="text-xs text-gray-400 mb-0.5">الأسنان المعنية</dt>
              <dd className="font-mono text-gray-700">{surgeryCase.teethInvolved ?? "—"}</dd>
            </div>
            <div>
              <dt className="text-xs text-gray-400 mb-0.5">الطبيب الجراح</dt>
              <dd className="text-gray-800">
                {surgeryCase.doctorName ? (
                  <div className="flex items-center gap-1.5">
                    <div
                      className="w-2 h-2 rounded-full"
                      style={{ backgroundColor: surgeryCase.doctorColor ?? "#0E7490" }}
                    />
                    {surgeryCase.doctorName}
                  </div>
                ) : (
                  "—"
                )}
              </dd>
            </div>
            <div>
              <dt className="text-xs text-gray-400 mb-0.5">تاريخ الإنشاء</dt>
              <dd className="text-gray-800">{formatArabicDate(surgeryCase.createdAt)}</dd>
            </div>
            <div>
              <dt className="text-xs text-gray-400 mb-0.5">الحالة</dt>
              <dd>
                <span
                  className={cn(
                    "text-xs px-2 py-0.5 rounded-full font-medium",
                    STATUS_COLORS[surgeryCase.status] ?? "bg-gray-100 text-gray-600"
                  )}
                >
                  {STATUS_LABELS[surgeryCase.status] ?? surgeryCase.status}
                </span>
              </dd>
            </div>
          </dl>
        </div>
      )}

      {/* Tab: Pre-op */}
      {activeTab === "preop" && (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-4">
          <h2 className="text-sm font-semibold text-gray-700">ما قبل الجراحة</h2>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1.5">تاريخ الجراحة</label>
              <input
                type="date"
                value={preop.surgeryDate ?? ""}
                onChange={(e) => setPreop((p) => ({ ...p, surgeryDate: e.target.value || undefined }))}
                className={inputCls}
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1.5">موقع الجراحة</label>
              <input
                type="text"
                value={preop.surgeryLocation ?? ""}
                onChange={(e) => setPreop((p) => ({ ...p, surgeryLocation: e.target.value || undefined }))}
                placeholder="مثلاً: غرفة العمليات 1"
                className={inputCls}
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1.5">نوع التخدير</label>
              <input
                type="text"
                value={preop.anesthesiaType ?? ""}
                onChange={(e) => setPreop((p) => ({ ...p, anesthesiaType: e.target.value || undefined }))}
                placeholder="مثلاً: تخدير موضعي، عام..."
                className={inputCls}
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1.5">الطبيب الجراح</label>
              <select
                value={preop.doctorId ?? ""}
                onChange={(e) => setPreop((p) => ({ ...p, doctorId: e.target.value || undefined }))}
                className={inputCls}
              >
                <option value="">اختر طبيباً...</option>
                {doctors.map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.name}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <input
              id="consent"
              type="checkbox"
              checked={preop.consentSigned ?? false}
              onChange={(e) => setPreop((p) => ({ ...p, consentSigned: e.target.checked }))}
              className="w-4 h-4 rounded border-gray-300 text-clinic-teal focus:ring-clinic-teal"
            />
            <label htmlFor="consent" className="text-sm text-gray-700 cursor-pointer">
              تم توقيع نموذج الموافقة على الجراحة
            </label>
          </div>

          <div className="flex items-center justify-end gap-3 pt-2 border-t border-gray-100">
            {preopSaved && (
              <span className="flex items-center gap-1 text-xs text-green-600">
                <CheckCircle className="w-3.5 h-3.5" />
                تم الحفظ
              </span>
            )}
            <button
              onClick={savePreop}
              disabled={savingPreop}
              className="flex items-center gap-2 px-5 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60 transition"
            >
              <Save className="w-4 h-4" />
              {savingPreop ? "جارٍ الحفظ..." : "حفظ"}
            </button>
          </div>
        </div>
      )}

      {/* Tab: Operative Report */}
      {activeTab === "operative" && (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-4">
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-semibold text-gray-700">التقرير الجراحي</h2>
            {operative.isApproved && (
              <span className="flex items-center gap-1.5 text-xs text-green-700 bg-green-50 px-2.5 py-1 rounded-full">
                <ShieldCheck className="w-3.5 h-3.5" />
                معتمد {operative.approvedByName ? `بواسطة ${operative.approvedByName}` : ""}
              </span>
            )}
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1.5">تاريخ الجراحة</label>
              <input
                type="date"
                value={operative.surgeryDate ?? ""}
                onChange={(e) => setOperative((p) => ({ ...p, surgeryDate: e.target.value || undefined }))}
                className={inputCls}
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1.5">وقت الجراحة</label>
              <input
                type="time"
                value={operative.surgeryTime ?? ""}
                onChange={(e) => setOperative((p) => ({ ...p, surgeryTime: e.target.value || undefined }))}
                className={inputCls}
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1.5">مدة العملية (دقيقة)</label>
              <input
                type="number"
                min={0}
                value={operative.durationMinutes ?? ""}
                onChange={(e) =>
                  setOperative((p) => ({
                    ...p,
                    durationMinutes: e.target.value ? parseInt(e.target.value) : undefined,
                  }))
                }
                placeholder="مثلاً: 45"
                className={inputCls}
                dir="ltr"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1.5">التخدير المستخدم</label>
              <input
                type="text"
                value={operative.anesthesiaUsed ?? ""}
                onChange={(e) => setOperative((p) => ({ ...p, anesthesiaUsed: e.target.value || undefined }))}
                placeholder="مثلاً: ليدوكائين 2% مع أدرينالين"
                className={inputCls}
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1.5">التقنية الجراحية</label>
              <select
                value={operative.technique ?? ""}
                onChange={(e) => setOperative((p) => ({ ...p, technique: e.target.value || undefined }))}
                className={inputCls}
              >
                <option value="">اختر...</option>
                {OPERATIVE_TECHNIQUES.map((t) => (
                  <option key={t.value} value={t.value}>{t.label}</option>
                ))}
              </select>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1.5">نتيجة العملية</label>
              <select
                value={operative.outcome ?? ""}
                onChange={(e) => setOperative((p) => ({ ...p, outcome: e.target.value || undefined }))}
                className={inputCls}
              >
                <option value="">اختر...</option>
                {OPERATIVE_OUTCOMES.map((o) => (
                  <option key={o.value} value={o.value}>{o.label}</option>
                ))}
              </select>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1.5">عدد الغرز</label>
              <input
                type="number"
                min={0}
                value={operative.suturesCount ?? ""}
                onChange={(e) =>
                  setOperative((p) => ({
                    ...p,
                    suturesCount: e.target.value ? parseInt(e.target.value) : undefined,
                  }))
                }
                placeholder="0"
                className={inputCls}
                dir="ltr"
              />
            </div>

            <div className="flex items-center gap-2 self-end pb-1">
              <input
                id="specimen"
                type="checkbox"
                checked={operative.specimenSent ?? false}
                onChange={(e) => setOperative((p) => ({ ...p, specimenSent: e.target.checked }))}
                className="w-4 h-4 rounded border-gray-300 text-clinic-teal focus:ring-clinic-teal"
              />
              <label htmlFor="specimen" className="text-sm text-gray-700 cursor-pointer">
                تم إرسال عينة
              </label>
            </div>
          </div>

          {/* Description */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">وصف العملية التفصيلي</label>
            <textarea
              value={operative.description ?? ""}
              onChange={(e) => setOperative((p) => ({ ...p, description: e.target.value || undefined }))}
              rows={5}
              placeholder="وصف تفصيلي لخطوات العملية الجراحية..."
              className={cn(inputCls, "resize-y text-start")}
            />
          </div>

          {/* Complications */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">المضاعفات</label>
            <textarea
              value={operative.complications ?? ""}
              onChange={(e) => setOperative((p) => ({ ...p, complications: e.target.value || undefined }))}
              rows={3}
              placeholder="أي مضاعفات حدثت أثناء أو بعد العملية..."
              className={cn(inputCls, "resize-y text-start")}
            />
          </div>

          {/* Actions */}
          <div className="flex items-center justify-between pt-2 border-t border-gray-100">
            <div>
              {operative.id && !operative.isApproved && (
                <button
                  onClick={approveOperative}
                  disabled={approving}
                  className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-green-600 text-white hover:bg-green-700 disabled:opacity-60 transition"
                >
                  <ShieldCheck className="w-4 h-4" />
                  {approving ? "جارٍ الاعتماد..." : "اعتماد التقرير"}
                </button>
              )}
            </div>
            <div className="flex items-center gap-3">
              {operativeSaved && (
                <span className="flex items-center gap-1 text-xs text-green-600">
                  <CheckCircle className="w-3.5 h-3.5" />
                  تم الحفظ
                </span>
              )}
              <button
                onClick={saveOperative}
                disabled={savingOperative}
                className="flex items-center gap-2 px-5 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60 transition"
              >
                <Save className="w-4 h-4" />
                {savingOperative ? "جارٍ الحفظ..." : "حفظ"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Tab: Post-op */}
      {activeTab === "postop" && (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-4">
          <h2 className="text-sm font-semibold text-gray-700">ما بعد الجراحة</h2>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">تعليمات ما بعد الجراحة</label>
            <textarea
              value={postop.instructions ?? ""}
              onChange={(e) => setPostop((p) => ({ ...p, instructions: e.target.value || undefined }))}
              rows={8}
              placeholder="أدخل التعليمات والإرشادات للمريض بعد الجراحة..."
              className={cn(inputCls, "resize-y text-start")}
            />
          </div>

          <div className="flex items-center justify-end gap-3 pt-2 border-t border-gray-100">
            {postopSaved && (
              <span className="flex items-center gap-1 text-xs text-green-600">
                <CheckCircle className="w-3.5 h-3.5" />
                تم الحفظ
              </span>
            )}
            <button
              onClick={savePostop}
              disabled={savingPostop}
              className="flex items-center gap-2 px-5 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60 transition"
            >
              <Save className="w-4 h-4" />
              {savingPostop ? "جارٍ الحفظ..." : "حفظ"}
            </button>
          </div>
        </div>
      )}

      {/* Tab: Hospital Referrals */}
      {activeTab === "referrals" && (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-4">
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-semibold text-gray-700">إحالات المستشفى</h2>
            <button
              onClick={() => setShowReferralForm(!showReferralForm)}
              className="flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 transition"
            >
              {showReferralForm ? <X className="w-4 h-4" /> : <Plus className="w-4 h-4" />}
              {showReferralForm ? "إغلاق" : "إحالة جديدة"}
            </button>
          </div>

          {/* New referral form */}
          {showReferralForm && (
            <form onSubmit={handleCreateReferral} className="bg-gray-50 rounded-lg p-4 border border-gray-100 space-y-3">
              <p className="text-xs font-semibold text-gray-500 mb-1">إنشاء إحالة جديدة</p>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs text-gray-500 mb-1">اسم المستشفى *</label>
                  <input
                    type="text"
                    value={referralHospital}
                    onChange={(e) => setReferralHospital(e.target.value)}
                    required
                    className="w-full text-sm border border-gray-200 rounded-lg px-3 py-2 focus:outline-none focus:border-clinic-teal"
                    placeholder="مثلاً: مستشفى الثورة..."
                  />
                </div>
                <div>
                  <label className="block text-xs text-gray-500 mb-1">تاريخ الإحالة *</label>
                  <input
                    type="date"
                    value={referralDate}
                    onChange={(e) => setReferralDate(e.target.value)}
                    required
                    className="w-full text-sm border border-gray-200 rounded-lg px-3 py-2 focus:outline-none focus:border-clinic-teal"
                  />
                </div>
                <div className="sm:col-span-2">
                  <label className="block text-xs text-gray-500 mb-1">السبب *</label>
                  <input
                    type="text"
                    value={referralReason}
                    onChange={(e) => setReferralReason(e.target.value)}
                    required
                    className="w-full text-sm border border-gray-200 rounded-lg px-3 py-2 focus:outline-none focus:border-clinic-teal"
                    placeholder="سبب الإحالة..."
                  />
                </div>
                <div className="sm:col-span-2">
                  <label className="block text-xs text-gray-500 mb-1">ملاحظات</label>
                  <textarea
                    value={referralNotes}
                    onChange={(e) => setReferralNotes(e.target.value)}
                    rows={2}
                    className="w-full text-sm border border-gray-200 rounded-lg px-3 py-2 focus:outline-none focus:border-clinic-teal resize-none"
                    placeholder="ملاحظات اختيارية..."
                  />
                </div>
              </div>
              <button
                type="submit"
                disabled={savingReferral}
                className="flex items-center gap-1.5 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 transition disabled:opacity-50"
              >
                <Plus className="w-3.5 h-3.5" />
                {savingReferral ? "جارٍ الإنشاء..." : "إنشاء الإحالة"}
              </button>
            </form>
          )}

          {/* Referrals list */}
          {loadingReferrals ? (
            <div className="space-y-2 animate-pulse">
              {Array.from({ length: 3 }).map((_, i) => (
                <div key={i} className="h-16 bg-gray-100 rounded-lg" />
              ))}
            </div>
          ) : referrals.length === 0 ? (
            <div className="text-center py-12 text-gray-400">
              <Building2 className="w-10 h-10 mx-auto mb-2 opacity-30" />
              <p className="text-sm">لا توجد إحالات مستشفى</p>
              <p className="text-xs mt-1">أنشئ إحالة جديدة من الزر أعلاه</p>
            </div>
          ) : (
            <div className="space-y-3">
              {referrals.map((ref) => (
                <div
                  key={ref.id}
                  className="flex items-start justify-between gap-3 p-4 bg-gray-50 rounded-lg border border-gray-100 hover:border-gray-200 transition"
                >
                  <div className="space-y-1">
                    <div className="flex items-center gap-2 flex-wrap">
                      <span className="text-sm font-semibold text-gray-900">{ref.hospitalName}</span>
                      <span className={cn(
                        "text-xs px-2 py-0.5 rounded-full font-medium",
                        REFERRAL_STATUS_COLORS[ref.status] ?? "bg-gray-100 text-gray-600"
                      )}>
                        {REFERRAL_STATUS_LABELS[ref.status] ?? ref.status}
                      </span>
                    </div>
                    <p className="text-sm text-gray-600">{ref.reason}</p>
                    <p className="text-xs text-gray-400">{formatArabicDate(ref.referralDate)}</p>
                    {ref.notes && <p className="text-xs text-gray-500 mt-1">{ref.notes}</p>}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
