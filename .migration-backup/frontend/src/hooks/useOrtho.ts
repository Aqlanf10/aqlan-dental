import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { orthoService } from "@/services/orthoService";
import { toast } from "@/stores/toastStore";
import type {
  ClinicalExam,
  CreateOrthoVisitRequest,
  ExtractionDecision,
  OrthoDiagnosis,
  OrthoPhoto,
  ProblemListItem,
  RecordsChecklist,
  RetentionRecord,
  RetentionVisit,
  TreatmentPlan,
  UpdateOrthoPhotoRequest,
} from "@/types/ortho";

export const orthoKeys = {
  case: (caseId: string) => ["ortho-case", caseId] as const,
  overview: (caseId: string) => ["ortho-overview", caseId] as const,
  exam: (caseId: string) => ["ortho-exam", caseId] as const,
  problems: (caseId: string) => ["ortho-problems", caseId] as const,
  diagnosis: (caseId: string) => ["ortho-diagnosis", caseId] as const,
  plan: (caseId: string) => ["ortho-plan", caseId] as const,
  plans: (caseId: string) => ["ortho-plans", caseId] as const,
  stages: (caseId: string) => ["ortho-stages", caseId] as const,
  visits: (caseId: string) => ["ortho-visits", caseId] as const,
  extraction: (caseId: string) => ["ortho-extraction", caseId] as const,
  retention: (caseId: string) => ["ortho-retention", caseId] as const,
  photos: (caseId: string) => ["ortho-photos", caseId] as const,
  checklist: (caseId: string) => ["ortho-checklist", caseId] as const,
  ceph: (caseId: string) => ["ortho-ceph", caseId] as const,
  cephDetail: (analysisId: string) => ["ortho-ceph-detail", analysisId] as const,
  photoAnalyses: (caseId: string) => ["ortho-photo-analyses", caseId] as const,
};

export function useOrthoCase(caseId: string) {
  return useQuery({
    queryKey: orthoKeys.case(caseId),
    queryFn: async () => (await orthoService.getCase(caseId)).data,
    enabled: !!caseId,
  });
}

export function useOrthoOverview(caseId: string) {
  return useQuery({
    queryKey: orthoKeys.overview(caseId),
    queryFn: async () => (await orthoService.getOverview(caseId)).data,
    enabled: !!caseId,
  });
}

export function useClinicalExam(caseId: string) {
  return useQuery({
    queryKey: orthoKeys.exam(caseId),
    queryFn: async () => (await orthoService.getClinicalExam(caseId)).data,
    enabled: !!caseId,
  });
}

export function useSaveClinicalExam(caseId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: Partial<ClinicalExam>) => orthoService.saveClinicalExam(caseId, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: orthoKeys.exam(caseId) });
      qc.invalidateQueries({ queryKey: orthoKeys.overview(caseId) });
      toast.success("تم حفظ الفحص السريري");
    },
    onError: () => toast.error("فشل حفظ الفحص السريري"),
  });
}

export function useProblemList(caseId: string) {
  return useQuery({
    queryKey: orthoKeys.problems(caseId),
    queryFn: async () => (await orthoService.getProblems(caseId)).data,
    enabled: !!caseId,
  });
}

export function useAddProblem(caseId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: Partial<ProblemListItem>) => orthoService.addProblem(caseId, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: orthoKeys.problems(caseId) });
      qc.invalidateQueries({ queryKey: orthoKeys.overview(caseId) });
      toast.success("تمت إضافة المشكلة");
    },
    onError: () => toast.error("فشل إضافة المشكلة"),
  });
}

export function useDeleteProblem(caseId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (problemId: string) => orthoService.deleteProblem(caseId, problemId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: orthoKeys.problems(caseId) });
      qc.invalidateQueries({ queryKey: orthoKeys.overview(caseId) });
      toast.success("تم حذف المشكلة");
    },
    onError: () => toast.error("فشل حذف المشكلة"),
  });
}

export function useDiagnosis(caseId: string) {
  return useQuery({
    queryKey: orthoKeys.diagnosis(caseId),
    queryFn: async () => (await orthoService.getDiagnosis(caseId)).data,
    enabled: !!caseId,
  });
}

export function useSaveDiagnosis(caseId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: Partial<OrthoDiagnosis>) => orthoService.saveDiagnosis(caseId, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: orthoKeys.diagnosis(caseId) });
      qc.invalidateQueries({ queryKey: orthoKeys.overview(caseId) });
      toast.success("تم حفظ التشخيص");
    },
    onError: () => toast.error("فشل حفظ التشخيص"),
  });
}

export function useApproveDiagnosis(caseId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => orthoService.approveDiagnosis(caseId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: orthoKeys.diagnosis(caseId) });
      qc.invalidateQueries({ queryKey: orthoKeys.overview(caseId) });
      toast.success("تم اعتماد التشخيص");
    },
    onError: () => toast.error("فشل اعتماد التشخيص"),
  });
}

export function useCaseCephAnalyses(caseId: string) {
  return useQuery({
    queryKey: orthoKeys.ceph(caseId),
    queryFn: async () => (await orthoService.getCephAnalyses(caseId)).data,
    enabled: !!caseId,
  });
}

export function useCaseCephAnalysis(analysisId?: string) {
  return useQuery({
    queryKey: orthoKeys.cephDetail(analysisId ?? ""),
    queryFn: async () => (await orthoService.getCephAnalysis(analysisId!)).data,
    enabled: !!analysisId,
  });
}

export function useCasePhotoAnalyses(caseId: string) {
  return useQuery({
    queryKey: orthoKeys.photoAnalyses(caseId),
    queryFn: async () => (await orthoService.getPhotoAnalyses(caseId)).data,
    enabled: !!caseId,
  });
}

export function useTreatmentPlan(caseId: string) {
  return useQuery({
    queryKey: orthoKeys.plan(caseId),
    queryFn: async () => (await orthoService.getTreatmentPlan(caseId)).data,
    enabled: !!caseId,
  });
}

export function useTreatmentPlans(caseId: string) {
  return useQuery({
    queryKey: orthoKeys.plans(caseId),
    queryFn: async () => (await orthoService.getTreatmentPlans(caseId)).data,
    enabled: !!caseId,
  });
}

export function useSaveTreatmentPlan(caseId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: Partial<TreatmentPlan>) => orthoService.saveTreatmentPlan(caseId, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: orthoKeys.plan(caseId) });
      qc.invalidateQueries({ queryKey: orthoKeys.plans(caseId) });
      qc.invalidateQueries({ queryKey: orthoKeys.overview(caseId) });
      toast.success("تم حفظ خطة العلاج");
    },
    onError: () => toast.error("فشل حفظ خطة العلاج"),
  });
}

export function useCreateTreatmentPlan(caseId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: Partial<TreatmentPlan>) => orthoService.createTreatmentPlan(caseId, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: orthoKeys.plans(caseId) });
      qc.invalidateQueries({ queryKey: orthoKeys.plan(caseId) });
      qc.invalidateQueries({ queryKey: orthoKeys.overview(caseId) });
      toast.success("تم إنشاء خطة علاج جديدة");
    },
    onError: () => toast.error("فشل إنشاء خطة العلاج"),
  });
}

export function useUpdateTreatmentPlan(caseId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ planId, data }: { planId: string; data: Partial<TreatmentPlan> }) =>
      orthoService.updateTreatmentPlan(caseId, planId, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: orthoKeys.plans(caseId) });
      qc.invalidateQueries({ queryKey: orthoKeys.plan(caseId) });
      qc.invalidateQueries({ queryKey: orthoKeys.overview(caseId) });
      toast.success("تم تحديث خطة العلاج");
    },
    onError: () => toast.error("فشل تحديث خطة العلاج"),
  });
}

export function useApproveTreatmentPlan(caseId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => orthoService.approveTreatmentPlan(caseId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: orthoKeys.plan(caseId) });
      qc.invalidateQueries({ queryKey: orthoKeys.plans(caseId) });
      qc.invalidateQueries({ queryKey: orthoKeys.overview(caseId) });
      toast.success("تم اعتماد خطة العلاج");
    },
    onError: () => toast.error("فشل اعتماد الخطة"),
  });
}

export function useApproveSpecificTreatmentPlan(caseId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (planId: string) => orthoService.approveSpecificTreatmentPlan(caseId, planId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: orthoKeys.plan(caseId) });
      qc.invalidateQueries({ queryKey: orthoKeys.plans(caseId) });
      qc.invalidateQueries({ queryKey: orthoKeys.overview(caseId) });
      toast.success("تم اعتماد خطة العلاج");
    },
    onError: () => toast.error("فشل اعتماد الخطة"),
  });
}

export function useDeleteTreatmentPlan(caseId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (planId: string) => orthoService.deleteTreatmentPlan(caseId, planId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: orthoKeys.plan(caseId) });
      qc.invalidateQueries({ queryKey: orthoKeys.plans(caseId) });
      qc.invalidateQueries({ queryKey: orthoKeys.overview(caseId) });
      toast.success("تم حذف خطة العلاج");
    },
    onError: () => toast.error("فشل حذف الخطة"),
  });
}

export function useOrthoStages(caseId: string) {
  return useQuery({
    queryKey: orthoKeys.stages(caseId),
    queryFn: async () => (await orthoService.getStages(caseId)).data,
    enabled: !!caseId,
  });
}

export function useOrthoVisits(caseId: string) {
  return useQuery({
    queryKey: orthoKeys.visits(caseId),
    queryFn: async () => (await orthoService.getVisits(caseId)).data,
    enabled: !!caseId,
  });
}

export function useAddOrthoVisit(caseId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateOrthoVisitRequest) => orthoService.addVisit(caseId, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: orthoKeys.visits(caseId) });
      qc.invalidateQueries({ queryKey: orthoKeys.case(caseId) });
      qc.invalidateQueries({ queryKey: orthoKeys.overview(caseId) });
      toast.success("تم تسجيل الزيارة");
    },
    onError: () => toast.error("فشل تسجيل الزيارة"),
  });
}

export function useUpdateOrthoVisit(caseId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ visitId, data }: { visitId: string; data: CreateOrthoVisitRequest }) =>
      orthoService.updateVisit(caseId, visitId, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: orthoKeys.visits(caseId) });
      qc.invalidateQueries({ queryKey: orthoKeys.case(caseId) });
      qc.invalidateQueries({ queryKey: orthoKeys.overview(caseId) });
      toast.success("تم تحديث الزيارة");
    },
    onError: () => toast.error("فشل تحديث الزيارة"),
  });
}

export function useDeleteOrthoVisit(caseId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (visitId: string) => orthoService.deleteVisit(caseId, visitId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: orthoKeys.visits(caseId) });
      qc.invalidateQueries({ queryKey: orthoKeys.case(caseId) });
      qc.invalidateQueries({ queryKey: orthoKeys.overview(caseId) });
      toast.success("تم حذف الزيارة");
    },
    onError: () => toast.error("فشل حذف الزيارة"),
  });
}

export function useExtractionDecision(caseId: string) {
  return useQuery({
    queryKey: orthoKeys.extraction(caseId),
    queryFn: async () => (await orthoService.getExtractionDecision(caseId)).data,
    enabled: !!caseId,
  });
}

export function useSaveExtractionDecision(caseId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: Partial<ExtractionDecision>) => orthoService.saveExtractionDecision(caseId, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: orthoKeys.extraction(caseId) });
      qc.invalidateQueries({ queryKey: orthoKeys.case(caseId) });
      toast.success("تم حفظ قرار الخلع");
    },
    onError: () => toast.error("فشل حفظ قرار الخلع"),
  });
}

export function useRetention(caseId: string) {
  return useQuery({
    queryKey: orthoKeys.retention(caseId),
    queryFn: async () => (await orthoService.getRetention(caseId)).data,
    enabled: !!caseId,
  });
}

export function useSaveRetention(caseId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: Partial<RetentionRecord>) => orthoService.saveRetention(caseId, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: orthoKeys.retention(caseId) });
      qc.invalidateQueries({ queryKey: orthoKeys.overview(caseId) });
      toast.success("تم حفظ سجل الاحتفاظ");
    },
    onError: () => toast.error("فشل حفظ سجل الاحتفاظ"),
  });
}

export function useAddRetentionVisit(caseId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: Partial<RetentionVisit>) => orthoService.addRetentionVisit(caseId, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: orthoKeys.retention(caseId) });
      toast.success("تم تسجيل زيارة الاحتفاظ");
    },
    onError: () => toast.error("فشل تسجيل زيارة الاحتفاظ"),
  });
}

export function useOrthoPhotos(caseId: string) {
  return useQuery({
    queryKey: orthoKeys.photos(caseId),
    queryFn: async () => (await orthoService.getPhotos(caseId)).data,
    enabled: !!caseId,
  });
}

/**
 * تحديث وسوم صورة تقويم (الفئة/النوع الفرعي/المرحلة/الإدراج في التقرير) —
 * تحديث تفاؤلي فوري مع تراجع تلقائي ورسالة عربية عند الفشل.
 */
export function useUpdateOrthoPhoto(caseId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ photoId, data }: { photoId: string; data: UpdateOrthoPhotoRequest }) =>
      orthoService.updatePhoto(caseId, photoId, data),
    onMutate: async ({ photoId, data }) => {
      await qc.cancelQueries({ queryKey: orthoKeys.photos(caseId) });
      const previous = qc.getQueryData<OrthoPhoto[]>(orthoKeys.photos(caseId));
      qc.setQueryData<OrthoPhoto[]>(orthoKeys.photos(caseId), (old) =>
        old?.map((p) => (p.id === photoId ? { ...p, ...data } : p))
      );
      return { previous };
    },
    onError: (_err, _vars, context) => {
      if (context?.previous) {
        qc.setQueryData(orthoKeys.photos(caseId), context.previous);
      }
      toast.error("فشل تحديث بيانات الصورة");
    },
    onSettled: () => {
      qc.invalidateQueries({ queryKey: orthoKeys.photos(caseId) });
      qc.invalidateQueries({ queryKey: orthoKeys.checklist(caseId) });
    },
  });
}

export function useRecordsChecklist(caseId: string) {
  return useQuery({
    queryKey: orthoKeys.checklist(caseId),
    queryFn: async () => (await orthoService.getChecklist(caseId)).data,
    enabled: !!caseId,
  });
}

export function useSaveChecklist(caseId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: Partial<RecordsChecklist>) => orthoService.saveChecklist(caseId, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: orthoKeys.checklist(caseId) });
      qc.invalidateQueries({ queryKey: orthoKeys.overview(caseId) });
      toast.success("تم تحديث قائمة السجلات");
    },
    onError: () => toast.error("فشل تحديث قائمة السجلات"),
  });
}
