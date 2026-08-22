export const PERMANENT_FDI_TEETH = [
  "18", "17", "16", "15", "14", "13", "12", "11",
  "21", "22", "23", "24", "25", "26", "27", "28",
  "48", "47", "46", "45", "44", "43", "42", "41",
  "31", "32", "33", "34", "35", "36", "37", "38"
] as const;

export const PRIMARY_FDI_TEETH = [
  "55", "54", "53", "52", "51",
  "61", "62", "63", "64", "65",
  "85", "84", "83", "82", "81",
  "71", "72", "73", "74", "75"
] as const;

export const TOOTH_CONDITION_OPTIONS = [
  { label: "سليم", value: "sound" },
  { label: "تسوس", value: "caries" },
  { label: "حشوة", value: "restored" },
  { label: "تاج", value: "crown" },
  { label: "مفقود", value: "missing" },
  { label: "علاج عصب", value: "root_canal" },
  { label: "زرعة", value: "implant" },
  { label: "كسر", value: "fracture" }
] as const;

export const TOOTH_CONDITION_LABELS: Record<string, string> = Object.fromEntries(
  TOOTH_CONDITION_OPTIONS.map((item) => [item.value, item.label])
);

export const TREATMENT_PLAN_PRIORITY_OPTIONS = [
  { label: "منخفضة", value: "low" },
  { label: "متوسطة", value: "medium" },
  { label: "عالية", value: "high" },
  { label: "عاجلة", value: "urgent" }
] as const;

export const TREATMENT_PLAN_PRIORITY_LABELS: Record<string, string> = {
  low: "منخفضة",
  medium: "متوسطة",
  high: "عالية",
  urgent: "عاجلة"
};

export const TREATMENT_PLAN_STATUS_LABELS: Record<string, string> = {
  planned: "مخطط",
  in_progress: "قيد التنفيذ",
  completed: "مكتمل",
  cancelled: "ملغي"
};

export type ToothCondition = {
  id: string;
  toothNumber: string;
  condition?: string | null;
  surfacesAffected?: string | null;
  treatmentDone?: string | null;
  notes?: string | null;
};

export type DentalChart = {
  id: string;
  patientId: string;
  chartDate: string;
  doctorName?: string | null;
  teeth: ToothCondition[];
};

export type UpdateToothInput = {
  toothNumber: string;
  condition?: string | null;
  surfacesAffected?: string | null;
  treatmentDone?: string | null;
  notes?: string | null;
};

export type GeneralTreatment = {
  id: string;
  patientId: string;
  visitId?: string | null;
  patientName?: string | null;
  treatmentType: string;
  toothNumber?: string | null;
  materialUsed?: string | null;
  anesthesiaType?: string | null;
  cost?: number | null;
  doctorName?: string | null;
  notes?: string | null;
  createdAt: string;
};

export type CreateGeneralTreatmentInput = {
  patientId: string;
  visitId?: string | null;
  treatmentType: string;
  toothNumber?: string | null;
  materialUsed?: string | null;
  anesthesiaType?: string | null;
  doctorId?: string | null;
  notes?: string | null;
};

export type PerioRecord = {
  id: string;
  patientId: string;
  toothNumber: number;
  probingDepth: number;
  clinicalAttachment: number;
  bleedingOnProbing: boolean;
  plaqueIndex: number;
  gingivalIndex: number;
  furcation: number;
  mobility: number;
  notes?: string | null;
  doctorName?: string | null;
  createdAt: string;
};

export type CreatePerioRecordInput = {
  patientId: string;
  toothNumber: number;
  probingDepth: number;
  clinicalAttachment: number;
  bleedingOnProbing: boolean;
  plaqueIndex: number;
  gingivalIndex: number;
  furcation: number;
  mobility: number;
  notes?: string | null;
  doctorId?: string | null;
};

export type GeneralTreatmentPlanItem = {
  id: string;
  patientId: string;
  toothNumber?: string | null;
  treatment: string;
  priority: string;
  status: string;
  estimatedCost?: number | null;
  notes?: string | null;
  doctorName?: string | null;
  createdAt: string;
  completedAt?: string | null;
};

export type CreateGeneralTreatmentPlanInput = {
  patientId: string;
  toothNumber?: string | null;
  treatment: string;
  priority: "low" | "medium" | "high" | "urgent";
  notes?: string | null;
  doctorId?: string | null;
};

export function canUseGeneralDentistry(role?: string | null): boolean {
  return role === "Admin" || role === "GeneralDentist";
}

export function toothConditionLabel(value?: string | null): string {
  if (!value) return "غير مسجل";
  return TOOTH_CONDITION_LABELS[value] ?? value;
}

export function isPermanentFdiTooth(value: string): boolean {
  return (PERMANENT_FDI_TEETH as readonly string[]).includes(value);
}
