"use client";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import api from "@/lib/api";
import type {
  ProblemListSuggestion,
  ExtractionDecisionAnalysis,
  CaseSummary,
  VtoSuggestion,
  SmartAlert,
  Notification,
  SaveRecommendationRequest,
  AiRecommendation,
} from "@/types/ai";

// ─── Smart Alerts ──────────────────────────────────────────────────────────

export function useSmartAlerts() {
  return useQuery({
    queryKey: ["smart-alerts"],
    queryFn: async () => {
      const { data } = await api.get<SmartAlert[]>("/api/ai/smart-alerts");
      return data;
    },
    staleTime: 2 * 60_000, // 2 minutes
    refetchInterval: 5 * 60_000, // refetch every 5 min
  });
}

// ─── Notifications ──────────────────────────────────────────────────────────

export function useNotifications(unreadOnly = false) {
  return useQuery({
    queryKey: ["notifications", { unreadOnly }],
    queryFn: async () => {
      const { data } = await api.get("/api/notifications", {
        params: { unreadOnly },
      });
      return data as { notifications: Notification[]; unreadCount: number };
    },
    staleTime: 30_000,
  });
}

export function useMarkNotificationRead() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const { data } = await api.put(`/api/notifications/${id}/read`);
      return data;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["notifications"] }),
  });
}

export function useMarkAllNotificationsRead() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      const { data } = await api.put("/api/notifications/read-all");
      return data;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["notifications"] }),
  });
}

// ─── AI Problem List Suggestion ──────────────────────────────────────────────

export function useProblemListSuggestion() {
  return useMutation({
    mutationFn: async (params: {
      clinicalExam: unknown;
      cephMeasurements?: unknown;
      patientAge?: number;
      patientGender?: string;
    }): Promise<{ suggestions: ProblemListSuggestion[] }> => {
      const resp = await fetch("/api/ai/problem-list-suggestion", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(params),
        signal: AbortSignal.timeout(60_000),
      });
      if (!resp.ok) throw new Error("فشل اقتراح قائمة المشاكل");
      return resp.json();
    },
  });
}

// ─── AI Extraction Decision ──────────────────────────────────────────────────

export function useExtractionDecision() {
  return useMutation({
    mutationFn: async (params: {
      clinicalExam: unknown;
      cephMeasurements?: unknown;
      modelAnalysis?: unknown;
      patientAge?: number;
      patientGender?: string;
      problemList?: unknown;
    }): Promise<ExtractionDecisionAnalysis> => {
      const resp = await fetch("/api/ai/extraction-decision", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(params),
        signal: AbortSignal.timeout(60_000),
      });
      if (!resp.ok) throw new Error("فشل تحليل قرار الخلع");
      return resp.json();
    },
  });
}

// ─── AI Case Summary ────────────────────────────────────────────────────────

export function useCaseSummary() {
  return useMutation({
    mutationFn: async (params: {
      caseData: unknown;
    }): Promise<CaseSummary> => {
      const resp = await fetch("/api/ai/case-summary", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(params),
        signal: AbortSignal.timeout(60_000),
      });
      if (!resp.ok) throw new Error("فشل إنشاء ملخص الحالة");
      return resp.json();
    },
  });
}

// ─── AI VTO Suggestion ──────────────────────────────────────────────────────

export function useVtoSuggestion() {
  return useMutation({
    mutationFn: async (params: {
      caseData: unknown;
      treatmentPlan?: unknown;
    }): Promise<VtoSuggestion> => {
      const resp = await fetch("/api/ai/vto-suggestion", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(params),
        signal: AbortSignal.timeout(60_000),
      });
      if (!resp.ok) throw new Error("فشل اقتراح VTO");
      return resp.json();
    },
  });
}

// ─── AI Recommendations (CRUD) ──────────────────────────────────────────────

export function useAiRecommendations(patientId: string, context?: string) {
  return useQuery({
    queryKey: ["ai-recommendations", patientId, context],
    queryFn: async () => {
      const { data } = await api.get<AiRecommendation[]>(
        `/api/ai/recommendations/${patientId}`,
        { params: { context } }
      );
      return data;
    },
    enabled: !!patientId,
  });
}

export function useSaveRecommendation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: SaveRecommendationRequest) => {
      const { data } = await api.post("/api/ai/save-recommendation", req);
      return data;
    },
    onSuccess: (_, req) =>
      qc.invalidateQueries({ queryKey: ["ai-recommendations", req.patientId] }),
  });
}

export function useUpdateRecommendationAction() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (params: {
      id: string;
      action: "approved" | "rejected" | "modified";
      doctorId?: string;
    }) => {
      const { data } = await api.put(
        `/api/ai/recommendation/${params.id}/action`,
        params
      );
      return data;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["ai-recommendations"] }),
  });
}

// ─── Ortho Case Data for AI ──────────────────────────────────────────────────

export function useOrthoCaseDataForAi(caseId: string) {
  return useQuery({
    queryKey: ["ortho-case-ai-data", caseId],
    queryFn: async () => {
      const { data } = await api.get(`/api/ai/ortho-case-data/${caseId}`);
      return data;
    },
    enabled: !!caseId,
    staleTime: 60_000,
  });
}
