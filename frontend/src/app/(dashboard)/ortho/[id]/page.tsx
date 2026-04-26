"use client";
import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { GitBranch, User, Calendar, Wallet, ClipboardList, Activity, ClipboardCheck } from "lucide-react";
import type { OrthoCase, TreatmentStage, OrthoVisit } from "@/types/ortho";
import api from "@/lib/api";
import { cn, formatYemeniRiyal, formatArabicDate } from "@/lib/utils";
import { TreatmentStagesPanel } from "@/components/ortho/TreatmentStagesPanel";
import { OrthoVisitTimeline } from "@/components/ortho/OrthoVisitTimeline";

type Tab = "info" | "exam" | "stages" | "visits" | "finance";

const TABS: { key: Tab; label: string; icon: typeof User }[] = [
  { key: "info",    label: "المعلومات",     icon: User },
  { key: "exam",    label: "الفحص السريري", icon: ClipboardCheck },
  { key: "stages",  label: "مراحل العلاج", icon: GitBranch },
  { key: "visits",  label: "سجل الزيارات", icon: Calendar },
  { key: "finance", label: "العقد المالي",  icon: Wallet },
];

interface ClinicalExam {
  examDate?: string;
  facialSymmetry?: string;
  profile?: string;
  lipsCompetence?: boolean;
  smileLine?: string;
  verticalProportion?: string;
  molarRelation?: string;
  canineRelation?: string;
  overjet?: number;
  overbite?: number;
  crossbite?: boolean;
  openBite?: boolean;
  upperCrowding?: string;
  lowerCrowding?: string;
  upperSpacing?: number;
  midlineUpper?: string;
  midlineLower?: string;
  coCrDiscrepancy?: boolean;
  tmjFindings?: string;
  habits?: string;
  notes?: string;
  doctorId?: string;
}

function ClinicalExamTab({ caseId, initial }: { caseId: string; initial: ClinicalExam | null }) {
  const inputCls = "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal";
  const [form, setForm] = useState<ClinicalExam>(initial ?? {});
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  const set = <K extends keyof ClinicalExam>(key: K, value: ClinicalExam[K]) => {
    setForm((prev) => ({ ...prev, [key]: value }));
    setSaved(false);
  };

  const handleSave = async () => {
    setSaving(true);
    try {
      await api.put(`/api/ortho-cases/${caseId}/clinical-exam`, {
        examDate:           form.examDate,
        facialSymmetry:     form.facialSymmetry,
        profile:            form.profile,
        lipsCompetence:     form.lipsCompetence,
        smileLine:          form.smileLine,
        verticalProportion: form.verticalProportion,
        molarRelation:      form.molarRelation,
        canineRelation:     form.canineRelation,
        overjet:            form.overjet,
        overbite:           form.overbite,
        crossbite:          form.crossbite ?? false,
        openBite:           form.openBite ?? false,
        upperCrowding:      form.upperCrowding,
        lowerCrowding:      form.lowerCrowding,
        upperSpacing:       form.upperSpacing,
        midlineUpper:       form.midlineUpper,
        midlineLower:       form.midlineLower,
        coCrDiscrepancy:    form.coCrDiscrepancy,
        tmjFindings:        form.tmjFindings,
        habits:             form.habits,
        notes:              form.notes,
        doctorId:           form.doctorId,
      });
      setSaved(true);
    } catch {
      // handle silently
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-6">
      {/* Section: خارج الفم */}
      <div>
        <h3 className="text-sm font-semibold text-gray-700 mb-3 border-b border-gray-100 pb-1">خارج الفم</h3>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          <div>
            <label className="block text-xs text-gray-500 mb-1">تاريخ الفحص</label>
            <input
              type="date"
              className={inputCls}
              value={form.examDate ?? ""}
              onChange={(e) => set("examDate", e.target.value)}
            />
          </div>
          <div>
            <label className="block text-xs text-gray-500 mb-1">التماثل الوجهي</label>
            <select className={inputCls} value={form.facialSymmetry ?? ""} onChange={(e) => set("facialSymmetry", e.target.value || undefined)}>
              <option value="">— اختر —</option>
              <option value="متماثل">متماثل</option>
              <option value="غير متماثل">غير متماثل</option>
            </select>
          </div>
          <div>
            <label className="block text-xs text-gray-500 mb-1">الملف</label>
            <select className={inputCls} value={form.profile ?? ""} onChange={(e) => set("profile", e.target.value || undefined)}>
              <option value="">— اختر —</option>
              <option value="Class I">Class I</option>
              <option value="Convex">Convex</option>
              <option value="Concave">Concave</option>
            </select>
          </div>
          <div>
            <label className="block text-xs text-gray-500 mb-1">خط الابتسامة</label>
            <select className={inputCls} value={form.smileLine ?? ""} onChange={(e) => set("smileLine", e.target.value || undefined)}>
              <option value="">— اختر —</option>
              <option value="منخفض">منخفض</option>
              <option value="متوسط">متوسط</option>
              <option value="مرتفع">مرتفع</option>
            </select>
          </div>
          <div>
            <label className="block text-xs text-gray-500 mb-1">التناسب الرأسي</label>
            <select className={inputCls} value={form.verticalProportion ?? ""} onChange={(e) => set("verticalProportion", e.target.value || undefined)}>
              <option value="">— اختر —</option>
              <option value="طبيعي">طبيعي</option>
              <option value="قصير">قصير</option>
              <option value="طويل">طويل</option>
            </select>
          </div>
          <div className="flex items-center gap-2 pt-5">
            <input
              id="lipsCompetence"
              type="checkbox"
              className="w-4 h-4 accent-clinic-teal"
              checked={form.lipsCompetence ?? false}
              onChange={(e) => set("lipsCompetence", e.target.checked)}
            />
            <label htmlFor="lipsCompetence" className="text-sm text-gray-700">كفاءة الشفاه</label>
          </div>
        </div>
      </div>

      {/* Section: داخل الفم */}
      <div>
        <h3 className="text-sm font-semibold text-gray-700 mb-3 border-b border-gray-100 pb-1">داخل الفم</h3>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          <div>
            <label className="block text-xs text-gray-500 mb-1">علاقة الرحى</label>
            <select className={inputCls} value={form.molarRelation ?? ""} onChange={(e) => set("molarRelation", e.target.value || undefined)}>
              <option value="">— اختر —</option>
              <option value="Class I">Class I</option>
              <option value="Class II">Class II</option>
              <option value="Class III">Class III</option>
            </select>
          </div>
          <div>
            <label className="block text-xs text-gray-500 mb-1">علاقة الناب</label>
            <select className={inputCls} value={form.canineRelation ?? ""} onChange={(e) => set("canineRelation", e.target.value || undefined)}>
              <option value="">— اختر —</option>
              <option value="Class I">Class I</option>
              <option value="Class II">Class II</option>
              <option value="Class III">Class III</option>
            </select>
          </div>
          <div>
            <label className="block text-xs text-gray-500 mb-1">Overjet (mm)</label>
            <input
              type="number"
              step="0.1"
              className={inputCls}
              value={form.overjet ?? ""}
              onChange={(e) => set("overjet", e.target.value ? parseFloat(e.target.value) : undefined)}
            />
          </div>
          <div>
            <label className="block text-xs text-gray-500 mb-1">Overbite (mm)</label>
            <input
              type="number"
              step="0.1"
              className={inputCls}
              value={form.overbite ?? ""}
              onChange={(e) => set("overbite", e.target.value ? parseFloat(e.target.value) : undefined)}
            />
          </div>
          <div>
            <label className="block text-xs text-gray-500 mb-1">تكدس علوي</label>
            <select className={inputCls} value={form.upperCrowding ?? ""} onChange={(e) => set("upperCrowding", e.target.value || undefined)}>
              <option value="">— اختر —</option>
              <option value="لا يوجد">لا يوجد</option>
              <option value="خفيف">خفيف</option>
              <option value="متوسط">متوسط</option>
              <option value="شديد">شديد</option>
            </select>
          </div>
          <div>
            <label className="block text-xs text-gray-500 mb-1">تكدس سفلي</label>
            <select className={inputCls} value={form.lowerCrowding ?? ""} onChange={(e) => set("lowerCrowding", e.target.value || undefined)}>
              <option value="">— اختر —</option>
              <option value="لا يوجد">لا يوجد</option>
              <option value="خفيف">خفيف</option>
              <option value="متوسط">متوسط</option>
              <option value="شديد">شديد</option>
            </select>
          </div>
          <div>
            <label className="block text-xs text-gray-500 mb-1">الخط المتوسط العلوي</label>
            <input type="text" className={inputCls} value={form.midlineUpper ?? ""} onChange={(e) => set("midlineUpper", e.target.value || undefined)} />
          </div>
          <div>
            <label className="block text-xs text-gray-500 mb-1">الخط المتوسط السفلي</label>
            <input type="text" className={inputCls} value={form.midlineLower ?? ""} onChange={(e) => set("midlineLower", e.target.value || undefined)} />
          </div>
          <div className="flex items-center gap-4 pt-5">
            <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
              <input
                type="checkbox"
                className="w-4 h-4 accent-clinic-teal"
                checked={form.crossbite ?? false}
                onChange={(e) => set("crossbite", e.target.checked)}
              />
              تقاطع عكسي
            </label>
            <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
              <input
                type="checkbox"
                className="w-4 h-4 accent-clinic-teal"
                checked={form.openBite ?? false}
                onChange={(e) => set("openBite", e.target.checked)}
              />
              لدغة مفتوحة
            </label>
          </div>
        </div>
      </div>

      {/* Section: وظيفي */}
      <div>
        <h3 className="text-sm font-semibold text-gray-700 mb-3 border-b border-gray-100 pb-1">وظيفي</h3>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="flex items-center gap-2">
            <input
              id="coCrDiscrepancy"
              type="checkbox"
              className="w-4 h-4 accent-clinic-teal"
              checked={form.coCrDiscrepancy ?? false}
              onChange={(e) => set("coCrDiscrepancy", e.target.checked)}
            />
            <label htmlFor="coCrDiscrepancy" className="text-sm text-gray-700">تناقض CO/CR</label>
          </div>
          <div>
            <label className="block text-xs text-gray-500 mb-1">مشاكل TMJ</label>
            <textarea
              rows={2}
              className={inputCls}
              value={form.tmjFindings ?? ""}
              onChange={(e) => set("tmjFindings", e.target.value || undefined)}
            />
          </div>
          <div>
            <label className="block text-xs text-gray-500 mb-1">العادات</label>
            <textarea
              rows={2}
              className={inputCls}
              value={form.habits ?? ""}
              onChange={(e) => set("habits", e.target.value || undefined)}
            />
          </div>
          <div>
            <label className="block text-xs text-gray-500 mb-1">ملاحظات</label>
            <textarea
              rows={2}
              className={inputCls}
              value={form.notes ?? ""}
              onChange={(e) => set("notes", e.target.value || undefined)}
            />
          </div>
        </div>
      </div>

      {/* Save button */}
      <div className="flex items-center gap-3 pt-2">
        <button
          onClick={handleSave}
          disabled={saving}
          className="px-5 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 transition disabled:opacity-50"
        >
          {saving ? "جاري الحفظ..." : "حفظ الفحص"}
        </button>
        {saved && (
          <span className="text-sm text-teal-600 font-medium">تم الحفظ بنجاح</span>
        )}
      </div>
    </div>
  );
}

export default function OrthoCaseDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [orthoCase, setOrthoCase] = useState<OrthoCase | null>(null);
  const [visits, setVisits] = useState<OrthoVisit[]>([]);
  const [clinicalExam, setClinicalExam] = useState<ClinicalExam | null>(null);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<Tab>("info");

  useEffect(() => {
    Promise.all([
      api.get<OrthoCase>(`/api/ortho-cases/${id}`),
      api.get<OrthoVisit[]>(`/api/ortho-cases/${id}/visits`),
      api.get<ClinicalExam | null>(`/api/ortho-cases/${id}/clinical-exam`),
    ])
      .then(([caseRes, visitsRes, examRes]) => {
        setOrthoCase(caseRes.data);
        setVisits(visitsRes.data);
        setClinicalExam(examRes.data);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [id]);

  const handleStageUpdate = (updated: TreatmentStage) => {
    setOrthoCase((prev) =>
      prev
        ? {
            ...prev,
            stages: prev.stages?.map((s) => (s.id === updated.id ? updated : s)),
          }
        : prev
    );
  };

  if (loading) {
    return (
      <div className="space-y-4 animate-pulse">
        <div className="h-28 bg-gray-100 rounded-xl" />
        <div className="h-64 bg-gray-100 rounded-xl" />
      </div>
    );
  }

  if (!orthoCase) {
    return <div className="text-center py-20 text-gray-400">الحالة غير موجودة</div>;
  }

  const progress = orthoCase.stagePercentage;

  return (
    <div className="space-y-5 max-w-5xl">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Link href="/ortho" className="hover:text-clinic-teal transition">التقويم</Link>
        <span>/</span>
        <span className="text-gray-900 font-medium">{orthoCase.caseNumber}</span>
      </div>

      {/* Banner */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
        <div className="flex items-start justify-between gap-4 flex-wrap">
          <div className="flex items-start gap-4">
            <div className="w-12 h-12 rounded-xl flex items-center justify-center text-white flex-shrink-0"
              style={{ backgroundColor: orthoCase.doctorColor ?? "#0E7490" }}
            >
              <GitBranch className="w-6 h-6" />
            </div>
            <div>
              <div className="flex items-center gap-3 flex-wrap">
                <h1 className="text-xl font-extrabold text-gray-900">{orthoCase.patientName}</h1>
                <span className="font-mono text-xs bg-gray-100 px-2.5 py-1 rounded text-gray-600">
                  {orthoCase.caseNumber}
                </span>
                <span className={cn(
                  "text-xs px-2 py-0.5 rounded-full font-medium",
                  orthoCase.status === "active" ? "bg-teal-50 text-teal-700" : "bg-gray-100 text-gray-500"
                )}>
                  {orthoCase.status === "active" ? "نشطة" : orthoCase.status}
                </span>
              </div>
              <div className="mt-2 flex flex-wrap items-center gap-4 text-sm text-gray-500">
                {orthoCase.applianceType && (
                  <span className="flex items-center gap-1">
                    <ClipboardList className="w-3.5 h-3.5" />
                    {orthoCase.applianceType}
                  </span>
                )}
                {orthoCase.doctorName && (
                  <span>{orthoCase.doctorName}</span>
                )}
                {orthoCase.startDate && (
                  <span>بدأت: {formatArabicDate(orthoCase.startDate)}</span>
                )}
                {orthoCase.totalFee && (
                  <span className="font-mono">{formatYemeniRiyal(orthoCase.totalFee)}</span>
                )}
              </div>

              {/* Progress bar */}
              <div className="mt-3 flex items-center gap-3">
                <div className="flex-1 h-2 bg-gray-100 rounded-full overflow-hidden max-w-xs">
                  <div
                    className="h-full bg-clinic-teal rounded-full transition-all"
                    style={{ width: `${progress}%` }}
                  />
                </div>
                <span className="text-xs font-medium text-gray-600">{progress}% مكتمل</span>
              </div>
            </div>
          </div>
          <Link
            href={`/patients/${orthoCase.patientId}`}
            className="flex items-center gap-1.5 px-3 py-1.5 text-sm border border-gray-200 rounded-lg hover:bg-gray-50 transition text-gray-600"
          >
            <User className="w-3.5 h-3.5" />
            ملف المريض
          </Link>
        </div>
      </div>

      {/* Tabs */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
        <div className="flex border-b border-gray-100 overflow-x-auto">
          {TABS.map(({ key, label, icon: Icon }) => (
            <button
              key={key}
              onClick={() => setActiveTab(key)}
              className={cn(
                "flex items-center gap-2 px-5 py-3.5 text-sm font-medium whitespace-nowrap border-b-2 transition",
                activeTab === key
                  ? "border-clinic-teal text-clinic-teal"
                  : "border-transparent text-gray-500 hover:text-gray-900"
              )}
            >
              <Icon className="w-4 h-4" />
              {label}
            </button>
          ))}
        </div>

        <div className="p-5">
          {activeTab === "info" && (
            <div className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {[
                ["المريض", orthoCase.patientName],
                ["رقم الحالة", orthoCase.caseNumber],
                ["الطبيب", orthoCase.doctorName ?? "—"],
                ["الجهاز", orthoCase.applianceType ?? "—"],
                ["تاريخ البداية", orthoCase.startDate ? formatArabicDate(orthoCase.startDate) : "—"],
                ["المدة المتوقعة", orthoCase.expectedDurationMonths ? `${orthoCase.expectedDurationMonths} أشهر` : "—"],
                ["المرحلة الحالية", orthoCase.currentStage ?? "—"],
                ["قرار الخلع", orthoCase.extractionDecisionValue ?? "—"],
                ["خطة الاحتفاظ", orthoCase.retentionPlan ?? "—"],
                ["الرسوم", orthoCase.totalFee ? formatYemeniRiyal(orthoCase.totalFee) : "—"],
              ].map(([label, value]) => (
                <div key={label} className="border-b border-gray-50 pb-3 last:border-0">
                  <p className="text-xs text-gray-400 mb-0.5">{label}</p>
                  <p className="text-sm font-medium text-gray-900">{value}</p>
                </div>
              ))}
            </div>
            <div className="pt-2 border-t border-gray-100">
              <Link
                href={`/ceph/new?orthoCaseId=${id}`}
                className="inline-flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg border border-clinic-teal text-clinic-teal hover:bg-clinic-teal/10 transition"
              >
                <Activity className="w-4 h-4" />
                إنشاء تحليل سيفالومتري
              </Link>
            </div>
            </div>
          )}

          {activeTab === "exam" && (
            <ClinicalExamTab caseId={id} initial={clinicalExam} />
          )}

          {activeTab === "stages" && (
            <TreatmentStagesPanel
              caseId={id}
              stages={orthoCase.stages ?? []}
              onUpdate={handleStageUpdate}
            />
          )}

          {activeTab === "visits" && (
            <OrthoVisitTimeline
              caseId={id}
              visits={visits}
              onVisitAdded={(v) => setVisits([v, ...visits])}
            />
          )}

          {activeTab === "finance" && (
            <div className="text-center py-12 text-gray-400">
              <Wallet className="w-10 h-10 mx-auto mb-2 opacity-30" />
              <p className="text-sm">لإدارة العقد المالي</p>
              <Link
                href="/finance/contracts"
                className="mt-3 inline-flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 transition"
              >
                الذهاب إلى المالية
              </Link>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
