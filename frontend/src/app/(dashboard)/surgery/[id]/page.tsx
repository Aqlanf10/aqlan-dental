"use client";
import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import {
  Scissors, User, ArrowRight, CheckCircle, Clock, XCircle,
  PlayCircle, ClipboardList, FileText, Save, AlertCircle,
} from "lucide-react";
import api from "@/lib/api";
import { cn, formatArabicDate } from "@/lib/utils";

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

const inputCls =
  "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal";

type Tab = "info" | "preop" | "postop";

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

  useEffect(() => {
    Promise.all([
      api.get<SurgeryCase>(`/api/surgery-cases/${id}`),
      api.get<Doctor[]>("/api/doctors"),
      api.get<PreopReport | null>(`/api/surgery-cases/${id}/preop`),
      api.get<PostopRecord | null>(`/api/surgery-cases/${id}/postop`),
    ])
      .then(([caseRes, doctorsRes, preopRes, postopRes]) => {
        setSurgeryCase(caseRes.data);
        setDoctors(doctorsRes.data);
        if (preopRes.data) setPreop(preopRes.data);
        if (postopRes.data) setPostop(postopRes.data);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [id]);

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
      <div className="flex items-center gap-1 border-b border-gray-200">
        {(
          [
            { id: "info",   label: "معلومات الحالة", icon: ClipboardList },
            { id: "preop",  label: "ما قبل الجراحة", icon: FileText },
            { id: "postop", label: "ما بعد الجراحة", icon: CheckCircle },
          ] as { id: Tab; label: string; icon: typeof ClipboardList }[]
        ).map(({ id: tabId, label, icon: Icon }) => (
          <button
            key={tabId}
            onClick={() => setActiveTab(tabId)}
            className={cn(
              "flex items-center gap-1.5 px-4 py-2.5 text-sm font-medium border-b-2 transition -mb-px",
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
              className={cn(inputCls, "resize-y")}
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
    </div>
  );
}
