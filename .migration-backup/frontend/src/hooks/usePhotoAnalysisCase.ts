"use client";

import { useCallback, useEffect, useState } from "react";
import api from "@/lib/api";

export interface SavedPhotoAnalysis {
  id: string;
  orthoCaseId: string;
  viewType: string;
  imageFileUrl: string;
  analysisDate: string;
}

type Pt = { x: number; y: number };

/**
 * Save/list helper for facial photo analyses attached to an orthodontic case.
 * No-ops gracefully when there is no case context (standalone analysis).
 */
export function usePhotoAnalysisCase(orthoCaseId: string | null, viewType: "profile" | "frontal") {
  const [saved, setSaved] = useState<SavedPhotoAnalysis[]>([]);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const reload = useCallback(async () => {
    if (!orthoCaseId) { setSaved([]); return; }
    try {
      const { data } = await api.get<SavedPhotoAnalysis[]>(
        `/api/photo-analysis?orthoCaseId=${encodeURIComponent(orthoCaseId)}`,
      );
      setSaved(data.filter((d) => d.viewType === viewType));
    } catch {
      // listing failures are non-fatal — the analysis tool still works
    }
  }, [orthoCaseId, viewType]);

  useEffect(() => { reload(); }, [reload]);

  const save = useCallback(
    async (imageFileUrl: string, points: Record<string, Pt>, measurements: unknown): Promise<string | null> => {
      if (!orthoCaseId) return null;
      setSaving(true);
      setError(null);
      try {
        const { data } = await api.post<{ id: string }>("/api/photo-analysis", {
          orthoCaseId,
          viewType,
          imageFileUrl,
          landmarksJson: JSON.stringify(points),
          measurementsJson: JSON.stringify(measurements),
        });
        await reload();
        return data.id;
      } catch (err) {
        const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
        setError(msg ?? "تعذّر حفظ التحليل في الحالة");
        return null;
      } finally {
        setSaving(false);
      }
    },
    [orthoCaseId, viewType, reload],
  );

  return { saved, saving, error, save, reload };
}
