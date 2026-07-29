// Spec 010: outgoing imaging orders the patient carries to an external
// radiology center. StudyType values align with Radiograph.XrayType.

export type RadiologyStudyType =
  | "panoramic"
  | "cbct_3d"
  | "lateral_ceph"
  | "pa_ceph"
  | "bitewing"
  | "periapical"
  | "other";

/** Arabic labels for the staff UI + English labels for the printed referral. */
export const STUDY_TYPES: { value: RadiologyStudyType; labelAr: string; labelEn: string }[] = [
  { value: "panoramic",    labelAr: "أشعة بانوراما (OPG)",            labelEn: "Panoramic Radiograph (OPG)" },
  { value: "cbct_3d",      labelAr: "أشعة ثلاثية الأبعاد (CBCT)",     labelEn: "3D CBCT Scan" },
  { value: "lateral_ceph", labelAr: "أشعة سيفالومترية جانبية",        labelEn: "Lateral Cephalometric Radiograph" },
  { value: "pa_ceph",      labelAr: "أشعة سيفالومترية أمامية خلفية",  labelEn: "PA Cephalometric Radiograph" },
  { value: "bitewing",     labelAr: "أشعة عضّة جناحية",               labelEn: "Bitewing Radiograph" },
  { value: "periapical",   labelAr: "أشعة ذروية",                     labelEn: "Periapical Radiograph" },
  { value: "other",        labelAr: "أخرى",                           labelEn: "Other" },
];

export const studyTypeLabelAr = (t: string) =>
  STUDY_TYPES.find((s) => s.value === t)?.labelAr ?? t;
export const studyTypeLabelEn = (t: string) =>
  STUDY_TYPES.find((s) => s.value === t)?.labelEn ?? t;

export interface RadiologyOrderListItem {
  id: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  doctorName?: string;
  studyType: string;
  region?: string;
  createdAt: string;
}

export interface RadiologyOrder {
  id: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  patientDateOfBirth?: string;
  patientGender?: string;
  doctorName?: string;
  studyType: string;
  region?: string;
  clinicalNotes?: string;
  createdAt: string;
}

export interface CreateRadiologyOrderRequest {
  patientId: string;
  doctorId?: string;
  studyType: RadiologyStudyType;
  region?: string;
  clinicalNotes?: string;
}
