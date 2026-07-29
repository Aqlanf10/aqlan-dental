export interface StoredPhotoMeasurement {
  key: string;
  nameAr: string;
  value: number | null;
  normal?: number;
  sd?: number;
  severity?: string;
  interpretationAr?: string;
}

export interface PhotoMeasurementComparison {
  key: string;
  nameAr: string;
  baseValue: number | null;
  targetValue: number | null;
  delta: number | null;
}

export interface DatedPhotoRecord {
  id: string;
  analysisDate: string;
}

export function selectChronologicalPhotoPair(
  records: DatedPhotoRecord[],
  requestedBaseId: string,
  requestedTargetId: string,
): { baseId: string; targetId: string } {
  if (records.length < 2) return { baseId: records[0]?.id ?? "", targetId: "" };
  const ordered = [...records].sort((a, b) => a.analysisDate.localeCompare(b.analysisDate));
  let baseIndex = ordered.findIndex(item => item.id === requestedBaseId);
  let targetIndex = ordered.findIndex(item => item.id === requestedTargetId);
  if (baseIndex < 0) baseIndex = 0;
  if (targetIndex < 0) targetIndex = ordered.length - 1;
  if (baseIndex >= targetIndex) {
    baseIndex = 0;
    targetIndex = ordered.length - 1;
  }
  return { baseId: ordered[baseIndex].id, targetId: ordered[targetIndex].id };
}

export function parseStoredPhotoMeasurements(json?: string | null): StoredPhotoMeasurement[] {
  if (!json) return [];
  try {
    const value: unknown = JSON.parse(json);
    if (!Array.isArray(value)) return [];

    const seen = new Set<string>();
    return value.flatMap(item => {
      if (!item || typeof item !== "object") return [];
      const record = item as Record<string, unknown>;
      if (typeof record.key !== "string" || !record.key.trim() || seen.has(record.key)) return [];
      if (typeof record.nameAr !== "string" || !record.nameAr.trim()) return [];
      const measurementValue = record.value;
      if (measurementValue !== null && (typeof measurementValue !== "number" || !Number.isFinite(measurementValue))) return [];
      seen.add(record.key);
      return [{
        key: record.key,
        nameAr: record.nameAr,
        value: measurementValue as number | null,
        normal: finiteNumber(record.normal),
        sd: finiteNumber(record.sd),
        severity: typeof record.severity === "string" ? record.severity : undefined,
        interpretationAr: typeof record.interpretationAr === "string" ? record.interpretationAr : undefined,
      }];
    });
  } catch {
    return [];
  }
}

export function compareStoredPhotoMeasurements(
  base: StoredPhotoMeasurement[],
  target: StoredPhotoMeasurement[],
): PhotoMeasurementComparison[] {
  const baseByKey = new Map(base.map(item => [item.key, item]));
  const targetByKey = new Map(target.map(item => [item.key, item]));
  const orderedKeys = [...new Set([...base.map(item => item.key), ...target.map(item => item.key)])];

  return orderedKeys.map(key => {
    const before = baseByKey.get(key);
    const after = targetByKey.get(key);
    const baseValue = before?.value ?? null;
    const targetValue = after?.value ?? null;
    return {
      key,
      nameAr: after?.nameAr ?? before?.nameAr ?? key,
      baseValue,
      targetValue,
      delta: baseValue !== null && targetValue !== null
        ? Number((targetValue - baseValue).toFixed(2))
        : null,
    };
  });
}

function finiteNumber(value: unknown): number | undefined {
  return typeof value === "number" && Number.isFinite(value) ? value : undefined;
}
