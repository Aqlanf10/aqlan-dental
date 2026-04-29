"use client";

import { useState, useEffect } from "react";
import { Calculator, Save, CheckCircle2, Info } from "lucide-react";
import api from "@/lib/api";
import { cn } from "@/lib/utils";
import type { ModelAnalysisDto, BoltonResult, BoltonCalculationRequest } from "@/types/ortho";

interface Props {
  orthoCaseId: string;
  modelAnalysis?: ModelAnalysisDto | null;
}

const UPPER_12_TEETH = [
  { fdi: "17", label: "17" }, { fdi: "16", label: "16" }, { fdi: "15", label: "15" },
  { fdi: "14", label: "14" }, { fdi: "13", label: "13" }, { fdi: "12", label: "12" },
  { fdi: "11", label: "11" }, { fdi: "21", label: "21" }, { fdi: "22", label: "22" },
  { fdi: "23", label: "23" }, { fdi: "24", label: "24" }, { fdi: "25", label: "25" },
  { fdi: "26", label: "26" }, { fdi: "27", label: "27" },
];
const LOWER_12_TEETH = [
  { fdi: "47", label: "47" }, { fdi: "46", label: "46" }, { fdi: "45", label: "45" },
  { fdi: "44", label: "44" }, { fdi: "43", label: "43" }, { fdi: "42", label: "42" },
  { fdi: "41", label: "41" }, { fdi: "31", label: "31" }, { fdi: "32", label: "32" },
  { fdi: "33", label: "33" }, { fdi: "34", label: "34" }, { fdi: "35", label: "35" },
  { fdi: "36", label: "36" }, { fdi: "37", label: "37" },
];
const UPPER_ANTERIOR_TEETH = [
  { fdi: "13", label: "13" }, { fdi: "12", label: "12" }, { fdi: "11", label: "11" },
  { fdi: "21", label: "21" }, { fdi: "22", label: "22" }, { fdi: "23", label: "23" },
];
const LOWER_ANTERIOR_TEETH = [
  { fdi: "43", label: "43" }, { fdi: "42", label: "42" }, { fdi: "41", label: "41" },
  { fdi: "31", label: "31" }, { fdi: "32", label: "32" }, { fdi: "33", label: "33" },
];

const inputCls = "w-full px-2 py-1.5 text-xs text-center rounded-md border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal";
const labelCls = "text-xs text-gray-500 mb-1";

function RatioBar({ value, normal, label }: { value: number; normal: number; label: string }) {
  const deviation = Math.abs(value - normal);
  const maxDeviation = 5;
  const barWidth = Math.min(100, (deviation / maxDeviation) * 100);
  const colorClass =
    deviation <= 1.91 ? "bg-green-500" :
    deviation <= 3 ? "bg-amber-500" :
    "bg-red-500";

  return (
    <div className="space-y-1">
      <div className="flex items-center justify-between text-xs">
        <span className="text-gray-600">{label}</span>
        <span className="font-mono font-semibold text-gray-900">{value.toFixed(2)}%</span>
      </div>
      <div className="flex items-center gap-2">
        <div className="flex-1 h-2 bg-gray-100 rounded-full overflow-hidden relative">
          <div className="absolute inset-y-0 w-0.5 bg-gray-400" style={{ left: "50%" }} />
          <div
            className={cn("h-full rounded-full transition-all", colorClass)}
            style={{ width: `${barWidth}%`, marginLeft: value >= normal ? "50%" : `${50 - barWidth}%` }}
          />
        </div>
        <span className="text-xs text-gray-400 font-mono">المعيار: {normal}%</span>
      </div>
    </div>
  );
}

export function ModelAnalysisTab({ orthoCaseId, modelAnalysis: initialAnalysis }: Props) {
  const [, setAnalysis] = useState<ModelAnalysisDto | null>(initialAnalysis ?? null);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  // Bolton width inputs
  const [upperWidths, setUpperWidths] = useState<number[]>(new Array(14).fill(0));
  const [lowerWidths, setLowerWidths] = useState<number[]>(new Array(14).fill(0));
  const [upperAntWidths, setUpperAntWidths] = useState<number[]>(new Array(6).fill(0));
  const [lowerAntWidths, setLowerAntWidths] = useState<number[]>(new Array(6).fill(0));
  const [boltonResult, setBoltonResult] = useState<BoltonResult | null>(null);
  const [calculating, setCalculating] = useState(false);

  // Arch & ALD inputs
  const [upperArchLength, setUpperArchLength] = useState<number | "">("");
  const [lowerArchLength, setLowerArchLength] = useState<number | "">("");
  const [upperAvailableSpace, setUpperAvailableSpace] = useState<number | "">("");
  const [lowerAvailableSpace, setLowerAvailableSpace] = useState<number | "">("");
  const [upperAld, setUpperAld] = useState<number | undefined>(undefined);
  const [lowerAld, setLowerAld] = useState<number | undefined>(undefined);

  // Pont Index
  const [interPremolarDist, setInterPremolarDist] = useState<number | "">("");
  const [interMolarDist, setInterMolarDist] = useState<number | "">("");

  const [notes, setNotes] = useState("");

  useEffect(() => {
    if (initialAnalysis) {
      setAnalysis(initialAnalysis);
      if (initialAnalysis.upperArchLength) setUpperArchLength(initialAnalysis.upperArchLength);
      if (initialAnalysis.lowerArchLength) setLowerArchLength(initialAnalysis.lowerArchLength);
      if (initialAnalysis.upperAld != null) setUpperAld(initialAnalysis.upperAld);
      if (initialAnalysis.lowerAld != null) setLowerAld(initialAnalysis.lowerAld);
      if (initialAnalysis.notes) setNotes(initialAnalysis.notes);
    }
  }, [initialAnalysis]);

  // Auto-calculate ALD
  useEffect(() => {
    if (upperArchLength !== "" && upperAvailableSpace !== "") {
      setUpperAld(Number(upperAvailableSpace) - Number(upperArchLength));
    }
  }, [upperArchLength, upperAvailableSpace]);

  useEffect(() => {
    if (lowerArchLength !== "" && lowerAvailableSpace !== "") {
      setLowerAld(Number(lowerAvailableSpace) - Number(lowerArchLength));
    }
  }, [lowerArchLength, lowerAvailableSpace]);

  const handleCalculateBolton = async () => {
    setCalculating(true);
    try {
      const payload: BoltonCalculationRequest = {
        upperWidths: upperWidths.filter((w) => w > 0),
        lowerWidths: lowerWidths.filter((w) => w > 0),
        upperAnteriorWidths: upperAntWidths.filter((w) => w > 0),
        lowerAnteriorWidths: lowerAntWidths.filter((w) => w > 0),
      };
      const { data } = await api.post<BoltonResult>(
        `/api/ortho-cases/${orthoCaseId}/bolton-calculation`,
        payload
      );
      setBoltonResult(data);
    } catch {
      // Silent fail
    } finally {
      setCalculating(false);
    }
  };

  const handleSave = async () => {
    setSaving(true);
    setSaved(false);
    try {
      const payload: Partial<ModelAnalysisDto> = {
        upperSum12: upperWidths.reduce((s, w) => s + w, 0) || undefined,
        lowerSum12: lowerWidths.reduce((s, w) => s + w, 0) || undefined,
        boltonOverall: boltonResult?.overallRatio,
        boltonAnterior: boltonResult?.anteriorRatio,
        upperArchLength: upperArchLength !== "" ? Number(upperArchLength) : undefined,
        lowerArchLength: lowerArchLength !== "" ? Number(lowerArchLength) : undefined,
        upperAld,
        lowerAld,
        pontIndex: interPremolarDist !== "" ? Number(interPremolarDist) : undefined,
        notes: notes || undefined,
        analysisDate: new Date().toISOString().split("T")[0],
      };
      const { data } = await api.put(`/api/ortho-cases/${orthoCaseId}/model-analysis`, payload);
      setAnalysis(data);
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } catch {
      // Silent fail
    } finally {
      setSaving(false);
    }
  };

  const deviationColor = (deviation: number) => {
    const abs = Math.abs(deviation);
    if (abs <= 1.91) return "text-green-600";
    if (abs <= 3) return "text-amber-600";
    return "text-red-600";
  };

  const deviationBg = (deviation: number) => {
    const abs = Math.abs(deviation);
    if (abs <= 1.91) return "bg-green-50 border-green-200";
    if (abs <= 3) return "bg-amber-50 border-amber-200";
    return "bg-red-50 border-red-200";
  };

  return (
    <div className="space-y-6">
      {/* ─── Section 1: Bolton Analysis ─── */}
      <div className="bg-white rounded-xl border border-gray-200 p-5 space-y-4">
        <div className="flex items-center gap-2">
          <Calculator className="w-5 h-5 text-clinic-teal" />
          <h3 className="text-sm font-semibold text-gray-800">تحليل بولتون (Bolton)</h3>
        </div>

        {/* 12 Upper Teeth */}
        <div>
          <p className={cn(labelCls, "font-semibold mb-2")}>أعرض الأسنان العلوية (12 سن) — FDI 17-27</p>
          <div className="grid grid-cols-7 gap-1.5">
            {UPPER_12_TEETH.map((tooth, i) => (
              <div key={tooth.fdi}>
                <label className="text-[10px] text-gray-400 text-center block mb-0.5">{tooth.label}</label>
                <input
                  type="number"
                  step="0.1"
                  min="0"
                  className={inputCls}
                  value={upperWidths[i] || ""}
                  onChange={(e) => {
                    const next = [...upperWidths];
                    next[i] = e.target.value ? parseFloat(e.target.value) : 0;
                    setUpperWidths(next);
                  }}
                />
              </div>
            ))}
          </div>
          <p className="text-xs text-gray-400 mt-1">المجموع: <span className="font-mono font-semibold">{upperWidths.reduce((s, w) => s + w, 0).toFixed(1)} mm</span></p>
        </div>

        {/* 12 Lower Teeth */}
        <div>
          <p className={cn(labelCls, "font-semibold mb-2")}>أعرض الأسنان السفلية (12 سن) — FDI 37-47</p>
          <div className="grid grid-cols-7 gap-1.5">
            {LOWER_12_TEETH.map((tooth, i) => (
              <div key={tooth.fdi}>
                <label className="text-[10px] text-gray-400 text-center block mb-0.5">{tooth.label}</label>
                <input
                  type="number"
                  step="0.1"
                  min="0"
                  className={inputCls}
                  value={lowerWidths[i] || ""}
                  onChange={(e) => {
                    const next = [...lowerWidths];
                    next[i] = e.target.value ? parseFloat(e.target.value) : 0;
                    setLowerWidths(next);
                  }}
                />
              </div>
            ))}
          </div>
          <p className="text-xs text-gray-400 mt-1">المجموع: <span className="font-mono font-semibold">{lowerWidths.reduce((s, w) => s + w, 0).toFixed(1)} mm</span></p>
        </div>

        {/* 6 Upper Anterior */}
        <div>
          <p className={cn(labelCls, "font-semibold mb-2")}>أعرض الأسنان الأمامية العلوية (6 أسنان) — 13-23</p>
          <div className="grid grid-cols-6 gap-1.5">
            {UPPER_ANTERIOR_TEETH.map((tooth, i) => (
              <div key={tooth.fdi}>
                <label className="text-[10px] text-gray-400 text-center block mb-0.5">{tooth.label}</label>
                <input
                  type="number"
                  step="0.1"
                  min="0"
                  className={inputCls}
                  value={upperAntWidths[i] || ""}
                  onChange={(e) => {
                    const next = [...upperAntWidths];
                    next[i] = e.target.value ? parseFloat(e.target.value) : 0;
                    setUpperAntWidths(next);
                  }}
                />
              </div>
            ))}
          </div>
          <p className="text-xs text-gray-400 mt-1">المجموع: <span className="font-mono font-semibold">{upperAntWidths.reduce((s, w) => s + w, 0).toFixed(1)} mm</span></p>
        </div>

        {/* 6 Lower Anterior */}
        <div>
          <p className={cn(labelCls, "font-semibold mb-2")}>أعرض الأسنان الأمامية السفلية (6 أسنان) — 33-43</p>
          <div className="grid grid-cols-6 gap-1.5">
            {LOWER_ANTERIOR_TEETH.map((tooth, i) => (
              <div key={tooth.fdi}>
                <label className="text-[10px] text-gray-400 text-center block mb-0.5">{tooth.label}</label>
                <input
                  type="number"
                  step="0.1"
                  min="0"
                  className={inputCls}
                  value={lowerAntWidths[i] || ""}
                  onChange={(e) => {
                    const next = [...lowerAntWidths];
                    next[i] = e.target.value ? parseFloat(e.target.value) : 0;
                    setLowerAntWidths(next);
                  }}
                />
              </div>
            ))}
          </div>
          <p className="text-xs text-gray-400 mt-1">المجموع: <span className="font-mono font-semibold">{lowerAntWidths.reduce((s, w) => s + w, 0).toFixed(1)} mm</span></p>
        </div>

        {/* Calculate button */}
        <button
          onClick={handleCalculateBolton}
          disabled={calculating}
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60 transition"
        >
          <Calculator className="w-4 h-4" />
          {calculating ? "جاري الحساب..." : "حساب بولتون"}
        </button>

        {/* Bolton Results */}
        {boltonResult && (
          <div className="space-y-4 mt-3 border border-gray-100 rounded-lg p-4 bg-gray-50">
            <h4 className="text-sm font-semibold text-gray-700">نتائج تحليل بولتون</h4>

            {/* Overall Ratio */}
            <div className="space-y-2">
              <RatioBar
                value={boltonResult.overallRatio}
                normal={boltonResult.normalOverall}
                label="النسبة الكلية (Overall)"
              />
              <div className={cn("p-2 rounded-md border text-xs", deviationBg(boltonResult.overallDiscrepancy))}>
                <span className={deviationColor(boltonResult.overallDiscrepancy)}>
                  التناقض الكلي: {boltonResult.overallDiscrepancy > 0 ? "+" : ""}{boltonResult.overallDiscrepancy.toFixed(2)} mm
                </span>
                <span className="text-gray-500 mr-2">
                  {boltonResult.overallDiscrepancy > 0 ? "(فائض سفلي)" : boltonResult.overallDiscrepancy < 0 ? "(فائض علوي)" : "(متطابق)"}
                </span>
              </div>
            </div>

            {/* Anterior Ratio */}
            {boltonResult.anteriorRatio != null && (
              <div className="space-y-2">
                <RatioBar
                  value={boltonResult.anteriorRatio}
                  normal={boltonResult.normalAnterior}
                  label="النسبة الأمامية (Anterior)"
                />
                {boltonResult.anteriorDiscrepancy != null && (
                  <div className={cn("p-2 rounded-md border text-xs", deviationBg(boltonResult.anteriorDiscrepancy))}>
                    <span className={deviationColor(boltonResult.anteriorDiscrepancy)}>
                      التناقض الأمامي: {boltonResult.anteriorDiscrepancy > 0 ? "+" : ""}{boltonResult.anteriorDiscrepancy.toFixed(2)} mm
                    </span>
                    <span className="text-gray-500 mr-2">
                      {boltonResult.anteriorDiscrepancy > 0 ? "(فائض سفلي أمامي)" : boltonResult.anteriorDiscrepancy < 0 ? "(فائض علوي أمامي)" : "(متطابق)"}
                    </span>
                  </div>
                )}
              </div>
            )}

            {/* Arabic Interpretation */}
            <div className="p-3 rounded-lg bg-white border border-gray-200">
              <div className="flex items-start gap-2">
                <Info className="w-4 h-4 text-clinic-teal flex-shrink-0 mt-0.5" />
                <p className="text-sm text-gray-700 leading-relaxed">{boltonResult.interpretationAr}</p>
              </div>
            </div>
          </div>
        )}
      </div>

      {/* ─── Section 2: Arch Length & ALD ─── */}
      <div className="bg-white rounded-xl border border-gray-200 p-5 space-y-4">
        <h3 className="text-sm font-semibold text-gray-800">طول القوس وتحليل التناقض (ALD)</h3>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {/* Upper */}
          <div className="space-y-3">
            <p className="text-xs font-semibold text-gray-600">القوس العلوية</p>
            <div>
              <label className={labelCls}>الطول المطلوب (mm)</label>
              <input
                type="number"
                step="0.1"
                className={inputCls}
                value={upperArchLength}
                onChange={(e) => setUpperArchLength(e.target.value ? parseFloat(e.target.value) : "")}
                placeholder="مجموع أعرض الأسنان"
              />
            </div>
            <div>
              <label className={labelCls}>المساحة المتاحة (mm)</label>
              <input
                type="number"
                step="0.1"
                className={inputCls}
                value={upperAvailableSpace}
                onChange={(e) => setUpperAvailableSpace(e.target.value ? parseFloat(e.target.value) : "")}
                placeholder="طول القوس المتاح"
              />
            </div>
            {upperAld != null && (
              <div className={cn(
                "p-2 rounded-md border text-sm font-medium",
                upperAld >= 0 ? "bg-green-50 border-green-200 text-green-700" : "bg-red-50 border-red-200 text-red-700"
              )}>
                ALD علوي: {upperAld > 0 ? "+" : ""}{upperAld.toFixed(1)} mm
                <span className="text-xs font-normal mr-1">
                  {upperAld >= 0 ? "(مساحة كافية)" : "(نقص مساحة / تكدس)"}
                </span>
              </div>
            )}
          </div>

          {/* Lower */}
          <div className="space-y-3">
            <p className="text-xs font-semibold text-gray-600">القوس السفلية</p>
            <div>
              <label className={labelCls}>الطول المطلوب (mm)</label>
              <input
                type="number"
                step="0.1"
                className={inputCls}
                value={lowerArchLength}
                onChange={(e) => setLowerArchLength(e.target.value ? parseFloat(e.target.value) : "")}
                placeholder="مجموع أعرض الأسنان"
              />
            </div>
            <div>
              <label className={labelCls}>المساحة المتاحة (mm)</label>
              <input
                type="number"
                step="0.1"
                className={inputCls}
                value={lowerAvailableSpace}
                onChange={(e) => setLowerAvailableSpace(e.target.value ? parseFloat(e.target.value) : "")}
                placeholder="طول القوس المتاح"
              />
            </div>
            {lowerAld != null && (
              <div className={cn(
                "p-2 rounded-md border text-sm font-medium",
                lowerAld >= 0 ? "bg-green-50 border-green-200 text-green-700" : "bg-red-50 border-red-200 text-red-700"
              )}>
                ALD سفلي: {lowerAld > 0 ? "+" : ""}{lowerAld.toFixed(1)} mm
                <span className="text-xs font-normal mr-1">
                  {lowerAld >= 0 ? "(مساحة كافية)" : "(نقص مساحة / تكدس)"}
                </span>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* ─── Section 3: Pont Index ─── */}
      <div className="bg-white rounded-xl border border-gray-200 p-5 space-y-4">
        <h3 className="text-sm font-semibold text-gray-800">معيار بونت (Pont Index)</h3>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label className={labelCls}>المسافة بين الأضراس العلوية (mm)</label>
            <input
              type="number"
              step="0.1"
              className={inputCls}
              value={interPremolarDist}
              onChange={(e) => setInterPremolarDist(e.target.value ? parseFloat(e.target.value) : "")}
              placeholder="Inter-molar distance"
            />
          </div>
          <div>
            <label className={labelCls}>المسافة بين الضواحك العلوية (mm)</label>
            <input
              type="number"
              step="0.1"
              className={inputCls}
              value={interMolarDist}
              onChange={(e) => setInterMolarDist(e.target.value ? parseFloat(e.target.value) : "")}
              placeholder="Inter-premolar distance"
            />
          </div>
        </div>

        {interPremolarDist !== "" && Number(interPremolarDist) > 0 && (
          <div className="p-3 rounded-lg bg-teal-50 border border-teal-200">
            <p className="text-sm text-teal-700">
              العرض المتوقع بين الأضراس حسب بونت: <span className="font-mono font-semibold">{(Number(interPremolarDist) * 0.64).toFixed(1)} mm</span>
            </p>
          </div>
        )}
      </div>

      {/* ─── Notes ─── */}
      <div>
        <label className="block text-xs font-medium text-gray-500 mb-1">ملاحظات</label>
        <textarea
          rows={3}
          className="w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal"
          value={notes}
          onChange={(e) => setNotes(e.target.value)}
          placeholder="ملاحظات إضافية على التحليل..."
        />
      </div>

      {/* ─── Save button ─── */}
      <div className="flex items-center gap-3 border-t border-gray-100 pt-4">
        <button
          onClick={handleSave}
          disabled={saving}
          className="flex items-center gap-2 px-5 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60 transition"
        >
          <Save className="w-4 h-4" />
          {saving ? "جاري الحفظ..." : "حفظ التحليل"}
        </button>
        {saved && (
          <span className="flex items-center gap-1 text-sm text-teal-600 font-medium">
            <CheckCircle2 className="w-4 h-4" />
            تم الحفظ بنجاح
          </span>
        )}
      </div>
    </div>
  );
}
