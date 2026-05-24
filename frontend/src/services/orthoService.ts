import api from "@/lib/api";
import type {
  ClinicalExam,
  CreateOrthoVisitRequest,
  ExtractionDecision,
  OrthoCase,
  OrthoDiagnosis,
  OrthoOverview,
  OrthoPhoto,
  OrthoVisit,
  ProblemListItem,
  RetentionRecord,
  RetentionVisit,
  TreatmentPlan,
  TreatmentStage,
} from "@/types/ortho";

const BASE = "/api/ortho-cases";

export const orthoService = {
  getCase: (caseId: string) => api.get<OrthoCase>(`${BASE}/${caseId}`),
  getOverview: (caseId: string) => api.get<OrthoOverview>(`${BASE}/${caseId}/overview`),

  getClinicalExam: (caseId: string) => api.get<ClinicalExam | null>(`${BASE}/${caseId}/clinical-exam`),
  saveClinicalExam: (caseId: string, data: Partial<ClinicalExam>) =>
    api.put(`${BASE}/${caseId}/clinical-exam`, data),

  getProblems: (caseId: string) => api.get<ProblemListItem[]>(`${BASE}/${caseId}/problem-list`),
  addProblem: (caseId: string, data: Partial<ProblemListItem>) =>
    api.post<ProblemListItem>(`${BASE}/${caseId}/problem-list`, data),
  deleteProblem: (caseId: string, problemId: string) =>
    api.delete(`${BASE}/${caseId}/problem-list/${problemId}`),

  getDiagnosis: (caseId: string) => api.get<OrthoDiagnosis | null>(`${BASE}/${caseId}/diagnosis`),
  saveDiagnosis: (caseId: string, data: Partial<OrthoDiagnosis>) =>
    api.put(`${BASE}/${caseId}/diagnosis`, data),

  getTreatmentPlan: (caseId: string) => api.get<TreatmentPlan | null>(`${BASE}/${caseId}/treatment-plan`),
  saveTreatmentPlan: (caseId: string, data: Partial<TreatmentPlan>) =>
    api.put(`${BASE}/${caseId}/treatment-plan`, data),
  approveTreatmentPlan: (caseId: string) =>
    api.patch<TreatmentPlan>(`${BASE}/${caseId}/treatment-plan/approve`),

  getStages: (caseId: string) => api.get<TreatmentStage[]>(`${BASE}/${caseId}/stages`),
  updateStage: (caseId: string, stageId: string, status: string) =>
    api.put<TreatmentStage>(`${BASE}/${caseId}/stages/${stageId}`, { status }),

  getVisits: (caseId: string) => api.get<OrthoVisit[]>(`${BASE}/${caseId}/visits`),
  addVisit: (caseId: string, data: CreateOrthoVisitRequest) =>
    api.post<OrthoVisit>(`${BASE}/${caseId}/visits`, data),

  getExtractionDecision: (caseId: string) =>
    api.get<ExtractionDecision | null>(`${BASE}/${caseId}/extraction-decision`),
  saveExtractionDecision: (caseId: string, data: Partial<ExtractionDecision>) =>
    api.put(`${BASE}/${caseId}/extraction-decision`, data),

  getRetention: (caseId: string) => api.get<RetentionRecord | null>(`${BASE}/${caseId}/retention`),
  saveRetention: (caseId: string, data: Partial<RetentionRecord>) =>
    api.put(`${BASE}/${caseId}/retention`, data),
  addRetentionVisit: (caseId: string, data: Partial<RetentionVisit>) =>
    api.post<RetentionVisit>(`${BASE}/${caseId}/retention/visits`, data),

  getPhotos: (caseId: string) => api.get<OrthoPhoto[]>(`${BASE}/${caseId}/photos`),
  addPhoto: (caseId: string, data: Partial<OrthoPhoto>) =>
    api.post<OrthoPhoto>(`${BASE}/${caseId}/photos`, data),
  deletePhoto: (caseId: string, photoId: string) =>
    api.delete(`${BASE}/${caseId}/photos/${photoId}`),
};
