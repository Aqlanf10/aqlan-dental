import type { CephDiagnosis, CephMeasurement } from "@/types/ceph";

export type CephAssessmentSection = "skeletal" | "dental" | "soft_tissue";
export type CephProblemSeverity = "mild" | "moderate" | "severe";

export interface CephAssessmentFinding {
  id: string;
  section: CephAssessmentSection;
  label: string;
  value: string;
  description: string;
  severity: CephProblemSeverity;
  isProblem: boolean;
  source: "diagnosis" | "measurement";
}

const SKELETAL_AR: Record<string, string> = {
  "Class I": "الصنف الأول",
  "Class II": "الصنف الثاني",
  "Class II Division 1": "الصنف الثاني، القسم الأول",
  "Class II Division 2": "الصنف الثاني، القسم الثاني",
  "Class III": "الصنف الثالث",
};

const VERTICAL_AR: Record<string, string> = {
  Normal: "طبيعي",
  Normodivergent: "طبيعي",
  Hyperdivergent: "نمط رأسي مرتفع",
  Hypodivergent: "نمط رأسي منخفض",
};

function measurementSection(name: string): CephAssessmentSection {
  const normalized = name.toUpperCase();
  if (/^(U1|L1|IMPA|FMIA|OVERJET|INTERINCISAL)/.test(normalized)) return "dental";
  if (
    normalized.includes("ELINE") ||
    normalized.includes("SLINE") ||
    normalized.includes("LIP") ||
    normalized.includes("NASOLABIAL")
  ) {
    return "soft_tissue";
  }
  return "skeletal";
}

export function buildCephAssessment(
  diagnosis: CephDiagnosis | null | undefined,
  measurements: CephMeasurement[],
): CephAssessmentFinding[] {
  const findings: CephAssessmentFinding[] = [];

  if (diagnosis?.skeletalClass) {
    const value = SKELETAL_AR[diagnosis.skeletalClass] ?? diagnosis.skeletalClass;
    const isProblem = diagnosis.skeletalClass !== "Class I";
    findings.push({
      id: "diagnosis-skeletal",
      section: "skeletal",
      label: "العلاقة الهيكلية",
      value,
      description: `علاقة هيكلية: ${value}`,
      severity: isProblem ? "moderate" : "mild",
      isProblem,
      source: "diagnosis",
    });
  }

  if (diagnosis?.verticalPattern) {
    const value = VERTICAL_AR[diagnosis.verticalPattern] ?? diagnosis.verticalPattern;
    const isProblem = !["Normal", "Normodivergent"].includes(diagnosis.verticalPattern);
    findings.push({
      id: "diagnosis-vertical",
      section: "skeletal",
      label: "النمط الرأسي",
      value,
      description: `النمط الرأسي: ${value}`,
      severity: isProblem ? "moderate" : "mild",
      isProblem,
      source: "diagnosis",
    });
  }

  if (diagnosis?.incisorInclination) {
    const isProblem = !diagnosis.incisorInclination.includes("ضمن الحدود الطبيعية");
    findings.push({
      id: "diagnosis-incisors",
      section: "dental",
      label: "ميلان القواطع",
      value: diagnosis.incisorInclination,
      description: `ميلان القواطع: ${diagnosis.incisorInclination}`,
      severity: isProblem ? "moderate" : "mild",
      isProblem,
      source: "diagnosis",
    });
  }

  if (diagnosis?.softTissueSummary) {
    const isProblem = !diagnosis.softTissueSummary.includes("طبيعي");
    findings.push({
      id: "diagnosis-soft-tissue",
      section: "soft_tissue",
      label: "الأنسجة الرخوة",
      value: diagnosis.softTissueSummary,
      description: `الأنسجة الرخوة: ${diagnosis.softTissueSummary}`,
      severity: isProblem ? "moderate" : "mild",
      isProblem,
      source: "diagnosis",
    });
  }

  for (const measurement of measurements) {
    if (measurement.value === null || measurement.severity === "normal" || measurement.severity === "unclassified") continue;
    const section = measurementSection(measurement.name);
    const interpretation = measurement.interpretationAr
      ?? measurement.apiInterpretation
      ?? `انحراف ${measurement.direction === "above" ? "أعلى" : "أدنى"} من المجال المرجعي`;
    findings.push({
      id: `measurement-${measurement.name}`,
      section,
      label: measurement.nameAr,
      value: `${measurement.value}${measurement.unit}`,
      description: `${measurement.nameAr}: ${interpretation} (${measurement.value}${measurement.unit})`,
      severity: measurement.severity,
      isProblem: true,
      source: "measurement",
    });
  }

  return findings;
}
