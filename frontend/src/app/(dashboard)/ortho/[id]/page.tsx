"use client";
import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { GitBranch, User, Calendar, Wallet, ClipboardList, Activity, ClipboardCheck, ListChecks, FileText, Plus, Trash2, Save, ShieldCheck, Camera, Ruler, Sparkles, Target } from "lucide-react";
import type { OrthoCase, TreatmentStage, OrthoVisit, TreatmentPlanDto, TreatmentPlanListDto, ModelAnalysisDto } from "@/types/ortho";
import type { Contract } from "@/types/finance";
import type { ProblemListSuggestion, ExtractionDecisionAnalysis } from "@/types/ai";
import api from "@/lib/api";
import { cn, formatYemeniRiyal, formatArabicDate } from "@/lib/utils";
import { TreatmentStagesPanel } from "@/components/ortho/TreatmentStagesPanel";
import { OrthoVisitTimeline } from "@/components/ortho/OrthoVisitTimeline";
import { RetentionTab } from "@/components/ortho/RetentionTab";
import { PhotosRadiographsTab } from "@/components/ortho/PhotosRadiographsTab";
import { TreatmentPlanAB } from "@/components/ortho/TreatmentPlanAB";
import { ModelAnalysisTab } from "@/components/ortho/ModelAnalysisTab";
import { AiCaseSummary } from "@/components/ortho/AiCaseSummary";
import { VtoModule } from "@/components/ceph/VtoModule";
import { useProblemListSuggestion, useExtractionDecision, useOrthoCaseDataForAi } from "@/hooks/useAiAdvisory";

type Tab = "info" | "exam" | "problems" | "plan" | "extraction" | "stages" | "visits" | "retention" | "photos" | "model" | "finance" | "summary" | "vto";

const TABS: { key: Tab; label: string; icon: typeof User }[] = [
  { key: "info",       label: "المعلومات",     icon: User },
  { key: "exam",       label: "الفحص السريري", icon: ClipboardCheck },
  { key: "problems",   label: "قائمة المشاكل", icon: ListChecks },
  { key: "plan",       label: "خطة العلاج",    icon: FileText },
  { key: "extraction", label: "قرار الخلع",    icon: Activity },
  { key: "stages",     label: "مراحل العلاج",  icon: GitBranch },
  { key: "visits",     label: "سجل الزيارات",  icon: Calendar },
  { key: "retention",  label: "الاحتفاظ",      icon: ShieldCheck },
  { key: "photos",     label: "الصور والأشعة",  icon: Camera },
  { key: "model",      label: "تحليل الموديل", icon: Ruler },
  { key: "finance",    label: "العقد المالي",   icon: Wallet },
  { key: "summary",    label: "ملخص AI",       icon: Sparkles },
  { key: "vto",         label: "VTO",           icon: Target },
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

// ─── AI Problem List Button ──────────────────────────────────────────────────
function AiProblemListButton({ caseId, onSuggestions }: { caseId: string; onSuggestions: (items: ProblemListSuggestion[]) => void }) {
  const suggestMutation = useProblemListSuggestion();
  const { data: caseData } = useOrthoCaseDataForAi(caseId);

  const handleSuggest = async () => {
    if (!caseData) return;
    suggestMutation.mutate(
      {
        clinicalExam: caseData.ClinicalExam,
        cephMeasurements: caseData.CephAnalysis?.Measurements,
        patientAge: caseData.OrthoCase?.PatientAge,
        patientGender: caseData.OrthoCase?.PatientGender,
      },
      {
        onSuccess: (data) => {
          if (data.suggestions) onSuggestions(data.suggestions);
        },
      }
    );
  };

  return (
    <button
      onClick={handleSuggest}
      disabled={suggestMutation.isPending || !caseData}
      className="text-xs px-3 py-1.5 rounded-lg bg-purple-600 text-white hover:bg-purple-700 disabled:opacity-50 transition flex items-center gap-1.5"
    >
      <Sparkles className="w-3 h-3" />
      {suggestMutation.isPending ? "جاري التحليل..." : "اقتراح المشاكل"}
    </button>
  );
}

// ─── Problem List Tab ─────────────────────────────────────────────────────────
interface ProblemItem { id: string; category: string; description: string; severity?: string; }

const PROBLEM_CATEGORIES = [
  { value: "skeletal", label: "هيكلية" },
  { value: "dental", label: "سنية" },
  { value: "soft_tissue", label: "أنسجة رخوة" },
  { value: "functional", label: "وظيفية" },
  { value: "space", label: "فراغات" },
];
const CATEGORY_LABELS: Record<string, string> = { skeletal:"هيكلية", dental:"سنية", soft_tissue:"أنسجة رخوة", functional:"وظيفية", space:"فراغات" };
const SEVERITY_COLORS: Record<string, string> = { mild:"bg-yellow-100 text-yellow-700", moderate:"bg-orange-100 text-orange-700", severe:"bg-red-100 text-red-700" };

function ProblemListTab({ caseId }: { caseId: string }) {
  const inputCls = "px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal";
  const [items, setItems] = useState<ProblemItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [form, setForm] = useState({ category: "skeletal", description: "", severity: "" });
  const [adding, setAdding] = useState(false);

  useEffect(() => {
    api.get<ProblemItem[]>(`/api/ortho-cases/${caseId}/problem-list`)
      .then((r) => setItems(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [caseId]);

  const handleAdd = async () => {
    if (!form.description.trim()) return;
    setAdding(true);
    try {
      const { data } = await api.post<ProblemItem>(`/api/ortho-cases/${caseId}/problem-list`, form);
      setItems((prev) => [...prev, data]);
      setForm({ category: "skeletal", description: "", severity: "" });
    } catch {} finally { setAdding(false); }
  };

  const handleDelete = async (itemId: string) => {
    try {
      await api.delete(`/api/ortho-cases/${caseId}/problem-list/${itemId}`);
      setItems((prev) => prev.filter((i) => i.id !== itemId));
    } catch {}
  };

  if (loading) return <div className="animate-pulse h-40 bg-gray-100 rounded-xl" />;

  return (
    <div className="space-y-4">
      {/* Add form */}
      <div className="bg-gray-50 rounded-xl border border-gray-200 p-4 space-y-3">
        <h3 className="text-sm font-semibold text-gray-700">إضافة مشكلة جديدة</h3>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
          <select value={form.category} onChange={(e) => setForm((f) => ({ ...f, category: e.target.value }))} className={inputCls}>
            {PROBLEM_CATEGORIES.map((c) => <option key={c.value} value={c.value}>{c.label}</option>)}
          </select>
          <input value={form.description} onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
            placeholder="وصف المشكلة..." className={`${inputCls} md:col-span-1`} />
          <select value={form.severity} onChange={(e) => setForm((f) => ({ ...f, severity: e.target.value }))} className={inputCls}>
            <option value="">الشدة (اختياري)</option>
            <option value="mild">خفيفة</option>
            <option value="moderate">متوسطة</option>
            <option value="severe">شديدة</option>
          </select>
        </div>
        <button onClick={handleAdd} disabled={adding || !form.description.trim()}
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60 transition"
        >
          <Plus className="w-4 h-4" />
          {adding ? "جاري الإضافة..." : "إضافة"}
        </button>
      </div>

      {/* AI Suggestion */}
      <div className="bg-purple-50 rounded-xl border border-purple-200 p-4">
        <div className="flex items-center justify-between mb-2">
          <div className="flex items-center gap-2">
            <Sparkles className="w-4 h-4 text-purple-600" />
            <h3 className="text-sm font-semibold text-purple-800">اقتراح ذكي</h3>
            <span className="text-[10px] px-1.5 py-0.5 bg-purple-100 text-purple-600 rounded-full">مقترح</span>
          </div>
          <AiProblemListButton caseId={caseId} onSuggestions={(suggestions) => {
            // Auto-add suggestions
            suggestions.forEach(async (s) => {
              try {
                const { data } = await api.post<ProblemItem>(`/api/ortho-cases/${caseId}/problem-list`, {
                  category: s.category,
                  description: s.description,
                  severity: s.severity,
                });
                setItems((prev) => [...prev, data]);
              } catch {}
            });
          }} />
        </div>
      </div>

      {/* List */}
      {items.length === 0 ? (
        <div className="text-center py-10 text-gray-400 text-sm">
          <ListChecks className="w-10 h-10 mx-auto mb-2 opacity-30" />
          لا توجد مشاكل مسجّلة
        </div>
      ) : (
        <div className="space-y-2">
          {items.map((item) => (
            <div key={item.id} className="flex items-start justify-between gap-3 bg-white border border-gray-100 rounded-lg px-4 py-3">
              <div className="flex items-start gap-3">
                <span className="text-xs font-medium px-2 py-0.5 rounded bg-gray-100 text-gray-600 mt-0.5 whitespace-nowrap">
                  {CATEGORY_LABELS[item.category] ?? item.category}
                </span>
                <span className="text-sm text-gray-900">{item.description}</span>
                {item.severity && (
                  <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${SEVERITY_COLORS[item.severity] ?? "bg-gray-100 text-gray-500"}`}>
                    {item.severity === "mild" ? "خفيفة" : item.severity === "moderate" ? "متوسطة" : "شديدة"}
                  </span>
                )}
              </div>
              <button onClick={() => handleDelete(item.id)} className="text-gray-300 hover:text-red-500 transition flex-shrink-0">
                <Trash2 className="w-4 h-4" />
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

// ─── AI Extraction Button ────────────────────────────────────────────────────
function AiExtractionButton({ caseId, onAnalysis }: { caseId: string; onAnalysis: (analysis: ExtractionDecisionAnalysis) => void }) {
  const extractionMutation = useExtractionDecision();
  const { data: caseData } = useOrthoCaseDataForAi(caseId);

  const handleAnalyze = async () => {
    if (!caseData) return;
    extractionMutation.mutate(
      {
        clinicalExam: caseData.ClinicalExam,
        cephMeasurements: caseData.CephAnalysis?.Measurements,
        modelAnalysis: caseData.ModelAnalysis,
        patientAge: caseData.OrthoCase?.PatientAge,
        patientGender: caseData.OrthoCase?.PatientGender,
        problemList: caseData.ProblemList,
      },
      {
        onSuccess: (data) => onAnalysis(data),
      }
    );
  };

  return (
    <button
      onClick={handleAnalyze}
      disabled={extractionMutation.isPending || !caseData}
      className="text-xs px-3 py-1.5 rounded-lg bg-purple-600 text-white hover:bg-purple-700 disabled:opacity-50 transition flex items-center gap-1.5"
    >
      <Sparkles className="w-3 h-3" />
      {extractionMutation.isPending ? "جاري التحليل..." : "تحليل بالذكاء الاصطناعي"}
    </button>
  );
}

// ─── Extraction Decision Tab ──────────────────────────────────────────────────
const EXTRACTION_OPTIONS = [
  { value: "no_extraction",  label: "بدون خلع" },
  { value: "extract_4",      label: "خلع 4 أضراس (علوي + سفلي)" },
  { value: "extract_2_upper",label: "خلع 2 علوي فقط" },
  { value: "extract_2_lower",label: "خلع 2 سفلي فقط" },
  { value: "borderline",     label: "حالة حدية — تحتاج تقييم إضافي" },
];

function ExtractionDecisionTab({ caseId }: { caseId: string }) {
  const inputCls = "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal";
  const [decision, setDecision] = useState("");
  const [notes, setNotes] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [decidedBy, setDecidedBy] = useState<string | null>(null);
  const [decidedAt, setDecidedAt] = useState<string | null>(null);

  useEffect(() => {
    api.get<{ decision?: string; doctorNotes?: string; decidedByName?: string; decidedAt?: string } | null>(`/api/ortho-cases/${caseId}/extraction-decision`)
      .then((r) => {
        if (r.data) {
          setDecision(r.data.decision ?? "");
          setNotes(r.data.doctorNotes ?? "");
          setDecidedBy(r.data.decidedByName ?? null);
          setDecidedAt(r.data.decidedAt ?? null);
        }
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [caseId]);

  const handleSave = async () => {
    setSaving(true); setSaved(false);
    try {
      await api.put(`/api/ortho-cases/${caseId}/extraction-decision`, { decision, doctorNotes: notes });
      setSaved(true); setTimeout(() => setSaved(false), 3000);
    } catch {} finally { setSaving(false); }
  };

  if (loading) return <div className="animate-pulse h-40 bg-gray-100 rounded-xl" />;

  const selectedOption = EXTRACTION_OPTIONS.find((o) => o.value === decision);

  return (
    <div className="space-y-5">
      {/* Options */}
      <div className="space-y-2">
        <p className="text-sm font-semibold text-gray-700 mb-3">قرار الخلع</p>
        {EXTRACTION_OPTIONS.map((opt) => (
          <label key={opt.value}
            className={cn(
              "flex items-center gap-3 p-3 rounded-xl border-2 cursor-pointer transition",
              decision === opt.value
                ? "border-clinic-teal bg-teal-50"
                : "border-gray-200 hover:border-gray-300"
            )}
          >
            <input type="radio" name="extraction" value={opt.value} checked={decision === opt.value}
              onChange={() => { setDecision(opt.value); setSaved(false); }}
              className="text-clinic-teal"
            />
            <span className={cn("text-sm font-medium", decision === opt.value ? "text-clinic-teal" : "text-gray-700")}>
              {opt.label}
            </span>
          </label>
        ))}
      </div>

      {/* AI Extraction Analysis */}
      <div className="bg-purple-50 rounded-xl border border-purple-200 p-4">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Sparkles className="w-4 h-4 text-purple-600" />
            <h4 className="text-sm font-semibold text-purple-800">تحليل AI لقرار الخلع</h4>
            <span className="text-[10px] px-1.5 py-0.5 bg-purple-100 text-purple-600 rounded-full">مقترح</span>
          </div>
          <AiExtractionButton caseId={caseId} onAnalysis={(analysis) => {
            // Auto-select AI recommendation
            if (analysis.recommendation) {
              setDecision(analysis.recommendation);
            }
          }} />
        </div>
      </div>

      {/* Decision summary */}
      {decision && (
        <div className={cn(
          "p-3 rounded-lg border text-sm font-medium",
          decision === "no_extraction" ? "bg-green-50 border-green-200 text-green-800" :
          decision === "borderline" ? "bg-yellow-50 border-yellow-200 text-yellow-800" :
          "bg-orange-50 border-orange-200 text-orange-800"
        )}>
          القرار: {selectedOption?.label}
        </div>
      )}

      {/* Doctor notes */}
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1.5">ملاحظات الطبيب</label>
        <textarea rows={3} value={notes} onChange={(e) => { setNotes(e.target.value); setSaved(false); }}
          placeholder="مبررات القرار، العوامل المحددة..." className={inputCls}
        />
      </div>

      <div className="flex items-center gap-3 pt-2 border-t border-gray-100">
        <button onClick={handleSave} disabled={saving || !decision}
          className="flex items-center gap-2 px-5 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60 transition"
        >
          <Save className="w-4 h-4" />
          {saving ? "جاري الحفظ..." : "تأكيد القرار"}
        </button>
        {saved && <span className="text-sm text-teal-600 font-medium">تم الحفظ بنجاح</span>}
        {decidedBy && (
          <span className="text-xs text-gray-400">
            آخر تحديث: {decidedBy} {decidedAt ? `· ${decidedAt}` : ""}
          </span>
        )}
      </div>
    </div>
  );
}

// ─── Patient Contracts Panel ─────────────────────────────────────────────────
function PatientContractsPanel({ patientId }: { patientId: string }) {
  const [contracts, setContracts] = useState<Contract[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api
      .get<Contract[]>(`/api/contracts?patientId=${patientId}&pageSize=10`)
      .then((r) => setContracts(r.data ?? []))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [patientId]);

  const STATUS_LABELS: Record<string, string> = { active: "نشط", completed: "مكتمل", cancelled: "ملغى" };
  const STATUS_COLORS: Record<string, string> = {
    active: "bg-teal-50 text-teal-700",
    completed: "bg-green-50 text-green-700",
    cancelled: "bg-gray-100 text-gray-500",
  };
  const SPECIALTY_LABELS: Record<string, string> = {
    orthodontics: "تقويم", general: "أسنان عام", surgery: "جراحة", other: "أخرى",
  };

  if (loading) {
    return <div className="space-y-2 animate-pulse">{Array.from({length:2}).map((_,i)=><div key={i} className="h-20 bg-gray-100 rounded-xl"/>)}</div>;
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h3 className="font-semibold text-gray-900">عقود المريض</h3>
        <Link
          href={`/finance/contracts/new?patientId=${patientId}`}
          className="flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 transition"
        >
          <Plus className="w-3.5 h-3.5" />
          عقد جديد
        </Link>
      </div>
      {contracts.length === 0 ? (
        <div className="text-center py-12 text-gray-400">
          <Wallet className="w-10 h-10 mx-auto mb-2 opacity-20" />
          <p className="text-sm">لا توجد عقود لهذا المريض</p>
        </div>
      ) : (
        <div className="space-y-3">
          {contracts.map((c) => {
            const net = c.totalAmount - (c.discountAmount ?? 0);
            const pct = net > 0 ? Math.min(100, Math.round((c.paidAmount / net) * 100)) : 0;
            return (
              <Link key={c.id} href={`/finance/contracts/${c.id}`}
                className="block bg-gray-50 rounded-xl border border-gray-200 p-4 hover:border-clinic-teal/40 hover:shadow-sm transition"
              >
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <div className="flex items-center gap-2 flex-wrap">
                      <span className="text-sm font-semibold text-gray-900">
                        {SPECIALTY_LABELS[c.specialty ?? ""] ?? c.specialty ?? "عقد"}
                      </span>
                      <span className={cn("text-xs px-2 py-0.5 rounded-full font-medium", STATUS_COLORS[c.status] ?? "bg-gray-100 text-gray-600")}>
                        {STATUS_LABELS[c.status] ?? c.status}
                      </span>
                    </div>
                    <p className="text-xs text-gray-500 mt-1">
                      {c.installmentsCount > 1
                        ? `${c.installmentsCount} أقساط · ${formatYemeniRiyal(c.installmentAmount ?? 0)} / قسط`
                        : "دفعة واحدة"}
                    </p>
                  </div>
                  <div className="text-end flex-shrink-0">
                    <p className="text-sm font-bold text-gray-900">{formatYemeniRiyal(c.paidAmount)}</p>
                    <p className="text-xs text-gray-400">من {formatYemeniRiyal(net)}</p>
                  </div>
                </div>
                <div className="mt-3">
                  <div className="flex items-center gap-2">
                    <div className="flex-1 h-1.5 bg-gray-200 rounded-full overflow-hidden">
                      <div className="h-full bg-clinic-teal rounded-full" style={{ width: `${pct}%` }} />
                    </div>
                    <span className="text-xs text-gray-500 flex-shrink-0">{pct}%</span>
                  </div>
                  {c.remainingAmount > 0 && (
                    <p className="text-xs text-red-500 mt-1">متبقي: {formatYemeniRiyal(c.remainingAmount)}</p>
                  )}
                </div>
              </Link>
            );
          })}
        </div>
      )}
    </div>
  );
}

export default function OrthoCaseDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [orthoCase, setOrthoCase] = useState<OrthoCase | null>(null);
  const [visits, setVisits] = useState<OrthoVisit[]>([]);
  const [clinicalExam, setClinicalExam] = useState<ClinicalExam | null>(null);
  const [treatmentPlans, setTreatmentPlans] = useState<TreatmentPlanDto[]>([]);
  const [selectedPlanId, setSelectedPlanId] = useState<string | undefined>();
  const [modelAnalysis, setModelAnalysis] = useState<ModelAnalysisDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<Tab>("info");

  useEffect(() => {
    Promise.all([
      api.get<OrthoCase>(`/api/ortho-cases/${id}`),
      api.get<OrthoVisit[]>(`/api/ortho-cases/${id}/visits`),
      api.get<ClinicalExam | null>(`/api/ortho-cases/${id}/clinical-exam`),
      api.get<TreatmentPlanListDto>(`/api/ortho-cases/${id}/treatment-plans`).catch((): { data: TreatmentPlanListDto } => ({ data: { plans: [] } })),
      api.get<ModelAnalysisDto>(`/api/ortho-cases/${id}/model-analysis`).catch(() => ({ data: null })),
    ])
      .then(([caseRes, visitsRes, examRes, plansRes, analysisRes]) => {
        setOrthoCase(caseRes.data);
        setVisits(visitsRes.data);
        setClinicalExam(examRes.data);
        setTreatmentPlans(plansRes.data?.plans ?? []);
        setSelectedPlanId(plansRes.data?.selectedPlanId);
        setModelAnalysis(analysisRes.data);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [id]);

  const refetchPlans = () => {
    api.get<TreatmentPlanListDto>(`/api/ortho-cases/${id}/treatment-plans`)
      .then((res) => {
        setTreatmentPlans(res.data?.plans ?? []);
        setSelectedPlanId(res.data?.selectedPlanId);
      })
      .catch(() => {});
  };

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

          {activeTab === "problems" && (
            <ProblemListTab caseId={id} />
          )}

          {activeTab === "plan" && (
            <TreatmentPlanAB
              orthoCaseId={id}
              plans={treatmentPlans}
              selectedPlanId={selectedPlanId}
              onPlansChange={refetchPlans}
            />
          )}

          {activeTab === "extraction" && (
            <ExtractionDecisionTab caseId={id} />
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

          {activeTab === "retention" && (
            <RetentionTab caseId={id} />
          )}
          {activeTab === "photos" && orthoCase && (
            <PhotosRadiographsTab patientId={orthoCase.patientId} orthoCaseId={id} />
          )}
          {activeTab === "model" && (
            <ModelAnalysisTab orthoCaseId={id} modelAnalysis={modelAnalysis} key={modelAnalysis?.id} />
          )}
          {activeTab === "finance" && orthoCase && (
            <PatientContractsPanel patientId={orthoCase.patientId} />
          )}
          {activeTab === "summary" && <AiCaseSummary caseId={id} />}
          {activeTab === "vto" && orthoCase && <VtoModule caseId={id} landmarks={[]} imageWidth={800} imageHeight={600} />}
        </div>
      </div>
    </div>
  );
}
