import { applySimilarity, IDENTITY_SIMILARITY, similarityFromPairs } from "@/lib/cephMath";

export interface SuperimpositionLandmark {
  key: string;
  x: number;
  y: number;
}

export interface SuperimpositionRecordInput {
  id: string;
  label: string;
  date: string;
  color: string;
  landmarks: SuperimpositionLandmark[];
}

export interface RegisteredSuperimpositionRecord extends Omit<SuperimpositionRecordInput, "landmarks"> {
  points: Map<string, { x: number; y: number }>;
}

export type SuperimpositionRegistrationResult =
  | {
      ok: true;
      referenceId: string;
      registerKeys: [string, string];
      records: RegisteredSuperimpositionRecord[];
      viewBox: string;
      bounds: { x: number; y: number; width: number; height: number };
    }
  | {
      ok: false;
      reason: "too_few_records" | "reference_missing" | "registration_landmarks_missing";
      missingRecordIds: string[];
    };

function landmarkMap(landmarks: SuperimpositionLandmark[]) {
  return new Map(landmarks.map(landmark => [landmark.key, landmark]));
}

export function registerSuperimpositionRecords(
  records: SuperimpositionRecordInput[],
  referenceId: string,
  registerKeys: [string, string] = ["S", "N"],
): SuperimpositionRegistrationResult {
  if (records.length < 2) {
    return { ok: false, reason: "too_few_records", missingRecordIds: [] };
  }

  const reference = records.find(record => record.id === referenceId);
  if (!reference) {
    return { ok: false, reason: "reference_missing", missingRecordIds: [referenceId] };
  }

  const [firstKey, secondKey] = registerKeys;
  const maps = new Map(records.map(record => [record.id, landmarkMap(record.landmarks)]));
  const missingRecordIds = records
    .filter(record => !maps.get(record.id)?.has(firstKey) || !maps.get(record.id)?.has(secondKey))
    .map(record => record.id);
  if (missingRecordIds.length > 0) {
    return { ok: false, reason: "registration_landmarks_missing", missingRecordIds };
  }

  const referenceMap = maps.get(reference.id)!;
  const referenceFirst = referenceMap.get(firstKey)!;
  const referenceSecond = referenceMap.get(secondKey)!;
  const registered = records.map(record => {
    const points = maps.get(record.id)!;
    const transform = record.id === reference.id
      ? IDENTITY_SIMILARITY
      : similarityFromPairs(
          points.get(firstKey)!,
          points.get(secondKey)!,
          referenceFirst,
          referenceSecond,
        );
    return {
      id: record.id,
      label: record.label,
      date: record.date,
      color: record.color,
      points: new Map([...points].map(([key, point]) => [key, applySimilarity(transform, point)])),
    };
  });

  const allPoints = registered.flatMap(record => [...record.points.values()]);
  let minX = Infinity;
  let minY = Infinity;
  let maxX = -Infinity;
  let maxY = -Infinity;
  for (const point of allPoints) {
    minX = Math.min(minX, point.x);
    minY = Math.min(minY, point.y);
    maxX = Math.max(maxX, point.x);
    maxY = Math.max(maxY, point.y);
  }
  const width = Math.max(1, maxX - minX);
  const height = Math.max(1, maxY - minY);
  const padding = Math.max(20, (width + height) * 0.06);

  const bounds = {
    x: minX - padding,
    y: minY - padding,
    width: width + 2 * padding,
    height: height + 2 * padding,
  };
  return {
    ok: true,
    referenceId,
    registerKeys,
    records: registered,
    viewBox: `${bounds.x} ${bounds.y} ${bounds.width} ${bounds.height}`,
    bounds,
  };
}

export function buildSuperimpositionExportMetadata(
  records: SuperimpositionRecordInput[],
  referenceId: string,
  opacities: Record<string, number>,
  registerKeys: [string, string] = ["S", "N"],
) {
  return {
    kind: "aqlan-ceph-superimposition",
    version: 1,
    referenceId,
    registerKeys,
    records: records.map(record => ({
      id: record.id,
      label: record.label,
      date: record.date,
      color: record.color,
      opacity: opacities[record.id] ?? 0.9,
    })),
  };
}

export function hasMixedOrthoCases(records: Array<{ orthoCaseId: string }>): boolean {
  return new Set(records.map(record => record.orthoCaseId)).size > 1;
}
