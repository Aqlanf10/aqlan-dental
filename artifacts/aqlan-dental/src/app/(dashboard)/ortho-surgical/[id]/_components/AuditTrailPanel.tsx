
import { useEffect, useState, useCallback } from "react";
import { History } from "lucide-react";
import type { OrthoSurgicalAuditEntry } from "@/types/orthoSurgical";
import { AUDIT_ACTION_LABELS } from "@/types/orthoSurgical";
import api from "@/lib/api";
import { formatArabicDate } from "@/lib/utils";

interface AuditTrailPanelProps {
  orthoSurgicalCaseId: string;
}

/** Sprint A4 — read-only view of the case's audit trail (existing AuditLogs, filtered per case). */
export function AuditTrailPanel({ orthoSurgicalCaseId }: AuditTrailPanelProps) {
  const [entries, setEntries] = useState<OrthoSurgicalAuditEntry[]>([]);
  const [loading, setLoading] = useState(true);

  const fetchTrail = useCallback(async () => {
    try {
      const res = await api.get<{ data: OrthoSurgicalAuditEntry[] }>(
        `/api/ortho-surgical-cases/${orthoSurgicalCaseId}/audit-trail`
      );
      setEntries(res.data?.data ?? []);
    } catch {
      /* silent — audit trail is supplementary */
    } finally {
      setLoading(false);
    }
  }, [orthoSurgicalCaseId]);

  useEffect(() => { fetchTrail(); }, [fetchTrail]);

  if (loading) {
    return (
      <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-sm p-5 space-y-2 animate-pulse">
        <div className="h-4 w-32 bg-gray-100 rounded" />
        <div className="h-8 bg-gray-100 rounded-lg" />
        <div className="h-8 bg-gray-100 rounded-lg" />
      </div>
    );
  }

  if (entries.length === 0) return null;

  return (
    <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-sm p-5 space-y-3">
      <h2 className="flex items-center gap-2 text-sm font-semibold text-gray-700">
        <History className="w-4 h-4 text-[#3d7ab5]" /> سجل التدقيق
      </h2>
      <ul className="space-y-1.5 max-h-64 overflow-y-auto">
        {entries.map((e) => (
          <li key={e.id} className="flex items-center justify-between gap-3 text-xs text-gray-600 py-1 border-b border-gray-50 last:border-0">
            <span>
              <span className="font-medium text-gray-800">{e.username}</span>
              {" — "}
              {AUDIT_ACTION_LABELS[e.action] ?? e.action}
            </span>
            <span className="text-gray-400 flex-shrink-0">{formatArabicDate(e.createdAt)}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}
