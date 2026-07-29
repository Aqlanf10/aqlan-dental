
import { useEffect, useMemo, useRef, useState } from "react";
import { Download } from "lucide-react";
import {
  buildSuperimpositionExportMetadata,
  registerSuperimpositionRecords,
  type SuperimpositionRecordInput,
} from "@/lib/cephSuperimposition";

interface Props {
  records: SuperimpositionRecordInput[];
  initialReferenceId?: string;
  registerKeys?: [string, string];
}

const SEGMENTS: [string, string][] = [
  ["S", "N"],
  ["N", "A"], ["A", "ANS"], ["ANS", "PNS"],
  ["N", "Pog"],
  ["Co", "Go"], ["Ar", "Go"], ["Go", "Me"], ["Me", "Pog"], ["Pog", "B"],
  ["U1A", "U1T"], ["L1A", "L1T"],
];

function Tracing({
  points,
  color,
  opacity,
}: {
  points: Map<string, { x: number; y: number }>;
  color: string;
  opacity: number;
}) {
  return (
    <g stroke={color} fill={color} opacity={opacity}>
      {SEGMENTS.map(([from, to]) => {
        const first = points.get(from);
        const second = points.get(to);
        if (!first || !second) return null;
        return (
          <line
            key={`${from}-${to}`}
            x1={first.x}
            y1={first.y}
            x2={second.x}
            y2={second.y}
            strokeWidth={1.4}
            strokeLinecap="round"
          />
        );
      })}
      {[...points.entries()].map(([key, point]) => (
        <circle key={key} cx={point.x} cy={point.y} r={2.4} stroke="#fff" strokeWidth={0.7} />
      ))}
    </g>
  );
}

export function CephSuperimposeCanvas({
  records,
  initialReferenceId,
  registerKeys = ["S", "N"],
}: Props) {
  const svgRef = useRef<SVGSVGElement>(null);
  const [referenceId, setReferenceId] = useState(initialReferenceId ?? records[0]?.id ?? "");
  const [opacities, setOpacities] = useState<Record<string, number>>({});

  useEffect(() => {
    if (initialReferenceId) setReferenceId(initialReferenceId);
  }, [initialReferenceId]);

  useEffect(() => {
    if (!records.some(record => record.id === referenceId)) {
      setReferenceId(initialReferenceId ?? records[0]?.id ?? "");
    }
  }, [records, referenceId, initialReferenceId]);

  useEffect(() => {
    setOpacities(current => {
      const next = Object.fromEntries(records.map(record => [record.id, current[record.id] ?? 0.9]));
      const currentKeys = Object.keys(current);
      const nextKeys = Object.keys(next);
      const unchanged = currentKeys.length === nextKeys.length
        && nextKeys.every(key => current[key] === next[key]);
      return unchanged ? current : next;
    });
  }, [records]);

  const result = useMemo(
    () => registerSuperimpositionRecords(records, referenceId, registerKeys),
    [records, referenceId, registerKeys],
  );

  const exportSvg = () => {
    if (!svgRef.current || !result.ok) return;
    const clone = svgRef.current.cloneNode(true) as SVGSVGElement;
    const metadata = document.createElementNS("http://www.w3.org/2000/svg", "metadata");
    metadata.textContent = JSON.stringify({
      ...buildSuperimpositionExportMetadata(records, referenceId, opacities, registerKeys),
      exportedAt: new Date().toISOString(),
    });
    clone.prepend(metadata);
    clone.setAttribute("xmlns", "http://www.w3.org/2000/svg");
    const blob = new Blob(
      [`<?xml version="1.0" encoding="UTF-8"?>\n${new XMLSerializer().serializeToString(clone)}`],
      { type: "image/svg+xml;charset=utf-8" },
    );
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = `ceph-superimposition-${new Date().toISOString().slice(0, 10)}.svg`;
    anchor.click();
    URL.revokeObjectURL(url);
  };

  if (!result.ok) {
    const missingLabels = records
      .filter(record => result.missingRecordIds.includes(record.id))
      .map(record => record.label)
      .join("، ");
    return (
      <div className="rounded-md border border-amber-200 bg-amber-50 p-3 text-xs text-amber-700">
        {result.reason === "registration_landmarks_missing"
          ? `يتطلب التراكب وجود المعلمين ${registerKeys.join(" و ")} في كل سجل. السجلات الناقصة: ${missingLabels}.`
          : "اختر سجلين على الأقل ومرجعاً صالحاً للتراكب."}
      </div>
    );
  }

  const legendFontSize = Math.max(7, result.bounds.width * 0.016);
  const legendLineHeight = legendFontSize * 1.45;
  const legendBandHeight = legendLineHeight * (records.length + 0.5);
  const displayBounds = {
    x: result.bounds.x,
    y: result.bounds.y - legendBandHeight,
    width: result.bounds.width,
    height: result.bounds.height + legendBandHeight,
  };
  const displayViewBox = `${displayBounds.x} ${displayBounds.y} ${displayBounds.width} ${displayBounds.height}`;

  return (
    <div className="space-y-3">
      <div className="grid gap-x-5 gap-y-2 border-b border-gray-100 pb-3 md:grid-cols-2">
        {records.map(record => (
          <div key={record.id} className="grid min-w-0 grid-cols-[auto_minmax(0,1fr)_7rem] items-center gap-2">
            <input
              type="radio"
              name="ceph-superimposition-reference"
              checked={referenceId === record.id}
              onChange={() => setReferenceId(record.id)}
              aria-label={`اختيار ${record.label} كمرجع`}
              className="accent-clinic-blue"
            />
            <span className="flex min-w-0 items-center gap-2 text-xs">
              <span className="h-0.5 w-4 shrink-0" style={{ backgroundColor: record.color }} />
              <span className="truncate font-medium text-gray-700">{record.label}</span>
              <span className="shrink-0 text-[10px] text-gray-400">{record.date}</span>
            </span>
            <input
              type="range"
              min={0.15}
              max={1}
              step={0.05}
              value={opacities[record.id] ?? 0.9}
              onChange={event => setOpacities(current => ({
                ...current,
                [record.id]: Number(event.target.value),
              }))}
              aria-label={`شفافية ${record.label}`}
              className="w-full accent-clinic-blue"
            />
          </div>
        ))}
      </div>

      <div className="flex items-center justify-between gap-3 text-[10px] text-gray-500">
        <span>المرجع: {records.find(record => record.id === referenceId)?.label} · التسجيل على {registerKeys.join("–")}</span>
        <button
          type="button"
          onClick={exportSvg}
          className="no-print inline-flex items-center gap-1.5 rounded-md border border-gray-200 px-2.5 py-1.5 font-medium text-gray-700 hover:bg-gray-50"
        >
          <Download className="h-3.5 w-3.5" />
          تصدير SVG
        </button>
      </div>

      <svg
        ref={svgRef}
        viewBox={displayViewBox}
        className="aspect-[4/3] w-full rounded-md border border-gray-200 bg-white"
        preserveAspectRatio="xMidYMid meet"
        role="img"
        aria-label={`تراكب سيفالومتري من ${records.length} سجلات على المرجع ${records.find(record => record.id === referenceId)?.label}`}
      >
        <rect
          x={displayBounds.x}
          y={displayBounds.y}
          width={displayBounds.width}
          height={displayBounds.height}
          fill="#fff"
        />
        {result.records.map(record => (
          <Tracing
            key={record.id}
            points={record.points}
            color={record.color}
            opacity={opacities[record.id] ?? 0.9}
          />
        ))}
        <g aria-label="وسيلة إيضاح السجلات">
          {result.records.map((record, index) => {
            const y = displayBounds.y + legendLineHeight * (index + 1);
            return (
              <g key={record.id}>
                <line
                  x1={result.bounds.x + legendFontSize}
                  y1={y}
                  x2={result.bounds.x + legendFontSize * 3}
                  y2={y}
                  stroke={record.color}
                  strokeWidth={Math.max(1.5, legendFontSize * 0.18)}
                  opacity={opacities[record.id] ?? 0.9}
                />
                <text
                  x={result.bounds.x + legendFontSize * 3.6}
                  y={y + legendFontSize * 0.3}
                  fontSize={legendFontSize}
                  fill="#374151"
                >
                  {record.label} · {record.date}{record.id === referenceId ? " · مرجع" : ""}
                </text>
              </g>
            );
          })}
        </g>
      </svg>
    </div>
  );
}
