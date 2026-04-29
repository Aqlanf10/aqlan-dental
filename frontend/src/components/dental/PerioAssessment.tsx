"use client";
import { useState, useCallback, useMemo } from "react";
import {
  Save, History, Plus, Trash2, Activity, CheckCircle2,
} from "lucide-react";
import {
  usePerioHistory, useCreatePerioAssessment, useUpdatePerioAssessment,
} from "@/hooks/useGeneral";
import { cn, formatArabicDate } from "@/lib/utils";
import type { PerioAssessment as PerioAssessmentType, PerioTooth, PerioSite } from "@/types/dental";

// FDI tooth numbering - 32 teeth
const UPPER_RIGHT = ["18", "17", "16", "15", "14", "13", "12", "11"];
const UPPER_LEFT = ["21", "22", "23", "24", "25", "26", "27", "28"];
const LOWER_RIGHT = ["48", "47", "46", "45", "44", "43", "42", "41"];
const LOWER_LEFT = ["31", "32", "33", "34", "35", "36", "37", "38"];

const ALL_TEETH = [...UPPER_RIGHT, ...UPPER_LEFT, ...LOWER_LEFT, ...LOWER_RIGHT];

const SITE_KEYS = ["mb", "b", "db", "ml", "l", "dl"] as const;
const SITE_LABELS: Record<string, string> = {
  mb: "أنسي شدقي",
  b: "شدقي",
  db: "وحشي شدقي",
  ml: "أنسي لساني",
  l: "لساني",
  dl: "وحشي لساني",
};
const SITE_SHORT: Record<string, string> = {
  mb: "AS",
  b: "S",
  db: "WS",
  ml: "AL",
  l: "L",
  dl: "WL",
};

const MOBILITY_OPTIONS = [
  { value: "0", label: "0 - ثابت" },
  { value: "I", label: "I - حركة 1مم" },
  { value: "II", label: "II - حركة 1-2مم" },
  { value: "III", label: "III - حركة >2مم" },
];

const FURCATION_OPTIONS = [
  { value: "none", label: "لا يوجد" },
  { value: "I", label: "درجة I" },
  { value: "II", label: "درجة II" },
  { value: "III", label: "درجة III" },
];

const PERIO_STAGES = [
  { value: "healthy", label: "سليم" },
  { value: "gingivitis", label: "التهاب لثة" },
  { value: "mild", label: "خفيف" },
  { value: "moderate", label: "متوسط" },
  { value: "severe", label: "شديد" },
];

const RECESSION_LEVELS = [
  { value: "none", label: "لا يوجد" },
  { value: "mild", label: "خفيف (<2مم)" },
  { value: "moderate", label: "متوسط (2-4مم)" },
  { value: "severe", label: "شديد (>4مم)" },
];

const BONE_LOSS_PATTERNS = [
  { value: "horizontal", label: "أفقي" },
  { value: "vertical", label: "عمودي" },
  { value: "angular", label: "زاوي" },
  { value: "furcation", label: "تفرع" },
  { value: "generalized", label: "منتشر" },
  { value: "localized", label: "موضعي" },
];

const ATTACHMENT_LOSS_OPTIONS = [
  { value: "none", label: "لا يوجد" },
  { value: "1-2mm", label: "1-2 مم" },
  { value: "3-4mm", label: "3-4 مم" },
  { value: "5-7mm", label: "5-7 مم" },
  { value: "7mm+", label: ">7 مم" },
];

function getPocketColor(depth: number): string {
  if (depth <= 3) return "bg-green-500";
  if (depth === 4) return "bg-yellow-500";
  if (depth === 5) return "bg-orange-500";
  return "bg-red-500";
}

function getPocketTextColor(depth: number): string {
  if (depth <= 3) return "text-green-700";
  if (depth === 4) return "text-yellow-700";
  if (depth === 5) return "text-orange-700";
  return "text-red-700";
}

function makeEmptyTooth(toothNumber: string): PerioTooth {
  const emptySite: PerioSite = { pocketDepth: 0, bleeding: false };
  return {
    toothNumber,
    sites: {
      mb: { ...emptySite },
      b: { ...emptySite },
      db: { ...emptySite },
      ml: { ...emptySite },
      l: { ...emptySite },
      dl: { ...emptySite },
    },
    mobility: "0",
    furcation: "none",
  };
}

const inputCls =
  "w-full px-2 py-1.5 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal text-center";
const smallInputCls =
  "w-full px-1 py-1 text-xs rounded border border-gray-300 bg-white focus:outline-none focus:ring-1 focus:ring-clinic-teal text-center";
const selectCls =
  "w-full px-2 py-1.5 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal";

interface Props {
  patientId: string;
}

export function PerioAssessmentComponent({ patientId }: Props) {
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [activeSection, setActiveSection] = useState<"chart" | "form" | "history">("chart");

  // Assessment data
  const [assessmentDate, setAssessmentDate] = useState(
    new Date().toISOString().split("T")[0]
  );
  const [doctorId, setDoctorId] = useState("");
  const [teeth, setTeeth] = useState<PerioTooth[]>(
    ALL_TEETH.map(makeEmptyTooth)
  );
  const [recessionLevel, setRecessionLevel] = useState("");
  const [perioStage, setPerioStage] = useState<string>("");
  const [boneLossPattern, setBoneLossPattern] = useState("");
  const [attachmentLoss, setAttachmentLoss] = useState("");
  const [plaqueIndex, setPlaqueIndex] = useState("");
  const [gingivalIndex, setGingivalIndex] = useState("");
  const [treatmentPlan, setTreatmentPlan] = useState("");
  const [recommendation, setRecommendation] = useState("");

  // History (using React Query hook)
  const [showHistory, setShowHistory] = useState(false);
  const { data: history = [], isLoading: loadingHistory, refetch: refetchHistory } = usePerioHistory(showHistory ? patientId : null);

  // Selected tooth for detail editing
  const [selectedTooth, setSelectedTooth] = useState<string | null>(null);
  
  // Edit mode (tracking existing assessment ID for updates)
  const [editingId, setEditingId] = useState<string | null>(null);
  
  // Mutations
  const createMutation = useCreatePerioAssessment();
  const updateMutation = useUpdatePerioAssessment();

  const getTooth = useCallback(
    (num: string) => teeth.find((t) => t.toothNumber === num),
    [teeth]
  );

  const updateTooth = useCallback(
    (toothNumber: string, updates: Partial<PerioTooth>) => {
      setTeeth((prev) =>
        prev.map((t) =>
          t.toothNumber === toothNumber ? { ...t, ...updates } : t
        )
      );
    },
    []
  );

  const updateSite = useCallback(
    (toothNumber: string, siteKey: string, updates: Partial<PerioSite>) => {
      setTeeth((prev) =>
        prev.map((t) => {
          if (t.toothNumber !== toothNumber) return t;
          return {
            ...t,
            sites: {
              ...t.sites,
              [siteKey]: { ...t.sites[siteKey as keyof typeof t.sites], ...updates },
            },
          };
        })
      );
    },
    []
  );

  // Auto-calculated summary
  const summary = useMemo(() => {
    const allSites: { depth: number; bleeding: boolean }[] = [];
    teeth.forEach((t) => {
      SITE_KEYS.forEach((sk) => {
        const s = t.sites[sk];
        if (s.pocketDepth > 0) {
          allSites.push({ depth: s.pocketDepth, bleeding: s.bleeding });
        }
      });
    });
    const totalSites = allSites.length;
    const avgDepth =
      totalSites > 0
        ? allSites.reduce((sum, s) => sum + s.depth, 0) / totalSites
        : 0;
    const bleedingSites = allSites.filter((s) => s.bleeding).length;
    const bleedingPct = totalSites > 0 ? (bleedingSites / totalSites) * 100 : 0;
    const pi = plaqueIndex ? parseFloat(plaqueIndex) : 0;
    const gi = gingivalIndex ? parseFloat(gingivalIndex) : 0;

    return { avgDepth, bleedingPct, totalSites, pi, gi };
  }, [teeth, plaqueIndex, gingivalIndex]);

  // Load existing assessment from history
  const loadAssessment = useCallback(
    (assessment: PerioAssessmentType) => {
      setEditingId(assessment.id);
      setAssessmentDate(assessment.assessmentDate);
      setDoctorId(assessment.doctorId ?? "");
      setRecessionLevel(assessment.recessionLevel ?? "");
      setPerioStage(assessment.perioStage ?? "");
      setBoneLossPattern(assessment.boneLossPattern ?? "");
      setAttachmentLoss(assessment.attachmentLoss ?? "");
      setPlaqueIndex(assessment.plaqueIndex?.toString() ?? "");
      setGingivalIndex(assessment.gingivalIndex?.toString() ?? "");
      setTreatmentPlan(assessment.treatmentPlan ?? "");
      setRecommendation(assessment.recommendation ?? "");
      // Map teeth from data
      const teethMap = new Map<string, PerioTooth>();
      assessment.teeth.forEach((t) => teethMap.set(t.toothNumber, t));
      setTeeth(ALL_TEETH.map((num) => teethMap.get(num) ?? makeEmptyTooth(num)));
      setActiveSection("chart");
    },
    []
  );

  // Save assessment (create or update)
  const handleSave = async () => {
    setSaving(true);
    setSaved(false);
    try {
      const payload = {
        patientId,
        assessmentDate,
        doctorId: doctorId || undefined,
        teeth: teeth.filter(
          (t) =>
            Object.values(t.sites).some((s) => s.pocketDepth > 0 || s.bleeding) ||
            t.mobility !== "0" ||
            t.furcation !== "none"
        ),
        recessionLevel: recessionLevel || undefined,
        perioStage: perioStage || undefined,
        boneLossPattern: boneLossPattern || undefined,
        attachmentLoss: attachmentLoss || undefined,
        plaqueIndex: plaqueIndex ? parseFloat(plaqueIndex) : undefined,
        gingivalIndex: gingivalIndex ? parseFloat(gingivalIndex) : undefined,
        treatmentPlan: treatmentPlan || undefined,
        recommendation: recommendation || undefined,
      };

      if (editingId) {
        await updateMutation.mutateAsync({ id: editingId, ...payload });
      } else {
        await createMutation.mutateAsync(payload);
      }
      
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
      refetchHistory();
    } catch {
      // error handled by mutation
    } finally {
      setSaving(false);
    }
  };

  // Section toggles
  const toggleHistory = () => {
    setShowHistory(!showHistory);
  };

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        <h2 className="text-lg font-bold text-gray-900 flex items-center gap-2">
          <Activity className="w-5 h-5 text-clinic-teal" />
          تقييم النسج الداعمة
        </h2>
        <div className="flex items-center gap-2">
          <button
            onClick={toggleHistory}
            className={cn(
              "flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-lg border transition",
              showHistory
                ? "border-clinic-teal text-clinic-teal bg-teal-50"
                : "border-gray-200 text-gray-600 hover:bg-gray-50"
            )}
          >
            <History className="w-4 h-4" />
            السجل
          </button>
          <button
            onClick={handleSave}
            disabled={saving}
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60 transition"
          >
            <Save className="w-4 h-4" />
            {saving ? "جارٍ الحفظ..." : "حفظ التقييم"}
          </button>
        </div>
      </div>

      {saved && (
        <div className="flex items-center gap-2 text-sm text-green-700 bg-green-50 border border-green-200 rounded-lg p-3">
          <CheckCircle2 className="w-4 h-4" />
          تم حفظ التقييم بنجاح
        </div>
      )}

      {/* History panel */}
      {showHistory && (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-4 space-y-3">
          <h3 className="text-sm font-semibold text-gray-700">سجل التقييمات السابقة</h3>
          {loadingHistory ? (
            <div className="space-y-2 animate-pulse">
              {Array.from({ length: 3 }).map((_, i) => (
                <div key={i} className="h-12 bg-gray-100 rounded-lg" />
              ))}
            </div>
          ) : history.length === 0 ? (
            <p className="text-sm text-gray-400 text-center py-6">لا توجد تقييمات سابقة</p>
          ) : (
            <div className="space-y-2 max-h-64 overflow-y-auto">
              {history.map((h) => (
                <button
                  key={h.id}
                  onClick={() => loadAssessment(h)}
                  className="w-full flex items-center justify-between p-3 bg-gray-50 rounded-lg hover:bg-teal-50 border border-transparent hover:border-teal-200 transition text-start"
                >
                  <div>
                    <span className="text-sm font-medium text-gray-900">
                      {formatArabicDate(h.assessmentDate)}
                    </span>
                    {h.perioStage && (
                      <span className="text-xs text-gray-500 mr-2">
                        ({PERIO_STAGES.find((s) => s.value === h.perioStage)?.label ?? h.perioStage})
                      </span>
                    )}
                  </div>
                  <div className="flex items-center gap-3 text-xs text-gray-500">
                    {h.avgPocketDepth !== undefined && (
                      <span>متوسط العمق: {h.avgPocketDepth.toFixed(1)}مم</span>
                    )}
                    {h.bleedingPercentage !== undefined && (
                      <span>نزيف: {h.bleedingPercentage.toFixed(0)}%</span>
                    )}
                  </div>
                </button>
              ))}
            </div>
          )}
        </div>
      )}

      {/* Basic info */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-4">
        <h3 className="text-sm font-semibold text-gray-700 mb-3">معلومات أساسية</h3>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">تاريخ التقييم</label>
            <input
              type="date"
              value={assessmentDate}
              onChange={(e) => setAssessmentDate(e.target.value)}
              className={inputCls}
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">الطبيب</label>
            <select
              value={doctorId}
              onChange={(e) => setDoctorId(e.target.value)}
              className={selectCls}
            >
              <option value="">اختر طبيباً...</option>
            </select>
          </div>
        </div>
      </div>

      {/* Section navigation */}
      <div className="flex items-center gap-1 border-b border-gray-200">
        {(
          [
            { id: "chart", label: "المخطط البصري", icon: Activity },
            { id: "form", label: "إدخال البيانات", icon: Plus },
          ] as { id: "chart" | "form"; label: string; icon: typeof Activity }[]
        ).map(({ id, label, icon: Icon }) => (
          <button
            key={id}
            onClick={() => setActiveSection(id)}
            className={cn(
              "flex items-center gap-1.5 px-4 py-2.5 text-sm font-medium border-b-2 transition -mb-px",
              activeSection === id
                ? "border-clinic-teal text-clinic-teal"
                : "border-transparent text-gray-500 hover:text-gray-700"
            )}
          >
            <Icon className="w-4 h-4" />
            {label}
          </button>
        ))}
      </div>

      {/* Visual Chart */}
      {activeSection === "chart" && (
        <PerioChartVisual
          teeth={teeth}
          selectedTooth={selectedTooth}
          onSelectTooth={setSelectedTooth}
        />
      )}

      {/* Data entry form */}
      {activeSection === "form" && (
        <div className="space-y-4">
          {/* Pocket depth grid */}
          <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-4">
            <h3 className="text-sm font-semibold text-gray-700 mb-3">
              أعماق الجيوب (مم) — 6 مواقع لكل سن
            </h3>
            <div className="overflow-x-auto">
              <table className="w-full text-xs border-collapse">
                <thead>
                  <tr className="bg-gray-50">
                    <th className="border border-gray-200 px-2 py-1.5 text-gray-500 font-medium">السن</th>
                    {SITE_KEYS.map((sk) => (
                      <th key={sk} className="border border-gray-200 px-2 py-1.5 text-gray-500 font-medium">
                        {SITE_SHORT[sk]}
                      </th>
                    ))}
                    <th className="border border-gray-200 px-2 py-1.5 text-gray-500 font-medium">الحركة</th>
                    <th className="border border-gray-200 px-2 py-1.5 text-gray-500 font-medium">التفرع</th>
                  </tr>
                </thead>
                <tbody>
                  {/* Upper jaw */}
                  <tr>
                    <td
                      colSpan={9}
                      className="border border-gray-200 bg-teal-50 text-center text-xs font-semibold text-teal-700 py-1"
                    >
                      الفك العلوي
                    </td>
                  </tr>
                  {[...UPPER_RIGHT, ...UPPER_LEFT].map((num) => (
                    <ToothRow
                      key={num}
                      tooth={getTooth(num)!}
                      onUpdateSite={updateSite}
                      onUpdateTooth={updateTooth}
                    />
                  ))}
                  <tr>
                    <td
                      colSpan={9}
                      className="border border-gray-200 bg-orange-50 text-center text-xs font-semibold text-orange-700 py-1"
                    >
                      الفك السفلي
                    </td>
                  </tr>
                  {[...LOWER_RIGHT, ...LOWER_LEFT].map((num) => (
                    <ToothRow
                      key={num}
                      tooth={getTooth(num)!}
                      onUpdateSite={updateSite}
                      onUpdateTooth={updateTooth}
                    />
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          {/* Bleeding grid */}
          <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-4">
            <h3 className="text-sm font-semibold text-gray-700 mb-3">النزيف عند الجس</h3>
            <div className="overflow-x-auto">
              <table className="w-full text-xs border-collapse">
                <thead>
                  <tr className="bg-gray-50">
                    <th className="border border-gray-200 px-2 py-1.5 text-gray-500 font-medium">السن</th>
                    {SITE_KEYS.map((sk) => (
                      <th key={sk} className="border border-gray-200 px-2 py-1.5 text-gray-500 font-medium">
                        {SITE_SHORT[sk]}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {[...UPPER_RIGHT, ...UPPER_LEFT, ...LOWER_RIGHT, ...LOWER_LEFT].map((num) => {
                    const tooth = getTooth(num)!;
                    return (
                      <tr key={num} className="hover:bg-gray-50">
                        <td className="border border-gray-200 px-2 py-1 font-mono font-bold text-gray-700 text-center">
                          {num}
                        </td>
                        {SITE_KEYS.map((sk) => (
                          <td key={sk} className="border border-gray-200 px-1 py-1 text-center">
                            <input
                              type="checkbox"
                              checked={tooth.sites[sk].bleeding}
                              onChange={(e) =>
                                updateSite(num, sk, { bleeding: e.target.checked })
                              }
                              className="w-4 h-4 rounded border-gray-300 text-red-600 focus:ring-red-500"
                            />
                          </td>
                        ))}
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* Selected tooth detail */}
      {selectedTooth && (
        <ToothDetailPanel
          tooth={getTooth(selectedTooth)!}
          onUpdateSite={updateSite}
          onUpdateTooth={updateTooth}
          onClose={() => setSelectedTooth(null)}
        />
      )}

      {/* Summary (auto-calculated) */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-4">
        <h3 className="text-sm font-semibold text-gray-700 mb-3">ملخص تلقائي</h3>
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
          <SummaryCard
            label="متوسط عمق الجيب"
            value={`${summary.avgDepth.toFixed(1)} مم`}
            color={summary.avgDepth <= 3 ? "green" : summary.avgDepth <= 5 ? "yellow" : "red"}
          />
          <SummaryCard
            label="نسبة النزيف"
            value={`${summary.bleedingPct.toFixed(0)}%`}
            color={summary.bleedingPct <= 10 ? "green" : summary.bleedingPct <= 30 ? "yellow" : "red"}
          />
          <SummaryCard
            label="مؤشر اللويحة"
            value={plaqueIndex || "—"}
            color="teal"
          />
          <SummaryCard
            label="مؤشر اللثة"
            value={gingivalIndex || "—"}
            color="teal"
          />
        </div>
      </div>

      {/* Clinical assessment */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-4">
        <h3 className="text-sm font-semibold text-gray-700 mb-3">التقييم السريري</h3>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">مستوى التراجع</label>
            <select value={recessionLevel} onChange={(e) => setRecessionLevel(e.target.value)} className={selectCls}>
              <option value="">اختر...</option>
              {RECESSION_LEVELS.map((o) => (
                <option key={o.value} value={o.value}>{o.label}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">مرحلة أمراض اللثة</label>
            <select value={perioStage} onChange={(e) => setPerioStage(e.target.value)} className={selectCls}>
              <option value="">اختر...</option>
              {PERIO_STAGES.map((o) => (
                <option key={o.value} value={o.value}>{o.label}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">نمط فقدان العظم</label>
            <select value={boneLossPattern} onChange={(e) => setBoneLossPattern(e.target.value)} className={selectCls}>
              <option value="">اختر...</option>
              {BONE_LOSS_PATTERNS.map((o) => (
                <option key={o.value} value={o.value}>{o.label}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">فقدان الارتباط</label>
            <select value={attachmentLoss} onChange={(e) => setAttachmentLoss(e.target.value)} className={selectCls}>
              <option value="">اختر...</option>
              {ATTACHMENT_LOSS_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>{o.label}</option>
              ))}
            </select>
          </div>
        </div>
      </div>

      {/* Treatment plan & recommendation */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-4 space-y-4">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">خطة العلاج</label>
          <textarea
            value={treatmentPlan}
            onChange={(e) => setTreatmentPlan(e.target.value)}
            rows={3}
            className={cn(inputCls, "resize-y text-start")}
            placeholder="خطة العلاج المقترحة..."
          />
        </div>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">التوصيات</label>
          <textarea
            value={recommendation}
            onChange={(e) => setRecommendation(e.target.value)}
            rows={3}
            className={cn(inputCls, "resize-y text-start")}
            placeholder="التوصيات والملاحظات..."
          />
        </div>
      </div>
    </div>
  );
}

// ─── Perio Chart Visual (SVG) ──────────────────────────────────────────────────

function PerioChartVisual({
  teeth,
  selectedTooth,
  onSelectTooth,
}: {
  teeth: PerioTooth[];
  selectedTooth: string | null;
  onSelectTooth: (num: string | null) => void;
}) {
  const getTooth = (num: string) => teeth.find((t) => t.toothNumber === num);

  return (
    <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-4 space-y-4">
      <div className="flex items-center justify-between mb-2">
        <h3 className="text-sm font-semibold text-gray-700">مخطط النسج الداعمة</h3>
        <div className="flex items-center gap-3 text-xs">
          {[
            { color: "bg-green-500", label: "1-3 مم" },
            { color: "bg-yellow-500", label: "4 مم" },
            { color: "bg-orange-500", label: "5 مم" },
            { color: "bg-red-500", label: "6+ مم" },
          ].map((l) => (
            <div key={l.label} className="flex items-center gap-1">
              <div className={cn("w-3 h-3 rounded-sm", l.color)} />
              <span className="text-gray-600">{l.label}</span>
            </div>
          ))}
          <div className="flex items-center gap-1">
            <div className="w-3 h-3 rounded-full bg-red-600" />
            <span className="text-gray-600">نزيف</span>
          </div>
        </div>
      </div>

      {/* Upper jaw */}
      <div>
        <p className="text-xs text-gray-400 text-center mb-2">الفك العلوي</p>
        <div className="grid gap-1" style={{ gridTemplateColumns: "repeat(16, 1fr)" }}>
          {[...UPPER_RIGHT, ...UPPER_LEFT].map((num) => (
            <PerioToothBox
              key={num}
              tooth={getTooth(num)!}
              isSelected={selectedTooth === num}
              onClick={() => onSelectTooth(selectedTooth === num ? null : num)}
            />
          ))}
        </div>
      </div>

      {/* Center divider */}
      <div className="flex items-center my-1">
        <div className="flex-1 h-px bg-gray-200" />
        <span className="px-3 text-xs text-gray-400">الخط المنتصف</span>
        <div className="flex-1 h-px bg-gray-200" />
      </div>

      {/* Lower jaw */}
      <div>
        <div className="grid gap-1" style={{ gridTemplateColumns: "repeat(16, 1fr)" }}>
          {[...LOWER_RIGHT, ...LOWER_LEFT].map((num) => (
            <PerioToothBox
              key={num}
              tooth={getTooth(num)!}
              isSelected={selectedTooth === num}
              onClick={() => onSelectTooth(selectedTooth === num ? null : num)}
            />
          ))}
        </div>
        <p className="text-xs text-gray-400 text-center mt-2">الفك السفلي</p>
      </div>
    </div>
  );
}

function PerioToothBox({
  tooth,
  isSelected,
  onClick,
}: {
  tooth: PerioTooth;
  isSelected: boolean;
  onClick: () => void;
}) {
  // Get max pocket depth across all sites
  const maxDepth = Math.max(
    ...SITE_KEYS.map((sk) => tooth.sites[sk].pocketDepth)
  );
  const hasBleeding = SITE_KEYS.some((sk) => tooth.sites[sk].bleeding);
  const hasMobility = tooth.mobility !== "0";
  const hasFurcation = tooth.furcation !== "none";

  const bgClass = maxDepth > 0 ? getPocketColor(maxDepth) : "bg-gray-100";

  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        "relative aspect-[2/3] flex flex-col items-center justify-center rounded border-2 transition text-xs",
        bgClass,
        isSelected
          ? "border-clinic-teal ring-2 ring-clinic-teal ring-offset-1"
          : "border-gray-200 hover:border-gray-400",
        maxDepth > 0 ? "text-white" : "text-gray-600"
      )}
      title={`السن ${tooth.toothNumber} — أقصى عمق: ${maxDepth}مم`}
    >
      <span className="font-mono font-bold text-xs">{tooth.toothNumber}</span>
      {maxDepth > 0 && (
        <span className="text-[10px] font-semibold">{maxDepth}مم</span>
      )}
      {/* Indicators */}
      <div className="absolute top-0.5 right-0.5 flex gap-0.5">
        {hasBleeding && (
          <div className="w-2 h-2 rounded-full bg-red-600 border border-white" />
        )}
        {hasMobility && (
          <div className="w-2 h-2 rounded-full bg-yellow-400 border border-white" />
        )}
        {hasFurcation && (
          <div className="w-2 h-2 rounded-full bg-purple-500 border border-white" />
        )}
      </div>
      {/* Mobility label */}
      {hasMobility && (
        <span className="text-[8px] opacity-90 mt-0.5">M{tooth.mobility}</span>
      )}
    </button>
  );
}

// ─── Tooth Row (for data entry table) ─────────────────────────────────────────

function ToothRow({
  tooth,
  onUpdateSite,
  onUpdateTooth,
}: {
  tooth: PerioTooth;
  onUpdateSite: (toothNum: string, siteKey: string, updates: Partial<PerioSite>) => void;
  onUpdateTooth: (toothNum: string, updates: Partial<PerioTooth>) => void;
}) {
  return (
    <tr className="hover:bg-gray-50">
      <td className="border border-gray-200 px-2 py-1 font-mono font-bold text-gray-700 text-center">
        {tooth.toothNumber}
      </td>
      {SITE_KEYS.map((sk) => (
        <td key={sk} className="border border-gray-200 px-1 py-0.5">
          <input
            type="number"
            min={0}
            max={12}
            value={tooth.sites[sk].pocketDepth || ""}
            onChange={(e) =>
              onUpdateSite(tooth.toothNumber, sk, {
                pocketDepth: parseInt(e.target.value) || 0,
              })
            }
            className={cn(
              smallInputCls,
              tooth.sites[sk].pocketDepth > 0 && getPocketTextColor(tooth.sites[sk].pocketDepth)
            )}
            placeholder="—"
          />
        </td>
      ))}
      <td className="border border-gray-200 px-1 py-0.5">
        <select
          value={tooth.mobility}
          onChange={(e) =>
            onUpdateTooth(tooth.toothNumber, {
              mobility: e.target.value as PerioTooth["mobility"],
            })
          }
          className="w-full px-1 py-0.5 text-xs rounded border border-gray-300 bg-white focus:outline-none focus:ring-1 focus:ring-clinic-teal"
        >
          {MOBILITY_OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>
              {o.value}
            </option>
          ))}
        </select>
      </td>
      <td className="border border-gray-200 px-1 py-0.5">
        <select
          value={tooth.furcation}
          onChange={(e) =>
            onUpdateTooth(tooth.toothNumber, {
              furcation: e.target.value as PerioTooth["furcation"],
            })
          }
          className="w-full px-1 py-0.5 text-xs rounded border border-gray-300 bg-white focus:outline-none focus:ring-1 focus:ring-clinic-teal"
        >
          {FURCATION_OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
      </td>
    </tr>
  );
}

// ─── Tooth Detail Panel ────────────────────────────────────────────────────────

function ToothDetailPanel({
  tooth,
  onUpdateSite,
  onUpdateTooth,
  onClose,
}: {
  tooth: PerioTooth;
  onUpdateSite: (toothNum: string, siteKey: string, updates: Partial<PerioSite>) => void;
  onUpdateTooth: (toothNum: string, updates: Partial<PerioTooth>) => void;
  onClose: () => void;
}) {
  return (
    <div className="bg-white rounded-xl border-2 border-clinic-teal shadow-sm p-4 space-y-3">
      <div className="flex items-center justify-between">
        <h3 className="font-bold text-gray-900">
          السن <span className="font-mono text-clinic-teal">{tooth.toothNumber}</span>
        </h3>
        <button onClick={onClose} className="p-1 hover:bg-gray-100 rounded transition">
          <Trash2 className="w-4 h-4 text-gray-400" />
        </button>
      </div>

      {/* Sites grid */}
      <div className="grid grid-cols-3 gap-2">
        {SITE_KEYS.map((sk) => {
          const site = tooth.sites[sk];
          return (
            <div key={sk} className="bg-gray-50 rounded-lg p-2 space-y-1.5">
              <p className="text-xs font-semibold text-gray-600">{SITE_LABELS[sk]}</p>
              <div>
                <label className="text-[10px] text-gray-400">العمق (مم)</label>
                <input
                  type="number"
                  min={0}
                  max={12}
                  value={site.pocketDepth || ""}
                  onChange={(e) =>
                    onUpdateSite(tooth.toothNumber, sk, {
                      pocketDepth: parseInt(e.target.value) || 0,
                    })
                  }
                  className={cn(smallInputCls, site.pocketDepth > 0 && getPocketTextColor(site.pocketDepth))}
                  placeholder="0"
                />
              </div>
              <div className="flex items-center gap-1">
                <input
                  type="checkbox"
                  checked={site.bleeding}
                  onChange={(e) =>
                    onUpdateSite(tooth.toothNumber, sk, { bleeding: e.target.checked })
                  }
                  className="w-3.5 h-3.5 rounded border-gray-300 text-red-600 focus:ring-red-500"
                />
                <span className="text-[10px] text-gray-500">نزيف</span>
              </div>
            </div>
          );
        })}
      </div>

      {/* Mobility & Furcation */}
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="block text-xs font-medium text-gray-500 mb-1">الحركة</label>
          <select
            value={tooth.mobility}
            onChange={(e) =>
              onUpdateTooth(tooth.toothNumber, {
                mobility: e.target.value as PerioTooth["mobility"],
              })
            }
            className={selectCls}
          >
            {MOBILITY_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>
        </div>
        <div>
          <label className="block text-xs font-medium text-gray-500 mb-1">تفرع الجذور</label>
          <select
            value={tooth.furcation}
            onChange={(e) =>
              onUpdateTooth(tooth.toothNumber, {
                furcation: e.target.value as PerioTooth["furcation"],
              })
            }
            className={selectCls}
          >
            {FURCATION_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>
        </div>
      </div>
    </div>
  );
}

// ─── Summary Card ──────────────────────────────────────────────────────────────

function SummaryCard({
  label,
  value,
  color,
}: {
  label: string;
  value: string;
  color: "green" | "yellow" | "red" | "teal";
}) {
  const colorMap = {
    green: "bg-green-50 text-green-700",
    yellow: "bg-yellow-50 text-yellow-700",
    red: "bg-red-50 text-red-700",
    teal: "bg-teal-50 text-teal-700",
  };
  return (
    <div className={cn("rounded-lg p-3", colorMap[color])}>
      <p className="text-xs opacity-70">{label}</p>
      <p className="text-lg font-bold">{value}</p>
    </div>
  );
}
