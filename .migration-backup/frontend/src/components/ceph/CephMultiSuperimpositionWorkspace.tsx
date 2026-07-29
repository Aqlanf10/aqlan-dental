"use client";

import { useMemo, useState } from "react";
import { useQueries } from "@tanstack/react-query";
import { AlertTriangle, Layers3, Loader2, X } from "lucide-react";
import api from "@/lib/api";
import { formatArabicDate } from "@/lib/utils";
import {
  ANALYSIS_TYPE_AR,
  type CephAnalysis,
  type CephVersionDetail,
  type CephVersionListItem,
} from "@/types/ceph";
import { CephSuperimposeCanvas } from "@/components/ceph/CephSuperimposeCanvas";
import { hasMixedOrthoCases } from "@/lib/cephSuperimposition";

const MAX_RECORDS = 6;
const COLORS = ["#2563eb", "#059669", "#dc2626", "#7c3aed", "#d97706", "#0891b2"];

interface SelectedEntry {
  key: string;
  analysisId: string;
  versionId?: string;
}

interface LoadedEntry {
  key: string;
  analysisId: string;
  orthoCaseId: string;
  patientName: string;
  label: string;
  date: string;
  landmarks: CephAnalysis["landmarks"];
}

interface Props {
  initialAnalysisIds: string[];
}

function analysisEntry(analysisId: string): SelectedEntry {
  return { key: `analysis:${analysisId}`, analysisId };
}

export function CephMultiSuperimpositionWorkspace({ initialAnalysisIds }: Props) {
  const [entries, setEntries] = useState<SelectedEntry[]>(() =>
    [...new Set(initialAnalysisIds)].slice(0, MAX_RECORDS).map(analysisEntry),
  );
  const [versionChoice, setVersionChoice] = useState("");

  const entryQueries = useQueries({
    queries: entries.map(entry => ({
      queryKey: ["ceph-superimposition-entry", entry.key],
      retry: false,
      queryFn: async (): Promise<LoadedEntry> => {
        const analysisRequest = api.get<CephAnalysis>(`/api/ceph/${encodeURIComponent(entry.analysisId)}`);
        if (!entry.versionId) {
          const analysis = (await analysisRequest).data;
          return {
            key: entry.key,
            analysisId: analysis.id,
            orthoCaseId: analysis.orthoCaseId,
            patientName: analysis.patientName,
            label: `تحليل ${ANALYSIS_TYPE_AR[analysis.analysisType] ?? analysis.analysisType}`,
            date: formatArabicDate(analysis.analysisDate),
            landmarks: analysis.landmarks,
          };
        }

        const [analysisResponse, versionResponse] = await Promise.all([
          analysisRequest,
          api.get<CephVersionDetail>(
            `/api/ceph/${encodeURIComponent(entry.analysisId)}/versions/${encodeURIComponent(entry.versionId)}`,
          ),
        ]);
        const analysis = analysisResponse.data;
        const version = versionResponse.data;
        return {
          key: entry.key,
          analysisId: analysis.id,
          orthoCaseId: analysis.orthoCaseId,
          patientName: analysis.patientName,
          label: `نسخة: ${version.label}`,
          date: formatArabicDate(version.snapshotDate),
          landmarks: version.landmarks,
        };
      },
    })),
  });

  const parentAnalysisIds = useMemo(
    () => [...new Set(entries.map(entry => entry.analysisId))],
    [entries],
  );
  const versionListQueries = useQueries({
    queries: parentAnalysisIds.map(analysisId => ({
      queryKey: ["ceph-versions", analysisId],
      retry: false,
      queryFn: async () => ({
        analysisId,
        versions: (
          await api.get<CephVersionListItem[]>(`/api/ceph/${encodeURIComponent(analysisId)}/versions`)
        ).data,
      }),
    })),
  });

  const loaded = entryQueries
    .map(query => query.data)
    .filter((entry): entry is LoadedEntry => Boolean(entry));
  const loading = entryQueries.some(query => query.isLoading);
  const failed = entryQueries.some(query => query.isError);
  const hasMixedCases = hasMixedOrthoCases(loaded);

  const availableVersions = versionListQueries
    .flatMap(query => query.data?.versions.map(version => ({
      ...version,
      key: `version:${version.cephAnalysisId}:${version.id}`,
    })) ?? [])
    .filter(version => !entries.some(entry => entry.key === version.key));

  const records = useMemo(
    () => loaded.map((entry, index) => ({
      id: entry.key,
      label: entry.label,
      date: entry.date,
      color: COLORS[index % COLORS.length],
      landmarks: entry.landmarks,
    })),
    [loaded],
  );

  const addVersion = (key: string) => {
    setVersionChoice("");
    if (!key || entries.length >= MAX_RECORDS) return;
    const version = availableVersions.find(item => item.key === key);
    if (!version) return;
    setEntries(current => [
      ...current,
      { key: version.key, analysisId: version.cephAnalysisId, versionId: version.id },
    ]);
  };

  const removeEntry = (key: string) => {
    setEntries(current => current.filter(entry => entry.key !== key));
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-gray-200 pb-3">
        <div>
          <h1 className="flex items-center gap-2 text-xl font-extrabold text-gray-900">
            <Layers3 className="h-5 w-5 text-clinic-blue" />
            التراكب السيفالومتري متعدد السجلات
          </h1>
          {loaded[0] && <p className="mt-1 text-xs text-gray-500">{loaded[0].patientName}</p>}
        </div>
        <div className="no-print flex items-center gap-2">
          <select
            value={versionChoice}
            onChange={event => addVersion(event.target.value)}
            disabled={entries.length >= MAX_RECORDS || availableVersions.length === 0}
            aria-label="إضافة نسخة محفوظة إلى التراكب"
            className="max-w-64 rounded-md border border-gray-200 bg-white px-2.5 py-2 text-xs text-gray-700 disabled:opacity-50"
          >
            <option value="">إضافة نسخة محفوظة</option>
            {availableVersions.map(version => (
              <option key={version.key} value={version.key}>
                {version.label} · {formatArabicDate(version.snapshotDate)}
              </option>
            ))}
          </select>
          <span className="text-[10px] tabular-nums text-gray-400">{entries.length}/{MAX_RECORDS}</span>
        </div>
      </div>

      <div className="no-print flex flex-wrap gap-2">
        {loaded.map((entry, index) => (
          <span
            key={entry.key}
            className="inline-flex max-w-full items-center gap-2 rounded-md border border-gray-200 bg-white px-2.5 py-1.5 text-xs text-gray-700"
          >
            <span className="h-2.5 w-2.5 shrink-0" style={{ backgroundColor: COLORS[index % COLORS.length] }} />
            <span className="max-w-52 truncate">{entry.label} · {entry.date}</span>
            <button
              type="button"
              onClick={() => removeEntry(entry.key)}
              aria-label={`إزالة ${entry.label}`}
              title="إزالة من التراكب"
              className="text-gray-400 hover:text-red-600"
            >
              <X className="h-3.5 w-3.5" />
            </button>
          </span>
        ))}
      </div>

      {loading ? (
        <div className="flex h-80 items-center justify-center text-clinic-blue">
          <Loader2 className="h-6 w-6 animate-spin" />
        </div>
      ) : failed ? (
        <div className="flex items-center gap-2 rounded-md border border-red-200 bg-red-50 p-3 text-xs text-red-700">
          <AlertTriangle className="h-4 w-4" />
          تعذر تحميل سجل واحد أو أكثر، أو لا تملك صلاحية الوصول إليه.
        </div>
      ) : hasMixedCases ? (
        <div className="flex items-center gap-2 rounded-md border border-red-200 bg-red-50 p-3 text-xs text-red-700">
          <AlertTriangle className="h-4 w-4" />
          يجب أن تنتمي جميع التحليلات والنسخ إلى حالة التقويم نفسها.
        </div>
      ) : records.length < 2 ? (
        <div className="rounded-md border border-amber-200 bg-amber-50 p-3 text-xs text-amber-700">
          أضف سجلين على الأقل لعرض التراكب.
        </div>
      ) : (
        <CephSuperimposeCanvas records={records} initialReferenceId={records[0].id} />
      )}
    </div>
  );
}
