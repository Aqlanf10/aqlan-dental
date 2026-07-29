
import { useEffect, useState, useCallback } from "react";
import { useParams } from "@/lib/nextNavCompat";
import Link from "@/lib/nextLinkCompat";
import {
  Scissors, User, ArrowRight, CheckCircle, Clock, XCircle,
  PlayCircle, ClipboardList, FileText, Save, AlertCircle,
  Pencil, Plus, Trash2, Loader2, ArrowRightLeft, Pill,
  Calendar, ShieldCheck, X, FlaskConical, GitBranch, ExternalLink,
} from "lucide-react";
import type {
  SurgeryCase, PreopReport, OperativeReport, PostopRecord, HospitalReferral,
  UpsertPreopRequest, UpsertOperativeReportRequest, UpsertPostopRequest,
  CreateHospitalReferralRequest,
  PrescriptionItem, FollowupItem,
} from "@/types/surgery";
import { SURGERY_STATUS_LABELS, SURGERY_STATUS_COLORS, REFERRAL_STATUS_LABELS } from "@/types/surgery";
import type { OrthoSurgicalCaseListItem } from "@/types/orthoSurgical";
import api from "@/lib/api";
import { useDoctors } from "@/hooks/useDoctors";
import { cn, formatArabicDate } from "@/lib/utils";
import { toast } from "@/stores/toastStore";

// ─── Local Types ──────────────────────────────────────────────────────────────

interface Doctor {
  id: string;
  name: string;
}

type Tab = "info" | "preop" | "operative" | "postop" | "referrals";

// ─── Constants ────────────────────────────────────────────────────────────────

const NEXT_STATUSES: Record<
  string,
  { status: string; label: string; icon: typeof PlayCircle; color: string }[]
> = {
  scheduled: [
    { status: "in_progress", label: "بدء الجراحة",  icon: PlayCircle,  color: "bg-yellow-500 hover:bg-yellow-600 text-white" },
    { status: "postponed",   label: "تأجيل",         icon: Clock,       color: "bg-orange-500 hover:bg-orange-600 text-white" },
    { status: "cancelled",   label: "إلغاء",          icon: XCircle,     color: "bg-gray-500 hover:bg-gray-600 text-white" },
  ],
  in_progress: [
    { status: "completed",   label: "إكمال الجراحة", icon: CheckCircle, color: "bg-green-600 hover:bg-green-700 text-white" },
    { status: "cancelled",   label: "إلغاء",           icon: XCircle,    color: "bg-gray-500 hover:bg-gray-600 text-white" },
  ],
  postponed: [
    { status: "scheduled",   label: "إعادة الجدولة", icon: Clock,       color: "bg-blue-500 hover:bg-blue-600 text-white" },
    { status: "cancelled",   label: "إلغاء",           icon: XCircle,    color: "bg-gray-500 hover:bg-gray-600 text-white" },
  ],
};

const PREOP_CHECKLIST_ITEMS = [
  { key: "blood_test",       label: "فحص الدم" },
  { key: "panoramic_xray",   label: "صورة بانوراما" },
  { key: "radiograph",       label: "تصوير أشعة" },
  { key: "anesthesia_eval",  label: "تقييم التخدير" },
  { key: "allergy_review",   label: "مراجعة الحساسية" },
  { key: "fasting",          label: "صيام" },
  { key: "stop_blood_thin",  label: "إيقاف مميعات الدم" },
] as const;

const REFERRAL_STATUS_COLORS: Record<string, string> = {
  pending:     "bg-blue-50 text-blue-700",
  in_progress: "bg-yellow-50 text-yellow-700",
  completed:   "bg-green-50 text-green-700",
  cancelled:   "bg-gray-100 text-gray-500",
};

const TABS: { id: Tab; label: string; icon: typeof ClipboardList }[] = [
  { id: "info",       label: "معلومات الحالة",    icon: ClipboardList },
  { id: "preop",      label: "ما قبل الجراحة",    icon: FileText },
  { id: "operative",  label: "تقرير الجراحة",      icon: Scissors },
  { id: "postop",     label: "ما بعد الجراحة",    icon: CheckCircle },
  { id: "referrals",  label: "إحالات المستشفيات",  icon: ArrowRightLeft },
];

const OUTCOME_OPTIONS = [
  "ناجحة بدون مضاعفات",
  "ناجحة مع مضاعفات بسيطة",
  "مضاعفات",
  "تحتاج متابعة",
];

const inputCls =
  "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-[#3d7ab5] focus:border-[#3d7ab5] transition";

const selectCls =
  "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-[#3d7ab5] focus:border-[#3d7ab5] transition";

const textareaCls =
  "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-[#3d7ab5] focus:border-[#3d7ab5] transition resize-y";

const primaryBtnCls =
  "flex items-center gap-2 px-5 py-2 text-sm font-medium rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] disabled:opacity-60 transition";

const dangerBtnCls =
  "flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg bg-red-500 text-white hover:bg-red-600 transition";

// ─── Page Component ───────────────────────────────────────────────────────────

export default function SurgeryDetailPage() {
  const { id } = useParams<{ id: string }>();

  // ── Core state ──────────────────────────────────────────────────────────────
  const [surgeryCase, setSurgeryCase] = useState<SurgeryCase | null>(null);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<Tab>("info");
  // FE-13: useDoctors() replaces the doctors fetch in the Promise.all.
  const { data: doctors = [] } = useDoctors();

  // ── Preop state ─────────────────────────────────────────────────────────────
  const [preop, setPreop] = useState<UpsertPreopRequest>({ consentSigned: false, checklist: {}, requiredTests: [] });
  const [savingPreop, setSavingPreop] = useState(false);
  const [preopSaved, setPreopSaved] = useState(false);
  const [newTest, setNewTest] = useState("");

  // ── Operative state ─────────────────────────────────────────────────────────
  const [operative, setOperative] = useState<UpsertOperativeReportRequest>({ specimenSent: false });
  const [operativeLoaded, setOperativeLoaded] = useState<OperativeReport | null>(null);
  const [savingOperative, setSavingOperative] = useState(false);
  const [operativeSaved, setOperativeSaved] = useState(false);
  const [approvingOperative, setApprovingOperative] = useState(false);

  // ── Postop state ────────────────────────────────────────────────────────────
  const [postop, setPostop] = useState<UpsertPostopRequest>({ prescription: [], followupSchedule: [] });
  const [savingPostop, setSavingPostop] = useState(false);
  const [postopSaved, setPostopSaved] = useState(false);

  // ── Referrals state ─────────────────────────────────────────────────────────
  const [referrals, setReferrals] = useState<HospitalReferral[]>([]);
  const [referralsLoading, setReferralsLoading] = useState(true);
  const [referralForm, setReferralForm] = useState<CreateHospitalReferralRequest>({});
  const [editingReferralId, setEditingReferralId] = useState<string | null>(null);
  const [savingReferral, setSavingReferral] = useState(false);

  // ── Status transition ───────────────────────────────────────────────────────
  const [transitioningStatus, setTransitioningStatus] = useState<string | null>(null);

  // ── Linked ortho-surgical planning case (reciprocal link) ───────────────────
  // If this surgery was opened from a shared ortho-surgical joint plan
  // (create-surgery-case), surface a link back to the ortho case's "جراحة
  // الفكين" tab so the surgeon isn't stranded here with no way back.
  const [linkedOrthoSurgical, setLinkedOrthoSurgical] = useState<OrthoSurgicalCaseListItem | null>(null);

  useEffect(() => {
    api.get<{ data: OrthoSurgicalCaseListItem[] }>("/api/ortho-surgical-cases", { params: { surgeryCaseId: id, pageSize: 1 } })
      .then((r) => setLinkedOrthoSurgical(r.data?.data?.[0] ?? null))
      .catch(() => {});
  }, [id]);

  // ── Data fetching ───────────────────────────────────────────────────────────

  const fetchReferrals = useCallback(() => {
    setReferralsLoading(true);
    api.get<HospitalReferral[]>(`/api/surgery-cases/${id}/referrals`)
      .then((r) => setReferrals(r.data ?? []))
      .catch(() => {})
      .finally(() => setReferralsLoading(false));
  }, [id]);

  useEffect(() => {
    // FE-13: doctors come from useDoctors() — only fetch the 4 surgery-case resources.
    Promise.all([
      api.get<SurgeryCase>(`/api/surgery-cases/${id}`),
      api.get<PreopReport | null>(`/api/surgery-cases/${id}/preop`),
      api.get<OperativeReport | null>(`/api/surgery-cases/${id}/operative`),
      api.get<PostopRecord | null>(`/api/surgery-cases/${id}/postop`),
    ])
      .then(([caseRes, preopRes, operativeRes, postopRes]) => {
        setSurgeryCase(caseRes.data);
        if (preopRes.data) {
          const { id: _preopId, ...rest } = preopRes.data;
          void _preopId;
          setPreop(rest);
        }
        if (operativeRes.data) {
          setOperativeLoaded(operativeRes.data);
          const { id: _opId, approvedAt: _opApprovedAt, ...rest } = operativeRes.data;
          void _opId; void _opApprovedAt;
          setOperative(rest);
        }
        if (postopRes.data) {
          const { id: _postId, ...rest } = postopRes.data;
          void _postId;
          setPostop(rest);
        }
      })
      .catch(() => {})
      .finally(() => setLoading(false));

    fetchReferrals();
  }, [id, fetchReferrals]);

  // ── Handlers ────────────────────────────────────────────────────────────────

  const updateStatus = async (status: string) => {
    setTransitioningStatus(status);
    try {
      await api.put(`/api/surgery-cases/${id}/status`, { status });
      setSurgeryCase((prev) => (prev ? { ...prev, status } : prev));
      toast.success(
        `تم تحديث حالة الجراحة إلى "${SURGERY_STATUS_LABELS[status] ?? status}"`,
      );
    } catch {
      toast.error("فشل تحديث حالة الجراحة");
    } finally {
      setTransitioningStatus(null);
    }
  };

  const savePreop = async () => {
    setSavingPreop(true);
    setPreopSaved(false);
    try {
      await api.put(`/api/surgery-cases/${id}/preop`, preop);
      setPreopSaved(true);
      toast.success("تم حفظ تقرير ما قبل الجراحة");
      setTimeout(() => setPreopSaved(false), 3000);
    } catch {
      toast.error("فشل حفظ تقرير ما قبل الجراحة");
    } finally {
      setSavingPreop(false);
    }
  };

  const saveOperative = async () => {
    setSavingOperative(true);
    setOperativeSaved(false);
    try {
      await api.put(`/api/surgery-cases/${id}/operative`, operative);
      setOperativeSaved(true);
      toast.success("تم حفظ تقرير الجراحة");
      setTimeout(() => setOperativeSaved(false), 3000);
    } catch {
      toast.error("فشل حفظ تقرير الجراحة");
    } finally {
      setSavingOperative(false);
    }
  };

  const approveOperative = async () => {
    setApprovingOperative(true);
    try {
      await api.put(`/api/surgery-cases/${id}/operative/approve`);
      setOperativeLoaded((prev) => (prev ? { ...prev, approvedAt: new Date().toISOString() } : prev));
      toast.success("تم اعتماد تقرير الجراحة");
    } catch {
      toast.error("فشل اعتماد تقرير الجراحة");
    } finally {
      setApprovingOperative(false);
    }
  };

  const savePostop = async () => {
    setSavingPostop(true);
    setPostopSaved(false);
    try {
      await api.put(`/api/surgery-cases/${id}/postop`, postop);
      setPostopSaved(true);
      toast.success("تم حفظ تقرير ما بعد الجراحة");
      setTimeout(() => setPostopSaved(false), 3000);
    } catch {
      toast.error("فشل حفظ تقرير ما بعد الجراحة");
    } finally {
      setSavingPostop(false);
    }
  };

  // ── Preop: Required tests ───────────────────────────────────────────────────

  const addTest = () => {
    const trimmed = newTest.trim();
    if (!trimmed) return;
    setPreop((p) => ({
      ...p,
      requiredTests: [...(p.requiredTests ?? []), trimmed],
    }));
    setNewTest("");
  };

  const removeTest = (index: number) => {
    setPreop((p) => ({
      ...p,
      requiredTests: (p.requiredTests ?? []).filter((_, i) => i !== index),
    }));
  };

  // ── Preop: Checklist toggle ─────────────────────────────────────────────────

  const toggleChecklist = (key: string) => {
    setPreop((p) => ({
      ...p,
      checklist: { ...(p.checklist ?? {}), [key]: !p.checklist?.[key] },
    }));
  };

  // ── Postop: Prescription ───────────────────────────────────────────────────

  const addPrescription = () => {
    setPostop((p) => ({
      ...p,
      prescription: [...(p.prescription ?? []), { medicine: "", dosage: "", frequency: "", duration: "" }],
    }));
  };

  const removePrescription = (index: number) => {
    setPostop((p) => ({
      ...p,
      prescription: (p.prescription ?? []).filter((_, i) => i !== index),
    }));
  };

  const updatePrescription = (index: number, field: keyof PrescriptionItem, value: string) => {
    setPostop((p) => ({
      ...p,
      prescription: (p.prescription ?? []).map((item, i) =>
        i === index ? { ...item, [field]: value } : item,
      ),
    }));
  };

  // ── Postop: Followup schedule ───────────────────────────────────────────────

  const addFollowup = () => {
    setPostop((p) => ({
      ...p,
      followupSchedule: [...(p.followupSchedule ?? []), { date: "", notes: "" }],
    }));
  };

  const removeFollowup = (index: number) => {
    setPostop((p) => ({
      ...p,
      followupSchedule: (p.followupSchedule ?? []).filter((_, i) => i !== index),
    }));
  };

  const updateFollowup = (index: number, field: keyof FollowupItem, value: string) => {
    setPostop((p) => ({
      ...p,
      followupSchedule: (p.followupSchedule ?? []).map((item, i) =>
        i === index ? { ...item, [field]: value } : item,
      ),
    }));
  };

  // ── Referrals CRUD ──────────────────────────────────────────────────────────

  const resetReferralForm = () => {
    setReferralForm({});
    setEditingReferralId(null);
  };

  const handleSaveReferral = async () => {
    setSavingReferral(true);
    try {
      if (editingReferralId) {
        await api.put(`/api/surgery-cases/${id}/referrals/${editingReferralId}`, referralForm);
        toast.success("تم تحديث الإحالة");
      } else {
        await api.post(`/api/surgery-cases/${id}/referrals`, referralForm);
        toast.success("تم إنشاء الإحالة بنجاح");
      }
      resetReferralForm();
      fetchReferrals();
    } catch {
      toast.error("فشل حفظ الإحالة");
    } finally {
      setSavingReferral(false);
    }
  };

  const handleEditReferral = (ref: HospitalReferral) => {
    setReferralForm({
      hospitalName: ref.hospitalName,
      reason: ref.reason,
      referralDate: ref.referralDate,
      notes: ref.notes,
    });
    setEditingReferralId(ref.id);
  };

  const handleDeleteReferral = async (refId: string) => {
    if (!window.confirm("هل أنت متأكد من حذف هذه الإحالة؟")) return;
    try {
      await api.delete(`/api/surgery-cases/${id}/referrals/${refId}`);
      toast.success("تم حذف الإحالة");
      fetchReferrals();
    } catch {
      toast.error("فشل حذف الإحالة");
    }
  };

  const handleReferralStatusChange = async (refId: string, status: HospitalReferral["status"]) => {
    try {
      await api.put(`/api/surgery-cases/${id}/referrals/${refId}`, { status });
      toast.success(`تم تحديث حالة الإحالة إلى "${REFERRAL_STATUS_LABELS[status]}"`);
      fetchReferrals();
    } catch {
      toast.error("فشل تحديث حالة الإحالة");
    }
  };

  // ── Loading & Not Found ─────────────────────────────────────────────────────

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
        <Link href="/surgery" className="mt-4 text-sm text-[#3d7ab5] hover:underline">
          العودة إلى قائمة الجراحة
        </Link>
      </div>
    );
  }

  const nextActions = NEXT_STATUSES[surgeryCase.status] ?? [];

  // ── Render ──────────────────────────────────────────────────────────────────

  return (
    <div className="space-y-5 max-w-4xl">
      {/* ── Breadcrumb ──────────────────────────────────────────────────────── */}
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Link href="/surgery" className="hover:text-[#3d7ab5] transition">
          الجراحة
        </Link>
        <span>/</span>
        <span className="text-gray-900 font-medium font-mono">{surgeryCase.caseNumber}</span>
      </div>

      {/* ── Back + Title ────────────────────────────────────────────────────── */}
      <div className="flex items-center gap-3">
        <Link
          href="/surgery"
          className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 transition text-gray-500"
        >
          <ArrowRight className="w-4 h-4" />
        </Link>
        <Scissors className="w-5 h-5 text-[#3d7ab5]" />
        <h1 className="text-2xl font-extrabold text-gray-900">{surgeryCase.surgeryType}</h1>
      </div>

      {/* ── Linked ortho-surgical joint plan (reciprocal link) ────────────────── */}
      {linkedOrthoSurgical && (
        <Link
          href={`/ortho/${linkedOrthoSurgical.orthoCaseId}?tab=surgical`}
          className="flex items-center justify-between gap-3 bg-[#f7fafd] border border-[#3d7ab5]/30 rounded-xl px-4 py-3 hover:border-[#3d7ab5] transition"
        >
          <span className="flex items-center gap-2 text-sm text-gray-700">
            <GitBranch className="w-4 h-4 text-[#3d7ab5]" />
            نشأت هذه الجراحة من خطة تقويمية جراحية مشتركة —
            <span className="font-mono text-xs font-semibold text-[#3d7ab5]">{linkedOrthoSurgical.caseNumber}</span>
            <span className="text-xs text-gray-500">({linkedOrthoSurgical.statusLabel})</span>
          </span>
          <span className="flex items-center gap-1 text-xs font-medium text-[#3d7ab5] flex-shrink-0">
            فتح الخطة المشتركة <ExternalLink className="w-3 h-3" />
          </span>
        </Link>
      )}

      {/* ── Banner ──────────────────────────────────────────────────────────── */}
      <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-sm p-5 space-y-4">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <User className="w-4 h-4 text-gray-400" />
              <Link
                href={`/patients/${surgeryCase.patientId}`}
                className="text-base font-semibold text-gray-900 hover:text-[#3d7ab5] transition"
              >
                {surgeryCase.patientName}
              </Link>
              <span className="text-xs text-gray-400 font-mono">{surgeryCase.patientNumber}</span>
            </div>
            <div className="flex items-center gap-2 flex-wrap">
              <span className="font-mono text-xs font-semibold text-[#3d7ab5]">{surgeryCase.caseNumber}</span>
              <span className="text-gray-300">·</span>
              <span className="text-sm text-gray-600">{surgeryCase.surgeryType}</span>
              {surgeryCase.teethInvolved && surgeryCase.teethInvolved.length > 0 && (
                <>
                  <span className="text-gray-300">·</span>
                  <span className="text-xs font-mono text-gray-500">
                    {Array.isArray(surgeryCase.teethInvolved) ? surgeryCase.teethInvolved.join(", ") : surgeryCase.teethInvolved}
                  </span>
                </>
              )}
            </div>
            {surgeryCase.doctorName && (
              <div className="flex items-center gap-1.5 text-sm text-gray-600">
                <div
                  className="w-2 h-2 rounded-full"
                  style={{ backgroundColor: surgeryCase.doctorColor ?? "#2563EB" }}
                />
                {surgeryCase.doctorName}
              </div>
            )}
          </div>

          <div className="flex flex-col items-end gap-2">
            <span
              className={cn(
                "text-sm px-3 py-1 rounded-full font-medium",
                SURGERY_STATUS_COLORS[surgeryCase.status] ?? "bg-gray-100 text-gray-600"
              )}
            >
              {SURGERY_STATUS_LABELS[surgeryCase.status] ?? surgeryCase.status}
            </span>
            <p className="text-xs text-gray-400">{formatArabicDate(surgeryCase.createdAt)}</p>
          </div>
        </div>

        {/* ── Status action buttons + Edit ───────────────────────────────────── */}
        {nextActions.length > 0 && (
          <div className="flex items-center gap-2 flex-wrap pt-3 border-t border-[#e8f0f9]">
            {nextActions.map(({ status, label, icon: Icon, color }) => (
              <button
                key={status}
                onClick={() => updateStatus(status)}
                disabled={transitioningStatus === status}
                className={cn(
                  "flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg transition disabled:opacity-60",
                  color
                )}
              >
                {transitioningStatus === status ? (
                  <Loader2 className="w-3.5 h-3.5 animate-spin" />
                ) : (
                  <Icon className="w-3.5 h-3.5" />
                )}
                {label}
              </button>
            ))}

            <div className="w-px h-6 mx-1 hidden sm:block bg-[#e8f0f9]" />

            <Link
              href={`/surgery/${id}/edit`}
              className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-lg transition border border-[#3d7ab5] text-[#3d7ab5] hover:bg-[#eef3f9]"
            >
              <Pencil className="w-3.5 h-3.5" />
              تعديل
            </Link>
          </div>
        )}

        {/* Edit button when no actions available */}
        {nextActions.length === 0 && (
          <div className="flex items-center justify-end pt-3 border-t border-[#e8f0f9]">
            <Link
              href={`/surgery/${id}/edit`}
              className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-lg transition border border-[#3d7ab5] text-[#3d7ab5] hover:bg-[#eef3f9]"
            >
              <Pencil className="w-3.5 h-3.5" />
              تعديل
            </Link>
          </div>
        )}
      </div>

      {/* ── Tabs ────────────────────────────────────────────────────────────── */}
      <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-sm overflow-hidden">
        <div className="flex overflow-x-auto" style={{ borderBottom: "2px solid #e8f0f9" }}>
          {TABS.map(({ id: tabId, label, icon: Icon }) => (
            <button
              key={tabId}
              onClick={() => setActiveTab(tabId)}
              className="flex items-center gap-1.5 px-4 py-2.5 text-sm font-medium whitespace-nowrap transition"
              style={{
                borderBottom: activeTab === tabId ? "2px solid #3d7ab5" : "2px solid transparent",
                color: activeTab === tabId ? "#3d7ab5" : "#64748b",
                fontWeight: activeTab === tabId ? 700 : 500,
                marginBottom: -2,
              }}
            >
              <Icon className="w-4 h-4" />
              {label}
            </button>
          ))}
        </div>

        <div className="p-5">
          {/* ══════════════════════════════════════════════════════════════════ */}
          {/* TAB: Info — معلومات الحالة                                     */}
          {/* ══════════════════════════════════════════════════════════════════ */}
          {activeTab === "info" && (
            <div className="space-y-4">
              <div className="flex items-center justify-between">
                <h2 className="text-sm font-semibold text-gray-700">معلومات الحالة</h2>
                <Link
                  href={`/patients/${surgeryCase.patientId}`}
                  className="flex items-center gap-1.5 text-xs font-medium text-[#3d7ab5] hover:underline"
                >
                  <User className="w-3.5 h-3.5" />
                  ملف المريض
                </Link>
              </div>

              <dl className="grid grid-cols-1 sm:grid-cols-2 gap-x-8 gap-y-4 text-sm">
                <div>
                  <dt className="text-xs text-gray-400 mb-0.5">رقم الحالة</dt>
                  <dd className="font-mono font-semibold text-[#3d7ab5]">{surgeryCase.caseNumber}</dd>
                </div>
                <div>
                  <dt className="text-xs text-gray-400 mb-0.5">نوع الجراحة</dt>
                  <dd className="text-gray-800">{surgeryCase.surgeryType}</dd>
                </div>
                <div>
                  <dt className="text-xs text-gray-400 mb-0.5">الأسنان المعنية</dt>
                  <dd className="font-mono text-gray-700">
                    {surgeryCase.teethInvolved && surgeryCase.teethInvolved.length > 0
                      ? surgeryCase.teethInvolved.join(", ")
                      : "—"}
                  </dd>
                </div>
                <div>
                  <dt className="text-xs text-gray-400 mb-0.5">الطبيب الجراح</dt>
                  <dd className="text-gray-800">
                    {surgeryCase.doctorName ? (
                      <div className="flex items-center gap-1.5">
                        <div
                          className="w-2 h-2 rounded-full"
                          style={{ backgroundColor: surgeryCase.doctorColor ?? "#2563EB" }}
                        />
                        {surgeryCase.doctorName}
                      </div>
                    ) : (
                      "—"
                    )}
                  </dd>
                </div>
                <div>
                  <dt className="text-xs text-gray-400 mb-0.5">الحالة</dt>
                  <dd>
                    <span
                      className={cn(
                        "text-xs px-2 py-0.5 rounded-full font-medium",
                        SURGERY_STATUS_COLORS[surgeryCase.status] ?? "bg-gray-100 text-gray-600"
                      )}
                    >
                      {SURGERY_STATUS_LABELS[surgeryCase.status] ?? surgeryCase.status}
                    </span>
                  </dd>
                </div>
                <div>
                  <dt className="text-xs text-gray-400 mb-0.5">تاريخ الإنشاء</dt>
                  <dd className="text-gray-800">{formatArabicDate(surgeryCase.createdAt)}</dd>
                </div>
              </dl>

              <div className="flex justify-end pt-2 border-t border-[#e8f0f9]">
                <Link
                  href={`/surgery/${id}/edit`}
                  className={cn(primaryBtnCls)}
                >
                  <Pencil className="w-4 h-4" />
                  تعديل الحالة
                </Link>
              </div>
            </div>
          )}

          {/* ══════════════════════════════════════════════════════════════════ */}
          {/* TAB: Preop — ما قبل الجراحة                                    */}
          {/* ══════════════════════════════════════════════════════════════════ */}
          {activeTab === "preop" && (
            <div className="space-y-5">
              <h2 className="text-sm font-semibold text-gray-700">ما قبل الجراحة</h2>

              {/* Basic fields */}
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
                    className={selectCls}
                  >
                    <option value="">اختر طبيباً...</option>
                    {doctors.map((d) => (
                      <option key={d.id} value={d.id}>{d.name}</option>
                    ))}
                  </select>
                </div>
              </div>

              {/* Consent checkbox */}
              <div className="flex items-center gap-2">
                <input
                  id="consent"
                  type="checkbox"
                  checked={preop.consentSigned ?? false}
                  onChange={(e) => setPreop((p) => ({ ...p, consentSigned: e.target.checked }))}
                  className="w-4 h-4 rounded border-gray-300 text-[#3d7ab5] focus:ring-[#3d7ab5]"
                />
                <label htmlFor="consent" className="text-sm text-gray-700 cursor-pointer">
                  تم توقيع نموذج الموافقة على الجراحة
                </label>
              </div>

              {/* Checklist section */}
              <div>
                <h3 className="text-sm font-semibold text-gray-700 mb-3 flex items-center gap-2">
                  <CheckCircle className="w-4 h-4 text-[#3d7ab5]" />
                  قائمة التحضيرات
                </h3>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                  {PREOP_CHECKLIST_ITEMS.map((item) => (
                    <label
                      key={item.key}
                      className={cn(
                        "flex items-center gap-2 px-3 py-2 rounded-lg border cursor-pointer transition text-sm",
                        preop.checklist?.[item.key]
                          ? "border-[#3d7ab5] bg-[#3d7ab508] text-[#3d7ab5] font-medium"
                          : "border-gray-200 text-gray-600 hover:border-gray-300"
                      )}
                    >
                      <input
                        type="checkbox"
                        checked={preop.checklist?.[item.key] ?? false}
                        onChange={() => toggleChecklist(item.key)}
                        className="w-4 h-4 rounded border-gray-300 text-[#3d7ab5] focus:ring-[#3d7ab5]"
                      />
                      {item.label}
                    </label>
                  ))}
                </div>
              </div>

              {/* Required tests section */}
              <div>
                <h3 className="text-sm font-semibold text-gray-700 mb-3 flex items-center gap-2">
                  <FlaskConical className="w-4 h-4 text-[#3d7ab5]" />
                  الفحوصات المطلوبة
                </h3>

                <div className="flex items-center gap-2 mb-3">
                  <input
                    type="text"
                    value={newTest}
                    onChange={(e) => setNewTest(e.target.value)}
                    onKeyDown={(e) => { if (e.key === "Enter") { e.preventDefault(); addTest(); } }}
                    placeholder="اسم الفحص..."
                    className={inputCls}
                  />
                  <button
                    type="button"
                    onClick={addTest}
                    disabled={!newTest.trim()}
                    className="flex items-center gap-1 px-3 py-2 text-sm font-medium rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] disabled:opacity-50 transition flex-shrink-0"
                  >
                    <Plus className="w-4 h-4" />
                    إضافة
                  </button>
                </div>

                {preop.requiredTests && preop.requiredTests.length > 0 && (
                  <div className="flex flex-wrap gap-2">
                    {preop.requiredTests.map((test, idx) => (
                      <span
                        key={idx}
                        className="inline-flex items-center gap-1 px-3 py-1 rounded-full text-xs font-medium bg-[#3d7ab518] text-[#3d7ab5] border border-[#3d7ab530]"
                      >
                        {test}
                        <button
                          type="button"
                          onClick={() => removeTest(idx)}
                          className="hover:text-red-500 transition"
                        >
                          <X className="w-3 h-3" />
                        </button>
                      </span>
                    ))}
                  </div>
                )}

                {(!preop.requiredTests || preop.requiredTests.length === 0) && (
                  <p className="text-xs text-gray-400">لم يتم إضافة فحوصات بعد</p>
                )}
              </div>

              {/* Save button */}
              <div className="flex items-center justify-end gap-3 pt-3 border-t border-[#e8f0f9]">
                {preopSaved && (
                  <span className="flex items-center gap-1 text-xs text-green-600">
                    <CheckCircle className="w-3.5 h-3.5" />
                    تم الحفظ
                  </span>
                )}
                <button onClick={savePreop} disabled={savingPreop} className={primaryBtnCls}>
                  {savingPreop ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
                  {savingPreop ? "جارٍ الحفظ..." : "حفظ"}
                </button>
              </div>
            </div>
          )}

          {/* ══════════════════════════════════════════════════════════════════ */}
          {/* TAB: Operative — تقرير الجراحة                                  */}
          {/* ══════════════════════════════════════════════════════════════════ */}
          {activeTab === "operative" && (
            <div className="space-y-5">
              <div className="flex items-center justify-between">
                <h2 className="text-sm font-semibold text-gray-700">تقرير الجراحة</h2>
                {operativeLoaded && !operativeLoaded.approvedAt && (
                  <button
                    onClick={approveOperative}
                    disabled={approvingOperative}
                    className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-lg bg-green-600 text-white hover:bg-green-700 disabled:opacity-60 transition"
                  >
                    {approvingOperative ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <ShieldCheck className="w-3.5 h-3.5" />}
                    اعتماد التقرير
                  </button>
                )}
                {operativeLoaded?.approvedAt && (
                  <span className="flex items-center gap-1 text-xs text-green-600 font-medium">
                    <ShieldCheck className="w-3.5 h-3.5" />
                    تم الاعتماد — {formatArabicDate(operativeLoaded.approvedAt)}
                  </span>
                )}
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">تاريخ ووقت الجراحة</label>
                  <input
                    type="datetime-local"
                    value={operative.surgeryDateTime ?? ""}
                    onChange={(e) => setOperative((p) => ({ ...p, surgeryDateTime: e.target.value || undefined }))}
                    className={inputCls}
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">مدة الجراحة (دقائق)</label>
                  <input
                    type="number"
                    min={0}
                    value={operative.durationMinutes ?? ""}
                    onChange={(e) => setOperative((p) => ({ ...p, durationMinutes: e.target.value ? Number(e.target.value) : undefined }))}
                    className={inputCls}
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">التخدير المستخدم</label>
                  <input
                    type="text"
                    value={operative.anesthesiaUsed ?? ""}
                    onChange={(e) => setOperative((p) => ({ ...p, anesthesiaUsed: e.target.value || undefined }))}
                    placeholder="مثلاً: تخدير موضعي ليدوكايين"
                    className={inputCls}
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">التقنية</label>
                  <input
                    type="text"
                    value={operative.technique ?? ""}
                    onChange={(e) => setOperative((p) => ({ ...p, technique: e.target.value || undefined }))}
                    placeholder="مثلاً: قلع جراحي بالرفع"
                    className={inputCls}
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">عدد الغرز</label>
                  <input
                    type="number"
                    min={0}
                    value={operative.suturesCount ?? ""}
                    onChange={(e) => setOperative((p) => ({ ...p, suturesCount: e.target.value ? Number(e.target.value) : undefined }))}
                    className={inputCls}
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">الطبيب الجراح</label>
                  <select
                    value={operative.doctorId ?? ""}
                    onChange={(e) => setOperative((p) => ({ ...p, doctorId: e.target.value || undefined }))}
                    className={selectCls}
                  >
                    <option value="">اختر طبيباً...</option>
                    {doctors.map((d) => (
                      <option key={d.id} value={d.id}>{d.name}</option>
                    ))}
                  </select>
                </div>
              </div>

              {/* Detailed description */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1.5">الوصف التفصيلي</label>
                <textarea
                  value={operative.detailedDescription ?? ""}
                  onChange={(e) => setOperative((p) => ({ ...p, detailedDescription: e.target.value || undefined }))}
                  rows={5}
                  placeholder="وصف تفصيلي للإجراء الجراحي..."
                  className={textareaCls}
                />
              </div>

              {/* Outcome */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1.5">نتيجة الجراحة</label>
                <select
                  value={operative.outcome ?? ""}
                  onChange={(e) => setOperative((p) => ({ ...p, outcome: e.target.value || undefined }))}
                  className={selectCls}
                >
                  <option value="">اختر النتيجة...</option>
                  {OUTCOME_OPTIONS.map((opt) => (
                    <option key={opt} value={opt}>{opt}</option>
                  ))}
                </select>
              </div>

              {/* Complications */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1.5">المضاعفات</label>
                <textarea
                  value={operative.complications ?? ""}
                  onChange={(e) => setOperative((p) => ({ ...p, complications: e.target.value || undefined }))}
                  rows={3}
                  placeholder="وصف أي مضاعفات..."
                  className={textareaCls}
                />
              </div>

              {/* Specimen checkbox */}
              <div className="flex items-center gap-2">
                <input
                  id="specimen"
                  type="checkbox"
                  checked={operative.specimenSent ?? false}
                  onChange={(e) => setOperative((p) => ({ ...p, specimenSent: e.target.checked }))}
                  className="w-4 h-4 rounded border-gray-300 text-[#3d7ab5] focus:ring-[#3d7ab5]"
                />
                <label htmlFor="specimen" className="text-sm text-gray-700 cursor-pointer">
                  تم إرسال عينة إلى المختبر
                </label>
              </div>

              {/* Save button */}
              <div className="flex items-center justify-end gap-3 pt-3 border-t border-[#e8f0f9]">
                {operativeSaved && (
                  <span className="flex items-center gap-1 text-xs text-green-600">
                    <CheckCircle className="w-3.5 h-3.5" />
                    تم الحفظ
                  </span>
                )}
                <button onClick={saveOperative} disabled={savingOperative} className={primaryBtnCls}>
                  {savingOperative ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
                  {savingOperative ? "جارٍ الحفظ..." : "حفظ التقرير"}
                </button>
              </div>
            </div>
          )}

          {/* ══════════════════════════════════════════════════════════════════ */}
          {/* TAB: Postop — ما بعد الجراحة                                    */}
          {/* ══════════════════════════════════════════════════════════════════ */}
          {activeTab === "postop" && (
            <div className="space-y-5">
              <h2 className="text-sm font-semibold text-gray-700">ما بعد الجراحة</h2>

              {/* Instructions */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1.5">تعليمات ما بعد الجراحة</label>
                <textarea
                  value={postop.instructions ?? ""}
                  onChange={(e) => setPostop((p) => ({ ...p, instructions: e.target.value || undefined }))}
                  rows={6}
                  placeholder="أدخل التعليمات والإرشادات للمريض بعد الجراحة..."
                  className={textareaCls}
                />
              </div>

              {/* Prescription section */}
              <div>
                <div className="flex items-center justify-between mb-3">
                  <h3 className="text-sm font-semibold text-gray-700 flex items-center gap-2">
                    <Pill className="w-4 h-4 text-[#3d7ab5]" />
                    الوصفة الطبية
                  </h3>
                  <button
                    type="button"
                    onClick={addPrescription}
                    className="flex items-center gap-1 px-3 py-1.5 text-xs font-medium rounded-lg border border-[#3d7ab5] text-[#3d7ab5] hover:bg-[#eef3f9] transition"
                  >
                    <Plus className="w-3.5 h-3.5" />
                    إضافة دواء
                  </button>
                </div>

                {postop.prescription && postop.prescription.length > 0 ? (
                  <div className="space-y-3">
                    {postop.prescription.map((rx, idx) => (
                      <div
                        key={idx}
                        className="relative bg-gray-50 rounded-lg border border-gray-200 p-3 space-y-2"
                      >
                        <button
                          type="button"
                          onClick={() => removePrescription(idx)}
                          className="absolute top-2 left-2 p-1 text-gray-400 hover:text-red-500 transition"
                        >
                          <Trash2 className="w-3.5 h-3.5" />
                        </button>
                        <span className="text-xs text-gray-400 font-medium">دواء #{idx + 1}</span>
                        <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                          <input
                            type="text"
                            value={rx.medicine}
                            onChange={(e) => updatePrescription(idx, "medicine", e.target.value)}
                            placeholder="اسم الدواء"
                            className={inputCls}
                          />
                          <input
                            type="text"
                            value={rx.dosage}
                            onChange={(e) => updatePrescription(idx, "dosage", e.target.value)}
                            placeholder="الجرعة"
                            className={inputCls}
                          />
                          <input
                            type="text"
                            value={rx.frequency}
                            onChange={(e) => updatePrescription(idx, "frequency", e.target.value)}
                            placeholder="التكرار (مثلاً: 3 مرات يومياً)"
                            className={inputCls}
                          />
                          <input
                            type="text"
                            value={rx.duration}
                            onChange={(e) => updatePrescription(idx, "duration", e.target.value)}
                            placeholder="المدة (مثلاً: 5 أيام)"
                            className={inputCls}
                          />
                        </div>
                      </div>
                    ))}
                  </div>
                ) : (
                  <p className="text-xs text-gray-400 py-2">لم يتم إضافة أدوية بعد</p>
                )}
              </div>

              {/* Followup schedule */}
              <div>
                <div className="flex items-center justify-between mb-3">
                  <h3 className="text-sm font-semibold text-gray-700 flex items-center gap-2">
                    <Calendar className="w-4 h-4 text-[#3d7ab5]" />
                    جدول المتابعة
                  </h3>
                  <button
                    type="button"
                    onClick={addFollowup}
                    className="flex items-center gap-1 px-3 py-1.5 text-xs font-medium rounded-lg border border-[#3d7ab5] text-[#3d7ab5] hover:bg-[#eef3f9] transition"
                  >
                    <Plus className="w-3.5 h-3.5" />
                    إضافة موعد متابعة
                  </button>
                </div>

                {postop.followupSchedule && postop.followupSchedule.length > 0 ? (
                  <div className="space-y-3">
                    {postop.followupSchedule.map((fu, idx) => (
                      <div
                        key={idx}
                        className="relative bg-gray-50 rounded-lg border border-gray-200 p-3 space-y-2"
                      >
                        <button
                          type="button"
                          onClick={() => removeFollowup(idx)}
                          className="absolute top-2 left-2 p-1 text-gray-400 hover:text-red-500 transition"
                        >
                          <Trash2 className="w-3.5 h-3.5" />
                        </button>
                        <span className="text-xs text-gray-400 font-medium">متابعة #{idx + 1}</span>
                        <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                          <input
                            type="date"
                            value={fu.date}
                            onChange={(e) => updateFollowup(idx, "date", e.target.value)}
                            className={inputCls}
                          />
                          <input
                            type="text"
                            value={fu.notes ?? ""}
                            onChange={(e) => updateFollowup(idx, "notes", e.target.value)}
                            placeholder="ملاحظات..."
                            className={inputCls}
                          />
                        </div>
                      </div>
                    ))}
                  </div>
                ) : (
                  <p className="text-xs text-gray-400 py-2">لم يتم إضافة مواعيد متابعة بعد</p>
                )}
              </div>

              {/* Save button */}
              <div className="flex items-center justify-end gap-3 pt-3 border-t border-[#e8f0f9]">
                {postopSaved && (
                  <span className="flex items-center gap-1 text-xs text-green-600">
                    <CheckCircle className="w-3.5 h-3.5" />
                    تم الحفظ
                  </span>
                )}
                <button onClick={savePostop} disabled={savingPostop} className={primaryBtnCls}>
                  {savingPostop ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
                  {savingPostop ? "جارٍ الحفظ..." : "حفظ"}
                </button>
              </div>
            </div>
          )}

          {/* ══════════════════════════════════════════════════════════════════ */}
          {/* TAB: Referrals — إحالات المستشفيات                               */}
          {/* ══════════════════════════════════════════════════════════════════ */}
          {activeTab === "referrals" && (
            <div className="space-y-5">
              <h2 className="text-sm font-semibold text-gray-700">إحالات المستشفيات</h2>

              {/* ── Create / Edit form ────────────────────────────────────────── */}
              <div className="bg-gray-50 rounded-lg border border-gray-200 p-4 space-y-3">
                <h3 className="text-xs font-semibold text-gray-500">
                  {editingReferralId ? "تعديل الإحالة" : "إحالة جديدة"}
                </h3>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <div>
                    <label className="block text-xs font-medium text-gray-600 mb-1">اسم المستشفى</label>
                    <input
                      type="text"
                      value={referralForm.hospitalName ?? ""}
                      onChange={(e) => setReferralForm((f) => ({ ...f, hospitalName: e.target.value || undefined }))}
                      placeholder="اسم المستشفى"
                      className={inputCls}
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-600 mb-1">تاريخ الإحالة</label>
                    <input
                      type="date"
                      value={referralForm.referralDate ?? ""}
                      onChange={(e) => setReferralForm((f) => ({ ...f, referralDate: e.target.value || undefined }))}
                      className={inputCls}
                    />
                  </div>
                  <div className="sm:col-span-2">
                    <label className="block text-xs font-medium text-gray-600 mb-1">السبب</label>
                    <input
                      type="text"
                      value={referralForm.reason ?? ""}
                      onChange={(e) => setReferralForm((f) => ({ ...f, reason: e.target.value || undefined }))}
                      placeholder="سبب الإحالة"
                      className={inputCls}
                    />
                  </div>
                  <div className="sm:col-span-2">
                    <label className="block text-xs font-medium text-gray-600 mb-1">ملاحظات</label>
                    <textarea
                      value={referralForm.notes ?? ""}
                      onChange={(e) => setReferralForm((f) => ({ ...f, notes: e.target.value || undefined }))}
                      rows={2}
                      placeholder="ملاحظات إضافية..."
                      className={textareaCls}
                    />
                  </div>
                </div>

                <div className="flex items-center gap-2 justify-end">
                  {editingReferralId && (
                    <button
                      type="button"
                      onClick={resetReferralForm}
                      className="px-3 py-1.5 text-xs font-medium rounded-lg border border-gray-300 text-gray-600 hover:bg-gray-100 transition"
                    >
                      إلغاء التعديل
                    </button>
                  )}
                  <button
                    type="button"
                    onClick={handleSaveReferral}
                    disabled={savingReferral}
                    className={cn(primaryBtnCls, "text-xs")}
                  >
                    {savingReferral ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Save className="w-3.5 h-3.5" />}
                    {editingReferralId ? "تحديث الإحالة" : "إنشاء إحالة"}
                  </button>
                </div>
              </div>

              {/* ── Referrals list ────────────────────────────────────────────── */}
              {referralsLoading ? (
                <div className="space-y-3 animate-pulse">
                  {[1, 2].map((i) => (
                    <div key={i} className="h-24 bg-gray-100 rounded-lg" />
                  ))}
                </div>
              ) : referrals.length === 0 ? (
                <div className="flex flex-col items-center justify-center py-12 text-gray-400">
                  <ArrowRightLeft className="w-10 h-10 mb-2 opacity-30" />
                  <p className="text-sm">لا توجد إحالات لهذه الحالة الجراحية</p>
                </div>
              ) : (
                <div className="space-y-3">
                  {referrals.map((ref) => (
                    <div
                      key={ref.id}
                      className="bg-white rounded-lg border border-gray-200 p-4 space-y-2 hover:border-[#e8f0f9] transition"
                    >
                      <div className="flex items-start justify-between gap-3">
                        <div className="space-y-1 min-w-0">
                          <div className="flex items-center gap-2 flex-wrap">
                            <span className="text-sm font-semibold text-gray-800">{ref.hospitalName ?? "—"}</span>
                            <span
                              className={cn(
                                "text-[10px] px-2 py-0.5 rounded-full font-medium",
                                REFERRAL_STATUS_COLORS[ref.status] ?? "bg-gray-100 text-gray-600"
                              )}
                            >
                              {REFERRAL_STATUS_LABELS[ref.status] ?? ref.status}
                            </span>
                          </div>
                          {ref.reason && (
                            <p className="text-xs text-gray-600">{ref.reason}</p>
                          )}
                          {ref.referralDate && (
                            <p className="text-xs text-gray-400 flex items-center gap-1">
                              <Calendar className="w-3 h-3" />
                              {formatArabicDate(ref.referralDate)}
                            </p>
                          )}
                          {ref.notes && (
                            <p className="text-xs text-gray-500 mt-1 bg-gray-50 rounded px-2 py-1">{ref.notes}</p>
                          )}
                        </div>
                      </div>

                      {/* Actions row */}
                      <div className="flex items-center gap-2 pt-2 border-t border-gray-100 flex-wrap">
                        <select
                          value={ref.status}
                          onChange={(e) => handleReferralStatusChange(ref.id, e.target.value as HospitalReferral["status"])}
                          className="text-xs px-2 py-1 rounded-lg border border-gray-200 bg-white focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]"
                        >
                          <option value="pending">قيد الانتظار</option>
                          <option value="in_progress">قيد التنفيذ</option>
                          <option value="completed">مكتملة</option>
                          <option value="cancelled">ملغاة</option>
                        </select>
                        <button
                          type="button"
                          onClick={() => handleEditReferral(ref)}
                          className="flex items-center gap-1 px-2 py-1 text-xs font-medium rounded-lg text-[#3d7ab5] hover:bg-[#eef3f9] transition"
                        >
                          <Pencil className="w-3 h-3" />
                          تعديل
                        </button>
                        <button
                          type="button"
                          onClick={() => handleDeleteReferral(ref.id)}
                          className={dangerBtnCls}
                        >
                          <Trash2 className="w-3 h-3" />
                          حذف
                        </button>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
