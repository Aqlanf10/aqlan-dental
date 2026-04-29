"use client";

import { useState, useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import api from "@/lib/api";
import type {
  CephAnalysis,
  CephLandmark,
  CephMeasurement,
  CephAnalysisList,
  MeasurementGroup,
} from "@/types/ceph";
import { ANALYSIS_GROUPS } from "@/types/ceph";
import { buildMeasurementList } from "@/lib/cephMath";

// ─── Comparison types ────────────────────────────────────────────────────────

export interface LandmarkDelta {
  key: string;
  nameAr: string;
  x1: number; y1: number;
  x2: number; y2: number;
  dx: number; dy: number;
  distance: number; // px
  distanceMm: number | null; // mm if calibrated
}

export interface MeasurementDelta {
  name: string;
  nameAr: string;
  value1: number | null;
  value2: number | null;
  unit: string;
  normal: number;
  change: number | null;
  changeDirection: "up" | "down" | "same" | "na";
  severity1: CephMeasurement["severity"];
  severity2: CephMeasurement["severity"];
  analysisGroup: MeasurementGroup;
  interpretation: string;
}

export interface ComparisonResult {
  analysis1: CephAnalysis;
  analysis2: CephAnalysis;
  landmarkDeltas: LandmarkDelta[];
  measurementDeltas: MeasurementDelta[];
  summary: {
    totalMeasurements: number;
    improved: number;
    worsened: number;
    unchanged: number;
    growthDirection: string;
    keyFindings: string[];
  };
}

// ─── Superimposition reference planes ────────────────────────────────────────

export type SuperimpositionRef = "SN" | "BaN";

// ─── Hook ────────────────────────────────────────────────────────────────────

export function useCephComparison(orthoCaseId: string | null) {
  const [analysisId1, setAnalysisId1] = useState<string | null>(null);
  const [analysisId2, setAnalysisId2] = useState<string | null>(null);

  // Fetch all analyses for this ortho case
  const analysesQuery = useQuery({
    queryKey: ["ceph-analyses", orthoCaseId],
    queryFn: async () => {
      if (!orthoCaseId) return [];
      const { data } = await api.get<CephAnalysisList[]>(
        `/api/ceph?orthoCaseId=${orthoCaseId}`
      );
      return data;
    },
    enabled: !!orthoCaseId,
    staleTime: 30_000,
  });

  // Fetch analysis 1 detail
  const analysis1Query = useQuery({
    queryKey: ["ceph-analysis", analysisId1],
    queryFn: async () => {
      if (!analysisId1) return null;
      const { data } = await api.get<CephAnalysis>(`/api/ceph/${analysisId1}`);
      return data;
    },
    enabled: !!analysisId1,
    staleTime: 30_000,
  });

  // Fetch analysis 2 detail
  const analysis2Query = useQuery({
    queryKey: ["ceph-analysis", analysisId2],
    queryFn: async () => {
      if (!analysisId2) return null;
      const { data } = await api.get<CephAnalysis>(`/api/ceph/${analysisId2}`);
      return data;
    },
    enabled: !!analysisId2,
    staleTime: 30_000,
  });

  // Compute comparison
  const comparison = useMemo<ComparisonResult | null>(() => {
    const a1 = analysis1Query.data;
    const a2 = analysis2Query.data;
    if (!a1 || !a2) return null;

    // ── Landmark deltas ──
    const lm1Map = new Map<string, CephLandmark>();
    a1.landmarks?.forEach((l) => lm1Map.set(l.key, l));
    const lm2Map = new Map<string, CephLandmark>();
    a2.landmarks?.forEach((l) => lm2Map.set(l.key, l));

    const ppm = a2.pixelsPerMm && a2.pixelsPerMm > 0 ? a2.pixelsPerMm : null;

    const landmarkDeltas: LandmarkDelta[] = [];
    const keySet = new Set<string>();
    lm1Map.forEach((_, k) => keySet.add(k));
    lm2Map.forEach((_, k) => keySet.add(k));
    keySet.forEach((key) => {
      const l1 = lm1Map.get(key);
      const l2 = lm2Map.get(key);
      if (!l1 || !l2) return;

      const dx = l2.x - l1.x;
      const dy = l2.y - l1.y;
      const dist = Math.sqrt(dx * dx + dy * dy);

      landmarkDeltas.push({
        key,
        nameAr: l1.nameAr,
        x1: l1.x, y1: l1.y,
        x2: l2.x, y2: l2.y,
        dx, dy,
        distance: dist,
        distanceMm: ppm ? +(dist / ppm).toFixed(1) : null,
      });
    });

    // ── Measurement deltas ──
    // Compute measurements for both analyses using their landmarks
    const pts1: Record<string, { x: number; y: number }> = {};
    a1.landmarks?.forEach((l) => { pts1[l.key] = { x: l.x, y: l.y }; });
    const pts2: Record<string, { x: number; y: number }> = {};
    a2.landmarks?.forEach((l) => { pts2[l.key] = { x: l.x, y: l.y }; });

    // Use analysis type to determine groups
    const groups1 = ANALYSIS_GROUPS[a1.analysisType as keyof typeof ANALYSIS_GROUPS] ?? ANALYSIS_GROUPS["full"];
    const groups2 = ANALYSIS_GROUPS[a2.analysisType as keyof typeof ANALYSIS_GROUPS] ?? ANALYSIS_GROUPS["full"];
    const groupSet = new Set<MeasurementGroup>();
    groups1.forEach((g) => groupSet.add(g));
    groups2.forEach((g) => groupSet.add(g));
    const allGroups: MeasurementGroup[] = [];
    groupSet.forEach((g) => allGroups.push(g));

    const ppm1 = a1.pixelsPerMm && a1.pixelsPerMm > 0 ? a1.pixelsPerMm : null;
    const ppm2 = a2.pixelsPerMm && a2.pixelsPerMm > 0 ? a2.pixelsPerMm : null;

    // Use saved measurements if available, otherwise compute
    const meas1 = a1.measurements?.length ? a1.measurements : buildMeasurementList(pts1, ppm1, allGroups);
    const meas2 = a2.measurements?.length ? a2.measurements : buildMeasurementList(pts2, ppm2, allGroups);

    const meas1Map = new Map<string, CephMeasurement>();
    meas1.forEach((m) => meas1Map.set(m.name, m));
    const meas2Map = new Map<string, CephMeasurement>();
    meas2.forEach((m) => meas2Map.set(m.name, m));

    const measurementDeltas: MeasurementDelta[] = [];
    const nameSet = new Set<string>();
    meas1Map.forEach((_, k) => nameSet.add(k));
    meas2Map.forEach((_, k) => nameSet.add(k));

    nameSet.forEach((name) => {
      const m1 = meas1Map.get(name);
      const m2 = meas2Map.get(name);
      if (!m1 && !m2) return;

      const v1 = m1?.value ?? null;
      const v2 = m2?.value ?? null;
      const change = v1 !== null && v2 !== null ? +(v2 - v1).toFixed(1) : null;

      let changeDirection: MeasurementDelta["changeDirection"] = "na";
      if (change !== null) {
        if (Math.abs(change) < 0.05) changeDirection = "same";
        else changeDirection = change > 0 ? "up" : "down";
      }

      // Determine if change is improvement or worsening
      // For angular measurements: moving toward normal is improvement
      // For linear: depends on context
      let interpretation = "";
      if (changeDirection === "same") {
        interpretation = "لا تغيير";
      } else if (change !== null && m1 && m2) {
        const dev1 = Math.abs(v1! - m1.normal);
        const dev2 = Math.abs(v2! - m2.normal);
        if (dev2 < dev1) {
          interpretation = "تحسّن ✓";
        } else if (dev2 > dev1) {
          interpretation = "تدهور ✗";
        } else {
          interpretation = "لا تغيير ملحوظ";
        }
      }

      measurementDeltas.push({
        name,
        nameAr: m1?.nameAr ?? m2?.nameAr ?? name,
        value1: v1,
        value2: v2,
        unit: m1?.unit ?? m2?.unit ?? "°",
        normal: m1?.normal ?? m2?.normal ?? 0,
        change,
        changeDirection,
        severity1: m1?.severity ?? "normal",
        severity2: m2?.severity ?? "normal",
        analysisGroup: m1?.analysisGroup ?? m2?.analysisGroup ?? "steiner",
        interpretation,
      });
    });

    // ── Summary ──
    let improved = 0;
    let worsened = 0;
    let unchanged = 0;
    measurementDeltas.forEach((d) => {
      if (d.interpretation.includes("تحسّن")) improved++;
      else if (d.interpretation.includes("تدهور")) worsened++;
      else unchanged++;
    });

    // Determine growth direction from key landmarks
    const pogDelta = landmarkDeltas.find((d) => d.key === "Pog");
    const bDelta = landmarkDeltas.find((d) => d.key === "B");
    const goDelta = landmarkDeltas.find((d) => d.key === "Go");

    let growthDirection = "لا يمكن التحديد";
    if (pogDelta && bDelta) {
      if (pogDelta.dx > 2 || bDelta.dx > 2) {
        growthDirection = "نمو أمامي (mandibular advancement)";
      } else if (pogDelta.dx < -2 || bDelta.dx < -2) {
        growthDirection = "نمو خلفي (retrognathic tendency)";
      } else {
        growthDirection = "نمو متوازن";
      }
    }

    // Key findings
    const keyFindings: string[] = [];
    const anb1 = meas1Map.get("ANB");
    const anb2 = meas2Map.get("ANB");
    if (anb1 && anb2 && anb1.value !== null && anb2.value !== null) {
      const anbChange = anb2.value - anb1.value;
      if (Math.abs(anbChange) > 0.5) {
        keyFindings.push(
          `ANB تغير من ${anb1.value}° إلى ${anb2.value}° (${anbChange > 0 ? "+" : ""}${anbChange.toFixed(1)}°)`
        );
      }
    }

    const fma1 = meas1Map.get("FMA");
    const fma2 = meas2Map.get("FMA");
    if (fma1 && fma2 && fma1.value !== null && fma2.value !== null) {
      const fmaChange = fma2.value - fma1.value;
      if (Math.abs(fmaChange) > 0.5) {
        keyFindings.push(
          `FMA تغير من ${fma1.value}° إلى ${fma2.value}° (${fmaChange > 0 ? "+" : ""}${fmaChange.toFixed(1)}°)`
        );
      }
    }

    if (goDelta && goDelta.distanceMm && goDelta.distanceMm > 2) {
      keyFindings.push(
        `نمو في زاوية الفك بمقدار ${goDelta.distanceMm} مم`
      );
    }

    return {
      analysis1: a1,
      analysis2: a2,
      landmarkDeltas,
      measurementDeltas,
      summary: {
        totalMeasurements: measurementDeltas.length,
        improved,
        worsened,
        unchanged,
        growthDirection,
        keyFindings,
      },
    };
  }, [analysis1Query.data, analysis2Query.data]);

  return {
    analyses: analysesQuery.data ?? [],
    analysesLoading: analysesQuery.isLoading,
    analysis1: analysis1Query.data,
    analysis2: analysis2Query.data,
    comparison,
    analysisId1,
    analysisId2,
    setAnalysisId1,
    setAnalysisId2,
  };
}
